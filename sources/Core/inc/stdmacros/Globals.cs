// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Globals
{
    public static nint INVALID_POINT_CC => unchecked((nint)0xCCCCCCCC_CCCCCCCC);

    public static nint INVALID_POINT_CD => unchecked((nint)0xCDCDCDCD_CDCDCDCD);

    public static unsafe string FMT_DBG_ADDR(void* ptr)
    {
        var address = unchecked((nint)ptr);

        if (Configuration.HOST_64BIT)
        {
            return $" {unchecked((int)(address >>> 32)):x8}`{unchecked((int)address):x8} ";
        }

        return $" {unchecked((int)address):x8} ";
    }

    public static unsafe string FMT_DSP_PTR(void* ptr)
    {
        var address = dspPtr(ptr);

        return Configuration.HOST_64BIT ? $"{address:X16}" : $"{address:X8}";
    }

    public static unsafe string FMT_PTR(void* ptr)
    {
        var address = unchecked((nint)ptr);

        return Configuration.HOST_64BIT ? $"{address:X16}" : $"{address:X8}";
    }
}
