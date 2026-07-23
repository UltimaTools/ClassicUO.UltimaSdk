// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using ZLibNative;

namespace ClassicUO.Utility
{
    public static class ZLibManaged
    {
        public static void Decompress
        (
            byte[] source,
            int sourceStart,
            int sourceLength,
            int offset,
            byte[] dest,
            int length
        )
        {
            using (MemoryStream stream = new MemoryStream(source, sourceStart, sourceLength - offset))
            {
                // Skip the 2-byte zlib header (CMF/FLG).
                stream.Position = 2;

                using (DeflateStream ds = new DeflateStream(stream, CompressionMode.Decompress))
                {
                    int totalRead = 0;
                    while (totalRead < length)
                    {
                        int read = ds.Read(dest, totalRead, length - totalRead);
                        if (read == 0)
                            break;
                        totalRead += read;
                    }
                }
            }
        }

        public static unsafe void Decompress(IntPtr source, int sourceLength, int offset, IntPtr dest, int length)
        {
            byte[] src = new byte[sourceLength - offset];
            Marshal.Copy(IntPtr.Add(source, offset), src, 0, src.Length);

            byte[] dst = new byte[length];
            Decompress(src, 0, src.Length, 0, dst, length);

            Marshal.Copy(dst, 0, dest, length);
        }

        public static unsafe void Decompress(ReadOnlySpan<byte> source, Span<byte> dest)
        {
            fixed (byte* srcPtr = source)
            fixed (byte* dstPtr = dest)
                Decompress((IntPtr)srcPtr, source.Length, 0, (IntPtr)dstPtr, dest.Length);
        }

        public static void Compress(byte[] dest, ref int destLength, byte[] source)
        {
            using (MemoryStream stream = new MemoryStream(dest))
            {
                // zlib header: 0x78 0x9C (default compression)
                stream.WriteByte(0x78);
                stream.WriteByte(0x9C);

                using (DeflateStream ds = new DeflateStream(stream, CompressionLevel.Optimal, true))
                {
                    ds.Write(source, 0, source.Length);
                    ds.Flush();
                }

                Adler32 adler = new Adler32();
                adler.Update(source);
                uint checksum = adler.GetValue();

                stream.WriteByte((byte)(checksum >> 24));
                stream.WriteByte((byte)(checksum >> 16));
                stream.WriteByte((byte)(checksum >> 8));
                stream.WriteByte((byte)checksum);

                destLength = (int)stream.Position;
            }
        }
    }
}
