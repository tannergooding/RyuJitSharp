// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    public static bool IsBitwiseInstruction(instruction ins)
    {
        return Configuration.TARGET_XARCH && (ins is INS_pandd or INS_pandnd or INS_pord or INS_pxord);
    }

    public static bool DoesWriteZeroFlagForResult(instruction ins)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.XarchInstructionFlagsAreUnavailableOnThisTarget
    );
            return default!;
        }

        // BSF/BSR set ZF from the source rather than the result.
        if (ins is INS_bsf or INS_bsr)
        {
            return false;
        }

        var flags = CodeGen.instInfo[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)];
        return (flags & Writes_ZF) != 0;
    }
}
