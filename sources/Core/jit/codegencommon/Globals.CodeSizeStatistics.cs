// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class Globals
{
    internal static class CodeSizeStatisticsData
    {
        internal static nuint grossVMsize;
        internal static nuint grossNCsize;
        internal static nuint totalNCsize;
        internal static nuint gcHeaderISize;
        internal static nuint gcPtrMapISize;
        internal static nuint gcHeaderNSize;
        internal static nuint gcPtrMapNSize;
    }
}
