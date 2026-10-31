using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
namespace BlackHoleBin {
internal static class SecurityRegressionTest {
static int Main() {
string root=Path.Combine(Path.GetTempPath(),"BlackHoleSecurity-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
RecoveryVault.RootOverrideForTests=Path.Combine(root,"vault");
string junction=Path.Combine(root,"junction");
try {
string original=Path.Combine(root,"original.txt");File.WriteAllText(original,"confidential-test");
var paths=new List<string>{original};string vault=RecoveryVault.CreateBackup(paths);File.Delete(original);
string manifest=Path.Combine(vault,"paths.tsv"),saved=File.ReadAllText(manifest);
File.WriteAllText(manifest,"..\\..\\external\tF\t"+Convert.ToBase64String(Encoding.UTF8.GetBytes(original)));
var restored=RecoveryVault.Restore(vault,paths);
if(restored.RestoredCount!=0 || string.IsNullOrEmpty(restored.ErrorMessage) || File.Exists(original))throw new Exception("Manifest traversal was accepted");
File.WriteAllText(manifest,saved);
restored=RecoveryVault.Restore(root,paths);
if(string.IsNullOrEmpty(restored.ErrorMessage))throw new Exception("Outside vault accepted");
try{RecoveryVault.DeleteRedundantVault(RecoveryVault.RootDirectory);throw new Exception("Vault root deletion accepted");}catch(IOException){}
var start=new ProcessStartInfo("cmd.exe","/d /c mklink /J \""+junction+"\" \""+vault+"\"");
start.UseShellExecute=false;start.CreateNoWindow=true;start.RedirectStandardOutput=true;start.RedirectStandardError=true;
using(var process=Process.Start(start)){process.StandardOutput.ReadToEnd();process.StandardError.ReadToEnd();process.WaitForExit();if(process.ExitCode!=0)throw new Exception("Cannot create test junction");}
try{PathSafety.RejectReparseChain(Path.Combine(junction,"items","000000"));throw new Exception("Junction parent accepted");}catch(IOException){}
Directory.Delete(junction);
string unsafeLink=Path.Combine(RecoveryVault.RootDirectory,"link");
start.Arguments="/d /c mklink /J \""+unsafeLink+"\" \""+Path.Combine(vault,"items")+"\"";
using(var process=Process.Start(start)){process.StandardOutput.ReadToEnd();process.StandardError.ReadToEnd();process.WaitForExit();if(process.ExitCode!=0)throw new Exception("Cannot create cleanup junction");}
try{RecoveryVault.ClearAll();throw new Exception("Linked cleanup accepted");}catch(IOException){}
Directory.Delete(unsafeLink);
if(!File.Exists(Path.Combine(vault,"items","000000")))throw new Exception("Cleanup damaged protected copy");
restored=RecoveryVault.Restore(vault,paths);
if(restored.RestoredCount!=1 || File.ReadAllText(original)!="confidential-test")throw new Exception("Valid recovery regressed");
Console.WriteLine("PASS: manifest traversal, outside vault, root delete, parent junction and linked cleanup blocked; valid restore retained.");return 0;
}catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}
finally {if(Directory.Exists(junction))Directory.Delete(junction);RecoveryVault.RootOverrideForTests=null;Directory.Delete(root,true);}
}
}
}
