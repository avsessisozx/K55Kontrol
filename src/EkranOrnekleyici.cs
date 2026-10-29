using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace K55Kontrol
{
    // Ana ekranı 5 dikey şeride böler ve her şeridin ortalama rengini yayınlar (ambilight).
    // Ekran küçük bir bit eşleme küçültülerek okunur; görüntü hiçbir yere kaydedilmez.
    public sealed class EkranOrnekleyici : IDisposable
    {
        private const int Genislik = 48, Yukseklik = 12;
        private const int AralikMs = 50;

        private Thread isParcacigi;
        private volatile bool calisiyor;
        private volatile Renk[] renkler = new Renk[K55.BolgeSayisi];

        public Renk[] Renkler => renkler;

        public void Baslat()
        {
            if (calisiyor) return;
            calisiyor = true;
            isParcacigi = new Thread(Dongu) { IsBackground = true, Name = "Ekran örnekleyici" };
            isParcacigi.Start();
        }

        public void Durdur()
        {
            calisiyor = false;
            if (isParcacigi != null) isParcacigi.Join(1000);
            isParcacigi = null;
        }

        public void Dispose() => Durdur();

        private void Dongu()
        {
            IntPtr ekranDc = GetDC(IntPtr.Zero);
            IntPtr bellekDc = CreateCompatibleDC(ekranDc);
            BITMAPINFOHEADER bilgi = new BITMAPINFOHEADER
            {
                biSize = Marshal.SizeOf(typeof(BITMAPINFOHEADER)),
                biWidth = Genislik,
                biHeight = -Yukseklik,      // yukarıdan aşağı
                biPlanes = 1,
                biBitCount = 32
            };
            IntPtr bitler;
            IntPtr bitEslem = CreateDIBSection(bellekDc, ref bilgi, 0, out bitler, IntPtr.Zero, 0);
            IntPtr eski = SelectObject(bellekDc, bitEslem);
            SetStretchBltMode(bellekDc, 4 /* HALFTONE */);
            SetBrushOrgEx(bellekDc, 0, 0, IntPtr.Zero);
            int[] pikseller = new int[Genislik * Yukseklik];

            try
            {
                while (calisiyor)
                {
                    int ew = GetSystemMetrics(0), eh = GetSystemMetrics(1);
                    if (StretchBlt(bellekDc, 0, 0, Genislik, Yukseklik, ekranDc, 0, 0, ew, eh, 0x00CC0020 /* SRCCOPY */))
                    {
                        GdiFlush();
                        Marshal.Copy(bitler, pikseller, 0, pikseller.Length);
                        Renk[] yeni = new Renk[K55.BolgeSayisi];
                        int serit = Genislik / K55.BolgeSayisi;
                        for (int b = 0; b < K55.BolgeSayisi; b++)
                        {
                            long r = 0, g = 0, bl = 0;
                            int n = 0;
                            for (int y = 0; y < Yukseklik; y++)
                                for (int x = b * serit; x < (b + 1) * serit; x++)
                                {
                                    int p = pikseller[y * Genislik + x];
                                    r += (p >> 16) & 0xFF; g += (p >> 8) & 0xFF; bl += p & 0xFF;
                                    n++;
                                }
                            yeni[b] = Canlandir(new Renk((int)(r / n), (int)(g / n), (int)(bl / n)));
                        }
                        renkler = yeni;
                    }
                    Thread.Sleep(AralikMs);
                }
            }
            finally
            {
                SelectObject(bellekDc, eski);
                DeleteObject(bitEslem);
                DeleteDC(bellekDc);
                ReleaseDC(IntPtr.Zero, ekranDc);
            }
        }

        // Ortalama renkler soluk olur; doygunluğu artırmak klavyede daha canlı görünmesini sağlar.
        private static Renk Canlandir(Renk c)
        {
            double t, s, v;
            c.HsvAl(out t, out s, out v);
            return Renk.Hsv(t, Math.Min(1, s * 1.5), Math.Min(1, v * 1.15));
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public int biSize, biWidth, biHeight;
            public short biPlanes, biBitCount;
            public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
        }

        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr h, IntPtr dc);
        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int i);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr dc, ref BITMAPINFOHEADER bilgi, uint kullanim, out IntPtr bitler, IntPtr bolum, uint ofs);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr nesne);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr nesne);
        [DllImport("gdi32.dll")] private static extern int SetStretchBltMode(IntPtr dc, int mod);
        [DllImport("gdi32.dll")] private static extern bool SetBrushOrgEx(IntPtr dc, int x, int y, IntPtr onceki);
        [DllImport("gdi32.dll")] private static extern bool StretchBlt(IntPtr hedef, int hx, int hy, int hw, int hh, IntPtr kaynak, int kx, int ky, int kw, int kh, uint islem);
        [DllImport("gdi32.dll")] private static extern bool GdiFlush();
    }
}
