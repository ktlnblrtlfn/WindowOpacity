using System;
using System.Windows;
using System.Windows.Interop;
using WindowOpacity.Native;
using static WindowOpacity.Native.NativeConstants;
using Rect = WindowOpacity.Native.Rect;
namespace WindowOpacity.UI;
public partial class TransparencyOverlay : Window
{
    public IntPtr Handle
    {
        get; private set;
    }
    public event Action? OpenRequested, ResetRequested; public event Action<int>? WheelRequested;
    public TransparencyOverlay()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => { Handle = new WindowInteropHelper(this).Handle; NativeMethods.TryStyle(Handle, GwlExStyle, out var ex); if (!NativeMethods.SetStyle(Handle, GwlExStyle, ex | NativeMethods.ToolWindow | NativeMethods.NoActivate)) throw new System.ComponentModel.Win32Exception(); HwndSource.FromHwnd(Handle).AddHook(Hook); };
        Control.Click += (_, _) => OpenRequested?.Invoke();
        Control.MouseRightButtonUp += (_, e) => { ResetRequested?.Invoke(); e.Handled = true; };
        Control.MouseWheel += (_, e) => { WheelRequested?.Invoke(Math.Sign(e.Delta) * 5); e.Handled = true; };
        new WindowInteropHelper(this).EnsureHandle();
    }
    IntPtr Hook(IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        if (msg == WmMouseActivate)
        {
            handled = true;
            return new IntPtr(MaNoActivate);
        }
        return IntPtr.Zero;
    }
    public bool Place(Rect r)
    {
        if (!IsVisible)
            Show();
        return NativeMethods.SetWindowPos(Handle, new IntPtr(-1), r.Left, r.Top, r.Width, r.Height, SwpNoActivate | SwpShowWindow);
    }
}
