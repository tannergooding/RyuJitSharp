// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class NaturalLoopIterInfo
{
    public int IterVar = BAD_VAR_NUM;
    public int LimitVar = BAD_VAR_NUM;
#if DEBUG
    public GenTree? InitTree;
#endif
    public int ConstInitValue;
    public GenTree? TestTree;
    public BasicBlock? TestBlock;
    public GenTree? IterTree;
    public bool ExitedOnTrue;
    public bool HasConstInit;
    public bool HasConstLimit;
    public bool HasSimdLimit;
    public bool HasInvariantLocalLimit;
    public bool HasArrayLengthLimit;
    public bool NeedsZeroTripGuard;
    public int LimitOffset;

    public int IterConst()
    {
        assert(IterTree is not null);
        return unchecked((int)IterTree.AsLclVar().Data.AsOp().Op2.AsIntCon().IconValue);
    }

    public genTreeOps IterOper()
    {
        assert(IterTree is not null);
        return IterTree.AsLclVar().Data.Oper;
    }

    public var_types IterOperType()
    {
        assert(IterTree is not null);
        assert(IterTree.Type.ActualType is TYP_INT);
        return IterTree.Type;
    }

    private bool IsReversed()
    {
        assert(TestTree is not null);
        var op2 = TestTree.AsOp().Op2;
        return op2.Oper.IsScalarLocal && (op2.AsLclVarCommon().LclNum == IterVar);
    }

    public genTreeOps TestOper()
    {
        assert(TestTree is not null);
        var oper = TestTree.Oper;
        if (IsReversed())
        {
            oper = oper.SwapRelop;
        }
        if (ExitedOnTrue)
        {
            oper = oper.ReverseRelop;
        }
        return oper;
    }

    public bool IsIncreasingLoop()
    {
        var testOper = TestOper();
        if (testOper is GT_LT or GT_LE)
        {
            return (IterOper() is GT_ADD && IterConst() > 0) ||
                (IterOper() is GT_SUB && IterConst() < 0);
        }
        if (testOper is GT_NE)
        {
            return (IterOper() is GT_ADD && IterConst() == 1) ||
                (IterOper() is GT_SUB && IterConst() == -1);
        }
        return false;
    }

    public bool IsDecreasingLoop()
    {
        var testOper = TestOper();
        if (testOper is GT_GT or GT_GE)
        {
            return (IterOper() is GT_ADD && IterConst() < 0) ||
                (IterOper() is GT_SUB && IterConst() > 0);
        }
        if (testOper is GT_NE)
        {
            return (IterOper() is GT_ADD && IterConst() == -1) ||
                (IterOper() is GT_SUB && IterConst() == 1);
        }
        return false;
    }

    public GenTree Iterator()
    {
        assert(TestTree is not null);
        return IsReversed() ? TestTree.AsOp().Op2 : TestTree.AsOp().Op1;
    }

    public GenTree Limit()
    {
        assert(TestTree is not null);
        return IsReversed() ? TestTree.AsOp().Op1 : TestTree.AsOp().Op2;
    }

    public GenTree LimitBase()
    {
        var limit = Limit();
        if (LimitOffset == 0)
        {
            return limit;
        }
        assert(limit.Oper is GT_ADD or GT_SUB && limit.Type is TYP_INT);
        var op = limit.AsOp();
        if (op.Op2.Oper.IsCnsIntOrI)
        {
            return op.Op1;
        }
        assert(op.Op1.Oper.IsCnsIntOrI && limit.Oper is GT_ADD);
        return op.Op2;
    }

    public int ConstLimit()
    {
        assert(HasConstLimit && LimitOffset == 0);
        return unchecked((int)LimitBase().AsIntCon().IconValue);
    }

    public int VarLimit()
    {
        assert(HasInvariantLocalLimit);
        return LimitBase().AsLclVarCommon().LclNum;
    }
}
