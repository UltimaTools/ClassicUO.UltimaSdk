using System;
using System.IO;
using CUOAssets = ClassicUO.Assets;
using CUOUtility = ClassicUO.Utility;

namespace Ultima
{
    public static class Files
    {
        private static CUOAssets.UOFileManager _manager;
        private static string _uoDirectory;

        public static CUOAssets.UOFileManager Manager => _manager;

        public static string Directory
        {
            get => _uoDirectory;
            set => _uoDirectory = value;
        }

        public static bool CacheData { get; set; }

        public static void Initialize(string uoPath, CUOUtility.ClientVersion version)
        {
            _uoDirectory = uoPath;
            _manager = new CUOAssets.UOFileManager(version, uoPath);
        }

        public static void Load(bool useVerdata = true, string lang = "enu", string mapsLayouts = "")
        {
            _manager?.Load(useVerdata, lang, mapsLayouts);
        }

        public static string GetFilePath(string file)
        {
            if (_manager != null)
            {
                try
                {
                    return _manager.GetUOFilePath(file);
                }
                catch
                {
                }
            }

            return Path.Combine(_uoDirectory ?? "", file);
        }

        public static void SetMulPath(string path)
        {
            _uoDirectory = path;
        }

        public static void SetMulPath(string path, string key)
        {
            if (_manager != null)
            {
                _manager.SetFileOverride(key, path);
            }
        }
    }

    public static class FilesDirectoryOverride
    {
        public static string Directory
        {
            get => Files.Directory;
            set => Files.Directory = value;
        }
    }
}
