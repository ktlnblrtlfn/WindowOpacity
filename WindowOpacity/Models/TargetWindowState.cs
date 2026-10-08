using System;
namespace WindowOpacity.Models;
public sealed record TargetWindowState(IntPtr Hwnd, uint ProcessId, long OriginalExtendedStyle, IntPtr Marker)
{
    public int CurrentOpacity { get; set; } = 100; public bool WasOriginallyLayered => (OriginalExtendedStyle & 0x80000) != 0; public byte OriginalAlpha => 255;
}
