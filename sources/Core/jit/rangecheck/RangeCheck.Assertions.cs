// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using static RyuJitSharp.Compiler.optAssertionKind;
using static RyuJitSharp.Compiler.optOp2Kind;

namespace RyuJitSharp;

public sealed partial class RangeCheck
{
    public static Range GetRange(Compiler comp, GenTree tree, BasicBlock block,
        ASSERT_TP? assertions, bool fast = true)
    {
        var budget = 256;
#if DEBUG
        if (comp.compStressCompile(Compiler.STRESS_GET_RANGE, 50))
        {
            fast = false;
            budget *= 16;
        }
#endif
        if (fast)
        {
            return GetRangeFromAssertions(comp, tree,
                BitVecOps.MaybeUninit(assertions) ? block.bbAssertionIn : assertions);
        }

        return comp.GetRangeCheck(budget).TryGetRange(block, tree, out var range)
            ? range : new(new(LimitType.Unknown));
    }

    public static Range GetRangeFromType(var_types type) => type switch
    {
        TYP_UBYTE => new(new(LimitType.Constant, 0), new(LimitType.Constant, byte.MaxValue)),
        TYP_BYTE => new(new(LimitType.Constant, sbyte.MinValue), new(LimitType.Constant, sbyte.MaxValue)),
        TYP_USHORT => new(new(LimitType.Constant, 0), new(LimitType.Constant, ushort.MaxValue)),
        TYP_SHORT => new(new(LimitType.Constant, short.MinValue), new(LimitType.Constant, short.MaxValue)),
        TYP_INT => new(new(LimitType.Constant, int.MinValue), new(LimitType.Constant, int.MaxValue)),
        _ => new(new(LimitType.Unknown)),
    };

    public static Range GetRangeFromAssertions(Compiler comp, GenTree tree, ASSERT_TP? assertions, int budget = 10)
    {
        var type = tree.Type;
        if (!varTypeIsIntegral(type))
        {
            return new(new(LimitType.Unknown));
        }

        var store = comp.vnStore;
        assert(store is not null);
        var vn = store.VNNormalValue(tree._vnPair.Conservative);
        return vn == ValueNumStore.NoVN
            ? GetRangeFromType(type)
            : GetRangeFromAssertionsWorker(comp, vn, assertions, budget, []);
    }

    public static Range GetRangeFromAssertions(Compiler comp, ValueNum vn, ASSERT_TP? assertions, int budget = 10)
    {
        return vn == ValueNumStore.NoVN
            ? new(new(LimitType.Unknown))
            : GetRangeFromAssertionsWorker(comp, vn, assertions, budget, []);
    }

    private static Range GetRangeFromAssertionsWorker(
        Compiler comp, ValueNum vn, ASSERT_TP? assertions, int budget, HashSet<ValueNum> visited)
    {
        assert(vn != ValueNumStore.NoVN);
        var store = comp.vnStore;
        assert(store is not null);
        var type = store.TypeOfVN(vn);
        var result = GetRangeFromType(type);
        if (budget <= 0)
        {
            return result;
        }

        if (varTypeIsGC(type))
        {
#if TARGET_64BIT
            return new(new(LimitType.Unknown));
#else
            return GetRangeFromType(TYP_INT);
#endif
        }

        if (store.IsVNConstant(vn))
        {
            return store.IsVNIntegralConstant(vn, out int constant)
                ? new(new(LimitType.Constant, constant))
                : new(new(LimitType.Unknown));
        }

        var app = new VNFuncApp();
        if (store.GetVNFunc(vn, ref app))
        {
#if FEATURE_HW_INTRINSICS
            var intrinsic = default(NamedIntrinsic);
            var simdSize = 0;
            var simdBaseType = TYP_UNDEF;
            if (store.IsVNHWIntrinsicFunc(vn, ref app, ref intrinsic, ref simdSize, ref simdBaseType))
            {
                if (HWIntrinsicInfo.ReturnsBoolean(intrinsic))
                {
                    result = new(new(LimitType.Constant, 0), new(LimitType.Constant, 1));
                }
                else if (HWIntrinsicInfo.ReturnsScalarT(intrinsic) && varTypeIsSmall(simdBaseType))
                {
                    result = GetRangeFromType(simdBaseType);
                }
            }
#endif
            switch (app.Func)
            {
                case VNF_Cast:
                {
                    store.GetCastOperFromVN(app.GetArg(1), out var toType, out var sourceUnsigned);
                    var fromVN = app.GetArg(0);
                    var fromType = store.TypeOfVN(fromVN);
                    var castFromType = sourceUnsigned ? varTypeToUnsigned(fromType) : fromType;
                    if (sourceUnsigned && varTypeIsSigned(fromType) && (fromType.Size < TYP_INT.Size))
                    {
                        castFromType = varTypeToUnsigned(fromType.ActualType);
                    }

                    var widensToSmallUnsigned = varTypeIsUnsigned(toType) &&
                        (toType.Size < TYP_INT.Size) && varTypeIsSigned(castFromType);
                    result = (castFromType.Size < toType.Size) && !widensToSmallUnsigned
                        ? GetRangeFromType(castFromType)
                        : GetRangeFromType(toType is TYP_UINT ? TYP_INT : toType);
                    var sourceRange = GetRangeFromAssertionsWorker(comp, fromVN, assertions, --budget, visited);
                    if (sourceRange.IsConstantRange())
                    {
                        if (!result.IsConstantRange())
                        {
                            if (!sourceUnsigned || (sourceRange.LowerLimit.Constant >= 0))
                            {
                                result = sourceRange;
                            }
                        }
                        else if ((sourceRange.LowerLimit.Constant >= result.LowerLimit.Constant) &&
                            (sourceRange.UpperLimit.Constant <= result.UpperLimit.Constant))
                        {
                            result = sourceRange;
                        }
                    }
                    break;
                }

                case VNF_NEG:
                {
                    var operand = GetRangeFromAssertionsWorker(comp, app.GetArg(0), assertions, --budget, visited);
                    var negated = RangeOps.Negate(operand);
                    result = negated.IsConstantRange() ? negated : result;
                    break;
                }

                case VNF_LSH:
                case VNF_ADD:
                case VNF_MUL:
                case VNF_SUB:
                case VNF_AND:
                case VNF_OR:
                case VNF_RSH:
                case VNF_RSZ:
                case VNF_UMOD:
                case VNF_UDIV:
                {
                    var left = GetRangeFromAssertionsWorker(comp, app.GetArg(0), assertions, --budget, visited);
                    var right = GetRangeFromAssertionsWorker(comp, app.GetArg(1), assertions, --budget, visited);
                    if (varTypeIsLong(type) && (app.Func is VNF_LSH or VNF_RSH or VNF_RSZ))
                    {
                        if (app.Func is VNF_LSH || !right.IsSingleValueConstant(out var amount) ||
                            (amount < (app.Func is VNF_RSZ ? 33 : 32)) || (amount >= 64))
                        {
                            return new(new(LimitType.Unknown));
                        }

                        if (app.Func is VNF_RSH)
                        {
                            result = GetRangeFromType(TYP_INT);
                            break;
                        }

                        left = new(new(LimitType.Constant, 0), new(LimitType.Constant, int.MaxValue));
                        right = new(new(LimitType.Constant, amount - 33));
                    }

                    var computed = app.Func switch
                    {
                        VNF_ADD => RangeOps.Add(left, right),
                        VNF_MUL => RangeOps.Multiply(left, right),
                        VNF_SUB => RangeOps.Subtract(left, right),
                        VNF_AND => RangeOps.And(left, right),
                        VNF_OR => RangeOps.Or(left, right),
                        VNF_LSH => RangeOps.ShiftLeft(left, right),
                        VNF_RSH => RangeOps.ShiftRight(left, right, false),
                        VNF_RSZ => RangeOps.ShiftRight(left, right, true),
                        VNF_UMOD => RangeOps.UnsignedMod(left, right),
                        VNF_UDIV => RangeOps.UnsignedDivide(left, right),
                        _ => throw new InvalidOperationException(),
                    };
                    result = computed.IsConstantRange() ? computed : result;
                    break;
                }

                case VNF_MDARR_LENGTH:
                case VNF_ARR_LENGTH:
                {
                    result = new(new(LimitType.Constant, 0), new(LimitType.Constant, CORINFO_Array_MaxLength));
                    break;
                }

                case VNF_GT:
                case VNF_GT_UN:
                case VNF_GE:
                case VNF_GE_UN:
                case VNF_LT:
                case VNF_LT_UN:
                case VNF_LE:
                case VNF_LE_UN:
                case VNF_EQ:
                case VNF_NE:
                {
                    result = new(new(LimitType.Constant, 0), new(LimitType.Constant, 1));
                    var left = GetRangeFromAssertionsWorker(comp, app.GetArg(0), assertions, --budget, visited);
                    var right = GetRangeFromAssertionsWorker(comp, app.GetArg(1), assertions, --budget, visited);
                    if (left.IsConstantRange() && right.IsConstantRange())
                    {
                        var unsigned = app.Func is VNF_GT_UN or VNF_GE_UN or VNF_LT_UN or VNF_LE_UN;
                        var op = app.Func switch
                        {
                            VNF_GT_UN => GT_GT,
                            VNF_GE_UN => GT_GE,
                            VNF_LT_UN => GT_LT,
                            VNF_LE_UN => GT_LE,
                            _ => (genTreeOps)app.Func,
                        };
                        result = RangeOps.EvalRelop(op, unsigned, left, right);

                        var addVN = ValueNumStore.NoVN;
                        var addConstant = 0;
                        if (!result.IsSingleValueConstant(out _) && (type.ActualType == TYP_INT) &&
                            store.IsVNBinFuncWithConst(app.GetArg(0), VNF_ADD, ref addVN, ref addConstant) &&
                            (addVN == app.GetArg(1)) && (addConstant < 0) && (addConstant > int.MinValue) &&
                            right.LowerLimit.IsConstant && (right.LowerLimit.Constant >= -addConstant))
                        {
                            result = op switch
                            {
                                GT_LT or GT_LE or GT_NE => new(new(LimitType.Constant, 1)),
                                GT_GT or GT_GE or GT_EQ => new(new(LimitType.Constant, 0)),
                                _ => result,
                            };
                        }
                    }
                    break;
                }

#if FEATURE_HW_INTRINSICS
                case VNF_HWI_Vector_ExtractMostSignificantBits:
#if TARGET_XARCH
                case VNF_HWI_X86Base_MoveMask:
                case VNF_HWI_AVX_MoveMask:
                case VNF_HWI_AVX2_MoveMask:
                case VNF_HWI_AVX512_MoveMask:
#endif
                {
                    var baseType = TYP_UNDEF;
                    var size = store.GetVNHWIntrinsicSizeAndBaseType(app, ref baseType);
                    var count = size / baseType.Size;
                    if (count <= 16)
                    {
                        result = new(new(LimitType.Constant, 0), new(LimitType.Constant, (1 << count) - 1));
                    }
                    break;
                }

#if TARGET_XARCH
                case VNF_HWI_AVX2_LeadingZeroCount:
                case VNF_HWI_AVX2_TrailingZeroCount:
                case VNF_HWI_AVX2_X64_LeadingZeroCount:
                case VNF_HWI_AVX2_X64_TrailingZeroCount:
                case VNF_HWI_X86Base_PopCount:
                case VNF_HWI_X86Base_X64_PopCount:
#endif
#endif
                case VNF_LeadingZeroCount:
                case VNF_TrailingZeroCount:
                case VNF_PopCount:
                {
                    var baseType = store.TypeOfVN(app.GetArg(0));
                    result = new(new(LimitType.Constant, 0),
                        new(LimitType.Constant, varTypeIsLong(baseType) ? 64 : 32));
                    break;
                }
            }
        }

        if (result.IsSingleValueConstant(out _))
        {
            return result;
        }

        var phiRange = new Range(new Limit(LimitType.Undef));
        Compiler.AssertVisit Visit(ValueNum reachingVN, ASSERT_TP? reachingAssertions)
        {
            var edgeRange = reachingVN == ValueNumStore.NoVN
                ? GetRangeFromType(type)
                : GetRangeFromAssertionsWorker(comp, reachingVN, reachingAssertions, --budget, visited);
            phiRange = phiRange.IsUndef() ? edgeRange : RangeOps.Merge(phiRange, edgeRange, false);
            return edgeRange.IsConstantRange() && !edgeRange.IsFullRange()
                ? Compiler.AssertVisit.Continue : Compiler.AssertVisit.Abort;
        }

        if (store.IsPhiDef(vn) && visited.Add(vn) &&
            (comp.optVisitReachingAssertions(vn, Visit) is Compiler.AssertVisit.Continue) && !phiRange.IsUndef())
        {
            assert(phiRange.IsConstantRange());
            result = phiRange;
        }

        MergeEdgeAssertionsWorker(comp, vn, ValueNumStore.NoVN, assertions, ref result,
            false, Math.Min(3, budget), visited);
        return result.IsConstantRange() ? result : new(new(LimitType.Unknown));
    }

    private static Limit TightenLimit(Limit first, Limit second, ValueNum preferredBound, bool isLower)
    {
        if (first.IsUndef || second.IsUndef)
        {
            return first.IsUndef ? second : first;
        }

        if (first.IsUnknown || second.IsUnknown)
        {
            return first.IsUnknown ? second : first;
        }

        if (first.IsDependent || second.IsDependent)
        {
            return first.IsDependent ? second : first;
        }

        if (first.IsConstant && second.IsConstant)
        {
            return isLower
                ? (first.Constant > second.Constant ? first : second)
                : (first.Constant < second.Constant ? first : second);
        }

        if (first.IsBinOpArray && second.IsBinOpArray)
        {
            if ((first.VN == preferredBound) && (second.VN != preferredBound))
            {
                return first;
            }

            if ((second.VN == preferredBound) && (first.VN != preferredBound))
            {
                return second;
            }

            return isLower
                ? (first.Constant > second.Constant ? first : second)
                : (first.Constant < second.Constant ? first : second);
        }

        if (first.IsConstant)
        {
            (first, second) = (second, first);
        }

        assert(first.IsBinOpArray && second.IsConstant);
        return (first.VN != preferredBound) || isLower ? second : first;
    }

    private static void MergeEdgeAssertions(Compiler comp, ValueNum vn, ValueNum preferredBound,
        ASSERT_TP? assertions, ref Range range, bool canUseCheckedBounds = true)
    {
        MergeEdgeAssertionsWorker(comp, vn, preferredBound, assertions, ref range,
            canUseCheckedBounds, 3, []);
    }
}
