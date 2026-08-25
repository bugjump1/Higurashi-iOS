using System;
using System.Globalization;

namespace Higurashi.IOS.Playback
{
    public struct LayerFilterDefinition
    {
        public int Rr;
        public int Rg;
        public int Rb;
        public int Gr;
        public int Gg;
        public int Gb;
        public int Br;
        public int Bg;
        public int Bb;

        public bool IsIdentity => Rr == 256 && Rg == 0 && Rb == 0 &&
            Gr == 0 && Gg == 256 && Gb == 0 && Br == 0 && Bg == 0 && Bb == 256;
    }

    public static class LayerFilterPolicy
    {
        public static bool TryResolve(string value, out LayerFilterDefinition filter)
        {
            var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "":
                case "none":
                    filter = Identity();
                    return true;
                case "grayscale":
                    filter = Create(55, 185, 18, 55, 185, 18, 55, 185, 18);
                    return true;
                case "flashback":
                    filter = Create(117, 127, 39, 58, 171, 20, 69, 107, 40);
                    return true;
                case "night":
                    filter = Create(222, 0, 0, 0, 222, 0, 0, 0, 256);
                    return true;
                case "sunset":
                    filter = Create(250, 0, 0, 0, 210, 0, 0, 0, 180);
                    return true;
            }

            var parts = normalized.Split(',');
            var numbers = new int[parts.Length];
            if (parts.Length != 3 && parts.Length != 9)
            {
                filter = Identity();
                return false;
            }

            for (var i = 0; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i].Trim(), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out numbers[i]))
                {
                    filter = Identity();
                    return false;
                }
            }

            filter = parts.Length == 3
                ? Create(numbers[0], 0, 0, 0, numbers[1], 0, 0, 0, numbers[2])
                : Create(numbers[0], numbers[1], numbers[2], numbers[3], numbers[4],
                    numbers[5], numbers[6], numbers[7], numbers[8]);
            return true;
        }

        private static LayerFilterDefinition Identity()
        {
            return Create(256, 0, 0, 0, 256, 0, 0, 0, 256);
        }

        private static LayerFilterDefinition Create(int rr, int rg, int rb, int gr,
            int gg, int gb, int br, int bg, int bb)
        {
            return new LayerFilterDefinition
            {
                Rr = rr, Rg = rg, Rb = rb,
                Gr = gr, Gg = gg, Gb = gb,
                Br = br, Bg = bg, Bb = bb
            };
        }
    }
}
