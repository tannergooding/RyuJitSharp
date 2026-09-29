// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

public partial class Emitter
{
    private static bool isIntegerRegister(regNumber reg)
    {
        return (reg >= REG_INT_FIRST) && (reg <= REG_INT_LAST);
    }

    private static bool isValidUimm(nint value, int bits)
    {
        var max = unchecked((nuint)(1 << bits));
        return (0 <= value) && (unchecked((nuint)value) < max);
    }

#if DEBUG
    private static void emitInsSveSanityCheck(instrDesc id)
        => throw new FatalJitException(CORJIT_SKIPPED, "ARM64 SVE instruction sanity checking is not ported.");

#endif
}
#endif
