// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public static bool instIsFP(instruction ins)
    {
        assert((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins) < (uint)instInfo.Length);
        var instructionFlags = instInfo[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)];
        return Configuration.TARGET_XARCH
            ? (instructionFlags & INS_FLAGS_X87Instr) != 0
            : (instructionFlags & (insInfoFlags)INST_FP) != 0;
    }

    public static bool instHasPseudoName(instruction ins)
    {
        if (!Configuration.TARGET_XARCH)
        {
            return false;
        }

        assert((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins) < (uint)instInfo.Length);
        return (instInfo[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)] & INS_FLAGS_HasPseudoName) != 0;
    }

    public static int instKMaskBaseSize(instruction ins)
    {
        if (!Configuration.TARGET_XARCH)
        {
            return 0;
        }

        assert((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins) < (uint)s_kMaskBaseSizes.Length);
        return s_kMaskBaseSizes[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)];
    }

    public static bool instIsEmbeddedMaskingCompatible(instruction ins)
    {
        return Configuration.TARGET_XARCH && (ins is not INS_invalid) && (instKMaskBaseSize(ins) != 0);
    }

    public bool IsEmbeddedBroadcastEnabled(instruction ins, GenTree operand)
    {
        if (!Configuration.TARGET_XARCH || !Configuration.FEATURE_HW_INTRINSICS)
        {
            return false;
        }

        return Emitter.UseEvexEncodings && instIsEmbeddedBroadcastCompatible(ins) &&
            operand.IsContained && operand.Oper.IsHWIntrinsic &&
            operand.AsHWIntrinsic().IsBroadcastScalar;
    }

    // Bit zero is the FP flag; the upper seven bits encode the SIMD element width.
    private const int InstInfoElemSizeShift = 1;

    public static byte instSimdElemSize(instruction ins)
    {
        if (!Configuration.TARGET_WASM)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "Wasm SIMD metadata is not available for the configured target.");
        }

        assert((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins) < (uint)instInfo.Length);
        return (byte)((ulong)instInfo[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)] >> InstInfoElemSizeShift);
    }
}
