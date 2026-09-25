// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
#if DEBUG
    private GenTree? lastConsumedNode;

    public void genNumberOperandUse(GenTree operand, ref int useNum)
    {
        assert(operand.UseNum == -1);

        if (!operand.IsContained && !operand.Oper.IsCopyOrReload)
        {
            operand.UseNum = useNum;
            useNum++;
        }
        else
        {
            foreach (var op in operand.Operands)
            {
                genNumberOperandUse(op, ref useNum);
            }
        }
    }

    private void genCheckConsumeNode(GenTree node)
    {
        if (_verbose)
        {
            if (node.UseNum == -1)
            {
                // Nodes not numbered for consumption do not need an ordering diagnostic.
            }
            else if ((node._debugFlags & GTF_DEBUG_NODE_CG_CONSUMED) != 0)
            {
                jitprintf("Node was consumed twice:\n");
                _compiler.gtDispTree(node, topOnly: true);
            }
            else if ((lastConsumedNode is not null) && (node.UseNum < lastConsumedNode.UseNum))
            {
                jitprintf("Nodes were consumed out-of-order:\n");
                _compiler.gtDispTree(lastConsumedNode, topOnly: true);
                _compiler.gtDispTree(node, topOnly: true);
            }
        }

        assert((node._debugFlags & GTF_DEBUG_NODE_CG_CONSUMED) == 0);
        assert((lastConsumedNode is null) || (node.UseNum == -1) || (node.UseNum > lastConsumedNode.UseNum));
        node._debugFlags |= GTF_DEBUG_NODE_CG_CONSUMED;
        lastConsumedNode = node;
    }
#endif

    public void genCopyRegIfNeeded(GenTree node, regNumber needReg)
    {
#if TARGET_AMD64
        assert((node.RegNum != REG_NA) && (needReg != REG_NA));
        assert(!node.IsUsedFromSpillTemp);
        inst_Mov(node.Type, needReg, node.RegNum, canSkip: true);
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Register copies outside AMD64 are not implemented.");
#endif
    }

    public void genConsumeRegAndCopy(GenTree node, regNumber needReg)
    {
        if (needReg == REG_NA)
        {
            return;
        }

        _ = genConsumeReg(node);
        genCopyRegIfNeeded(node, needReg);
    }

    public regNumber genConsumeReg(GenTree tree, byte multiRegIndex)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Indexed register consumption outside Windows AMD64 is not implemented.");
#else
        var reg = tree.GetRegByIndex(multiRegIndex);
        if (tree.Oper is GT_COPY)
        {
            reg = genRegCopy(tree, multiRegIndex);
        }
        else if (reg == REG_NA)
        {
            assert(tree.Oper is GT_RELOAD);
            reg = tree.AsUnOp().Op1.GetRegByIndex(multiRegIndex);
            assert(reg != REG_NA);
        }

        genUnspillRegIfNeeded(tree, multiRegIndex);
        assert(treeLifeUpdater is not null);

        if (tree.IsMultiRegLclVar && treeLifeUpdater.UpdateLifeFieldVar(tree.AsLclVar(), multiRegIndex))
        {
            var lcl = tree.AsLclVar();
            genSpillLocal(lcl.LclNum, lcl.GetFieldTypeByIndex(_compiler, multiRegIndex), lcl,
                lcl.GetRegByIndex(multiRegIndex));
        }

        if (tree.SkipCopyOrReload.Oper is GT_LCL_VAR)
        {
            assert(_compiler.lvaEnregMultiRegVars);
            var lcl = tree.SkipCopyOrReload.AsLclVar();
            assert(lcl.IsMultiReg);
            ref var varDsc = ref _compiler.lvaGetDesc(lcl.LclNum);
            assert(varDsc.lvPromoted);
            assert(multiRegIndex < varDsc.lvFieldCnt);
            ref var fieldVarDsc = ref _compiler.lvaGetDesc(varDsc.lvFieldLclStart + multiRegIndex);
            assert(fieldVarDsc.lvLRACandidate);

            if (fieldVarDsc.RegNum == REG_STK)
            {
                _gcInfo.gcMarkRegSetNpt(regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask));
            }
            else if (lcl.IsLastUse(multiRegIndex))
            {
                var fieldReg = fieldVarDsc.RegNum;
                _gcInfo.gcMarkRegSetNpt(regMaskTP.CreateFromRegNum(fieldReg, fieldReg.SingleTypeMask));
            }
        }
        else
        {
            var regAtIndex = tree.GetRegByIndex(multiRegIndex);
            if (regAtIndex != REG_NA)
            {
                _gcInfo.gcMarkRegSetNpt(regMaskTP.CreateFromRegNum(regAtIndex, regAtIndex.SingleTypeMask));
            }
        }

        return reg;
#endif
    }

    public regNumber genConsumeReg(GenTree tree)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Register consumption outside Windows AMD64 is not implemented.");
#else
        if (tree.Oper is GT_COPY)
        {
            genRegCopy(tree);
        }

        // Copy before updating lifetime: spilling can replace the old register home with REG_STK.
        if (genIsRegCandidateLocal(tree))
        {
            var lcl = tree.AsLclVarCommon();
            ref var varDsc = ref _compiler.lvaGetDesc(lcl.LclNum);
            if (varDsc.RegNum != REG_STK)
            {
                inst_Mov(varDsc.GetRegisterType(lcl), tree.RegNum, varDsc.RegNum, canSkip: true);
            }
        }

        genUnspillRegIfNeeded(tree);
        genUpdateLife(tree);

#if EMIT_GENERATE_GCINFO
        if (genIsRegCandidateLocal(tree))
        {
            assert(tree.HasReg(_compiler));
            ref var varDsc = ref _compiler.lvaGetDesc(tree.AsLclVar().LclNum);
            assert(varDsc.lvLRACandidate);

            if (varDsc.RegNum == REG_STK)
            {
                _gcInfo.gcMarkRegSetNpt(tree.RegMask);
            }
            else if ((tree.Flags & GTF_VAR_DEATH) != 0)
            {
                var reg = varDsc.RegNum;
                _gcInfo.gcMarkRegSetNpt(regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask));
            }
        }
        else if (tree.SkipCopyOrReload.IsMultiRegLclVar)
        {
            assert(_compiler.lvaEnregMultiRegVars);
            var lcl = tree.SkipCopyOrReload.AsLclVar();
            ref var varDsc = ref _compiler.lvaGetDesc(lcl.LclNum);

            for (byte i = 0; i < varDsc.lvFieldCnt; i++)
            {
                ref var fieldVarDsc = ref _compiler.lvaGetDesc(varDsc.lvFieldLclStart + i);
                assert(fieldVarDsc.lvLRACandidate);
                var reg = tree.Oper.IsCopyOrReload && (tree.GetRegByIndex(i) != REG_NA)
                    ? tree.GetRegByIndex(i) : lcl.GetRegNumByIdx(i);

                if (fieldVarDsc.RegNum == REG_STK)
                {
                    _gcInfo.gcMarkRegSetNpt(regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask));
                }
                else if (lcl.IsLastUse(i))
                {
                    var fieldReg = fieldVarDsc.RegNum;
                    _gcInfo.gcMarkRegSetNpt(regMaskTP.CreateFromRegNum(fieldReg, fieldReg.SingleTypeMask));
                }
            }
        }
        else
        {
            _gcInfo.gcMarkRegSetNpt(tree.RegMask);
        }
#endif

#if DEBUG
        genCheckConsumeNode(tree);
#endif
        return tree.RegNum;
#endif
    }

    public void genConsumeAddress(GenTree addr)
    {
        if (!addr.IsContained)
        {
            _ = genConsumeReg(addr);
        }
        else if (addr.Oper is GT_LEA)
        {
            genConsumeAddrMode(addr.AsAddrMode());
        }
    }

    public void genConsumeAddrMode(GenTreeAddrMode addr)
    {
        genConsumeOperands(addr);
    }

    public void genConsumeRegs(GenTree tree)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Operand register consumption outside AMD64 is not implemented.");
#else
        if (tree.IsUsedFromSpillTemp)
        {
            // Spill temps are untracked, so they have no variable lifetime to update.
        }
        else if (tree.IsContained)
        {
            if (tree.Oper.IsIndir)
            {
                genConsumeAddress(tree.AsIndir().Addr);
            }
            else if (tree.Oper is GT_LEA)
            {
                genConsumeAddress(tree);
            }
            else if (tree.Oper.IsCompare)
            {
                genConsumeRegs(tree.AsOp().Op1);
                genConsumeRegs(tree.AsOp().Op2);
            }
            else if (tree.Oper is GT_FIELD_LIST)
            {
                foreach (var use in tree.AsFieldList().Uses)
                {
                    genConsumeRegs(use.Node);
                }
            }
            else if (tree.Oper.IsLocalRead)
            {
                ref var varDsc = ref _compiler.lvaGetDesc(tree.AsLclVarCommon().LclNum);
                noway_assert(varDsc.RegNum == REG_STK);
                noway_assert(tree.IsRegOptional || !varDsc.lvLRACandidate);
                genUpdateLife(tree);
            }
#if FEATURE_HW_INTRINSICS
            else if (tree.Oper is GT_HWINTRINSIC)
            {
                genConsumeMultiOpOperands(tree.AsHWIntrinsic());
            }
#endif
            else if (tree.Oper is GT_BITCAST or GT_NEG or GT_CAST or GT_LSH or GT_RSH or GT_RSZ or GT_ROR or GT_BSWAP or GT_BSWAP16)
            {
                genConsumeRegs(tree.AsUnOp().Op1);
            }
            else if (tree.Oper is GT_MUL)
            {
                genConsumeRegs(tree.AsOp().Op1);
                genConsumeRegs(tree.AsOp().Op2);
            }
            else
            {
                assert(tree.Oper.IsLeaf || tree.IsVectorZero);
            }
        }
        else
        {
            _ = genConsumeReg(tree);
        }
#endif
    }

    public void genConsumeOperands(GenTreeUnOp tree)
    {
        if (tree.Op1 is GenTree firstOp)
        {
            genConsumeRegs(firstOp);
        }

        if ((tree is GenTreeOp op) && (op.Op2 is GenTree secondOp))
        {
            genConsumeRegs(secondOp);
        }
    }

#if FEATURE_SIMD || FEATURE_HW_INTRINSICS
    public void genConsumeMultiOpOperands(GenTreeMultiOp tree)
    {
        foreach (var operand in tree.Operands)
        {
            genConsumeRegs(operand);
        }
    }
#endif
}
