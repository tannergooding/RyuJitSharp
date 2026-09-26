// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class ValueNumStore
{
    public ValueNum VNForPtrSizeIntCon(target_ssize_t value)
    {
#if TARGET_64BIT
        return VNForLongCon(value);
#else
        return VNForIntCon(value);
#endif
    }

    public ValueNumPair VNPairForFunc(var_types type, VNFunc func)
    {
        var vn = VNForFunc(type, func);
        return new(vn, vn);
    }

    public ValueNumPair VNPairForFunc(var_types type, VNFunc func, ValueNumPair arg0, ValueNumPair arg1)
    {
        var liberal = VNForFunc(type, func, arg0.Liberal, arg1.Liberal);
        var conservative = arg0.BothEqual() && arg1.BothEqual()
            ? liberal : VNForFunc(type, func, arg0.Conservative, arg1.Conservative);
        return new(liberal, conservative);
    }

    public ValueNumPair VNPairForFuncNoFolding(var_types type, VNFunc func, ValueNumPair arg0, ValueNumPair arg1)
    {
        var liberal = VNForFuncNoFolding(type, func, arg0.Liberal, arg1.Liberal);
        var conservative = arg0.BothEqual() && arg1.BothEqual()
            ? liberal : VNForFuncNoFolding(type, func, arg0.Conservative, arg1.Conservative);
        return new(liberal, conservative);
    }

    public ValueNumPair VNPairForFunc(var_types type, VNFunc func, ValueNumPair arg0, ValueNumPair arg1,
        ValueNumPair arg2)
    {
        var liberal = VNForFunc(type, func, arg0.Liberal, arg1.Liberal, arg2.Liberal);
        var conservative = arg0.BothEqual() && arg1.BothEqual() && arg2.BothEqual()
            ? liberal : VNForFunc(type, func, arg0.Conservative, arg1.Conservative, arg2.Conservative);
        return new(liberal, conservative);
    }

    public ValueNumPair VNPairForFunc(var_types type, VNFunc func, ValueNumPair arg0, ValueNumPair arg1,
        ValueNumPair arg2, ValueNumPair arg3)
    {
        var liberal = VNForFunc(type, func, arg0.Liberal, arg1.Liberal, arg2.Liberal, arg3.Liberal);
        var conservative = arg0.BothEqual() && arg1.BothEqual() && arg2.BothEqual() && arg3.BothEqual()
            ? liberal : VNForFunc(type, func, arg0.Conservative, arg1.Conservative, arg2.Conservative,
                arg3.Conservative);
        return new(liberal, conservative);
    }

    public ValueNumPair VNPairForExpr(BasicBlock? block, var_types type)
    {
        var unique = VNForExpr(block, type);
        return new(unique, unique);
    }

    public ValueNum VNForCast(ValueNum source, var_types castToType, var_types castFromType,
        bool srcIsUnsigned = false, bool hasOverflowCheck = false)
    {
        if ((castFromType is TYP_I_IMPL) && (castToType is TYP_BYREF) && IsVNHandle(source))
        {
            return source;
        }

        var resultType = castToType.ActualType;
        if (!hasOverflowCheck && !varTypeIsFloating(castToType) && (castToType.Size <= castFromType.Size))
        {
            srcIsUnsigned = false;
        }

        VNUnpackExc(source, out var normal, out var exceptions);
        var castFunc = hasOverflowCheck ? VNF_CastOvf : VNF_Cast;
        var castType = VNForCastOper(castToType, srcIsUnsigned);
        var resultNormal = VNForFunc(resultType, castFunc, normal, castType);

        if (hasOverflowCheck && !IsVNConstant(resultNormal))
        {
            var overflow = VNForFunc(TYP_REF, VNF_ConvOverflowExc, normal, castType);
            exceptions = VNExcSetUnion(VNExcSetSingleton(overflow), exceptions);
        }

        return VNWithExc(resultNormal, exceptions);
    }

    public ValueNumPair VNPairForCast(ValueNumPair source, var_types castToType, var_types castFromType,
        bool srcIsUnsigned = false, bool hasOverflowCheck = false)
    {
        var liberal = VNForCast(source.Liberal, castToType, castFromType, srcIsUnsigned, hasOverflowCheck);
        var conservative = source.BothEqual() ? liberal :
            VNForCast(source.Conservative, castToType, castFromType, srcIsUnsigned, hasOverflowCheck);
        return new(liberal, conservative);
    }

    public ValueNum EncodeBitCastType(var_types castToType, ValueSize size)
    {
        if (!size.IsExact)
        {
            assert(varTypeHasUnknownSize(castToType));
            return VNForIntCon((int)castToType);
        }

        var exactSize = size.ExactSize;
        if (castToType is not TYP_STRUCT)
        {
            assert(exactSize == castToType.Size);
            return VNForIntCon((int)castToType);
        }

        assert(exactSize != 0);
        return VNForIntCon(unchecked((int)((uint)TYP_COUNT + exactSize)));
    }

    public var_types DecodeBitCastType(ValueNum castToType, out uint size)
    {
        var encoded = unchecked((uint)ConstantValue<int>(castToType));
        if (encoded < (uint)TYP_COUNT)
        {
            var type = (var_types)encoded;
            size = unchecked((uint)type.Size);
            return type;
        }

        size = encoded - (uint)TYP_COUNT;
        return TYP_STRUCT;
    }

    public ValueNum VNForBitCast(ValueNum source, var_types castToType, ValueSize size)
    {
        var app = new VNFuncApp();
        var found = GetVNFunc(source, ref app);
        if (found && app.FuncIs(VNF_BitCast))
        {
            source = app.GetArg(0);
        }

        var sourceType = TypeOfVN(source);
        if (sourceType == castToType)
        {
            return source;
        }

        assert((castToType is not TYP_STRUCT) || (sourceType is not TYP_STRUCT));
        if (found && app.FuncIs(VNF_ZeroObj))
        {
            return VNZeroForType(castToType);
        }

        return VNForFunc(castToType, VNF_BitCast, source, EncodeBitCastType(castToType, size));
    }

    public ValueNumPair VNPairForBitCast(ValueNumPair source, var_types castToType, ValueSize size)
    {
        var liberal = VNForBitCast(source.Liberal, castToType, size);
        var conservative = source.BothEqual() ? liberal : VNForBitCast(source.Conservative, castToType, size);
        return new(liberal, conservative);
    }

    public ValueNumPair VNPExcSetUnion(ValueNumPair left, ValueNumPair right)
        => new(VNExcSetUnion(left.Liberal, right.Liberal),
            VNExcSetUnion(left.Conservative, right.Conservative));

    public ValueNum VNExcSetIntersection(ValueNum left, ValueNum right)
    {
        if ((left == VNForEmptyExcSet()) || (right == VNForEmptyExcSet()))
        {
            return VNForEmptyExcSet();
        }

        var leftApp = new VNFuncApp();
        var foundLeft = GetVNFunc(left, ref leftApp);
        assert(foundLeft && leftApp.FuncIs(VNF_ExcSetCons));
        var rightApp = new VNFuncApp();
        var foundRight = GetVNFunc(right, ref rightApp);
        assert(foundRight && rightApp.FuncIs(VNF_ExcSetCons));

        var leftItem = leftApp.GetArg(0);
        var rightItem = rightApp.GetArg(0);
        if (unchecked((uint)leftItem) < unchecked((uint)rightItem))
        {
            assert(VNCheckAscending(leftItem, leftApp.GetArg(1)));
            return VNExcSetIntersection(leftApp.GetArg(1), right);
        }

        if (leftItem == rightItem)
        {
            assert(VNCheckAscending(leftItem, leftApp.GetArg(1)));
            assert(VNCheckAscending(rightItem, rightApp.GetArg(1)));
            return VNForFunc(TYP_REF, VNF_ExcSetCons, leftItem,
                VNExcSetIntersection(leftApp.GetArg(1), rightApp.GetArg(1)));
        }

        assert(VNCheckAscending(rightItem, rightApp.GetArg(1)));
        return VNExcSetIntersection(left, rightApp.GetArg(1));
    }

    public ValueNumPair VNPExcSetIntersection(ValueNumPair left, ValueNumPair right)
        => new(VNExcSetIntersection(left.Liberal, right.Liberal),
            VNExcSetIntersection(left.Conservative, right.Conservative));

    public bool VNExcIsSubset(ValueNum fullSet, ValueNum candidateSet)
    {
        if (candidateSet == VNForEmptyExcSet())
        {
            return true;
        }

        if ((fullSet == VNForEmptyExcSet()) || (fullSet == NoVN))
        {
            return false;
        }

        var fullApp = new VNFuncApp();
        var foundFull = GetVNFunc(fullSet, ref fullApp);
        assert(foundFull && fullApp.FuncIs(VNF_ExcSetCons));
        var candidateApp = new VNFuncApp();
        var foundCandidate = GetVNFunc(candidateSet, ref candidateApp);
        assert(foundCandidate && candidateApp.FuncIs(VNF_ExcSetCons));

        var previousFull = VNForNull();
        var previousCandidate = VNForNull();
        var remainingFull = fullApp.GetArg(1);
        var remainingCandidate = candidateApp.GetArg(1);

        while (true)
        {
            var fullItem = fullApp.GetArg(0);
            var candidateItem = candidateApp.GetArg(0);
            assert(unchecked((uint)fullItem) > unchecked((uint)previousFull));
            assert(unchecked((uint)candidateItem) >= unchecked((uint)previousCandidate));

            if (unchecked((uint)fullItem) > unchecked((uint)candidateItem))
            {
                return false;
            }

            if (fullItem == candidateItem)
            {
                if (remainingCandidate == VNForEmptyExcSet())
                {
                    return true;
                }

                foundCandidate = GetVNFunc(remainingCandidate, ref candidateApp);
                assert(foundCandidate && candidateApp.FuncIs(VNF_ExcSetCons));
                remainingCandidate = candidateApp.GetArg(1);
            }

            if (remainingFull == VNForEmptyExcSet())
            {
                return false;
            }

            foundFull = GetVNFunc(remainingFull, ref fullApp);
            assert(foundFull && fullApp.FuncIs(VNF_ExcSetCons));
            remainingFull = fullApp.GetArg(1);
            previousFull = fullItem;
            previousCandidate = candidateItem;
        }
    }

    public bool VNPExcIsSubset(ValueNumPair fullSet, ValueNumPair candidateSet)
        => VNExcIsSubset(fullSet.Liberal, candidateSet.Liberal) &&
            VNExcIsSubset(fullSet.Conservative, candidateSet.Conservative);

    public void VNPUnpackExc(ValueNumPair values, out ValueNumPair normal, out ValueNumPair exceptions)
    {
        VNUnpackExc(values.Liberal, out var liberal, out var liberalExc);
        VNUnpackExc(values.Conservative, out var conservative, out var conservativeExc);
        normal = new(liberal, conservative);
        exceptions = new(liberalExc, conservativeExc);
    }

    public ValueNum VNUnionExcSet(ValueNum value, ValueNum exceptions)
    {
        assert(value != NoVN);
        return VNExcSetUnion(VNExceptionSet(value), exceptions);
    }

    public ValueNumPair VNPUnionExcSet(ValueNumPair values, ValueNumPair exceptions)
        => new(VNUnionExcSet(values.Liberal, exceptions.Liberal),
            VNUnionExcSet(values.Conservative, exceptions.Conservative));

    public ValueNumPair VNPNormalPair(ValueNumPair values)
        => new(VNNormalValue(values.Liberal), VNNormalValue(values.Conservative));

    public ValueNum VNMakeNormalUnique(ValueNum value)
    {
        VNUnpackExc(value, out var normal, out var exceptions);
        return VNWithExc(VNForExpr(_compiler.compCurBB, TypeOfVN(normal)), exceptions);
    }

    public ValueNumPair VNPMakeNormalUniquePair(ValueNumPair values)
        => new(VNMakeNormalUnique(values.Liberal), VNMakeNormalUnique(values.Conservative));

    public ValueNum VNUniqueWithExc(var_types type, ValueNum exceptions)
    {
        var unique = VNForExpr(_compiler.compCurBB, type);
        if (exceptions == VNForEmptyExcSet())
        {
            return unique;
        }

#if DEBUG
        var app = new VNFuncApp();
        assert(GetVNFunc(exceptions, ref app) && app.FuncIs(VNF_ExcSetCons));
#endif
        return VNWithExc(unique, exceptions);
    }

    public ValueNumPair VNPUniqueWithExc(var_types type, ValueNumPair exceptions)
    {
#if DEBUG
        var app = new VNFuncApp();
        assert((GetVNFunc(exceptions.Liberal, ref app) && app.FuncIs(VNF_ExcSetCons)) ||
            (exceptions.Liberal == VNForEmptyExcSet()));
        assert((GetVNFunc(exceptions.Conservative, ref app) && app.FuncIs(VNF_ExcSetCons)) ||
            (exceptions.Conservative == VNForEmptyExcSet()));
#endif
        var unique = VNForExpr(_compiler.compCurBB, type);
        return VNPWithExc(new(unique, unique), exceptions);
    }
}
