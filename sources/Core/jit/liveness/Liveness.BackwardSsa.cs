// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Liveness<TLiveness>
    where TLiveness : ILivenessPolicy
{
    private bool ComputeLifeSsa(nint[] life, nint[] keepAliveVars
#if DEBUG
        , ref bool treeModified
#endif
    )
    {
        var statement = _compiler.compCurStmt;
        assert(statement is not null);
        noway_assert(VarSetOps.IsSubset(_compiler, keepAliveVars, life));
        var statementInfoDirty = false;

        for (var tree = statement.RootNode; tree is not null; tree = tree.Prev)
        {
            bool repeat;
            do
            {
                assert(tree.Oper is not GT_QMARK);
                repeat = false;
                var isUse = false;
                var storeRemoved = false;
                var localNumber = 0;
                if (tree is GenTreeCall call)
                {
                    var partialDef = ComputeLifeCall(life, keepAliveVars, call);
                    if (partialDef is not null)
                    {
                        assert((partialDef.Flags & GTF_VAR_USEASG) != 0);
                        isUse = true;
                        localNumber = partialDef.LclNum;
                    }
                }
                else if (tree.Oper.IsNonPhiLocal)
                {
                    isUse = (tree.Flags & GTF_VAR_USEASG) != 0;
                    if (ComputeLifeLocal(life, keepAliveVars, tree))
                    {
                        var local = tree.AsLclVarCommon();
                        localNumber = local.LclNum;
                        if (RemoveDeadStoreSsa(ref tree, life, ref repeat, ref statementInfoDirty,
                            out storeRemoved
#if DEBUG
                            , ref treeModified
#endif
                        ))
                        {
                            return statementInfoDirty;
                        }
                    }
                    else
                    {
                        isUse = false;
                    }
                }

                if (isUse && !storeRemoved)
                {
                    ref var descriptor = ref _compiler.lvaGetDesc(localNumber);
                    if (descriptor.lvTracked)
                    {
                        VarSetOps.AddElemD(_compiler, life, descriptor._varIndex);
                    }
                    if (descriptor.lvPromoted)
                    {
                        for (var field = descriptor.lvFieldLclStart;
                            field < descriptor.lvFieldLclStart + descriptor.lvFieldCnt; field++)
                        {
                            ref var fieldDescriptor = ref _compiler.lvaGetDesc(field);
                            if (fieldDescriptor.lvTracked)
                            {
                                VarSetOps.AddElemD(_compiler, life, fieldDescriptor._varIndex);
                            }
                        }
                    }
                }
            }
            while (repeat);
        }

        return statementInfoDirty;
    }

    private bool RemoveDeadStoreSsa(ref GenTree tree, nint[] life, ref bool repeat,
        ref bool statementInfoDirty, out bool storeRemoved
#if DEBUG
        , ref bool treeModified
#endif
    )
    {
        ref var descriptor = ref _compiler.lvaGetDesc(tree.AsLclVarCommon().LclNum);
        noway_assert(!descriptor.IsAddressExposed);
        if (!tree.Oper.IsLocalStore)
        {
            storeRemoved = false;
            return false;
        }

        storeRemoved = true;
        var store = tree.AsLclVarCommon();
        GenTree? sideEffects = null;
        var value = store.Data;
        if ((value.Flags & GTF_SIDE_EFFECT) != 0)
        {
#if DEBUG
            if (_compiler.verbose)
            {
                assert(_compiler.compCurBB is not null);
                jitprintf($"{FMT_BB(_compiler.compCurBB.bbNum)} - Dead store has side effects...\n");
                _compiler.gtDispTree(store);
                jitprintf("\n");
            }
#endif
            _compiler.gtExtractSideEffList(value, ref sideEffects);
        }

        var statement = _compiler.compCurStmt;
        assert(statement is not null);
        if (tree.Next is null)
        {
            noway_assert(statement.RootNode == store);
            JITDUMP("top level store\n");
            if (sideEffects is null)
            {
                JITDUMP("removing stmt with no side effects\n");
                assert(_compiler.compCurBB is not null);
                _compiler.fgRemoveStmt(_compiler.compCurBB, statement);
                return true;
            }

            noway_assert((sideEffects.Flags & GTF_SIDE_EFFECT) != 0);
#if DEBUG
            if (_compiler.verbose)
            {
                jitprintf("Extracted side effects list...\n");
                _compiler.gtDispTree(sideEffects);
                jitprintf("\n");
            }
#endif
            statement.RootNode = sideEffects;
            tree = sideEffects;
#if DEBUG
            treeModified = true;
#endif
            _compiler.gtSetStmtInfo(statement);
            _compiler.fgSetStmtSeq(statement);
            statementInfoDirty = false;
            repeat = true;
            return false;
        }

        if (descriptor.lvTracked)
        {
            noway_assert(!VarSetOps.IsMember(_compiler, life, descriptor._varIndex));
        }
        else
        {
            for (var field = descriptor.lvFieldLclStart;
                field < descriptor.lvFieldLclStart + descriptor.lvFieldCnt; field++)
            {
                ref var fieldDescriptor = ref _compiler.lvaGetDesc(field);
                noway_assert(fieldDescriptor.lvTracked &&
                    !VarSetOps.IsMember(_compiler, life, fieldDescriptor._varIndex));
            }
        }

        GenTree replacement;
        if (sideEffects is null)
        {
#if DEBUG
            if (_compiler.verbose)
            {
                assert(_compiler.compCurBB is not null);
                jitprintf("\nRemoving tree ");
                Compiler.printTreeId(store);
                jitprintf($" in {FMT_BB(_compiler.compCurBB.bbNum)} as useless\n");
                _compiler.gtDispTree(store);
                jitprintf("\n");
            }
#endif
            store.BashToNOP();
            replacement = store;
        }
        else
        {
            noway_assert((sideEffects.Flags & GTF_SIDE_EFFECT) != 0);
#if DEBUG
            if (_compiler.verbose)
            {
                jitprintf("Extracted side effects list from condition...\n");
                _compiler.gtDispTree(sideEffects);
                jitprintf("\n");
            }
#endif
            replacement = sideEffects.Oper is GT_COMMA
                ? new GenTreeOp(GT_COMMA, TYP_VOID, sideEffects.AsOp().Op1, sideEffects.AsOp().Op2,
                    store, NodeThreading.AllTrees)
                : new GenTreeOp(GT_COMMA, TYP_VOID, sideEffects, _compiler.gtNewNothingNode(),
                    store, NodeThreading.AllTrees);
            // ChangeOper retains flags, clears the old VN, then replaces only effect flags.
            replacement.Flags = store.Flags;
            replacement.SetOper(GT_COMMA);
            replacement.SetAllEffectsFlags(sideEffects);

            var link = _compiler.gtFindLink(statement, store);
            noway_assert(!Unsafe.IsNullRef(ref link.result));
            do
            {
                noway_assert(link.parent is not null);
                link.parent.ReplaceOperand(ref link.result, replacement);
                link = _compiler.gtFindLink(statement, store);
            }
            while (!Unsafe.IsNullRef(ref link.result));
        }

#if DEBUG
        treeModified = true;
#endif
        statementInfoDirty = true;
        _compiler.fgSetStmtSeq(statement);
        tree = replacement;
        return false;
    }
}
