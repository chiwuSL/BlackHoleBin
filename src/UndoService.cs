using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;

namespace BlackHoleBin
{
    internal sealed class UndoRecord
    {
        public DateTime OperationUtc { get; set; }
        public List<string> Paths { get; set; }
        public string VaultDirectory { get; set; }
    }

    internal sealed class UndoResult
    {
        public int TotalCount { get; set; }
        public int RestoredCount { get; set; }
        public string ErrorMessage { get; set; }

        public bool Success
        {
            get { return TotalCount > 0 && RestoredCount == TotalCount && string.IsNullOrEmpty(ErrorMessage); }
        }
    }

    internal static class UndoRecordStore
    {
        internal static string RegistryPath = AppSettingsStore.SettingsKey;
        public static void Save(IList<string> paths, DateTime operationUtc, string vaultDirectory)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
            {
                if (key == null)
                {
                    return;
                }

                key.SetValue("LastRecyclePaths", paths.ToArray(), RegistryValueKind.MultiString);
                key.SetValue("LastRecycleUtcTicks", operationUtc.Ticks, RegistryValueKind.QWord);
                key.SetValue("LastRecoveryVault", vaultDirectory ?? string.Empty, RegistryValueKind.String);
            }
        }

        public static UndoRecord Load()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, false))
                {
                    if (key == null)
                    {
                        return null;
                    }

                    string[] paths = key.GetValue("LastRecyclePaths") as string[];
                    object ticksValue = key.GetValue("LastRecycleUtcTicks");
                    if (paths == null || paths.Length == 0 || !(ticksValue is long))
                    {
                        return null;
                    }

                    return new UndoRecord
                    {
                        Paths = paths.Where(path => !string.IsNullOrWhiteSpace(path)).ToList(),
                        OperationUtc = new DateTime((long)ticksValue, DateTimeKind.Utc),
                        VaultDirectory = Convert.ToString(key.GetValue("LastRecoveryVault"))
                    };
                }
            }
            catch
            {
                return null;
            }
        }

        public static bool HasRecord()
        {
            UndoRecord record = Load();
            return record != null && record.Paths.Count > 0;
        }

        public static void Clear()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, true))
                {
                    if (key != null)
                    {
                        key.DeleteValue("LastRecyclePaths", false);
                        key.DeleteValue("LastRecycleUtcTicks", false);
                        key.DeleteValue("LastRecoveryVault", false);
                    }
                }
            }
            catch
            {
            }
        }
    }

    internal static class UndoService
    {
        private sealed class RecycleCandidate
        {
            public object Item { get; set; }
            public string OriginalPath { get; set; }
            public DateTime DeletedUtc { get; set; }
        }

        public static UndoResult RestoreLast()
        {
            UndoRecord record = UndoRecordStore.Load();
            if (record == null || record.Paths.Count == 0)
            {
                return new UndoResult { ErrorMessage = L.Text("UndoNone") };
            }

            UndoResult result = new UndoResult { TotalCount = record.Paths.Count };

            if (!string.IsNullOrEmpty(record.VaultDirectory)
                && Directory.Exists(record.VaultDirectory))
            {
                VaultRestoreResult vaultResult = RecoveryVault.Restore(record.VaultDirectory, record.Paths);
                result.RestoredCount = vaultResult.RestoredCount;
                result.ErrorMessage = vaultResult.ErrorMessage;
                if (vaultResult.RemainingPaths.Count == 0 && string.IsNullOrEmpty(vaultResult.ErrorMessage))
                {
                    UndoRecordStore.Clear();
                    try
                    {
                        RecoveryVault.DeleteRedundantVault(record.VaultDirectory);
                    }
                    catch
                    {
                    }
                }
                else
                {
                    UndoRecordStore.Save(
                        vaultResult.RemainingPaths,
                        record.OperationUtc,
                        record.VaultDirectory);
                }
                return result;
            }

            List<string> remaining = new List<string>();
            HashSet<string> restored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<RecycleCandidate> candidates = new List<RecycleCandidate>();
            object shell = null;
            object recycleBin = null;
            object items = null;

            try
            {
                Type shellType = Type.GetTypeFromProgID("Shell.Application", true);
                shell = Activator.CreateInstance(shellType);
                recycleBin = Invoke(shell, "NameSpace", new object[] { 10 });
                if (recycleBin == null)
                {
                    throw new InvalidOperationException("Recycle Bin shell folder is unavailable.");
                }

                items = Invoke(recycleBin, "Items", null);
                int count = Convert.ToInt32(Get(items, "Count"));
                for (int index = 0; index < count; index++)
                {
                    object item = null;
                    try
                    {
                        item = Invoke(items, "Item", new object[] { index });
                        string name = Convert.ToString(Get(item, "Name"));
                        string deletedFrom = Convert.ToString(
                            Invoke(item, "ExtendedProperty", new object[] { "System.Recycle.DeletedFrom" }));
                        object deletedValue = Invoke(
                            item,
                            "ExtendedProperty",
                            new object[] { "System.Recycle.DateDeleted" });

                        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(deletedFrom))
                        {
                            Release(item);
                            continue;
                        }

                        DateTime deletedUtc = DateTime.MinValue;
                        if (deletedValue != null && deletedValue != DBNull.Value)
                        {
                            deletedUtc = Convert.ToDateTime(deletedValue).ToUniversalTime();
                        }

                        candidates.Add(new RecycleCandidate
                        {
                            Item = item,
                            OriginalPath = Path.GetFullPath(Path.Combine(deletedFrom, name)),
                            DeletedUtc = deletedUtc
                        });
                    }
                    catch
                    {
                        Release(item);
                    }
                }

                foreach (string originalPath in record.Paths)
                {
                    if (File.Exists(originalPath) || Directory.Exists(originalPath))
                    {
                        remaining.Add(originalPath);
                        continue;
                    }

                    RecycleCandidate candidate = candidates
                        .Where(item => string.Equals(
                            item.OriginalPath,
                            Path.GetFullPath(originalPath),
                            StringComparison.OrdinalIgnoreCase))
                        .Where(item => item.DeletedUtc == DateTime.MinValue
                            || item.DeletedUtc >= record.OperationUtc.AddMinutes(-2))
                        .OrderByDescending(item => item.DeletedUtc)
                        .FirstOrDefault();

                    if (candidate == null)
                    {
                        remaining.Add(originalPath);
                        continue;
                    }

                    Invoke(candidate.Item, "InvokeVerb", new object[] { "undelete" });
                    if (WaitForPath(originalPath, 2500))
                    {
                        restored.Add(originalPath);
                        result.RestoredCount++;
                    }
                    else
                    {
                        remaining.Add(originalPath);
                    }
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                remaining = record.Paths
                    .Where(path => !restored.Contains(path))
                    .ToList();
                result.RestoredCount = result.TotalCount - remaining.Count;
            }
            finally
            {
                foreach (RecycleCandidate candidate in candidates)
                {
                    Release(candidate.Item);
                }
                Release(items);
                Release(recycleBin);
                Release(shell);
            }

            if (remaining.Count == 0)
            {
                UndoRecordStore.Clear();
            }
            else
            {
                UndoRecordStore.Save(remaining, record.OperationUtc, record.VaultDirectory);
            }
            return result;
        }

        private static bool WaitForPath(string path, int timeoutMilliseconds)
        {
            int waited = 0;
            while (waited < timeoutMilliseconds)
            {
                if (File.Exists(path) || Directory.Exists(path))
                {
                    return true;
                }
                Thread.Sleep(100);
                waited += 100;
            }
            return File.Exists(path) || Directory.Exists(path);
        }

        private static object Invoke(object target, string member, object[] arguments)
        {
            return target.GetType().InvokeMember(
                member,
                BindingFlags.InvokeMethod,
                null,
                target,
                arguments);
        }

        private static object Get(object target, string member)
        {
            return target.GetType().InvokeMember(
                member,
                BindingFlags.GetProperty,
                null,
                target,
                null);
        }

        private static void Release(object value)
        {
            if (value != null && Marshal.IsComObject(value))
            {
                try
                {
                    Marshal.FinalReleaseComObject(value);
                }
                catch
                {
                }
            }
        }
    }
}
