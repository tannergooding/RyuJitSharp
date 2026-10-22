// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using static RyuJitSharp.Compiler.optAssertionKind;
using static RyuJitSharp.Compiler.optOp1Kind;
using static RyuJitSharp.Compiler.optOp2Kind;

namespace RyuJitSharp;

public sealed partial class RangeCheck
{
    private static bool IsBoundLessOrEqual(
        Compiler comp, ASSERT_TP assertions, ValueNum lo, ValueNum hi, bool equalOnly, int budget)
    {
        if (lo == hi)
        {
            return true;
        }

        if ((budget <= 0) || !comp.optAssertionHasAssertionsForVN(lo))
        {
            return false;
        }

        assert(comp.apTraits is not null);
        var result = false;
        _ = BitVecOps.VisitBits(comp.apTraits, assertions, bitIndex =>
        {
            var assertion = comp.optGetAssertion(GetAssertionIndex((ushort)bitIndex));
            if (!assertion.Op1.KindIs(O1K_VN) ||
                !assertion.Op2.KindIs(O2K_VN_ADD_CNS) || (assertion.Op2.Cns != 0))
            {
                return true;
            }

            var op1VN = assertion.Op1.VN;
            var op2VN = assertion.Op2.VN;
            var next = ValueNumStore.NoVN;
            if (assertion.KindIs(OAK_EQUAL))
            {
                next = op1VN == lo ? op2VN : op2VN == lo ? op1VN : ValueNumStore.NoVN;
            }
            else if (!equalOnly && assertion.KindIs(OAK_LE, OAK_LT) && (op1VN == lo))
            {
                next = op2VN;
            }
            else if (!equalOnly && assertion.KindIs(OAK_GE, OAK_GT) && (op2VN == lo))
            {
                next = op1VN;
            }

            if ((next != ValueNumStore.NoVN) &&
                IsBoundLessOrEqual(comp, assertions, next, hi, equalOnly, budget - 1))
            {
                result = true;
                return false;
            }

            return true;
        });

        return result;
    }

    private static bool CanUsePreferredBound(
        Compiler comp, ASSERT_TP assertions, Compiler.AssertionDsc assertion, ValueNum preferredBoundVN)
    {
        const int maxChain = 3;
        var boundVN = assertion.Op2.VN;
        var boundCns = assertion.Op2.Cns;

        if (assertion.KindIs(OAK_LT, OAK_LE) && (boundCns <= 0) &&
            IsBoundLessOrEqual(comp, assertions, boundVN, preferredBoundVN, equalOnly: false, maxChain) &&
            ((boundCns == 0) || assertion.Op2.IsVNNeverNegative ||
             comp.optIsNeverNegativeCheckedBound(boundVN, assertions)))
        {
            return true;
        }

        return IsBoundLessOrEqual(comp, assertions, boundVN, preferredBoundVN, equalOnly: true, maxChain);
    }

    private static void MergeEdgeAssertionsWorker(Compiler comp, ValueNum vn, ValueNum preferredBound,
        ASSERT_TP? assertions, ref Range range, bool canUseCheckedBounds, int budget, HashSet<ValueNum> visited)
    {
        if (BitVecOps.MaybeUninit(assertions))
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(assertions);
        assert(comp.apTraits is not null);
        if (BitVecOps.IsEmpty(comp.apTraits, assertions) || !comp.optAssertionHasAssertionsForVN(vn))
        {
            return;
        }

        assert(canUseCheckedBounds || preferredBound == ValueNumStore.NoVN);
        var store = comp.vnStore;
        assert(store is not null);
        var current = range;
        var asserted = new Range(new Limit(LimitType.Unknown));
        _ = BitVecOps.VisitBits(comp.apTraits, assertions, index => {
            var assertionIndex = GetAssertionIndex((ushort)index);
            var assertion = comp.optGetAssertion(assertionIndex);
            var limit = new Limit(LimitType.Undef);
            var comparison = GT_NONE;
            var unsigned = false;

            if (canUseCheckedBounds && assertion.KindIs(OAK_LT_UN) &&
                assertion.Op2.KindIs(O2K_VN_ADD_CNS) && assertion.Op2.IsVNNeverNegative &&
                (assertion.Op2.VN == preferredBound) && (assertion.Op1.VN != vn))
            {
                var addVN = ValueNumStore.NoVN;
                var addCns = 0;
                if (!store.IsVNBinFuncWithConst(assertion.Op1.VN, VNF_ADD, ref addVN, ref addCns) ||
                    (addVN != vn))
                {
                    return true;
                }

                if (addCns >= 0)
                {
                    comparison = GT_LT;
                    limit = new(LimitType.BinOpArray, preferredBound, -addCns);
                }
                else if ((addCns > int.MinValue) && current.LowerLimit.IsConstant &&
                    CheckedOps.TryAdd(current.LowerLimit.Constant, addCns, out _))
                {
                    comparison = GT_GE;
                    limit = new(LimitType.Constant, -addCns);
                }
                else
                {
                    return true;
                }
            }
            else if (!canUseCheckedBounds &&
                (assertion.Kind is OAK_LT or OAK_LE or OAK_LT_UN or OAK_LE_UN) &&
                (assertion.Op1.VN == vn) && assertion.Op2.KindIs(O2K_VN_ADD_CNS) &&
                assertion.Op2.IsVNNeverNegative && (assertion.Op2.Cns == 0))
            {
                var boundVN = assertion.Op2.VN;
                var maximum = int.MaxValue;
                if (store.IsVNArrLen(boundVN))
                {
                    maximum = CORINFO_Array_MaxLength;
                }
                else if (store.IsVNIntegralConstant(boundVN, out int constant) && (constant >= 0))
                {
                    maximum = constant;
                }

                comparison = Compiler.AssertionDsc.ToCompareOper(assertion.Kind, out unsigned);
                limit = new(LimitType.Constant, maximum);
            }
            else if (canUseCheckedBounds && assertion.KindIs(OAK_LE_UN) &&
                (assertion.Op1.VN == vn) && assertion.Op2.KindIs(O2K_VN_ADD_CNS) &&
                assertion.Op2.IsVNNeverNegative && (assertion.Op2.VN == preferredBound) &&
                (assertion.Op2.Cns == 0))
            {
                comparison = GT_LE;
                limit = new(LimitType.BinOpArray, preferredBound, 0);
                unsigned = true;
            }
            else if ((assertion.Kind is OAK_GE or OAK_GT or OAK_LE or OAK_LT) &&
                assertion.Op2.KindIs(O2K_VN_ADD_CNS) && store.IsVNCheckedBound(assertion.Op2.VN))
            {
                var boundVN = assertion.Op2.VN;
                var offset = assertion.Op2.Cns;
                var addVN = ValueNumStore.NoVN;
                var addCns = 0;
                var matchesOp2 = ((vn == boundVN) && (offset == 0)) ||
                    ((offset != 0) && store.IsVNBinFuncWithConst(vn, VNF_ADD, ref addVN, ref addCns) &&
                        (addVN == boundVN) && (addCns == offset));
                if (canUseCheckedBounds && (vn == assertion.Op1.VN) &&
                    (preferredBound != ValueNumStore.NoVN) && (boundVN != preferredBound) &&
                    CanUsePreferredBound(comp, assertions, assertion, preferredBound))
                {
                    comparison = Compiler.AssertionDsc.ToCompareOper(assertion.Kind, out unsigned);
                    limit = new(LimitType.BinOpArray, preferredBound, offset);
                }
                // A checked bound from another path needs a nonnegative proof unless it is the preferred bound.
                else if (canUseCheckedBounds && (vn == assertion.Op1.VN) &&
                    (assertion.Op2.IsVNNeverNegative || (boundVN == preferredBound)))
                {
                    comparison = Compiler.AssertionDsc.ToCompareOper(assertion.Kind, out unsigned);
                    limit = new(LimitType.BinOpArray, boundVN, offset);
                }
                else if (matchesOp2)
                {
                    comparison = SwapRelop(Compiler.AssertionDsc.ToCompareOper(assertion.Kind, out unsigned));
                    if (store.IsVNInt32Constant(assertion.Op1.VN))
                    {
                        limit = new(LimitType.Constant, store.ConstantValue<int>(assertion.Op1.VN));
                    }
                    else if (canUseCheckedBounds && store.IsVNCheckedBound(assertion.Op1.VN) &&
                        (store.IsVNNeverNegative(assertion.Op1.VN) || (assertion.Op1.VN == preferredBound)))
                    {
                        limit = new(LimitType.BinOpArray, assertion.Op1.VN, 0);
                    }
                    else
                    {
                        return true;
                    }
                }
                else
                {
                    return true;
                }

                assert(!unsigned);
            }
            else if (assertion.IsRelop && assertion.Op2.KindIs(O2K_CONST_INT) &&
                (assertion.Op1.VN == vn) && FitsInI32(assertion.Op2.IntConstant))
            {
                comparison = Compiler.AssertionDsc.ToCompareOper(assertion.Kind, out unsigned);
                limit = new(LimitType.Constant, (int)assertion.Op2.IntConstant);
                assert((assertion.Op2.IntConstant > 0) || !unsigned);
            }
            else if (assertion.IsConstantInt32Assertion && (assertion.Op1.VN == vn))
            {
                if (varTypeIsGC(store.TypeOfVN(assertion.Op2.VN)))
                {
                    return true;
                }

                var constant = (int)assertion.Op2.IntConstant;
                assert(constant == store.CoercedConstantValue<int>(assertion.Op2.VN));
                limit = new(LimitType.Constant, constant);
                if (assertion.KindIs(OAK_EQUAL))
                {
                    comparison = GT_EQ;
                }
                else if (current.LowerLimit.IsConstant && (current.LowerLimit.Constant == constant))
                {
                    comparison = GT_GT;
                }
                else if (current.UpperLimit.IsConstant && (current.UpperLimit.Constant == constant))
                {
                    comparison = GT_LT;
                }
                else
                {
                    return true;
                }
            }
            else if (canUseCheckedBounds && assertion.KindIs(OAK_EQUAL, OAK_NOT_EQUAL) &&
                (assertion.Op1.VN == vn) && assertion.Op2.KindIs(O2K_VN_ADD_CNS) &&
                (assertion.Op2.Cns == 0) && store.IsVNCheckedBound(assertion.Op2.VN) &&
                (assertion.Op2.IsVNNeverNegative || (assertion.Op2.VN == preferredBound)))
            {
                var boundVN = assertion.Op2.VN;
                if (assertion.KindIs(OAK_EQUAL))
                {
                    limit = new(LimitType.BinOpArray, boundVN, 0);
                    comparison = GT_EQ;
                }
                else if (current.UpperLimit.IsBinOpArray && (current.UpperLimit.VN == boundVN) &&
                    (current.UpperLimit.Constant == 0))
                {
                    limit = new(LimitType.BinOpArray, boundVN, -1);
                    comparison = GT_LE;
                }
                else if (current.LowerLimit.IsBinOpArray && (current.LowerLimit.VN == boundVN) &&
                    (current.LowerLimit.Constant == 0))
                {
                    limit = new(LimitType.BinOpArray, boundVN, 1);
                    comparison = GT_GE;
                }
                else
                {
                    return true;
                }
            }
            else if (assertion.IsBoundsCheckNoThrow)
            {
                var indexVN = assertion.Op1.VN;
                var lengthVN = assertion.Op2.VN;
                if (vn == indexVN)
                {
                    if (canUseCheckedBounds)
                    {
                        unsigned = true;
                        comparison = GT_LT;
                        limit = new(LimitType.BinOpArray, lengthVN, 0);
                    }
                    else if (store.IsVNIntegralConstant(lengthVN, out int length) && (length >= 0))
                    {
                        unsigned = true;
                        comparison = GT_LT;
                        limit = new(LimitType.Constant, length);
                    }
                    else
                    {
                        comparison = GT_GE;
                        limit = new(LimitType.Constant, 0);
                    }
                }
                else if (vn == lengthVN)
                {
                    if (store.IsVNInt32Constant(indexVN))
                    {
                        var constant = store.GetConstantInt32(indexVN);
                        if (constant < 0)
                        {
                            return true;
                        }

                        comparison = GT_GT;
                        limit = new(LimitType.Constant, constant);
                    }
                    else
                    {
                        var addVN = ValueNumStore.NoVN;
                        var addCns = 0;
                        if (store.IsVNBinFuncWithConst(indexVN, VNF_ADD, ref addVN, ref addCns) &&
                            (addVN == vn) && (addCns < 0) && (addCns > int.MinValue))
                        {
                            comparison = GT_GE;
                            limit = new(LimitType.Constant, -addCns);
                        }
                        else
                        {
                            comparison = GT_GT;
                            limit = new(LimitType.Constant, 0);
                        }
                    }
                }
                else
                {
                    return true;
                }
            }
            else if (canUseCheckedBounds && current.LowerLimit.IsUnknown && current.UpperLimit.IsUnknown &&
                assertion.KindIs(OAK_LT_UN) && (assertion.Op1.VN == vn) &&
                assertion.Op2.KindIs(O2K_VN_ADD_CNS) && (assertion.Op2.VN != preferredBound) &&
                (assertion.Op2.Cns == 0) && (budget > 0))
            {
                var boundRange = GetRangeFromType(store.TypeOfVN(assertion.Op2.VN));
                MergeEdgeAssertionsWorker(comp, assertion.Op2.VN, preferredBound, assertions,
                    ref boundRange, canUseCheckedBounds, budget - 1, visited);
                if (!boundRange.LowerLimit.IsConstant || (boundRange.LowerLimit.Constant < 0) ||
                    !boundRange.UpperLimit.IsBinOpArray || (boundRange.UpperLimit.VN != preferredBound) ||
                    (boundRange.UpperLimit.Constant > 0))
                {
                    return true;
                }

                comparison = GT_LT;
                limit = boundRange.UpperLimit;
                unsigned = true;
            }
            else if (assertion.IsRelop && assertion.Op2.KindIs(O2K_VN_ADD_CNS) &&
                (assertion.Op2.Cns == 0) &&
                ((assertion.Op1.VN == vn) || (assertion.Op2.VN == vn)))
            {
                comparison = Compiler.AssertionDsc.ToCompareOper(assertion.Kind, out unsigned);
                if (unsigned || (budget <= 0))
                {
                    return true;
                }

                var otherVN = assertion.Op1.VN == vn ? assertion.Op2.VN : assertion.Op1.VN;
                if (assertion.Op1.VN != vn)
                {
                    comparison = SwapRelop(comparison);
                }

                var otherRange = GetRangeFromAssertionsWorker(comp, otherVN, assertions, budget - 1, visited);
                if (!otherRange.IsConstantRange())
                {
                    return true;
                }

                var derived = comparison switch
                {
                    GT_LT or GT_LE => otherRange.UpperLimit.Constant,
                    GT_GT or GT_GE => otherRange.LowerLimit.Constant,
                    _ => (int?)null,
                };
                if (derived is null)
                {
                    return true;
                }

                limit = new(LimitType.Constant, derived.Value);
                budget--;
            }
            else
            {
                return true;
            }

            assert(limit.IsConstantOrBinOp);
            if (Configuration.DEBUG && comp.verbose)
            {
                comp.optPrintAssertion(assertion, assertionIndex);
            }
            if (limit.IsBinOpArray && store.IsVNInt32Constant(limit.VN))
            {
                var simplified = new Limit(LimitType.Constant, store.ConstantValue<int>(limit.VN));
                if (simplified.AddConstant(limit.Constant))
                {
                    limit = simplified;
                }
            }

            if ((comparison == GT_LT) && !limit.AddConstant(-1))
            {
                return true;
            }

            if ((comparison == GT_GT) && !limit.AddConstant(1))
            {
                return true;
            }

            switch (comparison)
            {
                case GT_LT:
                case GT_LE:
                {
                    asserted.UpperLimit = limit;
                    if (unsigned)
                    {
                        asserted.LowerLimit = new(LimitType.Constant, 0);
                    }
                    break;
                }

                case GT_GT:
                case GT_GE:
                {
                    if (!unsigned)
                    {
                        asserted.LowerLimit = limit;
                    }
                    break;
                }

                case GT_EQ:
                {
                    asserted = new(limit);
                    break;
                }
            }

            if (!asserted.IsValid())
            {
                JITDUMP($"assertedRange is invalid: [{asserted}] - bail out\n");
                return false;
            }

            var copy = current;
            copy.LowerLimit = TightenLimit(asserted.LowerLimit, copy.LowerLimit, preferredBound, true);
            copy.UpperLimit = TightenLimit(asserted.UpperLimit, copy.UpperLimit, preferredBound, false);
            JITDUMP($"Tightening pRange: [{current}] with assertedRange: [{asserted}] into [{copy}]\n");
            if (!copy.IsValid())
            {
                JITDUMP("invalid range after tightening\n");
                return false;
            }

            current = copy;
            return true;
        });
        range = current;
    }

    private static genTreeOps SwapRelop(genTreeOps op) {
    switch (op)
    {
        case GT_LT:
            return GT_GT;
        case GT_LE:
            return GT_GE;
        case GT_GT:
            return GT_LT;
        case GT_GE:
            return GT_LE;
        case GT_EQ:
            return GT_EQ;
        case GT_NE:
            return GT_NE;
        default:
            JitExceptionHelpers.ThrowInvalidOperationException();
            return default;
    }
}}
