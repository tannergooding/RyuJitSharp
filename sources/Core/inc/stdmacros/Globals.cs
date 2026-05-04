// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Globalization;
using System.Text;

namespace RyuJitSharp;

public partial class Globals
{
#if !HOST_64BIT
    public static nuint INVALID_POINT_CC => unchecked((nuint)(0xCCCCCCCC_CCCCCCCC));

    public static nuint INVALID_POINT_CD => unchecked((nuint)(0xCDCDCDCD_CDCDCDCD));

    public static readonly CompositeFormat FMT_ADDR = CompositeFormat.Parse(" {0:X8}`{1:X8} ");

    public static unsafe string FMT_DBG_ADDR(void* ptr) => string.Format(CultureInfo.InvariantCulture, FMT_ADDR, unchecked((uint)((nuint)(ptr) >> 32)), unchecked((uint)(ptr)));
#else
    public const uint INVALID_POINT_CC = 0xCCCCCCCC;

    public const uint INVALID_POINT_CD = 0xCDCDCDCD;

    public static readonly CompositeFormat FMT_ADDR = CompositeFormat.Parse(" {0:X8} ");

    public static unsafe string FMT_DBG_ADDR(void* ptr) => string.Format(CultureInfo.InvariantCulture, FMT_ADDR, unchecked((uint)(ptr)));
#endif
}
