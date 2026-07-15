using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using CUOAssets = ClassicUO.Assets;

namespace Ultima
{
    public static class Art
    {
        public static Bitmap GetStatic(int itemID)
        {
            var artLoader = Files.Manager?.Arts;
            if (artLoader == null) return null;

            var file = artLoader.File;
            if (file == null) return null;

            ref var entry = ref file.GetValidRefEntry(itemID + 0x4000);
            if (entry.Equals(ClassicUO.IO.UOFileIndex.Invalid)) return null;

            var artFile = entry.File ?? file;

            // Check for compressed art (UOP)
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

            // Read the run-length encoded art data
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
