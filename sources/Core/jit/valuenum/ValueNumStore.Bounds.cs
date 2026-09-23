// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

public sealed partial class ValueNumStore
{
    private readonly HashSet<ValueNum> _checkedBoundVNs = [];
    private readonly HashSet<ValueNum> _checkedBoundIndexVNs = [];

    public readonly record struct UnsignedCompareCheckedBoundInfo(VNFunc CmpOper, ValueNum VNIdx, ValueNum VNBound)
    {
        public UnsignedCompareCheckedBoundInfo() : this(VNF_NONE, NoVN, NoVN)
        {
        }
    }

    public bool IsVNCheckedBound(ValueNum vn) => _checkedBoundVNs.Contains(vn) || IsVNArrLen(vn);

    public bool IsVNCheckedBoundIndex(ValueNum vn) => _checkedBoundIndexVNs.Contains(vn);

    public void SetVNIsCheckedBound(ValueNum vn, bool isIndex = false)
    {
        assert(!IsVNConstant(vn));
        _ = (isIndex ? _checkedBoundIndexVNs : _checkedBoundVNs).Add(vn);
    }

    public ValueNum VNForCastOper(var_types castToType, bool srcIsUnsigned)
    {
        assert(castToType != TYP_STRUCT);
        // Native reserves bit zero for unsigned source interpretation.
        return VNForIntCon(((int)castToType << 1) | (srcIsUnsigned ? 1 : 0));
    }

    public void GetCastOperFromVN(ValueNum vn, out var_types castToType, out bool srcIsUnsigned)
    {
        assert(IsVNInt32Constant(vn));
        var value = GetConstantInt32(vn);
        assert(value >= 0);
        srcIsUnsigned = (value & 1) != 0;
        castToType = (var_types)(value >> 1);
        assert(VNForCastOper(castToType, srcIsUnsigned) == vn);
    }

    public bool IsVNCastToULong(ValueNum vn, ref ValueNum castedOp)
    {
        var src = NoVN;
        var castInfo = NoVN;
        if (IsVNBinFunc(vn, VNF_Cast, ref src, ref castInfo))
        {
            GetCastOperFromVN(castInfo, out var castToType, out var srcIsUnsigned);
            if (srcIsUnsigned && (castToType == TYP_LONG))
            {
                castedOp = src;
                return true;
            }
        }

        return false;
    }

    public static VNFunc SwapRelop(VNFunc func)
    {
        if (func >= VNF_Boundary)
        {
            return func switch {
                VNF_LT_UN => VNF_GT_UN,
                VNF_LE_UN => VNF_GE_UN,
                VNF_GE_UN => VNF_LE_UN,
                VNF_GT_UN => VNF_LT_UN,
                _ => VNF_MemOpaque,
            };
        }

        var oper = (genTreeOps)func;
        return oper.IsCompare ? (VNFunc)oper.SwapRelop : VNF_MemOpaque;
    }

    private bool IsVNPositiveInt32Constant(ValueNum vn) => IsVNInt32Constant(vn) && (ConstantValue<int>(vn) > 0);

    public bool IsVNUnsignedCompareCheckedBound(ValueNum vn, ref UnsignedCompareCheckedBoundInfo info)
    {
        var app = new VNFuncApp();
        if (GetVNFunc(vn, ref app))
        {
            if (app.FuncIs(VNF_LT_UN, VNF_GE_UN))
            {
                var bound = app.GetArg(1);
                var index = app.GetArg(0);
                var castOp = NoVN;
                if (IsVNCheckedBound(bound) || (IsVNCastToULong(bound, ref castOp) && IsVNCheckedBound(castOp)))
                {
                    info = new(app.Func, index, castOp != NoVN ? castOp : bound);
                    return true;
                }
                else if (IsVNPositiveInt32Constant(bound) && IsVNCheckedBound(index))
                {
                    // len >= constant becomes (constant - 1) < len; likewise for its negation.
                    var validIndex = ConstantValue<int>(bound) - 1;
                    assert(validIndex >= 0);
                    info = new(app.FuncIs(VNF_GE_UN) ? VNF_LT_UN : VNF_GE_UN, VNForIntCon(validIndex), index);
                    return true;
                }
            }
            else if (app.FuncIs(VNF_GT_UN, VNF_LE_UN))
            {
                var bound = app.GetArg(0);
                var index = app.GetArg(1);
                var castOp = NoVN;
                if (IsVNCheckedBound(bound) || (IsVNCastToULong(bound, ref castOp) && IsVNCheckedBound(castOp)))
                {
                    info = new(app.FuncIs(VNF_GT_UN) ? VNF_LT_UN : VNF_GE_UN, index, castOp != NoVN ? castOp : bound);
                    return true;
                }
                else if (IsVNPositiveInt32Constant(bound) && IsVNCheckedBound(index))
                {
                    // constant <= len becomes (constant - 1) < len.
                    var validIndex = ConstantValue<int>(bound) - 1;
                    assert(validIndex >= 0);
                    info = new(app.FuncIs(VNF_LE_UN) ? VNF_LT_UN : VNF_GE_UN, VNForIntCon(validIndex), index);
                    return true;
                }
            }
        }

        return false;
    }

    public bool IsVNCheckedBoundAddConst(ValueNum vn, ref ValueNum checkedBoundVN, ref int addCns)
    {
        var app = new VNFuncApp();
        if (GetVNFunc(vn, ref app) && app.FuncIs(VNF_ADD, VNF_SUB) && IsVNCheckedBound(app.GetArg(0)) &&
            IsVNIntegralConstant(app.GetArg(1), out int constant))
        {
            if (app.FuncIs(VNF_SUB))
            {
                if (constant == int.MinValue)
                {
                    return false;
                }

                constant = -constant;
            }

            checkedBoundVN = app.GetArg(0);
            addCns = constant;
            return true;
        }

        return false;
    }
}
