using System;
using System.IO;

namespace Ultima
{
    public sealed class Verdata
    {
        public static Stream Stream { get; private set; }
        public static Entry5D[] Patches { get; private set; }

        private static string _path;

        static Verdata()
        {
            Initialize();
        }

        public static void Initialize()
        {
            try
            {
                _path = Files.GetFilePath("verdata.mul");
                if (string.IsNullOrEmpty(_path) || !File.Exists(_path))
                {
                    Patches = Array.Empty<Entry5D>();
                    Stream = Stream.Null;
                }
                else
                {
                    using (Stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var bin = new BinaryReader(Stream))
                    {
                        Patches = new Entry5D[bin.ReadInt32()];

                        for (int i = 0; i < Patches.Length; ++i)
                        {
                            Patches[i].File = bin.ReadInt32();
                            Patches[i].Index = bin.ReadInt32();
                            Patches[i].Lookup = bin.ReadInt32();
                            Patches[i].Length = bin.ReadInt32();
                            Patches[i].Extra = bin.ReadInt32();
                        }
                    }

                    Stream.Close();
                }
            }
            catch
            {
                Patches = Array.Empty<Entry5D>();
                Stream = Stream.Null;
            }
        }

        public static void Seek(int lookup)
        {
            if ((Stream?.CanRead != true || !Stream.CanSeek) && _path != null)
            {
                Stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            }

            _ = Stream.Seek(lookup, SeekOrigin.Begin);
        }
    }

    public struct Entry5D
    {
        public int File;
        public int Index;
        public int Lookup;
        public int Length;
        public int Extra;
    }
}