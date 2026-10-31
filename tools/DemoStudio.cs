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
using(var grad=new LinearGradientBrush(new Rectangle(0,0,1440,900),Color.FromArgb(15,23,29),Color.FromArgb(31,39,42),32f))g.FillRectangle(grad,0,0,1440,900);
using(var pen=new Pen(Color.FromArgb(14,255,255,255)))for(int x=-900;x<1600;x+=90)g.DrawLine(pen,x,0,x+900,900);
Label(g,"B H  /  BLACK HOLE BIN",16,Gold,76,68,true);
Label(g,"1.0.0  /  WINDOWS x64",13,Muted,1130,70);
Label(g,"黑洞回收站",52,Ink,70,265,true);
Label(g,"A small black hole.",29,Ink,76,350);
Label(g,"A familiar Recycle Bin.",29,Ink,76,394);
Label(g,"拖入 · 确认 · 回收",20,Gold,76,492);
Label(g,"DRAG. CONFIRM. RECYCLE.",12,Muted,78,537);
using(var pen=new Pen(Color.FromArgb(58,Gold)))g.DrawLine(pen,78,600,470,600);
Label(g,"离线运行  /  动态折射  /  撤回恢复",15,Muted,76,628);
Label(g,"OFFLINE    ·    LIVE LENSING    ·    UNDO",11,Muted,78,665);
using(var shadow=Round(new RectangleF(610,192,758,542),20))using(var brush=new SolidBrush(Color.FromArgb(95,0,0,0)))g.FillPath(brush,shadow);
using(var path=Round(new RectangleF(600,180,758,542),18))using(var brush=new SolidBrush(Color.FromArgb(232,233,227)))g.FillPath(brush,path);
using(var brush=new SolidBrush(Color.FromArgb(217,221,217)))g.FillRectangle(brush,600,231,758,1);
for(int i=0;i<3;i++)using(var brush=new SolidBrush(new[]{Color.FromArgb(183,139,125),Color.FromArgb(193,174,122),Color.FromArgb(135,163,139)}[i]))g.FillEllipse(brush,623+i*21,201,9,9);
Label(g,"SANDBOX / WORKSPACE",13,Color.FromArgb(91,105,106),725,196);
Label(g,"A place for everyday things.",24,Color.FromArgb(51,67,69),637,265,true);
Label(g,"Name",12,Color.FromArgb(115,125,123),637,325);
Label(g,"Type",12,Color.FromArgb(115,125,123),1125,325);
string[] files={"notes.md","drafts","reference.png","ideas.txt","weekend-plan.md","archive"};
for(int i=0;i<6;i++) {
int y=365+i*48;
using(var pen=new Pen(Color.FromArgb(210,216,210)))g.DrawLine(pen,635,y+36,1321,y+36);
using(var brush=new SolidBrush(Color.FromArgb(180,192,183)))g.FillRectangle(brush,640,y+9,12,16);
Label(g,files[i],16,Color.FromArgb(75,89,88),670,y+4);
Label(g,i==1||i==5?"Folder":"Document",12,Color.FromArgb(124,136,130),1125,y+6);
}
Label(g,"SIMULATED WORKSPACE",11,Color.FromArgb(127,137,132),637,687);
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
int size=620,x=750+(int)(48*Math.Sin(t*.85)),y=172+(int)(18*Math.Sin(t*.6));
using(var crop=scene.Clone(new Rectangle(x,y,size,size),PixelFormat.Format32bppArgb)) {
Bitmap hole=gpu.Render(crop,t,.7f,0,2);
using(var g=Graphics.FromImage(scene))g.DrawImageUnscaled(hole,x,y);
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
