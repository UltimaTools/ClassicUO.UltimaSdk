using System;

namespace Ultima.Drawing
{
    public struct Color
    {
        public byte A;
        public byte R;
        public byte G;
        public byte B;

        public Color(byte r, byte g, byte b)
            : this(255, r, g, b)
        {
        }

        public Color(byte a, byte r, byte g, byte b)
        {
            A = a;
            R = r;
            G = g;
            B = b;
        }

        public static Color Transparent { get; } = new Color(0, 0, 0, 0);
        public static Color Gray { get; } = FromArgb(128, 128, 128);
        public static Color Black { get; } = FromArgb(0, 0, 0);
        public static Color White { get; } = FromArgb(255, 255, 255);

        public static Color FromArgb(int r, int g, int b)
        {
            return FromArgb(255, r, g, b);
        }

        public static Color FromArgb(int a, int r, int g, int b)
        {
            return new Color((byte)a, (byte)r, (byte)g, (byte)b);
        }

        public static Color FromArgb(int argb)
        {
            return new Color(
                (byte)((argb >> 24) & 0xFF),
                (byte)((argb >> 16) & 0xFF),
                (byte)((argb >> 8) & 0xFF),
                (byte)(argb & 0xFF));
        }

        public int ToArgb()
        {
            return (A << 24) | (R << 16) | (G << 8) | B;
        }
    }

    public struct Point
    {
        public int X;
        public int Y;

        public Point(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static Point Empty { get; } = new Point(0, 0);

        public static bool operator ==(Point a, Point b)
        {
            return a.X == b.X && a.Y == b.Y;
        }

        public static bool operator !=(Point a, Point b)
        {
            return !(a == b);
        }

        public override bool Equals(object obj)
        {
            return obj is Point p && this == p;
        }

        public override int GetHashCode()
        {
            return X ^ Y;
        }
    }

    public struct Rectangle
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;

        public Rectangle(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int Left => X;
        public int Top => Y;
        public int Right => X + Width;
        public int Bottom => Y + Height;
    }

    public class ImageFormat
    {
        public static ImageFormat Bmp { get; } = new ImageFormat();
        public static ImageFormat Tiff { get; } = new ImageFormat();
        public static ImageFormat Png { get; } = new ImageFormat();

        private ImageFormat()
        {
        }
    }
}
