using System;
using System.Runtime.InteropServices;

namespace Ultima.Drawing.Imaging
{
    public enum PixelFormat
    {
        Format16bppArgb1555,
        Format16bppRgb555,
        Format32bppArgb
    }

    public enum ImageLockMode
    {
        ReadOnly,
        WriteOnly,
        ReadWrite
    }

    public class BitmapData : IDisposable
    {
        public IntPtr Scan0 { get; internal set; }
        public int Stride { get; internal set; }
        public int Width { get; internal set; }
        public int Height { get; internal set; }
        public PixelFormat PixelFormat { get; internal set; }

        internal GCHandle Handle;
        internal byte[] Buffer;
        internal Bitmap Owner;
        internal bool IsTempBuffer;

        public void Dispose()
        {
            if (Handle.IsAllocated)
            {
                Handle.Free();
            }
        }
    }
}
