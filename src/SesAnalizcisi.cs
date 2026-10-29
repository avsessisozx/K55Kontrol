using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace K55Kontrol
{
    // Varsayılan ses çıkışını (hoparlör/kulaklık) WASAPI "loopback" ile dinler; mikrofon kullanılmaz.
    // Sesi 6 frekans bandına ayırır (bas → tiz) ve her bandın 0-1 arası seviyesini yayınlar.
    public sealed class SesAnalizcisi : IDisposable
    {
        public const int BantSayisi = K55.BolgeSayisi;

        private static readonly double[] BantSinirlari = { 20, 120, 300, 800, 2000, 5000, 16000 };   // Hz
        private const int FftBoyu = 2048;

        private Thread isParcacigi;
        private volatile bool calisiyor;
        private volatile float[] bantlar = new float[BantSayisi];
        private readonly float[] tepeler = new float[BantSayisi];
        private readonly float[] halka = new float[FftBoyu];
        private int halkaKonumu;
        private float[] kayanTampon = new float[4096];
        private short[] kisaTampon = new short[4096];

        public bool Calisiyor => calisiyor;

        // Ses aygıtı açılamadıysa nedeni; dinleme başarılıysa null.
        public volatile string SonHata;

        // Son ölçülen bant seviyeleri (0-1). Ses yoksa sıfıra iner.
        public float[] Bantlar => bantlar;

        public void Baslat()
        {
            if (calisiyor) return;
            calisiyor = true;
            isParcacigi = new Thread(Dongu) { IsBackground = true, Name = "Ses analizcisi" };
            isParcacigi.SetApartmentState(ApartmentState.MTA);
            isParcacigi.Start();
        }

        public void Durdur()
        {
            calisiyor = false;
            if (isParcacigi != null) isParcacigi.Join(1000);
            isParcacigi = null;
            bantlar = new float[BantSayisi];
        }

        public void Dispose() => Durdur();

        private void Dongu()
        {
            while (calisiyor)
            {
                try { Yakala(); }
                catch (Exception ex) { SonHata = ex.Message; }
                // Aygıt değişti ya da kayboldu: kısa bir beklemeden sonra varsayılan aygıtla yeniden başla.
                bantlar = new float[BantSayisi];
                for (int i = 0; i < 10 && calisiyor; i++) Thread.Sleep(100);
            }
        }

        private void Yakala()
        {
            var numaralandirici = (IMMDeviceEnumerator)new MMDeviceEnumeratorSinifi();
            IMMDevice aygit;
            Denetle(numaralandirici.GetDefaultAudioEndpoint(0 /* eRender */, 1 /* eMultimedia */, out aygit));
            Guid iid = typeof(IAudioClient).GUID;
            object o;
            Denetle(aygit.Activate(ref iid, 23 /* CLSCTX_ALL */, IntPtr.Zero, out o));
            IAudioClient istemci = (IAudioClient)o;

            IntPtr bicimPtr;
            Denetle(istemci.GetMixFormat(out bicimPtr));
            int kanallar = Marshal.ReadInt16(bicimPtr, 2);
            int ornekHizi = Marshal.ReadInt32(bicimPtr, 4);
            int bit = Marshal.ReadInt16(bicimPtr, 14);
            int etiket = (ushort)Marshal.ReadInt16(bicimPtr, 0);
            bool kayan = etiket == 3 || (etiket == 0xFFFE && Marshal.ReadInt32(bicimPtr, 24) == 3);

            try
            {
                Denetle(istemci.Initialize(0 /* paylaşımlı */, 0x00020000 /* LOOPBACK */, 2000000, 0, bicimPtr, IntPtr.Zero));
            }
            finally { Marshal.FreeCoTaskMem(bicimPtr); }

            Guid yakalayiciIid = typeof(IAudioCaptureClient).GUID;
            object y;
            Denetle(istemci.GetService(ref yakalayiciIid, out y));
            IAudioCaptureClient yakalayici = (IAudioCaptureClient)y;
            Denetle(istemci.Start());
            SonHata = null;

            try
            {
                int analizSayaci = 0;
                while (calisiyor)
                {
                    uint paket;
                    Denetle(yakalayici.GetNextPacketSize(out paket));
                    while (paket > 0)
                    {
                        IntPtr veri; uint kare, bayrak; ulong k1, k2;
                        Denetle(yakalayici.GetBuffer(out veri, out kare, out bayrak, out k1, out k2));
                        bool sessiz = (bayrak & 0x2) != 0;
                        int ornek = (int)kare * kanallar;
                        if (!sessiz)
                        {
                            if (kayan && bit == 32)
                            {
                                if (kayanTampon.Length < ornek) kayanTampon = new float[ornek];
                                Marshal.Copy(veri, kayanTampon, 0, ornek);
                            }
                            else if (bit == 16)
                            {
                                if (kisaTampon.Length < ornek) kisaTampon = new short[ornek];
                                Marshal.Copy(veri, kisaTampon, 0, ornek);
                            }
                        }
                        for (int i = 0; i < kare; i++)
                        {
                            float toplam = 0;
                            if (!sessiz)
                                for (int c = 0; c < kanallar; c++)
                                {
                                    int s = i * kanallar + c;
                                    toplam += kayan && bit == 32 ? kayanTampon[s] : (bit == 16 ? kisaTampon[s] / 32768f : 0f);
                                }
                            halka[halkaKonumu] = toplam / kanallar;
                            halkaKonumu = (halkaKonumu + 1) % FftBoyu;
                        }
                        Denetle(yakalayici.ReleaseBuffer(kare));
                        Denetle(yakalayici.GetNextPacketSize(out paket));
                    }
                    Thread.Sleep(10);
                    if (++analizSayaci % 2 == 0) Analiz(ornekHizi);
                }
            }
            finally
            {
                istemci.Stop();
                Marshal.ReleaseComObject(yakalayici);
                Marshal.ReleaseComObject(istemci);
                Marshal.ReleaseComObject(aygit);
                Marshal.ReleaseComObject(numaralandirici);
            }
        }

        private readonly double[] gercek = new double[FftBoyu];
        private readonly double[] sanal = new double[FftBoyu];

        private void Analiz(int ornekHizi)
        {
            // Hann pencereli FFT
            for (int i = 0; i < FftBoyu; i++)
            {
                double pencere = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (FftBoyu - 1));
                gercek[i] = halka[(halkaKonumu + i) % FftBoyu] * pencere;
                sanal[i] = 0;
            }
            Fft(gercek, sanal);

            float[] yeni = new float[BantSayisi];
            float[] seviyeler = new float[BantSayisi];
            double binHz = (double)ornekHizi / FftBoyu;
            double toplamSeviye = 0;
            float enYuksekTepe = 0;
            for (int b = 0; b < BantSayisi; b++)
            {
                int bas = Math.Max(1, (int)(BantSinirlari[b] / binHz));
                int son = Math.Min(FftBoyu / 2 - 1, Math.Max(bas, (int)(BantSinirlari[b + 1] / binHz)));
                double e = 0;
                for (int k = bas; k <= son; k++) e += gercek[k] * gercek[k] + sanal[k] * sanal[k];
                seviyeler[b] = (float)Math.Sqrt(e / (son - bas + 1));
                toplamSeviye += seviyeler[b];
                // Her bandın tepe değeri yavaşça düşer (otomatik kazanç).
                tepeler[b] = Math.Max(seviyeler[b], tepeler[b] * 0.996f);
                enYuksekTepe = Math.Max(enYuksekTepe, tepeler[b]);
            }
            for (int b = 0; b < BantSayisi; b++)
            {
                // Bant kendi tepesine göre ölçeklenir; ama en güçlü bandın çeyreğinden küçük bir tepe kabul edilmez,
                // yoksa neredeyse sessiz bir banttaki gürültü de tam parlaklığa çıkar.
                float tepe = Math.Max(Math.Max(tepeler[b], enYuksekTepe * 0.25f), 0.02f);
                yeni[b] = Math.Min(1f, seviyeler[b] / tepe);
            }
            if (toplamSeviye < 0.002) Array.Clear(yeni, 0, yeni.Length);
            bantlar = yeni;
        }

        // Yerinde radix-2 FFT.
        private static void Fft(double[] re, double[] im)
        {
            int n = re.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bitler = n >> 1;
                for (; (j & bitler) != 0; bitler >>= 1) j ^= bitler;
                j ^= bitler;
                if (i < j)
                {
                    double t = re[i]; re[i] = re[j]; re[j] = t;
                    t = im[i]; im[i] = im[j]; im[j] = t;
                }
            }
            for (int boy = 2; boy <= n; boy <<= 1)
            {
                double aci = -2 * Math.PI / boy;
                double wr = Math.Cos(aci), wi = Math.Sin(aci);
                for (int i = 0; i < n; i += boy)
                {
                    double cr = 1, ci = 0;
                    for (int j = 0; j < boy / 2; j++)
                    {
                        int a = i + j, b = i + j + boy / 2;
                        double tr = re[b] * cr - im[b] * ci, ti = re[b] * ci + im[b] * cr;
                        re[b] = re[a] - tr; im[b] = im[a] - ti;
                        re[a] += tr; im[a] += ti;
                        double ncr = cr * wr - ci * wi;
                        ci = cr * wi + ci * wr;
                        cr = ncr;
                    }
                }
            }
        }

        private static void Denetle(int hr)
        {
            if (hr < 0) Marshal.ThrowExceptionForHR(hr);
        }

        // ---- WASAPI COM arayüzleri (yalnızca kullanılan yöntemler; sıra vtable ile aynı olmalı) ----

        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumeratorSinifi { }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int akis, int durum, out IntPtr aygitlar);
            [PreserveSig] int GetDefaultAudioEndpoint(int akis, int rol, out IMMDevice aygit);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid, int baglam, IntPtr parametreler, [MarshalAs(UnmanagedType.IUnknown)] out object arayuz);
        }

        [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioClient
        {
            [PreserveSig] int Initialize(int paylasim, int bayraklar, long tamponSuresi, long donem, IntPtr bicim, IntPtr oturum);
            [PreserveSig] int GetBufferSize(out uint boy);
            [PreserveSig] int GetStreamLatency(out long gecikme);
            [PreserveSig] int GetCurrentPadding(out uint dolgu);
            [PreserveSig] int IsFormatSupported(int paylasim, IntPtr bicim, out IntPtr yakin);
            [PreserveSig] int GetMixFormat(out IntPtr bicim);
            [PreserveSig] int GetDevicePeriod(out long varsayilan, out long enKucuk);
            [PreserveSig] int Start();
            [PreserveSig] int Stop();
            [PreserveSig] int Reset();
            [PreserveSig] int SetEventHandle(IntPtr olay);
            [PreserveSig] int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object hizmet);
        }

        [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioCaptureClient
        {
            [PreserveSig] int GetBuffer(out IntPtr veri, out uint kare, out uint bayrak, out ulong konum, out ulong zaman);
            [PreserveSig] int ReleaseBuffer(uint kare);
            [PreserveSig] int GetNextPacketSize(out uint kare);
        }
    }
}
