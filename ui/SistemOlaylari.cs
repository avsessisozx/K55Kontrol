using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace K55Kontrol
{
    // Gizli bir pencere üzerinden Windows olaylarını dinler: genel kısayollar, oturum kilidi ve monitörün açılıp kapanması.
    public sealed class SistemOlaylari : IDisposable
    {
        public event Action<bool> KilitDegisti;     // true = kilitlendi
        public event Action<bool> EkranDegisti;     // true = açık
        public event Action<Eylem> KisayolBasildi;

        private readonly HwndSource pencere;
        private readonly IntPtr gucBildirimi;
        private readonly Dictionary<int, Eylem> kayitliKisayollar = new Dictionary<int, Eylem>();

        private const int WM_HOTKEY = 0x0312;
        private const int WM_WTSSESSION_CHANGE = 0x02B1;
        private const int WM_POWERBROADCAST = 0x0218;
        private const int PBT_POWERSETTINGCHANGE = 0x8013;
        private const int WTS_SESSION_LOCK = 7, WTS_SESSION_UNLOCK = 8;
        private const int MOD_NOREPEAT = 0x4000;
        private static readonly Guid EkranDurumuGuid = new Guid("6fe69556-704a-47a0-8f24-c28d936fda47");   // GUID_CONSOLE_DISPLAY_STATE

        public SistemOlaylari()
        {
            // Görünmeyen, normal bir üst düzey pencere (yalnızca ileti almak için).
            pencere = new HwndSource(new HwndSourceParameters("K55Kontrol.Olaylar") { WindowStyle = 0, Width = 0, Height = 0 });
            pencere.AddHook(IletiIsle);
            WTSRegisterSessionNotification(pencere.Handle, 0);
            Guid g = EkranDurumuGuid;
            gucBildirimi = RegisterPowerSettingNotification(pencere.Handle, ref g, 0);
        }

        // Etkin kısayolları yeniden kaydeder; başka bir programın kullandığı (kaydedilemeyen) eylemleri döndürür.
        public HashSet<Eylem> KisayollariKaydet(IEnumerable<Kisayol> kisayollar, bool etkin)
        {
            KisayollariBirak();
            HashSet<Eylem> basarisiz = new HashSet<Eylem>();
            if (!etkin) return basarisiz;
            int kimlik = 1;
            foreach (Kisayol k in kisayollar)
            {
                if (k.Tus == 0) continue;
                if (RegisterHotKey(pencere.Handle, kimlik, (uint)(k.Degistirici | MOD_NOREPEAT), (uint)k.Tus))
                    kayitliKisayollar[kimlik] = k.Eylem;
                else
                    basarisiz.Add(k.Eylem);
                kimlik++;
            }
            return basarisiz;
        }

        public void KisayollariBirak()
        {
            foreach (int kimlik in kayitliKisayollar.Keys)
                UnregisterHotKey(pencere.Handle, kimlik);
            kayitliKisayollar.Clear();
        }

        // Klavye ya da fareye en son dokunulmasından bu yana geçen süre.
        public static TimeSpan BostaSure()
        {
            LASTINPUTINFO bilgi = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO)) };
            if (!GetLastInputInfo(ref bilgi)) return TimeSpan.Zero;
            return TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - bilgi.dwTime));
        }

        private IntPtr IletiIsle(IntPtr hwnd, int ileti, IntPtr wParam, IntPtr lParam, ref bool islendi)
        {
            switch (ileti)
            {
                case WM_HOTKEY:
                    Eylem e;
                    if (kayitliKisayollar.TryGetValue(wParam.ToInt32(), out e) && KisayolBasildi != null)
                        KisayolBasildi(e);
                    break;
                case WM_WTSSESSION_CHANGE:
                    int olay = wParam.ToInt32();
                    if ((olay == WTS_SESSION_LOCK || olay == WTS_SESSION_UNLOCK) && KilitDegisti != null)
                        KilitDegisti(olay == WTS_SESSION_LOCK);
                    break;
                case WM_POWERBROADCAST:
                    if (wParam.ToInt32() == PBT_POWERSETTINGCHANGE)
                    {
                        POWERBROADCAST_SETTING ayar = (POWERBROADCAST_SETTING)Marshal.PtrToStructure(lParam, typeof(POWERBROADCAST_SETTING));
                        if (ayar.PowerSetting == EkranDurumuGuid && EkranDegisti != null)
                            EkranDegisti(ayar.Data != 0);   // 0 = kapalı, 1 = açık, 2 = kısık
                    }
                    break;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            KisayollariBirak();
            WTSUnRegisterSessionNotification(pencere.Handle);
            if (gucBildirimi != IntPtr.Zero) UnregisterPowerSettingNotification(gucBildirimi);
            pencere.Dispose();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct POWERBROADCAST_SETTING { public Guid PowerSetting; public uint DataLength; public byte Data; }

        [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint mods, uint vk);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LASTINPUTINFO bilgi);
        [DllImport("user32.dll")] private static extern IntPtr RegisterPowerSettingNotification(IntPtr hwnd, ref Guid g, int bayrak);
        [DllImport("user32.dll")] private static extern bool UnregisterPowerSettingNotification(IntPtr h);
        [DllImport("wtsapi32.dll")] private static extern bool WTSRegisterSessionNotification(IntPtr hwnd, int bayrak);
        [DllImport("wtsapi32.dll")] private static extern bool WTSUnRegisterSessionNotification(IntPtr hwnd);
    }
}
