using System;
using System.Collections.Generic;
using Ultima.Drawing;
using Ultima.Drawing.Imaging;
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
        private static FileIndex _fileIndex;
        private static bool _fileIndexTried;
        private static byte[] _streamBuffer;

        private static FileIndex GetArtFileIndex()
        {
            if (!_fileIndexTried)
            {
                _fileIndexTried = true;
                try
                {
                    _fileIndex = new FileIndex("Artidx.mul", "Art.mul", "artLegacyMUL.uop", 0x14000, 4, ".tga", 0x13FDC, false);
                }
                catch
                {
                    _fileIndex = null;
                }
            }
            return _fileIndex;
        }

        public static Bitmap GetStatic(int itemID)
        {
            var fi = GetArtFileIndex();
            if (fi == null) return null;

            int idx = itemID + 0x4000;
            Stream stream = fi.Seek(idx, out int length, out int _, out bool patched);
            if (stream == null) return null;

            return LoadStatic(stream, length);
        }

        private static unsafe Bitmap LoadStaticFromFile(ClassicUO.IO.FileReader artFile, int length)
        {
            if (_streamBuffer == null || _streamBuffer.Length < length)
                _streamBuffer = new byte[length];

            artFile.Read(new Span<byte>(_streamBuffer, 0, length));

            fixed (byte* data = _streamBuffer)
            {
                return DecodeStaticUshort(data, length);
            }
        }

        private static unsafe Bitmap LoadStatic(Stream stream, int length)
        {
            if (_streamBuffer == null || _streamBuffer.Length < length)
                _streamBuffer = new byte[length];

            stream.Read(_streamBuffer, 0, length);

            fixed (byte* data = _streamBuffer)
            {
                return DecodeStaticUshort(data, length);
            }
        }

        private static unsafe Bitmap DecodeStaticUshort(byte* data, int length)
        {
            var binData = (ushort*)data;
            int count = 2;
            int width = binData[count++];
            int height = binData[count++];

            if (width <= 0 || height <= 0 || width > 1024 || height > 1024)
                return null;

            var lookups = new int[height];
            int start = height + 4;

            for (int i = 0; i < height; ++i)
                lookups[i] = start + binData[count++];

            var bmp = new Bitmap(width, height, PixelFormat.Format16bppArgb1555);
            var bd = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format16bppArgb1555);

            var line = (ushort*)bd.Scan0;
            int delta = bd.Stride >> 1;

            for (int y = 0; y < height; ++y, line += delta)
            {
                count = lookups[y];
                var cur = line;

                while (true)
                {
                    int xOffset = binData[count++];
                    int xRun = binData[count++];
                    if (xOffset + xRun == 0)
                        break;

                    cur += xOffset;
                    var end = cur + xRun;
                    while (cur < end)
                        *cur++ = (ushort)(binData[count++] ^ 0x8000);
                }
            }

            bmp.UnlockBits(bd);
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
                {
                    _cache[index] = new Bitmap(44, 44, PixelFormat.Format32bppArgb);
                    return _cache[index];
                }

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
            if (itemId < 0) return 0;
            if (!checkMaxId) return (ushort)itemId;
            int max = GetMaxItemId();
            if (itemId > max) return 0;
            return (ushort)itemId;
        }

        public static ushort GetLegalItemId(ushort id)
        {
            return (ushort)GetLegalItemId((int)id);
        }

        public static int GetIdxLength()
        {
            var fi = GetArtFileIndex();
            if (fi?.IdxLength > 0)
                return (int)(fi.IdxLength / 12);
            return 0;
        }

        public static bool IsUOAHS()
        {
            return GetIdxLength() >= 0x13FDC;
        }

        public static int GetMaxItemId()
        {
            int len = GetIdxLength();
            if (len >= 0x13FDC)
                return 0xFFDC;
            if (len == 0xC000)
                return 0x7FFF;
            return 0x3FFF;
        }

        public static bool IsValidLand(int id)
        {
            return id >= 0 && id < 0x4000;
        }

        public static bool IsValidStatic(int id)
        {
            id = GetLegalItemId(id);
            id += 0x4000;

            if (_removed != null && id >= 0 && id < _removed.Length && _removed[id])
                return false;

            if (_cache != null && id >= 0 && id < _cache.Length && _cache[id] != null)
                return true;

            var fi = GetArtFileIndex();
            if (fi == null) return false;
            Stream stream = fi.Seek(id, out int length, out int _, out bool _);
            if (stream == null) return false;

            stream.Seek(4, SeekOrigin.Current);
            byte[] buf = new byte[4];
            stream.Read(buf, 0, 4);
            short width = (short)(buf[0] | (buf[1] << 8));
            short height = (short)(buf[2] | (buf[3] << 8));
            return width > 0 && height > 0;
        }

        public static void Reload()
        {
            _cache = new Bitmap[0x14000];
            _removed = new bool[0x14000];
            _patched.Clear();
            Modified = false;
            _fileIndex = null;
            _fileIndexTried = false;
            _streamBuffer = null;
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
