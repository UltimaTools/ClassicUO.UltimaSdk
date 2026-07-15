using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using SDColor = System.Drawing.Color;
using CUOAssets = ClassicUO.Assets;

namespace Ultima
{
    public sealed class Hue
    {
        private readonly int _index;
        private readonly CUOAssets.HuesLoader _loader;
        private ushort[] _colors;
        private string _name;
        private ushort _tableStart;
        private ushort _tableEnd;

        public Hue(int index)
        {
            _index = index;
            _loader = Files.Manager?.Hues;
            LoadBlockData();
        }

        public Hue(int index, BinaryReader bin)
        {
            _index = index;
            _loader = Files.Manager?.Hues;
            LoadBlockData();
        }

        internal Hue(int index, CUOAssets.HuesLoader loader)
        {
            _index = index;
            _loader = loader;
            LoadBlockData();
        }

        private void LoadBlockData()
        {
            if (_loader == null) return;
            var groups = _loader.HuesRange;
            if (groups == null) return;
            int groupIdx = _index / 8;
            int entryIdx = _index % 8;
            if (groupIdx >= groups.Length) return;
            var entries = groups[groupIdx].Entries;
            var block = entries[entryIdx];

            unsafe
            {
                _name = new string((sbyte*)block.Name, 0, 20);
                int nullIdx = _name.IndexOf('\0');
                if (nullIdx >= 0) _name = _name.Substring(0, nullIdx);
            }

            _tableStart = block.TableStart;
            _tableEnd = block.TableEnd;
        }

        public int Index => _index;

        public string Name
        {
            get => _name ?? "Null";
            set => _name = value;
        }

        public ushort TableStart
        {
            get => _tableStart;
            set => _tableStart = value;
        }

        public ushort TableEnd
        {
            get => _tableEnd;
            set => _tableEnd = value;
        }

        public ushort[] Colors
        {
            get
            {
                if (_colors == null && _loader != null)
                {
                    _colors = new ushort[32];
                    var groups = _loader.HuesRange;
                    if (groups != null)
                    {
                        int groupIdx = _index / 8;
                        int entryIdx = _index % 8;
                        if (groupIdx < groups.Length)
                        {
                            var entries = groups[groupIdx].Entries;
                            var block = entries[entryIdx];
                            for (int i = 0; i < 32; i++)
                                _colors[i] = block.ColorTable[i];
                        }
                    }
                }
                return _colors ?? new ushort[32];
            }
        }

        public SDColor GetColor(int colorIndex)
        {
            try
            {
                uint rgba = GetColorRgba(colorIndex);
                byte r = (byte)(rgba & 0xFF);
                byte g = (byte)((rgba >> 8) & 0xFF);
                byte b = (byte)((rgba >> 16) & 0xFF);
                byte a = (byte)((rgba >> 24) & 0xFF);
                return SDColor.FromArgb(a, r, g, b);
            }
            catch
            {
                return SDColor.Gray;
            }
        }

        public uint GetColorRgba(int colorIndex)
        {
            return _loader.GetHueColorRgba8888((ushort)colorIndex, (ushort)(_index + 1));
        }

        public void ApplyTo(Bitmap bmp, bool onlyHueGrayPixels)
        {
            if (bmp == null || _loader == null || _index <= 0) return;

            try
            {
                var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                try
                {
                    int[] pixels = new int[bmp.Width * bmp.Height];
                    Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);

                    for (int i = 0; i < pixels.Length; i++)
                    {
                        int pixel = pixels[i];
                        byte a = (byte)((pixel >> 24) & 0xFF);
                        if (a == 0) continue;

                        byte r = (byte)((pixel >> 16) & 0xFF);
                        byte g = (byte)((pixel >> 8) & 0xFF);
                        byte b = (byte)(pixel & 0xFF);

                        if (onlyHueGrayPixels && (r != g || r != b))
                            continue;

                        int grayIndex = (r * 31 + 128) / 255;
                        grayIndex = Math.Max(0, Math.Min(31, grayIndex));

                        uint hueColor = _loader.GetHueColorRgba8888((ushort)grayIndex, (ushort)(_index + 1));
                        byte hr = (byte)(hueColor & 0xFF);
                        byte hg = (byte)((hueColor >> 8) & 0xFF);
                        byte hb = (byte)((hueColor >> 16) & 0xFF);

                        pixels[i] = (a << 24) | (hr << 16) | (hg << 8) | hb;
                    }

                    Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
                }
                finally
                {
                    bmp.UnlockBits(data);
                }
            }
            catch
            {
            }
        }

        public void Export(string fileName)
        {
            using var tex = new StreamWriter(new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite), Encoding.GetEncoding(1252));
            tex.WriteLine(Name);
            tex.WriteLine(TableStart);
            tex.WriteLine(TableEnd);
            for (int i = 0; i < 32; i++)
                tex.WriteLine(Colors[i]);
        }

        public void Import(string fileName)
        {
            if (!File.Exists(fileName)) return;
            var lines = File.ReadAllLines(fileName, Encoding.GetEncoding(1252));
            if (lines.Length < 3) return;
            Name = lines[0];
            TableStart = ushort.Parse(lines[1]);
            TableEnd = ushort.Parse(lines[2]);
            for (int i = 0; i < 32 && i + 3 < lines.Length; i++)
            {
                if (_colors == null) _colors = new ushort[32];
                _colors[i] = ushort.Parse(lines[i + 3]);
            }
        }
    }

    public static class Hues
    {
        private static Hue[] _list;

        public static void Initialize() { }

        public static Hue[] List
        {
            get
            {
                if (_list == null)
                {
                    var loader = Files.Manager?.Hues;
                    if (loader != null)
                    {
                        _list = new Hue[3000];
                        for (int i = 0; i < 3000; i++)
                            _list[i] = new Hue(i, loader);
                    }
                }
                return _list;
            }
        }

        public static Hue GetHue(int index)
        {
            if (List != null && index >= 0 && index < List.Length)
                return List[index];

            return new Hue(index, Files.Manager?.Hues);
        }

        public static void ExportHueList(string fileName)
        {
            var sb = new StringBuilder();
            foreach (var hue in List)
            {
                sb.AppendLine(hue.Name);
                sb.AppendLine(hue.TableStart.ToString());
                sb.AppendLine(hue.TableEnd.ToString());
                for (int i = 0; i < 32; i++)
                    sb.AppendLine(hue.Colors[i].ToString());
            }
            File.WriteAllText(fileName, sb.ToString());
        }

        public static void ApplyTo(Bitmap bmp, ushort[] colors, bool onlyHueGrayPixels)
        {
            if (bmp == null || colors == null) return;

            try
            {
                var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                try
                {
                    int[] pixels = new int[bmp.Width * bmp.Height];
                    Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);

                    for (int i = 0; i < pixels.Length; i++)
                    {
                        int pixel = pixels[i];
                        byte a = (byte)((pixel >> 24) & 0xFF);
                        if (a == 0) continue;

                        byte r = (byte)((pixel >> 16) & 0xFF);
                        byte g = (byte)((pixel >> 8) & 0xFF);
                        byte b = (byte)(pixel & 0xFF);

                        if (onlyHueGrayPixels && (r != g || r != b))
                            continue;

                        int grayIndex = (r * 31 + 128) / 255;
                        grayIndex = Math.Max(0, Math.Min(31, grayIndex));

                        ushort val = colors[grayIndex];
                        byte hr = (byte)((val >> 10) & 0x1F);
                        byte hg = (byte)((val >> 5) & 0x1F);
                        byte hb = (byte)(val & 0x1F);

                        hr = (byte)(hr * 255 / 31);
                        hg = (byte)(hg * 255 / 31);
                        hb = (byte)(hb * 255 / 31);

                        pixels[i] = (a << 24) | (hr << 16) | (hg << 8) | hb;
                    }

                    Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
                }
                finally
                {
                    bmp.UnlockBits(data);
                }
            }
            catch
            {
            }
        }

        public static void Save(string path)
        {
            // Stub - hue save not implemented
        }
    }
}
