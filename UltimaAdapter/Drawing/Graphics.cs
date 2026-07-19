using System;
using Ultima.Drawing.Imaging;

namespace Ultima.Drawing
{
    public class Graphics : IDisposable
    {
        private Bitmap _bitmap;

        public static Graphics FromImage(Bitmap bitmap)
        {
            if (bitmap == null)
                throw new ArgumentNullException(nameof(bitmap));

            return new Graphics(bitmap);
        }

        private Graphics(Bitmap bitmap)
        {
            _bitmap = bitmap;
        }

        public void Clear(Color color)
        {
            if (_bitmap.PixelFormat != PixelFormat.Format32bppArgb)
                throw new NotSupportedException("Clear is only supported on 32bpp ARGB bitmaps.");

            int colorValue = color.ToArgb();
            unsafe
            {
                fixed (byte* ptr = _bitmap._pixels)
                {
                    int* pixels = (int*)ptr;
                    int count = _bitmap.Width * _bitmap.Height;
                    for (int i = 0; i < count; i++)
                        pixels[i] = colorValue;
                }
            }
        }

        public void DrawImage(Bitmap source, int x, int y)
        {
            DrawImageUnscaled(source, x, y, 0, 0, source.Width, source.Height);
        }

        public void DrawImageUnscaled(Bitmap source, int x, int y, int width, int height)
        {
            DrawImageUnscaled(source, x, y, 0, 0, source.Width, source.Height);
        }

        private unsafe void DrawImageUnscaled(Bitmap source, int dstX, int dstY, int srcX, int srcY, int srcWidth, int srcHeight)
        {
            if (_bitmap.PixelFormat != PixelFormat.Format32bppArgb)
                throw new NotSupportedException("DrawImage target must be 32bpp ARGB.");

            int startSrcY = Math.Max(0, srcY);
            int startSrcX = Math.Max(0, srcX);
            int endSrcY = Math.Min(srcHeight, source.Height);
            int endSrcX = Math.Min(srcWidth, source.Width);

            int startDstY = dstY + startSrcY;
            int startDstX = dstX + startSrcX;

            int copyHeight = endSrcY - startSrcY;
            int copyWidth = endSrcX - startSrcX;

            if (copyWidth <= 0 || copyHeight <= 0)
                return;

            fixed (byte* dstPtr = _bitmap._pixels)
            fixed (byte* srcPtr = source._pixels)
            {
                int* dstPixels = (int*)dstPtr;
                int dstStride = _bitmap.Width;

                if (source.PixelFormat == PixelFormat.Format32bppArgb)
                {
                    int* srcPixels = (int*)srcPtr;
                    int srcStride = source.Width;

                    for (int row = 0; row < copyHeight; row++)
                    {
                        int sy = startSrcY + row;
                        int dy = startDstY + row;
                        if (dy < 0 || dy >= _bitmap.Height)
                            continue;

                        for (int col = 0; col < copyWidth; col++)
                        {
                            int sx = startSrcX + col;
                            int dx = startDstX + col;
                            if (dx < 0 || dx >= _bitmap.Width)
                                continue;

                            int srcColor = srcPixels[sy * srcStride + sx];
                            byte srcA = (byte)((srcColor >> 24) & 0xFF);

                            if (srcA == 0)
                                continue;

                            int dstIndex = dy * dstStride + dx;

                            if (srcA == 255)
                            {
                                dstPixels[dstIndex] = srcColor;
                            }
                            else
                            {
                                int dstColor = dstPixels[dstIndex];
                                byte dstB = (byte)(dstColor & 0xFF);
                                byte dstG = (byte)((dstColor >> 8) & 0xFF);
                                byte dstR = (byte)((dstColor >> 16) & 0xFF);
                                byte dstA = (byte)((dstColor >> 24) & 0xFF);

                                byte srcB = (byte)(srcColor & 0xFF);
                                byte srcG = (byte)((srcColor >> 8) & 0xFF);
                                byte srcR = (byte)((srcColor >> 16) & 0xFF);

                                int invA = 255 - srcA;
                                byte outA = (byte)(srcA + dstA * invA / 255);
                                byte outR = (byte)((srcR * srcA + dstR * invA) / 255);
                                byte outG = (byte)((srcG * srcA + dstG * invA) / 255);
                                byte outB = (byte)((srcB * srcA + dstB * invA) / 255);

                                dstPixels[dstIndex] = (outA << 24) | (outR << 16) | (outG << 8) | outB;
                            }
                        }
                    }
                }
                else if (source.PixelFormat == PixelFormat.Format16bppArgb1555 ||
                         source.PixelFormat == PixelFormat.Format16bppRgb555)
                {
                    ushort* srcPixels = (ushort*)srcPtr;
                    int srcStride = source.Width;

                    for (int row = 0; row < copyHeight; row++)
                    {
                        int sy = startSrcY + row;
                        int dy = startDstY + row;
                        if (dy < 0 || dy >= _bitmap.Height)
                            continue;

                        for (int col = 0; col < copyWidth; col++)
                        {
                            int sx = startSrcX + col;
                            int dx = startDstX + col;
                            if (dx < 0 || dx >= _bitmap.Width)
                                continue;

                            ushort val = srcPixels[sy * srcStride + sx];

                            if (source.PixelFormat == PixelFormat.Format16bppArgb1555)
                            {
                                if ((val & 0x8000) == 0)
                                    continue;
                            }

                            byte r = (byte)(((val >> 10) & 0x1F) * 255 / 31);
                            byte g = (byte)(((val >> 5) & 0x1F) * 255 / 31);
                            byte b = (byte)((val & 0x1F) * 255 / 31);

                            int dstIndex = dy * dstStride + dx;
                            dstPixels[dstIndex] = (255 << 24) | (r << 16) | (g << 8) | b;
                        }
                    }
                }
                else
                {
                    throw new NotSupportedException($"Source pixel format {source.PixelFormat} is not supported for DrawImage.");
                }
            }
        }

        public void Dispose()
        {
            _bitmap = null;
        }
    }
}
