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
    private static nint emitGetInsSC(instrDesc id)
        => throw new FatalJitException(CORJIT_SKIPPED, "ARM64 instruction constant access is not ported.");

    private static void emitInsSveSanityCheck(instrDesc id)
        => throw new FatalJitException(CORJIT_SKIPPED, "ARM64 SVE instruction sanity checking is not ported.");

    public abstract partial class instrDesc
    {
        public bool idIsTlsGD()
            => throw new FatalJitException(CORJIT_SKIPPED, "ARM64 TLS descriptor flags are not ported.");

        public bool idIsLclVar()
            => throw new FatalJitException(CORJIT_SKIPPED, "ARM64 local-variable descriptor flags are not ported.");

        public bool idIsReloc()
            => throw new FatalJitException(CORJIT_SKIPPED, "ARM64 relocation descriptor flags are not ported.");

        public regNumber idReg3()
            => throw new FatalJitException(CORJIT_SKIPPED, "ARM64 third-register descriptor access is not ported.");

        public regNumber idReg4()
            => throw new FatalJitException(CORJIT_SKIPPED, "ARM64 fourth-register descriptor access is not ported.");
    }
#endif
}
#endif
