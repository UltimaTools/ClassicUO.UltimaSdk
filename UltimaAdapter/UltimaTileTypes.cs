namespace Ultima
{
    public struct Tile
    {
        public ushort Id;
        public sbyte Z;

        public Tile(ushort id, sbyte z)
        {
            Id = id;
            Z = z;
        }
    }

    public struct HuedTile
    {
        public ushort Id;
        public int Hue;
        public sbyte Z;

        public HuedTile(ushort id, short hue, sbyte z)
        {
            Id = id;
            Hue = hue;
            Z = z;
        }
    }
}
