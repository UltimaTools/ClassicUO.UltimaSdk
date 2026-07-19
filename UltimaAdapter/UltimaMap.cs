using System;
using System.Collections.Generic;
using Ultima.Drawing;
using Ultima.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace Ultima
{
    public sealed class TileMatrixPatch
    {
        public bool IsLandBlockPatched(int x, int y) => false;
        public bool IsStaticBlockPatched(int x, int y) => false;
        public Tile[] GetLandBlock(int x, int y) => TileMatrixBackend.InvalidLandBlock;
        public HuedTile[][][] GetStaticBlock(int x, int y) => TileMatrixBackend.EmptyStaticBlock;
        public int LandBlocksCount => 0;
        public int StaticBlocksCount => 0;
        public Tile GetLandTile(int x, int y) => new Tile(0, (sbyte)0);
        public HuedTile[] GetStaticTiles(int x, int y) => Array.Empty<HuedTile>();
    }
}

namespace Ultima
{
    public sealed class TileMatrix
    {
        private readonly int _mapIndex;
        private readonly TileMatrixBackend _backend;

        public int BlockWidth => _backend.BlockWidth;
        public int BlockHeight => _backend.BlockHeight;

        public bool StaticIndexInit
        {
            get => _backend.StaticIndexInit;
            set => _backend.StaticIndexInit = value;
        }

        public TileMatrixPatch Patch { get; } = new TileMatrixPatch();

        public TileMatrix(int mapIndex)
        {
            _mapIndex = mapIndex;
            _backend = new TileMatrixBackend(mapIndex);
        }

        public TileMatrix(int fileIndex, int mapId, int width, int height, string path)
            : this(mapId)
        {
        }

        public bool AllFilesExist()
        {
            return _backend != null;
        }

        public Tile GetLandTile(int x, int y, bool patch = false)
        {
            return _backend.GetLandTile(x, y, patch);
        }

        public Tile[] GetLandBlock(int x, int y, bool patch = true)
        {
            return _backend.GetLandBlock(x, y, patch);
        }

        public HuedTile[] GetStaticTiles(int x, int y, bool patch = false)
        {
            return _backend.GetStaticTiles(x, y, patch);
        }

        public HuedTile[][][] GetStaticBlock(int x, int y, bool patch = true)
        {
            return _backend.GetStaticBlock(x, y, patch);
        }

        public void CloseStreams()
        {
            _backend.CloseStreams();
        }

        public bool PendingStatic(int blockX, int blockY)
        {
            return _backend.PendingStatic(blockX, blockY);
        }

        public StaticTile[] GetPendingStatics(int blockX, int blockY)
        {
            return _backend.GetPendingStatics(blockX, blockY);
        }

        public bool IsStaticBlockRemoved(int blockX, int blockY)
        {
            return _backend.IsStaticBlockRemoved(blockX, blockY);
        }

        public void RemoveStaticBlock(int blockX, int blockY)
        {
            _backend.RemoveStaticBlock(blockX, blockY);
        }

        public void AddPendingStatic(int blockX, int blockY, StaticTile toAdd)
        {
            _backend.AddPendingStatic(blockX, blockY, toAdd);
        }

        public void SetLandBlock(int x, int y, Tile[] value)
        {
            _backend.SetLandBlock(x, y, value);
        }
    }

    internal sealed class TileMatrixBackend
    {
        private readonly int _fileIndex;
        private readonly HuedTile[][][][][] _staticTiles;
        private readonly Tile[][][] _landTiles;
        private bool[][] _removedStaticBlock;
        private List<StaticTile>[][] _staticTilesToAdd;

        public static Tile[] InvalidLandBlock { get; private set; }
        public static HuedTile[][][] EmptyStaticBlock { get; private set; }

        private FileStream _map;
        private BinaryReader _uopReader;
        private FileStream _statics;
        private Entry3D[] _staticIndex;

        public bool StaticIndexInit { get; set; }

        public int BlockWidth { get; }
        public int BlockHeight { get; }
        public int Width { get; }
        public int Height { get; }

        private readonly string _mapPath;
        private readonly string _indexPath;
        private readonly string _staticsPath;

        public bool IsUOPFormat { get; set; }
        public bool IsUOPAlreadyRead { get; set; }

        public TileMatrixBackend(int fileIndex)
        {
            _fileIndex = fileIndex;

            var dims = _defaultDimensions;
            if (fileIndex >= 0 && fileIndex < _defaultDimensions.Length)
            {
                Width = dims[fileIndex].Width;
                Height = dims[fileIndex].Height;
            }

            BlockWidth = Width >> 3;
            BlockHeight = Height >> 3;

            _mapPath = ResolvePath($"map{fileIndex}LegacyMUL.uop");
            if (string.IsNullOrEmpty(_mapPath) || !File.Exists(_mapPath))
            {
                _mapPath = Files.GetFilePath($"map{fileIndex}.mul");
                if (string.IsNullOrEmpty(_mapPath) || !File.Exists(_mapPath))
                    _mapPath = null;
            }
            else
            {
                IsUOPFormat = true;
            }

            _indexPath = Files.GetFilePath($"staidx{fileIndex}.mul");
            _staticsPath = Files.GetFilePath($"statics{fileIndex}.mul");

            EmptyStaticBlock = new HuedTile[8][][];
            for (int i = 0; i < 8; ++i)
            {
                EmptyStaticBlock[i] = new HuedTile[8][];
                for (int j = 0; j < 8; ++j)
                    EmptyStaticBlock[i][j] = Array.Empty<HuedTile>();
            }

            InvalidLandBlock = new Tile[196];

            _landTiles = new Tile[BlockWidth][][];
            _staticTiles = new HuedTile[BlockWidth][][][][];
        }

        private static string ResolvePath(string file)
        {
            string baseDir = !string.IsNullOrEmpty(Files.RootDir) ? Files.RootDir : Files.Directory;
            if (!string.IsNullOrEmpty(baseDir))
            {
                string candidate = System.IO.Path.Combine(baseDir, file);
                if (File.Exists(candidate))
                    return candidate;
            }
            return Files.GetFilePath(file);
        }

        private static readonly (int Width, int Height)[] _defaultDimensions = new (int, int)[]
        {
            (6144, 4096), (6144, 4096), (2304, 1600), (2560, 2048), (1448, 1448), (1280, 4096)
        };

        public void CloseStreams()
        {
            _map?.Close();
            _uopReader?.Close();
            _statics?.Close();
        }

        public Tile GetLandTile(int x, int y, bool patch)
        {
            return GetLandBlock(x >> 3, y >> 3, patch)[((y & 0x7) << 3) + (x & 0x7)];
        }

        public HuedTile[] GetStaticTiles(int x, int y, bool patch)
        {
            return GetStaticBlock(x >> 3, y >> 3, patch)[x & 0x7][y & 0x7];
        }

        public Tile[] GetLandBlock(int x, int y, bool patch = true)
        {
            if (x < 0 || y < 0 || x >= BlockWidth || y >= BlockHeight)
                return InvalidLandBlock;

            if (_landTiles[x] == null)
                _landTiles[x] = new Tile[BlockHeight][];

            Tile[] tiles = _landTiles[x][y] ?? (_landTiles[x][y] = ReadLandBlock(x, y));

            return tiles ?? InvalidLandBlock;
        }

        public HuedTile[][][] GetStaticBlock(int x, int y, bool patch = true)
        {
            if (x < 0 || y < 0 || x >= BlockWidth || y >= BlockHeight)
                return EmptyStaticBlock;

            if (_staticTiles[x] == null)
                _staticTiles[x] = new HuedTile[BlockHeight][][][];

            HuedTile[][][] tiles = _staticTiles[x][y] ?? (_staticTiles[x][y] = ReadStaticBlock(x, y));

            return tiles ?? EmptyStaticBlock;
        }

        public void SetLandBlock(int x, int y, Tile[] value)
        {
            if (x < 0 || y < 0 || x >= BlockWidth || y >= BlockHeight)
                return;

            if (_landTiles[x] == null)
                _landTiles[x] = new Tile[BlockHeight][];

            _landTiles[x][y] = value;
        }

        public void RemoveStaticBlock(int blockX, int blockY)
        {
            if (_removedStaticBlock == null)
                _removedStaticBlock = new bool[BlockWidth][];

            if (_removedStaticBlock[blockX] == null)
                _removedStaticBlock[blockX] = new bool[BlockHeight];

            _removedStaticBlock[blockX][blockY] = true;

            if (_staticTiles[blockX] == null)
                _staticTiles[blockX] = new HuedTile[BlockHeight][][][];

            _staticTiles[blockX][blockY] = EmptyStaticBlock;
        }

        public bool IsStaticBlockRemoved(int blockX, int blockY)
        {
            if (_removedStaticBlock?[blockX] == null)
                return false;

            return _removedStaticBlock[blockX][blockY];
        }

        public bool PendingStatic(int blockX, int blockY)
        {
            if (_staticTilesToAdd?[blockY] == null)
                return false;

            if (_staticTilesToAdd[blockY][blockX] == null)
                return false;

            return true;
        }

        public void AddPendingStatic(int blockX, int blockY, StaticTile toAdd)
        {
            if (_staticTilesToAdd == null)
                _staticTilesToAdd = new List<StaticTile>[BlockHeight][];

            if (_staticTilesToAdd[blockY] == null)
                _staticTilesToAdd[blockY] = new List<StaticTile>[BlockWidth];

            if (_staticTilesToAdd[blockY][blockX] == null)
                _staticTilesToAdd[blockY][blockX] = new List<StaticTile>();

            _staticTilesToAdd[blockY][blockX].Add(toAdd);
        }

        public StaticTile[] GetPendingStatics(int blockX, int blockY)
        {
            if (_staticTilesToAdd?[blockY] == null)
                return null;

            if (_staticTilesToAdd[blockY][blockX] == null)
                return null;

            return _staticTilesToAdd[blockY][blockX].ToArray();
        }

        private void InitStatics()
        {
            _staticIndex = new Entry3D[BlockHeight * BlockWidth];
            if (_indexPath == null)
                return;

            using (var index = new FileStream(_indexPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                _statics = new FileStream(_staticsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                GCHandle gc = GCHandle.Alloc(_staticIndex, GCHandleType.Pinned);
                var buffer = new byte[index.Length];
                index.Read(buffer, 0, (int)index.Length);
                Marshal.Copy(buffer, 0, gc.AddrOfPinnedObject(), (int)Math.Min(index.Length, BlockHeight * BlockWidth * 12));
                gc.Free();
                for (var i = (int)Math.Min(index.Length, BlockHeight * BlockWidth); i < BlockHeight * BlockWidth; ++i)
                {
                    _staticIndex[i].Lookup = -1;
                    _staticIndex[i].Length = -1;
                    _staticIndex[i].Extra = -1;
                }

                StaticIndexInit = true;
            }
        }

        private static HuedTileList[][] _lists;
        private static byte[] _buffer;

        private unsafe HuedTile[][][] ReadStaticBlock(int x, int y)
        {
            if (!StaticIndexInit)
                InitStatics();

            if (_statics?.CanRead != true || !_statics.CanSeek)
                _statics = _staticsPath == null ? null : new FileStream(_staticsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            if (_statics == null)
                return EmptyStaticBlock;

            int lookup = _staticIndex[(x * BlockHeight) + y].Lookup;
            int length = _staticIndex[(x * BlockHeight) + y].Length;

            if (lookup < 0 || length <= 0)
                return EmptyStaticBlock;

            int count = length / 7;

            _statics.Seek(lookup, SeekOrigin.Begin);

            if (_buffer == null || _buffer.Length < length)
                _buffer = new byte[length];

            GCHandle gc = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
            try
            {
                _statics.Read(_buffer, 0, length);

                if (_lists == null)
                {
                    _lists = new HuedTileList[8][];
                    for (int i = 0; i < 8; ++i)
                    {
                        _lists[i] = new HuedTileList[8];
                        for (int j = 0; j < 8; ++j)
                            _lists[i][j] = new HuedTileList();
                    }
                }

                HuedTileList[][] lists = _lists;

                for (int i = 0; i < count; ++i)
                {
                    var ptr = new IntPtr((long)gc.AddrOfPinnedObject() + (i * sizeof(StaticTile)));
                    var cur = (StaticTile)Marshal.PtrToStructure(ptr, typeof(StaticTile));
                    lists[cur.X & 0x7][cur.Y & 0x7].Add(Art.GetLegalItemId(cur.Id), cur.Hue, cur.Z);
                }

                var tiles = new HuedTile[8][][];

                for (int i = 0; i < 8; ++i)
                {
                    tiles[i] = new HuedTile[8][];
                    for (int j = 0; j < 8; ++j)
                        tiles[i][j] = lists[i][j].ToArray();
                }

                return tiles;
            }
            finally
            {
                gc.Free();
            }
        }

        private readonly struct UopFile
        {
            public readonly long Offset;
            public readonly int Length;

            public UopFile(long offset, int length) { Offset = offset; Length = length; }
        }

        private UopFile[] UOPFiles { get; set; }

        private void ReadUOPFiles(string pattern)
        {
            _uopReader = new BinaryReader(_map);
            _uopReader.BaseStream.Seek(0, SeekOrigin.Begin);

            if (_uopReader.ReadInt32() != 0x50594D)
                throw new ArgumentException("Bad UOP file.");

            _uopReader.ReadInt64();
            long nextBlock = _uopReader.ReadInt64();
            _uopReader.ReadInt32();
            int count = _uopReader.ReadInt32();

            UOPFiles = new UopFile[count];

            var hashes = new Dictionary<ulong, int>();

            for (int i = 0; i < count; i++)
            {
                string file = $"build/{pattern}/{i:D8}.dat";
                ulong hash = HashFileName(file);
                if (!hashes.ContainsKey(hash)) hashes[hash] = i;
            }

            _uopReader.BaseStream.Seek(nextBlock, SeekOrigin.Begin);

            do
            {
                int filesCount = _uopReader.ReadInt32();
                nextBlock = _uopReader.ReadInt64();

                for (int i = 0; i < filesCount; i++)
                {
                    long offset = _uopReader.ReadInt64();
                    int headerLength = _uopReader.ReadInt32();
                    int compressedLength = _uopReader.ReadInt32();
                    int decompressedLength = _uopReader.ReadInt32();
                    ulong hash = _uopReader.ReadUInt64();
                    _uopReader.ReadUInt32();
                    short flag = _uopReader.ReadInt16();

                    int length = flag == 1 ? compressedLength : decompressedLength;

                    if (offset == 0)
                        continue;

                    if (hashes.TryGetValue(hash, out int idx))
                    {
                        if (idx < 0 || idx > UOPFiles.Length)
                            throw new IndexOutOfRangeException("hashes dictionary and files collection have different count of entries!");

                        UOPFiles[idx] = new UopFile(offset + headerLength, length);
                    }
                    else
                    {
                        throw new ArgumentException($"File with hash 0x{hash:X8} was not found in hashes dictionary!");
                    }
                }
            }
            while (_uopReader.BaseStream.Seek(nextBlock, SeekOrigin.Begin) != 0);
        }

        private long CalculateOffsetFromUOP(long offset)
        {
            long pos = 0;

            foreach (UopFile t in UOPFiles)
            {
                long currentPosition = pos + t.Length;
                if (offset < currentPosition)
                    return t.Offset + (offset - pos);
                pos = currentPosition;
            }

            return _map.Length;
        }

        private static ulong HashFileName(string s)
        {
            uint eax, ecx, edx, ebx, esi, edi;
            eax = ecx = edx = ebx = esi = edi = 0;
            ebx = edi = esi = (uint)s.Length + 0xDEADBEEF;
            int i = 0;
            for (i = 0; i + 12 < s.Length; i += 12)
            {
                edi = (uint)((s[i + 7] << 24) | (s[i + 6] << 16) | (s[i + 5] << 8) | s[i + 4]) + edi;
                esi = (uint)((s[i + 11] << 24) | (s[i + 10] << 16) | (s[i + 9] << 8) | s[i + 8]) + esi;
                edx = (uint)((s[i + 3] << 24) | (s[i + 2] << 16) | (s[i + 1] << 8) | s[i]) - esi;
                edx = (edx + ebx) ^ (esi >> 28) ^ (esi << 4);
                esi += edi;
                edi = (edi - edx) ^ (edx >> 26) ^ (edx << 6);
                edx += esi;
                esi = (esi - edi) ^ (edi >> 24) ^ (edi << 8);
                edi += edx;
                ebx = (edx - esi) ^ (esi >> 16) ^ (esi << 16);
                esi += edi;
                edi = (edi - ebx) ^ (ebx >> 13) ^ (ebx << 19);
                ebx += esi;
                esi = (esi - edi) ^ (edi >> 28) ^ (edi << 4);
                edi += ebx;
            }
            if (s.Length - i > 0)
            {
                switch (s.Length - i)
                {
                    case 12: esi += (uint)s[i + 11] << 24; goto case 11;
                    case 11: esi += (uint)s[i + 10] << 16; goto case 10;
                    case 10: esi += (uint)s[i + 9] << 8; goto case 9;
                    case 9: esi += (uint)s[i + 8]; goto case 8;
                    case 8: edi += (uint)s[i + 7] << 24; goto case 7;
                    case 7: edi += (uint)s[i + 6] << 16; goto case 6;
                    case 6: edi += (uint)s[i + 5] << 8; goto case 5;
                    case 5: edi += (uint)s[i + 4]; goto case 4;
                    case 4: ebx += (uint)s[i + 3] << 24; goto case 3;
                    case 3: ebx += (uint)s[i + 2] << 16; goto case 2;
                    case 2: ebx += (uint)s[i + 1] << 8; goto case 1;
                    case 1: ebx += (uint)s[i]; break;
                }
                esi = (esi ^ edi) - ((edi >> 18) ^ (edi << 14));
                ecx = (esi ^ ebx) - ((esi >> 21) ^ (esi << 11));
                edi = (edi ^ ecx) - ((ecx >> 7) ^ (ecx << 25));
                esi = (esi ^ edi) - ((edi >> 16) ^ (edi << 16));
                edx = (esi ^ ecx) - ((esi >> 28) ^ (esi << 4));
                edi = (edi ^ edx) - ((edx >> 18) ^ (edx << 14));
                eax = (esi ^ edi) - ((edi >> 8) ^ (edi << 24));
                return ((ulong)edi << 32) | eax;
            }
            return ((ulong)esi << 32) | eax;
        }

        private Tile[] ReadLandBlock(int x, int y)
        {
            if (_map?.CanRead != true || !_map.CanSeek)
            {
                _map = _mapPath == null ? null : new FileStream(_mapPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                if (IsUOPFormat && _mapPath != null && !IsUOPAlreadyRead)
                {
                    var fi = new FileInfo(_mapPath);
                    string uopPattern = fi.Name.Replace(fi.Extension, "").ToLowerInvariant();
                    ReadUOPFiles(uopPattern);
                    IsUOPAlreadyRead = true;
                }
            }

            var tiles = new Tile[64];
            if (_map == null)
                return tiles;

            long offset = (((x * BlockHeight) + y) * 196) + 4;

            if (IsUOPFormat)
                offset = CalculateOffsetFromUOP(offset);

            _map.Seek(offset, SeekOrigin.Begin);

            GCHandle gc = GCHandle.Alloc(tiles, GCHandleType.Pinned);
            try
            {
                if (_buffer == null || _buffer.Length < 192)
                    _buffer = new byte[192];

                _map.Read(_buffer, 0, 192);
                Marshal.Copy(_buffer, 0, gc.AddrOfPinnedObject(), 192);
            }
            finally
            {
                gc.Free();
            }

            return tiles;
        }
    }

    public sealed class Map
    {
        public static List<Map> Maps { get; private set; } = new List<Map>();

        private TileMatrix _tiles;
        private readonly int _mapId;
        private readonly string _path;
        private static bool _useDiff;
        private static int[] _zeroHeightTable = new int[0x10000];

        public static bool UseDiff
        {
            get => _useDiff;
            set
            {
                _useDiff = value;
                Reload();
            }
        }

        public static void LoadMapsFromMulPath()
        {
            Maps.Clear();

            if (string.IsNullOrEmpty(Files.RootDir))
                return;

            try { LoadMapsFromFolder(Files.RootDir); }
            catch { }
        }

        public static void LoadMapsFromFolder(string folder)
        {
            Maps.Clear();

            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                return;

            var files = new List<string>();
            try
            {
                files.AddRange(Directory.GetFiles(folder, "map*.mul", SearchOption.TopDirectoryOnly));
                files.AddRange(Directory.GetFiles(folder, "map*legacymul.uop", SearchOption.TopDirectoryOnly));
            }
            catch { }

            var indices = new HashSet<int>();

            foreach (var f in files)
            {
                var name = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();

                if (!name.StartsWith("map"))
                    continue;

                int pos = 3;
                int len = name.Length;
                var digits = new System.Text.StringBuilder();
                while (pos < len && char.IsDigit(name[pos]))
                {
                    digits.Append(name[pos]);
                    pos++;
                }

                if (digits.Length == 0)
                    continue;

                if (int.TryParse(digits.ToString(), out int idx))
                    indices.Add(idx);
            }

            var sorted = new List<int>(indices);
            sorted.Sort();

            foreach (int idx in sorted)
            {
                var map = new Map(folder, idx, idx);
                Maps.Add(map);
            }
        }

        public static Map GetMapByFileIndex(int index)
        {
            foreach (var m in Maps)
            {
                if (m.FileIndex == index)
                    return m;
            }
            return null;
        }

        public static Map Felucca = new Map(0, 0, 6144, 4096);
        public static Map Trammel = new Map(0, 1, 6144, 4096);
        public static readonly Map Ilshenar = new Map(2, 2, 2304, 1600);
        public static readonly Map Malas = new Map(3, 3, 2560, 2048);
        public static readonly Map Tokuno = new Map(4, 4, 1448, 1448);
        public static readonly Map TerMur = new Map(5, 5, 1280, 4096);

        public static Map Custom;

        public static void StartUpSetDiff(bool value)
        {
            _useDiff = value;
        }

        public Map(int fileIndex, int mapId, int width, int height)
        {
            FileIndex = fileIndex;
            _mapId = mapId;
            Width = width;
            Height = height;
            _path = null;
            SizeLabel = $"{width}x{height}";
        }

        public Map(string path, int fileIndex, int mapId, int width, int height)
        {
            FileIndex = fileIndex;
            _mapId = mapId;
            Width = width;
            Height = height;
            _path = path;
            SizeLabel = $"{width}x{height}";
        }

        public Map(string path, int fileIndex, int mapId)
        {
            FileIndex = fileIndex;
            _mapId = mapId;
            _path = path;
            Width = 6144;
            Height = 4096;
            SizeLabel = "6144x4096";
        }

        public TileMatrix Tiles => _tiles ??= new TileMatrix(_mapId);

        public int Width { get; set; }
        public int Height { get; }
        public int FileIndex { get; }
        public string SizeLabel { get; private set; }

        private bool _isCachedDefault;
        private bool _isCachedNoStatics;
        private bool _isCachedNoPatch;
        private bool _isCachedNoStaticsNoPatch;

        private ushort[][][] _cache;
        private ushort[][][] _cacheNoStatics;
        private ushort[][][] _cacheNoPatch;
        private ushort[][][] _cacheNoStaticsNoPatch;
        private ushort[] _black;

        public bool IsCached(bool statics)
        {
            if (UseDiff) return !statics ? _isCachedNoStatics : _isCachedDefault;
            return !statics ? _isCachedNoStaticsNoPatch : _isCachedNoPatch;
        }

        public static void Reload()
        {
            Felucca.Tiles.CloseStreams();
            Trammel.Tiles.CloseStreams();
            Ilshenar.Tiles.CloseStreams();
            Malas.Tiles.CloseStreams();
            Tokuno.Tiles.CloseStreams();
            TerMur.Tiles.CloseStreams();

            Felucca.Tiles.StaticIndexInit = false;
            Trammel.Tiles.StaticIndexInit = false;
            Ilshenar.Tiles.StaticIndexInit = false;
            Malas.Tiles.StaticIndexInit = false;
            Tokuno.Tiles.StaticIndexInit = false;
            TerMur.Tiles.StaticIndexInit = false;

            Felucca.ResetCache();
            Trammel.ResetCache();
            Ilshenar.ResetCache();
            Malas.ResetCache();
            Tokuno.ResetCache();
            TerMur.ResetCache();
        }

        public void ResetCache()
        {
            _cache = _cacheNoPatch = _cacheNoStatics = _cacheNoStaticsNoPatch = null;
            _isCachedDefault = _isCachedNoStatics = _isCachedNoPatch = _isCachedNoStaticsNoPatch = false;
        }

        public Bitmap GetImage(int x, int y, int width, int height, bool statics)
        {
            var bmp = new Bitmap(width << 3, height << 3, PixelFormat.Format16bppRgb555);
            GetImage(x, y, width, height, bmp, statics);
            return bmp;
        }

        public void PreloadRenderedBlock(int x, int y, bool statics)
        {
            TileMatrix matrix = Tiles;

            if (x < 0 || y < 0 || x >= matrix.BlockWidth || y >= matrix.BlockHeight)
            {
                if (_black == null) _black = new ushort[64];
                return;
            }

            ushort[][][] cache;
            if (UseDiff)
            {
                if (statics) _isCachedDefault = true;
                else _isCachedNoStatics = true;
                cache = (statics ? _cache : _cacheNoStatics);
            }
            else
            {
                if (statics) _isCachedNoPatch = true;
                else _isCachedNoStaticsNoPatch = true;
                cache = (statics ? _cacheNoPatch : _cacheNoStaticsNoPatch);
            }

            if (cache == null)
            {
                if (UseDiff)
                    cache = statics ? (_cache = new ushort[matrix.BlockHeight][][]) : (_cacheNoStatics = new ushort[matrix.BlockHeight][][]);
                else
                    cache = statics ? (_cacheNoPatch = new ushort[matrix.BlockHeight][][]) : (_cacheNoStaticsNoPatch = new ushort[matrix.BlockHeight][][]);
            }

            if (cache[y] == null)
                cache[y] = new ushort[matrix.BlockWidth][];

            if (cache[y][x] == null)
                cache[y][x] = RenderBlock(x, y, statics, UseDiff);

            matrix.CloseStreams();
        }

        private ushort[] GetRenderedBlock(int x, int y, bool statics)
        {
            TileMatrix matrix = Tiles;

            if (x < 0 || y < 0 || x >= matrix.BlockWidth || y >= matrix.BlockHeight)
                return _black ??= new ushort[64];

            ushort[][][] cache;
            if (UseDiff)
                cache = (statics ? _cache : _cacheNoStatics);
            else
                cache = (statics ? _cacheNoPatch : _cacheNoStaticsNoPatch);

            if (cache == null)
            {
                if (UseDiff)
                    cache = statics ? (_cache = new ushort[matrix.BlockHeight][][]) : (_cacheNoStatics = new ushort[matrix.BlockHeight][][]);
                else
                    cache = statics ? (_cacheNoPatch = new ushort[matrix.BlockHeight][][]) : (_cacheNoStaticsNoPatch = new ushort[matrix.BlockHeight][][]);
            }

            if (cache[y] == null)
                cache[y] = new ushort[matrix.BlockWidth][];

            ushort[] data = cache[y][x];

            if (data == null)
                cache[y][x] = data = RenderBlock(x, y, statics, UseDiff);

            return data;
        }

        private unsafe ushort[] RenderBlock(int x, int y, bool drawStatics, bool diff)
        {
            var data = new ushort[64];

            Tile[] tiles = Tiles.GetLandBlock(x, y, diff);

            int[] heightTable = TileData.HeightTable;
            if (heightTable == null || heightTable.Length == 0)
                heightTable = _zeroHeightTable;
            fixed (ushort* pColors = RadarCol.Colors)
            {
                fixed (int* pHeight = heightTable)
                {
                    fixed (Tile* ptTiles = tiles)
                    {
                        Tile* pTiles = ptTiles;

                        fixed (ushort* pData = data)
                        {
                            ushort* pvData = pData;

                            if (drawStatics)
                            {
                                HuedTile[][][] statics = Tiles.GetStaticBlock(x, y, diff);

                                for (int k = 0; k < 8; ++k)
                                {
                                    for (int p = 0; p < 8; ++p)
                                    {
                                        int highTop = -255;
                                        int highZ = -255;
                                        int highId = 0;
                                        int highHue = 0;
                                        int z, top;
                                        bool highStatic = false;

                                        HuedTile[] curStatics = statics[p][k];

                                        if (curStatics.Length > 0)
                                        {
                                            fixed (HuedTile* phtStatics = curStatics)
                                            {
                                                HuedTile* pStatics = phtStatics;
                                                HuedTile* pStaticsEnd = pStatics + curStatics.Length;

                                                while (pStatics < pStaticsEnd)
                                                {
                                                    z = pStatics->Z;
                                                    top = z + pHeight[pStatics->Id];

                                                    if (top > highTop || (z > highZ && top >= highTop))
                                                    {
                                                        highTop = top;
                                                        highZ = z;
                                                        highId = pStatics->Id;
                                                        highHue = pStatics->Hue;
                                                        highStatic = true;
                                                    }

                                                    ++pStatics;
                                                }
                                            }
                                        }

                                        z = pTiles->Z;
                                        top = z;

                                        if (top > highTop)
                                        {
                                            highId = pTiles->Id;
                                            highHue = 0;
                                            highStatic = false;
                                        }

                                        if (highHue == 0)
                                        {
                                            try
                                            {
                                                if (highStatic)
                                                    *pvData++ = pColors[highId + 0x4000];
                                                else
                                                    *pvData++ = pColors[highId];
                                            }
                                            catch { }
                                        }
                                        else
                                        {
                                            *pvData++ = Hues.GetHue(highHue - 1).Colors[(pColors[highId + 0x4000] >> 10) & 0x1F];
                                        }

                                        ++pTiles;
                                    }
                                }
                            }
                            else
                            {
                                Tile* pEnd = pTiles + 64;
                                while (pTiles < pEnd)
                                    *pvData++ = pColors[(pTiles++)->Id];
                            }
                        }
                    }
                }
            }

            return data;
        }

        public void GetImage(int x, int y, int width, int height, Bitmap bmp)
        {
            GetImage(x, y, width, height, bmp, true);
        }

        public unsafe void GetImage(int x, int y, int width, int height, Bitmap bmp, bool statics)
        {
            BitmapData bd = bmp.LockBits(
                new Rectangle(0, 0, width << 3, height << 3), ImageLockMode.WriteOnly, PixelFormat.Format16bppRgb555);
            int stride = bd.Stride;
            int blockStride = stride << 3;

            var pStart = (byte*)bd.Scan0;

            for (int oy = 0, by = y; oy < height; ++oy, ++by, pStart += blockStride)
            {
                var pRow0 = (int*)(pStart + (0 * stride));
                var pRow1 = (int*)(pStart + (1 * stride));
                var pRow2 = (int*)(pStart + (2 * stride));
                var pRow3 = (int*)(pStart + (3 * stride));
                var pRow4 = (int*)(pStart + (4 * stride));
                var pRow5 = (int*)(pStart + (5 * stride));
                var pRow6 = (int*)(pStart + (6 * stride));
                var pRow7 = (int*)(pStart + (7 * stride));

                for (int ox = 0, bx = x; ox < width; ++ox, ++bx)
                {
                    ushort[] blockData = GetRenderedBlock(bx, by, statics);

                    fixed (ushort* pData = blockData)
                    {
                        var pvData = (int*)pData;

                        *pRow0++ = *pvData++; *pRow0++ = *pvData++; *pRow0++ = *pvData++; *pRow0++ = *pvData++;
                        *pRow1++ = *pvData++; *pRow1++ = *pvData++; *pRow1++ = *pvData++; *pRow1++ = *pvData++;
                        *pRow2++ = *pvData++; *pRow2++ = *pvData++; *pRow2++ = *pvData++; *pRow2++ = *pvData++;
                        *pRow3++ = *pvData++; *pRow3++ = *pvData++; *pRow3++ = *pvData++; *pRow3++ = *pvData++;
                        *pRow4++ = *pvData++; *pRow4++ = *pvData++; *pRow4++ = *pvData++; *pRow4++ = *pvData++;
                        *pRow5++ = *pvData++; *pRow5++ = *pvData++; *pRow5++ = *pvData++; *pRow5++ = *pvData++;
                        *pRow6++ = *pvData++; *pRow6++ = *pvData++; *pRow6++ = *pvData++; *pRow6++ = *pvData++;
                        *pRow7++ = *pvData++; *pRow7++ = *pvData++; *pRow7++ = *pvData++; *pRow7++ = *pvData;
                    }
                }
            }

            bmp.UnlockBits(bd);
            Tiles.CloseStreams();
        }

        public static void DefragStatics(string path, Map map, int width, int height, bool remove)
        {
            string indexPath = Files.GetFilePath($"staidx{map.FileIndex}.mul");
            BinaryReader indexReader;
            if (indexPath != null)
            {
                FileStream index = new FileStream(indexPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                indexReader = new BinaryReader(index);
            }
            else
            {
                return;
            }

            string staticsPath = Files.GetFilePath($"statics{map.FileIndex}.mul");

            FileStream staticsStream;
            BinaryReader staticsReader;
            if (staticsPath != null)
            {
                staticsStream = new FileStream(staticsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                staticsReader = new BinaryReader(staticsStream);
            }
            else
            {
                return;
            }

            int blockx = width >> 3;
            int blocky = height >> 3;

            string idx = System.IO.Path.Combine(path, $"staidx{map.FileIndex}.mul");
            string mul = System.IO.Path.Combine(path, $"statics{map.FileIndex}.mul");

            using (var fsidx = new FileStream(idx, FileMode.Create, FileAccess.Write, FileShare.Write))
            using (var fsmul = new FileStream(mul, FileMode.Create, FileAccess.Write, FileShare.Write))
            {
                var memidx = new MemoryStream();
                var memmul = new MemoryStream();
                using (var binidx = new BinaryWriter(memidx))
                using (var binmul = new BinaryWriter(memmul))
                {
                    for (int bx = 0; bx < blockx; ++bx)
                    {
                        for (int by = 0; by < blocky; ++by)
                        {
                            try
                            {
                                indexReader.BaseStream.Seek(((bx * blocky) + by) * 12, SeekOrigin.Begin);
                                int lookup = indexReader.ReadInt32();
                                int length = indexReader.ReadInt32();
                                int extra = indexReader.ReadInt32();

                                if (((lookup < 0 || length <= 0) && (!map.Tiles.PendingStatic(bx, by))) ||
                                    (map.Tiles.IsStaticBlockRemoved(bx, by)))
                                {
                                    binidx.Write(-1); binidx.Write(-1); binidx.Write(-1);
                                }
                                else
                                {
                                    if ((lookup >= 0) && (length > 0))
                                        staticsStream.Seek(lookup, SeekOrigin.Begin);

                                    var fsmullength = (int)binmul.BaseStream.Position;
                                    int count = length / 7;
                                    if (!remove)
                                    {
                                        bool firstitem = true;
                                        for (int i = 0; i < count; ++i)
                                        {
                                            ushort graphic = staticsReader.ReadUInt16();
                                            byte sx = staticsReader.ReadByte();
                                            byte sy = staticsReader.ReadByte();
                                            sbyte sz = staticsReader.ReadSByte();
                                            short shue = staticsReader.ReadInt16();

                                            if (graphic > Art.GetMaxItemId()) continue;
                                            if (shue < 0) shue = 0;

                                            if (firstitem) { binidx.Write((int)binmul.BaseStream.Position); firstitem = false; }

                                            binmul.Write(graphic); binmul.Write(sx); binmul.Write(sy); binmul.Write(sz); binmul.Write(shue);
                                        }

                                        StaticTile[] tileList = map.Tiles.GetPendingStatics(bx, by);
                                        if (tileList != null)
                                        {
                                            for (int i = 0; i < tileList.Length; ++i)
                                            {
                                                if (tileList[i].Id > Art.GetMaxItemId()) continue;
                                                if (tileList[i].Hue < 0) tileList[i].Hue = 0;

                                                if (firstitem) { binidx.Write((int)binmul.BaseStream.Position); firstitem = false; }

                                                binmul.Write(tileList[i].Id); binmul.Write(tileList[i].X); binmul.Write(tileList[i].Y); binmul.Write(tileList[i].Z); binmul.Write(tileList[i].Hue);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        var tileList = new StaticTile[count];
                                        int j = 0;
                                        for (int i = 0; i < count; ++i)
                                        {
                                            var tile = new StaticTile
                                            {
                                                Id = staticsReader.ReadUInt16(),
                                                X = staticsReader.ReadByte(),
                                                Y = staticsReader.ReadByte(),
                                                Z = staticsReader.ReadSByte(),
                                                Hue = staticsReader.ReadInt16()
                                            };

                                            if (tile.Id > Art.GetMaxItemId()) continue;
                                            if (tile.Hue < 0) tile.Hue = 0;

                                            bool first = true;
                                            for (int k = 0; k < j; ++k)
                                            {
                                                if ((tileList[k].Id == tile.Id) && (tileList[k].X == tile.X) && (tileList[k].Y == tile.Y) && (tileList[k].Z == tile.Z) && (tileList[k].Hue == tile.Hue))
                                                { first = false; break; }
                                            }

                                            if (!first) continue;
                                            tileList[j] = tile; j++;
                                        }

                                        if (map.Tiles.PendingStatic(bx, by))
                                        {
                                            StaticTile[] pending = map.Tiles.GetPendingStatics(bx, by);
                                            StaticTile[] old = tileList;
                                            tileList = new StaticTile[old.Length + pending.Length];
                                            old.CopyTo(tileList, 0);
                                            for (int i = 0; i < pending.Length; ++i)
                                            {
                                                if (pending[i].Id > Art.GetMaxItemId()) continue;
                                                if (pending[i].Hue < 0) pending[i].Hue = 0;

                                                bool first = true;
                                                for (int k = 0; k < j; ++k)
                                                {
                                                    if ((tileList[k].Id == pending[i].Id) && (tileList[k].X == pending[i].X) && (tileList[k].Y == pending[i].Y) && (tileList[k].Z == pending[i].Z) && (tileList[k].Hue == pending[i].Hue))
                                                    { first = false; break; }
                                                }

                                                if (first) tileList[j++] = pending[i];
                                            }
                                        }

                                        if (j > 0)
                                        {
                                            binidx.Write((int)binmul.BaseStream.Position);
                                            for (int i = 0; i < j; ++i)
                                            {
                                                binmul.Write(tileList[i].Id); binmul.Write(tileList[i].X); binmul.Write(tileList[i].Y); binmul.Write(tileList[i].Z); binmul.Write(tileList[i].Hue);
                                            }
                                        }
                                    }

                                    fsmullength = (int)binmul.BaseStream.Position - fsmullength;
                                    if (fsmullength > 0)
                                    {
                                        binidx.Write(fsmullength);
                                        if (extra == -1) extra = 0;
                                        binidx.Write(extra);
                                    }
                                    else
                                    {
                                        binidx.Write(-1); binidx.Write(-1); binidx.Write(-1);
                                    }
                                }
                            }
                            catch
                            {
                                binidx.BaseStream.Seek(((bx * blocky) + by) * 12, SeekOrigin.Begin);
                                for (; bx < blockx; ++bx)
                                {
                                    for (; by < blocky; ++by)
                                    {
                                        binidx.Write(-1); binidx.Write(-1); binidx.Write(-1);
                                    }
                                    by = 0;
                                }
                            }
                        }
                    }

                    memidx.WriteTo(fsidx);
                    memmul.WriteTo(fsmul);
                }
            }

            indexReader.Close();
            staticsReader.Close();
        }

        public static void RewriteMap(string path, int mapIndex, int width, int height)
        {
            string mapPath = Files.GetFilePath($"map{mapIndex}.mul");
            BinaryReader mapReader;
            if (mapPath != null)
            {
                FileStream mapStream = new FileStream(mapPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                mapReader = new BinaryReader(mapStream);
            }
            else
            {
                return;
            }

            int blockX = width >> 3;
            int blockY = height >> 3;

            string mulPath = System.IO.Path.Combine(path, $"map{mapIndex}.mul");

            using (var fileStream = new FileStream(mulPath, FileMode.Create, FileAccess.Write, FileShare.Write))
            {
                var memoryStream = new MemoryStream();
                using (var binaryWriter = new BinaryWriter(memoryStream))
                {
                    for (int x = 0; x < blockX; ++x)
                    {
                        for (int y = 0; y < blockY; ++y)
                        {
                            try
                            {
                                mapReader.BaseStream.Seek(((x * blockY) + y) * 196, SeekOrigin.Begin);
                                int header = mapReader.ReadInt32();
                                binaryWriter.Write(header);
                                for (int i = 0; i < 64; ++i)
                                {
                                    short tileId = mapReader.ReadInt16();
                                    sbyte z = mapReader.ReadSByte();

                                    if (tileId is < 0 or >= 0x4000)
                                        tileId = 0;

                                    binaryWriter.Write(tileId);
                                    binaryWriter.Write(z);
                                }
                            }
                            catch
                            {
                                binaryWriter.BaseStream.Seek(((x * blockY) + y) * 196, SeekOrigin.Begin);
                                for (; x < blockX; ++x)
                                {
                                    for (; y < blockY; ++y)
                                    {
                                        binaryWriter.Write(0);
                                        for (int i = 0; i < 64; ++i)
                                        {
                                            binaryWriter.Write((short)0);
                                            binaryWriter.Write((sbyte)0);
                                        }
                                    }
                                    y = 0;
                                }
                            }
                        }
                    }

                    memoryStream.WriteTo(fileStream);
                }
            }

            mapReader.Close();
        }

        public void ReportInvalidMapIDs(string reportFile)
        {
        }

        public void ReportInvisibleStatics(string reportFile)
        {
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct StaticTile
    {
        public ushort Id;
        public byte X;
        public byte Y;
        public sbyte Z;
        public short Hue;
    }
}