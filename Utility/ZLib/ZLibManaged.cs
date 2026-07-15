// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.IO;
using System.IO.Compression;

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
            using (var stream = new MemoryStream(source, sourceStart, sourceLength - offset, true))
            {
#if NETFRAMEWORK
                // net472: skip zlib header (2 bytes) and use DeflateStream
                stream.ReadByte();
                stream.ReadByte();
                using (var ds = new DeflateStream(stream, CompressionMode.Decompress))
#else
                using (var ds = new ZLibStream(stream, CompressionMode.Decompress))
#endif
                {
                    int totalRead = 0;

                    while (totalRead < length)
                    {
                        int toRead = Math.Min(4096, length - totalRead);
                        int bytesRead = ds.Read(dest, totalRead, toRead);
                        if (bytesRead <= 0)
                            break;
                        totalRead += bytesRead;
                    }
                }
            }
        }

        public static unsafe void Decompress(IntPtr source, int sourceLength, int offset, IntPtr dest, int length)
        {
            byte[] tempDest = new byte[length];
            byte[] tempSource = new byte[sourceLength - offset];

            fixed (byte* tempSourcePtr = tempSource)
            {
                Buffer.MemoryCopy((byte*)source.ToPointer(), tempSourcePtr, tempSource.Length, tempSource.Length);
            }

            Decompress(tempSource, 0, sourceLength, offset, tempDest, length);

            fixed (byte* tempDestPtr = tempDest)
            {
                Buffer.MemoryCopy(tempDestPtr, (byte*)dest.ToPointer(), length, length);
            }
        }

        public static void Compress(byte[] dest, ref int destLength, byte[] source)
        {
            using (var stream = new MemoryStream(dest, true))
            {
#if NETFRAMEWORK
                stream.WriteByte(0x78);
                stream.WriteByte(0x9C);
                using (var ds = new DeflateStream(stream, CompressionMode.Compress, true))
#else
                using (var ds = new ZLibStream(stream, CompressionMode.Compress, true))
#endif
                {
                    ds.Write(source, 0, source.Length);
                    ds.Flush();
                }

                destLength = (int)stream.Position;
            }
        }
    }
}
