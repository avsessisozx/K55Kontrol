using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace K55Kontrol
{
    // Doygunluk/parlaklık karesi, ton çubuğu, HEX kutusu ve hazır renklerden oluşan renk seçici.
    public sealed class RenkSecici : StackPanel
    {
        public event Action<Renk> RenkDegisti;

        private static readonly string[] HazirRenkler =
        {
            "#FF0000", "#FF5A00", "#FFB000", "#FFE600", "#7CFF00", "#00FF55",
            "#00FFD5", "#00A8FF", "#0040FF", "#7A00FF", "#FF00D4", "#FFFFFF"
        };

        private double ton, doygunluk = 1, parlaklik = 1;
        private Renk renk = new Renk(255, 0, 0);
        private bool hexYaziliyor;

        private readonly Rectangle tonZemini = new Rectangle { RadiusX = 10, RadiusY = 10 };
        private readonly Canvas kareTutucu = new Canvas();
        private readonly Ellipse kareImleci = Imlec();
        private readonly Canvas tonTutucu = new Canvas();
        private readonly Ellipse tonImleci = Imlec();
        private readonly Border onizleme = new Border { Width = 38, Height = 38, CornerRadius = new CornerRadius(10) };
        private readonly TextBox hexKutusu = new TextBox { FontFamily = new FontFamily("Consolas"), FontSize = 14, MaxLength = 7 };

        public RenkSecici()
        {
            // Doygunluk / parlaklık karesi
            Grid kare = new Grid { Height = 150, Cursor = Cursors.Cross };
            kare.Children.Add(tonZemini);
            kare.Children.Add(new Rectangle
            {
                RadiusX = 10, RadiusY = 10,
                Fill = new LinearGradientBrush(Colors.White, Color.FromArgb(0, 255, 255, 255), 0)
            });
            kare.Children.Add(new Rectangle
            {
                RadiusX = 10, RadiusY = 10,
                Fill = new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Colors.Black, 90)
            });
            kareTutucu.Children.Add(kareImleci);
            kare.Children.Add(kareTutucu);
            Surukle(kare, p =>
            {
                doygunluk = Sinirla(p.X / kare.ActualWidth);
                parlaklik = 1 - Sinirla(p.Y / kare.ActualHeight);
                HsvdenGuncelle();
            });
            kare.SizeChanged += delegate { ImlecleriYerlestir(); };
            Children.Add(kare);

            // Ton çubuğu
            Grid tonCubugu = new Grid { Height = 16, Margin = new Thickness(0, 14, 0, 0), Cursor = Cursors.Hand };
            var gokkusagi = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            for (int i = 0; i <= 6; i++)
            {
                Renk c = Renk.Hsv(i / 6.0);
                gokkusagi.GradientStops.Add(new GradientStop(Color.FromRgb(c.R, c.G, c.B), i / 6.0));
            }
            tonCubugu.Children.Add(new Rectangle { RadiusX = 8, RadiusY = 8, Fill = gokkusagi });
            tonTutucu.Children.Add(tonImleci);
            tonCubugu.Children.Add(tonTutucu);
            Surukle(tonCubugu, p =>
            {
                ton = Sinirla(p.X / tonCubugu.ActualWidth) * 0.9999;
                HsvdenGuncelle();
            });
            tonCubugu.SizeChanged += delegate { ImlecleriYerlestir(); };
            Children.Add(tonCubugu);

            // Önizleme + HEX
            Grid satir = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            satir.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            satir.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            onizleme.BorderBrush = (Brush)Application.Current.FindResource("Cizgi");
            onizleme.BorderThickness = new Thickness(1);
            satir.Children.Add(onizleme);
            hexKutusu.Margin = new Thickness(10, 0, 0, 0);
            hexKutusu.VerticalContentAlignment = VerticalAlignment.Center;
            hexKutusu.TextChanged += delegate
            {
                if (hexYaziliyor) return;
                Renk c;
                if (hexKutusu.Text.Trim().TrimStart('#').Length == 6 && Renk.HexOku(hexKutusu.Text, out c))
                {
                    RenkAyarla(c, false);
                    Bildir();
                }
            };
            hexKutusu.LostKeyboardFocus += delegate { HexYaz(); };
            Grid.SetColumn(hexKutusu, 1);
            satir.Children.Add(hexKutusu);
            Children.Add(satir);

            // Hazır renkler
            UniformGrid hazir = new UniformGrid { Columns = 6, Margin = new Thickness(0, 14, 0, 0) };
            foreach (string h in HazirRenkler)
            {
                Renk c;
                Renk.HexOku(h, out c);
                Border b = new Border
                {
                    Height = 26, Margin = new Thickness(3), CornerRadius = new CornerRadius(7),
                    Background = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B)),
                    BorderBrush = (Brush)Application.Current.FindResource("Cizgi"), BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand, ToolTip = h
                };
                b.MouseLeftButtonUp += delegate { RenkAyarla(c, true); Bildir(); };
                hazir.Children.Add(b);
            }
            Children.Add(hazir);

            RenkAyarla(renk, true);
        }

        public Renk Renk
        {
            get { return renk; }
            set { if (!value.Equals(renk)) RenkAyarla(value, true); }
        }

        private void RenkAyarla(Renk c, bool hexiGuncelle)
        {
            renk = c;
            double s, v;
            c.HsvAl(out double t, out s, out v);
            // Gri tonlarda ton bilgisi yoktur; imleç zıplamasın diye eski tonu koru.
            if (s > 0.001 && v > 0.001) ton = t;
            doygunluk = s;
            parlaklik = v;
            Gorunumu(hexiGuncelle);
        }

        private void HsvdenGuncelle()
        {
            renk = Renk.Hsv(ton, doygunluk, parlaklik);
            Gorunumu(true);
            Bildir();
        }

        private void Gorunumu(bool hexiGuncelle)
        {
            Renk saf = Renk.Hsv(ton);
            tonZemini.Fill = new SolidColorBrush(Color.FromRgb(saf.R, saf.G, saf.B));
            onizleme.Background = new SolidColorBrush(Color.FromRgb(renk.R, renk.G, renk.B));
            if (hexiGuncelle) HexYaz();
            ImlecleriYerlestir();
        }

        private void HexYaz()
        {
            hexYaziliyor = true;
            hexKutusu.Text = renk.Hex;
            hexYaziliyor = false;
        }

        private void ImlecleriYerlestir()
        {
            FrameworkElement kare = (FrameworkElement)kareTutucu.Parent;
            Canvas.SetLeft(kareImleci, doygunluk * kare.ActualWidth - 8);
            Canvas.SetTop(kareImleci, (1 - parlaklik) * kare.ActualHeight - 8);
            FrameworkElement cubuk = (FrameworkElement)tonTutucu.Parent;
            Canvas.SetLeft(tonImleci, ton * cubuk.ActualWidth - 8);
            Canvas.SetTop(tonImleci, cubuk.ActualHeight / 2 - 8);
        }

        private void Bildir()
        {
            if (RenkDegisti != null) RenkDegisti(renk);
        }

        private static Ellipse Imlec() => new Ellipse
        {
            Width = 16, Height = 16, Stroke = Brushes.White, StrokeThickness = 3, IsHitTestVisible = false,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 4, ShadowDepth = 0, Opacity = 0.7 }
        };

        // Basılı tutup sürükleyerek değer seçmeyi sağlar.
        private static void Surukle(FrameworkElement e, Action<Point> secildi)
        {
            e.MouseLeftButtonDown += (s, a) => { e.CaptureMouse(); secildi(a.GetPosition(e)); a.Handled = true; };
            e.MouseMove += (s, a) => { if (e.IsMouseCaptured) secildi(a.GetPosition(e)); };
            e.MouseLeftButtonUp += (s, a) => e.ReleaseMouseCapture();
        }

        private static double Sinirla(double d) => d < 0 ? 0 : (d > 1 ? 1 : d);
    }
}
