using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace K55Kontrol
{
    // "Tuşa tepki" efekti için klavye basışlarını dinler. Yalnızca basılan tuşun fiziksel konumu (tarama kodu)
    // bölgeye çevrilir; hangi tuşa basıldığı saklanmaz. Kanca, ileti döngüsü olan iş parçacığında (arayüz) kurulmalıdır.
    public sealed class TusDinleyici : IDisposable
    {
        private readonly long[] sonBasis = new long[K55.BolgeSayisi];
        private readonly Stopwatch saat = Stopwatch.StartNew();
        private IntPtr kanca;
        private KancaYordami yordam;     // çöp toplayıcı silmesin diye alanda tutulur

        public bool Calisiyor => kanca != IntPtr.Zero;

        // Her bölge için son basıştan bu yana geçen süre (saniye); hiç basılmadıysa çok büyük bir değer.
        public double[] GecenSureler()
        {
            double simdi = saat.Elapsed.TotalSeconds;
            double[] r = new double[K55.BolgeSayisi];
            for (int i = 0; i < r.Length; i++)
            {
                long t = System.Threading.Interlocked.Read(ref sonBasis[i]);
                r[i] = t == 0 ? 1e9 : simdi - t / 1000.0;
            }
            return r;
        }

        public void Baslat()
        {
            if (kanca != IntPtr.Zero) return;
            yordam = KancaIsle;
            kanca = SetWindowsHookEx(13 /* WH_KEYBOARD_LL */, yordam, GetModuleHandle(null), 0);
        }

        public void Durdur()
        {
            if (kanca == IntPtr.Zero) return;
            UnhookWindowsHookEx(kanca);
            kanca = IntPtr.Zero;
        }

        public void Dispose() => Durdur();

        private IntPtr KancaIsle(int kod, IntPtr wParam, IntPtr lParam)
        {
            int ileti = wParam.ToInt32();
            if (kod >= 0 && (ileti == 0x100 || ileti == 0x104))   // WM_KEYDOWN, WM_SYSKEYDOWN
            {
                int tarama = Marshal.ReadInt32(lParam, 4);
                int bayrak = Marshal.ReadInt32(lParam, 8);
                int bolge = TusHaritasi.Bolge(tarama, (bayrak & 1) != 0);
                if (bolge >= 0)
                    System.Threading.Interlocked.Exchange(ref sonBasis[bolge], Math.Max(1, saat.ElapsedMilliseconds));
            }
            return CallNextHookEx(kanca, kod, wParam, lParam);
        }

        private delegate IntPtr KancaYordami(int kod, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int tur, KancaYordami yordam, IntPtr modul, uint isParcacigi);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr kanca);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr kanca, int kod, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string ad);
    }

    // Tarama kodlarını (set 1) K55'teki yatay konuma çevirir. Konumlar tuş birimi cinsindendir ve ana bloğun
    // sol kenarından ölçülür; önizlemedeki çizimle aynı yerleşimi kullanır (G sütunu + ana blok + gezinme + sayısal).
    public static class TusHaritasi
    {
        private const double GSutunu = 1.25;
        private const double ToplamGenislik = 23.75;

        private static readonly Dictionary<int, double> Konumlar = Olustur();

        public static int Bolge(int tarama, bool genisletilmis)
        {
            double x;
            if (!Konumlar.TryGetValue(tarama | (genisletilmis ? 0x100 : 0), out x)) return -1;
            int b = (int)((GSutunu + x) / ToplamGenislik * K55.BolgeSayisi);
            return Math.Max(0, Math.Min(K55.BolgeSayisi - 1, b));
        }

        private static Dictionary<int, double> Olustur()
        {
            var d = new Dictionary<int, double>();
            Action<int, double> e = (k, x) => d[k] = x;
            const int E = 0x100;

            // İşlev satırı
            e(0x01, 0.5);
            for (int i = 0; i < 4; i++) { e(0x3B + i, 2.5 + i); e(0x3F + i, 7 + i); }
            e(0x43, 11.5); e(0x44, 12.5); e(0x57, 13.5); e(0x58, 14.5);
            e(E | 0x37, 15.75); e(0x46, 16.75); e(0x45, 17.75);     // Pause genişletilmemiş 0x45 gelir
            // Rakam satırı
            e(0x29, 0.5);
            for (int i = 0; i < 12; i++) e(0x02 + i, 1.5 + i);
            e(0x0E, 14);
            // Q satırı
            e(0x0F, 0.75);
            for (int i = 0; i < 12; i++) e(0x10 + i, 2 + i);
            e(0x2B, 14.25);
            // A satırı
            e(0x3A, 0.875);
            for (int i = 0; i < 11; i++) e(0x1E + i, 2.25 + i);
            e(0x1C, 13.875);
            // Z satırı
            e(0x2A, 1.125); e(0x56, 1.5);
            for (int i = 0; i < 10; i++) e(0x2C + i, 2.75 + i);
            e(0x36, 13.625);
            // Alt satır
            e(0x1D, 0.625); e(E | 0x5B, 1.875); e(0x38, 3.125); e(0x39, 6.875);
            e(E | 0x38, 10.625); e(E | 0x5C, 11.875); e(E | 0x5D, 13.125); e(E | 0x1D, 14.375);
            // Gezinme ve ok tuşları
            e(E | 0x52, 15.75); e(E | 0x47, 16.75); e(E | 0x49, 17.75);
            e(E | 0x53, 15.75); e(E | 0x4F, 16.75); e(E | 0x51, 17.75);
            e(E | 0x48, 16.75); e(E | 0x4B, 15.75); e(E | 0x50, 16.75); e(E | 0x4D, 17.75);
            // Sayısal tuş takımı
            e(E | 0x45, 19); e(E | 0x35, 20); e(0x37, 21); e(0x4A, 22);    // Num Lock genişletilmiş 0x45 gelir
            e(0x47, 19); e(0x48, 20); e(0x49, 21); e(0x4E, 22);
            e(0x4B, 19); e(0x4C, 20); e(0x4D, 21);
            e(0x4F, 19); e(0x50, 20); e(0x51, 21); e(E | 0x1C, 22);
            e(0x52, 19.5); e(0x53, 21);
            return d;
        }
    }
}
