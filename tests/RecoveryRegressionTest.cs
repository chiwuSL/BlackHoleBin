using System;
using System.IO;
using System.Collections.Generic;
namespace BlackHoleBin {
internal static class RecoveryRegressionTest {
static int Main() {
string root = Path.Combine(Path.GetTempPath(), "BlackHoleRegression-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try {
RecoveryVault.RootOverrideForTests = Path.Combine(root, "vault");
string first = Path.Combine(root, "first.txt"), second = Path.Combine(root, "second.txt");
File.WriteAllText(first, "one"); File.WriteAllText(second, "two");
var paths = new List<string>{ first, second };
string vault = RecoveryVault.CreateBackup(paths);
File.Delete(first); File.Delete(second);
using (var locked = new FileStream(Path.Combine(vault,"items","000001"), FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
var result = RecoveryVault.Restore(vault, paths);
if (result.RestoredCount != 1 || result.RemainingPaths.Count != 1 || result.RemainingPaths[0] != second || string.IsNullOrEmpty(result.ErrorMessage)) throw new Exception("Partial restore incorrectly committed.");
if (File.Exists(second) || !Directory.Exists(vault)) throw new Exception("Failed restore damaged recovery state.");
}
var retry = RecoveryVault.Restore(vault, new List<string>{second});
if (retry.RestoredCount != 1 || File.ReadAllText(second) != "two") throw new Exception("Retry failed.");
var occupied = RecoveryVault.Restore(vault, paths);
if (occupied.RestoredCount != 0 || occupied.RemainingPaths.Count != 2) throw new Exception("Occupied paths counted as restored.");
var sink = new RecycleProgress();
if (sink.PreDeleteItem(0, IntPtr.Zero) >= 0 || !sink.Blocked) throw new Exception("Permanent-delete guard failed.");
Console.WriteLine("PASS: partial recovery, retry, occupied paths and delete guard."); return 0;
} finally { RecoveryVault.RootOverrideForTests = null; Directory.Delete(root, true); }
}
}
}
