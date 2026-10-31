// Controlled marketing scene using the shipped GPU renderer. No desktop capture.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
namespace BlackHoleBin {
internal static class DemoStudio {
static readonly Color Ink=Color.FromArgb(231,229,219), Muted=Color.FromArgb(150,158,159), Gold=Color.FromArgb(223,191,126);
static void Label(Graphics g,string text,float size,Color color,float x,float y,bool bold=false) {
using(var f=new Font("Microsoft YaHei UI",size,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel))
using(var b=new SolidBrush(color))g.DrawString(text,f,b,x,y);
}
static GraphicsPath Round(RectangleF r,float radius) {
var p=new GraphicsPath();float d=radius*2;
p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;
}
static Bitmap Scene(float t) {
var b=new Bitmap(1440,900,PixelFormat.Format32bppArgb);
using(var g=Graphics.FromImage(b)) {
g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
using(var grad=new LinearGradientBrush(new Rectangle(0,0,1440,900),Color.FromArgb(12,20,26),Color.FromArgb(25,30,34),25f))g.FillRectangle(grad,0,0,1440,900);
Label(g,"B H  /  BLACK HOLE BIN",16,Gold,76,68,true);
Label(g,"黑洞回收站",52,Ink,70,265,true);
Label(g,"A small black hole.",29,Ink,76,350);
Label(g,"A familiar Recycle Bin.",29,Ink,76,394);
Label(g,"拖入 · 确认 · 回收",20,Gold,76,492);
Label(g,"DRAG. CONFIRM. RECYCLE.",12,Muted,78,537);
using(var pen=new Pen(Color.FromArgb(58,Gold)))g.DrawLine(pen,78,600,470,600);
Label(g,"离线运行  /  动态折射  /  撤回恢复",15,Muted,76,628);
Label(g,"OFFLINE    ·    LIVE LENSING    ·    UNDO",11,Muted,78,665);
// Keep all type outside the sampled region. High-contrast card edges and file
// labels were being mirrored into the photon ring in the old composition.
Label(g,"1.0.0  /  WINDOWS x64",12,Muted,78,716);
Label(g,"CONTROLLED DEMO SCENE  /  ACTUAL GPU SHADER",11,Muted,77,818);
Label(g,"No real files or desktop content were captured.",11,Muted,77,842);
}
return b;
}
static int Main(string[] args) {
try {
string output=args[0];Directory.CreateDirectory(output);
using(var gpu=new GpuRenderer()) {
for(int i=0;i<180;i++) {
float t=i/30f;
using(var scene=Scene(t)) {
// Slow translation demonstrates lensing while preserving the approved shader.
int size=800,x=600+(int)(12*Math.Sin(t*.85)),y=50+(int)(8*Math.Sin(t*.6));
using(var crop=scene.Clone(new Rectangle(x,y,size,size),PixelFormat.Format32bppArgb)) {
using(var hi=new Bitmap(1024,1024,PixelFormat.Format32bppArgb)) {
using(var g=Graphics.FromImage(hi)) {g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(crop,0,0,1024,1024);}
Bitmap hole=gpu.Render(hi,t,.52f,0,2);
using(var g=Graphics.FromImage(scene)) {g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(hole,new Rectangle(x,y,size,size));}
}
}
scene.Save(Path.Combine(output,"frame-"+i.ToString("D3")+".png"),ImageFormat.Png);
if(i==60)scene.Save(args[1],ImageFormat.Png);
}
}
}
Console.WriteLine("Rendered 180 staged GPU frames, 1440x900.");return 0;
}catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}
}
}
}
