using System;
using System.Collections.Generic;
using System.IO;
using CUOAssets = ClassicUO.Assets;

namespace Ultima
{
    public sealed class TileMatrix
    {
        private readonly int _mapIndex;

        public TileMatrix(int mapIndex)
        {
            _mapIndex = mapIndex;
        }

        public Tile GetLandTile(int x, int y, bool patch = false)
        {
            var mapLoader = Files.Manager?.Maps;
            if (mapLoader == null) return new Tile(0, 0);

            try
            {
                mapLoader.LoadMap(_mapIndex);

                int blockX = x >> 3;
                int blockY = y >> 3;
                int cellX = x & 7;
                int cellY = y & 7;

                ref var index = ref mapLoader.GetIndex(_mapIndex, blockX, blockY);
                if (!index.IsValid()) return new Tile(0, 0);

                var mapFile = index.MapFile;
                if (mapFile == null) return new Tile(0, 0);

                // MapBlock: 4 bytes header + 64 cells of 3 bytes each (ushort TileID + sbyte Z)
                ulong cellOffset = index.MapAddress + 4 + (ulong)((cellY * 8 + cellX) * 3);
                mapFile.Seek((long)cellOffset, SeekOrigin.Begin);
                ushort tileID = mapFile.ReadUInt16();
                sbyte z = mapFile.ReadInt8();

                return new Tile(tileID, z);
            }
            catch
            {
                return new Tile(0, 0);
            }
        }

        public HuedTile[] GetStaticTiles(int x, int y, bool patch = false)
        {
            var mapLoader = Files.Manager?.Maps;
            if (mapLoader == null) return Array.Empty<HuedTile>();

            try
            {
                mapLoader.LoadMap(_mapIndex);

                int blockX = x >> 3;
                int blockY = y >> 3;
                int cellX = x & 7;
                int cellY = y & 7;

                ref var index = ref mapLoader.GetIndex(_mapIndex, blockX, blockY);
                if (!index.IsValid() || index.StaticCount == 0)
                    return Array.Empty<HuedTile>();

                var staticFile = index.StaticFile;
                if (staticFile == null) return Array.Empty<HuedTile>();

                staticFile.Seek((long)index.StaticAddress, SeekOrigin.Begin);

                var list = new List<HuedTile>();

                for (uint i = 0; i < index.StaticCount; i++)
                {
                    // StaticsBlock: ushort Color, byte X, byte Y, sbyte Z, ushort Hue = 7 bytes
                    ushort color = staticFile.ReadUInt16();
                    byte sx = staticFile.ReadUInt8();
                    byte sy = staticFile.ReadUInt8();
                    sbyte sz = staticFile.ReadInt8();
                    ushort hue = staticFile.ReadUInt16();

                    if (sx == cellX && sy == cellY)
                        list.Add(new HuedTile(color, (short)hue, sz));
                }

                return list.ToArray();
            }
            catch
            {
                return Array.Empty<HuedTile>();
            }
        }
    }

    public sealed class Map
    {
        private readonly int _mapIndex;
        private TileMatrix _tiles;

        public static readonly Map Felucca = new Map(0);
        public static readonly Map Trammel = new Map(1);
        public static readonly Map Ilshenar = new Map(2);
        public static readonly Map Malas = new Map(3);
        public static readonly Map Tokuno = new Map(4);
        public static readonly Map TerMur = new Map(5);

        public TileMatrix Tiles => _tiles ??= new TileMatrix(_mapIndex);

        private Map(int index)
        {
            _mapIndex = index;
        }
    }
}
