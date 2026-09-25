// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

#if TARGET_XARCH
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForCompare(GenTreeOp tree)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Scalar comparison generation requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper.IsCompare || (tree.Oper is GT_CMP or GT_TEST or GT_BT));
        if (varTypeIsFloating(tree.Op1.Type))
        {
            genCompareFloat(tree);
        }
        else
        {
            genCompareInt(tree);
        }
#endif
    }

    public void genCompareFloat(GenTreeOp tree)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Floating comparison generation requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper.IsCompare || (tree.Oper is GT_CMP));
        var op1 = tree.Op1;
        var op2 = tree.Op2;
        var type = op1.Type;
        assert(varTypeIsFloating(type));
        assert(type == op2.Type);

        var targetReg = tree.RegNum;
        GenCondition condition = default;
        if (tree.Oper is not GT_CMP)
        {
            condition = GenCondition.FromFloatRelop(tree);
            if (condition.PreferSwap)
            {
                condition = GenCondition.Swap(condition);
                (op1, op2) = (op2, op1);
            }
        }
        else
        {
            assert(targetReg == REG_NA);
        }

        var ins = type == TYP_FLOAT ? INS_ucomiss : INS_ucomisd;
        _ = Emitter.emitInsBinary(ins, type.EmitSize, op1, op2);

        if (targetReg != REG_NA)
        {
            if ((condition.Code == GenCondition.CodeKind.FNEU) && op1.IsUsedFromReg && op2.IsUsedFromReg &&
                (op1.RegNum == op2.RegNum))
            {
                // x != x is precisely the unordered (NaN) case.
                condition = new GenCondition(GenCondition.CodeKind.P);
            }

            inst_SETCC(condition, tree.Type, targetReg);
            genProduceReg(tree);
        }
#endif
    }

    public void genCompareInt(GenTreeOp tree)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Integer comparison generation requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper.IsCompare || (tree.Oper is GT_CMP or GT_TEST or GT_BT));
        var op1 = tree.Op1;
        var op2 = tree.Op2;
        var op1Type = op1.Type;
        var op2Type = op2.Type;
        var targetReg = tree.RegNum;
        var canReuseFlags = false;
        assert(!op1.IsContainedIntOrIImmed);
        assert(!varTypeIsFloating(op2Type));

        instruction ins;
        var type = TYP_UNKNOWN;
        if (tree.Oper is GT_TEST_EQ or GT_TEST_NE or GT_TEST)
        {
            ins = INS_test;

            // TEST has no full-width form with a sign-extended byte immediate.
            // A byte-sized mask can instead use a byte-sized TEST.
            if (op2.Oper.IsCnsIntOrI && FitsIn(TYP_UBYTE, op2.AsIntCon().IconValue))
            {
                type = TYP_UBYTE;
            }
        }
        else if (tree.Oper is GT_BITTEST_EQ or GT_BITTEST_NE or GT_BT)
        {
            ins = INS_bt;
            // The instruction masks the index, so its width need not match the value.
            type = op1Type.ActualType;
        }
        else if (op1.IsUsedFromReg && op2.IsIntegralConst(0))
        {
            if (_compiler.opts.OptimizationEnabled)
            {
                var op1Size = op1Type.EmitActualSize;
                assert((int)op1Size >= 4);
                if ((targetReg != REG_NA) && (tree.Oper is GT_LT or GT_GE) && !tree.IsUnsigned)
                {
                    inst_Mov(op1Type, targetReg, op1.RegNum, canSkip: true);
                    if (tree.Oper is GT_GE)
                    {
                        inst_RV(INS_not, targetReg, op1Type);
                    }
                    inst_RV_IV(INS_shr_N, targetReg, ((int)op1Size * 8) - 1, op1Size);
                    genProduceReg(tree);
                    return;
                }
                canReuseFlags = true;
            }

            ins = INS_test;
            op2 = op1;
        }
        else
        {
            ins = INS_cmp;
        }

        if (type == TYP_UNKNOWN)
        {
            if (op1Type == op2Type)
            {
                type = op1Type;
            }
            else if (op1Type.Size == op2Type.Size)
            {
                type = op1Type.Size == 8 ? TYP_LONG : TYP_INT;
            }
            else
            {
                type = TYP_INT;
            }

            assert(type.Size >= Math.Max(op1Type.Size, op2Type.Size));
            assert(!(varTypeIsSmall(type) && varTypeIsUnsigned(type)) || tree.IsUnsigned);
            assert((op1Type.Size >= type.Size) || !op1.IsUsedFromMemory);
            assert((op2Type.Size >= type.Size) || !op2.IsUsedFromMemory);
            assert(!op2.Oper.IsCnsIntOrI || !varTypeIsSmall(type) || FitsIn(type, op2.AsIntCon().IconValue));
        }

        assert(type.Size <= TYP_I_IMPL.Size);
        assert(!varTypeIsUnsigned(type) || varTypeIsSmall(type));
        if (!canReuseFlags || !genCanAvoidEmittingCompareAgainstZero(tree, type))
        {
            var size = type.EmitSize;
            var canSkip = _compiler.opts.OptimizationEnabled && (ins == INS_cmp) &&
                !op1.IsUsedFromMemory && !op2.IsUsedFromMemory &&
                Emitter.IsRedundantCmp(size, op1.RegNum, op2.RegNum);
            if (!canSkip)
            {
                _ = Emitter.emitInsBinary(ins, size, op1, op2);
            }
        }

        if (targetReg != REG_NA)
        {
            inst_SETCC(GenCondition.FromIntegralRelop(tree), tree.Type, targetReg);
            genProduceReg(tree);
        }
#endif
    }

    public bool genCanAvoidEmittingCompareAgainstZero(GenTree tree, var_types opType)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Comparison flag reuse requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        var op = tree.AsOp();
        var op1 = op.Op1;
        assert(op.Op2.IsIntegralConst(0));
        if (!op1.IsUsedFromReg)
        {
            return false;
        }

        GenTree? consumer = null;
        GenCondition condition;
        if (tree.Oper.IsCompare)
        {
            condition = GenCondition.FromIntegralRelop(op);
        }
        else
        {
            consumer = genTryFindFlagsConsumer(tree, out condition);
            if (consumer is null)
            {
                return false;
            }
        }

        if (Emitter.AreFlagsSetToZeroCmp(op1.RegNum, opType.EmitSize, condition))
        {
#if DEBUG
            if (_compiler.verbose)
            {
                jitprintf("Not emitting compare due to flags being already set\n");
            }
#endif
            return true;
        }

        if ((consumer is not null) && Emitter.AreFlagsSetForSignJumpOpt(op1.RegNum, opType.EmitSize, condition))
        {
#if DEBUG
            if (_compiler.verbose)
            {
                jitprintf($"Not emitting compare due to sign being already set; modifying [{consumer.TreeId:D6}] to check sign flag\n");
            }
#endif
            condition = new GenCondition(condition.Code == GenCondition.CodeKind.SLT
                ? GenCondition.CodeKind.S : GenCondition.CodeKind.NS);

            // Native returns a pointer to the condition; update the same owning node.
            if (consumer.Oper is GT_SELECTCC)
            {
                consumer.AsOpCC().Condition = condition;
            }
            else
            {
                consumer.AsCC().Condition = condition;
            }
            return true;
        }

        return false;
#endif
    }

    public static GenTree? genTryFindFlagsConsumer(GenTree producer, out GenCondition condition)
    {
        assert((producer.Flags & GTF_SET_FLAGS) != 0);
        condition = default;
        for (var candidate = producer.Next; candidate is not null; candidate = candidate.Next)
        {
            if (candidate.Oper is GT_JCC or GT_SETCC)
            {
                condition = candidate.AsCC().Condition;
                return candidate;
            }
            if (candidate.Oper is GT_SELECTCC)
            {
                condition = candidate.AsOpCC().Condition;
                return candidate;
            }

            // Resolution may insert these nodes without changing or consuming flags.
            if (candidate.Oper is not GT_LCL_VAR and not GT_COPY and not GT_SWAP)
            {
                return null;
            }
        }

        return null;
    }
}
#endif
