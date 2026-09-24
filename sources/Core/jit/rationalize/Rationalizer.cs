// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public sealed partial class Rationalizer : Phase
{
    private BasicBlock? _block;
    private ParameterUses?[]? _parameterUses;

    private static GenTree GetParent(GenTreeStack parents)
    {
        assert(parents.Count > 1);
        var enumerator = parents.GetEnumerator();
        _ = enumerator.MoveNext();
        _ = enumerator.MoveNext();
        return enumerator.Current;
    }

    public Rationalizer(Compiler compiler)
        : base(compiler, PHASE_RATIONALIZE)
    {
    }

    protected override PhaseStatus DoPhase()
    {
#if DEBUG
        SanityCheck();
#endif

        var compiler = CompilerInstance;
        var mapParameters = compiler.opts.OptimizationEnabled && !compiler.opts.IsOSR && (compiler.info.compArgsCount > 0);
#if TARGET_ARM
        mapParameters &= !compiler.compIsProfilerHookNeeded();
#endif
        if (mapParameters)
        {
            _parameterUses = new ParameterUses[compiler.info.compArgsCount];
        }
        compiler.compCurBB = null;
        compiler.fgOrder = Compiler.FGOrderLinear;

        var visitor = new Visitor(this, compiler);

        foreach (var block in compiler.Blocks)
        {
            compiler.compCurBB = block;
            _block = block;

            block.MakeLir(firstNode: null, lastNode: null);

            // Establish the first and last nodes for the block. This is necessary in order for the LIR
            // utilities that hang off the BasicBlock type to work correctly.
            var firstStatement = block.FirstStmt;

            if (firstStatement is null)
            {
                // No statements in this block; skip it.
                continue;
            }

            foreach (var statement in block.Statements)
            {
                assert(statement.TreeListBegin is not null);
                assert(statement.TreeListBegin.Prev is null);

                assert(statement.RootNode is not null);
                assert(statement.RootNode.Next is null);

                if (!statement.IsPhiDefnStmt) // Note that we get rid of PHI nodes here.
                {
                    var range = new LIR.Range(statement.TreeListBegin, statement.RootNode);
                    block.InsertAtEnd(range);

                    // If this statement has correct debug information, change it
                    // into a debug info node and insert it into the LIR. Note that
                    // we are currently reporting root info only back to the EE, so
                    // if the leaf debug info is invalid we still attach it.
                    // Note that we would like to have the invariant di.IsValid()
                    // => parent.IsValid() but it is currently not the case for
                    // NEWOBJ IL instructions where the debug info ends up attached
                    // to the allocation instead of the constructor call.
                    var di = statement.DebugInfo;

                    if (di.IsValid || di.GetRoot().IsValid)
                    {
                        var ilOffset = new GenTreeILOffset(di
#if DEBUG
                            , statement.LastILOffset
#endif
                        );
                        block.InsertBefore(statement.TreeListBegin, ilOffset);
                    }
                    _block = block;
                    _ = visitor.WalkTree(ref statement.RootNodeRef, user: null);
                }
            }

            block.FirstStmt = null;
        }

        compiler.compRationalIRForm = true;
        if (mapParameters)
        {
            RewriteParameterUses();
        }
        return PhaseStatus.MODIFIED_EVERYTHING;
    }

    /// <summary>Replace the given tree node by a GT_CALL.</summary>
    /// <param name="use">A reference to the tree node</param>
    /// <param name="sig">The signature info for callHnd</param>
    /// <param name="parents">The tree walk data providing the context</param>
    /// <param name="callHnd">The method handle of the call to be generated</param>
    /// <param name="entryPoint">The method entrypoint of the call to be generated</param>
    /// <param name="operands">The operand  list of the call to be generated</param>
    /// <param name="isSpecialIntrinsic">true if the GT_CALL should be marked as a special intrinsic</param>
    private unsafe void RewriteNodeAsCall(ref GenTree use, in CORINFO_SIG_INFO sig, Stack<GenTree> parents, CORINFO_METHOD_HANDLE callHnd, CORINFO_CONST_LOOKUP entryPoint, ReadOnlySpan<GenTree> operands, bool isSpecialIntrinsic)
    {
        var tree = use;
        var treeFirstNode = Compiler.fgGetFirstNode(tree);
        var insertionPoint = treeFirstNode.Prev;

        var block = _block;
        assert(block is not null);

        block.Remove(treeFirstNode, tree);

        // Create the call node
        var call = CompilerInstance.gtNewCallNode(tree.Type, CT_USER_FUNC, callHnd);

        if (isSpecialIntrinsic)
        {
#if TARGET_XARCH
            // Mark this as having been a special intrinsic node
            // This is used on xarch to track that it may need vzeroupper inserted to avoid the perf penalty on some hardware.
            call._callMoreFlags |= GTF_CALL_M_SPECIAL_INTRINSIC;
#endif
        }

        var retType = sig.retType.VarType;

        if (varTypeIsStruct(retType))
        {
            call.RetClsHnd = sig.retTypeClass;
            retType = CompilerInstance.impNormStructType(sig.retTypeClass);

            if (retType != call.Type)
            {
                assert(varTypeIsSimd(retType));
                call.ChangeType(retType);
            }

#if FEATURE_MULTIREG_RET
            call.InitializeStructReturnType(CompilerInstance, sig.retTypeClass, call.UnmanagedCallConv);
#endif

            var returnType = CompilerInstance.GetReturnTypeForStruct(sig.retTypeClass, call.UnmanagedCallConv, out var howToReturnStruct);

            if (howToReturnStruct == Compiler.SPK_ByReference)
            {
                assert(returnType == TYP_UNKNOWN);
                call._callMoreFlags |= GTF_CALL_M_RETBUFFARG;
            }
        }

        var sigArg = sig.args;
        var firstArg = 0;

        if (sig.hasThis())
        {
            var operand = operands[0];
            var arg = NewCallArg.CreateForPrimitive(operand).WithWellKnownArg(WellKnownArg.ThisPointer);

            _ = call.Args.PushBack(arg);
            call.Flags |= (operand.Flags & GTF_ALL_EFFECT);

            firstArg++;
        }

        for (var i = firstArg; i < operands.Length; i++)
        {
            var operand = operands[i];

            CORINFO_CLASS_HANDLE clsHnd;
            var_types sigTyp;

            fixed (CORINFO_SIG_INFO* pSigInfo = &sig)
            {
                var corTyp = strip(CompilerInstance.info.compCompHnd->getArgType(pSigInfo, sigArg, &clsHnd));
                sigTyp = corTyp.VarType;
            }

            NewCallArg arg;

            if (varTypeIsStruct(sigTyp))
            {
                // GenTreeFieldList should not have been introduced
                // for intrinsics that get rewritten back to user calls
                assert(!operand.Oper.IsFieldList);

                sigTyp = CompilerInstance.impNormStructType(clsHnd);

                if (varTypeIsMask(operand.Type))
                {
#if FEATURE_MASKED_HW_INTRINSICS
                    // No managed call takes TYP_MASK, so convert it back to a TYP_SIMD

                    var simdBaseType = CompilerInstance.getBaseTypeAndSizeOfSimdType(clsHnd, out var simdSize);
                    assert(simdSize != 0);

                    var cvtNode = CompilerInstance.gtNewSimdCvtMaskToVectorNode(sigTyp, operand, simdBaseType, (byte)simdSize);
                    block.InsertAfter(operand, new LIR.Range(CompilerInstance.fgSetTreeSeq(cvtNode), cvtNode));
                    operand = cvtNode;
#else
                    unreached();
#endif
                }
                arg = NewCallArg.CreateForStruct(operand, sigTyp, CompilerInstance.typGetObjLayout(clsHnd));
            }
            else
            {
                arg = NewCallArg.CreateForPrimitive(operand, sigTyp);
            }

            _ = call.Args.PushBack(arg);
            call.Flags |= (operand.Flags & GTF_ALL_EFFECT);

            sigArg = CompilerInstance.info.compCompHnd->getArgNext(sigArg);
        }

#if FEATURE_READYTORUN
        call._entryPoint = entryPoint;
#endif

        var tmpNum = BAD_VAR_NUM;

        if (call.ShouldHaveRetBufArg)
        {
            tmpNum = CompilerInstance.lvaGrabTemp(shortLifetime: true, "return buffer for hwintrinsic");
            CompilerInstance.lvaSetStruct(tmpNum, sig.retTypeClass, false);

            var destAddr = CompilerInstance.gtNewLclVarAddrNode(TYP_I_IMPL, tmpNum);
            var newArg = NewCallArg.CreateForPrimitive(destAddr).WithWellKnownArg(WellKnownArg.RetBuffer);

            _ = call.Args.InsertAfterThisOrFirst(newArg);
            call.Type = TYP_VOID;
        }

        call = CompilerInstance.fgMorphArgs(call);

        var result = call as GenTree;

        // Replace "tree" with "call"
        if (parents.Count > 1)
        {
            if (tmpNum != BAD_VAR_NUM)
            {
                result = CompilerInstance.gtNewLclvNode(retType, tmpNum);
            }

            if (varTypeIsMask(tree.Type))
            {
#if FEATURE_MASKED_HW_INTRINSICS
                // No managed call returns TYP_MASK, so convert it from a TYP_SIMD

                var simdBaseType = CompilerInstance.getBaseTypeAndSizeOfSimdType(call.RetClsHnd, out var simdSize);
                assert(simdSize != 0);

                result = CompilerInstance.gtNewSimdCvtVectorToMaskNode(TYP_MASK, result, simdBaseType, (byte)simdSize);

                if (tmpNum == BAD_VAR_NUM)
                {
                    // Propagate flags of "call" to its parent.
                    result.Flags |= (call.Flags & GTF_ALL_EFFECT) | GTF_CALL;
                }
#else
                unreached();
#endif
            }

            GetParent(parents).ReplaceOperand(ref use, result);

            if (tmpNum != BAD_VAR_NUM)
            {
                // We have a return buffer, so we need to insert both the result and the call
                // since they are independent trees. If we have a convert node, it will indirectly
                // insert the local node.

                CompilerInstance.gtSetEvalOrder(result);
                block.InsertAfter(insertionPoint, new LIR.Range(CompilerInstance.fgSetTreeSeq(result), result));

                CompilerInstance.gtSetEvalOrder(call);
                block.InsertAfter(insertionPoint, new LIR.Range(CompilerInstance.fgSetTreeSeq(call), call));
            }
            else
            {
                // We don't have a return buffer, so we only need to insert the result, which
                // will indirectly insert the call in the case we have a convert node as well.

                CompilerInstance.gtSetEvalOrder(result);
                block.InsertAfter(insertionPoint, new LIR.Range(CompilerInstance.fgSetTreeSeq(result), result));
            }
        }
        else
        {
            // If there's no parent, the tree being replaced is the root of the
            // statement (and no special handling is necessary).
            use = result;

            CompilerInstance.gtSetEvalOrder(call);
            block.InsertAfter(insertionPoint, new LIR.Range(CompilerInstance.fgSetTreeSeq(call), call));
        }

        if (tmpNum == BAD_VAR_NUM)
        {
            // Propagate flags of "call" to its parents.
            var callFlags = (call.Flags & GTF_ALL_EFFECT) | GTF_CALL;

            var isCurrentNode = true;
            foreach (var parent in parents)
            {
                if (isCurrentNode)
                {
                    isCurrentNode = false;
                    continue;
                }
                parent.Flags |= callFlags;
            }
        }
        else
        {
            // Normally the call replaces the node in pre-order, so we automatically continue visiting the call.
            // However, when we have a retbuf the node is replaced by a local with the call inserted before it,
            // so we need to make sure we visit it here.
            var visitor = new Visitor(this, CompilerInstance);

            var node = call as GenTree;
            _ = visitor.WalkTree(ref node, user: null);

            assert(node == call);
        }

        // Since "tree" is replaced with "result", pop "tree" node (i.e the current node) and replace it with "result" on parent stack.
        assert(parents.Peek() == tree);

        _ = parents.Pop();
        parents.Push(result);
    }

    /// <summary>Rewrite an intrinsic operator as a GT_CALL to the original method.</summary>
    /// <param name="use">A reference to the intrinsic node</param>
    /// <param name="parents">Tree walk data providing the context</param>
    /// <remarks>Some intrinsics, such as operation Sqrt, are rewritten back to calls, and some are not. The ones that are not being rewritten here must be handled in Codegen.Conceptually, the lower is the right place to do the rewrite. Keeping it in rationalization is mainly for throughput issue.</remarks>
    private unsafe void RewriteIntrinsicAsUserCall(ref GenTree use, GenTreeStack parents)
    {
        var intrinsic = use.AsIntrinsic();

        var inlineOperands = new InlineArray2<GenTree>();
        var operands = (Span<GenTree>)(inlineOperands);

        operands[0] = intrinsic.Op1;
        operands[1] = intrinsic.Op2;

        if (operands[0] is null)
        {
            operands = [];
        }
        else if (operands[1] is null)
        {
            operands = operands[..1];
        }

        var callHnd = intrinsic.MethodHandle;
        CompilerInstance.eeGetMethodSig(callHnd, out var sigInfo);

        // Regular Intrinsics often have their fallback in native and so should be treated as "special" once they become calls.
        RewriteNodeAsCall(ref use, in sigInfo, parents, callHnd, intrinsic.EntryPoint, operands, isSpecialIntrinsic: true);
    }

#if TARGET_ARM64
    private void RewriteSubLshDiv(ref GenTree use)
    {
        throw new NotImplementedException("ARM64 rationalization requires the target-specific rewrite closure.");
    }
#endif

    // Root visitor
    private Compiler.fgWalkResult RewriteNode(ref GenTree useEdge, GenTreeStack parentStack)
    {
        var node = useEdge;
        var compiler = CompilerInstance;

        var block = _block;
        assert(block is not null);

        // Clear the REVERSE_OPS flag on the current node.
        node.Flags &= ~GTF_REVERSE_OPS;

        scoped LIR.Use use;
        if (parentStack.Count < 2)
        {
            LIR.Use.MakeDummyUse(block, useEdge, out use);
        }
        else
        {
            use = new LIR.Use(block, ref useEdge, GetParent(parentStack));
        }

        assert(node == use.Def());

        switch (node.Oper)
        {
            case GT_LCL_FLD:
            {
                if (use.IsDummyUse())
                {
                    block.Remove(node);
                    return Compiler.WALK_CONTINUE;
                }
                goto case GT_STORE_LCL_VAR;
            }

            case GT_STORE_LCL_VAR:
            case GT_STORE_LCL_FLD:
            case GT_LCL_ADDR:
            {
                if (_parameterUses is not null)
                {
                    RecordParameterUse(node);
                }
                break;
            }

            case GT_CALL:
            {
                var call = node.AsCall();

                // In linear order we no longer need to retain the stores in early
                // args as these have now been sequenced.
                foreach (var arg in call.Args.EarlyArgs)
                {
                    if (arg.LateNode is not null)
                    {
                        var earlyNode = arg.EarlyNode;

                        assert(earlyNode is not null);
                        if (earlyNode.IsValue)
                        {
                            earlyNode.IsUnusedValue = true;
                        }
                        arg.EarlyNode = null;
                    }
                }

#if DEBUG
                // The above means that all argument nodes are now true arguments.
                foreach (var arg in call.Args.Args)
                {
                    assert((arg.EarlyNode is null) != (arg.LateNode is null));
                }
#endif
                break;
            }

            case GT_BOX:
            case GT_ARR_ADDR:
            {
                var unOp = node.AsUnOp();
                var op1 = unOp.Op1;

                // BOX/ARR_ADDR are "passthrough" nodes,
                // and at this point we no longer need them.
                if (op1 is not null)
                {
                    use.ReplaceWith(op1);
                    block.Remove(node);
                    node = op1;
                }
                break;
            }

            case GT_GCPOLL:
            {
                // GCPOLL is essentially a no-op, we used it as a hint for fgCreateGCPoll

                node.BashToNOP();
                return Compiler.WALK_CONTINUE;
            }

            case GT_COMMA:
            {
                var op = node.AsOp();
                var op1 = op.Op1;

                var lhsRange = block.GetTreeRange(op1, out var isClosed, out var sideEffects);

                if ((sideEffects & GTF_ALL_EFFECT) is 0)
                {
                    // The LHS has no side effects. Remove it.
                    // All transformations on pure trees keep their operands in LIR
                    // and should not violate tree order.
                    assert(isClosed);

                    ForgetParameterUses(lhsRange);
                    block.Delete(lhsRange);
                }
                else if (op1.IsValue)
                {
                    op1.IsUnusedValue = true;
                }

                block.Remove(node);

                var replacement = op.Op2;

                if (!use.IsDummyUse())
                {
                    use.ReplaceWith(replacement);
                    node = replacement;
                }
                else
                {
                    // This is a top-level comma. If the RHS has no side effects we can remove it as well.
                    var rhsRange = block.GetTreeRange(replacement, out isClosed, out sideEffects);

                    if ((sideEffects & GTF_ALL_EFFECT) is 0)
                    {
                        // All transformations on pure trees keep their operands in
                        // LIR and should not violate tree order.
                        assert(isClosed);

                        ForgetParameterUses(rhsRange);
                        block.Delete(rhsRange);
                    }
                    else
                    {
                        node = replacement;
                    }
                }
                break;
            }

            case GT_INTRINSIC:
            {
                // Non-target intrinsics should have already been rewritten back into user calls.
                assert(compiler.IsTargetIntrinsic(node.AsIntrinsic().IntrinsicName));
#if TARGET_ARM64
                throw new NotImplementedException("ARM64 intrinsic rationalization requires mask-reduction rewrites.");
#endif
                break;
            }

#if FEATURE_HW_INTRINSICS
            case GT_HWINTRINSIC:
            {
                RewriteHWIntrinsic(ref useEdge, parentStack);
                node = useEdge;
                break;
            }
#endif

            case GT_CAST:
            {
                var cast = node.AsCast();
                var castOp = cast.Op1;

                if (castOp.Oper.IsSimple)
                {
                    _ = compiler.fgSimpleLowerCastOfSmpOp(block, cast);
                }
                break;
            }

            case GT_BSWAP16:
            {
                var unOp = node.AsUnOp();
                if (unOp.Op1.Oper is GT_CAST)
                {
                    _ = compiler.fgSimpleLowerBswap16(block, unOp);
                }
                break;
            }

            default:
            {
                // Check that we don't have nodes not allowed in HIR here.
#if DEBUG
                assert((node.Oper.DebugKind & DBK_NOTHIR) == 0);
#endif
                break;
            }
        }

        // Do some extra processing on top-level nodes to remove unused local reads.
        if (node.Oper.IsLocalRead)
        {
            if (use.IsDummyUse())
            {
                ForgetParameterUses(new LIR.ReadOnlyRange(node, node));
                block.Remove(node);
            }
            else
            {
                // Local reads are side-effect-free; clear any flags leftover from frontend transformations.
                node.Flags &= ~GTF_ALL_EFFECT;
            }
        }
        else
        {
            if (node.IsValue && use.IsDummyUse())
            {
                node.IsUnusedValue = true;
            }

            if (node.Type is TYP_LONG)
            {
                compiler.compLongUsed = true;
            }
        }
        return Compiler.WALK_CONTINUE;
    }

#if DEBUG
    // general purpose sanity checking of de facto standard GenTree
    public void SanityCheck()
    {
        foreach (var block in CompilerInstance.Blocks)
        {
            foreach (var statement in block.Statements)
            {
                ValidateStatement(statement, block);
            }
        }
    }

    // sanity checking of rationalized IR
    public void SanityCheckRational()
    {
        SanityCheck();
    }

    public static void ValidateStatement(Statement stmt, BasicBlock block)
    {
        var compiler = JitTls.Compiler;
        assert(compiler is not null);
        compiler.fgDebugCheckNodeLinks(block, stmt);
    }
#endif
}
