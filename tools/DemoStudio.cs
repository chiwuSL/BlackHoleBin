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
static void FileIcon(Graphics g,int x,int y,Color color) {
using(var fill=new SolidBrush(Color.FromArgb(26,color)))g.FillRectangle(fill,x,y,30,38);
using(var pen=new Pen(Color.FromArgb(135,color),1.5f)) {
g.DrawLines(pen,new Point[]{new Point(x,y+38),new Point(x,y),new Point(x+20,y),new Point(x+30,y+10),new Point(x+30,y+38),new Point(x,y+38)});
g.DrawLines(pen,new Point[]{new Point(x+20,y),new Point(x+20,y+10),new Point(x+30,y+10)});
g.DrawLine(pen,x+7,y+20,x+23,y+20);g.DrawLine(pen,x+7,y+27,x+20,y+27);
}
}
static Bitmap Scene(float t) {
var b=new Bitmap(1440,900,PixelFormat.Format32bppArgb);
using(var g=Graphics.FromImage(b)) {
g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
using(var grad=new LinearGradientBrush(new Rectangle(0,0,1440,900),Color.FromArgb(12,20,26),Color.FromArgb(25,30,34),25f))g.FillRectangle(grad,0,0,1440,900);
// A continuous, borderless mock desktop makes background lensing visible.
// Moderate contrast avoids the distracting dark echoes of the old white card.
using(var grid=new Pen(Color.FromArgb(28,123,161,169),1)) {
for(int x=540;x<1440;x+=72)g.DrawLine(grid,x,0,x,900);
for(int y=18;y<900;y+=72)g.DrawLine(grid,520,y,1440,y);
}
string[] names={"notes.md","ideas.txt","photos","drafts","reference.png","archive","weekend-plan.md","projects","readme.txt","inspiration","sketches","assets"};
for(int row=0;row<4;row++)for(int col=0;col<3;col++) {
int x=625+col*254,y=150+row*164;
FileIcon(g,x,y,Color.FromArgb(148,185,189));
Label(g,names[row*3+col],20,Color.FromArgb(128,155,160),x+44,y+6);
}
Label(g,"B H  /  BLACK HOLE BIN",16,Gold,76,68,true);
Label(g,"黑洞回收站",52,Ink,70,265,true);
Label(g,"A small black hole.",29,Ink,76,350);
Label(g,"A familiar Recycle Bin.",29,Ink,76,394);
Label(g,"拖入 · 确认 · 回收",20,Gold,76,492);
Label(g,"DRAG. CONFIRM. RECYCLE.",12,Muted,78,537);
using(var pen=new Pen(Color.FromArgb(58,Gold)))g.DrawLine(pen,78,600,470,600);
Label(g,"离线运行  /  动态折射  /  撤回恢复",15,Muted,76,628);
Label(g,"OFFLINE    ·    LIVE LENSING    ·    UNDO",11,Muted,78,665);
Label(g,"1.0.0  /  WINDOWS x64",12,Muted,78,716);
Label(g,"CONTROLLED DEMO SCENE  /  ACTUAL GPU SHADER",11,Muted,77,818);
Label(g,"No real files or desktop content were captured.",11,Muted,77,842);
}
return b;
}
static int Main(string[] args) {
try {
string output=args[0];Directory.CreateDirectory(output);
int frameCount=args.Length>2?int.Parse(args[2]):240;
using(var gpu=new GpuRenderer()) {
for(int i=0;i<frameCount;i++) {
float t=i/30f;
using(var scene=Scene(t)) {
// A closed eight-second path shows the stationary desktop bending in motion.
double phase=t*Math.PI/4;
int size=800,x=580+(int)(45*Math.Cos(phase)),y=50+(int)(40*Math.Sin(phase));
using(var crop=scene.Clone(new Rectangle(x,y,size,size),PixelFormat.Format32bppArgb)) {
using(var hi=new Bitmap(1024,1024,PixelFormat.Format32bppArgb)) {
using(var g=Graphics.FromImage(hi)) {g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(crop,0,0,1024,1024);}
Bitmap hole=gpu.Render(hi,t,.52f,0,2);
using(var g=Graphics.FromImage(scene)) {g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(hole,new Rectangle(x,y,size,size));}
}
}
scene.Save(Path.Combine(output,"frame-"+i.ToString("D3")+".png"),ImageFormat.Png);
if(i==0)scene.Save(args[1],ImageFormat.Png);
}
}
}
Console.WriteLine("Rendered "+frameCount+" staged GPU frames, 1440x900.");return 0;
}catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}
}
}
}
