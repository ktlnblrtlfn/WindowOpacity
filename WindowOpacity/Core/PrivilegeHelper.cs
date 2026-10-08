using System;
using System.Runtime.InteropServices;
using System.Text;
using WindowOpacity.Native;
using static WindowOpacity.Native.NativeConstants;
namespace WindowOpacity.Core;
public static class PrivilegeHelper
{
    public static string? Executable(uint pid)
    {
        var h = NativeMethods.OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (h == IntPtr.Zero)
            return null;
        try
        {
            var s = new StringBuilder(32768);
            int n = s.Capacity;
            return NativeMethods.QueryFullProcessImageName(h, 0, s, ref n) ? s.ToString() : null;
        }
        finally { NativeMethods.CloseHandle(h); }
    }
    public static bool CanAccess(uint pid)
    {
        var other = Integrity(pid);
        return other.HasValue && other <= Integrity((uint)Environment.ProcessId);
    }
    static int? Integrity(uint pid)
    {
        var p = NativeMethods.OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (p == IntPtr.Zero)
            return null;
        IntPtr token = IntPtr.Zero, buffer = IntPtr.Zero;
        try
        {
            if (!NativeMethods.OpenProcessToken(p, TokenQuery, out token))
                return null;
            NativeMethods.GetTokenInformation(token, TokenIntegrityLevel, IntPtr.Zero, 0, out int size);
            if (size <= 0)
                return null;
            buffer = Marshal.AllocHGlobal(size);
            if (!NativeMethods.GetTokenInformation(token, TokenIntegrityLevel, buffer, size, out _))
                return null;
            var sid = Marshal.ReadIntPtr(buffer);
            var count = Marshal.ReadByte(NativeMethods.GetSidSubAuthorityCount(sid));
            return Marshal.ReadInt32(NativeMethods.GetSidSubAuthority(sid, (uint)(count - 1)));
        }
        finally { if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer); if (token != IntPtr.Zero) NativeMethods.CloseHandle(token); NativeMethods.CloseHandle(p); }
    }
}
