using System;
using System.Globalization;
using System.Runtime.Serialization;

namespace K55Kontrol
{
    // Arayüzden ve çizim kütüphanelerinden bağımsız basit RGB rengi; ayar dosyasında "#RRGGBB" olarak saklanır.
    [DataContract]
    public struct Renk : IEquatable<Renk>
    {
        public byte R, G, B;

        public Renk(int r, int g, int b)
        {
            R = (byte)Sinirla(r);
            G = (byte)Sinirla(g);
            B = (byte)Sinirla(b);
        }

        public static readonly Renk Siyah = new Renk(0, 0, 0);

        [DataMember(Name = "renk", Order = 0)]
        private string HexVeri
        {
            get { return Hex; }
            set { Renk r; if (HexOku(value, out r)) this = r; }
        }

        public string Hex => string.Format("#{0:X2}{1:X2}{2:X2}", R, G, B);

        public static bool HexOku(string s, out Renk renk)
        {
            renk = Siyah;
            if (s == null) return false;
            s = s.Trim().TrimStart('#');
            if (s.Length == 3) s = new string(new[] { s[0], s[0], s[1], s[1], s[2], s[2] });
            int deger;
            if (s.Length != 6 || !int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out deger))
                return false;
            renk = new Renk((deger >> 16) & 0xFF, (deger >> 8) & 0xFF, deger & 0xFF);
            return true;
        }

        public Renk Olcekle(double k) => new Renk((int)(R * k + 0.5), (int)(G * k + 0.5), (int)(B * k + 0.5));

        public static Renk Karistir(Renk a, Renk b, double t) =>
            new Renk((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

        // ton: 0-1 arası (döngüsel), doygunluk ve parlaklık: 0-1
        public static Renk Hsv(double ton, double doygunluk = 1, double parlaklik = 1)
        {
            ton -= Math.Floor(ton);
            double h = ton * 6;
            int bolum = (int)h % 6;
            double f = h - Math.Floor(h);
            double v = parlaklik * 255;
            double p = v * (1 - doygunluk), q = v * (1 - doygunluk * f), u = v * (1 - doygunluk * (1 - f));
            switch (bolum)
            {
                case 0: return new Renk((int)v, (int)u, (int)p);
                case 1: return new Renk((int)q, (int)v, (int)p);
                case 2: return new Renk((int)p, (int)v, (int)u);
                case 3: return new Renk((int)p, (int)q, (int)v);
                case 4: return new Renk((int)u, (int)p, (int)v);
                default: return new Renk((int)v, (int)p, (int)q);
            }
        }

        public void HsvAl(out double ton, out double doygunluk, out double parlaklik)
        {
            double r = R / 255.0, g = G / 255.0, b = B / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
            parlaklik = max;
            doygunluk = max <= 0 ? 0 : d / max;
            if (d <= 0) ton = 0;
            else if (max == r) ton = ((g - b) / d) / 6;
            else if (max == g) ton = ((b - r) / d + 2) / 6;
            else ton = ((r - g) / d + 4) / 6;
            if (ton < 0) ton += 1;
        }

        public bool Equals(Renk o) => R == o.R && G == o.G && B == o.B;
        public override bool Equals(object o) => o is Renk && Equals((Renk)o);
        public override int GetHashCode() => (R << 16) | (G << 8) | B;
        public override string ToString() => Hex;

        private static int Sinirla(int n) => n < 0 ? 0 : (n > 255 ? 255 : n);
    }
}
