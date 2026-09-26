// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;

namespace RyuJitSharp;

public sealed partial class ObjectAllocator
{
    private int NewPseudoIndex()
    {
        if (_numPseudos >= _maxPseudos)
        {
            assert(false, "unexpected number of pseudos");
            return BAD_VAR_NUM;
        }

        var result = _firstPseudoIndex + _numPseudos;
        _numPseudos++;
        return result;
    }

    private bool IsGuarded(BasicBlock block, GenTree tree, GuardInfo info, bool testOutcome)
    {
        var compiler = CompilerInstance;
        var domTree = compiler._domTree;
        assert(domTree is not null);

        JITDUMP($"Checking if [{TreeIdForDump(tree):D6}] in {FMT_BB(block.bbNum)} executes under a " +
            $"{(testOutcome ? "successful" : "failing")} GDV type test\n");

        for (var idomBlock = block.bbIDom; idomBlock is not null; idomBlock = idomBlock.bbIDom)
        {
            JITDUMP($"... examining dominator {FMT_BB(idomBlock.bbNum)}\n");
            if (idomBlock.Kind is not BBJ_COND)
            {
                JITDUMP("... not BBJ_COND\n");
                continue;
            }

            var trueSuccessorDominates = domTree.Dominates(idomBlock.TrueTarget, block);
            var falseSuccessorDominates = domTree.Dominates(idomBlock.FalseTarget, block);

            if (trueSuccessorDominates && falseSuccessorDominates)
            {
                JITDUMP("... both successors dominate?\n");
                continue;
            }

            if (!trueSuccessorDominates && !falseSuccessorDominates)
            {
                JITDUMP("... neither successor dominates\n");
                continue;
            }

            var guardingRelop = IsGuard(idomBlock, info);
            if (guardingRelop is null)
            {
                continue;
            }

            if (testOutcome)
            {
                var isReachableOnSuccess = (trueSuccessorDominates && (guardingRelop.Oper is GT_EQ)) ||
                    (falseSuccessorDominates && (guardingRelop.Oper is GT_NE));
                if (isReachableOnSuccess)
                {
                    info.Block = idomBlock;
                    return true;
                }

                JITDUMP("... guarded by failing GDV\n");
            }
            else
            {
                var isReachableOnFailure = (trueSuccessorDominates && (guardingRelop.Oper is GT_NE)) ||
                    (falseSuccessorDominates && (guardingRelop.Oper is GT_EQ));
                if (isReachableOnFailure)
                {
                    info.Block = idomBlock;
                    return true;
                }

                JITDUMP("... guarded by successful GDV\n");
            }
        }

        JITDUMP("... no more doms\n");
        return false;
    }

    private unsafe bool CheckForGuardedUse(BasicBlock block, GenTree tree, int lclNum)
    {
        if (!_enumeratorLocalToPseudoIndexMap.TryGetValue(lclNum, out var pseudoIndex))
        {
            JITDUMP("... no pseudo?\n");
            return false;
        }

        var info = new GuardInfo();
        if (!IsGuarded(block, tree, info, testOutcome: false))
        {
            JITDUMP("... not guarded?\n");
            return false;
        }

        if (!_cloneMap.TryGetValue(pseudoIndex, out var pseudoGuardInfo))
        {
            JITDUMP("... under non-gdv guard?\n");
            return false;
        }

        if ((info.Local == lclNum) && (pseudoGuardInfo.Local == lclNum) && (info.Type == pseudoGuardInfo.Type))
        {
            JITDUMP("... under GDV; tracking via pseudo index");
#if DEBUG
            if (CompilerInstance.verbose)
            {
                DumpIndex(pseudoIndex);
            }
#endif
            JITDUMP("\n");
            AddConnGraphEdgeIndex(pseudoIndex, LocalToIndex(lclNum));
            return true;
        }

        JITDUMP("... under different guard?\n");
        return false;
    }

    private unsafe void CheckForGuardedAllocationOrCopy(BasicBlock block, Statement stmt,
        ref GenTree use, GenTree? user, int lclNum)
    {
        var compiler = CompilerInstance;
        var tree = use;
        assert(tree.Oper.IsLocalStore);

        if (!CanHavePseudos())
        {
            return;
        }

        assert(compiler._domTree is not null);
        var data = tree.AsLclVarCommon().Data;

        if (data.Oper is GT_ALLOCOBJ)
        {
            if (compiler.ImpEnumeratorGdvLocalMap.TryGetValue(data, out var enumeratorLocal))
            {
                var clsHnd = data.AsAllocObj().ClsHnd;
                var allocType = AllocationKind(data);
                var size = 0;
                if (CanAllocateLclVarOnStack(enumeratorLocal, clsHnd, allocType, TARGET_POINTER_SIZE,
                    ref size, out _, preliminaryCheck: true))
                {
                    var pseudoIndex = NewPseudoIndex();
                    assert(pseudoIndex != BAD_VAR_NUM);
                    var added = !_enumeratorLocalToPseudoIndexMap.ContainsKey(enumeratorLocal);
                    _enumeratorLocalToPseudoIndexMap[enumeratorLocal] = pseudoIndex;

                    if (!added)
                    {
                        JITDUMP("Looks like enumerator var re-use (multiple defining GDVs)\n");
                    }

                    var info = new CloneInfo {
                        Local = enumeratorLocal,
                        Type = clsHnd,
                        PseudoIndex = pseudoIndex,
                        AppearanceMap = [],
                        AllocBlock = block,
                        AllocStmt = stmt,
                        AllocTree = data
                    };
                    _cloneMap[pseudoIndex] = info;

                    JITDUMP($"Enumerator allocation [{TreeIdForDump(data):D6}]: will track accesses to " +
                        $"V{enumeratorLocal:D2} guarded by type {compiler.eeGetClassName(clsHnd)} via");
#if DEBUG
                    if (compiler.verbose)
                    {
                        DumpIndex(pseudoIndex);
                    }
#endif
                    JITDUMP("\n");

                    if (lclNum != enumeratorLocal)
                    {
                        CheckForEnumeratorUse(enumeratorLocal, lclNum);
                        RecordAppearance(lclNum, block, stmt, ref use, user);
                    }
                }
                else
                {
                    JITDUMP($"Enumerator allocation [{TreeIdForDump(data):D6}]: enumerator type " +
                        $"{compiler.eeGetClassName(clsHnd)} cannot be stack allocated, " +
                        $"so not tracking enumerator local V{enumeratorLocal:D2}\n");
                }
            }
            else
            {
                JITDUMP($"Allocation [{TreeIdForDump(data):D6}] was not flagged for conditional escape tracking\n");
            }
        }
        else if (data.Oper is GT_LCL_VAR or GT_BOX)
        {
            var srcLclNum = (data.Oper is GT_BOX)
                ? data.AsBox().BoxOp.AsLclVarCommon().LclNum : data.AsLclVarCommon().LclNum;

            if (CheckForEnumeratorUse(srcLclNum, lclNum))
            {
                RecordAppearance(lclNum, block, stmt, ref use, user);
            }
        }
        else if (!data.IsIntegralConst(0) && _enumeratorLocalToPseudoIndexMap.ContainsKey(lclNum))
        {
            RecordAppearance(lclNum, block, stmt, ref use, user);
        }
    }

    private bool CheckForEnumeratorUse(int lclNum, int dstLclNum)
    {
        if (_enumeratorLocalToPseudoIndexMap.ContainsKey(dstLclNum))
        {
            return true;
        }

        if (!_enumeratorLocalToPseudoIndexMap.TryGetValue(lclNum, out var pseudoIndex))
        {
            return false;
        }

        if (!_cloneMap.TryGetValue(pseudoIndex, out var info))
        {
            return false;
        }

        var added = _enumeratorLocalToPseudoIndexMap.TryAdd(dstLclNum, pseudoIndex);
        assert(added);

        JITDUMP($"Enumerator allocation: will also track accesses to V{dstLclNum:D2} via");
#if DEBUG
        if (CompilerInstance.verbose)
        {
            DumpIndex(pseudoIndex);
        }
#endif
        JITDUMP("\n");

        info.AllocTemps ??= [];
        info.AllocTemps.Add(dstLclNum);
        return true;
    }

    private void RecordAppearance(int lclNum, BasicBlock block, Statement stmt, ref GenTree use, GenTree? user)
    {
        if (!_enumeratorLocalToPseudoIndexMap.TryGetValue(lclNum, out var pseudoIndex) ||
            !_cloneMap.TryGetValue(pseudoIndex, out var info))
        {
            return;
        }

        var tree = use;
        var isDef = tree.Oper.IsLocalStore;
        JITDUMP($"Found enumerator V{lclNum:D2} {(isDef ? "def" : "use")} at [{TreeIdForDump(tree):D6}]\n");

        var map = info.AppearanceMap;
        assert(map is not null);

        if (!map.TryGetValue(lclNum, out var variable))
        {
            variable = new EnumeratorVar { Appearances = [] };
            map[lclNum] = variable;
        }

        var appearance = new EnumeratorVarAppearance(block, stmt, lclNum, isDef, GenTreeUse.FromUse(ref use, user));
        if (isDef)
        {
            if (variable.Def is not null)
            {
                if (!variable.HasMultipleDefs)
                {
                    JITDUMP($"Enumerator V{lclNum:D2} has multiple defs\n");
                    variable.HasMultipleDefs = true;
                }
            }
            else
            {
                variable.Def = appearance;
            }

            if (stmt == info.AllocStmt)
            {
                variable.IsInitialAllocTemp = true;
            }
        }

        assert(variable.Appearances is not null);
        variable.Appearances.Add(appearance);
        info.AppearanceCount++;
    }
}
