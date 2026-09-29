// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

// Based on the RyuJIT compiler from dotnet/runtime, optimizer.cpp.

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Compiler
{
#if DEBUG
    private static ConfigMethodRange s_jitEnableVNBasedDeadStoreRemovalRange;
#endif

    public unsafe PhaseStatus optVNBasedDeadStoreRemoval()
    {
#if DEBUG
        s_jitEnableVNBasedDeadStoreRemovalRange.EnsureInit(JitConfig.JitEnableVNBasedDeadStoreRemovalRange);
        if (!s_jitEnableVNBasedDeadStoreRemovalRange.Contains(info.compMethodHash()))
        {
            JITDUMP("VN-based dead store removal disabled by JitEnableVNBasedDeadStoreRemovalRange\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }
#endif

        var madeChanges = false;

        for (var lclNum = 0; lclNum < lvaCount; lclNum++)
        {
            ref var varDsc = ref lvaGetDesc(lclNum);
            if (!varDsc.lvInSsa)
            {
                continue;
            }

            var defCount = varDsc.lvPerSsaData.Count;
            if (defCount <= 1)
            {
                continue;
            }

            if (compIsAsync && ((varDsc.Type is TYP_BYREF) ||
                ((varDsc.Type is TYP_STRUCT) &&
                    (varDsc.Layout ?? throw new InvalidOperationException("Struct local has no layout.")).HasGCByRef())))
            {
                // A byref store may not be dead if it crosses a suspension point.
                continue;
            }

            for (var defIndex = 1; defIndex < defCount; defIndex++)
            {
                ref var defDsc = ref varDsc.lvPerSsaData.GetSsaDefByIndex(defIndex);
                var store = defDsc.DefNode;
                if (store is null)
                {
                    continue;
                }

                assert(store.Oper.IsLocalStore && defDsc._vnPair.BothDefined());

#if DEBUG
                JITDUMP($"Considering [{store.TreeId:D6}] for removal...\n");
#endif

                if (store.LclNum != lclNum)
                {
                    JITDUMP(" -- no; composite definition\n");
                    continue;
                }

                ValueNum oldStoreValue;
                if ((store.Flags & GTF_VAR_USEASG) == 0)
                {
                    ref var lastDefDsc = ref varDsc.lvPerSsaData.GetSsaDefByIndex(defIndex - 1);
                    if (lastDefDsc.Block != defDsc.Block)
                    {
                        JITDUMP(" -- no; last def not in the same block\n");
                        continue;
                    }

                    if ((store.Flags & GTF_VAR_EXPLICIT_INIT) != 0)
                    {
                        // Removing explicit inits is not profitable for primitives and not safe for structs.
                        JITDUMP(" -- no; 'explicit init'\n");
                        continue;
                    }

                    // Avoid making enregisterable locals must-init and extending live ranges.
                    // The first SSA def is assumed to be the implicit live-in one, as in native.
                    if ((defIndex == 1) && (varDsc.Type is not TYP_STRUCT))
                    {
                        JITDUMP(" -- no; first explicit def of a non-STRUCT local\n");
                        continue;
                    }

                    oldStoreValue = lastDefDsc._vnPair.Conservative;
                }
                else
                {
                    assert(vnStore is not null);
                    var oldLclValue = varDsc.GetPerSsaData(defDsc.UseDefSsaNum)._vnPair.Conservative;
                    oldStoreValue = vnStore.VNForLoad(VNK_Conservative, oldLclValue, lvaLclValueSize(lclNum),
                        store.Type, store.LclOffs, store.AsLclFld().ValueSize);
                }

                assert(vnStore is not null);
                var data = store.Data;
                var storeValue = (store.Type is TYP_STRUCT) && data.IsIntegralConst(0)
                    ? vnStore.VNForZeroObj(store.GetLayout(this) ??
                        throw new InvalidOperationException("Struct store has no layout."))
                    : data._vnPair.Conservative;

                if (oldStoreValue != storeValue)
                {
                    JITDUMP(" -- no; not redundant\n");
                    continue;
                }

                JITDUMP("Removed dead store:\n");
                DISPTREE(store);

                var block = defDsc.Block ?? throw new InvalidOperationException("An SSA store has no defining block.");
                optVNReplaceDeadStore(block, store);
                // Native retags the def node as GT_COMMA; the managed descriptor cannot refer to that node type.
                // This phase invalidates SSA, so clear the detached store while retaining its VN descriptor.
                defDsc.DefNode = null;
                madeChanges = true;
            }
        }

        return madeChanges ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }

    private void optVNReplaceDeadStore(BasicBlock block, GenTreeLclVarCommon store)
    {
        var data = store.Data;
        var replacement = new GenTreeOp(GT_COMMA, TYP_VOID, data, gtNewNothingNode(), store, fgNodeThreading);
        replacement.SetAllEffectsFlags(data);

        if (optVNTryReplaceDeadStore(block, store, replacement))
        {
            return;
        }

        for (var candidate = fgFirstBB; candidate is not null; candidate = candidate.Next)
        {
            if ((candidate != block) && optVNTryReplaceDeadStore(candidate, store, replacement))
            {
                return;
            }
        }

        // Later phases can remove statements without clearing SSA definitions.
        // Native retags the detached node; only its SSA reference needs clearing here.
        return;
    }

    private bool optVNTryReplaceDeadStore(BasicBlock block, GenTreeLclVarCommon store, GenTreeOp replacement)
    {
        for (var statement = block.FirstStmt; statement is not null; statement = statement.NextStmt)
        {
            var link = gtFindLink(statement, store);
            if (Unsafe.IsNullRef(in link.result))
            {
                continue;
            }

            if (link.parent is GenTree parent)
            {
                parent.ReplaceOperand(ref link.result, replacement);
            }
            else
            {
                statement.RootNode = replacement;
            }

            if (fgNodeThreading is NodeThreading.AllTrees)
            {
                fgSetStmtSeq(statement);
            }

            gtUpdateSideEffects(statement, replacement);
            return true;
        }

        return false;
    }
}
