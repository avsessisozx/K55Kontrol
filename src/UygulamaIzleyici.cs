using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace K55Kontrol
{
    // Çalışan programları düzenli aralıklarla tarar; bir profile bağlı bir program açıksa o profili bildirir.
    // Birden çok eşleşme varsa profil listesindeki ilk profil kazanır.
    public sealed class UygulamaIzleyici : IDisposable
    {
        public event Action<Profil, string> Degisti;   // (eşleşen profil ya da null, eşleşen program adı)

        private readonly Func<IList<Profil>> profiller;
        private readonly Timer zamanlayici;
        private Profil sonProfil;
        private string sonUygulama;

        private const int TaramaAraligiMs = 2000;

        public UygulamaIzleyici(Func<IList<Profil>> profiller)
        {
            this.profiller = profiller;
            // Profil listesi arayüzde aynı anda değişebilir; o turdaki hata bir sonraki taramada düzelir.
            zamanlayici = new Timer(delegate { try { Tara(); } catch (Exception) { } }, null, 0, TaramaAraligiMs);
        }

        public Profil EslesenProfil => sonProfil;
        public string EslesenUygulama => sonUygulama;

        // Profil listesi değişince beklemeden yeniden tarar.
        public void HemenTara()
        {
            zamanlayici.Change(0, TaramaAraligiMs);
        }

        private void Tara()
        {
            HashSet<string> calisanlar = CalisanProgramlar();
            Profil bulunan = null;
            string uygulama = null;
            foreach (Profil p in profiller().ToList())
            {
                uygulama = p.Uygulamalar.FirstOrDefault(u => calisanlar.Contains(ProgramAdi(u)));
                if (uygulama != null) { bulunan = p; break; }
            }

            if (bulunan != sonProfil || uygulama != sonUygulama)
            {
                sonProfil = bulunan;
                sonUygulama = uygulama;
                Action<Profil, string> e = Degisti;
                if (e != null) e(bulunan, uygulama);
            }
        }

        // "cs2.exe", "CS2" ve "C:\...\cs2.exe" aynı programı ifade eder.
        public static string ProgramAdi(string s)
        {
            s = (s ?? "").Trim();
            int i = s.LastIndexOfAny(new[] { '\\', '/' });
            if (i >= 0) s = s.Substring(i + 1);
            if (s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) s = s.Substring(0, s.Length - 4);
            return s.ToLowerInvariant();
        }

        public static HashSet<string> CalisanProgramlar()
        {
            HashSet<string> sonuc = new HashSet<string>();
            foreach (Process p in Process.GetProcesses())
            {
                try { sonuc.Add(p.ProcessName.ToLowerInvariant()); }
                catch (Exception) { }
                finally { p.Dispose(); }
            }
            return sonuc;
        }

        // Profillere eklemek için penceresi olan (kullanıcının gördüğü) programları listeler.
        public static List<KeyValuePair<string, string>> PencereliProgramlar()
        {
            var sonuc = new Dictionary<string, string>();
            int kendim = Process.GetCurrentProcess().Id;
            foreach (Process p in Process.GetProcesses())
            {
                try
                {
                    if (p.Id == kendim || p.MainWindowHandle == IntPtr.Zero || string.IsNullOrEmpty(p.MainWindowTitle))
                        continue;
                    string ad = p.ProcessName.ToLowerInvariant();
                    if (!sonuc.ContainsKey(ad)) sonuc[ad] = p.MainWindowTitle;
                }
                catch (Exception) { }
                finally { p.Dispose(); }
            }
            return sonuc.OrderBy(k => k.Key).ToList();
        }

        public void Dispose()
        {
            zamanlayici.Dispose();
        }
    }
}
