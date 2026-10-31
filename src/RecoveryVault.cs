using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BlackHoleBin
{
    internal sealed class VaultRestoreResult
    {
        public int TotalCount { get; set; }
        public int RestoredCount { get; set; }
        public List<string> RemainingPaths { get; set; }
        public string ErrorMessage { get; set; }
    }

    internal static class RecoveryVault
    {
        private const string ManifestName = "paths.tsv";
        internal static string RootOverrideForTests { get; set; }

        public static string RootDirectory
        {
            get
            {
                if (!string.IsNullOrEmpty(RootOverrideForTests))
                {
                    return Path.GetFullPath(RootOverrideForTests);
                }
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "BlackHoleBin",
                    "Recovery");
            }
        }

        public static string CreateBackup(IList<string> paths)
        {
            PathSafety.RejectReparseChain(RootDirectory);
            string vaultDirectory = Path.Combine(
                RootDirectory,
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
            string itemDirectory = Path.Combine(vaultDirectory, "items");
            Directory.CreateDirectory(itemDirectory);

            List<string> manifestLines = new List<string>();
            manifestLines.Add("# BlackHoleBin recovery vault. Do not edit while the app is running.");

            for (int index = 0; index < paths.Count; index++)
            {
                string source = Path.GetFullPath(paths[index]);
                PathSafety.RejectReparseChain(source);
                string relativeName = index.ToString("D6");
                string destination = Path.Combine(itemDirectory, relativeName);
                bool isDirectory = Directory.Exists(source);

                if (isDirectory)
                {
                    CopyDirectory(source, destination);
                }
                else
                {
                    File.Copy(source, destination, false);
                    File.SetLastWriteTimeUtc(destination, File.GetLastWriteTimeUtc(source));
                }

                manifestLines.Add(
                    relativeName + "\t"
                    + (isDirectory ? "D" : "F") + "\t"
                    + Convert.ToBase64String(Encoding.UTF8.GetBytes(source)));
            }

            File.WriteAllLines(
                Path.Combine(vaultDirectory, ManifestName),
                manifestLines.ToArray(),
                new UTF8Encoding(false));
            File.WriteAllText(
                Path.Combine(vaultDirectory, "README.txt"),
                "This folder is a safety copy created before Windows recycle operations.\r\n"
                + "paths.tsv stores Base64-encoded original paths. BlackHoleBin can restore the latest operation.\r\n"
                + "此目录是在调用 Windows 回收站前创建的安全副本。paths.tsv 保存原路径，程序可撤回最近一次操作。\r\n",
                new UTF8Encoding(false));
            return vaultDirectory;
        }

        public static VaultRestoreResult Restore(string vaultDirectory, IList<string> requestedPaths)
        {
            VaultRestoreResult result = new VaultRestoreResult
            {
                TotalCount = requestedPaths.Count,
                RemainingPaths = requestedPaths.ToList()
            };

            try
            {
                vaultDirectory=PathSafety.ValidateVault(vaultDirectory);
                Dictionary<string, VaultEntry> entries = ReadManifest(vaultDirectory);
                foreach (string requestedPath in requestedPaths)
                {
                    string original = Path.GetFullPath(requestedPath);
                    PathSafety.RejectReparseChain(original);
                    if (File.Exists(original) || Directory.Exists(original))
                    {
                        continue;
                    }

                    VaultEntry entry;
                    if (!entries.TryGetValue(original, out entry))
                    {
                        continue;
                    }

                    string source = Path.Combine(vaultDirectory, "items", entry.RelativeName);
                    PathSafety.RejectReparseChain(source);
                    if (entry.IsDirectory && Directory.Exists(source))
                    {
                        string staging = original + ".blackhole-restore-" + Guid.NewGuid().ToString("N");
                        CopyDirectory(source, staging);
                        Directory.Move(staging, original);
                        result.RestoredCount++;
                        result.RemainingPaths.Remove(requestedPath);
                    }
                    else if (!entry.IsDirectory && File.Exists(source))
                    {
                        string parent = Path.GetDirectoryName(original);
                        if (!string.IsNullOrEmpty(parent))
                        {
                            Directory.CreateDirectory(parent);
                        }
                        string staging = original + ".blackhole-restore-" + Guid.NewGuid().ToString("N");
                        File.Copy(source, staging, false);
                        File.SetLastWriteTimeUtc(staging, File.GetLastWriteTimeUtc(source));
                        File.Move(staging, original);
                        result.RestoredCount++;
                        result.RemainingPaths.Remove(requestedPath);
                    }
                    else
                    {
                        // Keep unresolved entries in the operation record.
                    }
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                // Existence is not proof of a complete restore. Keep every uncommitted item.
            }

            return result;
        }

        public static void DeleteRedundantVault(string vaultDirectory)
        {
            if (string.IsNullOrEmpty(vaultDirectory) || !Directory.Exists(vaultDirectory))
            {
                return;
            }

            string root = Path.GetFullPath(RootDirectory)
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string target = Path.GetFullPath(vaultDirectory)
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Refusing to remove a directory outside the recovery vault.");
            }

            PathSafety.ValidateVault(vaultDirectory);
            RejectLinkedTree(vaultDirectory);
            Directory.Delete(vaultDirectory, true);
        }

        public static void ClearAll()
        {
            string root = Path.GetFullPath(RootDirectory);
            PathSafety.RejectReparseChain(root);
            if (Directory.Exists(root))
            {
                RejectLinkedTree(root);
                Directory.Delete(root, true);
            }
            Directory.CreateDirectory(root);
        }

        private static Dictionary<string, VaultEntry> ReadManifest(string vaultDirectory)
        {
            Dictionary<string, VaultEntry> result =
                new Dictionary<string, VaultEntry>(StringComparer.OrdinalIgnoreCase);
            string manifestPath = Path.Combine(vaultDirectory, ManifestName);
            PathSafety.RejectReparseChain(manifestPath);
            if(new FileInfo(manifestPath).Length>4*1024*1024)throw new IOException("Recovery manifest is too large.");
            foreach (string line in File.ReadAllLines(manifestPath, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                string[] parts = line.Split(new[] { '\t' }, 3);
                if (parts.Length != 3)
                {
                    throw new IOException("Invalid recovery manifest.");
                }
                if(parts[0].Length!=6 || parts[0].Any(c=>c<'0' || c>'9') || (parts[1]!="D" && parts[1]!="F"))
                    throw new IOException("Unsafe recovery item name.");

                string original = Encoding.UTF8.GetString(Convert.FromBase64String(parts[2]));
                result[Path.GetFullPath(original)] = new VaultEntry
                {
                    RelativeName = parts[0],
                    IsDirectory = string.Equals(parts[1], "D", StringComparison.Ordinal)
                };
            }
            return result;
        }

        private static void CopyDirectory(string source, string destination)
        {
            PathSafety.RejectReparseChain(source);
            PathSafety.RejectReparseChain(destination);
            if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Recovery does not follow reparse points.");
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source))
            {
                PathSafety.RejectReparseChain(file);
                string targetFile = Path.Combine(destination, Path.GetFileName(file));
                File.Copy(file, targetFile, false);
                File.SetLastWriteTimeUtc(targetFile, File.GetLastWriteTimeUtc(file));
            }

            foreach (string directory in Directory.GetDirectories(source))
            {
                CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
            }
            Directory.SetLastWriteTimeUtc(destination, Directory.GetLastWriteTimeUtc(source));
        }

        private static void RejectLinkedTree(string root)
        {
            var pending=new Stack<string>();pending.Push(root);
            while(pending.Count>0) {
                string current=pending.Pop();
                PathSafety.RejectReparseChain(current);
                if(Directory.Exists(current))foreach(string entry in Directory.GetFileSystemEntries(current))pending.Push(entry);
            }
        }

        private sealed class VaultEntry
        {
            public string RelativeName { get; set; }
            public bool IsDirectory { get; set; }
        }
    }
}
