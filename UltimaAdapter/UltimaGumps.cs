using System;
using System.Collections.Generic;
using Ultima.Drawing;
using Ultima.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace Ultima
{
    public static class Gumps
    {
        public static bool Modified;

        private static Bitmap[] _cache = new Bitmap[0x10000];
        private static bool[] _removed = new bool[0x10000];
        private static readonly Dictionary<int, bool> _patched = new Dictionary<int, bool>();
        private static FileIndex _fileIndex;
        private static bool _fileIndexTried;

        private static FileIndex GetGumpFileIndex()
        {
            if (!_fileIndexTried)
            {
                _fileIndexTried = true;
                try
                {
                    _fileIndex = new FileIndex("Gumpidx.mul", "Gumpart.mul", "gumpartLegacyMUL.uop", 0x10000, 6, ".tga", -1, true);
                }
                catch
                {
                    _fileIndex = null;
                }
            }
            return _fileIndex;
        }

        public static byte[] GetRawGump(int index, out int width, out int height)
        {
            var gumps = Files.Manager?.Gumps;
            if (gumps != null)
            {
                try
                {
                    var info = gumps.GetGump((uint)index);
                    if (!info.Pixels.IsEmpty)
                    {
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
                }
                catch
                {
                }
            }

            var fi = GetGumpFileIndex();
            if (fi == null || !fi.Valid(index, out int fiLength, out int fiExtra, out _))
            {
                width = height = 0;
                return null;
            }

            Stream fiStream = fi.Seek(index, out fiLength, out fiExtra, out _);
            if (fiStream == null)
            {
                width = height = 0;
                return null;
            }

            width = height = 0;
            if (fiExtra > 0)
            {
                width = fiExtra & 0xFFFF;
                height = (fiExtra >> 16) & 0xFFFF;
                var result = new byte[fiLength];
                fiStream.Read(result, 0, fiLength);
                return result;
            }

            return null;
        }

        public static int GetCount()
        {
            var gumps = Files.Manager?.Gumps;
            if (gumps?.File?.Entries != null)
                return gumps.File.Entries.Length;

            var fi = GetGumpFileIndex();
            if (fi?.FileAccessor?.IndexLength > 0)
                return fi.FileAccessor.IndexLength;

            return 0;
        }

        public static bool IsValidIndex(int index)
        {
            var file = Files.Manager?.Gumps?.File;
            if (file != null)
            {
                if (index >= 0 && index < file.Entries.Length)
                {
                    ref var entry = ref file.GetValidRefEntry(index);
                    if (!entry.Equals(ClassicUO.IO.UOFileIndex.Invalid))
                        return true;
                }
            }

            var fi = GetGumpFileIndex();
            return fi != null && fi.Valid(index, out _, out _, out _);
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
            if (gumps != null)
            {
                try
                {
                    var info = gumps.GetGump((uint)index);
                    if (!info.Pixels.IsEmpty && info.Width > 0 && info.Height > 0)
                    {
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
                }
                catch
                {
                }
            }

            var fi = GetGumpFileIndex();
            if (fi == null) return null;

            if (!fi.Valid(index, out int fiLength, out int fiExtra, out bool fiPatched))
                return null;

            Stream fiStream = fi.Seek(index, out fiLength, out fiExtra, out fiPatched);
            if (fiStream == null) return null;
            patched = fiPatched;

            var fiEntry = fi[index];
            if (fiEntry.Flag == CompressionFlag.Zlib)
            {
                byte[] compressed = new byte[fiLength];
                fiStream.Read(compressed, 0, fiLength);
                byte[] decompressed = new byte[fiEntry.DecompressedLength];
                ClassicUO.Utility.ZLibManaged.Decompress(compressed, 0, compressed.Length, 0, decompressed, decompressed.Length);

                int width = fiEntry.Extra & 0xFFFF;
                int height = (fiEntry.Extra >> 16) & 0xFFFF;

                if (width <= 0 || height <= 0) return null;

                var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                var bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                try
                {
                    var pixels = new int[width * height];
                    for (int i = 0; i < pixels.Length && i * 4 + 3 < decompressed.Length; i++)
                    {
                        byte b = decompressed[i * 4];
                        byte g = decompressed[i * 4 + 1];
                        byte r = decompressed[i * 4 + 2];
                        byte a = decompressed[i * 4 + 3];
                        pixels[i] = (a << 24) | (r << 16) | (g << 8) | b;
                    }
                    Marshal.Copy(pixels, 0, bmpData.Scan0, pixels.Length);
                }
                finally
                {
                    bmp.UnlockBits(bmpData);
                }

                _cache[index] = bmp;
                return bmp;
            }

            if (fiExtra > 0)
            {
                int width = fiExtra & 0xFFFF;
                int height = (fiExtra >> 16) & 0xFFFF;

                if (width > 0 && height > 0)
                {
                    var bmp = new Bitmap(width, height, PixelFormat.Format16bppRgb555);
                    var bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format16bppRgb555);

                    try
                    {
                        byte[] rawData = new byte[fiLength];
                        fiStream.Read(rawData, 0, fiLength);
                        Marshal.Copy(rawData, 0, bmpData.Scan0, Math.Min(rawData.Length, Math.Abs(bmpData.Stride) * height));
                    }
                    finally
                    {
                        bmp.UnlockBits(bmpData);
                    }

                    _cache[index] = bmp;
                    return bmp;
                }
            }

            return null;
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
