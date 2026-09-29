// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86
namespace RyuJitSharp;

public partial class Emitter
{
    public static bool IsExtendedReg(regNumber reg, emitAttr attr)
        => throw new FatalJitException(CORJIT_SKIPPED, "x86 extended-register attribute check is not ported.");

    public static bool emitVerifyEncodable(instruction ins, emitAttr size, regNumber reg1, regNumber reg2 = REG_NA)
        => throw new FatalJitException(CORJIT_SKIPPED, "x86 emitVerifyEncodable is not ported.");

    public uint emitInsSizeCV(instrDesc id, ulong code)
        => throw new FatalJitException(CORJIT_SKIPPED, "x86 emitInsSizeCV is not ported.");

    public uint emitInsSizeCV(instrDesc id, ulong code, int val)
        => throw new FatalJitException(CORJIT_SKIPPED, "x86 emitInsSizeCV with immediate is not ported.");

    public uint emitGetRexPrefixSize(instrDesc id, instruction ins)
        => throw new FatalJitException(CORJIT_SKIPPED, "x86 emitGetRexPrefixSize is not ported.");

    private void dispIns(instrDesc id)
        => throw new FatalJitException(CORJIT_SKIPPED, "x86 dispIns is not ported.");
}
#endif
