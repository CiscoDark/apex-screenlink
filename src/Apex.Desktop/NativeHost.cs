using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Apex.Desktop;
internal static class Native
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr CreateWindowEx(int ex, string cls, string title, int style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);
    [DllImport("user32.dll")] internal static extern bool DestroyWindow(IntPtr h);
    [DllImport("user32.dll", SetLastError = true)] internal static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll")] internal static extern IntPtr SetWindowLongPtr(IntPtr h, int index, IntPtr value);
    [DllImport("user32.dll")] internal static extern IntPtr GetWindowLongPtr(IntPtr h, int index);
    [DllImport("user32.dll")] internal static extern bool MoveWindow(IntPtr h, int x, int y, int w, int height, bool repaint);
    [DllImport("user32.dll")] internal static extern bool GetClientRect(IntPtr h, out Rect r);
    [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumProc callback, IntPtr param);
    internal delegate bool EnumProc(IntPtr h, IntPtr param);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] internal static extern bool PostMessage(IntPtr h, uint message, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] internal static extern IntPtr SetFocus(IntPtr h);
    [DllImport("shell32.dll")] internal static extern void DragAcceptFiles(IntPtr h, bool accept);
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    internal static IntPtr FindWindow(int pid)
    {
        IntPtr result = IntPtr.Zero;
        EnumWindows((h, _) => { GetWindowThreadProcessId(h, out uint p); if (p == pid && IsWindowVisible(h)) { result = h; return false; } return true; }, IntPtr.Zero); return result;
    }
}
public sealed class NativeHost : HwndHost
{
    private IntPtr host, child;
    protected override HandleRef BuildWindowCore(HandleRef parent)
    {
        host = Native.CreateWindowEx(0, "static", "Apex mirror viewport", unchecked((int)0x56000000), 0, 0, 10, 10, parent.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        if (host == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(); return new(this, host);
    }
    public void Attach(IntPtr window)
    {
        child = window;
        long style = Native.GetWindowLongPtr(child, -16).ToInt64();
        Native.SetWindowLongPtr(child, -16, new IntPtr((style & ~0x80000000L & ~0x00C40000L) | 0x40000000L));
        Marshal.SetLastPInvokeError(0); Native.SetParent(child, host);
        if (Marshal.GetLastPInvokeError() != 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError(), "Could not embed the mirror window.");
        Native.DragAcceptFiles(child, false); Resize(); Native.SetFocus(child);
    }
    public void Forget() { child = IntPtr.Zero; }
    private void Resize() { if (child != IntPtr.Zero && Native.GetClientRect(host, out var r)) Native.MoveWindow(child, 0, 0, r.Right, r.Bottom, true); }
    protected override void OnWindowPositionChanged(System.Windows.Rect rc) { base.OnWindowPositionChanged(rc); Resize(); }
    protected override void DestroyWindowCore(HandleRef hwnd) { Native.DestroyWindow(hwnd.Handle); host = IntPtr.Zero; }
    protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        if (msg == 7 && child != IntPtr.Zero) Native.SetFocus(child);
        return base.WndProc(hwnd, msg, w, l, ref handled);
    }
}
