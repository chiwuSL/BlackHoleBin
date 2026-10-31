using System;
using System.IO;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;
namespace BlackHoleBin {
internal static class CombinedFlowTest {
[STAThread] static int Main() {
string root=Path.Combine(Path.GetTempPath(),"BlackHoleCombined-"+Guid.NewGuid().ToString("N"));
string key="Software\\BlackHoleBin-Test-"+Guid.NewGuid().ToString("N");
UndoRecordStore.RegistryPath=key;
Directory.CreateDirectory(root);RecoveryVault.RootOverrideForTests=Path.Combine(root,"vault");
try {
Application.EnableVisualStyles();
var settings=AppSettings.CreateDefault();settings.ConfirmBeforeRecycle=false;
string file=Path.Combine(root,"drop.txt");File.WriteAllText(file,"combined animation + recycle + restore");
using(var form=new BlackHoleForm(settings)) {
form.Show();Pump(1000);
var type=typeof(BlackHoleForm);
if(type.GetField("gpu",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(form)==null)throw new Exception("GPU not active");
var data=new DataObject();data.SetData(DataFormats.FileDrop,new string[]{file});
var ev=new DragEventArgs(data,0,0,0,DragDropEffects.Move,DragDropEffects.None);
type.GetMethod("HandleDragEnter",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,new object[]{form,ev});
if(ev.Effect!=DragDropEffects.Move)throw new Exception("Drop rejected");
var busy=type.GetField("operationBusy",BindingFlags.NonPublic|BindingFlags.Instance);
type.GetMethod("HandleDragDrop",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,new object[]{form,ev});
DateTime deadline=DateTime.UtcNow.AddSeconds(30);int ticks=0;
using(var timer=new Timer()) {timer.Interval=16;timer.Tick+=delegate{ticks++;};timer.Start();
while((bool)busy.GetValue(form) && DateTime.UtcNow<deadline)Pump(20);timer.Stop();}
if((bool)busy.GetValue(form) || File.Exists(file) || !UndoRecordStore.HasRecord())throw new Exception("Combined recycle failed");
var undo=OperationWorker.Run(()=>UndoService.RestoreLast());
while(!undo.IsCompleted && DateTime.UtcNow<deadline)Pump(20);
if(!undo.IsCompleted || !undo.Result.Success || !File.Exists(file))throw new Exception("STA undo failed");
Console.WriteLine("PASS: GPU active, file-drop event, real recycle, STA undo, UI timer ticks="+ticks);
form.Hide();
}
return 0;
}catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}
finally {RecoveryVault.RootOverrideForTests=null;UndoRecordStore.RegistryPath=AppSettingsStore.SettingsKey;Registry.CurrentUser.DeleteSubKeyTree(key,false);Directory.Delete(root,true);}
}
static void Pump(int ms){var until=DateTime.UtcNow.AddMilliseconds(ms);while(DateTime.UtcNow<until){Application.DoEvents();System.Threading.Thread.Sleep(2);}}
}
}
