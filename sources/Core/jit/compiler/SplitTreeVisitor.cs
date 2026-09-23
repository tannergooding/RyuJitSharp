// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial struct SplitTreeVisitor : IGenTreeVisitor<SplitTreeVisitor>
{
    public static bool DoPostOrder => true;

    public static bool DoPreOrder => true;

    public static bool UseExecutionOrder => true;

    private readonly Compiler _compiler;
    private readonly GenTreeStack _ancestors;

    private BasicBlock _bb;
    private Statement _splitStmt;
    private GenTree _splitNode;
    private bool _early;
    private List<UseInfo> _useStack;

    private Statement? _firstStatement;
    private UseInfo? _splitNodeUse;
    private bool _madeChanges;

    public SplitTreeVisitor(Compiler compiler, BasicBlock bb, Statement stmt, GenTree splitNode, bool early)
    {
        _compiler = compiler;
        _ancestors = [];

        _bb = bb;
        _splitStmt = stmt;
        _splitNode = splitNode;
        _early = early;
        _useStack = [];
    }

    public readonly Statement? FirstStatement => _firstStatement;

    public readonly bool MadeChanges => _madeChanges;

    public readonly ref GenTree SplitNodeUse
    {
        get
        {
            if (_splitNodeUse is UseInfo info)
            {
                return ref info.GetUse(_splitStmt);
            }

            return ref Unsafe.NullRef<GenTree>();
        }
    }

    public Compiler.fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user)
    {
        if (use == _splitNode)
        {
            var userIsReturned = false;
            // Split all siblings and ancestor siblings.
            int i;

            for (i = 0; i < _useStack.Count - 1; i++)
            {
                var useInf = _useStack[i];
                ref var currentUse = ref useInf.GetUse(_splitStmt);

                if (Unsafe.AreSame(ref currentUse, ref use))
                {
                    break;
                }

                // If this has the same user as the next node then it is a
                // sibling of an ancestor -- and thus not on the "path"
                // that contains the split node.
                if (_useStack[i + 1].User == useInf.User)
                {
                    SplitOutUse(ref currentUse, useInf.User, userIsReturned);
                }
                else
                {
                    // This is an ancestor of the node we're splitting on.
                    userIsReturned = IsReturned(ref currentUse, useInf.User, userIsReturned);
                }
            }

            var splitUse = _useStack[i];
            assert(Unsafe.AreSame(ref splitUse.GetUse(_splitStmt), ref use));
            userIsReturned = IsReturned(ref use, splitUse.User, userIsReturned);

            // The remaining nodes should be operands of the split node.
            for (i++; i < _useStack.Count; i++)
            {
                var useInf = _useStack[i];
                assert(useInf.User == use);
                SplitOutUse(ref useInf.GetUse(_splitStmt), useInf.User, userIsReturned);
            }

            _splitNodeUse = splitUse;

            return Compiler.fgWalkResult.WALK_ABORT;
        }

        while (!Unsafe.AreSame(ref _useStack[^1].GetUse(_splitStmt), ref use))
        {
            _useStack.RemoveAt(_useStack.Count - 1);
        }

        return Compiler.fgWalkResult.WALK_CONTINUE;
    }

    public readonly Compiler.fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
    {
        assert(use.Oper is not GT_QMARK);

        if (user is not null)
        {
            var index = 0;

            foreach (ref var operand in user.UseEdges)
            {
                if (Unsafe.AreSame(ref operand, ref use))
                {
                    _useStack.Add(new UseInfo(user, index));

                    return Compiler.fgWalkResult.WALK_CONTINUE;
                }

                index++;
            }

            throw new System.InvalidOperationException("The visited operand does not belong to its user.");
        }

        _useStack.Add(new UseInfo(null, -1));

        return Compiler.fgWalkResult.WALK_CONTINUE;
    }

    public Compiler.fgWalkResult WalkTree(ref GenTree use, GenTree? user)
        => IGenTreeVisitor<SplitTreeVisitor>.WalkTree(ref this, ref use, user, _ancestors);

    private readonly bool IsReturned(ref GenTree use, GenTree? user, bool userIsReturned)
    {
        if (user is not null)
        {
            if (user.Oper is GT_RETURN)
            {
                return true;
            }

            if (userIsReturned && (user.Oper is GT_COMMA) && Unsafe.AreSame(ref user.AsOp().Op2Ref, ref use))
            {
                return true;
            }
        }

        return false;
    }

    private readonly bool IsValue(ref GenTree use, GenTree? user)
    {
        // Some places create void-typed commas that wrap actual values
        // (e.g. VN-based dead store removal), so we need the double check
        // here.

        if (!use.IsValue || !use.EffectiveVal.IsValue)
        {
            return false;
        }

        if (user is null)
        {
            return false;
        }

        if ((user.Oper is GT_COMMA) && Unsafe.AreSame(ref user.AsOp().Op1Ref, ref use))
        {
            return false;
        }

        if (user.Oper is GT_CALL)
        {
            var call = user.AsCall();

            foreach (var callArg in call.Args.Args)
            {
                if (Unsafe.AreSame(ref callArg.EarlyNodeRef, ref use) && (callArg.LateNode is not null))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private unsafe void SplitOutUse(ref GenTree use, GenTree? user, bool userIsReturned)
    {
        if (use.Oper.IsInvariant)
        {
            return;
        }

        var compiler = _compiler;

        if (use.Oper is GT_LCL_VAR)
        {
            ref var lclVarDesc = ref compiler.lvaGetDesc(use.AsLclVar().LclNum);

            if (!lclVarDesc.IsAddressExposed && !(_early && lclVarDesc.lvHasLdAddrOp))
            {
                // The splitting we do here should always guarantee that we
                // only introduce locals for the tree edges that overlap the
                // split point, so it should be ok to avoid creating statements
                // for locals that aren't address exposed. Note that this
                // relies on it being illegal IR to have a tree edge for a
                // register candidate that overlaps with an interfering node.
                //
                // For example, this optimization would be problematic if IR
                // like the following could occur:
                //
                // CALL
                //   LCL_VAR V00
                //   CALL
                //     STORE_LCL_VAR<V00>(...) (setup)
                //     LCL_VAR V00
                //
                return;
            }
        }

#if !TARGET_64BIT
        // GT_MUL with GTF_MUL_64RSLT is required to stay with casts on the
        // operands. Note that one operand may also be a constant, but we
        // would have exited early above for that case.

        if ((user is not null) && (user.Oper is GT_MUL) && user.Is64RsltMul)
        {
            assert(use.Oper is GT_CAST);
            SplitOutUse(ref use.AsCast().Op1Ref, use, userIsReturned: false);

            return;
        }
#endif

        if (use.Oper is GT_COMMA)
        {
            // We might as well get rid of the comma while we're at it. This avoids unnecessarily nested trees and
            // also handles splitting some irregular nodes when nested under commas
            // (like COMMA(op1, FIELD_LIST(...))).
            SplitOutUse(ref use.AsOp().Op1Ref, use, userIsReturned: false);

            use = use.AsOp().Op2;
            SplitOutUse(ref use, user, userIsReturned);

            _madeChanges = true;

            return;
        }

        if (use.Oper is GT_FIELD_LIST or GT_INIT_VAL)
        {
            foreach (ref var operandUse in use.UseEdges)
            {
                SplitOutUse(ref operandUse, use, userIsReturned: false);
            }

            return;
        }

        var stmt = null as Statement;

        if (!IsValue(ref use, user))
        {
            var sideEffects = null as GenTree;
            _compiler.gtExtractSideEffList(use, ref sideEffects);

            if (sideEffects is not null)
            {
                stmt = _compiler.fgNewStmtFromTree(sideEffects, block: null, _splitStmt.DebugInfo);
            }

            use = _compiler.gtNewNothingNode();
            _madeChanges = true;
        }
        else
        {
            var lclNum = _compiler.lvaGrabTemp(shortLifetime: true, "Spilling to split statement for tree");

            if (varTypeIsStruct(use.Type) && (use.IsMultiRegNode || (IsReturned(ref use, user, userIsReturned) && _compiler.compMethodReturnsMultiRegRetType)))
            {
                _compiler.lvaGetDesc(lclNum).lvIsMultiRegRet = true;
            }

            var value = use;
            var store = _compiler.gtNewTempStore(lclNum, value);

            ref var lclDsc = ref _compiler.lvaGetDesc(lclNum);
            lclDsc.lvSingleDef = true;
            JITDUMP($"Marked V{lclNum:D2} as a single def temp\n");

            if (value.Type is TYP_REF)
            {
                var clsHnd = _compiler.gtGetClassHandle(value, out var isExact, out var isNonNull);

                if (clsHnd != NO_CLASS_HANDLE)
                {
                    _compiler.lvaSetClass(lclNum, clsHnd, isExact);
                }
            }

            stmt = _compiler.fgNewStmtFromTree(store, block: null, _splitStmt.DebugInfo);
            use = _compiler.gtNewLclvNode(value.Type.ActualType, lclNum);

            _madeChanges = true;
        }

        if (stmt is not null)
        {
            _firstStatement ??= stmt;
            _compiler.fgInsertStmtBefore(_bb, _splitStmt, stmt);
        }
    }
}
