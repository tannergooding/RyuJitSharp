// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Globals
{
#if HOST_64BIT
    public static long INVALID_POINT_CC => unchecked((long)(0xCCCCCCCC_CCCCCCCC));

    public static long INVALID_POINT_CD => unchecked((long)(0xCDCDCDCD_CDCDCDCD));

    public static unsafe string FMT_DBG_ADDR(void* ptr) => $" {unchecked((int)((nint)(ptr) >>> 32)):X8}`{unchecked((int)(ptr)):X8} ";
#else
    public const int INVALID_POINT_CC = unchecked((int)(0xCCCCCCCC));

    public const int INVALID_POINT_CD = unchecked((int)(0xCDCDCDCD));

    public static unsafe string FMT_DBG_ADDR(void* ptr) => $" {unchecked((int)(ptr)):X8} ";
#endif
}
