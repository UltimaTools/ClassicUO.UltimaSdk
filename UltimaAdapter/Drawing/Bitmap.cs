using System;
using System.IO;
using System.Runtime.InteropServices;
using Ultima.Drawing.Imaging;

namespace Ultima.Drawing
{
    public class Bitmap : IDisposable
    {
        public int Width { get; }
        public int Height { get; }
        public PixelFormat PixelFormat { get; }

        internal byte[] _pixels;
        private BitmapData _activeLock;

        public Bitmap(int width, int height)
            : this(width, height, PixelFormat.Format32bppArgb)
        {
        }

        public Bitmap(int width, int height, PixelFormat pixelFormat)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height));

            Width = width;
            Height = height;
            PixelFormat = pixelFormat;
            _pixels = new byte[width * height * (GetBitsPerPixel(pixelFormat) / 8)];
        }

        public Bitmap(Bitmap source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            Width = source.Width;
            Height = source.Height;
            PixelFormat = source.PixelFormat;
            _pixels = new byte[source._pixels.Length];
            Array.Copy(source._pixels, _pixels, _pixels.Length);
        }

        public Bitmap Clone() => new Bitmap(this);

        public BitmapData LockBits(Rectangle rect, ImageLockMode mode, PixelFormat format)
        {
            if (_activeLock != null)
                throw new InvalidOperationException("Bitmap is already locked.");

            int bpp = GetBitsPerPixel(format) / 8;
            int stride = Width * bpp;

            if (format == PixelFormat)
            {
                // Fast path: pin the native buffer directly.
                var handle = GCHandle.Alloc(_pixels, GCHandleType.Pinned);

                _activeLock = new BitmapData
                {
                    Scan0 = handle.AddrOfPinnedObject(),
                    Stride = stride,
                    Width = rect.Width,
                    Height = rect.Height,
                    PixelFormat = format,
                    Handle = handle,
                    Buffer = _pixels,
                    Owner = this,
                    IsTempBuffer = false
                };
            }
            else
            {
                // Slow path: allocate a temp buffer in the requested format and convert.
                byte[] temp = new byte[Width * Height * bpp];

                if (mode != ImageLockMode.WriteOnly)
                {
                    ConvertToFormat(_pixels, PixelFormat, temp, format, Width, Height);
                }

                var handle = GCHandle.Alloc(temp, GCHandleType.Pinned);

                _activeLock = new BitmapData
                {
                    Scan0 = handle.AddrOfPinnedObject(),
                    Stride = stride,
                    Width = rect.Width,
                    Height = rect.Height,
                    PixelFormat = format,
                    Handle = handle,
                    Buffer = temp,
                    Owner = this,
                    IsTempBuffer = true
                };
            }

            return _activeLock;
        }

        public void UnlockBits(BitmapData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (data.Owner != this)
                throw new ArgumentException("BitmapData does not belong to this Bitmap.");

            if (data.IsTempBuffer)
            {
                // Convert temp buffer back to native format.
                ConvertToFormat(data.Buffer, data.PixelFormat, _pixels, PixelFormat, Width, Height);
            }

            if (_activeLock == data)
            {
                _activeLock.Dispose();
                _activeLock = null;
            }
            else
            {
                data.Dispose();
            }
        }

        private static unsafe void ConvertToFormat(byte[] src, PixelFormat srcFormat, byte[] dst, PixelFormat dstFormat, int width, int height)
        {
            int count = width * height;

            fixed (byte* srcPtr = src)
            fixed (byte* dstPtr = dst)
            {
                if (srcFormat == PixelFormat.Format32bppArgb &&
                    (dstFormat == PixelFormat.Format16bppArgb1555 || dstFormat == PixelFormat.Format16bppRgb555))
                {
                    int* srcPixels = (int*)srcPtr;
                    ushort* dstPixels = (ushort*)dstPtr;
                    bool hasAlpha = dstFormat == PixelFormat.Format16bppArgb1555;

                    for (int i = 0; i < count; i++)
                    {
                        int val = srcPixels[i];
                        byte a = (byte)((val >> 24) & 0xFF);
                        byte r = (byte)((val >> 16) & 0xFF);
                        byte g = (byte)((val >> 8) & 0xFF);
                        byte b = (byte)(val & 0xFF);

                        ushort r5 = (ushort)((r * 31 + 127) / 255);
                        ushort g5 = (ushort)((g * 31 + 127) / 255);
                        ushort b5 = (ushort)((b * 31 + 127) / 255);

                        if (hasAlpha)
                            dstPixels[i] = (ushort)((a >= 128 ? 0x8000 : 0) | (r5 << 10) | (g5 << 5) | b5);
                        else
                            dstPixels[i] = (ushort)((r5 << 10) | (g5 << 5) | b5);
                    }
                }
                else if ((srcFormat == PixelFormat.Format16bppArgb1555 || srcFormat == PixelFormat.Format16bppRgb555) &&
                         dstFormat == PixelFormat.Format32bppArgb)
                {
                    ushort* srcPixels = (ushort*)srcPtr;
                    int* dstPixels = (int*)dstPtr;
                    bool hasAlpha = srcFormat == PixelFormat.Format16bppArgb1555;

                    for (int i = 0; i < count; i++)
                    {
                        ushort val = srcPixels[i];

                        byte a = hasAlpha ? (byte)(((val >> 15) != 0) ? 255 : 0) : (byte)255;
                        byte r = (byte)(((val >> 10) & 0x1F) * 255 / 31);
                        byte g = (byte)(((val >> 5) & 0x1F) * 255 / 31);
                        byte b = (byte)((val & 0x1F) * 255 / 31);

                        dstPixels[i] = (a << 24) | (r << 16) | (g << 8) | b;
                    }
                }
                else if (srcFormat == dstFormat)
                {
                    Buffer.MemoryCopy(srcPtr, dstPtr, dst.Length, src.Length);
                }
                else
                {
                    throw new NotSupportedException($"Conversion from {srcFormat} to {dstFormat} is not supported.");
                }
            }
        }

        public void Save(Stream stream, ImageFormat format)
        {
            if (format == null)
                throw new ArgumentNullException(nameof(format));

            // Only BMP is implemented; treat Tiff/Png as BMP for now.
            EncodeBmp(stream);
        }

        public void Save(string path, ImageFormat format)
        {
            using var fs = File.Create(path);
            Save(fs, format);
        }

        private unsafe void EncodeBmp(Stream stream)
        {
            // Supports 32bpp ARGB output. 16bpp inputs are converted to 24bpp RGB.
            int bitsPerPixel;
            bool convertTo24bpp;

            if (PixelFormat == PixelFormat.Format32bppArgb)
            {
                bitsPerPixel = 32;
                convertTo24bpp = false;
            }
            else
            {
                bitsPerPixel = 24;
                convertTo24bpp = true;
            }

            int rowSize = ((Width * bitsPerPixel + 31) / 32) * 4;
            int imageSize = rowSize * Height;
            int headerSize = 54;
            int fileSize = headerSize + imageSize;

            byte[] header = new byte[headerSize];
            // Signature
            header[0] = (byte)'B';
            header[1] = (byte)'M';
            // File size
            BitConverter.GetBytes(fileSize).CopyTo(header, 2);
            // Reserved
            BitConverter.GetBytes(0).CopyTo(header, 6);
            // Data offset
            BitConverter.GetBytes(headerSize).CopyTo(header, 10);
            // Header size
            BitConverter.GetBytes(40).CopyTo(header, 14);
            // Width / Height
            BitConverter.GetBytes(Width).CopyTo(header, 18);
            BitConverter.GetBytes(Height).CopyTo(header, 22);
            // Planes
            BitConverter.GetBytes((ushort)1).CopyTo(header, 26);
            // Bits per pixel
            BitConverter.GetBytes((ushort)bitsPerPixel).CopyTo(header, 28);
            // Compression
            BitConverter.GetBytes(0).CopyTo(header, 30);
            // Image size
            BitConverter.GetBytes(imageSize).CopyTo(header, 34);
            // PPM
            BitConverter.GetBytes(2835).CopyTo(header, 38);
            BitConverter.GetBytes(2835).CopyTo(header, 42);
            // Colors
            BitConverter.GetBytes(0).CopyTo(header, 46);
            BitConverter.GetBytes(0).CopyTo(header, 50);

            stream.Write(header, 0, header.Length);

            byte[] row = new byte[rowSize];

            fixed (byte* src = _pixels)
            {
                for (int y = Height - 1; y >= 0; y--)
                {
                    int dst = 0;

                    if (convertTo24bpp)
                    {
                        for (int x = 0; x < Width; x++)
                        {
                            ushort val = ((ushort*)src)[y * Width + x];
                            byte r = (byte)(((val >> 10) & 0x1F) * 255 / 31);
                            byte g = (byte)(((val >> 5) & 0x1F) * 255 / 31);
                            byte b = (byte)((val & 0x1F) * 255 / 31);
                            row[dst++] = b;
                            row[dst++] = g;
                            row[dst++] = r;
                        }
                    }
                    else
                    {
                        for (int x = 0; x < Width; x++)
                        {
                            int val = ((int*)src)[y * Width + x];
                            row[dst++] = (byte)(val & 0xFF);       // B
                            row[dst++] = (byte)((val >> 8) & 0xFF);  // G
                            row[dst++] = (byte)((val >> 16) & 0xFF); // R
                            row[dst++] = (byte)((val >> 24) & 0xFF); // A
                        }
                    }

                    stream.Write(row, 0, rowSize);
                }
            }
        }

        public void Dispose()
        {
            _activeLock?.Dispose();
            _activeLock = null;
        }

        internal static int GetBitsPerPixel(PixelFormat format)
        {
            switch (format)
            {
                case PixelFormat.Format16bppArgb1555:
                case PixelFormat.Format16bppRgb555:
                    return 16;
                case PixelFormat.Format32bppArgb:
                    return 32;
                default:
                    throw new NotSupportedException($"Pixel format {format} is not supported.");
            }
        }

        internal int BytesPerPixel => GetBitsPerPixel(PixelFormat) / 8;
        internal int Stride => Width * BytesPerPixel;
    }
}
