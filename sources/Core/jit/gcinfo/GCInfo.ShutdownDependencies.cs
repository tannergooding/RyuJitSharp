// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if MEASURE_PTRTAB_SIZE
namespace RyuJitSharp;

public partial struct GCInfo
{
    internal static nuint s_gcRegPtrDscSize => throw new FatalJitException(CORJIT_SKIPPED, "GCInfo::s_gcRegPtrDscSize collection is not ported.");

    internal static nuint s_gcTotalPtrTabSize => throw new FatalJitException(CORJIT_SKIPPED, "GCInfo::s_gcTotalPtrTabSize collection is not ported.");
}
#endif
