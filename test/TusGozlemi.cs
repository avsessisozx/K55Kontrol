using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace K55Kontrol
{
    // G tuşlarının ne gönderdiğini bulmak için: klavyenin tüm HID arayüzlerinden gelen ham raporları ve
    // Windows'a ulaşan tuş basışlarını (düşük düzey kanca) belirli bir süre boyunca dosyaya yazar.
    internal static class TusGozlemi
    {
        private static readonly Stopwatch saat = Stopwatch.StartNew();
        private static readonly object kilit = new object();
        private static StreamWriter cikti;

        [STAThread]
        private static void Main(string[] args)
        {
            int saniye = args.Length > 0 ? int.Parse(args[0]) : 45;
            string dosya = args.Length > 1 ? args[1] : "tus-gozlemi.txt";
            cikti = new StreamWriter(dosya, false, new UTF8Encoding(false)) { AutoFlush = true };

            var cihazlar = new List<HidCihaz>();
            foreach (string yol in HidCihaz.Yollar(K55.VID, K55.PID))
            {
                HidCihaz c = HidCihaz.YolAc(yol);
                Yaz("ARAYUZ " + (c == null ? "acilamadi" : string.Format("page=0x{0:X4} usage=0x{1:X4}", c.UsagePage, c.Usage)) + " | " + yol);
                if (c == null) continue;
                cihazlar.Add(c);
                HidCihaz cihaz = c;
                string kisa = KisaAd(yol);
                new Thread(delegate ()
                {
                    while (true)
                    {
                        byte[] r = cihaz.Oku(1000);
                        if (r == null) { if (!cihaz.Bagli) break; continue; }
                        Yaz("HID " + kisa + " " + string.Join(" ", r.Take(24).Select(b => b.ToString("X2"))));
                    }
                }) { IsBackground = true }.Start();
            }

            KancaYordami yordam = Kanca;
            IntPtr kanca = SetWindowsHookEx(13, yordam, GetModuleHandle(null), 0);
            Yaz("BASLADI " + saniye + " sn");

            var zamanlayici = new System.Windows.Forms.Timer { Interval = saniye * 1000 };
            zamanlayici.Tick += delegate { Application.ExitThread(); };
            zamanlayici.Start();
            Application.Run();

            UnhookWindowsHookEx(kanca);
            GC.KeepAlive(yordam);
            Yaz("BITTI");
            foreach (var c in cihazlar) c.Dispose();
            cikti.Dispose();
        }

        private static string KisaAd(string yol)
        {
            string k = yol.ToLowerInvariant();
            int i = k.IndexOf("&mi_");
            string mi = i >= 0 ? k.Substring(i + 1, 5) : "?";
            int j = k.IndexOf("&col");
            return mi + (j >= 0 ? k.Substring(j, 6) : "");
        }

        private static IntPtr Kanca(int kod, IntPtr wParam, IntPtr lParam)
        {
            if (kod >= 0)
            {
                int ileti = wParam.ToInt32();
                int vk = Marshal.ReadInt32(lParam), tarama = Marshal.ReadInt32(lParam, 4), bayrak = Marshal.ReadInt32(lParam, 8);
                string tur = ileti == 0x100 || ileti == 0x104 ? "BAS" : "BIRAK";
                Yaz(string.Format("TUS {0} vk=0x{1:X2} ({2}) tarama=0x{3:X2} bayrak=0x{4:X2}", tur, vk, (Keys)vk, tarama, bayrak));
            }
            return CallNextHookEx(IntPtr.Zero, kod, wParam, lParam);
        }

        private static void Yaz(string s)
        {
            lock (kilit)
            {
                string satir = string.Format("{0,8:0.000} {1}", saat.Elapsed.TotalSeconds, s);
                cikti.WriteLine(satir);
                Console.WriteLine(satir);
            }
        }

        private delegate IntPtr KancaYordami(int kod, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int tur, KancaYordami yordam, IntPtr modul, uint isParcacigi);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr kanca);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr kanca, int kod, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string ad);
    }
}
