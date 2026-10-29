using System;
using System.Threading;

namespace K55Kontrol
{
    // Corsair K55 RGB PRO (1B1C:1BA4) aydınlatma protokolü.
    // Komutlar 65 baytlık çıkış raporlarıdır: [0]=rapor kimliği (0), [1]=0x08 (kablolu yazma), [2]=komut, ...
    internal sealed class K55 : IDisposable
    {
        public const ushort VID = 0x1B1C;
        public const ushort PID = 0x1BA4;
        public const int BolgeSayisi = 6;

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
        private byte aydinlatmaUcNoktasi = 0x22;
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
        public byte UcNoktasi { get { return aydinlatmaUcNoktasi; } }

        // Yazılım moduna geçer ve kullanılabilir aydınlatma uç noktasını belirler.
        public void YazilimModunaGec()
        {
            lock (kilit)
            {
                Gonder(KOMUT_AYARLA, OZELLIK_MOD, 0x00, MOD_YAZILIM);
                Gonder(KOMUT_OKU, 0x5F);

                // Bazı cihazlar 0x22 uç noktasını desteklemez; hata dönerse 0x01'e düş.
                aydinlatmaUcNoktasi = 0x22;
                byte sonuc = Gonder(KOMUT_ISLEM_BASLAT, 0x00, aydinlatmaUcNoktasi);
                if (sonuc != 0)
                {
                    aydinlatmaUcNoktasi = 0x01;
                    Gonder(KOMUT_ISLEM_BASLAT, 0x00, aydinlatmaUcNoktasi);
                }
                Gonder(KOMUT_ISLEM_BITIR, 0x01, 0x00);
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

        // Her bölge için bir renk gönderir. Veri düzlemsel: önce tüm R, sonra tüm G, sonra tüm B.
        public void RenkleriYaz(Renk[] renkler)
        {
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
                Gonder(KOMUT_ISLEM_BASLAT, 0x00, aydinlatmaUcNoktasi);

                byte[] rapor = new byte[raporBoyu];
                rapor[1] = YazmaKimligi;
                rapor[2] = KOMUT_BLOK_ILK;
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
                    kopya = Math.Min(veri.Length - gonderilen, raporBoyu - 4);
                    Buffer.BlockCopy(veri, gonderilen, rapor, 4, kopya);
                    hid.Yaz(rapor);
                    Thread.Sleep(2);
                    gonderilen += kopya;
                }

                Gonder(KOMUT_ISLEM_BITIR, 0x01, 0x00);
            }
        }

        // Komut gönderir, cevabın durum baytını döndürür (0 = başarılı, 0xFF = cevap yok).
        private byte Gonder(byte komut, params byte[] parametreler)
        {
            byte[] rapor = new byte[raporBoyu];
            rapor[1] = YazmaKimligi;
            rapor[2] = komut;
            Buffer.BlockCopy(parametreler, 0, rapor, 3, parametreler.Length);
            hid.Yaz(rapor);
            byte[] cevap = hid.Oku(CevapBekleme);
            return (cevap != null && cevap.Length > 2) ? cevap[2] : (byte)0xFF;
        }

        public void Dispose()
        {
            try { if (Bagli) DonanimModunaGec(); } catch (Exception) { }
            hid.Dispose();
        }
    }
}
