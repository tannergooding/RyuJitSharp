// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public struct SubstitutePlaceholdersAndDevirtualizeWalker : IGenTreeVisitor<SubstitutePlaceholdersAndDevirtualizeWalker>
{
    public static bool DoPostOrder => true;

    public static bool DoPreOrder => true;

    public static bool UseExecutionOrder => true;

    private readonly Compiler _compiler;
    private readonly GenTreeStack _ancestors;
    private Statement? _curStmt;
    private Statement? _firstNewStmt;
    private bool _madeChanges;

    public SubstitutePlaceholdersAndDevirtualizeWalker(Compiler compiler)
    {
        _compiler = compiler;
        _ancestors = [];
    }

    public readonly bool MadeChanges => _madeChanges;

    /// <summary>re-examine calls after inlining to see if we can do more devirtualization</summary>
    /// <param name="use">reference to tree to examine for updates</param>
    /// <param name="parent">parent node containing the pTree edge</param>
    private unsafe void LateDevirtualization(ref GenTree use, GenTree? parent)
    {
        // We used to check this opportunistically in the preorder callback for
        // calls where the `obj` was fed by a return, but we now re-examine
        // all calls.
        //
        // Late devirtualization (and eventually, perhaps, other type-driven
        // opts like cast optimization) can happen now because inlining or other
        // optimizations may have provided more accurate types than we saw when
        // first importing the trees.
        //
        // It would be nice to screen candidate sites based on the likelihood
        // that something has changed. Otherwise we'll waste some time retrying
        // an optimization that will just fail again.

        var tree = use;

        // In some (rare) cases the parent node of tree will be smashed to a NOP during
        // the preorder by AttachStructToInlineeArg.
        //
        // jit\Methodical\VT\callconv\_il_reljumper3 for x64 linux
        //
        // If so, just bail out here.

        if (tree is null)
        {
            assert((parent is not null) && (parent.Oper is GT_NOP));
            return;
        }

        if (tree.Oper is GT_CALL)
        {
            var call = tree.AsCall();
            var tryLateDevirt = call.IsDevirtualizationCandidate(_compiler);

#if DEBUG
            tryLateDevirt = tryLateDevirt && (JitConfig.JitEnableLateDevirtualization == 1);
#endif

            if (tryLateDevirt)
            {
#if DEBUG
                if (_compiler.verbose)
                {
                    jitprintf("**** Late devirt opportunity\n");
                    _compiler.gtDispTree(call);
                }
#endif

                var lateDevirtualizationInfo = call._lateDevirtualizationInfo;
                assert(lateDevirtualizationInfo is not null);

                var method = lateDevirtualizationInfo.methodHnd;
                var context = lateDevirtualizationInfo.exactContextHnd;
                var inlinersContext = call._inlineContext;
                var methodFlags = (CorInfoFlag)(0);
                var isLateDevirtualization = true;
                var explicitTailCall = call.IsTailPrefixedCall;

                var contextInput = context;
                context = null;

                _compiler.impDevirtualizeCall(
                    call,
                    Unsafe.NullRef<CORINFO_RESOLVED_TOKEN>(),
                    ref method,
                    ref methodFlags,
                    ref contextInput,
                    out context,
                    isLateDevirtualization,
                    explicitTailCall);

                if (!call.IsDevirtualizationCandidate(_compiler))
                {
                    assert(context is not null);
                    assert(inlinersContext is not null);

                    var callInfo = new CORINFO_CALL_INFO {
                        hMethod = method,
                        methodFlags = methodFlags,
                    };
                    _compiler.impMarkInlineCandidate(call, context, callInfo, inlinersContext);

                    if (call.IsInlineCandidate)
                    {
                        assert(_compiler.compCurBB is not null);
                        assert(_curStmt is not null);

                        _compiler.gtSplitTree(_compiler.compCurBB, _curStmt, call, out var newStmt, out var splitChanged, early: true);

                        if (splitChanged)
                        {
                            _firstNewStmt ??= newStmt;
                        }

                        // If the call is the root expression in a statement, and it returns void,
                        // we can inline it directly without creating a RET_EXPR.
                        if ((parent is not null) || (call._returnType is not TYP_VOID))
                        {
                            assert(call._lateDevirtualizationInfo is not null);
                            var di = new DebugInfo(call._inlineContext, call._lateDevirtualizationInfo.ilLocation);

                            assert(_compiler.compCurBB is not null);
                            assert(_curStmt is not null);

                            var stmt = _compiler.gtNewStmt(call, di);
                            _compiler.fgInsertStmtBefore(_compiler.compCurBB, _curStmt, stmt);

                            _firstNewStmt ??= stmt;
                            var retExpr = _compiler.gtNewInlineCandidateReturnExpr(call.AsCall(), call.Type.ActualType);

                            assert(call.SingleInlineCandidateInfo is not null);
                            call.SingleInlineCandidateInfo.retExpr = retExpr;

#if DEBUG
                            JITDUMP($"Creating new RET_EXPR for [{call.TreeId:D6}]:\n");
                            DISPTREE(retExpr);
#endif

                            use = retExpr;
                        }

                        JITDUMP("New inline candidate due to late devirtualization:\n");
                        DISPTREE(call);
                    }
                }

                _madeChanges = true;
            }
        }
        else if (tree.Oper is GT_STORE_LCL_VAR)
        {
            var lclVar = tree.AsLclVar();

            var lclNum = lclVar.LclNum;
            var value = lclVar.Data;

            // If we're storing to a ref typed local that has one definition,
            // we may be able to sharpen the type for the local.

            if (tree.Type is TYP_REF)
            {
                ref var lcl = ref _compiler.lvaGetDesc(lclNum);

                if (lcl.lvSingleDef)
                {
                    if (lcl.lvClassHnd == NO_CLASS_HANDLE)
                    {
                        _compiler.lvaSetClass(lclNum, value);
                    }
                    else
                    {
                        _compiler.lvaUpdateClass(lclNum, value);
                    }

                    _madeChanges = true;
                    _compiler.hasUpdatedTypeLocals = true;
                }
            }

            // If we created a self-store (say because we are sharing return spill temps) we can remove it.
            //
            if ((value.Oper is GT_LCL_VAR) && (value.AsLclVar().LclNum == lclNum))
            {
                JITDUMP("... removing self-store\n");
                DISPTREE(tree);

                tree.BashToNOP();
                _madeChanges = true;
            }
        }
        else if (tree.Oper is GT_JTRUE)
        {
            // See if this jtrue is now foldable.
            var jtrue = tree.AsUnOp();

            var block = _compiler.compCurBB;
            assert(block is not null);

            var condTree = jtrue.Op1;
            var modifiedTree = false;

            assert(block.LastStmt is not null);
            assert(tree == block.LastStmt.RootNode);

            while (condTree.Oper is GT_COMMA)
            {
                // Tree is a root node, and condTree its only child.
                // Move comma effects to a prior statement.

                var comma = condTree.AsOp();

                var sideEffects = null as GenTree;
                _compiler.gtExtractSideEffList(comma.Op1, ref sideEffects);

                if (sideEffects is not null)
                {
                    _compiler.fgNewStmtNearEnd(block, sideEffects);
                }

                // Splice out the comma with its value
                //
                var valueTree = comma.Op2;
                condTree = valueTree;

                jtrue.Op1 = valueTree;
                modifiedTree = true;
            }

            if (modifiedTree)
            {
                _compiler.gtUpdateNodeSideEffects(tree);
            }

            if (condTree.Oper is GT_CNS_INT)
            {
#if DEBUG
                JITDUMP($" ... found foldable jtrue at [{tree.TreeId:D6}] in {FMT_BB(block.bbNum)}\n");
#endif
                _compiler.Metrics.InlinerBranchFold++;

                // We have a constant operand, and should have the all clear to optimize.
                // Update side effects on the tree, assert there aren't any, and bash to nop.
                _compiler.gtUpdateNodeSideEffects(tree);

                assert((tree.Flags & GTF_SIDE_EFFECT) == 0);
                tree.BashToNOP();
                _madeChanges = true;

                var removedEdge = block.FalseEdge;
                var retainedEdge = block.TrueEdge;

                if (condTree.IsIntegralConst(0))
                {
                    (removedEdge, retainedEdge) = (retainedEdge, removedEdge);
                }

                _compiler.fgRemoveRefPred(removedEdge);
                block.SetKindAndTargetEdge(BBJ_ALWAYS, retainedEdge);

                // Update profile, make it consistent if possible.
                _compiler.fgRepairProfileCondToUncond(block, retainedEdge, removedEdge, ref _compiler.Metrics.ProfileInconsistentInlinerBranchFold);
            }
            else
            {
                assert(condTree.Oper.IsCompare);
            }
        }
        else
        {
            use = _compiler.gtFoldExpr(tree);
            _madeChanges = true;
        }
    }

    public Compiler.fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
    {
        var tree = use;

        // All the operations here and in the corresponding postorder
        // callback (LateDevirtualization) are triggered by GT_CALL or
        // GT_RET_EXPR trees, and these (should) have the call side
        // effect flag.
        //
        // So bail out for any trees that don't have this flag.

        if ((tree.Flags & GTF_CALL) is 0)
        {
            return Compiler.fgWalkResult.WALK_SKIP_SUBTREES;
        }

        if (tree.Oper is GT_RET_EXPR)
        {
            UpdateInlineReturnExpressionPlaceHolder(ref use, user);
        }

#if DEBUG && FEATURE_MULTIREG_RET
        // Make sure we don't have a tree like so: V05 = (, , , retExpr);
        // Since we only look one level above for the parent for '=' and
        // do not check if there is a series of COMMAs. See above.
        // Importer and FlowGraph will not generate such a tree, so just
        // leaving an assert in here. This can be fixed by looking ahead
        // when we visit stores similar to AttachStructInlineeToStore.

        if (tree.Oper.IsStore)
        {
            var value = tree.Data;

            if (value.Oper is GT_COMMA)
            {
                var effectiveValue = value.EffectiveVal;

                noway_assert(!varTypeIsStruct(effectiveValue.Type) ||
                             (effectiveValue.Oper is not GT_RET_EXPR) ||
                             !effectiveValue.AsRetExpr().InlineCandidate.HasMultiRegRetVal);
            }
        }
#endif

        return Compiler.fgWalkResult.WALK_CONTINUE;
    }

    public Compiler.fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user)
    {
        LateDevirtualization(ref use, user);

        if ((use is not null) && (user is not null))
        {
            user.Flags |= use.Flags & GTF_ALL_EFFECT;
        }

        return Compiler.fgWalkResult.WALK_CONTINUE;
    }

    /// <summary>replace an inline return expression placeholder if there is one.</summary>
    /// <param name="use">edge for the tree that is a GT_RET_EXPR node</param>
    /// <param name="parent">node containing the edge</param>
    /// <remarks>fgWalkResult indicating the walk should continue; that is we wish to fully explore the tree.</remarks>
    private unsafe void UpdateInlineReturnExpressionPlaceHolder(ref GenTree use, GenTree? parent)
    {
        // Looks for GT_RET_EXPR nodes that arose from tree splitting done
        // during importation for inline candidates, and replaces them.
        //
        // For successful inlines, substitutes the return value expression
        // from the inline body for the GT_RET_EXPR.
        //
        // For failed inlines, rejoins the original call into the tree from
        // whence it was split during importation.
        //
        // The code doesn't actually know if the corresponding inline
        // succeeded or not; it relies on the fact that gtInlineCandidate
        // initially points back at the call and is modified in place to
        // the inlinee return expression if the inline is successful (see
        // tail end of fgInsertInlineeBlocks for the update of iciCall).
        //
        // If the return type is a struct type and we're on a platform
        // where structs can be returned in multiple registers, ensure the
        // call has a suitable parent.
        //
        // If the original call type and the substitution type are different
        // the functions makes necessary updates. It could happen if there was
        // an implicit conversion in the inlinee body.

        while (use.Oper is GT_RET_EXPR)
        {
            var tree = use;

            // Skip through chains of GT_RET_EXPRs (say from nested inlines)
            // to the actual tree to use.

            var inlineeBB = null as BasicBlock;
            var inlineCandidate = tree;

            do
            {
                var retExpr = inlineCandidate.AsRetExpr();

                inlineCandidate = retExpr.SubstExpr;
                assert(inlineCandidate is not null);

                inlineeBB = retExpr.SubstBB;
            }
            while (inlineCandidate.Oper is GT_RET_EXPR);

            // We might as well try and fold the return value. Eg returns of
            // constant bools will have CASTS. This folding may uncover more
            // GT_RET_EXPRs, so we loop around until we've got something distinct.
            //
            inlineCandidate = _compiler.gtFoldExpr(inlineCandidate);

            // If this use is an unused ret expr, is the first child of a comma, the return value is ignored.
            // Extract any side effects.
            //
            if ((parent is not null) && (parent.Oper is GT_COMMA) && (parent.AsOp().Op1 == use))
            {
#if DEBUG
                JITDUMP($"\nReturn expression placeholder [{tree.TreeId:D6}] value [{inlineCandidate.TreeId:D6}] unused\n");
#endif

                var sideEffects = null as GenTree;
                _compiler.gtExtractSideEffList(inlineCandidate, ref sideEffects);

                if (sideEffects is null)
                {
                    JITDUMP("\nInline return expression had no side effects\n");
                    use.BashToNOP();
                }
                else
                {
                    JITDUMP("\nInserting the inline return expression side effects\n");

                    DISPTREE(sideEffects);
                    JITDUMP("\n");

                    use = sideEffects;
                }
            }
            else
            {
#if DEBUG
                JITDUMP($"\nReplacing the return expression placeholder [{tree.TreeId:D6}] with [{inlineCandidate.TreeId:D6}]\n");
#endif
                DISPTREE(tree);

                var retType = tree.Type;
                var newType = inlineCandidate.Type;

                // If we end up swapping type we may need to retype the tree:
                if (retType != newType)
                {
                    if ((retType is TYP_BYREF) && (inlineCandidate.Oper is GT_IND))
                    {
                        // - in an RVA static if we've reinterpreted it as a byref;
                        assert(newType is TYP_I_IMPL);
                        JITDUMP("Updating type of the return GT_IND expression to TYP_BYREF\n");
                        inlineCandidate.Type = TYP_BYREF;
                    }
                }

                JITDUMP("\nInserting the inline return expression\n");

                DISPTREE(inlineCandidate);
                JITDUMP("\n");

                use = inlineCandidate;
            }

            _madeChanges = true;

            if (inlineeBB is not null)
            {
                // IR may potentially contain nodes that requires mandatory BB flags to be set.
                // Propagate those flags from the containing BB.

                assert(_compiler.compCurBB is not null);
                _compiler.compCurBB.CopyFlags(inlineeBB, BBF_COPY_PROPAGATE);
            }
        }

        // If the inline was rejected and returns a retbuffer, then mark that
        // local as DNER now so that promotion knows to leave it up to physical
        // promotion.
        if (use.Oper.IsCall)
        {
            var call = use.AsCall();
            var retBuffer = call.Args.RetBufferArg;

            if (retBuffer is not null)
            {
                var node = retBuffer.Node;
                assert(node is not null);

                if (node.Oper is GT_LCL_ADDR)
                {
                    _compiler.lvaSetVarDoNotEnregister(node.AsLclVarCommon().LclNum, DoNotEnregisterReason.HiddenBufferStructArg);
                }
            }

#if FEATURE_MULTIREG_RET
            // If an inline was rejected and the call returns a struct, we may
            // have deferred some work when importing call for cases where the
            // struct is returned in multiple registers.
            //
            // See the bail-out clauses in impFixupCallStructReturn for inline
            // candidates.
            //
            // Do the deferred work now.

            if (varTypeIsStruct(call.Type) && call.HasMultiRegRetVal)
            {
                // See assert below, we only look one level above for a store parent.

                assert(parent is not null);
                if (parent.Oper.IsStore)
                {
                    // The inlinee can only be the value.
                    assert(parent.Data == call);
                    AttachStructInlineeToStore(parent, call.RetClsHnd);
                }
                else
                {
                    // Just store the inlinee to a variable to keep it simple.
                    use = StoreStructInlineeToVar(call, call.RetClsHnd);
                }
                _madeChanges = true;
            }
#endif
        }
    }

    /// <summary>Walk the tree of a statement, and return the first newly added statement if any, otherwise return the original statement.</summary>
    /// <param name="stmt">the statement to walk.</param>
    /// <returns>The first newly added statement if any, or the original statement.</returns>
    public Statement WalkStatement(Statement stmt)
    {
        _curStmt = stmt;
        _firstNewStmt = null;

        _ = WalkTree(ref _curStmt.RootNodeRef, null);
        return (_firstNewStmt is null) ? _curStmt : _firstNewStmt;
    }

    public Compiler.fgWalkResult WalkTree(ref GenTree use, GenTree? user)
        => IGenTreeVisitor<SubstitutePlaceholdersAndDevirtualizeWalker>.WalkTree(ref this, ref use, user, _ancestors);

#if FEATURE_MULTIREG_RET
    /// <summary>Update a "STORE(..., inlinee)" tree.</summary>
    /// <param name="store">The store with the inlinee as value</param>
    /// <param name="retClsHnd">The struct handle for the inlinee</param>
    /// <remarks>Morphs inlinees that are multi-reg nodes into the (only) supported shape of "lcl = node()", either by marking the store local "lvIsMultiRegRet" or storing the node into a temp and using that as the new value.</remarks>
    private readonly unsafe void AttachStructInlineeToStore(GenTree store, CORINFO_CLASS_HANDLE retClsHnd)
    {
        assert(store.Oper.IsStore);
        var inlinee = store.Data;

        // We need to force all stores from multi-reg nodes into the "lcl = node()" form.
        if (inlinee.IsMultiRegNode)
        {
            // Special case: we already have a local, the only thing to do is mark it appropriately. Except
            // if it may turn into an indirection. TODO-Bug: this does not account for x86 varargs args.
            if ((store.Oper is GT_STORE_LCL_VAR) && !_compiler.lvaIsImplicitByRefLocal(store.AsLclVar().LclNum))
            {
                _compiler.lvaGetDesc(store.AsLclVar().LclNum).lvIsMultiRegRet = true;
            }
            else
            {
                // Here, we store our node into a fresh temp and then use that temp as the new value.
                store.DataRef = StoreStructInlineeToVar(inlinee, retClsHnd);
            }
        }
    }

    /// <summary>Store the struct inlinee to a temp local.</summary>
    /// <param name="inlinee">The inlinee of the RET_EXPR node</param>
    /// <param name="retClsHnd">The struct class handle of the type of the inlinee.</param>
    /// <returns>Value representing the freshly defined temp.</returns>
    private readonly unsafe GenTreeOp StoreStructInlineeToVar(GenTree inlinee, CORINFO_CLASS_HANDLE retClsHnd)
    {
        assert(inlinee.Oper is not GT_RET_EXPR);

        var lclNum = _compiler.lvaGrabTemp(shortLifetime: false, "RetBuf for struct inline return candidates.");
        ref var varDsc = ref _compiler.lvaGetDesc(lclNum);
        _compiler.lvaSetStruct(lclNum, retClsHnd, false);

        // Sink the store below any COMMAs: this is required for multi-reg nodes.
        var src = inlinee;
        var lastComma = null as GenTree;

        while (src.Oper is GT_COMMA)
        {
            lastComma = src;
            src = src.AsOp().Op2;
        }

        // When storing a multi-register value to a local var, make sure the variable is marked as lvIsMultiRegRet.
        if (src.IsMultiRegNode)
        {
            varDsc.lvIsMultiRegRet = true;
        }

        var store = _compiler.gtNewStoreLclVarNode(lclNum, src) as GenTree;

        // If inlinee was comma, new inlinee is (, , , lcl = inlinee).
        if (inlinee.Oper is GT_COMMA)
        {
            assert(lastComma is not null);
            lastComma.AsOp().Op2 = store;
            store = inlinee;
        }

        var lcl = _compiler.gtNewLclvNode(varDsc.Type, lclNum);
        return _compiler.gtNewCommaNode(lcl.Type, store, lcl);
    }
#endif
}
