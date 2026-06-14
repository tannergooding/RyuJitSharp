// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Compiler;

namespace RyuJitSharp;

/// <summary>a flexible tree walker implemented using the curiously-recurring-template pattern.</summary>
/// <typeparam name="TSelf"></typeparam>
public interface IGenTreeVisitor<TSelf>
    where TSelf : struct, IGenTreeVisitor<TSelf>, allows ref struct
{
    /// <summary>when true, the walker will push each node onto the `_ancestors` stack. "Ancestors" is a bit of a misnomer, as the first entry will always be the current node.</summary>
    static virtual bool ComputeStack => false;

    /// <summary>when true, the walker will invoke `TVisitor.PreOrderVisit` with the current node as an argument before visiting the node's operands.</summary>
    static virtual bool DoPreOrder => false;

    /// <summary>when true, the walker will invoke `TVisitor.PostOrderVisit` with the current node as an argument after visiting the node's operands.</summary>
    static virtual bool DoPostOrder => false;

    /// <summary>when true, the walker will only invoke `TVisitor.PreOrderVisit` for lclVar nodes. `DoPreOrder` must be true if this option is true.</summary>
    static virtual bool DoLclVarsOnly => false;

    /// <summary>when true, then walker will visit a node's operands in execution order (e.g. if a binary operator has the `GTF_REVERSE_OPS` flag set, the second operand will be visited before the first).</summary>
    static virtual bool UseExecutionOrder => false;

    protected static fgWalkResult WalkTree(ref TSelf self, ref GenTree use, GenTree? user, GenTreeStack ancestors)
    {
        assert(TSelf.DoPreOrder || TSelf.DoPostOrder);
        assert(!TSelf.DoLclVarsOnly || TSelf.DoPreOrder);

        var node = use;

        if (TSelf.ComputeStack)
        {
            ancestors.Push(node);
        }

        var result = fgWalkResult.WALK_CONTINUE;

        if (TSelf.DoPreOrder && !TSelf.DoLclVarsOnly)
        {
            result = self.PreOrderVisit(ref use, user);

            if (result is not fgWalkResult.WALK_ABORT)
            {
                node = use;
            }
        }

        if ((node is not null) && (result is not fgWalkResult.WALK_SKIP_SUBTREES and not fgWalkResult.WALK_ABORT))
        {
            var oper = node.Oper;

            if (oper.IsLeaf)
            {
                if (TSelf.DoLclVarsOnly && (oper is GT_LCL_VAR or GT_LCL_FLD or GT_LCL_ADDR))
                {
                    result = self.PreOrderVisit(ref use, user);
                }
            }
            else if (oper.IsBinary)
            {
                var op = node.AsOp();

                ref var op1Use = ref op.Op1Ref;
                ref var op2Use = ref op.Op2Ref;

                if (TSelf.UseExecutionOrder && node.IsReverseOp)
                {
                    (op1Use, op2Use) = (op2Use, op1Use);
                }

                if (op1Use is not null)
                {
                    result = WalkTree(ref self, ref op1Use, op, ancestors);
                }
                else
                {
#if DEBUG
                    assert(op.IsNullOp1Legal);
#endif
                }

                // We can have null op1 and non-null op2 for some nodes, such as GT_LEA

                if ((result is not fgWalkResult.WALK_ABORT) && (op2Use is not null))
                {
                    result = WalkTree(ref self, ref op2Use, op, ancestors);
                }
                else
                {
#if DEBUG
                    assert(op.IsNullOp2Legal);
#endif
                }
            }
            else if (oper.IsUnary)
            {
                if (TSelf.DoLclVarsOnly && oper.IsLocalStore)
                {
                    result = self.PreOrderVisit(ref use, user);
                }

                var unOp = node.AsUnOp();

                if ((result is not fgWalkResult.WALK_ABORT) && (unOp.Op1 is not null))
                {
                    result = WalkTree(ref self, ref unOp.Op1Ref, unOp, ancestors);
                }
                else
                {
#if DEBUG
                    assert(unOp.IsNullOp1Legal);
#endif
                }
            }
            else
            {
                assert(oper.IsSpecial);

                switch (oper)
                {
                    case GT_PHI:
                    {
                        var phi = node.AsPhi();

                        foreach (var phiUse in phi.Uses)
                        {
                            result = WalkTree(ref self, ref phiUse.NodeRef, phi, ancestors);

                            if (result is fgWalkResult.WALK_ABORT)
                            {
                                break;
                            }
                        }
                        break;
                    }

                    case GT_CMPXCHG:
                    {
                        var cmpXchg = node.AsCmpXchg();
                        result = WalkTree(ref self, ref cmpXchg.AddrRef, cmpXchg, ancestors);

                        if (result is not fgWalkResult.WALK_ABORT)
                        {
                            result = WalkTree(ref self, ref cmpXchg.DataRef, cmpXchg, ancestors);

                            if (result is not fgWalkResult.WALK_ABORT)
                            {
                                result = WalkTree(ref self, ref cmpXchg.ComparandRef, cmpXchg, ancestors);
                            }
                        }
                        break;
                    }

                    case GT_SELECT:
                    {
                        var conditional = node.AsConditional();
                        result = WalkTree(ref self, ref conditional.CondRef, conditional, ancestors);

                        if (result is not fgWalkResult.WALK_ABORT)
                        {
                            result = WalkTree(ref self, ref conditional.Op1Ref, conditional, ancestors);

                            if (result is not fgWalkResult.WALK_ABORT)
                            {
                                result = WalkTree(ref self, ref conditional.Op2Ref, conditional, ancestors);
                            }
                        }
                        break;
                    }

#if (FEATURE_HW_INTRINSICS)
                    case GT_HWINTRINSIC:
                    {
                        var hwintrinsic = node.AsHWIntrinsic();
                        var operands = hwintrinsic.Operands;

                        if (TSelf.UseExecutionOrder && node.IsReverseOp)
                        {
                            assert(operands.Length == 2);
                            result = WalkTree(ref self, ref operands[1], hwintrinsic, ancestors);

                            if (result is not fgWalkResult.WALK_ABORT)
                            {
                                result = WalkTree(ref self, ref operands[0], hwintrinsic, ancestors);
                            }
                        }
                        else
                        {
                            foreach (ref var operand in operands)
                            {
                                result = WalkTree(ref self, ref operand, hwintrinsic, ancestors);

                                if (result is fgWalkResult.WALK_ABORT)
                                {
                                    break;
                                }
                            }
                        }
                        break;
                    }
#endif

                    case GT_ARR_ELEM:
                    {
                        var arrElem = node.AsArrElem();
                        result = WalkTree(ref self, ref arrElem.ArrObjRef, arrElem, ancestors);

                        if (result is not fgWalkResult.WALK_ABORT)
                        {
                            var arrInds = arrElem.ArrInds;

                            foreach (ref var arrInd in arrInds[..arrElem.ArrRank])
                            {
                                result = WalkTree(ref self, ref arrInd, arrElem, ancestors);

                                if (result is fgWalkResult.WALK_ABORT)
                                {
                                    break;
                                }
                            }
                        }
                        break;
                    }

                    case GT_CALL:
                    {
                        var call = node.AsCall();
                        ref var args = ref call.Args;

                        foreach (var arg in args.EarlyArgs)
                        {
                            result = WalkTree(ref self, ref arg.EarlyNodeRef, call, ancestors);

                            if (result is fgWalkResult.WALK_ABORT)
                            {
                                break;
                            }
                        }

                        if (result is not fgWalkResult.WALK_ABORT)
                        {
                            foreach (var arg in args.LateArgs)
                            {
                                result = WalkTree(ref self, ref arg.LateNodeRef, call, ancestors);

                                if (result is fgWalkResult.WALK_ABORT)
                                {
                                    break;
                                }
                            }

                            if ((result is not fgWalkResult.WALK_ABORT) && (call.ControlExpr is not null))
                            {
                                result = WalkTree(ref self, ref call.ControlExprRef, call, ancestors);
                            }
                        }
                        break;
                    }

                    case GT_FIELD_LIST:
                    {
                        var fieldList = node.AsFieldList();

                        foreach (var fieldListUse in fieldList.Uses)
                        {
                            result = WalkTree(ref self, ref fieldListUse.NodeRef, node, ancestors);

                            if (result is fgWalkResult.WALK_ABORT)
                            {
                                break;
                            }
                        }
                        break;
                    }
                }
            }
        }

        if (result is not fgWalkResult.WALK_ABORT)
        {
            if (TSelf.DoPostOrder)
            {
                // Finally, visit the current node
                result = self.PostOrderVisit(ref use, user);
            }

            if (TSelf.ComputeStack)
            {
                ancestors.Pop();
            }
        }
        return result;
    }

    fgWalkResult WalkTree(ref GenTree use, GenTree? user);

    protected fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user);

    protected fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user);
}
