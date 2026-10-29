using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace K55Kontrol
{
    // Windows HID API üzerinden ham rapor okuma/yazma (harici kütüphane gerektirmez).
    internal sealed class HidCihaz : IDisposable
    {
        public string Yol { get; private set; }
        public int CikisRaporBoyu { get; private set; }

        private SafeFileHandle tutamac;
        private FileStream akis;
        private Thread okuyucu;
        private volatile bool calisiyor;
        private readonly BlockingCollection<byte[]> gelenler = new BlockingCollection<byte[]>(64);

        private HidCihaz() { }

        public ushort UsagePage { get; private set; }
        public ushort Usage { get; private set; }

        // VID/PID ve arayüz numarasına (MI_xx) uyan, istenen usage page'e sahip ilk HID yolunu açar.
        public static HidCihaz Ac(ushort vid, ushort pid, int arayuz, ushort usagePage)
        {
            string aranan = string.Format("vid_{0:x4}&pid_{1:x4}&mi_{2:x2}", vid, pid, arayuz);
            foreach (string yol in HidYollari())
            {
                if (yol.ToLowerInvariant().IndexOf(aranan, StringComparison.Ordinal) < 0)
                    continue;
                HidCihaz c = YolAc(yol);
                if (c == null) continue;
                if (c.UsagePage == usagePage) return c;
                c.Dispose();
            }
            return null;
        }

        // VID/PID'e ait tüm HID arayüz yolları.
        public static List<string> Yollar(ushort vid, ushort pid)
        {
            string aranan = string.Format("vid_{0:x4}&pid_{1:x4}", vid, pid);
            return HidYollari().FindAll(y => y.ToLowerInvariant().IndexOf(aranan, StringComparison.Ordinal) >= 0);
        }

        // Belirli bir HID yolunu okuma (mümkünse yazma) için açar; açılamazsa null.
        public static HidCihaz YolAc(string yol)
        {
            bool yazilabilir = true;
            SafeFileHandle h = Yerel.CreateFile(yol, Yerel.GENERIC_READ | Yerel.GENERIC_WRITE,
                Yerel.FILE_SHARE_READ | Yerel.FILE_SHARE_WRITE, IntPtr.Zero, Yerel.OPEN_EXISTING,
                Yerel.FILE_FLAG_OVERLAPPED, IntPtr.Zero);
            if (h.IsInvalid)
            {
                yazilabilir = false;
                h = Yerel.CreateFile(yol, Yerel.GENERIC_READ, Yerel.FILE_SHARE_READ | Yerel.FILE_SHARE_WRITE,
                    IntPtr.Zero, Yerel.OPEN_EXISTING, Yerel.FILE_FLAG_OVERLAPPED, IntPtr.Zero);
                if (h.IsInvalid) return null;
            }

            Yerel.HIDP_CAPS caps;
            if (!Yetenekler(h, out caps))
            {
                h.Dispose();
                return null;
            }

            HidCihaz c = new HidCihaz();
            c.Yol = yol;
            c.UsagePage = caps.UsagePage;
            c.Usage = caps.Usage;
            c.CikisRaporBoyu = caps.OutputReportByteLength;
            c.tutamac = h;
            c.akis = new FileStream(h, yazilabilir ? FileAccess.ReadWrite : FileAccess.Read,
                Math.Max((int)caps.InputReportByteLength, 1), true);
            if (caps.InputReportByteLength > 0) c.OkuyucuyuBaslat(caps.InputReportByteLength);
            return c;
        }

        public void Yaz(byte[] rapor)
        {
            akis.Write(rapor, 0, rapor.Length);
            akis.Flush();
        }

        // Zaman aşımına kadar gelen bir giriş raporunu bekler; yoksa null döner.
        public byte[] Oku(int zamanAsimiMs)
        {
            byte[] r;
            return gelenler.TryTake(out r, zamanAsimiMs) ? r : null;
        }

        public void GelenleriTemizle()
        {
            byte[] r;
            while (gelenler.TryTake(out r)) { }
        }

        public bool Bagli { get { return calisiyor; } }

        private void OkuyucuyuBaslat(int boy)
        {
            calisiyor = true;
            okuyucu = new Thread(delegate ()
            {
                byte[] tampon = new byte[boy];
                try
                {
                    while (calisiyor)
                    {
                        int n = akis.Read(tampon, 0, tampon.Length);
                        if (n <= 0) break;
                        byte[] kopya = new byte[n];
                        Buffer.BlockCopy(tampon, 0, kopya, 0, n);
                        if (!gelenler.TryAdd(kopya))
                        {
                            byte[] at;
                            gelenler.TryTake(out at);
                            gelenler.TryAdd(kopya);
                        }
                    }
                }
                catch (Exception) { }
                calisiyor = false;
            });
            okuyucu.IsBackground = true;
            okuyucu.Name = "HID okuyucu";
            okuyucu.Start();
        }

        public void Dispose()
        {
            calisiyor = false;
            try { if (akis != null) akis.Dispose(); } catch (Exception) { }
            try { if (tutamac != null) tutamac.Dispose(); } catch (Exception) { }
        }

        private static bool Yetenekler(SafeFileHandle h, out Yerel.HIDP_CAPS caps)
        {
            caps = new Yerel.HIDP_CAPS();
            IntPtr veri;
            if (!Yerel.HidD_GetPreparsedData(h, out veri))
                return false;
            try { return Yerel.HidP_GetCaps(veri, out caps) == Yerel.HIDP_STATUS_SUCCESS; }
            finally { Yerel.HidD_FreePreparsedData(veri); }
        }

        private static List<string> HidYollari()
        {
            List<string> sonuc = new List<string>();
            Guid hidGuid;
            Yerel.HidD_GetHidGuid(out hidGuid);
            IntPtr set = Yerel.SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero,
                Yerel.DIGCF_PRESENT | Yerel.DIGCF_DEVICEINTERFACE);
            if (set == new IntPtr(-1))
                return sonuc;
            try
            {
                for (uint i = 0; ; i++)
                {
                    Yerel.SP_DEVICE_INTERFACE_DATA arayuz = new Yerel.SP_DEVICE_INTERFACE_DATA();
                    arayuz.cbSize = Marshal.SizeOf(arayuz);
                    if (!Yerel.SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref hidGuid, i, ref arayuz))
                        break;

                    int gerekli;
                    Yerel.SetupDiGetDeviceInterfaceDetail(set, ref arayuz, IntPtr.Zero, 0, out gerekli, IntPtr.Zero);
                    IntPtr detay = Marshal.AllocHGlobal(gerekli);
                    try
                    {
                        // SP_DEVICE_INTERFACE_DETAIL_DATA_W.cbSize: x64'te 8, x86'da 6
                        Marshal.WriteInt32(detay, IntPtr.Size == 8 ? 8 : 6);
                        if (Yerel.SetupDiGetDeviceInterfaceDetail(set, ref arayuz, detay, gerekli, out gerekli, IntPtr.Zero))
                            sonuc.Add(Marshal.PtrToStringUni(new IntPtr(detay.ToInt64() + 4)));
                    }
                    finally { Marshal.FreeHGlobal(detay); }
                }
            }
            finally { Yerel.SetupDiDestroyDeviceInfoList(set); }
            return sonuc;
        }
    }

    internal static class Yerel
    {
        public const uint GENERIC_READ = 0x80000000, GENERIC_WRITE = 0x40000000;
        public const uint FILE_SHARE_READ = 1, FILE_SHARE_WRITE = 2, OPEN_EXISTING = 3;
        public const uint FILE_FLAG_OVERLAPPED = 0x40000000;
        public const int DIGCF_PRESENT = 0x2, DIGCF_DEVICEINTERFACE = 0x10;
        public const int HIDP_STATUS_SUCCESS = 0x00110000;

        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;
            public Guid InterfaceClassGuid;
            public int Flags;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HIDP_CAPS
        {
            public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes, NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices,
                NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices,
                NumberFeatureButtonCaps, NumberFeatureValueCaps, NumberFeatureDataIndices;
        }

        [DllImport("hid.dll")] public static extern void HidD_GetHidGuid(out Guid g);
        [DllImport("hid.dll")] public static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr data);
        [DllImport("hid.dll")] public static extern bool HidD_FreePreparsedData(IntPtr data);
        [DllImport("hid.dll")] public static extern int HidP_GetCaps(IntPtr data, out HIDP_CAPS caps);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SetupDiGetClassDevs(ref Guid g, IntPtr enumerator, IntPtr parent, int flags);
        [DllImport("setupapi.dll")]
        public static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr devInfo, ref Guid g, uint index, ref SP_DEVICE_INTERFACE_DATA data);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode)]
        public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref SP_DEVICE_INTERFACE_DATA data, IntPtr detail, int size, out int required, IntPtr devInfo);
        [DllImport("setupapi.dll")] public static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr sec, uint disp, uint flags, IntPtr template);
    }
}
