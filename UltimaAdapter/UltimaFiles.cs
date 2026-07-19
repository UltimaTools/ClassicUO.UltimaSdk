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

        public static CUOAssets.UOFileManager Manager
        {
            get
            {
                if (_manager == null)
                {
                    InitManager();
                }
                return _manager;
            }
        }

        private static void InitManager()
        {
            if (_manager != null) return;

            string dir = !string.IsNullOrEmpty(RootDir) ? RootDir : _uoDirectory;
            if (string.IsNullOrEmpty(dir)) return;

            try
            {
                var version = DetectVersion(dir);
                _manager = new CUOAssets.UOFileManager(version, dir);
                _manager.Load(true, "ENU");
            }
            catch
            {
                _manager = null;
            }
        }

        public static string Directory
        {
            get => _uoDirectory;
            set
            {
                _uoDirectory = value;
                RootDir = value;
                _manager?.Dispose();
                _manager = null;
            }
        }

        public static bool CacheData { get; set; } = true;

        public static string RootDir { get; set; }

        public static Dictionary<string, string> MulPath { get; set; } = new Dictionary<string, string>();

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

                // Case-insensitive fallback for Linux/macOS
                if (!ClassicUO.Utility.Platforms.PlatformHelper.IsWindows)
                {
                    try
                    {
                        var dirPath = System.IO.Path.GetFullPath(RootDir);
                        if (System.IO.Directory.Exists(dirPath))
                        {
                            foreach (var f in System.IO.Directory.GetFiles(dirPath))
                            {
                                if (string.Equals(f, candidate, StringComparison.OrdinalIgnoreCase))
                                    return f;
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }

            return Path.Combine(_uoDirectory ?? "", file);
        }

        public static void SetMulPath(string path)
        {
            _uoDirectory = path;
            RootDir = path;
            ReInitialize();
        }

        public static void SetMulPath(string path, string key)
        {
            if (_manager != null)
            {
                _manager.SetFileOverride(key, path);
            }
        }

        private static void ReInitialize()
        {
            _manager?.Dispose();
            _manager = null;

            TileData.Reset();
            Hues.Invalidate();
            Art.Reload();
            Gumps.Reload();

            if (!string.IsNullOrEmpty(RootDir))
            {
                var version = DetectVersion(RootDir);
                _manager = new CUOAssets.UOFileManager(version, RootDir);
                _manager.Load(true, "ENU");
            }
        }

        private static CUOUtility.ClientVersion DetectVersion(string path)
        {
            if (FileExistsCaseInsensitive(path, "MainMisc.uop"))
                return CUOUtility.ClientVersion.CV_7000;
            if (FileExistsCaseInsensitive(path, "art.mul") || FileExistsCaseInsensitive(path, "artidx.mul"))
                return CUOUtility.ClientVersion.CV_5090;
            return CUOUtility.ClientVersion.CV_7000;
        }

        private static bool FileExistsCaseInsensitive(string directory, string file)
        {
            string candidate = Path.Combine(directory, file);
            if (File.Exists(candidate))
                return true;

            if (ClassicUO.Utility.Platforms.PlatformHelper.IsWindows)
                return false;

            try
            {
                if (System.IO.Directory.Exists(directory))
                {
                    foreach (var f in System.IO.Directory.GetFiles(directory))
                    {
                        if (string.Equals(f, candidate, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }
            catch
            {
            }

            return false;
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
