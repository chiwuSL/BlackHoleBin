using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
namespace BlackHoleBin {
internal static class GpuRenderTest {
static int Main(string[] args) {
string output=args[0];Directory.CreateDirectory(output);
using(var gpu=new GpuRenderer()) using(var bg=new Bitmap(420,420,PixelFormat.Format32bppArgb)) {
var watch=Stopwatch.StartNew();
for(int i=0;i<90;i++) {
using(var g=Graphics.FromImage(bg)) {
g.Clear(Color.FromArgb(224,228,234));
for(int x=0;x<420;x+=30)g.DrawLine(Pens.SlateGray,x,0,x,420);
for(int y=-30;y<450;y+=30)g.DrawLine(Pens.SlateGray,0,y+i%30,420,y+i%30);
g.DrawString("LIVE DESKTOP / GPU",SystemFonts.DefaultFont,Brushes.Black,30,50);
}
gpu.Render(bg,i/30f,.95f,0,2).Save(Path.Combine(output,string.Format("frame-{0:D3}.png",i)),ImageFormat.Png);
}
Console.WriteLine("PASS: 90 GPU frames rendered; render + PNG export ms="+watch.ElapsedMilliseconds);
}
return 0;
}
}
}
