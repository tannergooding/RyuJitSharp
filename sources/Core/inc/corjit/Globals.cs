// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Globals
{
    public const int FACILITY_WINDOWS = 8;

    public const int FACILITY_URT = 19;

    public const int FACILITY_UMI = 22;

    public const int FACILITY_SXS = 23;

    public const int FACILITY_STORAGE = 3;

    public const int FACILITY_SSPI = 9;

    public const int FACILITY_SCARD = 16;

    public const int FACILITY_SETUPAPI = 15;

    public const int FACILITY_SECURITY = 9;

    public const int FACILITY_RPC = 1;

    public const int FACILITY_WIN32 = 7;

    public const int FACILITY_CONTROL = 10;

    public const int FACILITY_NULL = 0;

    public const int FACILITY_MSMQ = 14;

    public const int FACILITY_MEDIASERVER = 13;

    public const int FACILITY_INTERNET = 12;

    public const int FACILITY_ITF = 4;

    public const int FACILITY_DPLAY = 21;

    public const int FACILITY_DISPATCH = 2;

    public const int FACILITY_COMPLUS = 17;

    public const int FACILITY_CERT = 11;

    public const int FACILITY_ACS = 20;

    public const int FACILITY_AAF = 18;

    public const int NO_ERROR = 0;

    public const int SEVERITY_SUCCESS = 0;

    public const int SEVERITY_ERROR = 1;

    public const int FACILITY_NT_BIT = 0x1000_0000;

    public const int DEFAULT_UNKNOWN_HANDLE = 1;

    public const int UNKNOWN_HANDLE_MIN = 1;

    public const int UNKNOWN_HANDLE_MAX = 33;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool SUCCEEDED(int Status) => Status >= 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool FAILED(int Status) => Status < 0;

    // diff from win32
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IS_ERROR(uint Status) => (Status >> 31) == SEVERITY_ERROR;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int HRESULT_CODE(int hr) => (hr) & 0xFFFF;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint SCODE_CODE(uint sc) => (sc) & 0xFFFF;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int HRESULT_FACILITY(int hr) => ((hr) >> 16) & 0x1FFF;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint SCODE_FACILITY(uint sc) => ((sc) >> 16) & 0x1FFF;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int HRESULT_SEVERITY(int hr) => ((hr) >> 31) & 0x1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint SCODE_SEVERITY(uint sc) => ((sc) >> 31) & 0x1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MAKE_HRESULT(int sev, int fac, int code) => (sev << 31) | (fac << 16) | code;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint MAKE_SCODE(uint sev, uint fac, uint code) => (sev << 31) | (fac << 16) | code;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int HRESULT_FROM_WIN32(int x) => (x <= 0) ? x : MAKE_HRESULT(SEVERITY_ERROR, FACILITY_WIN32, x & 0xFFFF);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int __HRESULT_FROM_WIN32(int x) => HRESULT_FROM_WIN32(x);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int HRESULT_FROM_NT(int x) => x | FACILITY_NT_BIT;
}
