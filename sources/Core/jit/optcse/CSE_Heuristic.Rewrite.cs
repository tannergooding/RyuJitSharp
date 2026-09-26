// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;
#if DEBUG
using static RyuJitSharp.GenTreeDebugFlags;
#endif

namespace RyuJitSharp;

public abstract partial class CSE_HeuristicCommon
{
    public virtual void PerformCSE(CSE_Candidate successfulCandidate)
    {
        AdjustHeuristic(successfulCandidate);
        var descriptor = successfulCandidate.CseDsc();

#if DEBUG
        var category = successfulCandidate.IsAggressive() ? ": aggressive"
            : successfulCandidate.IsModerate() ? ": moderate"
            : successfulCandidate.IsConservative() ? ": conservative"
            : successfulCandidate.IsStressCSE() ? ": stress"
            : successfulCandidate.IsRandom() ? ": random" : "";
        m_sequence.Add(unchecked((uint)descriptor.csdIndex));
#endif

        var cseLclVarNum = m_compiler.lvaGrabTemp(false,
#if DEBUG
            $"CSE #{descriptor.csdIndex}{category}"
#else
            $"CSE #{descriptor.csdIndex}"
#endif
        );
        var cseLclVarTyp = successfulCandidate.Expr().Type.ActualType;
        if (cseLclVarTyp is TYP_STRUCT)
        {
            m_compiler.lvaSetStruct(cseLclVarNum, successfulCandidate.Expr().GetLayout(m_compiler),
                unsafeValueClsCheck: false);
        }

        ref var local = ref m_compiler.lvaGetDesc(cseLclVarNum);
        local.Type = cseLclVarTyp;
        local.lvIsCSE = true;
        m_addCSEcount++;
        m_compiler.RecordCsePromotion();

#if DEBUG
        local.lvIsMultiDefCSE = descriptor.csdDefCount > 1;
#endif

        var setRefCnt = true;
        var isSharedConst = successfulCandidate.IsSharedConst();
        var bestVN = ValueNumStore.NoVN;
        var bestIsDef = false;
        nint bestConstValue = 0;
#if DEBUG
        var allSame = true;
#endif

        for (var occurrence = descriptor.csdTreeList; occurrence is not null; occurrence = occurrence.tslNext)
        {
            var expression = occurrence.tslTree;
            if (!IS_CSE_INDEX(expression._cseNum))
            {
                continue;
            }

            var store = m_compiler.vnStore;
            assert(store is not null);
            var currentVN = store.VNNormalValue(expression._vnPair.Liberal);
            assert(currentVN != ValueNumStore.NoVN);
            var currentConstValue = isSharedConst ? store.CoercedConstantValue<nint>(currentVN) : 0;
            var isDef = IS_CSE_DEF(expression._cseNum);

            if (bestVN == ValueNumStore.NoVN)
            {
                bestVN = currentVN;
                if (isSharedConst)
                {
                    bestConstValue = currentConstValue;
                    bestIsDef = isDef;
                }
            }
            else if (currentVN != bestVN)
            {
                assert(isSharedConst);
#if DEBUG
                allSame = false;
#endif
                var difference = unchecked(currentConstValue - bestConstValue);
                // ARM addressing permits a subtraction of up to 255; retain the native base choice.
                if ((bestIsDef && (difference < -255)) || (!bestIsDef && (difference < 0)))
                {
                    bestVN = currentVN;
                    bestConstValue = currentConstValue;
                    bestIsDef = isDef;
                }
            }

            var weight = occurrence.tslBlock.getBBWeight(m_compiler);
            if (setRefCnt)
            {
                local.setLvRefCnt(1);
                local.setLvRefCntWtd(weight);
                setRefCnt = false;
            }
            else
            {
                local.incRefCnts(weight, m_compiler);
            }

            if (isDef)
            {
                local.incRefCnts(weight, m_compiler);
#if DEBUG
                local.lvIsHoist |= (expression.Flags & GTF_MAKE_CSE) != 0;
#endif
            }
        }

        descriptor.csdConstDefValue = bestConstValue;
        descriptor.csdConstDefVN = bestVN;

#if DEBUG
        if (!allSame && m_compiler.verbose)
        {
            JITDUMP($"\nWe have shared Const CSE's and selected ${bestVN:x} " +
                $"with a value of 0x{bestConstValue:x} as the base.\n");
        }
#endif

        var ssaBuilder = new IncrementalSsaBuilder(m_compiler, cseLclVarNum);
        var defUses = new List<UseDefLocation>();
        var vnStore = m_compiler.vnStore;
        assert(vnStore is not null);

        for (var occurrence = descriptor.csdTreeList; occurrence is not null; occurrence = occurrence.tslNext)
        {
            var expression = occurrence.tslTree;
            if (!IS_CSE_DEF(expression._cseNum))
            {
                continue;
            }

            var statement = occurrence.tslStmt;
            var block = occurrence.tslBlock;
#if DEBUG
            if (m_compiler.verbose)
            {
                Globals.jitprintf($"\n{FMT_CSE(GET_CSE_INDEX(expression._cseNum))} def at " +
                    $"[{expression.TreeId:D6}] replaced in {FMT_BB(block.bbNum)} with def of V{cseLclVarNum:D2}\n");
            }
#endif
            var value = expression;
            if (isSharedConst)
            {
                var currentVN = vnStore.VNNormalValue(expression._vnPair.Liberal);
                var currentValue = vnStore.CoercedConstantValue<nint>(currentVN);
                var delta = unchecked(currentValue - descriptor.csdConstDefValue);
                if (delta != 0)
                {
                    value = m_compiler.gtNewIconNode(cseLclVarTyp, descriptor.csdConstDefValue);
                    value._vnPair.SetBoth(descriptor.csdConstDefVN);
                }
            }

            var originalStore = m_compiler.gtNewTempStore(cseLclVarNum, value);
            var storeNode = originalStore.Oper is GT_STORE_LCL_VAR
                ? originalStore
                : originalStore.EffectiveVal;
            if (originalStore.Oper is not GT_STORE_LCL_VAR)
            {
                noway_assert((originalStore.Oper is GT_COMMA) && ReferenceEquals(originalStore, value));
            }
            else
            {
                noway_assert(ReferenceEquals(storeNode.AsLclVar().Data, value));
            }

            var valueExceptions = vnStore.VNPExceptionSet(value._vnPair);
            storeNode._vnPair = vnStore.VNPWithExc(ValueNumStore.VNPForVoid(), valueExceptions);
            noway_assert(storeNode.Oper is GT_STORE_LCL_VAR);
            // Preserve the completed def on the store for optCSE_canSwap and incremental SSA.
            storeNode._cseNum = expression._cseNum;
            expression._cseNum = NO_CSE;

            var cseLocal = m_compiler.gtNewLclvNode(cseLclVarTyp, cseLclVarNum);
            cseLocal._vnPair = vnStore.VNPNormalPair(value._vnPair);
            GenTree cseUse = cseLocal;
            if (isSharedConst)
            {
                var currentVN = vnStore.VNNormalValue(expression._vnPair.Liberal);
                var currentValue = vnStore.CoercedConstantValue<nint>(currentVN);
                var delta = unchecked(currentValue - descriptor.csdConstDefValue);
                if (delta != 0)
                {
                    var deltaNode = m_compiler.gtNewIconNode(cseLclVarTyp, delta);
                    cseUse = m_compiler.gtNewBinaryNode(GT_ADD, cseLclVarTyp, cseLocal, deltaNode);
                    cseUse.CanCse = false;
                    cseUse._vnPair.SetBoth(currentVN);
                }
            }

            var replacement = m_compiler.gtNewCommaNode(expression.Type.ActualType, originalStore, cseUse);
            var storeExceptions = vnStore.VNPExceptionSet(originalStore._vnPair);
            replacement._vnPair = vnStore.VNPWithExc(cseUse._vnPair, storeExceptions);

            ReplaceCSENode(statement, expression, replacement);
            ssaBuilder.InsertDef(new UseDefLocation(block, statement, storeNode.AsLclVar()));
            defUses.Add(new UseDefLocation(block, statement, cseLocal));
        }

        var insertIntoSsa = ssaBuilder.FinalizeDefs();
        if (insertIntoSsa)
        {
            JITDUMP("Inserting each use created for defs into SSA\n");
            foreach (var defUse in defUses)
            {
                InsertUseIntoSsa(ssaBuilder, defUse);
            }
        }

        for (var occurrence = descriptor.csdTreeList; occurrence is not null; occurrence = occurrence.tslNext)
        {
            var expression = occurrence.tslTree;
            if (!IS_CSE_USE(expression._cseNum))
            {
                continue;
            }

            var statement = occurrence.tslStmt;
            var block = occurrence.tslBlock;
            m_compiler.SetCseWeight(block.getBBWeight(m_compiler));

#if DEBUG
            if (m_compiler.verbose)
            {
                Globals.jitprintf($"\nWorking on the replacement of the " +
                    $"{FMT_CSE(expression._cseNum)} use at [{expression.TreeId:D6}] " +
                    $"in {FMT_BB(block.bbNum)}\n");
            }
#endif
            var cseLocal = m_compiler.gtNewLclvNode(cseLclVarTyp, cseLclVarNum);
            GenTree replacement = cseLocal;
            if (isSharedConst)
            {
                cseLocal._vnPair.SetBoth(descriptor.csdConstDefVN);
                var currentVN = vnStore.VNNormalValue(expression._vnPair.Liberal);
                var currentValue = vnStore.CoercedConstantValue<nint>(currentVN);
                var delta = unchecked(currentValue - descriptor.csdConstDefValue);
                if (delta != 0)
                {
                    var deltaNode = m_compiler.gtNewIconNode(cseLclVarTyp, delta);
                    replacement = m_compiler.gtNewBinaryNode(GT_ADD, cseLclVarTyp, replacement, deltaNode);
                    replacement.CanCse = false;
                    replacement._vnPair.SetBoth(currentVN);
                }
            }
            else
            {
                replacement._vnPair = vnStore.VNPNormalPair(expression._vnPair);
            }

#if DEBUG
            replacement._debugFlags |= GTF_DEBUG_VAR_CSE_REF;
#endif

            expression._cseNum = NO_CSE;
            var sideEffects = m_compiler.ExtractCseSideEffects(expression);
            if (sideEffects is not null)
            {
#if DEBUG
                if (m_compiler.verbose)
                {
                    Globals.jitprintf("\nThis CSE use has side effects and/or nested CSE defs. The sideEffectList:\n");
                    m_compiler.gtDispTree(sideEffects);
                    Globals.jitprintf("\n");
                }
#endif
                var sideEffectExceptions = vnStore.VNPExceptionSet(sideEffects._vnPair);
                var combinedVN = vnStore.VNPWithExc(replacement._vnPair, sideEffectExceptions);
                replacement = m_compiler.gtNewCommaNode(expression.Type.ActualType, sideEffects, replacement);
                replacement._vnPair = combinedVN;
            }

            ReplaceCSENode(statement, expression, replacement);

            if (insertIntoSsa)
            {
                var oldVNPair = cseLocal._vnPair;
                InsertUseIntoSsa(ssaBuilder, new UseDefLocation(block, statement, cseLocal));
                if ((sideEffects is not null) && (cseLocal._vnPair != oldVNPair))
                {
                    assert(!isSharedConst && ReferenceEquals(replacement.EffectiveVal, cseLocal));
                    var sideEffectExceptions = vnStore.VNPExceptionSet(sideEffects._vnPair);
                    replacement._vnPair = vnStore.VNPWithExc(cseLocal._vnPair, sideEffectExceptions);
                }
            }
        }
    }

    private void ReplaceCSENode(Statement statement, GenTree expression, GenTree replacement)
    {
        replacement.CopyReg(expression);
        expression.ClearRegNum();

        var link = m_compiler.gtFindLink(statement, expression);
#if DEBUG
        if (Unsafe.IsNullRef(ref link.result))
        {
            m_compiler.gtDispStmt(statement);
            m_compiler.gtDispTree(expression);
        }
#endif
        noway_assert(!Unsafe.IsNullRef(ref link.result));
        link.result = replacement;

        m_compiler.gtSetStmtInfo(statement);
        m_compiler.fgSetStmtSeq(statement);
        m_compiler.gtUpdateStmtSideEffects(statement);
    }

    private void InsertUseIntoSsa(IncrementalSsaBuilder ssaBuilder, UseDefLocation use)
    {
        ssaBuilder.InsertUse(use);

        var local = use.Tree ?? throw new InvalidOperationException("A CSE SSA use needs a local tree.");
        assert(local.HasSsaName);

        ref var localDescriptor = ref m_compiler.lvaGetDesc(local.LclNum);
        ref var ssaDescriptor = ref localDescriptor.GetPerSsaData(local.SsaNum);
        var oldConservativeVN = local._vnPair.Conservative;
        local._vnPair = ssaDescriptor._vnPair;

        var vnStore = m_compiler.vnStore;
        assert(vnStore is not null);
        if ((oldConservativeVN != ssaDescriptor._vnPair.Conservative) &&
            vnStore.IsVNCheckedBound(oldConservativeVN) &&
            !vnStore.IsVNConstant(ssaDescriptor._vnPair.Conservative))
        {
            vnStore.SetVNIsCheckedBound(ssaDescriptor._vnPair.Conservative);
        }
    }
}
