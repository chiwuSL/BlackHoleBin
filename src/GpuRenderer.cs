using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace BlackHoleBin {
internal sealed class GpuRenderer : IDisposable {
    [DllImport("BlackHoleGpu.dll", CallingConvention=CallingConvention.Cdecl)] static extern IntPtr BHCreate();
    [DllImport("BlackHoleGpu.dll", CallingConvention=CallingConvention.Cdecl)] static extern void BHDestroy(IntPtr handle);
    [DllImport("BlackHoleGpu.dll", CallingConvention=CallingConvention.Cdecl)] static extern int BHRender(IntPtr handle,int size,IntPtr bg,int bgStride,IntPtr output,int outStride,float time,float intensity,float activity,float theme);
    private IntPtr handle;
    private Bitmap frame;
    public GpuRenderer() { handle=BHCreate(); if(handle==IntPtr.Zero)throw new InvalidOperationException("Direct3D 11 hardware initialization failed."); }
    public Bitmap Render(Bitmap background,float time,float intensity,float activity,int theme) {
        int n=background.Width;
        if(frame==null || frame.Width!=n){if(frame!=null)frame.Dispose();frame=new Bitmap(n,n,PixelFormat.Format32bppArgb);}
        BitmapData src=null,dst=null;
        try {
            src=background.LockBits(new Rectangle(0,0,n,n),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            dst=frame.LockBits(new Rectangle(0,0,n,n),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
            Marshal.ThrowExceptionForHR(BHRender(handle,n,src.Scan0,src.Stride,dst.Scan0,dst.Stride,time,intensity,activity,theme));
        } finally {if(src!=null)background.UnlockBits(src);if(dst!=null)frame.UnlockBits(dst);}
        return frame;
    }
    public void Dispose(){if(handle!=IntPtr.Zero){BHDestroy(handle);handle=IntPtr.Zero;}if(frame!=null){frame.Dispose();frame=null;}}
}
}
