using System;
using System.Threading;

namespace K55Kontrol
{
    // K55 RGB PRO'nun G1–G6 makro tuşlarını dinler. Bu tuşlar Windows'a tuş basışı göndermez; klavyenin
    // üçüncü HID arayüzünden (MI_02, usage page 0xFF42) "basılı tuşlar" bit haritası içeren raporlar gelir:
    //   rapor[2] = 0x02, G1–G5 = rapor[19] bitleri 0x08..0x80, G6 = rapor[20] biti 0x01.
    // Olaylar arka plan iş parçacığından gelir.
    public sealed class GTuslari : IDisposable
    {
        public const int TusSayisi = 6;

        public event Action<int> Basildi;      // 1-6
        public event Action<int> Birakildi;    // 1-6

        private const int Arayuz = 2;
        private const ushort UsagePage = 0xFF42;
        private static readonly int[] Bayt = { 19, 19, 19, 19, 19, 20 };
        private static readonly byte[] Bit = { 0x08, 0x10, 0x20, 0x40, 0x80, 0x01 };

        private readonly Thread isParcacigi;
        private volatile bool calisiyor = true;
        private HidCihaz cihaz;

        public GTuslari()
        {
            isParcacigi = new Thread(Dongu) { IsBackground = true, Name = "G tuşları" };
            isParcacigi.Start();
        }

        public bool Bagli { get { HidCihaz c = cihaz; return c != null && c.Bagli; } }

        private void Dongu()
        {
            bool[] basili = new bool[TusSayisi];
            while (calisiyor)
            {
                if (!Bagli)
                {
                    Kapat();
                    try { cihaz = HidCihaz.Ac(K55.VID, K55.PID, Arayuz, UsagePage); } catch (Exception) { cihaz = null; }
                    if (cihaz == null) { Thread.Sleep(2000); continue; }
                }

                byte[] r = cihaz.Oku(500);
                if (r == null || r.Length <= 20 || r[2] != 0x02) continue;

                for (int i = 0; i < TusSayisi; i++)
                {
                    bool simdi = (r[Bayt[i]] & Bit[i]) != 0;
                    if (simdi == basili[i]) continue;
                    basili[i] = simdi;
                    Action<int> olay = simdi ? Basildi : Birakildi;
                    try { if (olay != null) olay(i + 1); } catch (Exception) { }
                }
            }
            Kapat();
        }

        private void Kapat()
        {
            HidCihaz c = cihaz;
            cihaz = null;
            if (c != null) c.Dispose();
        }

        public void Dispose()
        {
            calisiyor = false;
            isParcacigi.Join(1500);
        }
    }
}
