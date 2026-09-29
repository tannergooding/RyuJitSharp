// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
namespace RyuJitSharp;

public partial class Emitter
{
    private static sbyte encodeRegAsIval(regNumber opReg)
    {
        assert(((opReg >= REG_XMM0) && (opReg <= REG_XMM15)) || isMaskReg(opReg));
        var ival = (nint)opReg;
        assert((ival >= 0) && (ival <= 0xFF));

        return unchecked((sbyte)ival);
    }

#if TARGET_X86
#if FEATURE_HW_INTRINSICS
    public bool IsThreeOperandAVXInstruction(instruction ins)
    {
        if (!UseSimdEncoding())
        {
            return false;
        }

        return (prefixFlags(ins) & INS_FLAGS_Is3OperandInstructionMask) != 0;
    }
#else
    public bool IsThreeOperandAVXInstruction(instruction ins)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "x86 three-operand classification without hardware intrinsics is not ported.");
    }

    private static bool isAvxBlendv(instruction ins)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "x86 AVX blend classification without hardware intrinsics is not ported.");
    }

    private static bool isAvx512Blendv(instruction ins)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "x86 AVX-512 blend classification without hardware intrinsics is not ported.");
    }

#endif
#endif
}
#endif
