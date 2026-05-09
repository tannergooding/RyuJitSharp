// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Globals
{
    // ******************* HRESULT types ****************************************

    public const int FACILITY_NULL = 0;

    public const int FACILITY_ITF = 4;

    public const int FACILITY_WIN32 = 7;

    public const int FACILITY_CONTROL = 10;

    public const int FACILITY_URT = 19;

    public const int NO_ERROR = 0;

    public const int SEVERITY_SUCCESS = 0;

    public const int SEVERITY_ERROR = 1;

    public const int FACILITY_NT_BIT = 0x10000000;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool SUCCEEDED(int Status) => (Status >= 0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool FAILED(int Status) => (Status < 0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int HRESULT_CODE(int hr) => (hr & 0xFFFF);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int HRESULT_FACILITY(int hr) => ((hr >>> 16) & 0x1fff);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MAKE_HRESULT(int sev, int fac, int code) => (sev << 31) | (fac << 16) | code;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MAKE_SCODE(int sev, int fac, int code) => (sev << 31) | (fac << 16) | code;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int HRESULT_FROM_WIN32(int x) => (x <= 0) ? x : MAKE_HRESULT(SEVERITY_ERROR, FACILITY_WIN32, x & 0xFFFF);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int __HRESULT_FROM_WIN32(int x) => HRESULT_FROM_WIN32(x);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int HRESULT_FROM_NT(int x) => x | FACILITY_NT_BIT;
}
