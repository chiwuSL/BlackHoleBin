using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
internal static class WindowProbe {
delegate bool Visitor(IntPtr hwnd,IntPtr data);
[DllImport("user32.dll")] static extern bool EnumWindows(Visitor callback,IntPtr data);
[DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
[DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
[DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);
[DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd,out Rect r);
[DllImport("user32.dll")] static extern bool GetLayeredWindowAttributes(IntPtr hwnd,out uint key,out byte alpha,out uint flags);
[DllImport("user32.dll")] static extern bool GetWindowDisplayAffinity(IntPtr hwnd,out uint affinity);
[StructLayout(LayoutKind.Sequential)] struct Rect {public int L,T,R,B;}
static void Main() {
foreach(var p in Process.GetProcessesByName("BlackHoleBin")) {
Console.WriteLine("Process="+p.Id);
EnumWindows(delegate(IntPtr hwnd,IntPtr data){uint pid;GetWindowThreadProcessId(hwnd,out pid);if(pid!=(uint)p.Id)return true;
Rect r;GetWindowRect(hwnd,out r);uint key,flags,affinity;byte alpha;bool layered=GetLayeredWindowAttributes(hwnd,out key,out alpha,out flags);GetWindowDisplayAffinity(hwnd,out affinity);
Console.WriteLine("HWND="+hwnd+" visible="+IsWindowVisible(hwnd)+" minimized="+IsIconic(hwnd)+" rect="+r.L+","+r.T+","+r.R+","+r.B+" layered="+layered+" alpha="+alpha+" affinity="+affinity);return true;},IntPtr.Zero);
}
using(var key=Registry.CurrentUser.OpenSubKey("Software\\BlackHoleBin")){if(key!=null)foreach(string n in key.GetValueNames())if(n=="HideWhenMaximized" || n=="HoleSize")Console.WriteLine(n+"="+key.GetValue(n));}
}
}
