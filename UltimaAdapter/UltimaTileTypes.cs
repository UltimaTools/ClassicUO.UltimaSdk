using System;

namespace Ultima
{
    public struct Tile
    {
        public ushort Id;
        public sbyte Z;

        public Tile(int id, sbyte z)
        {
            Id = (ushort)id;
            Z = z;
        }

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

        public HuedTile(int id, short hue, sbyte z)
        {
            Id = (ushort)id;
            Hue = hue;
            Z = z;
        }

        public HuedTile(ushort id, short hue, sbyte z)
        {
            Id = id;
            Hue = hue;
            Z = z;
        }
    }

    public struct MTile : IComparable
    {
        public ushort Id { get; internal set; }
        public sbyte Z { get; set; }

        public sbyte Flag { get; set; }

        public int Unk1 { get; set; }

        public int Solver { get; set; }

        public MTile(int id, sbyte z)
        {
            Id = Art.GetLegalItemId((ushort)id);
            Z = z;
            Flag = 1;
            Solver = 0;
            Unk1 = 0;
        }

        public MTile(ushort id, sbyte z)
        {
            Id = Art.GetLegalItemId(id);
            Z = z;
            Flag = 1;
            Solver = 0;
            Unk1 = 0;
        }

        public MTile(int id, sbyte z, sbyte flag)
        {
            Id = Art.GetLegalItemId((ushort)id);
            Z = z;
            Flag = flag;
            Solver = 0;
            Unk1 = 0;
        }

        public MTile(ushort id, sbyte z, sbyte flag)
        {
            Id = Art.GetLegalItemId(id);
            Z = z;
            Flag = flag;
            Solver = 0;
            Unk1 = 0;
        }

        public MTile(int id, sbyte z, sbyte flag, int unk1)
        {
            Id = Art.GetLegalItemId((ushort)id);
            Z = z;
            Flag = flag;
            Solver = 0;
            Unk1 = unk1;
        }

        public MTile(ushort id, sbyte z, sbyte flag, int unk1)
        {
            Id = Art.GetLegalItemId(id);
            Z = z;
            Flag = flag;
            Solver = 0;
            Unk1 = unk1;
        }

        public void Set(int id, sbyte z)
        {
            Id = Art.GetLegalItemId((ushort)id);
            Z = z;
        }

        public void Set(ushort id, sbyte z)
        {
            Id = Art.GetLegalItemId(id);
            Z = z;
        }

        public void Set(int id, sbyte z, sbyte flag)
        {
            Id = Art.GetLegalItemId((ushort)id);
            Z = z;
            Flag = flag;
        }

        public void Set(ushort id, sbyte z, sbyte flag)
        {
            Id = Art.GetLegalItemId(id);
            Z = z;
            Flag = flag;
        }

        public void Set(int id, sbyte z, sbyte flag, int unk1)
        {
            Id = Art.GetLegalItemId((ushort)id);
            Z = z;
            Flag = flag;
            Unk1 = unk1;
        }

        public void Set(ushort id, sbyte z, sbyte flag, int unk1)
        {
            Id = Art.GetLegalItemId(id);
            Z = z;
            Flag = flag;
            Unk1 = unk1;
        }

        public int CompareTo(object x)
        {
            if (x == null)
            {
                return 1;
            }

            if (!(x is MTile))
            {
                throw new ArgumentNullException();
            }

            var a = (MTile)x;

            ItemData ourData = TileData.ItemTable[Id];
            ItemData theirData = TileData.ItemTable[a.Id];

            int ourThreshold = 0;
            if (ourData.Height > 0)
            {
                ++ourThreshold;
            }

            if (((TileFlag)ourData.Flags & TileFlag.Background) != 0)
            {
                ++ourThreshold;
            }

            if (TileData.ItemTable[Id].Height > 0)
            {
                ++ourThreshold;
            }

            int theirThreshold = 0;

            if (theirData.Height > 0)
            {
                ++theirThreshold;
            }

            if (((TileFlag)theirData.Flags & TileFlag.Background) != 0)
            {
                ++theirThreshold;
            }

            if (TileData.ItemTable[a.Id].Height > 0)
            {
                ++theirThreshold;
            }

            if (ourThreshold > theirThreshold)
            {
                return -1;
            }

            if (ourThreshold < theirThreshold)
            {
                return 1;
            }

            if (Unk1 > a.Unk1)
            {
                return -1;
            }

            if (Unk1 < a.Unk1)
            {
                return 1;
            }

            return 0;
        }
    }
}
