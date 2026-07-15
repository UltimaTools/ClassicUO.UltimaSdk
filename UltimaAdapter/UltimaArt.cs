using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using CUOAssets = ClassicUO.Assets;

namespace Ultima
{
    public static class Art
    {
        public static bool Modified;
        public static readonly Bitmap Empty = new Bitmap(1, 1);

        private static Bitmap[] _cache = new Bitmap[0x14000];
        private static bool[] _removed = new bool[0x14000];
        private static readonly Dictionary<int, bool> _patched = new Dictionary<int, bool>();

        public static Bitmap GetStatic(int itemID)
        {
            var artLoader = Files.Manager?.Arts;
            if (artLoader == null) return null;

            var file = artLoader.File;
            if (file == null) return null;

            ref var entry = ref file.GetValidRefEntry(itemID + 0x4000);
            if (entry.Equals(ClassicUO.IO.UOFileIndex.Invalid)) return null;

            var artFile = entry.File ?? file;

            if (entry.CompressionFlag != ClassicUO.IO.CompressionType.None)
            {
                artFile.Seek(entry.Offset, SeekOrigin.Begin);
                byte[] buf = new byte[entry.Length];
                artFile.Read(buf);

                byte[] decompressed;
                if (entry.CompressionFlag == ClassicUO.IO.CompressionType.Zlib)
                {
                    decompressed = new byte[entry.DecompressedLength];
                    ClassicUO.Utility.ZLibManaged.Decompress(buf, 0, buf.Length, 0, decompressed, decompressed.Length);
                }
                else
                {
                    decompressed = buf;
                }

                return DecodeStaticArt(decompressed);
            }

            artFile.Seek(entry.Offset, SeekOrigin.Begin);
            int width = artFile.ReadInt16();
            int height = artFile.ReadInt16();

            if (width <= 0 || height <= 0 || width > 1024 || height > 1024)
                return null;

            var lookups = new int[height];
            for (int i = 0; i < height; i++)
                lookups[i] = artFile.ReadInt32();

            long dataStart = artFile.Position;

            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            var bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            try
            {
                var pixels = new int[width * height];

                for (int y = 0; y < height; y++)
                {
                    artFile.Seek(dataStart + lookups[y], SeekOrigin.Begin);
                    int x = 0;

                    while (x < width)
                    {
                        int offset = artFile.ReadUInt16();
                        int run = artFile.ReadUInt16();

                        if (offset + run == 0)
                            break;

                        x += offset;

                        for (int i = 0; i < run; i++)
                        {
                            ushort val = artFile.ReadUInt16();
                            int a = (val >> 15) * 255;
                            int r = ((val >> 10) & 0x1F) * 255 / 31;
                            int g = ((val >> 5) & 0x1F) * 255 / 31;
                            int b = (val & 0x1F) * 255 / 31;
                            pixels[y * width + x] = (a << 24) | (r << 16) | (g << 8) | b;
                            x++;
                        }
                    }
                }

                Marshal.Copy(pixels, 0, bmpData.Scan0, pixels.Length);
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }

            return bmp;
        }

        public static Bitmap GetStatic(int index, out bool patched, bool checkMaxId = true)
        {
            patched = false;
            if (checkMaxId && index > GetMaxItemId())
                return null;
            return GetStatic(index);
        }

        public static Bitmap GetLand(int index)
        {
            return GetLand(index, out bool _);
        }

        public static Bitmap GetLand(int index, out bool patched)
        {
            index &= 0x3FFF;
            patched = _patched.ContainsKey(index) && _patched[index];

            if (_removed[index])
                return null;

            if (_cache[index] != null)
                return _cache[index];

            var artLoader = Files.Manager?.Arts;
            if (artLoader == null) return null;

            try
            {
                var info = artLoader.GetArt((uint)index);
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

        public static ushort GetLegalItemId(int itemId, bool checkMaxId = true)
        {
            if (itemId < 0 || itemId > 0x3FFF)
                return 0;
            return (ushort)itemId;
        }

        public static ushort GetLegalItemId(ushort id)
        {
            return (ushort)GetLegalItemId((int)id);
        }

        public static int GetIdxLength()
        {
            var artLoader = Files.Manager?.Arts;
            if (artLoader?.File?.Entries != null)
                return artLoader.File.Entries.Length;
            return 0;
        }

        public static bool IsUOAHS()
        {
            return GetIdxLength() >= 0x13FDC;
        }

        public static int GetMaxItemId()
        {
            var artLoader = Files.Manager?.Arts;
            if (artLoader?.File != null)
                return artLoader.File.Entries.Length - 0x4000;
            return 0x3FFF;
        }

        public static bool IsValidLand(int id)
        {
            return id >= 0 && id < 0x4000;
        }

        public static bool IsValidStatic(int id)
        {
            if (id < 0x4000)
                return false;

            int staticIdx = id - 0x4000;
            var artLoader = Files.Manager?.Arts;
            if (artLoader?.File != null && staticIdx < artLoader.File.Entries.Length)
            {
                ref var entry = ref artLoader.File.GetValidRefEntry(id);
                return !entry.Equals(ClassicUO.IO.UOFileIndex.Invalid);
            }

            return false;
        }

        public static void Reload()
        {
            _cache = new Bitmap[0x14000];
            _removed = new bool[0x14000];
            _patched.Clear();
            Modified = false;
        }

        public static void ReplaceStatic(int index, Bitmap bmp)
        {
            index &= 0x3FFF;
            index += 0x4000;
            _cache[index] = bmp;
            _removed[index] = false;
            Modified = true;
        }

        public static void ReplaceLand(int index, Bitmap bmp)
        {
            index &= 0x3FFF;
            _cache[index] = bmp;
            _removed[index] = false;
            Modified = true;
        }

        public static void RemoveStatic(int index)
        {
            index &= 0x3FFF;
            index += 0x4000;
            _removed[index] = true;
            _cache[index] = null;
            Modified = true;
        }

        public static void RemoveLand(int index)
        {
            index &= 0x3FFF;
            _removed[index] = true;
            _cache[index] = null;
            Modified = true;
        }

        public static unsafe void Measure(Bitmap bmp, out int xMin, out int yMin, out int xMax, out int yMax)
        {
            xMin = yMin = 0;
            xMax = yMax = -1;

            if (bmp == null || bmp.Width <= 0 || bmp.Height <= 0)
                return;

            var bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            try
            {
                int delta = bmpData.Stride / 4;
                int* line = (int*)bmpData.Scan0;

                xMin = bmp.Width;
                xMax = 0;

                for (int y = 0; y < bmp.Height; y++)
                {
                    for (int x = 0; x < bmp.Width; x++)
                    {
                        if ((line[x] & 0xFF000000) != 0)
                        {
                            if (x < xMin) xMin = x;
                            if (x > xMax) xMax = x;
                            if (y < yMin) yMin = y;
                            if (y > yMax) yMax = y;
                        }
                    }

                    line += delta;
                }
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }
        }

        public static void Save(string path)
        {
            throw new NotSupportedException("Art.Save is not supported in ClassicUO.UltimaSdk adapter");
        }

        private static Bitmap DecodeStaticArt(byte[] data)
        {
            if (data.Length < 4) return null;

            int width = BitConverter.ToInt16(data, 0);
            int height = BitConverter.ToInt16(data, 2);

            if (width <= 0 || height <= 0 || width > 1024 || height > 1024)
                return null;

            var lookups = new int[height];
            int offset = 4;
            for (int i = 0; i < height && offset + 4 <= data.Length; i++, offset += 4)
                lookups[i] = BitConverter.ToInt32(data, offset);

            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            var bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            try
            {
                var pixels = new int[width * height];

                for (int y = 0; y < height; y++)
                {
                    int pos = lookups[y];
                    int x = 0;

                    while (x < width && pos + 4 <= data.Length)
                    {
                        int xoff = BitConverter.ToUInt16(data, pos); pos += 2;
                        int run = BitConverter.ToUInt16(data, pos); pos += 2;

                        if (xoff + run == 0) break;
                        x += xoff;

                        for (int i = 0; i < run && pos + 2 <= data.Length; i++, pos += 2)
                        {
                            ushort val = BitConverter.ToUInt16(data, pos);
                            int a = (val >> 15) * 255;
                            int r = ((val >> 10) & 0x1F) * 255 / 31;
                            int g = ((val >> 5) & 0x1F) * 255 / 31;
                            int b = (val & 0x1F) * 255 / 31;
                            if (x < width)
                                pixels[y * width + x] = (a << 24) | (r << 16) | (g << 8) | b;
                            x++;
                        }
                    }
                }

                Marshal.Copy(pixels, 0, bmpData.Scan0, pixels.Length);
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }

            return bmp;
        }
    }
}
