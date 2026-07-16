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

        public MultiComponentList(BinaryReader reader, int count, bool useNewMultiFormat)
        {
            _min = _max = Point.Empty;

            SortedTiles = new MultiTileEntry[count];

            for (int i = 0; i < count; ++i)
            {
                var mt = new MultiTileEntry
                {
                    m_ItemID = Art.GetLegalItemId(reader.ReadUInt16()),
                    m_OffsetX = reader.ReadInt16(),
                    m_OffsetY = reader.ReadInt16(),
                    m_OffsetZ = reader.ReadInt16(),
                    m_Flag = (sbyte)reader.ReadInt32(),
                };

                if (useNewMultiFormat)
                    reader.ReadInt32();

                if (mt.m_OffsetX < _min.X) _min.X = mt.m_OffsetX;
                if (mt.m_OffsetY < _min.Y) _min.Y = mt.m_OffsetY;
                if (mt.m_OffsetX > _max.X) _max.X = mt.m_OffsetX;
                if (mt.m_OffsetY > _max.Y) _max.Y = mt.m_OffsetY;
                if (mt.m_OffsetZ > MaxHeight) MaxHeight = mt.m_OffsetZ;

                SortedTiles[i] = mt;
            }

            RebuildTilesArray();
            reader.Close();
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

            int xMin = 1000, yMin = 1000;
            int xMax = -1000, yMax = -1000;

            for (int x = 0; x < Width; ++x)
            {
                for (int y = 0; y < Height; ++y)
                {
                    foreach (var mTile in Tiles[x][y])
                    {
                        Bitmap bmp = Art.GetStatic(mTile.Id);
                        if (bmp == null) continue;

                        int px = (x - y) * 22;
                        int py = (x + y) * 22;

                        px -= bmp.Width / 2;
                        py -= mTile.Z << 2;
                        py -= bmp.Height;

                        if (px < xMin) xMin = px;
                        if (py < yMin) yMin = py;

                        px += bmp.Width;
                        py += bmp.Height;

                        if (px > xMax) xMax = px;
                        if (py > yMax) yMax = py;
                    }
                }
            }

            if (xMax <= xMin || yMax <= yMin)
                return null;

            var canvas = new Bitmap(xMax - xMin, yMax - yMin);
            using (var gfx = Graphics.FromImage(canvas))
            {
                gfx.Clear(Color.Transparent);

                for (int x = 0; x < Width; ++x)
                {
                    for (int y = 0; y < Height; ++y)
                    {
                        foreach (var mTile in Tiles[x][y])
                        {
                            if (mTile.Z > maximumHeight) continue;

                            Bitmap bmp = Art.GetStatic(mTile.Id);
                            if (bmp == null) continue;

                            int px = (x - y) * 22;
                            int py = (x + y) * 22;

                            px -= bmp.Width / 2;
                            py -= mTile.Z << 2;
                            py -= bmp.Height;
                            px -= xMin;
                            py -= yMin;

                            gfx.DrawImageUnscaled(bmp, px, py, bmp.Width, bmp.Height);
                        }
                    }
                }
            }

            return canvas;
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

        private static MultiComponentList[] _components = new MultiComponentList[MaximumMultiIndex];
        private static FileIndex _fileIndex = new FileIndex("Multi.idx", "Multi.mul", MaximumMultiIndex, 14);

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
            if (index >= 0 && index < _components.Length)
            {
                var mcl = _components[index];
                if (mcl == null)
                    _components[index] = mcl = Load(index);
                return mcl;
            }
            return MultiComponentList.Empty;
        }

        public static MultiComponentList Load(int index)
        {
            try
            {
                Stream stream = _fileIndex.Seek(index, out int length, out int _, out bool _);
                if (stream == null)
                    return MultiComponentList.Empty;

                bool isUohs = Art.IsUOAHS();
                return new MultiComponentList(new BinaryReader(stream), isUohs ? length / 16 : length / 12, isUohs);
            }
            catch
            {
                return MultiComponentList.Empty;
            }
        }

        public static MultiComponentList Load(int index, string path, int width, int height)
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
                return MultiComponentList.Empty;

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

                    if (type == ImportType.WSC)
                    {
                        if (line.Contains("ID\t"))
                        {
                            int idIdx = Array.IndexOf(parts, "ID");
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
                        if (parts[0].StartsWith("0x"))
                            entry.m_ItemID = Convert.ToInt32(parts[0], 16);
                        else
                            int.TryParse(parts[0], out entry.m_ItemID);
                        int.TryParse(parts[1], out entry.m_OffsetX);
                        int.TryParse(parts[2], out entry.m_OffsetY);
                        int.TryParse(parts[3], out entry.m_OffsetZ);
                        if (parts.Length > 4)
                            int.TryParse(parts[4], out entry.m_Flag);
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
                return MultiComponentList.Empty;
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
            if (index >= 0 && index < _components.Length)
                _components[index] = comp;
        }

        public static void Remove(int index)
        {
            if (index >= 0 && index < _components.Length)
                _components[index] = MultiComponentList.Empty;
        }

        public static void Reload()
        {
            _fileIndex = new FileIndex("Multi.idx", "Multi.mul", MaximumMultiIndex, 14);
            _components = new MultiComponentList[MaximumMultiIndex];
        }

        public static void Save(string path)
        {
            throw new NotSupportedException("Multis.Save is not supported in ClassicUO.UltimaSdk adapter");
        }
    }
}
