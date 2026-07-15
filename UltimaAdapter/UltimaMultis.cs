using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
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

    public sealed class MultiComponentList
    {
        private Point _min;
        private Point _max;

        public Point Min { get => _min; set => _min = value; }
        public Point Max { get => _max; set => _max = value; }

        public Point Center => new Point((_min.X + _max.X) / 2, (_min.Y + _max.Y) / 2);

        public int Width { get; private set; }
        public int Height { get; private set; }
        public int MaxHeight { get; private set; }
        public int Surface { get; private set; }

        public MTile[][][] Tiles { get; private set; }
        public MultiTileEntry[] SortedTiles { get; internal set; }

        public int Count => SortedTiles?.Length ?? 0;

        public static MultiComponentList Empty { get; } = new MultiComponentList();

        public MultiComponentList()
        {
            SortedTiles = Array.Empty<MultiTileEntry>();
            Tiles = Array.Empty<MTile[][]>();
        }

        public MultiComponentList(int multiID, int x, int y)
        {
            SortedTiles = Array.Empty<MultiTileEntry>();
            _min = new Point(0, 0);
            _max = new Point(0, 0);

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

                _min = new Point(minX, minY);
                _max = new Point(maxX, maxY);
                SortedTiles = tiles.ToArray();
                RebuildTilesArray();
            }
            catch
            {
            }
        }

        public MultiComponentList(MTileList[][] newTiles, int count, int width, int height)
        {
            SortedTiles = new MultiTileEntry[count];
            Width = width;
            Height = height;
            Tiles = new MTile[width][][];
            for (int x = 0; x < width; x++)
            {
                Tiles[x] = new MTile[height][];
                for (int y = 0; y < height; y++)
                    Tiles[x][y] = newTiles[x][y].ToArray();
            }

            int idx = 0;
            MaxHeight = 0;
            _min = new Point(0, 0);
            _max = new Point(width - 1, height - 1);
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var tilesArr = Tiles[x][y];
                    for (int i = 0; i < tilesArr.Length && idx < count; i++)
                    {
                        var mt = tilesArr[i];
                        if (mt.Z > MaxHeight) MaxHeight = mt.Z;
                        SortedTiles[idx++] = new MultiTileEntry
                        {
                            m_ItemID = mt.Id,
                            m_OffsetX = x,
                            m_OffsetY = y,
                            m_OffsetZ = mt.Z,
                            m_Flag = mt.Flag
                        };
                    }
                }
            }
        }

        internal void RebuildTilesArray()
        {
            Width = _max.X - _min.X + 1;
            Height = _max.Y - _min.Y + 1;

            if (Width <= 0 || Height <= 0)
            {
                Width = Math.Max(1, Width);
                Height = Math.Max(1, Height);
            }

            Tiles = new MTile[Width][][];
            var list = new MTileList[Width][];

            for (int x = 0; x < Width; x++)
            {
                Tiles[x] = new MTile[Height][];
                list[x] = new MTileList[Height];
                for (int y = 0; y < Height; y++)
                    list[x][y] = new MTileList();
            }

            MaxHeight = 0;

            foreach (var t in SortedTiles)
            {
                int x = t.m_OffsetX - _min.X;
                int y = t.m_OffsetY - _min.Y;

                if (x >= 0 && x < Width && y >= 0 && y < Height)
                {
                    list[x][y].Add((ushort)t.m_ItemID, (sbyte)t.m_OffsetZ, (sbyte)t.m_Flag);

                    if (t.m_OffsetZ > MaxHeight)
                        MaxHeight = t.m_OffsetZ;
                }
            }

            Surface = 0;
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    Tiles[x][y] = list[x][y].ToArray();
                    if (Tiles[x][y].Length > 0)
                        Surface++;
                }
            }
        }

        public Bitmap GetImage(int maximumHeight = 300)
        {
            if (Width == 0 || Height == 0)
                return null;

            int maxTileHeight = 0;
            for (int i = 0; i < SortedTiles.Length; i++)
            {
                int z = SortedTiles[i].m_OffsetZ;
                if (z > maxTileHeight)
                    maxTileHeight = z;
            }

            int imageWidth = Width * 44;
            int imageHeight = (Height + maxTileHeight / 20) * 44;

            var bmp = new Bitmap(imageWidth, imageHeight, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.Transparent);

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    var tiles = Tiles[x][y];
                    if (tiles == null || tiles.Length == 0)
                        continue;

                    int drawX = x * 44;
                    int drawY = (y * 44) + (Height * 44) - 44 - (maxTileHeight / 20 * 44);

                    foreach (var tile in tiles)
                    {
                        var art = Art.GetStatic(tile.Id);
                        if (art != null)
                        {
                            int adjust = maxTileHeight - tile.Z;
                            g.DrawImage(art, drawX, drawY - (adjust * 2));
                        }
                    }
                }
            }

            return bmp;
        }

        public void ExportToTextFile(string fileName)
        {
            using var w = new StreamWriter(fileName);
            w.WriteLine("// MultiComponentList Export");
            w.WriteLine("// Format: X Y Z ID Flag");
            foreach (var t in SortedTiles)
                w.WriteLine($"{t.m_OffsetX}\t{t.m_OffsetY}\t{t.m_OffsetZ}\t0x{t.m_ItemID:X4}\t{t.m_Flag}");
        }

        public void ExportToWscFile(string fileName)
        {
            using var w = new StreamWriter(fileName);
            foreach (var t in SortedTiles)
            {
                w.WriteLine("SECTION WORLDITEM {");
                w.WriteLine($"\tSERIAL\t-1");
                w.WriteLine($"\tNAME\t\"multi\"");
                w.WriteLine($"\tID\t{t.m_ItemID}");
                w.WriteLine($"\tX\t{t.m_OffsetX}");
                w.WriteLine($"\tY\t{t.m_OffsetY}");
                w.WriteLine($"\tZ\t{t.m_OffsetZ}");
                w.WriteLine("}");
            }
        }

        public void ExportToUOAFile(string fileName)
        {
            using var w = new StreamWriter(fileName);
            foreach (var t in SortedTiles)
            {
                w.Write($"{t.m_ItemID.ToString("X4")}");
                w.Write($" {t.m_OffsetX}");
                w.Write($" {t.m_OffsetY}");
                w.Write($" {t.m_OffsetZ}");
                w.Write($" {t.m_Flag}");
                w.WriteLine();
            }
        }

        public void ExportToUox3File(string fileName)
        {
            ExportToWscFile(fileName);
        }

        public void ExportToCsvFile(string fileName)
        {
            using var w = new StreamWriter(fileName);
            w.WriteLine("ItemID;X;Y;Z;Flag");
            foreach (var t in SortedTiles)
                w.WriteLine($"{t.m_ItemID};{t.m_OffsetX};{t.m_OffsetY};{t.m_OffsetZ};{t.m_Flag}");
        }

        public void ExportToXmlFile(string fileName, string entryId)
        {
            using var w = new StreamWriter(fileName);
            w.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            w.WriteLine("<multis>");
            w.WriteLine($"  <entry id=\"{entryId}\">");
            foreach (var t in SortedTiles)
            {
                w.WriteLine($"    <item id=\"0x{t.m_ItemID:X4}\" x=\"{t.m_OffsetX}\" y=\"{t.m_OffsetY}\" z=\"{t.m_OffsetZ}\" flag=\"{t.m_Flag}\" />");
            }
            w.WriteLine("  </entry>");
            w.WriteLine("</multis>");
        }
    }

    public static class Multis
    {
        public const int MaximumMultiIndex = 0x2200;

        public enum ImportType
        {
            TXT,
            UOA,
            UOAB,
            WSC,
            CSV,
            UOX3,
            MULTICACHE,
            UOADESIGN,
            XML
        }

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
            result.RebuildTilesArray();
            return result;
        }

        public static MultiComponentList Load(int index)
        {
            return GetComponents(index);
        }

        public static MultiComponentList LoadFromFile(string fileName, ImportType type)
        {
            return ImportFromFile(0, fileName, type);
        }

        public static MultiComponentList ImportFromFile(int index, string fileName, ImportType type)
        {
            if (!File.Exists(fileName))
                return Empty;

            try
            {
                var lines = File.ReadAllLines(fileName);
                var entries = new List<MultiTileEntry>();

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//") || line.StartsWith("#") || line.StartsWith("SECTION"))
                        continue;

                    var parts = line.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 4) continue;

                    int idIdx = 0, xIdx = 1, yIdx = 2, zIdx = 3, flagIdx = 4;

                    if (type == ImportType.WSC)
                    {
                        if (line.Contains("ID\t"))
                        {
                            idIdx = Array.IndexOf(parts, "ID");
                            if (idIdx >= 0 && idIdx + 1 < parts.Length)
                            {
                                var entry = new MultiTileEntry();
                                int.TryParse(parts[idIdx + 1], out entry.m_ItemID);
                                int.TryParse(parts[Array.IndexOf(parts, "X") + 1], out entry.m_OffsetX);
                                int.TryParse(parts[Array.IndexOf(parts, "Y") + 1], out entry.m_OffsetY);
                                int.TryParse(parts[Array.IndexOf(parts, "Z") + 1], out entry.m_OffsetZ);
                                entry.m_Flag = 1;
                                entries.Add(entry);
                            }
                        }
                        continue;
                    }

                    if (type == ImportType.UOA)
                    {
                        var entry = new MultiTileEntry();
                        if (parts[idIdx].StartsWith("0x"))
                            entry.m_ItemID = Convert.ToInt32(parts[idIdx], 16);
                        else
                            int.TryParse(parts[idIdx], out entry.m_ItemID);
                        int.TryParse(parts[xIdx], out entry.m_OffsetX);
                        int.TryParse(parts[yIdx], out entry.m_OffsetY);
                        int.TryParse(parts[zIdx], out entry.m_OffsetZ);
                        if (parts.Length > flagIdx)
                            int.TryParse(parts[flagIdx], out entry.m_Flag);
                        else
                            entry.m_Flag = 1;
                        entries.Add(entry);
                    }
                }

                var result = new MultiComponentList();
                result.SortedTiles = entries.ToArray();
                int minX = 0, minY = 0, maxX = 0, maxY = 0;
                foreach (var e in entries)
                {
                    if (e.m_OffsetX < minX) minX = e.m_OffsetX;
                    if (e.m_OffsetY < minY) minY = e.m_OffsetY;
                    if (e.m_OffsetX > maxX) maxX = e.m_OffsetX;
                    if (e.m_OffsetY > maxY) maxY = e.m_OffsetY;
                }
                result.Min = new Point(minX, minY);
                result.Max = new Point(maxX, maxY);
                result.RebuildTilesArray();
                return result;
            }
            catch
            {
                return Empty;
            }
        }

        public static List<MultiComponentList> LoadFromCache(string fileName)
        {
            return new List<MultiComponentList>();
        }

        public static List<object[]> LoadFromDesigner(string fileName)
        {
            return new List<object[]>();
        }

        public static void Add(int index, MultiComponentList comp)
        {
            // Stub - multi add not implemented
        }

        public static void Remove(int index)
        {
            // Stub - multi remove not implemented
        }

        public static void Reload()
        {
            // Stub - multi reload not implemented
        }

        public static void Save(string path)
        {
            throw new NotSupportedException("Multis.Save is not supported in ClassicUO.UltimaSdk adapter");
        }

        private static MultiComponentList _empty;

        private static MultiComponentList Empty
        {
            get
            {
                if (_empty == null)
                {
                    _empty = new MultiComponentList();
                    _empty.SortedTiles = Array.Empty<MultiTileEntry>();
                }
                return _empty;
            }
        }
    }
}
