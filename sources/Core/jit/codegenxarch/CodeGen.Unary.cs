// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.InteropServices;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForNegNotXarch(GenTreeUnOp tree)
    {
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper is GT_NEG or GT_NOT);
        var targetReg = tree.RegNum;
        var targetType = tree.Type;

        if (varTypeIsFloating(targetType))
        {
            assert(tree.Oper is GT_NEG);
            genIntrinsicBitwiseOp(tree);
        }
        else
        {
            var operand = tree.Op1;
            assert(operand.IsUsedFromReg);
            var operandReg = genConsumeReg(operand);
            var ins = genGetInsForOper(tree.Oper, targetType);
            Emitter.emitIns_BASE_R_R(ins, targetType.EmitActualSize, targetReg, operandReg);
        }

        genProduceReg(tree);
    }

    public unsafe void genIntrinsicBitwiseOp(GenTree tree)
    {
        Emitter.RequireSupportedInstructionRecording();
        var targetReg = tree.RegNum;
        var operand = tree.AsUnOp().Op1;
        var operandReg = genConsumeReg(operand);
        assert(varTypeIsFloating(tree.Type));
        assert(operand.IsUsedFromReg);

        ulong mask = 0;
        var ins = INS_invalid;
        if (tree.Oper is GT_NEG)
        {
            // Packed masks flip only the sign bit of each scalar lane.
            ins = INS_xorps;
            mask = tree.Type is TYP_FLOAT ? 0x8000000080000000UL : 0x8000000000000000UL;
        }
        else if (tree.Oper is GT_INTRINSIC)
        {
            assert(tree.AsIntrinsic().IntrinsicName == NI_System_Math_Abs);
            ins = INS_andps;
            mask = tree.Type is TYP_FLOAT ? 0x7FFFFFFF7FFFFFFFUL : 0x7FFFFFFFFFFFFFFFUL;
        }
        else
        {
            assert(false, "genIntrinsicBitwiseOp: unsupported oper");
        }

        simd16_t value = default;
        value.u64[0] = mask;
        value.u64[1] = mask;
        var handle = Configuration.FEATURE_SIMD
            ? Emitter.emitSimd16Const(value)
            : Emitter.emitBlkConst(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(in value, 1)),
                16, tree.Type);
        Emitter.emitIns_SIMD_R_R_C(ins, EA_16BYTE, targetReg, operandReg, handle, 0, INS_OPTS_NONE);
    }
}
