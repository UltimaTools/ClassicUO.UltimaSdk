namespace Ultima
{
    public static class ZLibManaged
    {
        public static void Decompress(byte[] source, int sourceStart, int sourceLength, int offset, byte[] dest, int length)
        {
            ClassicUO.Utility.ZLibManaged.Decompress(source, sourceStart, sourceLength, offset, dest, length);
        }
    }
}
