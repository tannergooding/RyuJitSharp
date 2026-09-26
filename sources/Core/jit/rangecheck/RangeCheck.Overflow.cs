// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class RangeCheck
{
    private int GetArrLength(ValueNum lengthVN)
    {
        var store = _compiler.vnStore;
        assert(store is not null);
        var app = new VNFuncApp();
        var arrayVN = (lengthVN != ValueNumStore.NoVN) &&
            store.GetVNFunc(lengthVN, ref app) && app.FuncIs(VNF_ARR_LENGTH, VNF_MDArrLength)
                ? app.GetArg(0) : ValueNumStore.NoVN;
        return store.TryGetNewArrSize(arrayVN, out var size) ? size : 0;
    }

    private bool GetLimitMax(Limit limit, out int maximum)
    {
        maximum = 0;
        if (limit.IsConstant)
        {
            maximum = limit.Constant;
            return true;
        }

        if (!limit.IsBinOpArray)
        {
            return false;
        }

        var size = GetArrLength(limit.VN);
        if (size <= 0)
        {
            var store = _compiler.vnStore;
            assert(store is not null);
            size = store.IsVNArrLen(limit.VN) ? CORINFO_Array_MaxLength : int.MaxValue;
        }

        return CheckedOps.TryAdd(size, limit.Constant, out maximum);
    }

    private bool AddOverflows(Limit first, Limit second)
    {
        return !GetLimitMax(first, out var maxFirst) || !GetLimitMax(second, out var maxSecond) ||
            !CheckedOps.TryAdd(maxFirst, maxSecond, out _);
    }

    private bool MultiplyOverflows(Limit first, Limit second)
    {
        return !GetLimitMax(first, out var maxFirst) || !GetLimitMax(second, out var maxSecond) ||
            !CheckedOps.TryMul(maxFirst, maxSecond, out _);
    }

    private bool DoesBinOpOverflow(BasicBlock block, GenTreeOp binary, Range range)
    {
        var left = binary.Op1;
        var right = binary.Op2;
        if ((!_searchPath.ContainsKey(left) && ComputeDoesOverflow(block, left, range)) ||
            (!_searchPath.ContainsKey(right) && ComputeDoesOverflow(block, right, range)))
        {
            return true;
        }

        if (!_rangeMap.TryGetValue(left, out var leftRange) || !_rangeMap.TryGetValue(right, out var rightRange))
        {
            return true;
        }

        if (binary.Oper is GT_ADD)
        {
            return AddOverflows(leftRange.UpperLimit, rightRange.UpperLimit);
        }

        if (binary.Oper is GT_MUL)
        {
            return MultiplyOverflows(leftRange.UpperLimit, rightRange.UpperLimit);
        }

        if (binary.Oper is GT_LSH)
        {
            var multiplier = RangeOps.ConvertShiftToMultiply(rightRange);
            return MultiplyOverflows(leftRange.UpperLimit, multiplier.UpperLimit);
        }

        if (binary.Oper is GT_OR && leftRange.LowerLimit.IsConstant && rightRange.LowerLimit.IsConstant &&
            (leftRange.LowerLimit.Constant >= 0) && (rightRange.LowerLimit.Constant >= 0))
        {
            return false;
        }

        return true;
    }

    private bool DoesVarDefOverflow(BasicBlock block, GenTreeLclVarCommon local, Range range)
    {
        var definition = GetSsaDefStore(local);
        if (definition is null)
        {
            return !((local.SsaNum == SsaConfig.FIRST_SSA_NUM) && _compiler.lvaGetDesc(local.LclNum).lvIsParam);
        }

        var assertionRange = new Range(new Limit(LimitType.Unknown));
        MergeAssertion(block, local, ref assertionRange);
        var merged = RangeOps.Merge(range, assertionRange, false);
        if (merged.LowerLimit.Equals(range.LowerLimit) && merged.UpperLimit.Equals(range.UpperLimit))
        {
            return false;
        }

        var def = definition.Value;
        assert((def.Block is not null) && (def.DefNode is not null));
        return ComputeDoesOverflow(def.Block, def.DefNode.Data, range);
    }

    private bool DoesPhiOverflow(BasicBlock block, GenTree tree, Range range)
    {
        foreach (var use in tree.AsPhi().Uses)
        {
            var argument = use.Node;
            if (!_searchPath.ContainsKey(argument) && ComputeDoesOverflow(block, argument, range))
            {
                return true;
            }
        }

        return false;
    }

    private bool ComputeDoesOverflow(BasicBlock block, GenTree tree, Range range)
    {
#if DEBUG
        JITDUMP($"Does overflow [{tree.TreeId:D6}]?\n");
#endif
        if (IsOverBudget)
        {
            return true;
        }

        _visitBudget--;
        _searchPath[tree] = block;
        var store = _compiler.vnStore;
        assert(store is not null);
        var overflows = true;
        if (_searchPath.Count > MaxSearchDepth)
        {
            overflows = true;
        }
        else if (store.IsVNConstant(tree._vnPair.Conservative) || tree.Oper is GT_IND or GT_ARR_LENGTH)
        {
            overflows = false;
        }
        else if (tree.Oper is GT_COMMA)
        {
            overflows = ComputeDoesOverflow(block, tree.EffectiveVal, range);
        }
        else if (tree.Oper.IsLocal)
        {
            overflows = DoesVarDefOverflow(block, tree.AsLclVarCommon(), range);
        }
        else if (tree.Oper is GT_ADD or GT_OR or GT_MUL or GT_LSH)
        {
            overflows = DoesBinOpOverflow(block, tree.AsOp(), range);
        }
        else if (tree.Oper is GT_AND or GT_RSH or GT_RSZ or GT_UMOD or GT_NEG)
        {
            overflows = false;
            foreach (var operand in tree.Operands)
            {
                if (!_searchPath.ContainsKey(operand) && ComputeDoesOverflow(block, operand, range))
                {
                    overflows = true;
                    break;
                }
            }
        }
        else if ((tree.Oper is GT_XOR) && store.IsVNLog2(store.VNNormalValue(tree._vnPair.Conservative)))
        {
            overflows = false;
        }
        else if (tree.Oper is GT_PHI)
        {
            overflows = DoesPhiOverflow(block, tree, range);
        }
        else if (tree.Oper is GT_CAST)
        {
            overflows = ComputeDoesOverflow(block, tree.AsUnOp().Op1, range);
        }

        _ = _searchPath.Remove(tree);
#if DEBUG
        JITDUMP($"[{tree.TreeId:D6}] {(overflows ? "overflows" : "does not overflow")}\n");
#endif
        return overflows;
    }
}
