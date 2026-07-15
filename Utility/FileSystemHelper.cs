// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using ClassicUO.Utility.Logging;
using ClassicUO.Utility.Platforms;

namespace ClassicUO.Utility
{
    public static class FileSystemHelper
    {
        public static string CreateFolderIfNotExists(string path, params string[] parts)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            char[] invalid = Path.GetInvalidFileNameChars();

            for (int i = 0; i < parts.Length; i++)
            {
                for (int j = 0; j < invalid.Length; j++)
                {
                    parts[i] = parts[i].Replace(invalid[j].ToString(), "");
                }
            }

            var sb = new StringBuilder();

            foreach (string part in parts)
            {
                sb.Append(Path.Combine(path, part));

                string r = sb.ToString();

                if (!Directory.Exists(r))
                {
                    Directory.CreateDirectory(r);
                }

                path = r;
                sb.Clear();
            }

            return path;
        }

        public static string RemoveInvalidChars(string text)
        {
            char[] invalid = Path.GetInvalidFileNameChars();

            for (int j = 0; j < invalid.Length; j++)
            {
                text = text.Replace(invalid[j].ToString(), "");
            }

            return text;
        }

        public static void EnsureFileExists(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(path);
            }
        }


        public static void CopyAllTo(this DirectoryInfo source, DirectoryInfo target)
        {
            Directory.CreateDirectory(target.FullName);

            // Copy each file into the new directory.
            foreach (FileInfo fi in source.GetFiles())
            {
                Console.WriteLine(@"Copying {0}\{1}", target.FullName, fi.Name);
                fi.CopyTo(Path.Combine(target.FullName, fi.Name), true);
            }

            // Copy each subdirectory using recursion.
            foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
            {
                DirectoryInfo nextTargetSubDir = target.CreateSubdirectory(diSourceSubDir.Name);

                diSourceSubDir.CopyAllTo(nextTargetSubDir);
            }
        }

        public static void OpenFileWithDefaultApp(string filePath)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    ProcessStartInfo p = new ProcessStartInfo("xdg-open") { Arguments = $"\"{filePath}\"" };
                    Process.Start(p);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    ProcessStartInfo p = new ProcessStartInfo("open") { Arguments = $"\"{filePath}\"" };
                    Process.Start(p);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error opening file: " + ex.Message);
            }
        }

        public static bool OpenLocation(string dirOrFilePath)
        {
            try
            {
                string dir = Path.GetDirectoryName(dirOrFilePath);
                if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                    return false;

                // This may not be 100% water-tight.
                // Think this may work better than relying on ton xdg-open for Linux, though.
                Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true, Verb = "open" });

                // We return a 'true' here to avoid having to wait sync on the UI thread (since async introduces some undue complexity).
                // Suboptimal but good enough for this case. The same issue is already present in `OpenFileWithDefaultApp` equivalent
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Error opening directory '{dirOrFilePath}': {ex.Message}");
                return false;
            }
        }

        // This method attempts to find the correct case-sensitive path for a given file path on case-sensitive file systems (like Linux).
        // In case of any failure, just returns original path, which is fine because it could be legit file not found
        public static string GetCaseInsensitiveFilePath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("Invalid file path");
            }

            // On Windows, file system is already case-insensitive, so skip the extra work
            if (PlatformHelper.IsWindows)
            {
                return filePath;
            }

            try
            {
                string root = Path.GetPathRoot(filePath);
                string remainingPath = filePath.Substring(root.Length);

                // Split into directory components and filename
                string[] parts = remainingPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string fileName = parts[parts.Length - 1];

                // Build the corrected path starting from root
                string correctedPath = root;

                // Process each directory level
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    string part = parts[i];
                    if (string.IsNullOrWhiteSpace(part))
                        continue;

                    bool found = false;
                    foreach (var dir in Directory.GetDirectories(correctedPath))
                    {
                        if (string.Equals(Path.GetFileName(dir), part, StringComparison.OrdinalIgnoreCase))
                        {
                            correctedPath = dir;
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        return filePath;
                    }
                }

                // Finally, handle the filename
                foreach (var f in Directory.GetFiles(correctedPath))
                {
                    if (string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        return f;
                    }
                }

                // Directory path was fixed up but file wasn't found — use corrected dir with original filename
                return Path.Combine(correctedPath, fileName);
            }
            catch (Exception)
            {
                return filePath;
            }
        }
    }
}
