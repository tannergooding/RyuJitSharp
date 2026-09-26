// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class RangeCheck
{
    public bool TryGetRange(BasicBlock block, GenTree tree, out Range range,
        ValueNum preferredBoundVN = ValueNumStore.NoVN)
    {
        if (tree.IsIntCnsFitsInI32)
        {
            range = new(new(LimitType.Constant, (int)tree.AsIntCon().IconValue));
            return true;
        }

        _preferredBound = preferredBoundVN;
        _rangeMap.Clear();
        _searchPath.Clear();

        var computed = GetRangeWorker(block, tree, false);
        assert(computed.IsValid());
        if (computed.UpperLimit.IsUnknown || computed.LowerLimit.IsUnknown)
        {
            range = default;
            JITDUMP("Range is completely unknown.\n");
            return false;
        }

        if (ComputeDoesOverflow(block, tree, computed))
        {
            range = default;
            JITDUMP("Range determined to overflow.\n");
            return false;
        }

#if DEBUG
        JITDUMP($"Range value {computed}\n");
#endif
        _searchPath.Clear();
        Widen(block, tree, ref computed);
        if (computed.UpperLimit.IsUnknown || computed.LowerLimit.IsUnknown)
        {
            range = default;
            return false;
        }

        range = computed;
        return true;
    }

    private Range GetRangeWorker(BasicBlock block, GenTree tree, bool monotonicallyIncreasing)
    {
#if DEBUG
        if (_compiler.verbose)
        {
            JITDUMP($"[RangeCheck::GetRangeWorker] BB{block.bbNum:D2} ");
            _compiler.gtDispTree(tree);
            JITDUMP("{\n");
        }
#endif
        var found = _rangeMap.TryGetValue(tree, out var cached);
        var range = found ? cached : ComputeRange(block, tree, monotonicallyIncreasing);
#if DEBUG
        JITDUMP($"   {(found ? "Cached" : "Computed")} Range [{tree.TreeId:D6}] => {range}\n}}\n");
#endif
        return range;
    }

    private Range ComputeRange(BasicBlock block, GenTree tree, bool monotonicallyIncreasing)
    {
        var newlyAdded = !_searchPath.ContainsKey(tree);
        _searchPath[tree] = block;
        var result = new Range(new Limit(LimitType.Undef));
        var store = _compiler.vnStore;
        assert(store is not null);
        var vn = store.VNNormalValue(tree._vnPair.Conservative);
        if (newlyAdded)
        {
            assert(!_rangeMap.ContainsKey(tree));
            _visitBudget--;
        }

        if (IsOverBudget)
        {
            result = new(new(LimitType.Unknown));
            JITDUMP("GetRangeWorker not tractable within max node visit budget.\n");
        }
        else if (_searchPath.Count > MaxSearchDepth)
        {
            result = new(new(LimitType.Unknown));
            JITDUMP("GetRangeWorker not tractable within max stack depth.\n");
        }
        else if (tree.Type.ActualType != TYP_INT)
        {
            result = GetRangeFromAssertions(_compiler, tree, block.bbAssertionIn);
        }
        else if (store.IsVNConstant(vn))
        {
            result = store.IsVNIntegralConstant(vn, out int constant)
                ? new(new(LimitType.Constant, constant))
                : new(new(LimitType.Unknown));
        }
        else if ((_preferredBound != ValueNumStore.NoVN) && (vn == _preferredBound))
        {
            result = new(new(LimitType.BinOpArray, _preferredBound, 0));
        }
        else if (tree.Oper.IsLocal)
        {
            var definition = GetSsaDefStore(tree.AsLclVarCommon());
            if (definition is null)
            {
                result = new(new(LimitType.Unknown));
            }
            else
            {
                var def = definition.Value;
                assert((def.Block is not null) && (def.DefNode is not null));
                result = GetRangeWorker(def.Block, def.DefNode.Data, monotonicallyIncreasing);
            }

            MergeAssertion(block, tree, ref result);
        }
        else if (tree.Oper is GT_XOR or GT_OR or GT_ADD or GT_AND or GT_RSH or GT_RSZ or GT_LSH or GT_UMOD or GT_MUL)
        {
            result = ComputeRangeForBinOp(block, tree.AsOp(), monotonicallyIncreasing);
        }
        else if (tree.Oper is GT_NEG)
        {
            result = RangeOps.Negate(GetRangeWorker(block, tree.AsUnOp().Op1, monotonicallyIncreasing));
        }
        else if (tree.Oper is GT_PHI)
        {
            foreach (var use in tree.AsPhi().Uses)
            {
                var arg = use.Node;
                var argRange = _searchPath.ContainsKey(arg)
                    ? new Range(new Limit(LimitType.Dependent))
                    : GetRangeWorker(block, arg, monotonicallyIncreasing);
                assert(!argRange.LowerLimit.IsUndef && !argRange.UpperLimit.IsUndef);
                MergeAssertion(block, arg, ref argRange);
                result = RangeOps.Merge(result, argRange, monotonicallyIncreasing);
            }
        }
        else if (tree.Oper is GT_COMMA)
        {
            result = GetRangeWorker(block, tree.EffectiveVal, monotonicallyIncreasing);
        }
        else if (tree.Oper is GT_ARR_LENGTH)
        {
            result = new(new(LimitType.Constant, 0), new(LimitType.Constant, CORINFO_Array_MaxLength));
        }
        else
        {
            result = GetRangeFromAssertions(_compiler, tree, block.bbAssertionIn);
        }

        _rangeMap[tree] = result;
        _ = _searchPath.Remove(tree);
        return result;
    }

    private Range ComputeRangeForBinOp(BasicBlock block, GenTreeOp binary, bool monotonicallyIncreasing)
    {
        assert(binary.Type.ActualType == TYP_INT);
        var store = _compiler.vnStore;
        assert(store is not null);
        if (binary.Oper is GT_XOR)
        {
            var upperBound = 0;
            return store.IsVNLog2(store.VNNormalValue(binary._vnPair.Conservative), ref upperBound)
                ? new(new(LimitType.Constant, 0), new(LimitType.Constant, upperBound))
                : new(new(LimitType.Unknown));
        }

        var left = binary.Op1;
        var right = binary.Op2;
        var leftIsConstant = store.IsVNConstant(left._vnPair.Conservative);
        var rightIsConstant = store.IsVNConstant(right._vnPair.Conservative);
        if (binary.Oper.IsCommutative && leftIsConstant && !rightIsConstant)
        {
            (left, right) = (right, left);
        }

        Range GetOperandRange(GenTree operand)
        {
            if (_rangeMap.TryGetValue(operand, out var cached))
            {
                return cached;
            }

            var value = _searchPath.ContainsKey(operand)
                ? new Range(new Limit(LimitType.Dependent))
                : GetRangeWorker(block, operand, monotonicallyIncreasing);
            MergeAssertion(block, operand, ref value);
            assert(value.IsValid());
            return value;
        }

        var leftRange = GetOperandRange(left);
        var rightRange = GetOperandRange(right);
        var result = binary.Oper switch
        {
            GT_ADD => RangeOps.Add(leftRange, rightRange),
            GT_MUL => RangeOps.Multiply(leftRange, rightRange),
            GT_RSH => RangeOps.ShiftRight(leftRange, rightRange, false),
            GT_RSZ => RangeOps.ShiftRight(leftRange, rightRange, true),
            GT_LSH => RangeOps.ShiftLeft(leftRange, rightRange),
            GT_AND => RangeOps.And(leftRange, rightRange),
            GT_OR => RangeOps.Or(leftRange, rightRange),
            GT_UMOD => RangeOps.UnsignedMod(leftRange, rightRange),
            _ => new(new(LimitType.Unknown)),
        };
#if DEBUG
        JITDUMP($"BinOp {leftRange} {binary.Oper} {rightRange} = {result}\n");
#endif
        if (!result.IsValid())
        {
#if DEBUG
            JITDUMP($"BinOp range is invalid: {result}\n");
#endif
            return new(new(LimitType.Unknown));
        }

        return result;
    }
}
