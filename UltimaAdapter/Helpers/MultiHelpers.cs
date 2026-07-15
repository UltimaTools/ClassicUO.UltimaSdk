using System.IO;

namespace Ultima.Helpers
{
    public static class MultiHelpers
    {
        public static string ReadUOAString(BinaryReader bin)
        {
            byte flag = bin.ReadByte();

            return flag == 0 ? null : bin.ReadString();
        }
    }
}
