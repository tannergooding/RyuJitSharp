// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, rangecheck.cpp.

namespace RyuJitSharp;

public sealed partial class RangeCheck
{
    public bool BetweenBounds(Range range, GenTree upper, int arraySize)
    {
        assert(range.IsValid());
        var store = _compiler.vnStore;
        assert(store is not null);
        var upperVN = store.VNNormalValue(upper._vnPair.Conservative);
#if DEBUG
        if (_compiler.verbose)
        {
            jitprintf($"{range} BetweenBounds <0, [{upper.TreeId:D6}]>\n");
            jitprintf($"{upperVN} upper bound is: ");
            store.vnDump(_compiler, upperVN);
            jitprintf("\n");
        }
#endif
        if ((arraySize <= 0) && !store.IsVNCheckedBound(upperVN))
        {
            return false;
        }

        JITDUMP($"Array size is: {arraySize}\n");
        if (range.UpperLimit.IsBinOpArray)
        {
            if (range.UpperLimit.VN != upperVN)
            {
                return false;
            }

            var upperOffset = range.UpperLimit.GetConstant();
            if (upperOffset >= 0)
            {
                return false;
            }

            if (range.LowerLimit.IsConstant && (range.LowerLimit.GetConstant() >= 0))
            {
                return true;
            }

            if (arraySize <= 0)
            {
                return false;
            }

            if (range.LowerLimit.IsBinOpArray)
            {
                var lowerOffset = range.LowerLimit.GetConstant();
                assert(arraySize > 0);
                if ((lowerOffset >= 0) || (lowerOffset < -arraySize))
                {
                    return false;
                }

                return (range.LowerLimit.VN == upperVN) && (lowerOffset <= upperOffset);
            }
        }
        else if (range.UpperLimit.IsConstant)
        {
            if (arraySize <= 0)
            {
                return false;
            }

            var upperConstant = range.UpperLimit.GetConstant();
            if (upperConstant >= arraySize)
            {
                return false;
            }

            if (range.LowerLimit.IsConstant)
            {
                var lowerConstant = range.LowerLimit.GetConstant();
                return (lowerConstant >= 0) && (lowerConstant <= upperConstant);
            }

            if (range.LowerLimit.IsBinOpArray)
            {
                var lowerOffset = range.LowerLimit.GetConstant();
                assert(arraySize > 0);
                if ((lowerOffset >= 0) || (lowerOffset < -arraySize))
                {
                    return false;
                }

                return (range.LowerLimit.VN == upperVN) &&
                    ((arraySize + lowerOffset) <= upperConstant);
            }
        }

        return false;
    }

    public void OptimizeRangeCheck(BasicBlock block, Statement statement, GenTree parent)
    {
        var isComma = parent.Oper is GT_COMMA;
        if (!isComma && (parent != statement.RootNode))
        {
            return;
        }

        var tree = isComma ? parent.AsOp().Op1 : parent;
        if (tree.Oper is not GT_BOUNDS_CHECK)
        {
            return;
        }

        var check = tree.AsBoundsChk();
        var store = _compiler.vnStore;
        assert(store is not null);
        var arrayLengthVN = _compiler.optConservativeNormalVN(check.ArrayLength);
        if (!TryGetRange(block, check.Index, out var range, arrayLengthVN))
        {
            JITDUMP("Failed to get range\n");
            return;
        }

        var arraySizeRange = GetRangeFromAssertions(_compiler, check.ArrayLength, block.bbAssertionIn);
        if (arraySizeRange.IsConstantRange())
        {
            var arraySize = arraySizeRange.LowerLimit.GetConstant();
            if (BetweenBounds(range, check.ArrayLength, arraySize))
            {
                JITDUMP("[RangeCheck::OptimizeRangeCheck] Between bounds\n");
                _ = _compiler.optRemoveRangeCheck(check, isComma ? parent : null, statement);
                _updateStmt = true;
            }
        }
    }

    public bool OptimizeRangeChecks()
    {
        _visitBudget = MaxVisitBudget;
        _preferredBound = ValueNumStore.NoVN;
        var madeChanges = false;

        foreach (var block in _compiler.Blocks)
        {
            foreach (var statement in block.Statements)
            {
                _updateStmt = false;
                foreach (var tree in statement.TreeList)
                {
                    if (IsOverBudget && !_updateStmt)
                    {
                        return madeChanges;
                    }

                    if (tree.Oper is GT_BOUNDS_CHECK)
                    {
                        block.SetFlags(BBF_MAY_HAVE_BOUNDS_CHECKS);
                    }

                    OptimizeRangeCheck(block, statement, tree);
                }

                if (_updateStmt)
                {
                    _compiler.gtSetStmtInfo(statement);
                    _compiler.fgSetStmtSeq(statement);
                    madeChanges = true;
                }
            }
        }

        return madeChanges;
    }
}
