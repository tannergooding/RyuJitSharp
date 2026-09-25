// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForBinary(GenTreeOp tree)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Binary arithmetic generation requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        if (tree.HasOverflowCheckEx)
        {
            RequireSharedThrowHelperBlocks();
        }

#if DEBUG
        var valid = tree.Oper is GT_ADD or GT_SUB;
        valid |= varTypeIsFloating(tree.Type)
            ? tree.Oper is GT_MUL or GT_DIV
            : tree.Oper is GT_AND or GT_OR or GT_XOR;
        assert(valid);
#endif
        genConsumeOperands(tree);

        var oper = tree.Oper;
        var targetReg = tree.RegNum;
        var targetType = tree.Type;
        var op1 = tree.Op1;
        var op2 = tree.Op2;
        var eligibleForNdd = false;

        if (!op1.IsUsedFromReg)
        {
            assert(oper.IsCommutative);
            assert(op1.IsMemoryOp || op1.Oper.IsLocal || op1.IsCnsNonZeroFltOrDbl ||
                op1.IsIntCnsFitsInI32 || op1.IsRegOptional);
            op1 = tree.Op2;
            op2 = tree.Op1;
        }

        var ins = genGetInsForOper(oper, targetType);
        noway_assert(targetReg != REG_NA);
        var op1Reg = op1.IsUsedFromReg ? op1.RegNum : REG_NA;
        var op2Reg = op2.IsUsedFromReg ? op2.RegNum : REG_NA;

        if (varTypeIsFloating(targetType))
        {
            var isRmw = !_compiler.canUseVexEncoding();
            if (!isRmw && (oper is not GT_DIV and not GT_SUB))
            {
                assert(oper is GT_ADD or GT_MUL);
                if (!Emitter.IsExtendedReg(op1Reg) && Emitter.IsExtendedReg(op2Reg))
                {
                    // Scalar upper lanes are irrelevant, so commutative operations
                    // may swap sources to permit the shorter VEX prefix.
                    (op1, op2) = (op2, op1);
                    (op1Reg, op2Reg) = (op2Reg, op1Reg);
                }
            }

            inst_RV_RV_TT(ins, tree.Type.EmitSize, targetReg, op1Reg, op2, isRmw, INS_OPTS_NONE);
            genProduceReg(tree);
            return;
        }

        GenTree dst;
        GenTree src;
        if (op1Reg == targetReg)
        {
            dst = op1;
            src = op2;
        }
        else if (op2Reg == targetReg)
        {
            assert(oper.IsCommutative || genIsSameLocalVar(op1, op2));
            dst = op2;
            src = op1;
        }
        else if ((oper is GT_ADD) && !varTypeIsFloating(targetType) && !tree.HasOverflowCheckEx &&
            (op2.IsContainedIntOrIImmed || op2.IsUsedFromReg) && ((tree.Flags & GTF_SET_FLAGS) == 0))
        {
            // LEA avoids the destination copy, but cannot provide arithmetic flags.
            if (op2.IsContainedIntOrIImmed)
            {
                Emitter.emitIns_R_AR(INS_lea, tree.Type.EmitSize, targetReg, op1Reg,
                    unchecked((int)op2.AsIntConCommon().IconValue));
            }
            else
            {
                assert(op2Reg != REG_NA);
                Emitter.emitIns_R_ARX(INS_lea, tree.Type.EmitSize, targetReg, op1Reg, op2Reg, 1, 0);
            }

            genProduceReg(tree);
            return;
        }
        else
        {
            eligibleForNdd = Emitter.DoJitUseApxNDD(ins);
            if (!eligibleForNdd)
            {
                var op1Type = op1.Type;
                inst_Mov(op1Type, targetReg, op1Reg, canSkip: false);
                _regSet.verifyRegUsed(targetReg);
                GCInfo.gcMarkRegPtrVal(targetReg, op1Type);
                dst = tree;
                src = op2;
            }
            else
            {
                dst = op1;
                src = op2;
            }
        }

        assert(!varTypeIsFloating(targetType));
        if ((oper is GT_ADD) && src.IsContainedIntOrIImmed && !tree.HasOverflowCheckEx)
        {
            if (src.IsIntegralConst(1))
            {
                Emitter.emitIns_BASE_R_R(INS_inc, tree.Type.EmitSize, targetReg, dst.RegNum);
                genProduceReg(tree);
                return;
            }
            else if (src.IsIntegralConst(-1))
            {
                Emitter.emitIns_BASE_R_R(INS_dec, tree.Type.EmitSize, targetReg, dst.RegNum);
                genProduceReg(tree);
                return;
            }
        }

        regNumber result;
        if (eligibleForNdd)
        {
            assert(dst.IsUsedFromReg);
            assert(op1Reg != targetReg);
            assert(op2Reg != targetReg);
            result = Emitter.emitIns_BASE_R_R_RM(ins, tree.Type.EmitSize, targetReg, tree, dst, src);
        }
        else
        {
            result = Emitter.emitInsBinary(ins, tree.Type.EmitSize, dst, src);
        }
        noway_assert(result == targetReg);

        if (tree.HasOverflowCheckEx)
        {
            assert(oper is GT_ADD or GT_SUB);
            genCheckOverflow(tree);
        }

        genProduceReg(tree);
#endif
    }

    public void inst_JMP(emitJumpKind jump, BasicBlock target, bool isRemovableJmpCandidate = false)
    {
#if TARGET_AMD64
        Emitter.emitIns_J(RyuJitSharp.Emitter.emitJumpKindToIns(jump), target, false, isRemovableJmpCandidate);
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Jump generation requires AMD64.");
#endif
    }
}
#endif
