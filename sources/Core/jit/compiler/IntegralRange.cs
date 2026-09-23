// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

using System;
using System.Diagnostics;
using static RyuJitSharp.SymbolicIntegerValue;

namespace RyuJitSharp;

/// <summary>An integral range with ordered symbolic bounds, always interpreted in the signed domain.</summary>
/// <remarks>
/// Keeping input and output in the same signed domain allows cast overflow reasoning.
/// A checked uint-to-ulong cast consumes [IntMin..IntMax] but produces [Zero..UIntMax];
/// a checked uint-to-int cast consumes and produces [Zero..IntMax].
/// </remarks>
public readonly struct IntegralRange : IEquatable<IntegralRange>
{
    private static ReadOnlySpan<long> SymbolicToRealMap => [
        long.MinValue, int.MinValue, short.MinValue, sbyte.MinValue,
        0, 1, sbyte.MaxValue, byte.MaxValue, short.MaxValue, ushort.MaxValue,
        CORINFO_Array_MaxLength, int.MaxValue, uint.MaxValue, long.MaxValue,
    ];

    public IntegralRange(SymbolicIntegerValue lowerBound, SymbolicIntegerValue upperBound)
    {
        assert(lowerBound <= upperBound);
        LowerBound = lowerBound;
        UpperBound = upperBound;
    }

    public SymbolicIntegerValue LowerBound { get; }

    public SymbolicIntegerValue UpperBound { get; }

    public bool IsNonNegative => LowerBound >= Zero;

    public bool Contains(long value)
    {
        var lowerBound = SymbolicToRealValue(LowerBound);
        var upperBound = SymbolicToRealValue(UpperBound);
        return (lowerBound <= value) && (value <= upperBound);
    }

    public bool Contains(IntegralRange other)
        => (LowerBound <= other.LowerBound) && (other.UpperBound <= UpperBound);

    public bool Equals(IntegralRange other)
        => (LowerBound == other.LowerBound) && (UpperBound == other.UpperBound);

    public override bool Equals(object? obj) => obj is IntegralRange other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(LowerBound, UpperBound);

    public static bool operator ==(IntegralRange left, IntegralRange right) => left.Equals(right);

    public static bool operator !=(IntegralRange left, IntegralRange right) => !left.Equals(right);

    public static long SymbolicToRealValue(SymbolicIntegerValue value)
    {
        assert(SymbolicToRealMap[(int)LongMin] == long.MinValue);
        assert(SymbolicToRealMap[(int)Zero] == 0);
        assert(SymbolicToRealMap[(int)LongMax] == long.MaxValue);
        return SymbolicToRealMap[(int)value];
    }

    public static SymbolicIntegerValue LowerBoundForType(var_types type) => type switch {
        TYP_UBYTE or TYP_USHORT => Zero,
        TYP_BYTE => ByteMin,
        TYP_SHORT => ShortMin,
        TYP_INT => IntMin,
        TYP_LONG => LongMin,
        _ => throw new UnreachableException(),
    };

    public static SymbolicIntegerValue UpperBoundForType(var_types type) => type switch {
        TYP_BYTE => ByteMax,
        TYP_UBYTE => UByteMax,
        TYP_SHORT => ShortMax,
        TYP_USHORT => UShortMax,
        TYP_INT => IntMax,
        TYP_UINT => UIntMax,
        TYP_LONG => LongMax,
        _ => throw new UnreachableException(),
    };

    public static IntegralRange ForType(var_types type) => new(LowerBoundForType(type), UpperBoundForType(type));

    public static IntegralRange ForNode(GenTree node, Compiler compiler)
    {
        assert(varTypeIsIntegral(node.Type));
        var rangeType = node.Type;

        switch (node.Oper)
        {
            case GT_EQ:
            case GT_NE:
            case GT_LT:
            case GT_LE:
            case GT_GE:
            case GT_GT:
            {
                return new(Zero, One);
            }

            case GT_AND:
            {
                var leftRange = ForNode(node.AsOp().Op1, compiler);
                var rightRange = ForNode(node.AsOp().Op2, compiler);

                if (leftRange.IsNonNegative && rightRange.IsNonNegative)
                {
                    // With both operands non-negative, AND cannot exceed either upper bound.
                    var upperBound = leftRange.UpperBound < rightRange.UpperBound ? leftRange.UpperBound : rightRange.UpperBound;
                    return new(Zero, upperBound);
                }

                if (leftRange.IsNonNegative || rightRange.IsNonNegative)
                {
                    return new(Zero, UpperBoundForType(rangeType));
                }

                break;
            }

            case GT_ARR_LENGTH:
            case GT_MDARR_LENGTH:
            {
                return new(Zero, ArrayLenMax);
            }

            case GT_CALL:
            {
                if (node.AsCall().NormalizesSmallTypesOnReturn)
                {
                    rangeType = node.AsCall()._returnType;
                }

                break;
            }

            case GT_IND:
            {
                var addr = node.AsIndir().Addr;

                if ((node.Type is TYP_INT) && (addr.Oper is GT_ADD)
                    && (addr.AsOp().Op1.Oper is GT_LCL_VAR)
                    && addr.AsOp().Op2.IsIntegralConst(OFFSETOF__CORINFO_Span__length))
                {
                    var lclVar = addr.AsOp().Op1.AsLclVar();

                    if (compiler.lvaGetDesc(lclVar.LclNum).IsSpan)
                    {
                        assert(compiler.lvaIsImplicitByRefLocal(lclVar.LclNum));
                        return new(Zero, UpperBoundForType(rangeType));
                    }
                }

                break;
            }

            case GT_LCL_FLD:
            {
                var lclFld = node.AsLclFld();
                ref var varDsc = ref compiler.lvaGetDesc(lclFld.LclNum);

                if ((node.Type is TYP_INT) && varDsc.IsSpan && (lclFld.LclOffs == OFFSETOF__CORINFO_Span__length))
                {
                    return new(Zero, UpperBoundForType(rangeType));
                }

                break;
            }

            case GT_LCL_VAR:
            {
                ref var varDsc = ref compiler.lvaGetDesc(node.AsLclVar().LclNum);

                if (varDsc.lvNormalizeOnStore)
                {
                    rangeType = varDsc.Type;
                }

                if (varDsc.IsNeverNegative)
                {
                    return new(Zero, UpperBoundForType(rangeType));
                }

                break;
            }

            case GT_CNS_INT:
            case GT_CNS_LNG:
            {
                if (node.IsIntegralConst(0) || node.IsIntegralConst(1))
                {
                    return new(Zero, One);
                }

                var constValue = node.AsIntConCommon().IntegralValue;

                if (FitsIn(TYP_INT, constValue))
                {
                    rangeType = TYP_INT;
                }
                else if (FitsIn(TYP_UINT, constValue))
                {
                    rangeType = TYP_UINT;
                }

                if (constValue >= 0)
                {
                    return new(Zero, UpperBoundForType(rangeType));
                }

                break;
            }

            case GT_QMARK:
            {
                return Union(ForNode(node.AsQmark().ThenNode, compiler), ForNode(node.AsQmark().ElseNode, compiler));
            }

            case GT_CAST:
            {
                return ForCastOutput(node.AsCast(), compiler);
            }

#if FEATURE_HW_INTRINSICS
            case GT_HWINTRINSIC:
            {
                var hwintrinsic = node.AsHWIntrinsic();
                var id = hwintrinsic.HWIntrinsicId;

                if (HWIntrinsicInfo.ReturnsBoolean(id))
                {
                    return new(Zero, One);
                }

                if (HWIntrinsicInfo.ReturnsScalarT(id))
                {
                    var simdBaseType = hwintrinsic.SimdBaseType;

                    if (varTypeIsSmall(simdBaseType))
                    {
                        rangeType = simdBaseType;
                    }

                    break;
                }

                switch (id)
                {
#if TARGET_XARCH
                    case NI_X86Base_MoveMask:
                    case NI_AVX_MoveMask:
                    case NI_AVX2_MoveMask:
                    case NI_AVX512_MoveMask:
#elif TARGET_WASM
                    case NI_PackedSimd_Bitmask:
#endif
                    case NI_Vector_ExtractMostSignificantBits:
                    {
                        // One output bit per element; the remaining high bits are zero.
                        var elementSize = hwintrinsic.SimdBaseType.Size;
                        var elementCount = hwintrinsic.SimdSize / elementSize;

                        if (elementCount <= 8)
                        {
                            rangeType = TYP_UBYTE;
                        }
                        else if (elementCount <= 16)
                        {
                            rangeType = TYP_USHORT;
                        }
                        else if ((elementCount == 32) && varTypeIsLong(rangeType))
                        {
                            return new(Zero, UpperBoundForType(TYP_UINT));
                        }

                        break;
                    }

#if TARGET_XARCH
                    case NI_AVX2_LeadingZeroCount:
                    case NI_AVX2_TrailingZeroCount:
                    case NI_AVX2_X64_LeadingZeroCount:
                    case NI_AVX2_X64_TrailingZeroCount:
                    case NI_X86Base_PopCount:
                    case NI_X86Base_X64_PopCount:
#elif TARGET_ARM64
                    case NI_ArmBase_LeadingZeroCount:
                    case NI_ArmBase_Arm64_LeadingZeroCount:
                    case NI_ArmBase_Arm64_LeadingSignCount:
#elif TARGET_WASM
                    // TODO-WASM: Support CTZ/CLZ ranges here when native does.
#else
#error Unsupported platform
#endif
#if TARGET_XARCH || TARGET_ARM64
                    {
                        // Actual range is [0..32] or [0..64].
                        return new(Zero, ByteMax);
                    }
#endif

                    default:
                    {
                        break;
                    }
                }

                break;
            }
#endif

            case GT_INTRINSIC:
            {
                switch (node.AsIntrinsic().IntrinsicName)
                {
                    case NI_PRIMITIVE_LeadingZeroCount:
                    case NI_PRIMITIVE_PopCount:
                    case NI_PRIMITIVE_TrailingZeroCount:
                    {
                        return new(Zero, ByteMax);
                    }

                    case NI_PRIMITIVE_SaturateToInt8:
                    {
                        return new(ByteMin, ByteMax);
                    }

                    case NI_PRIMITIVE_SaturateToInt16:
                    {
                        return new(ShortMin, ShortMax);
                    }

                    case NI_PRIMITIVE_SaturateToUInt8:
                    {
                        return new(Zero, UByteMax);
                    }

                    case NI_PRIMITIVE_SaturateToUInt16:
                    {
                        return new(Zero, UShortMax);
                    }

                    case NI_System_Runtime_CompilerServices_RuntimeHelpers_IsKnownConstant:
                    {
                        return new(Zero, One);
                    }

                    default:
                    {
                        break;
                    }
                }

                break;
            }

            default:
            {
                break;
            }
        }

        return ForType(rangeType);
    }

    /// <summary>Get the signed-domain input range that does not overflow an integral cast.</summary>
    public static IntegralRange ForCastInput(GenTreeCast cast)
    {
        var fromType = cast.CastOp.Type.ActualType;
        var toType = cast.CastType;
        var fromUnsigned = (cast.Flags & GTF_UNSIGNED) != 0;
        assert((fromType is TYP_INT or TYP_LONG) || varTypeIsGC(fromType));
        assert(varTypeIsIntegral(toType));

        if (varTypeIsGC(fromType))
        {
            fromType = TYP_I_IMPL;
        }

        if (!cast.HasOverflowCheck)
        {
            if (varTypeIsSmall(toType))
            {
                return ForType(toType);
            }

            // Representation-changing unchecked casts cannot be removed regardless of the range.
            return ForType(fromType);
        }

        SymbolicIntegerValue lowerBound;
        SymbolicIntegerValue upperBound;

        if (varTypeIsSmall(toType))
        {
            lowerBound = fromUnsigned ? Zero : LowerBoundForType(toType);
            upperBound = UpperBoundForType(toType);
        }
        else
        {
            switch (toType)
            {
                case TYP_UINT:
                {
                    if (fromType is TYP_LONG)
                    {
                        lowerBound = Zero;
                        upperBound = UIntMax;
                    }
                    else
                    {
                        lowerBound = fromUnsigned ? IntMin : Zero;
                        upperBound = IntMax;
                    }

                    break;
                }

                case TYP_INT:
                {
                    lowerBound = fromUnsigned ? Zero : IntMin;
                    upperBound = IntMax;
                    break;
                }

                case TYP_ULONG:
                {
                    lowerBound = fromUnsigned ? LowerBoundForType(fromType) : Zero;
                    upperBound = UpperBoundForType(fromType);
                    break;
                }

                case TYP_LONG:
                {
                    lowerBound = fromUnsigned && (fromType is TYP_LONG) ? Zero : LowerBoundForType(fromType);
                    upperBound = UpperBoundForType(fromType);
                    break;
                }

                default:
                {
                    throw new UnreachableException();
                }
            }
        }

        return new(lowerBound, upperBound);
    }

    /// <summary>Get the range produced by a successful cast, including representation changes.</summary>
    public static IntegralRange ForCastOutput(GenTreeCast cast, Compiler compiler)
    {
        var fromType = cast.CastOp.Type.ActualType;
        var toType = cast.CastType;
        var fromUnsigned = (cast.Flags & GTF_UNSIGNED) != 0;
        assert((fromType is TYP_INT or TYP_LONG) || varTypeIsFloating(fromType) || varTypeIsGC(fromType));
        assert(varTypeIsIntegral(toType));

        if (varTypeIsFloating(fromType))
        {
            if (!varTypeIsSmall(toType))
            {
                toType = toType.ActualType;
            }

            return ForType(toType);
        }

        if (varTypeIsGC(fromType))
        {
            fromType = TYP_I_IMPL;
        }

        if (varTypeIsSmall(toType) || (toType.ActualType == fromType))
        {
            return ForCastInput(cast);
        }

        if (!fromUnsigned && (toType.Size >= fromType.Size))
        {
            fromUnsigned = cast.CastOp.IsNeverNegative(compiler);
        }

        if (!cast.HasOverflowCheck)
        {
            if ((fromType is TYP_INT) && fromUnsigned)
            {
                return new(Zero, UIntMax);
            }

            return new(IntMin, IntMax);
        }

        SymbolicIntegerValue lowerBound;
        SymbolicIntegerValue upperBound;

        switch (toType)
        {
            case TYP_UINT:
            {
                lowerBound = IntMin;
                upperBound = IntMax;
                break;
            }

            case TYP_INT:
            {
                lowerBound = fromUnsigned ? Zero : IntMin;
                upperBound = IntMax;
                break;
            }

            case TYP_ULONG:
            {
                lowerBound = Zero;
                upperBound = fromUnsigned ? UIntMax : IntMax;
                break;
            }

            case TYP_LONG:
            {
                lowerBound = fromUnsigned ? Zero : IntMin;
                upperBound = fromUnsigned ? UIntMax : IntMax;
                break;
            }

            default:
            {
                throw new UnreachableException();
            }
        }

        return new(lowerBound, upperBound);
    }

    public static IntegralRange Union(IntegralRange range1, IntegralRange range2)
    {
        var lowerBound = range1.LowerBound < range2.LowerBound ? range1.LowerBound : range2.LowerBound;
        var upperBound = range1.UpperBound > range2.UpperBound ? range1.UpperBound : range2.UpperBound;
        return new(lowerBound, upperBound);
    }

#if DEBUG
    public static void Print(IntegralRange range)
    {
        jitprintf($"[{SymbolicToRealValue(range.LowerBound)}");
        jitprintf("..");
        jitprintf($"{SymbolicToRealValue(range.UpperBound)}]");
    }
#endif
}
