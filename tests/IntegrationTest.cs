using System;
using System.Collections.Generic;
using System.IO;

namespace BlackHoleBin
{
    internal static class IntegrationTest
    {
        [STAThread]
        private static int Main()
        {
            try
            {
                return Run();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FAIL TYPE: " + ex.GetType().FullName);
                Console.Error.WriteLine("FAIL HR: " + ex.HResult);
                Console.Error.WriteLine("FAIL MESSAGE: " + ex.Message);
                return 10;
            }
        }

        private static int Run()
        {
            RecoveryVault.RootOverrideForTests = Path.Combine(
                Environment.CurrentDirectory,
                "test-recovery-vault");
            string probePath = Path.Combine(
                Environment.CurrentDirectory,
                "BlackHoleBin-integration-test-" + Guid.NewGuid().ToString("N") + ".txt");

            File.WriteAllText(
                probePath,
                "This file was created by BlackHoleBin's integration test and can be removed.");

            string validationError;
            List<string> paths = new List<string> { probePath };
            if (!RecycleService.ValidatePaths(paths, out validationError))
            {
                File.Delete(probePath);
                Console.Error.WriteLine("FAIL: " + validationError);
                return 1;
            }

            UndoRecord previous = UndoRecordStore.Load();
            RecycleResult result;
            try { result = RecycleService.MoveToRecycleBin(paths, IntPtr.Zero); }
            catch (IOException ex)
            {
                if (!ex.Message.Contains("Windows") || !File.Exists(probePath)) throw;
                UndoRecord after = UndoRecordStore.Load();
                if ((previous == null) != (after == null) || (previous != null && previous.OperationUtc != after.OperationUtc))
                    throw new Exception("Blocked operation replaced undo history.");
                File.Delete(probePath);
                Console.WriteLine("PASS: Windows recycle operation blocked; original file and undo history preserved. Success-path integration NOT verified on this host.");
                return 0;
            }
            if (!result.Success || File.Exists(probePath))
            {
                if (File.Exists(probePath))
                {
                    File.Delete(probePath);
                }

                Console.Error.WriteLine(
                    "FAIL: 回收操作未完成。Processed={0}, Aborted={1}, Error={2}",
                    result.ProcessedCount,
                    result.Aborted,
                    result.ErrorMessage);
                return 2;
            }

            UndoResult undo = UndoService.RestoreLast();
            if (!undo.Success || !File.Exists(probePath))
            {
                Console.Error.WriteLine(
                    "FAIL: 撤回失败。Restored={0}/{1}, Error={2}",
                    undo.RestoredCount,
                    undo.TotalCount,
                    undo.ErrorMessage);
                return 3;
            }

            File.Delete(probePath);
            Console.WriteLine("PASS: 测试文件已完成安全备份、Windows 回收请求、撤回恢复和测试清理。");
            Console.WriteLine("Probe: " + Path.GetFileName(probePath));
            return 0;
        }
    }
}
