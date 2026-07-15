using System;
using System.Collections.Generic;
using System.IO;
using CUOAssets = ClassicUO.Assets;
using CUOUtility = ClassicUO.Utility;

namespace Ultima
{
    public static class Files
    {
        public delegate void FileSaveHandler();
        public static event FileSaveHandler FileSaveEvent;

        internal static void FireFileSaveEvent()
        {
            FileSaveEvent?.Invoke();
        }

        public static bool IsManagedFromRoot(string key)
        {
            return true;
        }
        private static CUOAssets.UOFileManager _manager;
        private static string _uoDirectory;
        private static bool _mulPathLocked;

        public static CUOAssets.UOFileManager Manager => _manager;

        public static string Directory
        {
            get => _uoDirectory;
            set
            {
                _uoDirectory = value;
                RootDir = value;
            }
        }

        public static bool CacheData { get; set; } = true;

        public static string RootDir { get; set; }

        public static Dictionary<string, string> MulPath { get; set; }

        public static bool MulPathLocked
        {
            get => _mulPathLocked;
            set => _mulPathLocked = value;
        }

        public static void Initialize(string uoPath, CUOUtility.ClientVersion version)
        {
            _uoDirectory = uoPath;
            RootDir = uoPath;
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

            if (!string.IsNullOrEmpty(RootDir))
            {
                string candidate = Path.Combine(RootDir, file);
                if (File.Exists(candidate))
                    return candidate;
            }

            return Path.Combine(_uoDirectory ?? "", file);
        }

        public static void SetMulPath(string path)
        {
            _uoDirectory = path;
            RootDir = path;
        }

        public static void SetMulPath(string path, string key)
        {
            if (_manager != null)
            {
                _manager.SetFileOverride(key, path);
            }
        }

        public static void LoadMulPath()
        {
            if (MulPathLocked)
                return;

            if (MulPath == null)
                MulPath = new Dictionary<string, string>();

            MulPath.Clear();
            RootDir = _uoDirectory ?? string.Empty;
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
