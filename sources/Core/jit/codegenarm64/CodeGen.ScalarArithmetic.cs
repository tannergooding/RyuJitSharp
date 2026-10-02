// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.insCond;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForIncSaturate(GenTree tree)
    {
        var targetReg = tree.RegNum;

        // The arithmetic node must be sitting in a register (since it's not contained).
        assert(!tree.IsContained);
        assert(targetReg != REG_NA);

        var operand = tree.AsUnOp().Op1;
        assert(!operand.IsContained);
        var operandReg = genConsumeReg(operand);

        Emitter.emitIns_R_R_I(INS_adds, emitActualTypeSize(tree), targetReg, operandReg, 1);
        Emitter.emitIns_R_R_COND(INS_cinv, emitActualTypeSize(tree), targetReg, targetReg, INS_COND_HS);

        genProduceReg(tree);
    }

    public void genCodeForMulHi(GenTreeOp treeNode)
    {
        assert(!treeNode.HasOverflowCheckEx);

        genConsumeOperands(treeNode);

        var targetReg = treeNode.RegNum;
        var targetType = treeNode.Type;
        var emit = Emitter;
        var attr = emitActualTypeSize(treeNode);
        var isUnsigned = treeNode.IsUnsigned;

        var op1 = treeNode.Op1;
        var op2 = treeNode.Op2;

        assert(!varTypeIsFloating(targetType));
        assert(targetReg != REG_NA);

        if (EA_SIZE(attr) == EA_8BYTE)
        {
            var ins = isUnsigned ? INS_umulh : INS_smulh;
            var resultReg = emit.emitInsTernary(ins, attr, treeNode, op1, op2);

            assert(resultReg == targetReg);
        }
        else
        {
            assert(EA_SIZE(attr) == EA_4BYTE);

            var ins = isUnsigned ? INS_umull : INS_smull;
            _ = emit.emitInsTernary(ins, EA_8BYTE, treeNode, op1, op2);

            emit.emitIns_R_R_I(isUnsigned ? INS_lsr : INS_asr, EA_8BYTE, targetReg, targetReg, 32);
        }

        genProduceReg(treeNode);
    }

    public void genCodeForNegNot(GenTreeOp tree)
    {
        assert(tree.Oper is GT_NEG or GT_NOT);

        var targetType = tree.Type;
        assert(tree.Oper is not GT_NOT || !varTypeIsFloating(targetType));

        var targetReg = tree.RegNum;
        var ins = genGetInsForOper(tree.Oper, targetType);

        if ((tree.Flags & GTF_SET_FLAGS) != 0)
        {
            switch (tree.Oper)
            {
                case GT_NEG:
                {
                    ins = INS_negs;
                    break;
                }

                default:
                {
                    noway_assert(false, "Unexpected unary operator with GTF_SET_FLAGS set.");
                    break;
                }
            }
        }

        assert(!tree.IsContained);
        assert(targetReg != REG_NA);

        var operand = tree.Op1;
        if ((tree.Oper is GT_NEG or GT_NOT) && operand.IsContained)
        {
            var oper = operand.Oper;

            switch (oper)
            {
                case GT_MUL:
                {
                    assert(tree.Oper is GT_NEG);

                    ins = INS_mneg;
                    var op1 = tree.Op1;
                    var a = op1.AsOp().Op1;
                    var b = op1.AsOp().Op2;
                    genConsumeRegs(op1);
                    Emitter.emitIns_R_R_R(ins, emitActualTypeSize(tree), targetReg, a.RegNum, b.RegNum);
                    break;
                }

                case GT_LSH:
                case GT_RSH:
                case GT_RSZ:
                {
                    assert(ins is INS_neg or INS_negs or INS_mvn);
                    assert(operand.AsOp().Op2.Oper.IsCnsIntOrI);
                    assert(operand.AsOp().Op2.IsContained);

                    var op1 = tree.Op1;
                    var a = op1.AsOp().Op1;
                    var b = op1.AsOp().Op2;
                    genConsumeRegs(op1);
                    Emitter.emitIns_R_R_I(ins, emitActualTypeSize(tree), targetReg, a.RegNum,
                        unchecked((nint)b.AsIntConCommon().IntegralValue), ShiftOpToInsOpts(oper));
                    break;
                }

                default:
                {
                    unreached();
                    break;
                }
            }
        }
        else
        {
            assert(!operand.IsContained);
            var operandReg = genConsumeReg(operand);
            Emitter.emitIns_R_R(ins, emitActualTypeSize(tree), targetReg, operandReg);
        }

        genProduceReg(tree);
    }

    public void genCodeForBfiz(GenTreeOp tree)
    {
        assert(tree.Oper is GT_BFIZ);

        var size = emitActualTypeSize(tree);
        var shiftBy = unchecked((uint)tree.Op2.AsIntCon().IconValue);
        var bitWidth = unchecked((uint)tree.Type.ActualType.Size * BITS_PER_BYTE);
        var shiftByImm = shiftBy & (bitWidth - 1);
        var cast = tree.Op1.AsCast();
        var castOp = cast.CastOp;

        genConsumeRegs(castOp);

        var srcBits = varTypeIsSmall(cast.CastType)
            ? unchecked((uint)cast.CastType.Size * BITS_PER_BYTE)
            : unchecked((uint)castOp.Type.ActualType.Size * BITS_PER_BYTE);
        var isUnsigned = cast.IsUnsigned || varTypeIsUnsigned(cast.CastType);

        Emitter.emitIns_R_R_I_I(isUnsigned ? INS_ubfiz : INS_sbfiz, size, tree.RegNum, castOp.RegNum,
            (int)shiftByImm, (int)srcBits);

        genProduceReg(tree);
    }

    public void genCodeForBfx(GenTreeBfm tree)
    {
        assert(tree.Oper is GT_BFX);

        var size = emitActualTypeSize(tree);
        var src = tree.Op1;

        var bitWidth = unchecked((uint)tree.Type.ActualType.Size * BITS_PER_BYTE);
        var lsb = tree.GetOffset();
        var width = tree.GetWidth();

        assert(bitWidth is 32 or 64);
        assert(lsb < bitWidth);
        assert(width > 0);
        assert(unchecked(lsb + width) <= bitWidth);

        genConsumeRegs(src);

        Emitter.emitIns_R_R_I_I(INS_ubfx, size, tree.RegNum, src.RegNum, (int)lsb, (int)width);

        genProduceReg(tree);
    }
}
#endif
