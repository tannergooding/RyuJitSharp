// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class RangeCheck
{
    private LclSsaVarDsc? GetSsaDefStore(GenTreeLclVarCommon local)
    {
        if ((local.Oper is GT_LCL_FLD) || (local.SsaNum == SsaConfig.RESERVED_SSA_NUM))
        {
            return null;
        }

        var descriptor = _compiler.lvaGetDesc(local.LclNum);
        var definition = descriptor.GetPerSsaData(local.SsaNum);
        if (definition.DefNode is null)
        {
            if (descriptor.lvIsParam && (local.SsaNum == SsaConfig.FIRST_SSA_NUM))
            {
                assert(definition.Block == _compiler.fgFirstBB);
            }

            return null;
        }

        if ((definition.DefNode.Oper is not GT_STORE_LCL_VAR) || !definition.DefNode.HasSsaName)
        {
            return null;
        }

        return definition;
    }

    private bool IsMonotonicallyIncreasing(GenTree tree, bool rejectNegativeConstant)
    {
        if (IsOverBudget)
        {
            return false;
        }

        _visitBudget--;
        if (_searchPath.ContainsKey(tree))
        {
            return true;
        }

        _searchPath[tree] = null;
        try
        {
            if (_searchPath.Count > MaxSearchDepth)
            {
                return false;
            }

            var store = _compiler.vnStore;
            assert(store is not null);
            var vn = tree._vnPair.Conservative;
            if (store.IsVNInt32Constant(vn))
            {
                return !rejectNegativeConstant || (store.ConstantValue<int>(vn) >= 0);
            }

            if (tree.Oper.IsLocal)
            {
                var definition = GetSsaDefStore(tree.AsLclVarCommon());
                return definition is not null &&
                    IsMonotonicallyIncreasing(definition.Value.DefNode!.Data, rejectNegativeConstant);
            }

            if (tree.Oper is GT_ADD)
            {
                var left = tree.AsOp().Op1;
                var right = tree.AsOp().Op2;
                if (right.Oper is GT_LCL_VAR)
                {
                    (left, right) = (right, left);
                }

                if (left.Oper is not GT_LCL_VAR)
                {
                    return false;
                }

                if (right.Oper is GT_LCL_VAR)
                {
                    return IsMonotonicallyIncreasing(left, true) && IsMonotonicallyIncreasing(right, true);
                }

                return right.Oper is GT_CNS_INT && (right.AsIntCon().IconValue >= 0) &&
                    IsMonotonicallyIncreasing(left, false);
            }

            if (tree.Oper is GT_PHI)
            {
                foreach (var use in tree.AsPhi().Uses)
                {
                    if (!_searchPath.ContainsKey(use.Node) &&
                        !IsMonotonicallyIncreasing(use.Node, rejectNegativeConstant))
                    {
                        return false;
                    }
                }

                return true;
            }

            return tree.Oper is GT_COMMA &&
                IsMonotonicallyIncreasing(tree.EffectiveVal, rejectNegativeConstant);
        }
        finally
        {
            _ = _searchPath.Remove(tree);
        }
    }

    private void Widen(BasicBlock block, GenTree tree, ref Range range)
    {
        if ((range.LowerLimit.IsDependent || range.LowerLimit.IsUnknown) &&
            IsMonotonicallyIncreasing(tree, false))
        {
#if DEBUG
            JITDUMP($"[{tree.TreeId:D6}] is monotonically increasing.\n");
#endif
            _rangeMap.Clear();
            range = GetRangeWorker(block, tree, true);
        }
    }

    private void MergeEdgeAssertions(GenTreeLclVarCommon local, ASSERT_TP? assertions, ref Range range)
    {
        if (local.SsaNum == SsaConfig.RESERVED_SSA_NUM)
        {
            return;
        }

        var store = _compiler.vnStore;
        assert(store is not null);
        var definition = _compiler.lvaGetDesc(local.LclNum).GetPerSsaData(local.SsaNum);
        var normalVN = store.VNNormalValue(definition._vnPair.Conservative);
        MergeEdgeAssertions(_compiler, normalVN, _preferredBound, assertions, ref range);
    }

    private void MergeAssertion(BasicBlock block, GenTree tree, ref Range range)
    {
#if DEBUG
        JITDUMP($"Merging assertions from pred edges of BB{block.bbNum:D2} for op [{tree.TreeId:D6}]\n");
#endif
        if (_compiler.AssertionCount < 1)
        {
            return;
        }

        ASSERT_TP? assertions = null;
        if (tree.Oper is GT_PHI_ARG)
        {
            var predecessor = tree.AsPhiArg().PredBB;
            if ((predecessor.bbPreds is null) && (predecessor != _compiler.fgFirstBB) &&
                !_compiler.bbIsHandlerBeg(predecessor))
            {
                return;
            }

            assertions = _compiler.optGetEdgeAssertions(block, predecessor);
        }
        else if (tree.Oper.IsLocal)
        {
            assertions = block.bbAssertionIn;
            if (block.HasFlag(BBF_MAY_HAVE_BOUNDS_CHECKS))
            {
                assert(_compiler.apTraits is not null);
                assertions = BitVecOps.MakeCopy(_compiler.apTraits, assertions);
                var budget = 50;
                foreach (var statement in block.Statements)
                {
                    var visitor = new AssertionsAccumulator(_compiler, budget, assertions, tree);
                    var root = statement.RootNode;
                    var result = visitor.WalkTree(ref root, null);
                    budget = visitor.Budget;
                    if (result is Compiler.WALK_ABORT)
                    {
                        break;
                    }
                }
            }
        }

        if (!BitVecOps.MaybeUninit(assertions))
        {
            MergeEdgeAssertions(tree.AsLclVarCommon(), assertions, ref range);
        }
    }

    private struct AssertionsAccumulator : IGenTreeVisitor<AssertionsAccumulator>
    {
        public static bool DoPostOrder => true;
        public static bool UseExecutionOrder => true;

        private readonly Compiler _compiler;
        private readonly ASSERT_TP _assertions;
        private readonly GenTree _target;
        private readonly GenTreeStack _ancestors;

        public int Budget { get; private set; }

        public AssertionsAccumulator(Compiler compiler, int budget, ASSERT_TP assertions, GenTree target)
        {
            _compiler = compiler;
            _assertions = assertions;
            _target = target;
            _ancestors = [];
            Budget = budget;
        }

        public readonly Compiler.fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
            => Compiler.WALK_CONTINUE;

        public Compiler.fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user)
        {
            if (use == _target)
            {
                return Compiler.WALK_ABORT;
            }

            Budget--;
            if (Budget <= 0)
            {
                return Compiler.WALK_ABORT;
            }

            if (use.GeneratesAssertion)
            {
                assert(_compiler.apTraits is not null);
                BitVecOps.AddElemD(_compiler.apTraits, _assertions, use.AssertionInfo.AssertionIndex - 1);
            }

            return Compiler.WALK_CONTINUE;
        }

        public Compiler.fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<AssertionsAccumulator>.WalkTree(ref this, ref use, user, _ancestors);
    }
}
