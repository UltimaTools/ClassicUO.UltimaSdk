using System.IO;
using System;
using System.Xml.Linq;

namespace Ultima.Helpers
{
    public static class MapSizeDetector
    {
        public static string MapNamesPathOverride { get; set; }

        public static bool TryDetectMapSize(int fileIndex, string path, out int width, out int height)
        {
            width = 0;
            height = 0;

            if (!string.IsNullOrEmpty(MapNamesPathOverride) && File.Exists(MapNamesPathOverride))
            {
                try
                {
                    var doc = XDocument.Load(MapNamesPathOverride);
                    var elems = doc.Descendants("map");
                    foreach (var el in elems)
                    {
                        var idxAttr = el.Attribute("index");
                        if (idxAttr == null) continue;
                        if (!int.TryParse(idxAttr.Value, out int idx)) continue;
                        if (idx != fileIndex) continue;

                        var wAttr = el.Attribute("width");
                        var hAttr = el.Attribute("height");
                        if (wAttr != null && hAttr != null && int.TryParse(wAttr.Value, out int w) && int.TryParse(hAttr.Value, out int h))
                        {
                            width = w;
                            height = h;
                            return true;
                        }
                    }
                }
                catch
                {
                }
            }

            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string xml = Path.Combine(appData, "UoFiddler", "Mapnames.xml");

                if (File.Exists(xml))
                {
                    var doc = XDocument.Load(xml);
                    var elems = doc.Descendants("map");
                    foreach (var el in elems)
                    {
                        var idxAttr = el.Attribute("index");
                        if (idxAttr == null) continue;
                        if (!int.TryParse(idxAttr.Value, out int idx)) continue;
                        if (idx != fileIndex) continue;

                        var wAttr = el.Attribute("width");
                        var hAttr = el.Attribute("height");
                        if (wAttr != null && hAttr != null && int.TryParse(wAttr.Value, out int w) && int.TryParse(hAttr.Value, out int h))
                        {
                            width = w;
                            height = h;
                            return true;
                        }
                    }
                }
            }
            catch
            {
            }

            string idxPath = path == null
                ? Files.GetFilePath($"staidx{fileIndex}.mul")
                : Path.Combine(path, $"staidx{fileIndex}.mul");

            if (!string.IsNullOrEmpty(idxPath) && File.Exists(idxPath))
            {
                long len = new FileInfo(idxPath).Length;
                if (len > 0)
                {
                    long blocks = len / 12;
                    return TryFromBlockCount(blocks, out width, out height);
                }
            }

            string mapPath = path == null
                ? Files.GetFilePath($"map{fileIndex}.mul")
                : Path.Combine(path, $"map{fileIndex}.mul");

            if (!string.IsNullOrEmpty(mapPath) && File.Exists(mapPath))
            {
                if (mapPath.EndsWith(".uop", System.StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                long len = new FileInfo(mapPath).Length;
                if (len > 0)
                {
                    long blocks = len / 196;
                    return TryFromBlockCount(blocks, out width, out height);
                }
            }

            return false;
        }

        private static bool TryFromBlockCount(long blocks, out int width, out int height)
        {
            width = 0;
            height = 0;

            int[] candidateBlockWidths = { 896, 768, 320, 288, 181, 160 };

            foreach (int bw in candidateBlockWidths)
            {
                if (blocks % bw == 0)
                {
                    long bh = blocks / bw;
                    if (bh > 0 && bh <= 8192)
                    {
                        width = bw * 8;
                        height = (int)bh * 8;
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
