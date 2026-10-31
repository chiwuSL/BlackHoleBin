using System;
using System.Runtime.InteropServices;
namespace BlackHoleBin {
[ComVisible(true), Guid("04b0f1a7-9490-44bc-96e1-4296a31252e2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IRecycleProgress {
[PreserveSig] int StartOperations();
[PreserveSig] int FinishOperations(int result);
[PreserveSig] int PreRenameItem(uint flags, IntPtr item, [MarshalAs(UnmanagedType.LPWStr)] string name);
[PreserveSig] int PostRenameItem(uint flags, IntPtr item, [MarshalAs(UnmanagedType.LPWStr)] string name, int result, IntPtr created);
[PreserveSig] int PreMoveItem(uint flags, IntPtr item, IntPtr folder, [MarshalAs(UnmanagedType.LPWStr)] string name);
[PreserveSig] int PostMoveItem(uint flags, IntPtr item, IntPtr folder, [MarshalAs(UnmanagedType.LPWStr)] string name, int result, IntPtr created);
[PreserveSig] int PreCopyItem(uint flags, IntPtr item, IntPtr folder, [MarshalAs(UnmanagedType.LPWStr)] string name);
[PreserveSig] int PostCopyItem(uint flags, IntPtr item, IntPtr folder, [MarshalAs(UnmanagedType.LPWStr)] string name, int result, IntPtr created);
[PreserveSig] int PreDeleteItem(uint flags, IntPtr item);
[PreserveSig] int PostDeleteItem(uint flags, IntPtr item, int result, IntPtr created);
[PreserveSig] int PreNewItem(uint flags, IntPtr folder, [MarshalAs(UnmanagedType.LPWStr)] string name);
[PreserveSig] int PostNewItem(uint flags, IntPtr folder, [MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] string template, uint attributes, int result, IntPtr created);
[PreserveSig] int UpdateProgress(uint total, uint completed);
[PreserveSig] int ResetTimer();
[PreserveSig] int PauseTimer();
[PreserveSig] int ResumeTimer();
}
[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
public sealed class RecycleProgress : IRecycleProgress {
public bool Confirmed;
public bool UnexpectedDeletion;
public bool Blocked;
internal Func<bool> Preflight;
public int StartOperations() { return 0; }
public int FinishOperations(int result) { return 0; }
public int PreRenameItem(uint flags, IntPtr item, [MarshalAs(UnmanagedType.LPWStr)] string name) { return 0; }
public int PostRenameItem(uint flags, IntPtr item, [MarshalAs(UnmanagedType.LPWStr)] string name, int result, IntPtr created) { return 0; }
public int PreMoveItem(uint flags, IntPtr item, IntPtr folder, [MarshalAs(UnmanagedType.LPWStr)] string name) { return 0; }
public int PostMoveItem(uint flags, IntPtr item, IntPtr folder, [MarshalAs(UnmanagedType.LPWStr)] string name, int result, IntPtr created) { return 0; }
public int PreCopyItem(uint flags, IntPtr item, IntPtr folder, [MarshalAs(UnmanagedType.LPWStr)] string name) { return 0; }
public int PostCopyItem(uint flags, IntPtr item, IntPtr folder, [MarshalAs(UnmanagedType.LPWStr)] string name, int result, IntPtr created) { return 0; }
public int PreDeleteItem(uint flags, IntPtr item) {
    try { Blocked = (flags & 0x80) == 0 || (Preflight!=null && !Preflight()); }
    catch { Blocked=true; }
    return Blocked ? unchecked((int)0x80004004) : 0;
}
public int PostDeleteItem(uint flags, IntPtr item, int result, IntPtr created) { Confirmed = result >= 0 && created != IntPtr.Zero; UnexpectedDeletion = result >= 0 && created == IntPtr.Zero; return UnexpectedDeletion ? unchecked((int)0x80004004) : 0; }
public int PreNewItem(uint flags, IntPtr folder, [MarshalAs(UnmanagedType.LPWStr)] string name) { return 0; }
public int PostNewItem(uint flags, IntPtr folder, [MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] string template, uint attributes, int result, IntPtr created) { return 0; }
public int UpdateProgress(uint total, uint completed) { return 0; }
public int ResetTimer() { return 0; }
public int PauseTimer() { return 0; }
public int ResumeTimer() { return 0; }
}
}
