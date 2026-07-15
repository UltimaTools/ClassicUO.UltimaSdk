using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using SDColor = System.Drawing.Color;
using CUOAssets = ClassicUO.Assets;

namespace Ultima
{
    public sealed class Hue
    {
        private readonly int _index;
        private readonly CUOAssets.HuesLoader _loader;
        private ushort[] _colors;

        internal Hue(int index, CUOAssets.HuesLoader loader)
        {
            _index = index;
            _loader = loader;
        }

        public ushort[] Colors
        {
            get
            {
                if (_colors == null && _loader != null)
                {
                    _colors = new ushort[32];
                    var groups = _loader.HuesRange;
                    if (groups != null)
                    {
                        int groupIdx = _index / 8;
                        int entryIdx = _index % 8;
                        if (groupIdx < groups.Length)
                        {
                            var entries = groups[groupIdx].Entries;
                            var block = entries[entryIdx];
                            for (int i = 0; i < 32; i++)
                                _colors[i] = block.ColorTable[i];
                        }
                    }
                }
                return _colors ?? new ushort[32];
            }
        }

        public SDColor GetColor(int colorIndex)
        {
            try
            {
                uint rgba = GetColorRgba(colorIndex);
                byte r = (byte)(rgba & 0xFF);
                byte g = (byte)((rgba >> 8) & 0xFF);
                byte b = (byte)((rgba >> 16) & 0xFF);
                byte a = (byte)((rgba >> 24) & 0xFF);
                return SDColor.FromArgb(a, r, g, b);
            }
            catch
            {
                return SDColor.Gray;
            }
        }

        /// <summary>
        /// Returns the raw RGBA 32-bit color (0xAABBGGRR) for the given shade index.
        /// </summary>
        public uint GetColorRgba(int colorIndex)
        {
            return _loader.GetHueColorRgba8888((ushort)colorIndex, (ushort)(_index + 1));
        }

        public void ApplyTo(Bitmap bmp, bool onlyHueGrayPixels)
        {
            if (bmp == null || _loader == null || _index <= 0) return;

            try
            {
                var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                try
                {
                    int[] pixels = new int[bmp.Width * bmp.Height];
                    Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);

                    for (int i = 0; i < pixels.Length; i++)
                    {
                        int pixel = pixels[i];
                        byte a = (byte)((pixel >> 24) & 0xFF);
                        if (a == 0) continue;

                        byte r = (byte)((pixel >> 16) & 0xFF);
                        byte g = (byte)((pixel >> 8) & 0xFF);
                        byte b = (byte)(pixel & 0xFF);

                        if (onlyHueGrayPixels && (r != g || r != b))
                            continue;

                        // Map to 5-bit grayscale index
                        int grayIndex = (r * 31 + 128) / 255;
                        grayIndex = Math.Max(0, Math.Min(31, grayIndex));

                        uint hueColor = _loader.GetHueColorRgba8888((ushort)grayIndex, (ushort)(_index + 1));
                        byte hr = (byte)(hueColor & 0xFF);
                        byte hg = (byte)((hueColor >> 8) & 0xFF);
                        byte hb = (byte)((hueColor >> 16) & 0xFF);

                        pixels[i] = (a << 24) | (hr << 16) | (hg << 8) | hb;
                    }

                    Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
                }
                finally
                {
                    bmp.UnlockBits(data);
                }
            }
            catch
            {
            }
        }
    }

    public static class Hues
    {
        private static Hue[] _list;

        public static Hue[] List
        {
            get
            {
                if (_list == null)
                {
                    var loader = Files.Manager?.Hues;
                    if (loader != null)
                    {
                        _list = new Hue[3000];
                        for (int i = 0; i < 3000; i++)
                            _list[i] = new Hue(i, loader);
                    }
                }
                return _list;
            }
        }

        public static Hue GetHue(int index)
        {
            if (List != null && index >= 0 && index < List.Length)
                return List[index];

            return new Hue(index, Files.Manager?.Hues);
        }
    }
}
