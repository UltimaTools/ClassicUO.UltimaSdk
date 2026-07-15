using System;
using System.IO;
using CUOAssets = ClassicUO.Assets;

namespace Ultima
{
    public struct LandData
    {
        public string Name;
        public TileFlag Flags;
        public int TextureId;

        public LandData(string name, TileFlag flags, int textureId)
        {
            Name = name;
            Flags = flags;
            TextureId = textureId;
        }

        public void ReadData(string[] split)
        {
        }

        internal LandData(CUOAssets.LandTiles lt)
        {
            Name = lt.Name;
            Flags = (TileFlag)lt.Flags;
            TextureId = lt.TexID;
        }
    }

    public struct ItemData
    {
        private string _name;
        private TileFlag _flags;
        private int _weight;
        private int _height;
        private int _hue;
        private byte _quality;
        private byte _layer;
        private int _count;
        private ushort _animID;
        private int _value;
        private int _stackingOffset;
        private short _miscData;
        private byte _unk2;
        private byte _unk3;

        public string Name { get => _name; set => _name = value; }
        public TileFlag Flags { get => _flags; set => _flags = value; }
        public int Height { get => _height; set => _height = value; }
        public int Weight { get => _weight; set => _weight = value; }
        public int Hue { get => _hue; set => _hue = value; }
        public int Quality { get => _quality; set => _quality = (byte)value; }
        public int Layer { get => _layer; set => _layer = (byte)value; }
        public int AnimID { get => _animID; set => _animID = (ushort)value; }
        public int Animation { get => _animID; set => _animID = (ushort)value; }
        public int Quantity { get => _count; set => _count = value; }
        public int Value { get => _value; set => _value = value; }
        public int StackingOffset { get => _stackingOffset; set => _stackingOffset = value; }
        public short MiscData { get => _miscData; set => _miscData = value; }
        public byte Unk2 { get => _unk2; set => _unk2 = value; }
        public byte Unk3 { get => _unk3; set => _unk3 = value; }

        public bool Bridge => (_flags & TileFlag.Bridge) != 0;
        public bool Surface => (_flags & TileFlag.Surface) != 0;
        public bool Background => (_flags & TileFlag.Background) != 0;
        public bool Wearable
        {
            get => (_flags & TileFlag.Wearable) != 0;
            set => _flags = value ? (_flags | TileFlag.Wearable) : (_flags & ~TileFlag.Wearable);
        }

        public void ReadData(string[] split)
        {
        }

        public int CalcHeight => (_flags & TileFlag.Surface) != 0
            ? ((_flags & TileFlag.Bridge) != 0 ? _height / 2 : _height)
            : 0;

        public ItemData(string name, TileFlag flags, int unk1, int weight, int quality,
            int quantity, int value, int height, int anim, int hue,
            int stackingOffset, int miscData, int unk2, int unk3)
        {
            _name = name;
            _flags = flags;
            _weight = weight;
            _quality = (byte)quality;
            _height = height;
            _hue = hue;
            _animID = (ushort)anim;
            _layer = 0;
            _count = quantity;
            _value = value;
            _stackingOffset = stackingOffset;
            _miscData = (short)miscData;
            _unk2 = (byte)unk2;
            _unk3 = (byte)unk3;
        }

        internal ItemData(CUOAssets.StaticTiles st)
        {
            _name = st.Name;
            _flags = (TileFlag)st.Flags;
            _weight = st.Weight;
            _quality = (byte)st.Layer;
            _height = st.Height;
            _hue = st.Hue;
            _animID = st.AnimID;
            _layer = (byte)st.Layer;
            _count = st.Count;
            _value = 0;
            _stackingOffset = 0;
            _miscData = 0;
            _unk2 = 0;
            _unk3 = 0;
        }
    }

    public static class TileData
    {
        private static LandData[] _landTable;
        private static ItemData[] _itemTable;
        private static bool _initialized;

        public static LandData[] LandTable
        {
            get
            {
                EnsureInit();
                return _landTable;
            }
        }

        public static ItemData[] ItemTable
        {
            get
            {
                EnsureInit();
                return _itemTable;
            }
        }

        public static int[] HeightTable { get; private set; }

        public static void Initialize()
        {
            EnsureInit();
        }

        internal static void EnsureInit()
        {
            if (_initialized) return;
            _initialized = true;

            var td = Files.Manager?.TileData;
            if (td == null) return;

            var landSrc = td.LandData;
            _landTable = new LandData[landSrc.Length];
            for (int i = 0; i < landSrc.Length; i++)
                _landTable[i] = new LandData(landSrc[i]);

            var staticSrc = td.StaticData;
            _itemTable = new ItemData[staticSrc.Length];
            for (int i = 0; i < staticSrc.Length; i++)
                _itemTable[i] = new ItemData(staticSrc[i]);

            HeightTable = new int[_itemTable.Length];
            for (int i = 0; i < _itemTable.Length; i++)
                HeightTable[i] = _itemTable[i].Height;
        }

        public static ItemData GetItemData(int id)
        {
            EnsureInit();
            if (_itemTable == null || id < 0 || id >= _itemTable.Length)
                return default;
            return _itemTable[id];
        }

        public static LandData GetLandData(int id)
        {
            EnsureInit();
            if (_landTable == null || id < 0 || id >= _landTable.Length)
                return default;
            return _landTable[id];
        }

        public static void ExportItemDataToCsv(string fileName)
        {
            using var w = new StreamWriter(fileName);
            w.WriteLine("#;Name;Flags;Weight;Quality;Quantity;Value;Height;Animation;Hue;StackingOffset;MiscData;Unk2;Unk3");
            for (int i = 0; i < ItemTable.Length; i++)
            {
                var it = ItemTable[i];
                w.WriteLine($"{i};{it.Name};{(ulong)it.Flags};{it.Weight};{it.Quality};{it.Quantity};{it.Value};{it.Height};{it.Animation};{it.Hue};{it.StackingOffset};{it.MiscData};{it.Unk2};{it.Unk3}");
            }
        }

        public static void ExportLandDataToCsv(string fileName)
        {
            using var w = new StreamWriter(fileName);
            w.WriteLine("#;Name;Flags;TextureId");
            for (int i = 0; i < LandTable.Length; i++)
            {
                var lt = LandTable[i];
                w.WriteLine($"{i};{lt.Name};{(ulong)lt.Flags};{lt.TextureId}");
            }
        }

        public static void ImportItemDataFromCsv(string fileName)
        {
            // Stub - CSV import not implemented
        }

        public static void ImportLandDataFromCsv(string fileName)
        {
            // Stub - CSV import not implemented
        }

        public static void SaveTileData(string fileName)
        {
            // Stub - save not implemented
        }

        internal static void Reset()
        {
            _initialized = false;
            _landTable = null;
            _itemTable = null;
        }
    }
}
