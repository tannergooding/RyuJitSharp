// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System.Numerics;

namespace RyuJitSharp;

public partial class Emitter
{
    private static bool isValidSimm(nint value, int bits)
    {
        var sizeBits = nint.Size * 8;
        assert(bits > 0);
        assert(bits <= sizeBits);
        if (bits == sizeBits)
        {
            return true;
        }
        else
        {
            nint max = 1;
            max <<= bits - 1;
            return (-max <= value) && (value < max);
        }
    }

    private static uint NaturalScale_helper(emitAttr size)
    {
        assert((size == EA_1BYTE) || (size == EA_2BYTE) || (size == EA_4BYTE) ||
               (size == EA_8BYTE) || (size == EA_16BYTE));
        return (uint)BitOperations.Log2((uint)size);
    }

    private static uint insGetRegisterListSize(instruction ins)
    {
        uint registerListSize = 0;
        switch (ins)
        {
            case INS_ld1:
            case INS_ld1r:
            case INS_st1:
            case INS_tbl:
            case INS_tbx:
            {
                registerListSize = 1;
                break;
            }

            case INS_ld1_2regs:
            case INS_ld2:
            case INS_ld2r:
            case INS_st1_2regs:
            case INS_st2:
            case INS_tbl_2regs:
            case INS_tbx_2regs:
            {
                registerListSize = 2;
                break;
            }

            case INS_ld1_3regs:
            case INS_ld3:
            case INS_ld3r:
            case INS_st1_3regs:
            case INS_st3:
            case INS_tbl_3regs:
            case INS_tbx_3regs:
            {
                registerListSize = 3;
                break;
            }

            case INS_ld1_4regs:
            case INS_ld4:
            case INS_ld4r:
            case INS_st1_4regs:
            case INS_st4:
            case INS_tbl_4regs:
            case INS_tbx_4regs:
            {
                registerListSize = 4;
                break;
            }

            default:
            {
                assert(false, "Unexpected instruction");
                break;
            }
        }

        return registerListSize;
    }

    private static bool isValidGeneralLSDatasize(emitAttr size)
    {
        return (size == EA_8BYTE) || (size == EA_4BYTE) || (size == EA_2BYTE) || (size == EA_1BYTE);
    }

    private static bool isValidVectorLSDatasize(emitAttr size)
    {
        return (size == EA_16BYTE) || (size == EA_8BYTE) || (size == EA_4BYTE) ||
               (size == EA_2BYTE) || (size == EA_1BYTE);
    }

    public void emitInsSve_R_R_I(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        nint imm, insOpts opt = INS_OPTS_NONE, insScalableOpts sopt = insScalableOpts.INS_SCALABLE_OPTS_NONE)
        => throw new FatalJitException(CORJIT_SKIPPED, "ARM64 SVE two-register/immediate instruction recording is not ported.");

    private static bool TryFoldPageOffsetIntoLdr(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2)
        => throw new FatalJitException(CORJIT_SKIPPED, "ARM64 relocatable page-offset load folding is not ported.");

    private static bool OptimizePostIndexed(instruction ins, regNumber reg, nint imm, emitAttr regAttr)
        => throw new FatalJitException(CORJIT_SKIPPED, "ARM64 post-indexed instruction optimization is not ported.");

    private static bool OptimizeLdrStr(instruction ins, emitAttr reg1Attr, regNumber reg1, regNumber reg2,
        nint imm, emitAttr size, insFormat fmt, bool localVar = false, int varx = -1, int offs = -1
#if DEBUG
        , bool useRsvdReg = false
#endif
        )
        => throw new FatalJitException(CORJIT_SKIPPED, "ARM64 load/store instruction optimization is not ported.");
}
#endif
