using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Ultima
{
    public static class Gumps
    {
        public static bool Modified;

        private static Bitmap[] _cache = new Bitmap[0x10000];
        private static bool[] _removed = new bool[0x10000];
        private static readonly Dictionary<int, bool> _patched = new Dictionary<int, bool>();

        public static byte[] GetRawGump(int index, out int width, out int height)
        {
            var gumps = Files.Manager?.Gumps;
            if (gumps == null)
            {
                width = height = 0;
                return null;
            }

            try
            {
                var info = gumps.GetGump((uint)index);
                width = info.Width;
                height = info.Height;

                var result = new byte[info.Pixels.Length * 4];
                for (int i = 0; i < info.Pixels.Length; i++)
                {
                    uint pixel = info.Pixels[i];
                    result[i * 4] = (byte)(pixel & 0xFF);
                    result[i * 4 + 1] = (byte)((pixel >> 8) & 0xFF);
                    result[i * 4 + 2] = (byte)((pixel >> 16) & 0xFF);
                    result[i * 4 + 3] = (byte)((pixel >> 24) & 0xFF);
                }
                return result;
            }
            catch
            {
                width = height = 0;
                return null;
            }
        }

        public static int GetCount()
        {
            var gumps = Files.Manager?.Gumps;
            if (gumps?.File?.Entries != null)
                return gumps.File.Entries.Length;
            return 0;
        }

        public static bool IsValidIndex(int index)
        {
            var file = Files.Manager?.Gumps?.File;
            if (file == null) return false;
            if (index < 0 || index >= file.Entries.Length) return false;
            ref var entry = ref file.GetValidRefEntry(index);
            return !entry.Equals(ClassicUO.IO.UOFileIndex.Invalid);
        }

        public static Bitmap GetGump(int index)
        {
            return GetGump(index, out bool _);
        }

        public static unsafe Bitmap GetGump(int index, out bool patched)
        {
            patched = _patched.ContainsKey(index) && _patched[index];

            if (_removed[index])
                return null;

            if (_cache[index] != null)
                return _cache[index];

            var gumps = Files.Manager?.Gumps;
            if (gumps == null) return null;

            try
            {
                var info = gumps.GetGump((uint)index);
                if (info.Pixels.IsEmpty || info.Width <= 0 || info.Height <= 0)
                    return null;

                var bmp = new Bitmap(info.Width, info.Height, PixelFormat.Format32bppArgb);
                var bmpData = bmp.LockBits(new Rectangle(0, 0, info.Width, info.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                try
                {
                    var pixels = new int[info.Width * info.Height];
                    for (int i = 0; i < pixels.Length && i < info.Pixels.Length; i++)
                        pixels[i] = (int)info.Pixels[i];
                    Marshal.Copy(pixels, 0, bmpData.Scan0, pixels.Length);
                }
                finally
                {
                    bmp.UnlockBits(bmpData);
                }

                _cache[index] = bmp;
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        public static unsafe Bitmap GetGump(int index, Hue hue, bool onlyHueGrayPixels, out bool patched)
        {
            var bmp = GetGump(index, out patched);
            if (bmp == null || hue == null)
                return bmp;

            hue.ApplyTo(bmp, onlyHueGrayPixels);
            return bmp;
        }

        public static void Reload()
        {
            _cache = new Bitmap[0x10000];
            _removed = new bool[0x10000];
            _patched.Clear();
            Modified = false;
        }

        public static void ReplaceGump(int index, Bitmap bmp)
        {
            _cache[index] = bmp;
            _removed[index] = false;
            Modified = true;
        }

        public static void RemoveGump(int index)
        {
            _removed[index] = true;
            _cache[index] = null;
            Modified = true;
        }

        public static void Save(string path)
        {
            throw new NotSupportedException("Gumps.Save is not supported in ClassicUO.UltimaSdk adapter");
        }
    }
}
