using System;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
namespace BlackHoleBin {
internal static class VisibilityRegressionTest {
[STAThread] static int Main() {
try {
Application.EnableVisualStyles();
var settings=AppSettings.CreateDefault();settings.HideWhenMaximized=true;
using(var request=new EventWaitHandle(false,EventResetMode.AutoReset))
using(var form=new BlackHoleForm(settings)) {
Program.ShowRequest=request;
if(form.Opacity<.9)throw new Exception("Startup remains transparent");
form.Show();Pump();
form.Hide();form.Location=new Point(-30000,-30000);form.Opacity=0;
request.Set();Pump();
if(!form.Visible || form.Opacity<.9 || form.WindowState!=FormWindowState.Normal)throw new Exception("Reactivation did not restore visible window");
if(!Screen.FromPoint(Cursor.Position).WorkingArea.Contains(form.Bounds))throw new Exception("Reactivation left window off screen");
var flag=typeof(BlackHoleForm).GetField("manualShowOverride",BindingFlags.NonPublic|BindingFlags.Instance);
if(!(bool)flag.GetValue(form))throw new Exception("Manual show is not protected from auto-hide");
typeof(BlackHoleForm).GetMethod("ApplyDisplayPolicy",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,null);
if(!form.Visible)throw new Exception("Policy hid manually restored window");
typeof(BlackHoleForm).GetField("allowExit",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(form,true);
form.Close();
Console.WriteLine("PASS: opaque startup, IPC wake, off-screen recovery, manual-show override.");
}Program.ShowRequest=null;return 0;
}catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}
}
static void Pump(){var until=DateTime.UtcNow.AddSeconds(1);while(DateTime.UtcNow<until){Application.DoEvents();Thread.Sleep(10);}}
}
}
