using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace K55Kontrol
{
    // "Sistem göstergesi" efektinin değer kaynakları: CPU ve RAM kullanımı, NVIDIA GPU sıcaklığı
    // ve (HWiNFO paylaşılan belleği açıksa) HWiNFO'daki tüm sıcaklıklar. Değerler saniyede bir okunur.
    public sealed class SistemGostergesi : IDisposable
    {
        public sealed class Kaynak
        {
            public string Anahtar, Ad, Birim;
            public double VarsayilanAlt, VarsayilanUst;
            public bool AralikDuzenlenebilir;
        }

        public const string Cpu = "cpu", Ram = "ram", GpuSicaklik = "gpu-sicaklik";

        private Timer zamanlayici;
        private readonly object kilit = new object();
        private Dictionary<string, double> degerler = new Dictionary<string, double>();
        private long oncekiBos, oncekiToplam;

        public bool Calisiyor => zamanlayici != null;

        public void Baslat()
        {
            if (zamanlayici != null) return;
            zamanlayici = new Timer(delegate { try { Oku(); } catch (Exception) { } }, null, 0, 1000);
        }

        public void Durdur()
        {
            if (zamanlayici == null) return;
            zamanlayici.Dispose();
            zamanlayici = null;
        }

        public void Dispose() => Durdur();

        public double? Deger(string anahtar)
        {
            lock (kilit)
            {
                double d;
                return anahtar != null && degerler.TryGetValue(anahtar, out d) ? d : (double?)null;
            }
        }

        // Arayüzde seçilebilecek kaynaklar (o an erişilebilir olanlar).
        public static List<Kaynak> Kaynaklar()
        {
            var l = new List<Kaynak>
            {
                new Kaynak { Anahtar = Cpu, Ad = "İşlemci (CPU) kullanımı", Birim = "%", VarsayilanAlt = 0, VarsayilanUst = 100 },
                new Kaynak { Anahtar = Ram, Ad = "Bellek (RAM) kullanımı", Birim = "%", VarsayilanAlt = 0, VarsayilanUst = 100 },
            };
            if (Nvidia.Sicaklik().HasValue)
                l.Add(new Kaynak { Anahtar = GpuSicaklik, Ad = "Ekran kartı (GPU) sıcaklığı", Birim = "°C", VarsayilanAlt = 35, VarsayilanUst = 85, AralikDuzenlenebilir = true });
            foreach (var h in HwInfo.Okumalar())
                if (h.Tur == 1)
                    l.Add(new Kaynak { Anahtar = h.Anahtar, Ad = "HWiNFO: " + h.Sensor + " – " + h.Etiket, Birim = "°C", VarsayilanAlt = 35, VarsayilanUst = 90, AralikDuzenlenebilir = true });
            return l;
        }

        private void Oku()
        {
            var yeni = new Dictionary<string, double>();

            long bos, cekirdek, kullanici;
            if (GetSystemTimes(out bos, out cekirdek, out kullanici))
            {
                long toplam = cekirdek + kullanici;   // çekirdek süresi boşta süresini de içerir
                long dBos = bos - oncekiBos, dToplam = toplam - oncekiToplam;
                if (oncekiToplam != 0 && dToplam > 0) yeni[Cpu] = 100.0 * (dToplam - dBos) / dToplam;
                oncekiBos = bos;
                oncekiToplam = toplam;
            }

            MEMORYSTATUSEX bellek = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)) };
            if (GlobalMemoryStatusEx(ref bellek)) yeni[Ram] = bellek.dwMemoryLoad;

            int? gpu = Nvidia.Sicaklik();
            if (gpu.HasValue) yeni[GpuSicaklik] = gpu.Value;

            foreach (var h in HwInfo.Okumalar()) yeni[h.Anahtar] = h.Deger;

            lock (kilit)
            {
                // CPU ilk turda hesaplanamaz; önceki değeri koru.
                double eski;
                if (!yeni.ContainsKey(Cpu) && degerler.TryGetValue(Cpu, out eski)) yeni[Cpu] = eski;
                degerler = yeni;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength, dwMemoryLoad;
            public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll")] private static extern bool GetSystemTimes(out long bos, out long cekirdek, out long kullanici);
        [DllImport("kernel32.dll")] private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX m);

        // ---- NVIDIA sürücüsünün NVAPI arayüzü (yönetici yetkisi gerektirmez) ----
        private static class Nvidia
        {
            private static bool hazir, denendi;
            private static readonly object kilit = new object();
            private static EnumGpus gpulariListele;
            private static IsilAyarlar isilAyarlar;

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Baslat();
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int EnumGpus([Out] IntPtr[] gpular, out int sayi);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int IsilAyarlar(IntPtr gpu, int sensor, ref NV_GPU_THERMAL_SETTINGS ayar);

            [DllImport("nvapi64.dll", EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
            private static extern IntPtr QueryInterface(uint kimlik);

            [StructLayout(LayoutKind.Sequential)]
            private struct Sensor { public int Denetleyici, VarsayilanMin, VarsayilanMax, Sicaklik, Hedef; }

            [StructLayout(LayoutKind.Sequential)]
            private struct NV_GPU_THERMAL_SETTINGS
            {
                public uint Surum, Sayi;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public Sensor[] Sensorler;
            }

            private static bool Hazirla()
            {
                lock (kilit)
                {
                    if (denendi) return hazir;
                    denendi = true;
                    try
                    {
                        var baslat = Al<Baslat>(0x0150E828);
                        gpulariListele = Al<EnumGpus>(0xE5AC921F);
                        isilAyarlar = Al<IsilAyarlar>(0xE3640A56);
                        hazir = baslat != null && gpulariListele != null && isilAyarlar != null && baslat() == 0;
                    }
                    catch (Exception) { hazir = false; }
                    return hazir;
                }
            }

            private static T Al<T>(uint kimlik) where T : class
            {
                IntPtr p = QueryInterface(kimlik);
                return p == IntPtr.Zero ? null : (T)(object)Marshal.GetDelegateForFunctionPointer(p, typeof(T));
            }

            public static int? Sicaklik()
            {
                if (!Hazirla()) return null;
                try
                {
                    IntPtr[] gpular = new IntPtr[64];
                    int sayi;
                    if (gpulariListele(gpular, out sayi) != 0 || sayi < 1) return null;
                    var ayar = new NV_GPU_THERMAL_SETTINGS
                    {
                        Surum = (uint)Marshal.SizeOf(typeof(NV_GPU_THERMAL_SETTINGS)) | (2u << 16),
                        Sensorler = new Sensor[3]
                    };
                    if (isilAyarlar(gpular[0], 15 /* tüm sensörler */, ref ayar) != 0 || ayar.Sayi < 1) return null;
                    return ayar.Sensorler[0].Sicaklik;
                }
                catch (Exception) { return null; }
            }
        }

        // ---- HWiNFO paylaşılan belleği ("Shared Memory Support" açık olmalı) ----
        private static class HwInfo
        {
            public sealed class Okuma
            {
                public string Anahtar, Sensor, Etiket;
                public int Tur;
                public double Deger;
            }

            public static List<Okuma> Okumalar()
            {
                var l = new List<Okuma>();
                try
                {
                    using (var mmf = MemoryMappedFile.OpenExisting("Global\\HWiNFO_SENS_SM2", MemoryMappedFileRights.Read))
                    using (var g = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read))
                    {
                        if (g.ReadUInt32(0) != 0x53695748) return l;   // "HWiS"
                        uint sensorOfs = g.ReadUInt32(20), sensorBoy = g.ReadUInt32(24), sensorSayi = g.ReadUInt32(28);
                        uint okumaOfs = g.ReadUInt32(32), okumaBoy = g.ReadUInt32(36), okumaSayi = g.ReadUInt32(40);

                        var sensorAdlari = new string[sensorSayi];
                        for (uint i = 0; i < sensorSayi; i++)
                            sensorAdlari[i] = Metin(g, sensorOfs + i * sensorBoy + 8 + 128, 128);

                        for (uint i = 0; i < okumaSayi; i++)
                        {
                            long o = okumaOfs + i * okumaBoy;
                            uint tur = g.ReadUInt32(o), sensor = g.ReadUInt32(o + 4), kimlik = g.ReadUInt32(o + 8);
                            l.Add(new Okuma
                            {
                                Tur = (int)tur,
                                Anahtar = "hwinfo:" + sensor + ":" + kimlik,
                                Sensor = sensor < sensorSayi ? sensorAdlari[sensor] : "?",
                                Etiket = Metin(g, o + 12 + 128, 128),
                                Deger = g.ReadDouble(o + 12 + 128 + 128 + 16)
                            });
                        }
                    }
                }
                catch (Exception) { }
                return l;
            }

            private static string Metin(MemoryMappedViewAccessor g, long ofs, int boy)
            {
                byte[] b = new byte[boy];
                g.ReadArray(ofs, b, 0, boy);
                int son = Array.IndexOf(b, (byte)0);
                return Encoding.Default.GetString(b, 0, son < 0 ? boy : son).Trim();
            }
        }
    }
}
