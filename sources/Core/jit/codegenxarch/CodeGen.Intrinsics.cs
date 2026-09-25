// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCkfinite(GenTree tree)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Finite-check generation requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper == GT_CKFINITE);
        var operand = tree.AsUnOp().Op1;
        var targetType = tree.Type;
        // The exponent is bits 23..30 of a float or bits 20..30 of a double's high word.
        var expMask = targetType == TYP_FLOAT ? 0x7F800000 : 0x7FF00000;
        var targetReg = tree.RegNum;
        var tempReg = _internalRegisters.GetSingle(tree);
        _ = genConsumeReg(operand);

        var sourceReg = operand.RegNum;
        var targetIntType = targetType == TYP_FLOAT ? TYP_INT : TYP_LONG;
        inst_Mov(targetIntType, tempReg, sourceReg, canSkip: false, targetType.EmitActualSize);
        if (targetType == TYP_DOUBLE)
        {
            inst_RV_SH(INS_shr, EA_8BYTE, tempReg, 32);
        }

        inst_RV_IV(INS_and, tempReg, expMask, EA_4BYTE);
        inst_RV_IV(INS_cmp, tempReg, expMask, EA_4BYTE);
        genJumpToThrowHlpBlk(EJ_je, SCK_ARITH_EXCPN);
        inst_Mov(targetType, targetReg, operand.RegNum, canSkip: true);

        genProduceReg(tree);
#endif
    }

    public void genIntrinsicRoundOp(GenTreeOp tree)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Scalar rounding intrinsic generation requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper == GT_INTRINSIC);
        var source = tree.Op1;
        assert(varTypeIsFloating(source.Type));
        assert(source.Type == tree.Type);
        assert(!tree.IsUsedFromMemory);
        genConsumeOperands(tree);

        var ins = tree.Type == TYP_FLOAT ? INS_roundss : INS_roundsd;
        var size = tree.Type.EmitSize;
        var targetReg = tree.RegNum;
        sbyte immediate;
        switch (tree.AsIntrinsic().IntrinsicName)
        {
            case NI_System_Math_Round:
            {
                immediate = 4;
                break;
            }

            case NI_System_Math_Ceiling:
            {
                immediate = 10;
                break;
            }

            case NI_System_Math_Floor:
            {
                immediate = 9;
                break;
            }

            case NI_System_Math_Truncate:
            {
                immediate = 11;
                break;
            }

            default:
            {
                assert(false, "genRoundOp: unsupported intrinsic");
                unreached();
                return;
            }
        }

        var isRMW = !_compiler.canUseVexEncoding();
        inst_RV_RV_TT_IV(ins, size, targetReg, targetReg, source, immediate, isRMW, INS_OPTS_NONE);
#endif
    }

    public void genIntrinsic(GenTreeIntrinsic tree)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Intrinsic generation requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        switch (tree.IntrinsicName)
        {
            case NI_System_Math_Abs:
            {
                genIntrinsicBitwiseOp(tree);
                break;
            }

            case NI_System_Math_Ceiling:
            case NI_System_Math_Floor:
            case NI_System_Math_Truncate:
            case NI_System_Math_Round:
            {
                genIntrinsicRoundOp(tree);
                break;
            }

            case NI_System_Math_Sqrt:
            {
                var source = tree.Op1;
                assert(varTypeIsFloating(source.Type));
                assert(source.Type == tree.Type);
                genConsumeOperands(tree);

                var ins = tree.Type == TYP_FLOAT ? INS_sqrtss : INS_sqrtsd;
                var targetReg = tree.RegNum;
                var isRMW = !_compiler.canUseVexEncoding();
                inst_RV_RV_TT(ins, tree.Type.EmitSize, targetReg, targetReg, source, isRMW, INS_OPTS_NONE);
                break;
            }

#if FEATURE_SIMD
            case NI_SIMD_UpperRestore:
            {
                genSimdUpperRestore(tree);
                return;
            }

            case NI_SIMD_UpperSave:
            {
                genSimdUpperSave(tree);
                return;
            }
#endif
            default:
            {
                assert(false, "genIntrinsic: Unsupported intrinsic");
                unreached();
                return;
            }
        }

        genProduceReg(tree);
#endif
    }
}
#endif
