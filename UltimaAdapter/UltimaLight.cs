using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Ultima
{
    public sealed class Light
    {
        private static FileIndex _fileIndex = new FileIndex("lightidx.mul", "light.mul", 100, -1);
        private static Bitmap[] _cache = new Bitmap[100];
        private static bool[] _removed = new bool[100];
        private static byte[] _streamBuffer;

        public static void Reload()
        {
            _fileIndex = new FileIndex("lightidx.mul", "light.mul", 100, -1);
            _cache = new Bitmap[100];
            _removed = new bool[100];
        }

        public static int GetCount()
        {
            string idxPath = Files.GetFilePath("lightidx.mul");
            if (idxPath == null)
                return 0;

            using (var index = new FileStream(idxPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return (int)(index.Length / 12);
            }
        }

        public static bool TestLight(int index)
        {
            if (_removed[index])
                return false;

            if (_cache[index] != null)
                return true;

            Stream stream = _fileIndex.Seek(index, out int _, out int extra, out bool _);

            if (stream == null)
                return false;

            stream.Close();

            int width = (extra & 0xFFFF);
            int height = ((extra >> 16) & 0xFFFF);

            return (width > 0) && (height > 0);
        }

        public static void Remove(int index)
        {
            _removed[index] = true;
        }

        public static void Replace(int index, Bitmap bmp)
        {
            _cache[index] = bmp;
            _removed[index] = false;
        }

        public static byte[] GetRawLight(int index, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (_removed[index])
                return null;

            Stream stream = _fileIndex.Seek(index, out int length, out int extra, out bool _);

            if (stream == null)
                return null;

            width = (extra & 0xFFFF);
            height = ((extra >> 16) & 0xFFFF);
            var buffer = new byte[length];
            _ = stream.Read(buffer, 0, length);
            stream.Close();

            return buffer;
        }

        public static unsafe Bitmap GetLight(int index)
        {
            if (_removed[index])
                return null;

            if (_cache[index] != null)
                return _cache[index];

            Stream stream = _fileIndex.Seek(index, out int length, out int extra, out bool _);

            if (stream == null)
                return null;

            int width = (extra & 0xFFFF);
            int height = ((extra >> 16) & 0xFFFF);

            if (_streamBuffer == null || _streamBuffer.Length < length)
                _streamBuffer = new byte[length];

            _ = stream.Read(_streamBuffer, 0, length);

            var bmp = new Bitmap(width, height, PixelFormat.Format16bppArgb1555);
            BitmapData bd = bmp.LockBits(
                new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format16bppArgb1555);

            var line = (ushort*)bd.Scan0;
            int delta = bd.Stride >> 1;

            fixed (byte* data = _streamBuffer)
            {
                var bindat = (sbyte*)data;
                for (int y = 0; y < height; ++y, line += delta)
                {
                    ushort* cur = line;
                    ushort* end = cur + width;

                    while (cur < end)
                    {
                        sbyte value = *bindat++;
                        *cur++ = (ushort)(((0x1f + value) << 10) + ((0x1F + value) << 5) + (0x1F + value));
                    }
                }
            }

            bmp.UnlockBits(bd);
            stream.Close();
            if (!Files.CacheData)
                return _cache[index] = bmp;

            return bmp;
        }

        public static unsafe void Save(string path)
        {
            string idx = System.IO.Path.Combine(path, "lightidx.mul");
            string mul = System.IO.Path.Combine(path, "light.mul");

            using (var fsidx = new FileStream(idx, FileMode.Create, FileAccess.Write, FileShare.Write))
            using (var fsmul = new FileStream(mul, FileMode.Create, FileAccess.Write, FileShare.Write))
            using (var binidx = new BinaryWriter(fsidx))
            using (var binmul = new BinaryWriter(fsmul))
            {
                for (int index = 0; index < _cache.Length; index++)
                {
                    if (_cache[index] == null)
                        _cache[index] = GetLight(index);

                    Bitmap bmp = _cache[index];

                    if ((bmp == null) || (_removed[index]))
                    {
                        binidx.Write(-1);
                        binidx.Write(0);
                        binidx.Write(0);
                    }
                    else
                    {
                        BitmapData bd = bmp.LockBits(
                            new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly,
                            PixelFormat.Format16bppArgb1555);
                        var line = (ushort*)bd.Scan0;
                        int delta = bd.Stride >> 1;

                        binidx.Write((int)fsmul.Position);
                        var length = (int)fsmul.Position;

                        for (int y = 0; y < bmp.Height; ++y, line += delta)
                        {
                            ushort* cur = line;
                            ushort* end = cur + bmp.Width;
                            while (cur < end)
                            {
                                ushort ccur = *cur++;
                                sbyte value = 0;

                                if (ccur > 0)
                                    value = (sbyte)(((ccur >> 10) & 0xffff) - 0x1f);

                                if (value > 0)
                                    --value;

                                binmul.Write(value);
                            }
                        }

                        length = (int)fsmul.Position - length;
                        binidx.Write(length);
                        binidx.Write((bmp.Height << 16) + bmp.Width);
                        bmp.UnlockBits(bd);
                    }
                }
            }
        }
    }
}