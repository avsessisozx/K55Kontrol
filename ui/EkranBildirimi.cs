using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace K55Kontrol
{
    // Kısayola basılınca ekranın alt ortasında kısa süre görünen, odak çalmayan ve tıklamaları geçiren bildirim.
    public sealed class EkranBildirimi : Window
    {
        private readonly TextBlock simge = new TextBlock();
        private readonly TextBlock baslik = new TextBlock();
        private readonly TextBlock ayrinti = new TextBlock();
        private readonly DispatcherTimer kapatici = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1600) };

        public EkranBildirimi()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            Focusable = false;
            SizeToContent = SizeToContent.WidthAndHeight;
            ResizeMode = ResizeMode.NoResize;
            Opacity = 0;

            simge.FontFamily = (FontFamily)Application.Current.FindResource("Simgeler");
            simge.FontSize = 20;
            simge.Foreground = (Brush)Application.Current.FindResource("VurguAcik");
            simge.VerticalAlignment = VerticalAlignment.Center;
            simge.Margin = new Thickness(0, 0, 14, 0);
            baslik.FontSize = 15;
            baslik.FontWeight = FontWeights.SemiBold;
            baslik.Foreground = (Brush)Application.Current.FindResource("Metin");
            ayrinti.FontSize = 12;
            ayrinti.Foreground = (Brush)Application.Current.FindResource("IkincilMetin");

            StackPanel metinler = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            metinler.Children.Add(baslik);
            metinler.Children.Add(ayrinti);
            StackPanel satir = new StackPanel { Orientation = Orientation.Horizontal };
            satir.Children.Add(simge);
            satir.Children.Add(metinler);

            Content = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xF2, 0x18, 0x1B, 0x22)),
                BorderBrush = (Brush)Application.Current.FindResource("Cizgi"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(20, 14, 26, 14),
                Margin = new Thickness(16),
                MinWidth = 220,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 16, ShadowDepth = 2, Opacity = 0.5 },
                Child = satir
            };

            kapatici.Tick += delegate
            {
                kapatici.Stop();
                var gizle = new DoubleAnimation(0, TimeSpan.FromMilliseconds(250));
                gizle.Completed += delegate { if (Opacity == 0) Hide(); };
                BeginAnimation(OpacityProperty, gizle);
            };
        }

        public void Goster(string simgeKarakteri, string baslikMetni, string ayrintiMetni = null)
        {
            simge.Text = simgeKarakteri;
            baslik.Text = baslikMetni;
            ayrinti.Text = ayrintiMetni ?? "";
            ayrinti.Visibility = string.IsNullOrEmpty(ayrintiMetni) ? Visibility.Collapsed : Visibility.Visible;

            if (!IsVisible) Show();
            UpdateLayout();
            Rect alan = SystemParameters.WorkArea;
            Left = alan.Left + (alan.Width - ActualWidth) / 2;
            Top = alan.Bottom - ActualHeight - 60;

            BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(120)));
            kapatici.Stop();
            kapatici.Start();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            // Tıklamaları geçir, Alt+Tab'da görünme, odak alma.
            IntPtr h = new WindowInteropHelper(this).Handle;
            int stil = GetWindowLong(h, GWL_EXSTYLE);
            SetWindowLong(h, GWL_EXSTYLE, stil | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_LAYERED);
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_LAYERED = 0x80000, WS_EX_NOACTIVATE = 0x08000000;

        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr h, int i);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr h, int i, int v);
    }
}
