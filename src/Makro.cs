using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Threading;

namespace K55Kontrol
{
    public enum GTuru
    {
        Yok,
        TusKombinasyonu,
        Metin,
        Program,
        Medya,
        UygulamaEylemi,
        Makro
    }

    public enum MedyaTusu
    {
        OynatDuraklat,
        Sonraki,
        Onceki,
        Durdur,
        SesArtir,
        SesAzalt,
        SessizeAl
    }

    // Kaydedilmiş makronun bir adımı: tuşa basma ya da bırakma ve öncesinde beklenecek süre.
    [DataContract]
    public sealed class MakroAdimi
    {
        [DataMember(Order = 0)] public int Vk;
        [DataMember(Order = 1)] public int Tarama;
        [DataMember(Order = 2)] public bool Genisletilmis;
        [DataMember(Order = 3)] public bool Basili;
        [DataMember(Order = 4)] public int BeklemeMs;
    }

    // Bir G tuşuna atanan eylem.
    [DataContract]
    public sealed class GAtama
    {
        [DataMember(Order = 0)] public int Tus;                 // 1-6
        [DataMember(Order = 1)] public GTuru Tur;
        [DataMember(Order = 2)] public int Vk;                  // tuş kombinasyonu
        [DataMember(Order = 3)] public int Degistirici;         // Kisayol.Alt/Ctrl/Shift/Win
        [DataMember(Order = 4)] public string Metin;
        [DataMember(Order = 5)] public string Yol;              // program, dosya ya da web adresi
        [DataMember(Order = 6)] public MedyaTusu Medya;
        [DataMember(Order = 7)] public string UygulamaEylemi;   // "eylem:<Eylem>" ya da "profil:<ad>"
        [DataMember(Order = 8)] public List<MakroAdimi> Adimlar;

        public GAtama Kopya()
        {
            var a = (GAtama)MemberwiseClone();
            a.Adimlar = Adimlar == null ? new List<MakroAdimi>() : new List<MakroAdimi>(Adimlar);
            return a;
        }

        public static string EylemAnahtari(Eylem e) => "eylem:" + e;
        public static string ProfilAnahtari(string ad) => "profil:" + ad;
    }

    // Makro kaydı: kayıt süresince gerçek (programların göndermediği) tuş basışlarını ve aralarındaki süreleri toplar.
    // Kanca, ileti döngüsü olan iş parçacığında (arayüz) kurulmalıdır.
    public sealed class MakroKaydedici : IDisposable
    {
        public event Action<MakroAdimi> AdimEklendi;

        public readonly List<MakroAdimi> Adimlar = new List<MakroAdimi>();
        private readonly Stopwatch saat = new Stopwatch();
        private readonly HashSet<int> basili = new HashSet<int>();
        private IntPtr kanca;
        private KancaYordami yordam;

        public void Baslat()
        {
            Adimlar.Clear();
            basili.Clear();
            saat.Reset();
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
            if (kod >= 0)
            {
                int ileti = wParam.ToInt32();
                int vk = Marshal.ReadInt32(lParam), tarama = Marshal.ReadInt32(lParam, 4), bayrak = Marshal.ReadInt32(lParam, 8);
                bool enjekte = (bayrak & 0x10) != 0;
                bool bas = ileti == 0x100 || ileti == 0x104;
                // Basılı tutunca gelen otomatik tekrarları atla.
                if (!enjekte && (bas ? basili.Add(vk) : basili.Remove(vk)))
                {
                    var adim = new MakroAdimi
                    {
                        Vk = vk,
                        Tarama = tarama,
                        Genisletilmis = (bayrak & 0x1) != 0,
                        Basili = bas,
                        BeklemeMs = saat.IsRunning ? (int)saat.ElapsedMilliseconds : 0
                    };
                    saat.Restart();
                    Adimlar.Add(adim);
                    if (AdimEklendi != null) AdimEklendi(adim);
                }
            }
            return CallNextHookEx(kanca, kod, wParam, lParam);
        }

        private delegate IntPtr KancaYordami(int kod, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int tur, KancaYordami yordam, IntPtr modul, uint isParcacigi);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr kanca);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr kanca, int kod, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string ad);
    }

    // Tuş basışlarını, metni ve medya tuşlarını Windows'a SendInput ile gönderir.
    public static class MakroCalistirici
    {
        public static void TusKombinasyonu(int vk, int degistirici)
        {
            var girdiler = new List<INPUT>();
            int[] degistiriciTuslari = DegistiriciTuslari(degistirici);
            foreach (int d in degistiriciTuslari) girdiler.Add(Tus(d, false));
            girdiler.Add(Tus(vk, false));
            girdiler.Add(Tus(vk, true));
            for (int i = degistiriciTuslari.Length - 1; i >= 0; i--) girdiler.Add(Tus(degistiriciTuslari[i], true));
            Gonder(girdiler);
        }

        public static void MetinYaz(string metin)
        {
            if (string.IsNullOrEmpty(metin)) return;
            var girdiler = new List<INPUT>();
            foreach (char c in metin.Replace("\r\n", "\n"))
            {
                if (c == '\n')
                {
                    girdiler.Add(Tus(0x0D, false));
                    girdiler.Add(Tus(0x0D, true));
                    continue;
                }
                girdiler.Add(Unicode(c, false));
                girdiler.Add(Unicode(c, true));
            }
            Gonder(girdiler);
        }

        public static void Medya(MedyaTusu m)
        {
            int vk;
            switch (m)
            {
                case MedyaTusu.Sonraki: vk = 0xB0; break;
                case MedyaTusu.Onceki: vk = 0xB1; break;
                case MedyaTusu.Durdur: vk = 0xB2; break;
                case MedyaTusu.SesArtir: vk = 0xAF; break;
                case MedyaTusu.SesAzalt: vk = 0xAE; break;
                case MedyaTusu.SessizeAl: vk = 0xAD; break;
                default: vk = 0xB3; break;
            }
            Gonder(new List<INPUT> { Tus(vk, false), Tus(vk, true) });
        }

        public static void ProgramAc(string yol)
        {
            if (string.IsNullOrWhiteSpace(yol)) return;
            Process.Start(new ProcessStartInfo(yol.Trim()) { UseShellExecute = true });
        }

        // Kaydedilmiş makroyu arka planda, kayıttaki bekleme süreleriyle oynatır.
        public static void Oynat(List<MakroAdimi> adimlar)
        {
            if (adimlar == null || adimlar.Count == 0) return;
            List<MakroAdimi> kopya = new List<MakroAdimi>(adimlar);
            new Thread(delegate ()
            {
                HashSet<int> basili = new HashSet<int>();
                foreach (MakroAdimi a in kopya)
                {
                    if (a.BeklemeMs > 0) Thread.Sleep(Math.Min(a.BeklemeMs, 10000));
                    Gonder(new List<INPUT> { Tus(a.Vk, !a.Basili, a.Tarama, a.Genisletilmis) });
                    if (a.Basili) basili.Add(a.Vk); else basili.Remove(a.Vk);
                }
                // Kayıt bir tuş basılıyken bittiyse onu bırak, takılı kalmasın.
                foreach (int vk in basili) Gonder(new List<INPUT> { Tus(vk, true) });
            }) { IsBackground = true, Name = "Makro oynatıcı" }.Start();
        }

        public static string MedyaAdi(MedyaTusu m)
        {
            switch (m)
            {
                case MedyaTusu.Sonraki: return "Sonraki parça";
                case MedyaTusu.Onceki: return "Önceki parça";
                case MedyaTusu.Durdur: return "Durdur";
                case MedyaTusu.SesArtir: return "Sesi artır";
                case MedyaTusu.SesAzalt: return "Sesi azalt";
                case MedyaTusu.SessizeAl: return "Sessize al / aç";
                default: return "Oynat / duraklat";
            }
        }

        private static int[] DegistiriciTuslari(int d)
        {
            var l = new List<int>();
            if ((d & Kisayol.Ctrl) != 0) l.Add(0xA2);
            if ((d & Kisayol.Alt) != 0) l.Add(0xA4);
            if ((d & Kisayol.Shift) != 0) l.Add(0xA0);
            if ((d & Kisayol.Win) != 0) l.Add(0x5B);
            return l.ToArray();
        }

        private static bool GenisletilmisMi(int vk) =>
            (vk >= 0x21 && vk <= 0x28) || vk == 0x2D || vk == 0x2E || vk == 0x5B || vk == 0x5C || vk == 0x5D ||
            vk == 0x6F || vk == 0x90 || vk == 0xA3 || vk == 0xA5 || (vk >= 0xA6 && vk <= 0xB7);

        private static INPUT Tus(int vk, bool birak, int tarama = -1, bool? genisletilmis = null)
        {
            uint bayrak = 0;
            if (genisletilmis ?? GenisletilmisMi(vk)) bayrak |= 0x1;
            if (birak) bayrak |= 0x2;
            return new INPUT
            {
                type = 1,
                u = new GIRDI_BIRLESIM
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = (ushort)vk,
                        wScan = (ushort)(tarama >= 0 ? (uint)tarama : MapVirtualKey((uint)vk, 0)),
                        dwFlags = bayrak
                    }
                }
            };
        }

        private static INPUT Unicode(char c, bool birak) => new INPUT
        {
            type = 1,
            u = new GIRDI_BIRLESIM { ki = new KEYBDINPUT { wVk = 0, wScan = c, dwFlags = 0x4 | (birak ? 0x2u : 0u) } }
        };

        private static void Gonder(List<INPUT> girdiler)
        {
            if (girdiler.Count == 0) return;
            SendInput((uint)girdiler.Count, girdiler.ToArray(), Marshal.SizeOf(typeof(INPUT)));
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Explicit)]
        private struct GIRDI_BIRLESIM
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT { public uint type; public GIRDI_BIRLESIM u; }

        [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint sayi, INPUT[] girdiler, int boy);
        [DllImport("user32.dll")] private static extern uint MapVirtualKey(uint kod, uint tur);
    }
}
