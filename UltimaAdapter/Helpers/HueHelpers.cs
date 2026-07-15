using System.Drawing;

namespace Ultima.Helpers
{
    public static class HueHelpers
    {
        public static ushort ColorToHue(Color color)
        {
            const double scale = 31.0 / 255;

            ushort origRed = color.R;
            var newRed = (ushort)(origRed * scale);
            if (newRed == 0 && origRed != 0)
            {
                newRed = 1;
            }

            ushort origGreen = color.G;
            var newGreen = (ushort)(origGreen * scale);
            if (newGreen == 0 && origGreen != 0)
            {
                newGreen = 1;
            }

            ushort origBlue = color.B;
            var newBlue = (ushort)(origBlue * scale);
            if (newBlue == 0 && origBlue != 0)
            {
                newBlue = 1;
            }

            return (ushort)((newRed << 10) | (newGreen << 5) | newBlue);
        }

        public static Color HueToColor(ushort hue)
        {
            const int scale = 255 / 31;
            return Color.FromArgb(((hue & 0x7c00) >> 10) * scale, ((hue & 0x3e0) >> 5) * scale, (hue & 0x1f) * scale);
        }

        public static int HueToColorR(ushort hue)
        {
            return ((hue & 0x7c00) >> 10) * (255 / 31);
        }

        public static int HueToColorG(ushort hue)
        {
            return ((hue & 0x3e0) >> 5) * (255 / 31);
        }

        public static int HueToColorB(ushort hue)
        {
            return (hue & 0x1f) * (255 / 31);
        }
    }
}
