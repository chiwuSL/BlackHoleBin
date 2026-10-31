using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BlackHoleBin
{
    internal static class SmokeTest
    {
        private static readonly Guid FileOperationClassGuid =
            new Guid("3AD05575-8857-4850-9277-11B85BDB8E09");
        private static readonly Guid ShellItemGuid =
            new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE");

        [STAThread]
        private static int Main()
        {
            Console.WriteLine("STEP 1: 路径验证");
            object operation = null;
            IShellItem shellItem = null;
            string probePath = Path.Combine(
                Path.GetTempPath(),
                "BlackHoleBin-interface-probe-" + Guid.NewGuid().ToString("N") + ".tmp");
            string ruleRoot = Path.Combine(
                Path.GetTempPath(),
                "BlackHoleBin-rules-" + Guid.NewGuid().ToString("N"));
            string ruleFile = Path.Combine(ruleRoot, "allowed.tmp");
            string oversizedFile = Path.Combine(ruleRoot, "oversized.tmp");
            try
            {
                File.WriteAllText(probePath, "This temporary file must not be deleted by the smoke test.");
                Directory.CreateDirectory(ruleRoot);
                File.WriteAllText(ruleFile, "rule test");
                string validationError;
                bool accepted = RecycleService.ValidatePaths(
                    new List<string> { probePath },
                    out validationError);

                if (!accepted)
                {
                    Console.Error.WriteLine("本机固定磁盘路径验证失败：" + validationError);
                    return 1;
                }

                SafetyReport criticalReport = RecycleService.InspectPaths(
                    new List<string> { Environment.GetFolderPath(Environment.SpecialFolder.Windows) });
                if (criticalReport.IsSafe)
                {
                    Console.Error.WriteLine("Windows 关键目录保护测试失败。");
                    return 4;
                }

                try
                {
                    RecycleService.MoveToRecycleBin(
                        new List<string> { Environment.GetFolderPath(Environment.SpecialFolder.Windows) },
                        IntPtr.Zero);
                    Console.Error.WriteLine("核心回收方法绕过了关键目录保护。");
                    return 5;
                }
                catch (InvalidOperationException)
                {
                    // Expected: the final execution method repeats the safety scan.
                }

                Console.WriteLine("STEP 2: 验证大小和目录规则");
                using (FileStream largeStream = File.Create(oversizedFile))
                {
                    largeStream.SetLength(11L * 1024L * 1024L);
                }

                AppSettings limited = AppSettings.CreateDefault();
                limited.MaximumFileSizeMb = 10;
                if (RecycleService.InspectPaths(new List<string> { oversizedFile }, limited).IsSafe)
                {
                    Console.Error.WriteLine("可配置文件大小上限测试失败。");
                    return 6;
                }

                AppSettings blacklist = AppSettings.CreateDefault();
                blacklist.ProtectionMode = ProtectionMode.Blacklist;
                blacklist.RulePaths.Add(ruleRoot);
                if (RecycleService.InspectPaths(new List<string> { ruleFile }, blacklist).IsSafe)
                {
                    Console.Error.WriteLine("黑名单测试失败。");
                    return 7;
                }

                AppSettings whitelist = AppSettings.CreateDefault();
                whitelist.ProtectionMode = ProtectionMode.Whitelist;
                whitelist.RulePaths.Add(ruleRoot);
                if (!RecycleService.InspectPaths(new List<string> { ruleFile }, whitelist).IsSafe
                    || RecycleService.InspectPaths(new List<string> { ruleRoot }, whitelist).IsSafe
                    || RecycleService.InspectPaths(new List<string> { probePath }, whitelist).IsSafe)
                {
                    Console.Error.WriteLine("白名单测试失败。");
                    return 8;
                }

                L.Language = AppLanguage.English;
                SafetyReport englishReport = RecycleService.InspectPaths(
                    new List<string> { Environment.GetFolderPath(Environment.SpecialFolder.Windows) });
                if (englishReport.IsSafe || !englishReport.ErrorMessage.Contains("protected"))
                {
                    Console.Error.WriteLine("English localization test failed.");
                    return 9;
                }
                L.Language = AppLanguage.Chinese;

                Console.WriteLine("STEP 3: 验证实时尺寸预览和保险库清理");
                int previewSize = 0;
                using (SettingsDialog dialog = new SettingsDialog(
                    AppSettings.CreateDefault(),
                    size => previewSize = size))
                {
                    TrackBar sizeTrack = FindFirst<TrackBar>(dialog);
                    sizeTrack.Value = 260;
                    if (previewSize != 260)
                    {
                        Console.Error.WriteLine("设置滑块实时预览测试失败。");
                        return 10;
                    }
                }

                string vaultRoot = Path.Combine(ruleRoot, "vault");
                RecoveryVault.RootOverrideForTests = vaultRoot;
                Directory.CreateDirectory(Path.Combine(vaultRoot, "old-operation"));
                File.WriteAllText(Path.Combine(vaultRoot, "old-operation", "backup.tmp"), "backup");
                RecoveryVault.ClearAll();
                if (!Directory.Exists(vaultRoot) || Directory.GetFileSystemEntries(vaultRoot).Length != 0)
                {
                    Console.Error.WriteLine("保险库启动清理测试失败。");
                    return 11;
                }
                RecoveryVault.RootOverrideForTests = null;

                Console.WriteLine("STEP 4: 创建 IFileOperation");
                Type type = Type.GetTypeFromCLSID(FileOperationClassGuid, true);
                operation = Activator.CreateInstance(type);
                IFileOperation fileOperation = (IFileOperation)operation;

                Console.WriteLine("STEP 5: 设置回收站标志");
                int setFlagsResult = fileOperation.SetOperationFlags(0x00080000);
                if (setFlagsResult < 0)
                {
                    Marshal.ThrowExceptionForHR(setFlagsResult);
                }

                Console.WriteLine("STEP 6: 创建 Shell 项目");
                Guid shellItemGuid = ShellItemGuid;
                int createResult = SHCreateItemFromParsingName(
                    probePath,
                    IntPtr.Zero,
                    ref shellItemGuid,
                    out shellItem);
                if (createResult < 0)
                {
                    Marshal.ThrowExceptionForHR(createResult);
                }

                Console.WriteLine("STEP 7: 排入回收队列但不执行");
                int deleteResult = fileOperation.DeleteItem(shellItem, IntPtr.Zero);
                if (deleteResult < 0)
                {
                    Marshal.ThrowExceptionForHR(deleteResult);
                }

                if (!File.Exists(probePath))
                {
                    Console.Error.WriteLine("探针文件在未执行操作时消失。");
                    return 3;
                }

                Console.WriteLine("PASS: 本地路径安全检查、Shell 项目和回收队列接口可用。");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FAIL TYPE: " + ex.GetType().FullName);
                Console.Error.WriteLine("FAIL HR: " + ex.HResult);
                Console.Error.WriteLine("FAIL MESSAGE: " + ex.Message);
                return 2;
            }
            finally
            {
                if (shellItem != null && Marshal.IsComObject(shellItem))
                {
                    Marshal.FinalReleaseComObject(shellItem);
                }
                if (operation != null && Marshal.IsComObject(operation))
                {
                    Marshal.FinalReleaseComObject(operation);
                }
                if (File.Exists(probePath))
                {
                    File.Delete(probePath);
                }
                if (Directory.Exists(ruleRoot))
                {
                    Directory.Delete(ruleRoot, true);
                }
            }
        }

        private static T FindFirst<T>(Control root) where T : Control
        {
            foreach (Control child in root.Controls)
            {
                T match = child as T;
                if (match != null)
                {
                    return match;
                }
                match = FindFirst<T>(child);
                if (match != null)
                {
                    return match;
                }
            }
            return null;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SHCreateItemFromParsingName(
            string path,
            IntPtr bindContext,
            ref Guid interfaceId,
            [MarshalAs(UnmanagedType.Interface)] out IShellItem shellItem);
    }
}
