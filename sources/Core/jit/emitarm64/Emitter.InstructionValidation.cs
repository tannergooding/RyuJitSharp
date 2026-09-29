// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

public partial class Emitter
{
    private static bool isValidImmShift(nint imm, emitAttr size)
    {
        return (imm >= 0) && (imm < getBitWidth(size));
    }

    private static bool isValidVectorShiftAmount(nint shiftAmount, emitAttr size, bool rightShift)
    {
        return (rightShift && (shiftAmount >= 1) && (shiftAmount <= getBitWidth(size))) ||
               ((shiftAmount >= 0) && (shiftAmount < getBitWidth(size)));
    }

    private static bool isValidGeneralDatasize(emitAttr size)
    {
        return (size == EA_8BYTE) || (size == EA_4BYTE);
    }

    private static bool isValidScalarDatasize(emitAttr size)
    {
        return (size == EA_8BYTE) || (size == EA_4BYTE) || (size == EA_2BYTE);
    }

    private static bool isValidVectorDatasize(emitAttr size)
    {
        return (size == EA_16BYTE) || (size == EA_8BYTE);
    }

    private static bool isValidVectorLSPDatasize(emitAttr size)
    {
        return (size == EA_16BYTE) || (size == EA_8BYTE) || (size == EA_4BYTE);
    }

    private static bool isValidVectorElemsize(emitAttr size)
    {
        return (size == EA_8BYTE) || (size == EA_4BYTE) || (size == EA_2BYTE) || (size == EA_1BYTE);
    }

    private static bool isValidVectorFcvtsize(emitAttr size)
    {
        return (size == EA_8BYTE) || (size == EA_4BYTE) || (size == EA_2BYTE);
    }

    private static bool isValidVectorElemsizeFloat(emitAttr size)
    {
        return (size == EA_8BYTE) || (size == EA_4BYTE);
    }

    private static bool isGeneralRegister(regNumber reg)
    {
        return (reg >= REG_INT_FIRST) && (reg <= REG_LR);
    }

    private static bool isGeneralRegisterOrZR(regNumber reg)
    {
        return (reg >= REG_INT_FIRST) && (reg <= REG_ZR);
    }

    private static bool isVectorRegister(regNumber reg)
    {
        return (reg >= REG_FP_FIRST && reg <= REG_FP_LAST);
    }

    private static bool insOptsNone(insOpts opt)
    {
        return (opt == INS_OPTS_NONE);
    }

    private static bool insOptsIndexed(insOpts opt)
    {
        return (opt == INS_OPTS_PRE_INDEX) || (opt == INS_OPTS_POST_INDEX);
    }

    private static bool insOptsPostIndex(insOpts opt)
    {
        return (opt == INS_OPTS_POST_INDEX);
    }

    private static bool insOptsLSL12(insOpts opt) // special 12-bit shift only used for imm12
    {
        return (opt == INS_OPTS_LSL12);
    }

    private static bool insOptsAnyShift(insOpts opt)
    {
        return ((opt >= INS_OPTS_LSL) && (opt <= INS_OPTS_ROR));
    }

    private static bool insOptsAluShift(insOpts opt) // excludes ROR
    {
        return ((opt >= INS_OPTS_LSL) && (opt <= INS_OPTS_ASR));
    }

    private static bool insOptsLSL(insOpts opt)
    {
        return (opt == INS_OPTS_LSL);
    }

    private static bool insOptsAnyExtend(insOpts opt)
    {
        return ((opt >= INS_OPTS_UXTB) && (opt <= INS_OPTS_SXTX));
    }

    private static bool insOptsLSExtend(insOpts opt)
    {
        return ((opt == INS_OPTS_NONE) || (opt == INS_OPTS_LSL) || (opt == INS_OPTS_UXTW) || (opt == INS_OPTS_SXTW) ||
                (opt == INS_OPTS_UXTX) || (opt == INS_OPTS_SXTX));
    }

    private static bool insOptsAnyArrangement(insOpts opt)
    {
        return ((opt >= INS_OPTS_8B) && (opt <= INS_OPTS_2D));
    }

    private static bool insOptsConvertFloatToFloat(insOpts opt)
    {
        return ((opt >= INS_OPTS_S_TO_D) && (opt <= INS_OPTS_D_TO_H));
    }

    private static bool insOptsConvertFloatToInt(insOpts opt)
    {
        return ((opt >= INS_OPTS_S_TO_4BYTE) && (opt <= INS_OPTS_H_TO_8BYTE));
    }

    private static bool insOptsConvertIntToFloat(insOpts opt)
    {
        return ((opt >= INS_OPTS_4BYTE_TO_S) && (opt <= INS_OPTS_8BYTE_TO_H));
    }
}
#endif
