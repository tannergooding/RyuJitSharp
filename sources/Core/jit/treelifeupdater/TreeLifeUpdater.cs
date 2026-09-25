// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed class TreeLifeUpdater
{
    private readonly Compiler _compiler;
    private readonly bool _forCodeGen;
#if DEBUG
    private readonly int _epoch;
    private nint[] _oldLife;
    private nint[] _oldStackPtrsLife;
#endif

    public TreeLifeUpdater(Compiler compiler, bool forCodeGen)
    {
        _compiler = compiler;
        _forCodeGen = forCodeGen;
#if DEBUG
        _epoch = compiler.CurLVEpoch;
        _oldLife = VarSetOps.MakeEmpty(compiler);
        _oldStackPtrsLife = VarSetOps.MakeEmpty(compiler);
#endif
    }

    public bool UpdateLifeFieldVar(GenTreeLclVar lclNode, byte multiRegIndex)
    {
        ref var parentVarDsc = ref _compiler.lvaGetDesc(lclNode.LclNum);
        assert(parentVarDsc.lvPromoted && (multiRegIndex < parentVarDsc.lvFieldCnt) &&
            lclNode.IsMultiReg && _compiler.lvaEnregMultiRegVars);
        var fieldVarNum = parentVarDsc.lvFieldLclStart + multiRegIndex;
        ref var fldVarDsc = ref _compiler.lvaGetDesc(fieldVarNum);
        assert(fldVarDsc.lvTracked);
        assert((lclNode.Flags & GTF_VAR_USEASG) == 0);

        StoreCurrentLifeForDump();
        var isBorn = (lclNode.Flags & GTF_VAR_DEF) != 0;
        var isDying = !isBorn && lclNode.IsLastUse(multiRegIndex);

        if (isBorn || isDying)
        {
            var previouslyLive = VarSetOps.IsMember(_compiler, _compiler.compCurLife, fldVarDsc._varIndex);
            UpdateLifeBit(_compiler.compCurLife, in fldVarDsc, isBorn, isDying);

            if (_forCodeGen)
            {
                assert(_compiler.codeGen is not null);
                var reg = lclNode.GetRegNumByIdx(multiRegIndex);
                var isInReg = fldVarDsc.lvIsInReg && (reg != REG_NA);
                var isInMemory = !isInReg || fldVarDsc.IsAlwaysAliveInMemory;

                if (isInReg)
                {
                    if (isBorn)
                    {
                        _compiler.codeGen.genUpdateVarReg(ref fldVarDsc, lclNode, multiRegIndex);
                    }

                    _compiler.codeGen.genUpdateRegLife(in fldVarDsc, isBorn, isDying
#if DEBUG
                        , lclNode
#endif
                        );
                }

                if (isInMemory &&
                    VarSetOps.IsMember(_compiler, _compiler.codeGen.GCInfo.gcTrkStkPtrLcls, fldVarDsc._varIndex))
                {
                    UpdateLifeBit(_compiler.codeGen.GCInfo.gcVarPtrSetCur, in fldVarDsc, isBorn, isDying);
                }

                if (previouslyLive != isBorn)
                {
                    _compiler.codeGen.getVariableLiveKeeper().siStartOrCloseVariableLiveRange(
                        in fldVarDsc, fieldVarNum, isBorn, isDying);
                }
            }
        }

        var spill = false;
        if (_forCodeGen && ((lclNode.Flags & lclNode.GetRegSpillFlagByIdx(multiRegIndex) & GTF_SPILL) != 0))
        {
            assert(_compiler.codeGen is not null);
            if (VarSetOps.IsMember(_compiler, _compiler.codeGen.GCInfo.gcTrkStkPtrLcls, fldVarDsc._varIndex))
            {
                if (!VarSetOps.IsMember(_compiler, _compiler.codeGen.GCInfo.gcVarPtrSetCur, fldVarDsc._varIndex))
                {
                    VarSetOps.AddElemD(_compiler, _compiler.codeGen.GCInfo.gcVarPtrSetCur, fldVarDsc._varIndex);
#if DEBUG
                    if (_compiler.verbose)
                    {
                        jitprintf($"\t\t\t\t\t\t\tVar V{fieldVarNum:D2} becoming live\n");
                    }
#endif
                }
            }

            spill = true;
        }

        DumpLifeDelta(lclNode);

        return spill;
    }

    private void UpdateLifeVar(GenTree tree, GenTreeLclVarCommon lclVarTree)
    {
        assert(lclVarTree.Oper.IsNonPhiLocal || (lclVarTree.Oper == GT_LCL_ADDR));
        var lclNum = lclVarTree.LclNum;
        ref var varDsc = ref _compiler.lvaGetDesc(lclNum);
        _compiler.compCurLifeTree = tree;

        // Codegen can retype a promoted struct, so its fields are selected by the descriptor.
        if (!varDsc.lvTracked && !varDsc.lvPromoted)
        {
            return;
        }

        StoreCurrentLifeForDump();
        var isBorn = ((lclVarTree.Flags & GTF_VAR_DEF) != 0) && ((lclVarTree.Flags & GTF_VAR_USEASG) == 0);

        if (varDsc.lvTracked)
        {
            assert(!varDsc.lvPromoted && !lclVarTree.IsMultiRegLclVar);
            var isDying = (lclVarTree.Flags & GTF_VAR_DEATH) != 0;

            if (isBorn || isDying)
            {
                var previouslyLive = _forCodeGen && VarSetOps.IsMember(_compiler, _compiler.compCurLife, varDsc._varIndex);
                UpdateLifeBit(_compiler.compCurLife, in varDsc, isBorn, isDying);

                if (_forCodeGen)
                {
                    assert(_compiler.codeGen is not null);
                    if (isBorn && varDsc.lvIsRegCandidate && tree.HasReg(_compiler))
                    {
                        _compiler.codeGen.genUpdateVarReg(ref varDsc, tree);
                    }

                    var isInReg = varDsc.lvIsInReg && (tree.RegNum != REG_NA);
                    var isInMemory = !isInReg || varDsc.IsAlwaysAliveInMemory;

                    if (isInReg)
                    {
                        _compiler.codeGen.genUpdateRegLife(in varDsc, isBorn, isDying
#if DEBUG
                            , tree
#endif
                            );
                    }

                    if (isInMemory &&
                        VarSetOps.IsMember(_compiler, _compiler.codeGen.GCInfo.gcTrkStkPtrLcls, varDsc._varIndex))
                    {
                        UpdateLifeBit(_compiler.codeGen.GCInfo.gcVarPtrSetCur, in varDsc, isBorn, isDying);
                    }

                    if (isDying == previouslyLive)
                    {
                        _compiler.codeGen.getVariableLiveKeeper().siStartOrCloseVariableLiveRange(
                            in varDsc, lclNum, !isDying, isDying);
                    }
                }
            }

#if HAS_FIXED_REGISTER_SET
            if (_forCodeGen && ((lclVarTree.Flags & GTF_SPILL) != 0))
            {
                assert(_compiler.codeGen is not null);
                _compiler.codeGen.genSpillVar(tree);

                if (VarSetOps.IsMember(_compiler, _compiler.codeGen.GCInfo.gcTrkStkPtrLcls, varDsc._varIndex))
                {
                    if (!VarSetOps.IsMember(_compiler, _compiler.codeGen.GCInfo.gcVarPtrSetCur, varDsc._varIndex))
                    {
                        VarSetOps.AddElemD(_compiler, _compiler.codeGen.GCInfo.gcVarPtrSetCur, varDsc._varIndex);
#if DEBUG
                        if (_compiler.verbose)
                        {
                            jitprintf($"\t\t\t\t\t\t\tVar V{lclNum:D2} becoming live\n");
                        }
#endif
                    }
                }
            }
#endif
        }
        else if (varDsc.lvPromoted)
        {
            var isMultiRegLocal = lclVarTree.IsMultiRegLclVar;
#if DEBUG
            if (isMultiRegLocal)
            {
                assert(lclVarTree == tree);
                assert((lclVarTree.Flags & GTF_VAR_USEASG) == 0);
            }
#endif
            var isAnyFieldDying = lclVarTree.HasLastUse;

            if (isBorn || isAnyFieldDying)
            {
                var firstFieldVarNum = varDsc.lvFieldLclStart;

                for (byte i = 0; i < varDsc.lvFieldCnt; i++)
                {
                    var fldLclNum = firstFieldVarNum + i;
                    ref var fldVarDsc = ref _compiler.lvaGetDesc(fldLclNum);
                    assert(fldVarDsc.lvIsStructField);

                    if (!fldVarDsc.lvTracked)
                    {
                        assert(!isMultiRegLocal);
                        continue;
                    }

                    var previouslyLive = _forCodeGen &&
                        VarSetOps.IsMember(_compiler, _compiler.compCurLife, fldVarDsc._varIndex);
                    var isDying = lclVarTree.IsLastUse(i);
                    UpdateLifeBit(_compiler.compCurLife, in fldVarDsc, isBorn, isDying);

                    if (!_forCodeGen)
                    {
                        continue;
                    }

                    assert(_compiler.codeGen is not null);
                    assert(isMultiRegLocal || !fldVarDsc.lvIsInReg);
                    var isInReg = fldVarDsc.lvIsInReg && (lclVarTree.AsLclVar().GetRegNumByIdx(i) != REG_NA);
                    var isInMemory = !isInReg || fldVarDsc.IsAlwaysAliveInMemory;

                    if (isInReg)
                    {
                        if (isBorn)
                        {
                            _compiler.codeGen.genUpdateVarReg(ref fldVarDsc, tree, i);
                        }

                        _compiler.codeGen.genUpdateRegLife(in fldVarDsc, isBorn, isDying
#if DEBUG
                            , tree
#endif
                            );
#if DEBUG
                        // genProduceReg must have spilled a field marked for spill before this point.
                        var fieldNeedsSpill = ((lclVarTree.Flags & GTF_SPILL) != 0) &&
                            ((lclVarTree.AsLclVar().GetRegSpillFlagByIdx(i) & GTF_SPILL) != 0);
                        assert(!fieldNeedsSpill);
#endif
                    }

                    if (isInMemory &&
                        VarSetOps.IsMember(_compiler, _compiler.codeGen.GCInfo.gcTrkStkPtrLcls, fldVarDsc._varIndex))
                    {
                        UpdateLifeBit(_compiler.codeGen.GCInfo.gcVarPtrSetCur, in fldVarDsc, isBorn, isDying);
                    }

                    if (isDying == previouslyLive)
                    {
                        _compiler.codeGen.getVariableLiveKeeper().siStartOrCloseVariableLiveRange(
                            in fldVarDsc, fldLclNum, !isDying, isDying);
                    }
                }
            }
        }

        DumpLifeDelta(tree);
    }

    public void UpdateLife(GenTree tree, bool generalLclAddrHandling = false)
    {
#if DEBUG
        assert(_compiler.CurLVEpoch == _epoch);
#endif
        if (tree == _compiler.compCurLifeTree)
        {
            return;
        }

        if (tree.Oper.IsNonPhiLocal)
        {
            UpdateLifeVar(tree, tree.AsLclVarCommon());
        }
        else if (!generalLclAddrHandling && tree.Oper.IsIndir && (tree.IndirOrArrMetaDataAddr.Oper == GT_LCL_ADDR))
        {
            UpdateLifeVar(tree, tree.IndirOrArrMetaDataAddr.AsLclVarCommon());
        }
        else if (tree.Oper == GT_CALL)
        {
            _ = tree.VisitPhysicalLocalDefNodes(_compiler, local =>
            {
                UpdateLifeVar(tree, local.AsLclVarCommon());
                return GenTree.VisitResult.Continue;
            });
        }
        else if (generalLclAddrHandling && (tree.Oper == GT_LCL_ADDR))
        {
            UpdateLifeVar(tree, tree.AsLclVarCommon());
        }
    }

    private void UpdateLifeBit(Span<nint> set, in LclVarDsc dsc, bool isBorn, bool isDying)
    {
        if (isDying)
        {
            VarSetOps.RemoveElemD(_compiler, set, dsc._varIndex);
        }
        else if (isBorn)
        {
            VarSetOps.AddElemD(_compiler, set, dsc._varIndex);
        }
    }

    private void StoreCurrentLifeForDump()
    {
#if DEBUG
        if (_compiler.verbose)
        {
            VarSetOps.Assign(_compiler, ref _oldLife, _compiler.compCurLife);

            if (_forCodeGen)
            {
                assert(_compiler.codeGen is not null);
                VarSetOps.Assign(_compiler, ref _oldStackPtrsLife, _compiler.codeGen.GCInfo.gcVarPtrSetCur);
            }
        }
#endif
    }

    private void DumpLifeDelta(GenTree tree)
    {
#if DEBUG
        if (_compiler.verbose && !VarSetOps.Equal(_compiler, _oldLife, _compiler.compCurLife))
        {
            jitprintf($"\t\t\t\t\t\t\tLive vars after [{tree.TreeId:D6}]: ");
            dumpConvertedVarSet(_compiler, _oldLife);
            var deadSet = VarSetOps.Diff(_compiler, _oldLife, _compiler.compCurLife);
            var bornSet = VarSetOps.Diff(_compiler, _compiler.compCurLife, _oldLife);

            if (!VarSetOps.IsEmpty(_compiler, deadSet))
            {
                jitprintf(" -");
                dumpConvertedVarSet(_compiler, deadSet);
            }

            if (!VarSetOps.IsEmpty(_compiler, bornSet))
            {
                jitprintf(" +");
                dumpConvertedVarSet(_compiler, bornSet);
            }

            jitprintf(" => ");
            dumpConvertedVarSet(_compiler, _compiler.compCurLife);
            jitprintf("\n");
        }

        if (_forCodeGen && _compiler.verbose)
        {
            assert(_compiler.codeGen is not null);

            if (!VarSetOps.Equal(_compiler, _oldStackPtrsLife, _compiler.codeGen.GCInfo.gcVarPtrSetCur))
            {
                jitprintf($"\t\t\t\t\t\t\tGC vars after [{tree.TreeId:D6}]: ");
                dumpConvertedVarSet(_compiler, _oldStackPtrsLife);
                jitprintf(" => ");
                dumpConvertedVarSet(_compiler, _compiler.codeGen.GCInfo.gcVarPtrSetCur);
                jitprintf("\n");
            }
        }
#endif
    }
}
