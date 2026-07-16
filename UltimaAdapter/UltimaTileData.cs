using System;
using System.IO;
using System.Runtime.InteropServices;
using CUOAssets = ClassicUO.Assets;

namespace Ultima
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct OldLandTileDataMul
    {
        public readonly uint flags;
        public readonly ushort texID;
        public fixed byte name[20];
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct NewLandTileDataMul
    {
        public readonly ulong flags;
        public readonly ushort texID;
        public fixed byte name[20];
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct OldItemTileDataMul
    {
        public readonly uint flags;
        public readonly byte weight;
        public readonly byte quality;
        public readonly short miscData;
        public readonly byte unk2;
        public readonly byte quantity;
        public readonly short anim;
        public readonly byte unk3;
        public readonly byte hue;
        public readonly byte stackingOffset;
        public readonly byte value;
        public readonly byte height;
        public fixed byte name[20];
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct NewItemTileDataMul
    {
        public readonly ulong flags;
        public readonly byte weight;
        public readonly byte quality;
        public readonly short miscData;
        public readonly byte unk2;
        public readonly byte quantity;
        public readonly short anim;
        public readonly byte unk3;
        public readonly byte hue;
        public readonly byte stackingOffset;
        public readonly byte value;
        public readonly byte height;
        public fixed byte name[20];
    }

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

        public unsafe LandData(NewLandTileDataMul mulStruct)
        {
            TextureId = mulStruct.texID;
            Flags = (TileFlag)mulStruct.flags;
            Name = Ultima.Helpers.TileDataHelpers.ReadNameString(mulStruct.name);
        }

        public unsafe LandData(OldLandTileDataMul mulStruct)
        {
            TextureId = mulStruct.texID;
            Flags = (TileFlag)mulStruct.flags;
            Name = Ultima.Helpers.TileDataHelpers.ReadNameString(mulStruct.name);
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

        public unsafe ItemData(NewItemTileDataMul mulStruct)
        {
            _name = Ultima.Helpers.TileDataHelpers.ReadNameString(mulStruct.name);
            _flags = (TileFlag)mulStruct.flags;
            _weight = mulStruct.weight;
            _quality = mulStruct.quality;
            _count = mulStruct.quantity;
            _value = mulStruct.value;
            _height = mulStruct.height;
            _animID = (ushort)mulStruct.anim;
            _hue = mulStruct.hue;
            _layer = 0;
            _stackingOffset = mulStruct.stackingOffset;
            _miscData = mulStruct.miscData;
            _unk2 = mulStruct.unk2;
            _unk3 = mulStruct.unk3;
        }

        public unsafe ItemData(OldItemTileDataMul mulStruct)
        {
            _name = Ultima.Helpers.TileDataHelpers.ReadNameString(mulStruct.name);
            _flags = (TileFlag)mulStruct.flags;
            _weight = mulStruct.weight;
            _quality = mulStruct.quality;
            _count = mulStruct.quantity;
            _value = mulStruct.value;
            _height = mulStruct.height;
            _animID = (ushort)mulStruct.anim;
            _hue = mulStruct.hue;
            _layer = 0;
            _stackingOffset = mulStruct.stackingOffset;
            _miscData = mulStruct.miscData;
            _unk2 = mulStruct.unk2;
            _unk3 = mulStruct.unk3;
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

            string filePath = Files.GetFilePath("tiledata.mul");
            if (filePath == null)
            {
                _landTable = Array.Empty<LandData>();
                _itemTable = Array.Empty<ItemData>();
                HeightTable = Array.Empty<int>();
                return;
            }

            bool useNewFormat = Art.IsUOAHS();

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var buffer = new byte[fs.Length];
                fs.Read(buffer, 0, buffer.Length);

                var gc = System.Runtime.InteropServices.GCHandle.Alloc(buffer, System.Runtime.InteropServices.GCHandleType.Pinned);
                long pos = 0;

                try
                {
                    _landTable = new LandData[0x4000];
                    for (int i = 0; i < 0x4000; i += 32)
                    {
                        pos += 4;
                        for (int cnt = 0; cnt < 32; cnt++)
                        {
                            var ptr = new IntPtr(gc.AddrOfPinnedObject().ToInt64() + pos);
                            if (useNewFormat)
                            {
                                pos += Marshal.SizeOf<NewLandTileDataMul>();
                                _landTable[i + cnt] = new LandData((NewLandTileDataMul)Marshal.PtrToStructure(ptr, typeof(NewLandTileDataMul)));
                            }
                            else
                            {
                                pos += Marshal.SizeOf<OldLandTileDataMul>();
                                _landTable[i + cnt] = new LandData((OldLandTileDataMul)Marshal.PtrToStructure(ptr, typeof(OldLandTileDataMul)));
                            }
                        }
                    }

                    long remaining = buffer.Length - pos;
                    int structSize = useNewFormat ? Marshal.SizeOf<NewItemTileDataMul>() : Marshal.SizeOf<OldItemTileDataMul>();
                    int headerCount = (int)(remaining / ((structSize * 32) + 4));
                    int itemLength = headerCount * 32;

                    _itemTable = new ItemData[itemLength];
                    HeightTable = new int[Math.Max(itemLength, 0x4000)];

                    for (int i = 0; i < itemLength; i += 32)
                    {
                        pos += 4;
                        for (int cnt = 0; cnt < 32; cnt++)
                        {
                            var ptr = new IntPtr(gc.AddrOfPinnedObject().ToInt64() + pos);
                            if (useNewFormat)
                            {
                                pos += Marshal.SizeOf<NewItemTileDataMul>();
                                var cur = (NewItemTileDataMul)Marshal.PtrToStructure(ptr, typeof(NewItemTileDataMul));
                                _itemTable[i + cnt] = new ItemData(cur);
                                HeightTable[i + cnt] = cur.height;
                            }
                            else
                            {
                                pos += Marshal.SizeOf<OldItemTileDataMul>();
                                var cur = (OldItemTileDataMul)Marshal.PtrToStructure(ptr, typeof(OldItemTileDataMul));
                                _itemTable[i + cnt] = new ItemData(cur);
                                HeightTable[i + cnt] = cur.height;
                            }
                        }
                    }

                    for (int i = itemLength; i < HeightTable.Length; i++)
                        HeightTable[i] = 0;
                }
                finally
                {
                    gc.Free();
                }
            }
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
