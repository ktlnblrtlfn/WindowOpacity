namespace WindowOpacity.Native;

// Values verified against Microsoft's WinSDK headers by scripts/Verify-NativeConstants.ps1.
public static class NativeConstants
{
    public const int GwlStyle = -16, GwlExStyle = -20;
    public const uint WmGetTitlebarInfoEx = 0x033F, WmMouseActivate = 0x0021, WmHotkey = 0x0312;
    public const uint LwaAlpha = 2, WineventSkipOwnProcess = 2, WineventOutOfContext = 0;
    public const uint EventForeground = 3, EventMinimizeStart = 0x16, EventMinimizeEnd = 0x17;
    public const uint EventDestroy = 0x8001, EventShow = 0x8002, EventHide = 0x8003, EventLocationChange = 0x800B;
    public const uint SmtoAbortIfHung = 2, GaRoot = 2, GwOwner = 4, MonitorNearest = 2;
    public const uint SwpNoSize = 1, SwpNoMove = 2, SwpNoZOrder = 4, SwpNoActivate = 0x10, SwpFrameChanged = 0x20, SwpShowWindow = 0x40;
    public const uint ModAlt = 1, ModControl = 2, ModShift = 4, ModNoRepeat = 0x4000;
    public const int DwmCaptionButtonBounds = 5, DwmExtendedFrameBounds = 9, DwmCloaked = 14;
    public const int MaNoActivate = 3, SmCxSize = 30, TokenIntegrityLevel = 25;
    public const uint ProcessQueryLimitedInformation = 0x1000, TokenQuery = 8;
    public const int WhMouseLl = 14;
}
