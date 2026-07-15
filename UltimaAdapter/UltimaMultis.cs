using System.Collections.Generic;
using CUOAssets = ClassicUO.Assets;

namespace Ultima
{
    public struct MultiTileEntry
    {
        public int m_ItemID;
        public int m_OffsetX;
        public int m_OffsetY;
        public int m_OffsetZ;
        public int m_Flag;
    }

    public struct Point
    {
        public int X, Y;

        public Point(int x, int y) { X = x; Y = y; }
    }

    public sealed class MultiComponentList
    {
        public Point Min { get; set; }
        public Point Max { get; set; }
        public MultiTileEntry[] SortedTiles { get; set; }

        public int Count => SortedTiles?.Length ?? 0;

        public MultiComponentList() { }

        public MultiComponentList(int multiID, int x, int y)
        {
            SortedTiles = System.Array.Empty<MultiTileEntry>();
            Min = new Point(0, 0);
            Max = new Point(0, 0);

            try
            {
                int rawID = multiID & 0x3FFF;
                var components = Files.Manager?.Multis?.GetMultis((uint)rawID);
                if (components == null) return;

                var tiles = new List<MultiTileEntry>();
                int minX = 0, minY = 0, maxX = 0, maxY = 0;

                foreach (var c in components)
                {
                    int cx = c.X + x;
                    int cy = c.Y + y;

                    if (cx < minX) minX = cx;
                    if (cy < minY) minY = cy;
                    if (cx > maxX) maxX = cx;
                    if (cy > maxY) maxY = cy;

                    tiles.Add(new MultiTileEntry
                    {
                        m_ItemID = c.ID,
                        m_OffsetX = cx,
                        m_OffsetY = cy,
                        m_OffsetZ = c.Z,
                        m_Flag = c.IsVisible ? 1 : 0
                    });
                }

                Min = new Point(minX, minY);
                Max = new Point(maxX, maxY);
                SortedTiles = tiles.ToArray();
            }
            catch
            {
            }
        }
    }

    public static class Multis
    {
        public static MultiComponentList GetComponents(int index)
        {
            var result = new MultiComponentList();
            var tiles = new List<MultiTileEntry>();

            try
            {
                int rawID = index & 0x3FFF;
                var components = Files.Manager?.Multis?.GetMultis((uint)rawID);
                if (components != null)
                {
                    int minX = 0, minY = 0, maxX = 0, maxY = 0;

                    foreach (var c in components)
                    {
                        if (c.X < minX) minX = c.X;
                        if (c.Y < minY) minY = c.Y;
                        if (c.X > maxX) maxX = c.X;
                        if (c.Y > maxY) maxY = c.Y;

                        tiles.Add(new MultiTileEntry
                        {
                            m_ItemID = c.ID,
                            m_OffsetX = c.X,
                            m_OffsetY = c.Y,
                            m_OffsetZ = c.Z,
                            m_Flag = c.IsVisible ? 1 : 0
                        });
                    }

                    result.Min = new Point(minX, minY);
                    result.Max = new Point(maxX, maxY);
                }
            }
            catch
            {
            }

            result.SortedTiles = tiles.ToArray();
            return result;
        }
    }
}
