using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Ultima
{
    public sealed class FileIndex
    {
        public IFileAccessor FileAccessor { get; }

        public long IndexLength => FileAccessor?.IndexLength ?? 0;
        public long IdxLength => FileAccessor?.IdxLength ?? 0;

        public IEntry this[int index]
        {
            get => FileAccessor[index];
            set => FileAccessor[index] = (Entry6D)value;
        }

        private readonly string _mulPath;

        public FileIndex(string idxFile, string mulFile, int length, int file)
            : this(idxFile, mulFile, null, length, file, ".dat", -1, false)
        {
        }

        public FileIndex(string idxFile, string mulFile, string uopFile, int length, int file, string uopEntryExtension,
            int idxLength, bool hasExtra)
        {
            string idxPath = null;
            _mulPath = null;

            string baseDir = !string.IsNullOrEmpty(Files.RootDir) ? Files.RootDir : Files.Directory;

            if (!string.IsNullOrEmpty(baseDir))
            {
                idxPath = Path.Combine(baseDir, idxFile);
                if (!File.Exists(idxPath))
                    idxPath = null;

                string candidateMul = Path.Combine(baseDir, mulFile);
                string candidateUop = string.IsNullOrEmpty(uopFile) ? null : Path.Combine(baseDir, uopFile);

                if (!string.IsNullOrEmpty(candidateUop) && File.Exists(candidateUop))
                    _mulPath = candidateUop;
                else if (File.Exists(candidateMul))
                    _mulPath = candidateMul;
            }

            // Try Files.GetFilePath as fallback
            if (idxPath == null)
            {
                idxPath = Files.GetFilePath(idxFile);
            }

            if (_mulPath == null)
            {
                _mulPath = Files.GetFilePath(uopFile ?? mulFile);
            }

            if (_mulPath?.EndsWith(".uop", StringComparison.OrdinalIgnoreCase) == true)
            {
                FileAccessor = new UopFileAccessor(_mulPath, uopEntryExtension, length, idxLength, hasExtra);
            }
            else if (idxPath != null && _mulPath != null)
            {
                FileAccessor = new MulFileAccessor(idxPath, _mulPath, length);
            }
            else
            {
                return;
            }

            if (file <= -1)
                return;

            Entry5D[] verdataPatches = Verdata.Patches;
            foreach (var patch in verdataPatches)
            {
                if (patch.File != file || patch.Index < 0 || patch.Index >= length)
                    continue;

                FileAccessor.ApplyPatch(patch);
            }
        }

        public FileIndex(string idxFile, string mulFile, int file)
        {
            _mulPath = null;

            string baseDir = !string.IsNullOrEmpty(Files.RootDir) ? Files.RootDir : Files.Directory;

            string idxPath = null;
            if (!string.IsNullOrEmpty(baseDir))
            {
                idxPath = Path.Combine(baseDir, idxFile);
                if (!File.Exists(idxPath))
                    idxPath = null;

                string candidateMul = Path.Combine(baseDir, mulFile);
                if (File.Exists(candidateMul))
                    _mulPath = candidateMul;
            }

            if (idxPath == null)
                idxPath = Files.GetFilePath(idxFile);

            if (_mulPath == null)
                _mulPath = Files.GetFilePath(mulFile);

            if (idxPath != null && _mulPath != null)
            {
                FileAccessor = new MulFileAccessor(idxPath, _mulPath);
            }
            else
            {
                return;
            }

            if (file <= -1)
                return;

            foreach (var patch in Verdata.Patches)
            {
                if (patch.File != file || patch.Index < 0 || patch.Index >= (FileAccessor?.IndexLength ?? 0))
                    continue;

                FileAccessor.ApplyPatch(patch);
            }
        }
        public Stream Seek(int index, out int length, out int extra, out bool patched)
        {
            if (FileAccessor is null)
            {
                length = extra = 0;
                patched = false;
                return null;
            }

            if (index < 0 || index >= FileAccessor.IndexLength)
            {
                length = extra = 0;
                patched = false;
                return null;
            }

            IEntry e = FileAccessor.GetEntry(index);

            if (e.Lookup < 0 || (e.Lookup > 0 && e.Length == -1))
            {
                length = extra = 0;
                patched = false;
                return null;
            }

            length = e.Length & 0x7FFFFFFF;
            extra = e.Extra;

            if ((e.Length & (1 << 31)) != 0)
            {
                patched = true;
                Verdata.Seek(e.Lookup);
                return Verdata.Stream;
            }

            if (e.Length < 0)
            {
                length = extra = 0;
                patched = false;
                return null;
            }

            if ((FileAccessor.Stream?.CanRead != true) || (!FileAccessor.Stream.CanSeek))
            {
                FileAccessor.Stream = _mulPath == null ? null : new FileStream(_mulPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            }

            if (FileAccessor.Stream == null)
            {
                length = extra = 0;
                patched = false;
                return null;
            }

            if (FileAccessor.Stream.Length < e.Lookup)
            {
                length = extra = 0;
                patched = false;
                return null;
            }

            patched = false;

            FileAccessor.Stream.Seek(e.Lookup, SeekOrigin.Begin);
            return FileAccessor.Stream;
        }
        public bool Valid(int index, out int length, out int extra, out bool patched)
        {
            if (FileAccessor is null)
            {
                length = extra = 0;
                patched = false;
                return false;
            }

            if (index < 0 || index >= FileAccessor.IndexLength)
            {
                length = extra = 0;
                patched = false;
                return false;
            }

            IEntry e = FileAccessor.GetEntry(index);

            if (e.Lookup < 0)
            {
                length = extra = 0;
                patched = false;
                return false;
            }

            length = e.Length & 0x7FFFFFFF;
            extra = e.Extra;

            if ((e.Length & (1 << 31)) != 0)
            {
                patched = true;
                return true;
            }

            if (e.Length < 0)
            {
                length = extra = 0;
                patched = false;
                return false;
            }

            if ((_mulPath == null) || !File.Exists(_mulPath))
            {
                length = extra = 0;
                patched = false;
                return false;
            }

            if ((FileAccessor.Stream?.CanRead != true) || (!FileAccessor.Stream.CanSeek))
            {
                FileAccessor.Stream = new FileStream(_mulPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            }

            if (FileAccessor.Stream.Length < e.Lookup)
            {
                length = extra = 0;
                patched = false;
                return false;
            }

            patched = false;
            return true;
        }
    }

    public enum CompressionFlag
    {
        None = 0,
        Zlib = 1,
        Mythic = 3
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Entry3D : IEntry
    {
        public int lookup;
        public int length;
        public int extra;

        public int Lookup { get => lookup; set => lookup = value; }
        public int Length { get => length; set => length = value; }
        public int Extra { get => extra; set => extra = value; }
        public int DecompressedLength { get => length; set => length = value; }

        public int Extra1
        {
            get => (int)((Extra & 0xFFFF0000) >> 16);
            set => Extra = Extra & 0x0000FFFF | (value << 16);
        }

        public int Extra2
        {
            get => Extra & 0x0000FFFF;
            set => Extra = (int)((Extra & 0xFFFF0000) | (uint)value);
        }

        public CompressionFlag Flag { get => CompressionFlag.None; set { } }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Entry6D : IEntry
    {
        public IEntry Invalid => new Entry6D();

        public int Lookup { get; set; }
        public int Length { get; set; }

        private int extra1;
        private int extra2;

        public int Extra
        {
            get => extra1 << 16 | extra2;
            set
            {
                extra1 = value & 0x0000FFFF;
                extra2 = (int)((value & 0xFFFF0000) >> 16);
            }
        }

        public int DecompressedLength { get; set; }
        public int Extra1 { get; set; }
        public int Extra2 { get; set; }
        public CompressionFlag Flag { get; set; }
    }

    public interface IEntry
    {
        int Lookup { get; set; }
        int Length { get; set; }
        int Extra { get; set; }
        int DecompressedLength { get; set; }
        int Extra1 { get; set; }
        int Extra2 { get; set; }
        CompressionFlag Flag { get; set; }
    }

    public interface IFileAccessor
    {
        IEntry GetEntry(int index);
        void ApplyPatch(Entry5D patch);
        FileStream Stream { get; set; }
        int IndexLength { get; }
        long IdxLength { get; }
        IEntry this[int index] { get; set; }
    }

    public class MulFileAccessor : IFileAccessor
    {
        public Entry3D[] Index { get; }

        public long IdxLength { get; }

        public FileStream Stream { get; set; }

        public int IndexLength => Index.Length;

        public IEntry this[int index] { get => Index[index]; set => Index[index] = (Entry3D)value; }
        public MulFileAccessor(string idxPath, string path, int length)
        {
            Index = new Entry3D[length];

            // FileShare.ReadWrite lets us read these files while the live UO client has them open.
            using (var index = new FileStream(idxPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                Stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var count = (int)(index.Length / 12);
                IdxLength = index.Length;
                GCHandle gc = GCHandle.Alloc(Index, GCHandleType.Pinned);
                var buffer = new byte[index.Length];
                index.ReadStreamFully(buffer, 0, (int)index.Length);
                Marshal.Copy(buffer, 0, gc.AddrOfPinnedObject(), (int)Math.Min(IdxLength, Index.Length * 12));
                gc.Free();
                for (int i = count; i < Index.Length; ++i)
                {
                    Index[i].Lookup = -1;
                    Index[i].Length = -1;
                    Index[i].Extra = -1;
                }
            }
        }
        public MulFileAccessor(string idxPath, string path)
        {
            // FileShare.ReadWrite lets us read these files while the live UO client has them open.
            using (var index = new FileStream(idxPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                Stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var count = (int)(index.Length / 12);
                IdxLength = index.Length;
                Index = new Entry3D[count];
                GCHandle gc = GCHandle.Alloc(Index, GCHandleType.Pinned);
                var buffer = new byte[index.Length];
                index.ReadStreamFully(buffer, 0, (int)index.Length);
                Marshal.Copy(buffer, 0, gc.AddrOfPinnedObject(), (int)index.Length);
                gc.Free();
            }
        }
        public void ApplyPatch(Entry5D patch)
        {
            Index[patch.Index].Lookup = patch.Lookup;
            Index[patch.Index].Length = patch.Length | (1 << 31);
            Index[patch.Index].Extra = patch.Extra;
        }

        public IEntry GetEntry(int index)
        {
            if (index < 0 || index >= Index.Length)
                return new Entry3D();

            return Index[index];
        }
    }

    public class UopFileAccessor : IFileAccessor
    {
        public Entry6D[] Index { get; }

        public FileStream Stream { get; set; }

        public long IdxLength { get; }

        public int IndexLength => Index.Length;

        public IEntry this[int index]
        {
            get => Index[index];
            set => Index[index] = (Entry6D)value;
        }
        public UopFileAccessor(string path, string uopEntryExtension, int length, int idxLength, bool hasextra)
        {
            Index = new Entry6D[length];

            if (idxLength > 0)
                IdxLength = idxLength * 12;

            // Use ReadWrite sharing so we can read client data files while the live UO client
            // (or another tool) has them open. FileShare.Read alone throws IOException in that
            // case, which callers swallow, silently corrupting downstream data (e.g. this caused
            // Art.IsUOAHS() to misreport, making TileData parse tiledata.mul with the wrong
            // (old 4-byte-flags) record layout).
            Stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            var fileInfo = new FileInfo(path);
            string uopPattern = fileInfo.Name.Replace(fileInfo.Extension, "").ToLowerInvariant();

            using (var br = new BinaryReader(Stream))
            {
                br.BaseStream.Seek(0, SeekOrigin.Begin);

                if (br.ReadInt32() != 0x50594D)
                    throw new ArgumentException("Bad UOP file.");

                _ = br.ReadUInt32();
                _ = br.ReadUInt32();
                long nextBlock = br.ReadInt64();
                _ = br.ReadUInt32();
                _ = br.ReadInt32();

                var hashes = new System.Collections.Generic.Dictionary<ulong, int>();

                for (int i = 0; i < length; i++)
                {
                    string entryName8 = $"build/{uopPattern}/{i:D8}{uopEntryExtension}";
                    ulong hash8 = HashFileName(entryName8);
                    if (!hashes.ContainsKey(hash8)) hashes[hash8] = i;

                    string entryName6 = $"build/{uopPattern}/{i:D6}{uopEntryExtension}";
                    ulong hash6 = HashFileName(entryName6);
                    if (!hashes.ContainsKey(hash6)) hashes[hash6] = i;
                }

                br.BaseStream.Seek(nextBlock, SeekOrigin.Begin);

                for (var i = 0; i < Index.Length; i++)
                {
                    Index[i].Lookup = -1;
                    Index[i].Length = -1;
                    Index[i].Extra = -1;
                }

                do
                {
                    int filesCount = br.ReadInt32();
                    nextBlock = br.ReadInt64();

                    for (int i = 0; i < filesCount; i++)
                    {
                        long offset = br.ReadInt64();
                        int headerLength = br.ReadInt32();
                        int compressedLength = br.ReadInt32();
                        int decompressedLength = br.ReadInt32();
                        ulong hash = br.ReadUInt64();
                        _ = br.ReadUInt32();
                        short flag = br.ReadInt16();

                        if (offset == 0)
                            continue;

                        if (!hashes.TryGetValue(hash, out int idx))
                            continue;

                        if (idx < 0 || idx > Index.Length)
                            throw new IndexOutOfRangeException("hashes dictionary and files collection have different count of entries!");

                        offset += headerLength;

                        if (hasextra && flag != 3)
                        {
                            long curPos = br.BaseStream.Position;

                            br.BaseStream.Seek(offset, SeekOrigin.Begin);

                            var extra1 = br.ReadInt32();
                            var extra2 = br.ReadInt32();
                            Index[idx].Lookup = (int)(offset + 8);
                            Index[idx].Length = compressedLength - 8;
                            Index[idx].DecompressedLength = decompressedLength;
                            Index[idx].Flag = (CompressionFlag)flag;
                            Index[idx].Extra = extra1 << 16 | extra2;
                            Index[idx].Extra1 = extra1;
                            Index[idx].Extra2 = extra2;

                            br.BaseStream.Seek(curPos, SeekOrigin.Begin);
                        }
                        else
                        {
                            Index[idx].Lookup = (int)(offset);
                            Index[idx].Length = compressedLength;
                            Index[idx].DecompressedLength = decompressedLength;
                            Index[idx].Flag = (CompressionFlag)flag;
                            Index[idx].Extra = 0x0FFFFFFF;
                        }
                    }
                }
                while (br.BaseStream.Seek(nextBlock, SeekOrigin.Begin) != 0);
            }
        }
        public void ApplyPatch(Entry5D patch)
        {
            Index[patch.Index].Lookup = patch.Lookup;
            Index[patch.Index].Length = patch.Length | (1 << 31);
            Index[patch.Index].Extra = patch.Extra;
        }

        public IEntry GetEntry(int index)
        {
            if (index < 0 || index >= Index.Length)
                return new Entry6D();

            return Index[index];
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
                    case 12:
                        esi += (uint)s[i + 11] << 24;
                        goto case 11;
                    case 11:
                        esi += (uint)s[i + 10] << 16;
                        goto case 10;
                    case 10:
                        esi += (uint)s[i + 9] << 8;
                        goto case 9;
                    case 9:
                        esi += (uint)s[i + 8];
                        goto case 8;
                    case 8:
                        edi += (uint)s[i + 7] << 24;
                        goto case 7;
                    case 7:
                        edi += (uint)s[i + 6] << 16;
                        goto case 6;
                    case 6:
                        edi += (uint)s[i + 5] << 8;
                        goto case 5;
                    case 5:
                        edi += (uint)s[i + 4];
                        goto case 4;
                    case 4:
                        ebx += (uint)s[i + 3] << 24;
                        goto case 3;
                    case 3:
                        ebx += (uint)s[i + 2] << 16;
                        goto case 2;
                    case 2:
                        ebx += (uint)s[i + 1] << 8;
                        goto case 1;
                    case 1:
                        ebx += (uint)s[i];
                        break;
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

    }
}

internal static class StreamExtensions
{
    public static void ReadStreamFully(this Stream stream, byte[] buffer, int offset, int count)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = stream.Read(buffer, offset + totalRead, count - totalRead);
            if (read == 0) throw new EndOfStreamException();
            totalRead += read;
        }
    }
}