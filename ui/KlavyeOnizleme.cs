using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace K55Kontrol
{
    // K55 RGB PRO'nun canlı çizimi: tuşlar bulundukları bölgenin rengiyle boyanır, arkada bulanık bir ışık yansıması olur.
    // Bölge seçimi açıkken bölgeye tıklanarak o bölge seçilebilir.
    public sealed class KlavyeOnizleme : Grid
    {
        public event Action<int> BolgeTiklandi;

        private readonly ParlamaKatmani parlama = new ParlamaKatmani();
        private readonly TusKatmani tuslar = new TusKatmani();

        public KlavyeOnizleme()
        {
            parlama.Effect = new BlurEffect { Radius = 70, RenderingBias = RenderingBias.Performance };
            parlama.Opacity = 0.55;
            parlama.IsHitTestVisible = false;
            Children.Add(parlama);
            Children.Add(tuslar);
            tuslar.BolgeTiklandi += b => { if (BolgeTiklandi != null) BolgeTiklandi(b); };
        }

        public void Goster(Renk[] renkler)
        {
            parlama.Renkler = renkler;
            tuslar.Renkler = renkler;
            parlama.InvalidateVisual();
            tuslar.InvalidateVisual();
        }

        public bool BolgeSecimi
        {
            get { return tuslar.BolgeSecimi; }
            set { tuslar.BolgeSecimi = value; tuslar.Cursor = value ? Cursors.Hand : null; tuslar.InvalidateVisual(); }
        }

        public int SeciliBolge
        {
            get { return tuslar.SeciliBolge; }
            set { tuslar.SeciliBolge = value; tuslar.InvalidateVisual(); }
        }

        // ---- Yerleşim (tuş birimi cinsinden) ----

        private const double Genislik = 23.75;      // G sütunu + ana blok + gezinme + sayısal tuş takımı
        private const double Yukseklik = 6.25;
        private const double Kenar = 0.55;
        private const double AltBosluk = 0.9;       // bölge numaraları için

        private static readonly List<Rect> Tuslar = TuslariOlustur();

        private static List<Rect> TuslariOlustur()
        {
            var t = new List<Rect>();
            double[] satirY = { 0, 1.25, 2.25, 3.25, 4.25, 5.25 };
            foreach (double y in satirY) t.Add(new Rect(0, y, 1, 1));   // G1-G6

            const double x0 = 1.25;
            Action<double, double, double[]> satir = (x, y, gen) =>
            {
                foreach (double g in gen)
                {
                    if (g > 0) t.Add(new Rect(x, y, g, 1));
                    x += Math.Abs(g);
                }
            };
            Func<int, double, double[]> tekrar = (n, g) => { var a = new double[n]; for (int i = 0; i < n; i++) a[i] = g; return a; };
            Func<double[][], double[]> birlestir = parcalar => { var l = new List<double>(); foreach (var p in parcalar) l.AddRange(p); return l.ToArray(); };

            satir(x0, 0, birlestir(new[] { new double[] { 1, -1 }, tekrar(4, 1), new double[] { -0.5 }, tekrar(4, 1), new double[] { -0.5 }, tekrar(4, 1) }));
            satir(x0, 1.25, birlestir(new[] { tekrar(13, 1), new double[] { 2 } }));
            satir(x0, 2.25, birlestir(new[] { new double[] { 1.5 }, tekrar(12, 1), new double[] { 1.5 } }));
            satir(x0, 3.25, birlestir(new[] { new double[] { 1.75 }, tekrar(11, 1), new double[] { 2.25 } }));
            satir(x0, 4.25, birlestir(new[] { new double[] { 2.25 }, tekrar(10, 1), new double[] { 2.75 } }));
            satir(x0, 5.25, new double[] { 1.25, 1.25, 1.25, 6.25, 1.25, 1.25, 1.25, 1.25 });

            double gx = x0 + 15.25;
            satir(gx, 0, tekrar(3, 1));
            satir(gx, 1.25, tekrar(3, 1));
            satir(gx, 2.25, tekrar(3, 1));
            satir(gx + 1, 4.25, new double[] { 1 });
            satir(gx, 5.25, tekrar(3, 1));

            double nx = x0 + 18.5;
            satir(nx, 1.25, tekrar(4, 1));
            satir(nx, 2.25, tekrar(3, 1));
            satir(nx, 3.25, tekrar(3, 1));
            satir(nx, 4.25, tekrar(3, 1));
            satir(nx, 5.25, new double[] { 2, 1 });
            t.Add(new Rect(nx + 3, 2.25, 1, 2));    // +
            t.Add(new Rect(nx + 3, 4.25, 1, 2));    // Enter
            return t;
        }

        public static int Bolge(double x) => Math.Max(0, Math.Min(K55.BolgeSayisi - 1, (int)(x / Genislik * K55.BolgeSayisi)));

        // Kontrol boyutuna göre birim büyüklüğü ve klavye gövdesinin sol üst köşesi.
        private static void Olcu(Size alan, out double u, out Point koke)
        {
            double tg = Genislik + 2 * Kenar, ty = Yukseklik + 2 * Kenar + AltBosluk;
            u = Math.Max(1, Math.Min(alan.Width / tg, alan.Height / ty));
            koke = new Point((alan.Width - tg * u) / 2 + Kenar * u, (alan.Height - ty * u) / 2 + Kenar * u);
        }

        private static Rect Donustur(Rect r, double u, Point koke) =>
            new Rect(koke.X + r.X * u, koke.Y + r.Y * u, r.Width * u, r.Height * u);

        private static Color Medya(Renk r) => Color.FromRgb(r.R, r.G, r.B);

        private static readonly Renk TusZemini = new Renk(0x23, 0x26, 0x2E);

        private sealed class ParlamaKatmani : FrameworkElement
        {
            public Renk[] Renkler;

            protected override void OnRender(DrawingContext dc)
            {
                if (Renkler == null) return;
                double u; Point koke;
                Olcu(RenderSize, out u, out koke);
                double bg = Genislik / K55.BolgeSayisi;
                for (int i = 0; i < Renkler.Length; i++)
                {
                    var b = new SolidColorBrush(Medya(Renkler[i]));
                    b.Freeze();
                    Rect r = Donustur(new Rect(i * bg - 0.3, -0.4, bg + 0.6, Yukseklik + 0.8), u, koke);
                    dc.DrawRectangle(b, null, r);
                }
            }
        }

        private sealed class TusKatmani : FrameworkElement
        {
            public event Action<int> BolgeTiklandi;
            public Renk[] Renkler;
            public bool BolgeSecimi;
            public int SeciliBolge = -1;
            private int uzerindekiBolge = -1;

            private static readonly Brush Govde = Dondur(new SolidColorBrush(Color.FromRgb(0x14, 0x16, 0x1B)));
            private static readonly Pen GovdeKenari = Dondur(new Pen(new SolidColorBrush(Color.FromRgb(0x2E, 0x33, 0x3F)), 1.5));
            private static readonly Brush NumaraRengi = Dondur(new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80)));
            private static readonly Pen SecimKalemi = Dondur(new Pen(Brushes.White, 2) { DashStyle = new DashStyle(new double[] { 3, 2 }, 0) });
            private static readonly Pen UzerindeKalemi = Dondur(new Pen(new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)), 1.5));

            private static T Dondur<T>(T f) where T : Freezable { f.Freeze(); return f; }

            protected override void OnRender(DrawingContext dc)
            {
                // Fare olaylarının boş alanlarda da gelmesi için şeffaf bir isabet alanı.
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
                double u; Point koke;
                Olcu(RenderSize, out u, out koke);

                Rect govde = Donustur(new Rect(-Kenar, -Kenar, Genislik + 2 * Kenar, Yukseklik + 2 * Kenar), u, koke);
                dc.DrawRoundedRectangle(Govde, GovdeKenari, govde, u * 0.45, u * 0.45);

                Brush[] dolgular = new Brush[K55.BolgeSayisi];
                Pen[] kenarlar = new Pen[K55.BolgeSayisi];
                for (int i = 0; i < K55.BolgeSayisi; i++)
                {
                    Renk c = Renkler != null && i < Renkler.Length ? Renkler[i] : Renk.Siyah;
                    var dolgu = new LinearGradientBrush(
                        Medya(Renk.Karistir(TusZemini, c, 0.78)), Medya(Renk.Karistir(TusZemini, c, 0.5)), 90);
                    dolgu.Freeze();
                    dolgular[i] = dolgu;
                    var kenar = new Pen(new SolidColorBrush(Medya(Renk.Karistir(c, new Renk(255, 255, 255), 0.25))), 1);
                    kenar.Brush.Opacity = c.Equals(Renk.Siyah) ? 0.15 : 0.6;
                    kenar.Freeze();
                    kenarlar[i] = kenar;
                }

                double bosluk = u * 0.09, kose = u * 0.16;
                foreach (Rect t in Tuslar)
                {
                    int b = Bolge(t.X + t.Width / 2);
                    Rect r = Donustur(t, u, koke);
                    r.Inflate(-bosluk, -bosluk);
                    dc.DrawRoundedRectangle(dolgular[b], kenarlar[b], r, kose, kose);
                }

                if (BolgeSecimi)
                {
                    double bg = Genislik / K55.BolgeSayisi;
                    for (int i = 0; i < K55.BolgeSayisi; i++)
                    {
                        Rect alan = Donustur(new Rect(i * bg + 0.05, -0.25, bg - 0.1, Yukseklik + 0.5), u, koke);
                        if (i == SeciliBolge)
                            dc.DrawRoundedRectangle(null, SecimKalemi, alan, u * 0.3, u * 0.3);
                        else if (i == uzerindekiBolge)
                            dc.DrawRoundedRectangle(null, UzerindeKalemi, alan, u * 0.3, u * 0.3);

                        var yazi = new FormattedText("Bölge " + (i + 1), System.Globalization.CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight, new Typeface("Segoe UI"), Math.Max(10, u * 0.42),
                            i == SeciliBolge ? Brushes.White : NumaraRengi, 1.0);
                        dc.DrawText(yazi, new Point(alan.X + (alan.Width - yazi.Width) / 2, govde.Bottom + u * 0.25));
                    }
                }
            }

            private int FareBolgesi(MouseEventArgs e)
            {
                double u; Point koke;
                Olcu(RenderSize, out u, out koke);
                double x = (e.GetPosition(this).X - koke.X) / u;
                return x < -Kenar || x > Genislik + Kenar ? -1 : Bolge(x);
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                if (!BolgeSecimi) return;
                int b = FareBolgesi(e);
                if (b != uzerindekiBolge) { uzerindekiBolge = b; InvalidateVisual(); }
            }

            protected override void OnMouseLeave(MouseEventArgs e)
            {
                if (uzerindekiBolge != -1) { uzerindekiBolge = -1; InvalidateVisual(); }
            }

            protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
            {
                if (!BolgeSecimi) return;
                int b = FareBolgesi(e);
                if (b >= 0 && BolgeTiklandi != null) BolgeTiklandi(b);
            }
        }
    }
}
