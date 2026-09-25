// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public bool genIsRegCandidateLocal(GenTree tree)
    {
        if (!tree.Oper.IsLocal)
        {
            return false;
        }

        return _compiler.lvaGetDesc(tree.AsLclVarCommon().LclNum).lvIsRegCandidate;
    }

    public void genUnspillLocal(int varNum, var_types type, GenTreeLclVar lclNode, regNumber regNum,
        bool reSpill, bool isLastUse)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Local reloads outside AMD64 are not implemented.");
#else
        ref var varDsc = ref _compiler.lvaGetDesc(varNum);
#if DEBUG
        assert((uint)lclNode.LclNum < (uint)_compiler.lvaCount);
        Emitter.emitVarRefOffs = unchecked((int)lclNode.LclIlOffs);
#endif
        var ins = ins_Load(type, _compiler.isSIMDTypeLocalAligned(varNum));
        Emitter.emitIns_R_S(ins, type.EmitSize, regNum, varNum, 0);

        // Native forces the mask instead of genUpdateRegLife: LSRA resolution can leave
        // this register marked live. Its TODO notes a possible GC hole; preserve that behavior.
        if (!reSpill)
        {
            varDsc.RegNum = regNum;

            // Live ranges exclude their end, so a last use must not open a new location.
            if (!isLastUse)
            {
                getVariableLiveKeeper().siUpdateVariableLiveRange(in varDsc, varNum);
            }

            if (!varDsc.IsAlwaysAliveInMemory)
            {
#if DEBUG
                if (_compiler.verbose && VarSetOps.IsMember(_compiler, _gcInfo.gcVarPtrSetCur, varDsc._varIndex))
                {
                    jitprintf($"\t\t\t\t\t\t\tRemoving V{varNum:D2} from gcVarPtrSetCur\n");
                }
#endif
                VarSetOps.RemoveElemD(_compiler, _gcInfo.gcVarPtrSetCur, varDsc._varIndex);
            }

#if DEBUG
            if (_compiler.verbose)
            {
                jitprintf($"\t\t\t\t\t\t\tV{varNum:D2} in reg ");
                varDsc.PrintVarReg();
                jitprintf(" is becoming live  ");
                Compiler.printTreeId(lclNode);
                jitprintf("\n");
            }
#endif
            _regSet.AddMaskVars(genGetRegMask(in varDsc));
        }

        _gcInfo.gcMarkRegPtrVal(regNum, type);
#endif
    }

    public void genUnspillRegIfNeeded(GenTree tree, byte multiRegIndex)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Indexed register reloads outside Windows AMD64 are not implemented.");
#else
        var unspillTree = tree;
        assert(unspillTree.IsMultiRegNode);

        if (tree.Oper is GT_RELOAD)
        {
            unspillTree = tree.AsUnOp().Op1;
        }

        if ((unspillTree.Flags & GTF_SPILLED) == 0)
        {
            return;
        }

        var spillFlags = unspillTree.GetRegSpillFlagByIdx(multiRegIndex);
        if ((spillFlags & GTF_SPILLED) == 0)
        {
            return;
        }

        var dstReg = tree.GetRegByIndex(multiRegIndex);
        if (dstReg == REG_NA)
        {
            assert(tree.Oper.IsCopyOrReload);
            dstReg = unspillTree.GetRegByIndex(multiRegIndex);
        }

        if (tree.IsMultiRegLclVar)
        {
            var lclNode = tree.AsLclVar();
            var fieldVarNum = _compiler.lvaGetDesc(lclNode.LclNum).lvFieldLclStart + multiRegIndex;
            var reSpill = (spillFlags & GTF_SPILL) != 0;
            var isLastUse = lclNode.IsLastUse(multiRegIndex);
            genUnspillLocal(fieldVarNum, _compiler.lvaGetDesc(fieldVarNum).Type, lclNode, dstReg, reSpill, isLastUse);
        }
        else
        {
            var dstType = unspillTree.GetRegTypeByIndex(multiRegIndex);
            var unspillTreeReg = unspillTree.GetRegByIndex(multiRegIndex);
            var temp = _regSet.rsUnspillInPlace(unspillTree, unspillTreeReg, multiRegIndex);
            Emitter.emitIns_R_S(ins_Load(dstType), dstType.EmitActualSize, dstReg, temp.tdTempNum, 0);
            _regSet.tmpRlsTemp(temp);
            _gcInfo.gcMarkRegPtrVal(dstReg, dstType);
        }
#endif
    }

    public void genUnspillRegIfNeeded(GenTree tree)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Register reloads outside Windows AMD64 are not implemented.");
#else
        var unspillTree = tree;
        if (tree.Oper is GT_RELOAD)
        {
            unspillTree = tree.AsUnOp().Op1;
        }

        if ((unspillTree.Flags & GTF_SPILLED) != 0)
        {
            if (genIsRegCandidateLocal(unspillTree))
            {
                assert(ReferenceEquals(tree, unspillTree));
                unspillTree.Flags &= ~GTF_SPILLED;
                var lcl = unspillTree.AsLclVar();
                ref var varDsc = ref _compiler.lvaGetDesc(lcl.LclNum);

                // A later narrow use can rely on normalization performed by this reload,
                // even when this node's type is wider than the normalize-on-load local.
                var unspillType = varDsc.lvNormalizeOnLoad ? varDsc.Type : varDsc.GetStackSlotHomeType();
                if (varTypeIsGC(lcl.Type))
                {
                    unspillType = lcl.Type;
                }

                var reSpill = (unspillTree.Flags & GTF_SPILL) != 0;
                var isLastUse = lcl.IsLastUse(0);
                genUnspillLocal(lcl.LclNum, unspillType, lcl, tree.RegNum, reSpill, isLastUse);
            }
            else if (unspillTree.IsMultiRegLclVar)
            {
                assert(ReferenceEquals(tree, unspillTree));
                var lclNode = unspillTree.AsLclVar();
                ref var varDsc = ref _compiler.lvaGetDesc(lclNode.LclNum);

                for (byte i = 0; i < varDsc.lvFieldCnt; i++)
                {
                    var spillFlags = lclNode.GetRegSpillFlagByIdx(i);
                    if ((spillFlags & GTF_SPILLED) != 0)
                    {
                        var reg = lclNode.GetRegNumByIdx(i);
                        var fieldVarNum = varDsc.lvFieldLclStart + i;
                        var reSpill = (spillFlags & GTF_SPILL) != 0;
                        var isLastUse = lclNode.IsLastUse(i);
                        genUnspillLocal(fieldVarNum, _compiler.lvaGetDesc(fieldVarNum).Type, lclNode, reg,
                            reSpill, isLastUse);
                    }
                }
            }
            else if (unspillTree.IsMultiRegNode)
            {
                var regCount = unspillTree.GetMultiRegCount(_compiler);
                for (byte i = 0; i < regCount; i++)
                {
                    genUnspillRegIfNeeded(tree, i);
                }
                unspillTree.Flags &= ~GTF_SPILLED;
            }
            else
            {
                // The original producer owns the spill temp; a GT_RELOAD owns the new destination.
                var temp = _regSet.rsUnspillInPlace(unspillTree, unspillTree.RegNum);
                var dstReg = tree.RegNum;
                Emitter.emitIns_R_S(ins_Load(unspillTree.Type), unspillTree.Type.EmitActualSize,
                    dstReg, temp.tdTempNum, 0);
                _regSet.tmpRlsTemp(temp);
                unspillTree.Flags &= ~GTF_SPILLED;
                _gcInfo.gcMarkRegPtrVal(dstReg, unspillTree.Type);
            }
        }
#endif
    }
}
