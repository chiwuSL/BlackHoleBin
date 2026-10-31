using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;
namespace BlackHoleBin {
internal static class MovementIntegrationTest {
[STAThread] static int Main() {
try {
Application.EnableVisualStyles();
Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
using(var backdrop=new Form()) using(var hole=new BlackHoleForm(AppSettings.CreateDefault())) {
backdrop.FormBorderStyle=FormBorderStyle.None;backdrop.Bounds=new Rectangle(50,50,1100,700);
backdrop.Paint+=delegate(object s,PaintEventArgs e) {
using(var brush=new LinearGradientBrush(backdrop.ClientRectangle,Color.Red,Color.Blue,0f))e.Graphics.FillRectangle(brush,backdrop.ClientRectangle);
};
backdrop.Show();hole.Show();Console.WriteLine("Window shown");Pump(500);hole.Location=new Point(180,220);Pump(500);Console.WriteLine("Initial sampling done");
var type=typeof(BlackHoleForm);var flags=BindingFlags.NonPublic|BindingFlags.Instance;
var down=type.GetMethod("HandleMouseDown",flags);var move=type.GetMethod("HandleMouseMove",flags);var up=type.GetMethod("HandleMouseUp",flags);
var snapshot=type.GetField("desktopSnapshot",flags);var moving=type.GetField("moveDragging",flags);var captured=type.GetField("lastCapture",flags);
if(type.GetField("gpu",flags).GetValue(hole)==null)throw new Exception("GPU inactive");
Bitmap before=(Bitmap)snapshot.GetValue(hole);if(before==null)throw new Exception("No initial background");
int initialBlue=before.GetPixel(100,100).B;
long initialCapture=(long)captured.GetValue(hole);
int paints=0,changes=0,lastBlue=initialBlue;
hole.Paint+=delegate {paints++;};
Point initialPosition=hole.Location;
down.Invoke(hole,new object[]{hole,new MouseEventArgs(MouseButtons.Left,1,100,100,0)});
for(int i=0;i<24;i++) {
move.Invoke(hole,new object[]{hole,new MouseEventArgs(MouseButtons.Left,0,110,100,0)});
Pump(35);
if(!(bool)moving.GetValue(hole))throw new Exception("Drag unexpectedly ended");
Bitmap frame=(Bitmap)snapshot.GetValue(hole);int blue=frame.GetPixel(100,100).B;
if(blue>lastBlue){changes++;lastBlue=blue;}
}
if(hole.Left-initialPosition.X!=240)throw new Exception("Mouse grab offset was lost");
if(lastBlue-initialBlue<35 || changes<8 || paints<8 || (long)captured.GetValue(hole)<=initialCapture)
throw new Exception("Background/animation did not update during held drag: changes="+changes+" paints="+paints);
up.Invoke(hole,new object[]{hole,new MouseEventArgs(MouseButtons.Left,1,100,100,0)});
if(hole.Capture || (bool)moving.GetValue(hole))throw new Exception("Mouse capture not released");
Console.WriteLine("PASS: held drag moved 240px; background changes="+changes+", paints="+paints+", blue delta="+(lastBlue-initialBlue));
type.GetField("allowExit",flags).SetValue(hole,true);hole.Close();
}return 0;
}catch(Exception ex){Console.Error.WriteLine(ex.Message);Console.Error.WriteLine(ex.StackTrace);if(ex.InnerException!=null){Console.Error.WriteLine(ex.InnerException.Message);Console.Error.WriteLine(ex.InnerException.StackTrace);}return 1;}
}
static void Pump(int ms){var until=DateTime.UtcNow.AddMilliseconds(ms);while(DateTime.UtcNow<until){Application.DoEvents();System.Threading.Thread.Sleep(2);}}
}
}
