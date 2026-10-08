$ErrorActionPreference = 'Stop'
$base = 'https://raw.githubusercontent.com/microsoft/win32metadata/main/generation/WinSDK/RecompiledIdlHeaders/um/'
$user = Invoke-RestMethod ($base + 'WinUser.h')
$nt = Invoke-RestMethod ($base + 'winnt.h')
$dwm = Invoke-RestMethod ($base + 'dwmapi.h')
$windef = Invoke-RestMethod 'https://raw.githubusercontent.com/microsoft/win32metadata/main/generation/WinSDK/RecompiledIdlHeaders/shared/windef.h'
$expected = @{
    GWL_STYLE=-16; GWL_EXSTYLE=-20; WS_EX_LAYERED=0x80000; WS_EX_TOOLWINDOW=0x80;
    WS_EX_NOACTIVATE=0x08000000; WS_CAPTION=0x00C00000; WS_MINIMIZEBOX=0x20000; WS_CHILD=0x40000000;
    WM_GETTITLEBARINFOEX=0x033F; WM_MOUSEACTIVATE=0x21; WM_HOTKEY=0x312; LWA_ALPHA=2;
    WINEVENT_OUTOFCONTEXT=0; WINEVENT_SKIPOWNPROCESS=2; EVENT_SYSTEM_FOREGROUND=3;
    EVENT_SYSTEM_MINIMIZESTART=0x16; EVENT_SYSTEM_MINIMIZEEND=0x17; EVENT_OBJECT_DESTROY=0x8001;
    EVENT_OBJECT_SHOW=0x8002; EVENT_OBJECT_HIDE=0x8003; EVENT_OBJECT_LOCATIONCHANGE=0x800B;
    SMTO_ABORTIFHUNG=2; GA_ROOT=2; GW_OWNER=4; MONITOR_DEFAULTTONEAREST=2;
    SWP_NOSIZE=1; SWP_NOMOVE=2; SWP_NOZORDER=4; SWP_NOACTIVATE=0x10; SWP_FRAMECHANGED=0x20; SWP_SHOWWINDOW=0x40;
    MOD_ALT=1; MOD_CONTROL=2; MOD_SHIFT=4; MOD_NOREPEAT=0x4000; MA_NOACTIVATE=3; SM_CXSIZE=30; WH_MOUSE_LL=14;
    PROCESS_QUERY_LIMITED_INFORMATION=0x1000; TOKEN_QUERY=8;
}
foreach ($name in $expected.Keys) {
    $match = [regex]::Match(($user + "`n" + $nt), '(?m)^\s*#define\s+' + $name + '\s+\(?(-?(?:0x[0-9a-fA-F]+|\d+))')
    if (-not $match.Success) { throw "Missing official definition: $name" }
    $value = $match.Groups[1].Value
    $actual = if ($value.StartsWith('0x')) { [Convert]::ToInt64($value.Substring(2),16) } else { [long]::Parse($value) }
    if ($actual -ne $expected[$name]) { throw "Constant mismatch: $name" }
}
$enum = [regex]::Match($dwm, '(?s)enum DWMWINDOWATTRIBUTE\s*\{(.*?)\};').Groups[1].Value
$enum = [regex]::Replace($enum, '//[^\r\n]*', '')
$value = 0
$attributes = @{}
foreach ($part in $enum.Split(',')) {
    $match = [regex]::Match($part, '(DWMWA_\w+)\s*(?:=\s*(\d+))?')
    if (-not $match.Success) { continue }
    if ($match.Groups[2].Success) { $value = [int]$match.Groups[2].Value }
    $attributes[$match.Groups[1].Value] = $value
    $value++
}
if ($attributes.DWMWA_CAPTION_BUTTON_BOUNDS -ne 5 -or $attributes.DWMWA_EXTENDED_FRAME_BOUNDS -ne 9 -or $attributes.DWMWA_CLOAKED -ne 14) { throw 'DWM attribute mismatch' }
if ($windef -notmatch 'DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2\s+\(\(DPI_AWARENESS_CONTEXT\)-4\)') { throw 'DPI awareness context mismatch' }
$tokens = [regex]::Match($nt, '(?s)typedef enum _TOKEN_INFORMATION_CLASS\s*\{(.*?)\}').Groups[1].Value
$value = 0
$integrity = -1
foreach ($part in $tokens.Split(',')) {
    $match = [regex]::Match($part, '(Token\w+)\s*(?:=\s*(\d+))?')
    if (-not $match.Success) { continue }
    if ($match.Groups[2].Success) { $value = [int]$match.Groups[2].Value }
    if ($match.Groups[1].Value -eq 'TokenIntegrityLevel') { $integrity = $value }
    $value++
}
if ($integrity -ne 25) { throw 'Token integrity information class mismatch' }
Write-Output "PASS: $($expected.Count + 5) constants verified against official Microsoft SDK headers."
