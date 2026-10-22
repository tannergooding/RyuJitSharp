// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct GCInfo
{
    internal static class PointerTableStatisticsData
    {
        internal static readonly nuint s_nativeRegPtrDscSize =
            Configuration.MEASURE_PTRTAB_SIZE ? getNativeRegPtrDscSize() : 0;

        internal static nuint s_gcRegPtrDscSize;

        internal static nuint s_gcTotalPtrTabSize;
    }
}
