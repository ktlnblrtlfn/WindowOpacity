using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using WindowOpacity.Native;
using static WindowOpacity.Native.NativeConstants;
using WindowOpacity.Core;
using Rect = WindowOpacity.Native.Rect;
namespace WindowOpacity.UI;
public partial class OpacityPopup : Window
{
    bool updating; public IntPtr Handle
    {
        get; private set;
    }
    public event Action<int>? ValueChanged; public event Action? ResetRequested;
    public OpacityPopup()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => { Handle = new WindowInteropHelper(this).Handle; NativeMethods.TryStyle(Handle, GwlExStyle, out var ex); if (!NativeMethods.SetStyle(Handle, GwlExStyle, ex | NativeMethods.ToolWindow | NativeMethods.NoActivate)) throw new System.ComponentModel.Win32Exception(); HwndSource.FromHwnd(Handle).AddHook((IntPtr h, int m, IntPtr w, IntPtr l, ref bool done) => { if (m == WmMouseActivate) { done = true; return new IntPtr(MaNoActivate); } return IntPtr.Zero; }); };
        OpacitySlider.ValueChanged += (_, _) => { Percentage.Text = $"{OpacitySlider.Value:0}%"; if (!updating) ValueChanged?.Invoke((int)OpacitySlider.Value); };
        ResetButton.Click += (_, _) => ResetRequested?.Invoke();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Hide(); };
        new WindowInteropHelper(this).EnsureHandle();
    }
    public void SetValue(int value, int minimum)
    {
        updating = true;
        OpacitySlider.Minimum = minimum;
        MinimumPercentage.Text = $"{minimum}%";
        OpacitySlider.Value = value;
        Percentage.Text = $"{value}%";
        updating = false;
    }
    public void ShowFor(IntPtr target, Rect anchor, int value, int min)
    {
        SetValue(value, min);
        ErrorText.Text = "";
        if (!MonitorHelper.TryGet(target, out var monitor))
            return;
        double scale = DpiHelper.Scale(target);
        int width = (int)Math.Ceiling(Width * scale), height = (int)Math.Ceiling(Height * scale);
        int x = Math.Clamp(anchor.Left, monitor.Work.Left, Math.Max(monitor.Work.Left, monitor.Work.Right - width));
        int y = anchor.Bottom + 4;
        if (y + height > monitor.Work.Bottom)
            y = anchor.Top - height - 4;
        y = Math.Clamp(y, monitor.Work.Top, Math.Max(monitor.Work.Top, monitor.Work.Bottom - height));
        Show();
        if (!NativeMethods.SetWindowPos(Handle, new IntPtr(-1), x, y, width, height, SwpNoActivate | SwpShowWindow))
            Hide();
    }
    public void ShowError() => ErrorText.Text = "Windows could not change this window.";
}
