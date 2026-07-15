using System;
using System.IO;
using System.Text;

namespace Ultima
{
    public enum WaveFormat
    {
        PCM = 1
    }

    public sealed class WaveFormatException : Exception
    {
        public WaveFormatException(string message) : base(message) { }
        public WaveFormatException(string message, Exception inner) : base(message, inner) { }
    }
}

namespace System.IO
{
    internal static class BinaryReaderStringExtensions
    {
        public static string ReadString(this BinaryReader reader, int length)
        {
            return Encoding.ASCII.GetString(reader.ReadBytes(length));
        }
    }

    internal static class BinaryWriterStringExtensions
    {
        public static void WriteString(this BinaryWriter writer, string value)
        {
            writer.Write(Encoding.ASCII.GetBytes(value));
        }
    }
}