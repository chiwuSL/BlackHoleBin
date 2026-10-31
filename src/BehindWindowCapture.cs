using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
namespace BlackHoleBin {
    // Render windows below the overlay, rather than excluding the overlay from
    // every capture consumer (which also makes remote desktop unable to see it).
    internal static class BehindWindowCapture {
        delegate bool Visitor(IntPtr window, IntPtr state);
        [StructLayout(LayoutKind.Sequential)] struct Rect {public int L,T,R,B;}
        [DllImport("user32.dll")] static extern bool EnumWindows(Visitor callback,IntPtr state);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr w);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr w);
        [DllImport("user32.dll")] static extern bool IsHungAppWindow(IntPtr w);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr w,out Rect r);
        [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr w,IntPtr dc,uint flags);
        [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr w,int attribute,out int value,int size);
        internal static Bitmap Capture(IntPtr overlay,Rectangle bounds) {
            var windows=new List<IntPtr>();bool below=false;
            EnumWindows(delegate(IntPtr w,IntPtr state) {
                if(w==overlay){below=true;return true;}
                if(!below || !IsWindowVisible(w) || IsIconic(w) || IsHungAppWindow(w))return true;
                int cloaked;if(DwmGetWindowAttribute(w,14,out cloaked,4)==0 && cloaked!=0)return true;
                Rect r;if(GetWindowRect(w,out r) && Rectangle.FromLTRB(r.L,r.T,r.R,r.B).IntersectsWith(bounds))windows.Add(w);
                return true;
            },IntPtr.Zero);
            var output=new Bitmap(bounds.Width,bounds.Height);
            try {
                using(var g=Graphics.FromImage(output)) {
                    g.Clear(SystemColors.Desktop);
                    // Bottom to top preserves normal window occlusion.
                    for(int i=windows.Count-1;i>=0;i--) {
                        Rect r;if(!GetWindowRect(windows[i],out r))continue;
                        int width=r.R-r.L,height=r.B-r.T;
                        if(width<1 || height<1 || width>16384 || height>16384)continue;
                        // PrintWindow can reset the supplied DC viewport. Render the
                        // complete window and crop afterward, otherwise its title bar
                        // is incorrectly copied into the lens (and can look like feedback).
                        using(var layer=new Bitmap(width,height)) {
                            bool ok;
                            using(var lg=Graphics.FromImage(layer)) {
                                IntPtr dc=lg.GetHdc();
                                try {ok=PrintWindow(windows[i],dc,2);}
                                finally {lg.ReleaseHdc(dc);}
                            }
                            if(ok) {
                                Rectangle area=Rectangle.Intersect(bounds,Rectangle.FromLTRB(r.L,r.T,r.R,r.B));
                                Rectangle source=area;
                                source.Offset(-r.L,-r.T);
                                area.Offset(-bounds.Left,-bounds.Top);
                                g.DrawImage(layer,area,source,GraphicsUnit.Pixel);
                            }
                        }
                    }
                }
                return output;
            } catch {output.Dispose();throw;}
        }
    }
}
