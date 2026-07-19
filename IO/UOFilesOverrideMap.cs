using ClassicUO.Utility.Logging;
using System;
using System.Collections.Generic;
using System.IO;

namespace ClassicUO.IO
{
    public sealed class UOFilesOverrideMap : Dictionary<string, string>
    {
        private readonly string _OverrideFile;
        public UOFilesOverrideMap(string overrideFile = "")
        {
            _OverrideFile = overrideFile;
        }

        private static string ResolveCaseInsensitive(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            if (System.IO.File.Exists(path))
                return path;

            if (ClassicUO.Utility.Platforms.PlatformHelper.IsWindows)
                return null;

            try
            {
                string dir = System.IO.Path.GetDirectoryName(path);
                string name = System.IO.Path.GetFileName(path);
                if (System.IO.Directory.Exists(dir))
                {
                    foreach (var f in System.IO.Directory.GetFiles(dir))
                    {
                        if (string.Equals(f, path, StringComparison.OrdinalIgnoreCase))
                            return f;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        public void Load()
        {
            string resolvedPath = ResolveCaseInsensitive(_OverrideFile);
            if (resolvedPath == null)
            {
                Log.Trace($"No Override File found, ignoring.");
                return;
            }

            Log.Trace($"Loading Override File:\t\t{resolvedPath}");

            using (FileStream stream = new FileStream(resolvedPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (StreamReader reader = new StreamReader(stream))
            {
                // we will gracefully ignore any failures when trying to read
                while (!reader.EndOfStream)
                {
                    try
                    {
                        string line = reader.ReadLine();
                        string testCommentLine = line.TrimStart(' ');
                        if (testCommentLine.IndexOf(';') == 0 || testCommentLine.IndexOf('#') == 0) continue; // skip comment lines aka ; or #
                        string[] segments = line.Split('=');
                        if (segments.Length == 2)
                        {
                            string file = segments[0].ToLowerInvariant();
                            string filePath = segments[1];

                            Log.Trace($"Override entry: {file} => {filePath}.");

                            Add(file, filePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warn("Something went wrong when trying to parse UOFileOverride file.");
                        Log.Warn(ex.ToString());
                    }
                }
            }
        }
    }
}
