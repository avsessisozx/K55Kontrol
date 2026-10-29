using System;
using System.IO;
using System.Threading;

namespace K55Kontrol
{
    // Corsair K55 RGB PRO (1B1C:1BA4) aydınlatma protokolü.
    // Komutlar 65 baytlık çıkış raporlarıdır: [0]=rapor kimliği (0), [1]=0x08 (kablolu yazma), [2]=komut, ...
    internal sealed class K55 : IDisposable
    {
        public const ushort VID = 0x1B1C;
        public const ushort PID = 0x1BA4;
        // Klavyede 6 ışık var: 0 numaralı ışık Windows kilidi / makro kaydı göstergesi, 1–5 soldan sağa tuş bölgeleri.
        public const int IsikSayisi = 6;
        public const int BolgeSayisi = 5;

        private const int Arayuz = 1;
        private const ushort UsagePage = 0xFF42;
        private const byte YazmaKimligi = 0x08;

        private const byte KOMUT_AYARLA = 0x01;
        private const byte KOMUT_OKU = 0x02;
        private const byte KOMUT_ISLEM_BITIR = 0x05;
        private const byte KOMUT_BLOK_ILK = 0x06;
        private const byte KOMUT_BLOK_DEVAM = 0x07;
        private const byte KOMUT_ISLEM_BASLAT = 0x0D;

        private const byte OZELLIK_MOD = 0x03;
        private const byte MOD_DONANIM = 0x01;
        private const byte MOD_YAZILIM = 0x02;

        private const int CevapBekleme = 50;

        private readonly HidCihaz hid;
        private readonly int raporBoyu;
        private const byte AydinlatmaUcNoktasi = 0x01;
        private readonly object kilit = new object();

        private K55(HidCihaz hid)
        {
            this.hid = hid;
            raporBoyu = hid.CikisRaporBoyu > 0 ? hid.CikisRaporBoyu : 65;
        }

        public static K55 Bul()
        {
            HidCihaz h = HidCihaz.Ac(VID, PID, Arayuz, UsagePage);
            return h == null ? null : new K55(h);
        }

        public bool Bagli { get { return hid.Bagli; } }
        public string Yol { get { return hid.Yol; } }
        public byte UcNoktasi { get { return AydinlatmaUcNoktasi; } }

        // Yazılım moduna geçer. Bu klavyede (iCUE de dahil) anlık aydınlatma 0x01 uç noktasına düzlemsel R/G/B
        // olarak yazılır; 0x22 uç noktası başka (üçlü) bir veri biçimi beklediği için kullanılmaz.
        public void YazilimModunaGec()
        {
            lock (kilit)
            {
                Gonder(KOMUT_AYARLA, OZELLIK_MOD, 0x00, MOD_YAZILIM);
                Gonder(KOMUT_OKU, 0x5F);
            }
        }

        // Aydınlatmayı klavyenin kendi (kayıtlı) moduna geri bırakır.
        public void DonanimModunaGec()
        {
            lock (kilit)
            {
                Gonder(KOMUT_AYARLA, OZELLIK_MOD, 0x00, MOD_DONANIM);
            }
        }

        // Gösterge ışığının (Windows kilidi) yazılım modundaki rengi.
        public Renk Gosterge { get; set; }

        // Her tuş bölgesi için bir renk gönderir (ya da IsikSayisi kadar renkle ışıkları doğrudan yazar).
        // Veri düzlemsel: önce tüm R, sonra tüm G, sonra tüm B.
        public void RenkleriYaz(Renk[] bolgeler)
        {
            Renk[] renkler = bolgeler;
            if (bolgeler.Length != IsikSayisi)
            {
                renkler = new Renk[IsikSayisi];
                renkler[0] = Gosterge;
                Array.Copy(bolgeler, 0, renkler, 1, Math.Min(bolgeler.Length, BolgeSayisi));
            }
            int n = renkler.Length;
            byte[] veri = new byte[n * 3];
            for (int i = 0; i < n; i++)
            {
                veri[i] = renkler[i].R;
                veri[n + i] = renkler[i].G;
                veri[2 * n + i] = renkler[i].B;
            }

            lock (kilit)
            {
                hid.GelenleriTemizle();
                Gonder(KOMUT_ISLEM_BASLAT, 0x00, AydinlatmaUcNoktasi);
                BlokYaz(0x00, veri);
                Gonder(KOMUT_ISLEM_BITIR, 0x01, 0x00);
            }
        }

        // Klavyenin dahili hafızasındaki donanım aydınlatması dosyası (iCUE'nun "cihaz hafızası" kaydı).
        // Uygulama kapalıyken ve klavye kendi modundayken bu dosyadaki efekt oynatılır.
        private const byte HafizaTutamaci = 0x01;
        private static readonly byte[] AydinlatmaDosyasi = { 0x61, 0x6D };   // dosya 0x6D61
        private const byte KOMUT_DOSYA_TEMIZLE = 0x09;

        // Dosyayı açar, içeriğini siler, veriyi yazar ve kapatır. Dosya işlemleri yalnızca yazılım modunda kabul
        // edildiği için önce o moda geçilir; klavyeyi kendi moduna döndürmek çağırana kalır.
        // Cevap biçimi: [0]=rapor kimliği, [1]=0, [2]=komutun yankısı, [3]=durum (0 = başarılı).
        public void DonanimAydinlatmasiniYaz(byte[] veri)
        {
            lock (kilit)
            {
                hid.GelenleriTemizle();
                Gonder(KOMUT_AYARLA, OZELLIK_MOD, 0x00, MOD_YAZILIM);
                Denetle(GonderCevap(KOMUT_ISLEM_BASLAT, HafizaTutamaci, AydinlatmaDosyasi[0], AydinlatmaDosyasi[1]), "hafıza dosyası açılamadı");
                try
                {
                    Denetle(GonderCevap(KOMUT_DOSYA_TEMIZLE, HafizaTutamaci), "hafıza dosyası temizlenemedi");
                    BlokYaz(HafizaTutamaci, veri);
                }
                finally
                {
                    GonderCevap(KOMUT_ISLEM_BITIR, 0x01, HafizaTutamaci);
                }
            }
        }

        private static void Denetle(byte[] cevap, string islem)
        {
            if (cevap == null) throw new IOException("Klavye cevap vermedi (" + islem + ").");
            if (cevap.Length > 3 && cevap[3] != 0)
                throw new IOException("Klavye isteği reddetti (" + islem + ", kod " + cevap[3] + ").");
        }

        // Veriyi verilen tutamaca blok halinde yazar: ilk pakette 4 baytlık uzunluk, sonra devam paketleri.
        private void BlokYaz(byte tutamac, byte[] veri)
        {
            byte[] rapor = new byte[raporBoyu];
            rapor[1] = YazmaKimligi;
            rapor[2] = KOMUT_BLOK_ILK;
            rapor[3] = tutamac;
            rapor[4] = (byte)(veri.Length & 0xFF);
            rapor[5] = (byte)(veri.Length >> 8);
            int kopya = Math.Min(veri.Length, raporBoyu - 8);
            Buffer.BlockCopy(veri, 0, rapor, 8, kopya);
            hid.Yaz(rapor);
            hid.Oku(5);

            int gonderilen = kopya;
            while (gonderilen < veri.Length)
            {
                rapor = new byte[raporBoyu];
                rapor[1] = YazmaKimligi;
                rapor[2] = KOMUT_BLOK_DEVAM;
                rapor[3] = tutamac;
                kopya = Math.Min(veri.Length - gonderilen, raporBoyu - 4);
                Buffer.BlockCopy(veri, gonderilen, rapor, 4, kopya);
                hid.Yaz(rapor);
                Thread.Sleep(2);
                gonderilen += kopya;
            }
        }

        // Komut gönderir, cevabın durum baytını döndürür (0 = başarılı, 0xFF = cevap yok).
        private byte Gonder(byte komut, params byte[] parametreler)
        {
            byte[] cevap = GonderCevap(komut, parametreler);
            return (cevap != null && cevap.Length > 2) ? cevap[2] : (byte)0xFF;
        }

        private byte[] GonderCevap(byte komut, params byte[] parametreler)
        {
            byte[] rapor = new byte[raporBoyu];
            rapor[1] = YazmaKimligi;
            rapor[2] = komut;
            Buffer.BlockCopy(parametreler, 0, rapor, 3, parametreler.Length);
            hid.Yaz(rapor);
            return hid.Oku(CevapBekleme);
        }

        public void Dispose()
        {
            try { if (Bagli) DonanimModunaGec(); } catch (Exception) { }
            hid.Dispose();
        }
    }
}
