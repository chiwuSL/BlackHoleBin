using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace BlackHoleBin {
internal static class LiveGpuTest {
[DllImport("user32.dll")] static extern bool GetWindowDisplayAffinity(IntPtr w,out uint affinity);
[DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr w);
[DllImport("user32.dll")] static extern int ReleaseDC(IntPtr w,IntPtr dc);
[DllImport("dwmapi.dll")] static extern int DwmFlush();
[DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr dest,int x,int y,int width,int height,IntPtr src,int sx,int sy,uint flags);
[STAThread] static int Main(string[] args) {
try { return Run(args); } catch(Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
}
static int Run(string[] args) {
Application.EnableVisualStyles();
using(var backdrop=new Form()) using(var hole=new BlackHoleForm(AppSettings.CreateDefault())) {
backdrop.StartPosition=FormStartPosition.Manual;backdrop.Location=new Point(100,100);backdrop.Size=new Size(700,700);backdrop.BackColor=Color.Coral;backdrop.Show();
hole.Show();Pump();hole.Location=new Point(250,250); Pump();
Console.WriteLine("Hole="+hole.Bounds+" Backdrop="+backdrop.Bounds);
var field=typeof(BlackHoleForm).GetField("desktopSnapshot",BindingFlags.Instance|BindingFlags.NonPublic);
var bitmap=(Bitmap)field.GetValue(hole);
if(bitmap==null)throw new Exception("No background captured");
Color sample=bitmap.GetPixel(100,100);
bitmap.Save(args[0]+".background.png",ImageFormat.Png);
Console.WriteLine("Captured underlying desktop: "+sample);
if(Math.Abs(sample.R-Color.Coral.R)>8 || Math.Abs(sample.G-Color.Coral.G)>8)throw new Exception("Self-exclusion failed");
backdrop.BackColor=Color.CornflowerBlue;Pump();
bitmap=(Bitmap)field.GetValue(hole);sample=bitmap.GetPixel(100,100);
if(Math.Abs(sample.B-Color.CornflowerBlue.B)>8 || Math.Abs(sample.R-Color.CornflowerBlue.R)>8)throw new Exception("Live background did not update");
for(int y=5;y<195;y+=15)for(int x=5;x<195;x+=15) {
Color c=bitmap.GetPixel(x,y);
if(Math.Abs(c.B-Color.CornflowerBlue.B)>8 || Math.Abs(c.R-Color.CornflowerBlue.R)>8)throw new Exception("Background crop/feedback error at "+x+","+y);
}
using(var frame=new Bitmap(hole.Width,hole.Height)){hole.DrawToBitmap(frame,new Rectangle(Point.Empty,hole.Size));frame.Save(args[0],ImageFormat.Png);}
uint affinity;GetWindowDisplayAffinity(hole.Handle,out affinity);
if(affinity!=0)throw new Exception("Overlay is hidden from screen consumers");
using(var screen=new Bitmap(hole.Width,hole.Height)) {
hole.Refresh();DwmFlush();
using(var g=Graphics.FromImage(screen)) {
IntPtr src=GetDC(IntPtr.Zero),dest=g.GetHdc();
try {if(!BitBlt(dest,0,0,hole.Width,hole.Height,src,hole.Left,hole.Top,0x40CC0020))throw new Exception("Screen read failed");}
finally {g.ReleaseHdc(dest);ReleaseDC(IntPtr.Zero,src);}
}
Color core=screen.GetPixel(hole.Width/2,hole.Height/2-12);
if(core.R>65 || core.G>65 || core.B>65)throw new Exception("Screen capture cannot see the black-hole core: "+core);
screen.Save(args[0]+".screen.png",ImageFormat.Png);
}
Console.WriteLine("PASS: live background refresh, no feedback, capture-visible overlay (affinity=0).");
}return 0;
}
static void Pump(){var until=DateTime.UtcNow.AddSeconds(1);while(DateTime.UtcNow<until){Application.DoEvents();System.Threading.Thread.Sleep(10);}}
}
}
