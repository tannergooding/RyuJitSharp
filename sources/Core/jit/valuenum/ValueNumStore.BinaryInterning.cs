// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class ValueNumStore
{
    public ValueNum VNForFunc(var_types type, VNFunc func, ValueNum arg0VN, ValueNum arg1VN)
    {
        assert((arg0VN != NoVN) && (arg1VN != NoVN));
        assert((arg0VN == VNNormalValue(arg0VN)) && (arg1VN == VNNormalValue(arg1VN)));
        assert(VNFuncArity(func) is 0 or 2);
        assert(func != VNF_MapSelect);

        if ((arg0VN != arg1VN) && (func is VNF_EQ or VNF_NE))
        {
            var compared = VNEvalFoldTypeCompare(type, func, arg0VN, arg1VN);
            if (compared != NoVN)
            {
                return compared;
            }
        }

        if (VNFuncIsCommutative(func))
        {
            if (arg0VN > arg1VN)
            {
                (arg0VN, arg1VN) = (arg1VN, arg0VN);
            }

            if (IsVNConstantNonHandle(arg0VN))
            {
                (arg0VN, arg1VN) = (arg1VN, arg0VN);
            }
        }

        _vnFunc2Map ??= [];
        var key = (func, arg0VN, arg1VN);
        if (_vnFunc2Map.TryGetValue(key, out var result) && (result != NoVN))
        {
            return result;
        }

        // Recursive identity folding can intern other binary functions.
        _vnFunc2Map[key] = NoVN;
        if (func is VNF_CastClass or VNF_IsInstanceOf)
        {
            result = VNForCast(func, arg0VN, arg1VN);
        }
        else
        {
            result = NoVN;
            var folded = false;
            if (VNEvalCanFoldBinaryFunc(type, func, arg0VN, arg1VN) &&
                VNEvalShouldFold(type, func, arg0VN, arg1VN))
            {
                result = EvalFuncForConstantArgs(type, func, arg0VN, arg1VN);
            }

            if (result != NoVN)
            {
                folded = true;
            }
            else
            {
                result = EvalUsingMathIdentity(type, func, arg0VN, arg1VN);
            }

            if ((result == NoVN) || (!folded && (TypeOfVN(result).ActualType != type.ActualType)))
            {
                var chunk = GetAllocChunk(type, ChunkExtraAttribs.CEA_Func2);
                var offset = chunk.AllocVN();
                var record = chunk.FuncApp(offset, 2).Span;
                record[0] = (int)func;
                record[1] = arg0VN;
                record[2] = arg1VN;
                result = unchecked(chunk.BaseVN + offset);
            }
        }

        _vnFunc2Map[key] = result;
        return result;
    }

    private unsafe ValueNum VNEvalFoldTypeCompare(var_types type, VNFunc func, ValueNum arg0VN, ValueNum arg1VN)
    {
        assert(func is VNF_EQ or VNF_NE);
        var first = new VNFuncApp();
        if (!GetVNFunc(arg0VN, ref first) || !first.FuncIs(VNF_TypeHandleToRuntimeType))
        {
            return NoVN;
        }

        var second = new VNFuncApp();
        if (!GetVNFunc(arg1VN, ref second) || !second.FuncIs(VNF_TypeHandleToRuntimeType))
        {
            return NoVN;
        }

        var handle0 = first.GetArg(0);
        var handle1 = second.GetArg(0);
        if (!IsVNHandle(handle0) || !IsVNHandle(handle1))
        {
            return NoVN;
        }

        assert(GetHandleFlags(handle0) == GTF_ICON_CLASS_HDL);
        assert(GetHandleFlags(handle1) == GTF_ICON_CLASS_HDL);

        nint compileTimeHandle0 = 0;
        nint compileTimeHandle1 = 0;
        var found0 = EmbeddedHandleMapLookup(ConstantValue<nint>(handle0), ref compileTimeHandle0);
        var found1 = EmbeddedHandleMapLookup(ConstantValue<nint>(handle1), ref compileTimeHandle1);
        assert(found0 && found1);
        if ((compileTimeHandle0 == 0) || (compileTimeHandle1 == 0))
        {
            return NoVN;
        }

        var class0 = (CORINFO_CLASS_HANDLE)compileTimeHandle0;
        var class1 = (CORINFO_CLASS_HANDLE)compileTimeHandle1;
#if DEBUG
        if (_compiler.verbose)
        {
            JITDUMP($"Asking runtime to compare {FMT_PTR((void*)_compiler.dspPtr(class0))} " +
                $"({_compiler.eeGetClassName(class0)}) and {FMT_PTR((void*)_compiler.dspPtr(class1))} " +
                $"({_compiler.eeGetClassName(class1)}) for equality\n");
        }
#endif
        var state = _compiler.info.compCompHnd->compareTypesForEquality(
            class0, class1);
        if (state is TypeCompareState.May)
        {
            return NoVN;
        }

        var equal = state is TypeCompareState.Must;
        var comparison = (func == VNF_EQ) ^ equal ? 0 : 1;
        JITDUMP($"Runtime reports comparison is known at jit time: {comparison}\n");
        return VNForIntCon(comparison);
    }

    private unsafe ValueNum VNForCast(VNFunc func, ValueNum castToVN, ValueNum objVN)
    {
        assert(func is VNF_CastClass or VNF_IsInstanceOf);
        if (objVN == VNForNull())
        {
            return VNForNull();
        }

        var innerClass = NoVN;
        var innerObject = NoVN;
        if (IsVNBinFunc(objVN, VNF_IsInstanceOf, ref innerClass, ref innerObject) && (innerClass == castToVN))
        {
            return objVN;
        }

        if (IsVNTypeHandle(castToVN))
        {
            var castFrom = GetObjectType(objVN, out var isExact, out _);
            nint compileTimeHandle = 0;
            if ((castFrom != NO_CLASS_HANDLE) &&
                EmbeddedHandleMapLookup(ConstantValue<nint>(castToVN), ref compileTimeHandle) &&
                (compileTimeHandle != 0))
            {
                var state = _compiler.info.compCompHnd->compareTypesForCast(
                    castFrom, (CORINFO_CLASS_HANDLE)compileTimeHandle);
                if (state is TypeCompareState.Must)
                {
                    return objVN;
                }

                if ((state is TypeCompareState.MustNot) && isExact && (func == VNF_IsInstanceOf))
                {
                    return VNForNull();
                }
            }
        }

        if (func == VNF_CastClass)
        {
            var exception = VNForFuncNoFolding(TYP_REF, VNF_InvalidCastExc, objVN, castToVN);
            return VNWithExc(objVN, VNExcSetSingleton(exception));
        }

        var chunk = GetAllocChunk(TYP_REF, ChunkExtraAttribs.CEA_Func2);
        var offset = chunk.AllocVN();
        var record = chunk.FuncApp(offset, 2).Span;
        record[0] = (int)VNF_IsInstanceOf;
        record[1] = castToVN;
        record[2] = objVN;
        return unchecked(chunk.BaseVN + offset);
    }

    private ValueNum VNOneForType(var_types type) => type switch {
        TYP_BYTE or TYP_UBYTE or TYP_SHORT or TYP_USHORT or TYP_INT or TYP_UINT => VNForIntCon(1),
        TYP_LONG or TYP_ULONG => VNForLongCon(1),
        TYP_FLOAT => VNForFloatCon(1.0f),
        TYP_DOUBLE => VNForDoubleCon(1.0),
        _ => NoVN,
    };

    private ValueNum VNAllBitsForType(var_types type, int elementCount)
    {
        return type switch {
            TYP_INT or TYP_UINT => VNForIntCon(-1),
            TYP_LONG or TYP_ULONG => VNForLongCon(-1),
#if FEATURE_SIMD
            TYP_SIMD8 => VNForSimd8Con(simd8_t.AllBitsSet),
            TYP_SIMD12 => VNForSimd12Con(simd12_t.AllBitsSet),
            TYP_SIMD16 => VNForSimd16Con(simd16_t.AllBitsSet),
#if TARGET_XARCH
            TYP_SIMD32 => VNForSimd32Con(simd32_t.AllBitsSet),
            TYP_SIMD64 => VNForSimd64Con(simd64_t.AllBitsSet),
#elif TARGET_ARM64
            TYP_SIMD => throw new System.NotImplementedException("Scalable VN all-bits constant storage is not yet ported."),
#endif
#if FEATURE_MASKED_HW_INTRINSICS
            TYP_MASK => VNForSimdMaskCon(simdmask_t.AllBitsSet(elementCount)),
#endif
#endif
            _ => NoVN,
        };
    }

    private bool IsVNRelop(ValueNum vn)
    {
        var app = new VNFuncApp();
        return GetVNFunc(vn, ref app) && (app.Arity == 2) && VNFuncIsComparison(app.Func);
    }

    private ValueNum GetReverseRelop(ValueNum vn)
    {
        assert(vn == VNNormalValue(vn));
        var app = new VNFuncApp();
        if ((vn == NoVN) || !GetVNFunc(vn, ref app) || (app.Arity != 2) ||
            varTypeIsFloating(TypeOfVN(app.GetArg(0))))
        {
            return NoVN;
        }

        VNFunc reversed;
        if (app.Func >= VNF_Boundary)
        {
            reversed = app.Func switch {
                VNF_LT_UN => VNF_GE_UN,
                VNF_LE_UN => VNF_GT_UN,
                VNF_GE_UN => VNF_LT_UN,
                VNF_GT_UN => VNF_LE_UN,
                _ => VNF_MemOpaque,
            };
        }
        else
        {
            var op = (genTreeOps)app.Func;
            reversed = op.IsCompare ? (VNFunc)op.ReverseRelop : VNF_MemOpaque;
        }

        return reversed == VNF_MemOpaque ? NoVN : VNForFunc(TYP_INT, reversed, app.GetArg(0), app.GetArg(1));
    }
}
