// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System;
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

    public void genCodeForDivMod(GenTreeOp tree)
    {
        assert(tree.Oper is GT_DIV or GT_UDIV);

        var targetType = tree.Type;
        genConsumeOperands(tree);

        if (varTypeIsFloating(targetType))
        {
            // Floating point divide never raises an exception.
            genCodeForBinary(tree);
        }
        else
        {
            var divisor = tree.Op2;
            var size = emitActualTypeSize(tree);
            var divisorReg = divisor.RegNum;
            var exceptionFlags = tree.Exceptions(_compiler);

            if ((exceptionFlags & ExceptionSetFlags.DivideByZeroException) != ExceptionSetFlags.None)
            {
                if (divisor.IsIntegralConst(0))
                {
                    // The operation always throws, but its result still needs to be marked produced.
                    genJumpToThrowHlpBlk(emitJumpKind.EJ_jmp, SCK_DIV_BY_ZERO);
                    genProduceReg(tree);
                    return;
                }

                genJumpToThrowHlpBlk(SCK_DIV_BY_ZERO, (target, invert) =>
                {
                    var condition = invert ? GenCondition.NE : GenCondition.EQ;
                    genCompareImmAndJump(condition, divisorReg, 0, size, target);
                });
            }

            if ((exceptionFlags & ExceptionSetFlags.ArithmeticException) != ExceptionSetFlags.None)
            {
                genCodeForDivModOverflowCheck(tree);
            }

            genCodeForBinary(tree);
        }
    }

    private void genCodeForDivModOverflowCheck(GenTreeOp tree)
    {
        assert(tree.Oper is GT_DIV);
        assert(!tree.Op2.IsIntegralConst(0));

        var size = emitActualTypeSize(tree);
        var divisorReg = tree.Op2.RegNum;
        var dividendReg = tree.Op1.RegNum;
        var sdivLabel = genCreateTempLabel();

        // If the divisor is not -1, skip the signed division overflow check.
        Emitter.emitIns_R_I(INS_cmp, size, divisorReg, -1);
        inst_JMP(emitJumpKind.EJ_ne, sdivLabel);

        // Comparing dividendReg with 1 sets V exactly when dividendReg is MinInt.
        Emitter.emitIns_R_I(INS_cmp, size, dividendReg, 1);
        genJumpToThrowHlpBlk(emitJumpKind.EJ_vs, SCK_ARITH_EXCPN);

        genDefineTempLabel(sdivLabel);
    }

    private void genCompareImmAndJump(GenCondition.CodeKind condition, regNumber reg,
        nint compareImm, emitAttr size, BasicBlock target)
    {
        assert(condition is GenCondition.EQ or GenCondition.NE);

        if (compareImm == 0)
        {
            // Use CBZ/CBNZ for comparisons against zero.
            var ins = condition is GenCondition.EQ ? INS_cbz : INS_cbnz;
            Arm64EmitRegisterBranch(ins, size, target, reg);
        }
        else
        {
            var jumpKind = condition is GenCondition.EQ ? emitJumpKind.EJ_eq : emitJumpKind.EJ_ne;
            Emitter.emitIns_R_I(INS_cmp, size, reg, compareImm);
            inst_JMP(jumpKind, target);
        }
    }

    public void genCodeForJumpCompare(GenTreeOpCC tree)
    {
        assert(_compiler.compCurBB is not null);
        var block = _compiler.compCurBB!;
        assert(block.KindIs(BBJ_COND));

        var op1 = tree.Op1;
        var op2 = tree.Op2 ?? throw new NullReferenceException();

        assert(tree.Oper is GT_JCMP or GT_JTEST);
        assert(!varTypeIsFloating(tree.Type));
        assert(!op1.IsUsedFromMemory);
        assert(!op2.IsUsedFromMemory);
        assert(op2.Oper.IsCnsIntOrI);
        assert(op2.IsContained);

        var condition = tree.Condition.Code;
        assert(condition is GenCondition.EQ or GenCondition.NE);

        genConsumeOperands(tree);

        var reg = op1.RegNum;
        var attr = emitActualTypeSize(op1);

        if (tree.Oper is GT_JTEST)
        {
            var compareImm = op2.AsIntConCommon().IconValue;
            var unsignedCompareImm = unchecked((ulong)(nuint)compareImm);

            assert(System.Numerics.BitOperations.IsPow2(unsignedCompareImm));

            var ins = condition is GenCondition.EQ ? INS_tbz : INS_tbnz;
            var bitIndex = System.Numerics.BitOperations.Log2(unsignedCompareImm);
            Arm64EmitRegisterBranchImmediate(ins, attr, block.TrueTarget, reg, bitIndex);
        }
        else
        {
            assert(op2.IsIntegralConst(0));

            var ins = condition is GenCondition.EQ ? INS_cbz : INS_cbnz;
            Arm64EmitRegisterBranch(ins, attr, block.TrueTarget, reg);
        }

        var falseTarget = block.FalseTarget;
        if (!block.CanRemoveJumpToTarget(falseTarget, _compiler))
        {
            inst_JMP(EJ_jmp, falseTarget);
        }
    }

    private void genJumpToThrowHlpBlk(SpecialCodeKind codeKind, Action<BasicBlock, bool> emitJumpCode,
        BasicBlock? throwBlock = null)
    {
        if (throwBlock is null)
        {
            if (_compiler.fgUseThrowHelperBlocks())
            {
                assert(_compiler.compCurBB is not null);
                var add = _compiler.fgGetExcptnTarget(codeKind, _compiler.compCurBB);
                assert(add.acdUsed);
                throwBlock = add.acdDstBlk;
#if !FEATURE_FIXED_OUT_ARGS
                assert(add.acdStkLvlInit || IsFramePointerUsed);
#endif
                noway_assert(throwBlock is not null);
            }
        }

        if (throwBlock is not null)
        {
            // Branch to the shared throw block on the matching condition.
            emitJumpCode(throwBlock, false);
        }
        else
        {
            // Inline the throw and invert the condition to branch around it.
            var continuation = genCreateTempLabel();
            emitJumpCode(continuation, true);
            genEmitHelperCall(Compiler.acdHelper(codeKind), 0, EA_UNKNOWN);
            genDefineTempLabel(continuation);
        }
    }

    private void Arm64EmitRegisterBranch(instruction ins, emitAttr attr, BasicBlock target, regNumber reg)
    {
        Emitter.emitIns_J_R(ins, attr, target, reg);
    }

    private void Arm64EmitRegisterBranchImmediate(
        instruction ins, emitAttr attr, BasicBlock target, regNumber reg, int immediate)
    {
        Emitter.emitIns_J_R_I(ins, attr, target, reg, immediate);
    }
}
#endif
