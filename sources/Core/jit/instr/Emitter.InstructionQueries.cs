// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    internal static bool IsSimdInstruction(instruction ins)
    {
#if TARGET_XARCH
        return ins >= FIRST_SSE_INSTRUCTION && ins <= LAST_AVX512_INSTRUCTION;
#else
        return false;
#endif
    }
}
