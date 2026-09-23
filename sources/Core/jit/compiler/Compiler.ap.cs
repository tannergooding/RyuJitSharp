// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using static RyuJitSharp.Compiler.optAssertionKind;
using static RyuJitSharp.Compiler.optOp1Kind;
using static RyuJitSharp.Compiler.optOp2Kind;

namespace RyuJitSharp;

public partial class Compiler
{
    /// <summary>Data structures for assertion prop</summary>
    public BitVecTraits? apTraits;

    public unsafe ASSERT_TP? apFull;

    public unsafe ASSERT_TP? apLocal;

    public unsafe ASSERT_TP? apLocalPostorder;

    public unsafe ASSERT_TP? apLocalIfTrue;

#if DEBUG
    private static ConfigMethodRange s_jitEnableCrossBlockLocalAssertionPropRange;
#endif

    public AssertionIndex AssertionCount => optAssertionCount;

    public static nint optCastConstantSmall(nint iconVal, var_types smallType)
    {
        switch (smallType)
        {
            case TYP_BYTE:
            {
                return unchecked((sbyte)iconVal);
            }

            case TYP_SHORT:
            {
                return unchecked((short)iconVal);
            }

            case TYP_USHORT:
            {
                return unchecked((ushort)iconVal);
            }

            case TYP_UBYTE:
            {
                return unchecked((byte)iconVal);
            }

            default:
            {
                assert(false, "Unexpected type to truncate to");
                return iconVal;
            }
        }
    }

    public AssertionIndex optAssertionIsSubrange(GenTree tree, IntegralRange range, ASSERT_TP assertions)
    {
        assert(optLocalAssertionProp);
        assert(apTraits is not null);
        var result = NO_ASSERTION_INDEX;
        _ = BitVecOps.VisitBits(apTraits, assertions, bitIndex => {
            var index = GetAssertionIndex((ushort)bitIndex);
            var assertion = optGetAssertion(index);
            if (assertion.CanPropSubRange && (assertion.Op1.LclNum == tree.AsLclVarCommon().LclNum)
                && range.Contains(assertion.Op2.Range))
            {
                result = index;
                return false;
            }

            return true;
        });
        return result;
    }

    public AssertionIndex optLocalAssertionIsEqualOrNotEqual(optOp1Kind op1Kind, int lclNum,
        optOp2Kind op2Kind, nint cnsVal, ASSERT_TP assertions)
    {
        noway_assert(op1Kind is O1K_LCLVAR);
        noway_assert(op2Kind is O2K_CONST_INT or O2K_ZEROOBJ);
        assert(optLocalAssertionProp);
        assert(apTraits is not null);
        var dependent = BitVecOps.Intersection(apTraits, GetAssertionDep(lclNum), assertions);
        var result = NO_ASSERTION_INDEX;
        _ = BitVecOps.VisitBits(apTraits, dependent, bitIndex => {
            var index = GetAssertionIndex((ushort)bitIndex);
            var assertion = optGetAssertion(index);
            if (assertion.CanPropEqualOrNotEqual && assertion.Op1.KindIs(op1Kind)
                && (assertion.Op1.LclNum == lclNum) && assertion.Op2.KindIs(op2Kind))
            {
                var constantIsEqual = assertion.Op2.KindIs(O2K_ZEROOBJ) || (assertion.Op2.IntConstant == cnsVal);
                if (constantIsEqual || assertion.KindIs(OAK_EQUAL))
                {
                    result = index;
                    return false;
                }
            }

            return true;
        });
        return result;
    }

    public static bool optAssertionProp_LclVarTypeCheck(GenTree tree, in LclVarDsc lclVarDsc, in LclVarDsc copyVarDsc)
    {
        // Small struct fields are stored at their exact width but loaded widened.
        // Substitution must not turn a short field's load into a four-byte access.
        if (copyVarDsc.lvIsStructField)
        {
            var type = copyVarDsc.Type;
            return !varTypeIsSmall(type) || (type == tree.Type);
        }

        return true;
    }

    public ref ASSERT_TP GetAssertionDep(int lclNum, bool mustExist = false)
    {
        assert(optAssertionDep is not null);

        while (optAssertionDep.Count <= lclNum)
        {
            optAssertionDep.Add([]);
        }

        ref var dep = ref CollectionsMarshal.AsSpan(optAssertionDep)[lclNum];
        if (BitVecOps.MaybeUninit(dep))
        {
            assert(!mustExist, "No dependent assertions for local var");
            assert(apTraits is not null);
            dep = BitVecOps.MakeEmpty(apTraits);
        }

        return ref CollectionsMarshal.AsSpan(optAssertionDep)[lclNum];
    }

    public void optAssertionTraitsInit(AssertionIndex assertionCount)
    {
        apTraits = new BitVecTraits(this, assertionCount);
        apFull = BitVecOps.MakeFull(apTraits);
    }

    public unsafe void optAssertionInit(bool isLocalProp)
    {
        var maxTrackedLocals = JitConfig.JitMaxLocalsToTrack;
        optLocalAssertionProp = isLocalProp;
        optCrossBlockLocalAssertionProp = isLocalProp;

        if (isLocalProp)
        {
            if (JitConfig.JitEnableCrossBlockLocalAssertionProp == 0)
            {
                JITDUMP("Disabling cross-block assertion prop by config setting\n");
                optCrossBlockLocalAssertionProp = false;
            }

#if DEBUG
            s_jitEnableCrossBlockLocalAssertionPropRange.EnsureInit(JitConfig.JitEnableCrossBlockLocalAssertionPropRange);
            if (!s_jitEnableCrossBlockLocalAssertionPropRange.Contains(info.compMethodHash()))
            {
                JITDUMP("Disabling cross-block assertion prop by config range\n");
                optCrossBlockLocalAssertionProp = false;
            }
#endif

            if (lvaCount > maxTrackedLocals)
            {
                JITDUMP("Disabling cross-block assertion prop: too many locals\n");
                optCrossBlockLocalAssertionProp = false;
            }

            if (optCrossBlockLocalAssertionProp)
            {
                // Native heuristic: about 1.16 assertions per tracked local, rounded to groups of 64.
                optMaxAssertionCount = lvaTrackedCount < 24 ? (AssertionIndex)64
                    : lvaTrackedCount < 64 ? (AssertionIndex)128
                    : (AssertionIndex)Math.Min(maxTrackedLocals, ((3 * lvaTrackedCount / 128) + 1) * 64);
                JITDUMP($"Cross-block table size {optMaxAssertionCount} (for {lvaTrackedCount} tracked locals)\n");
                optComplementaryAssertionMap = new AssertionIndex[optMaxAssertionCount + 1];
            }
            else
            {
                optMaxAssertionCount = 64;
            }

            optAssertionDep = [];
            _ = optAssertionDep.EnsureCapacity(Math.Max(1, lvaCount));
        }
        else
        {
            optMaxAssertionCount = (AssertionIndex)Math.Max(64, Math.Min(256, (lvaTrackedCount + (3 * fgBBcount) + 48) >> 2));
            optComplementaryAssertionMap = new AssertionIndex[optMaxAssertionCount + 1];
        }

        optAssertionTabPrivate = new AssertionDsc[optMaxAssertionCount];
        optAssertionTraitsInit(optMaxAssertionCount);
        optAssertionCount = 0;
        optAssertionOverflow = 0;
        optAssertionPropagated = false;
        bbJtrueAssertionOut = null;
        optCanPropLclVar = false;
    }

    public AssertionDsc optGetAssertion(AssertionIndex assertIndex)
    {
        assert((assertIndex != NO_ASSERTION_INDEX) && (assertIndex <= optAssertionCount));
        var assertion = optAssertionTabPrivate[assertIndex - 1];
#if DEBUG
        optDebugCheckAssertion(assertion);
#endif
        return assertion;
    }

    public AssertionIndex optAddAssertion(AssertionDsc newAssertion)
    {
        assert(apTraits is not null);
        var canAddNewAssertions = optAssertionCount < optMaxAssertionCount;
        if (optLocalAssertionProp)
        {
            assert(newAssertion.Op1.KindIs(O1K_LCLVAR));
            var duplicate = NO_ASSERTION_INDEX;
            _ = BitVecOps.VisitBits(apTraits, GetAssertionDep(newAssertion.Op1.LclNum), bitIndex => {
                var index = GetAssertionIndex((ushort)bitIndex);
                if (optGetAssertion(index).Equals(newAssertion, vnBased: false))
                {
                    duplicate = index;
                    return false;
                }

                return true;
            });
            if (duplicate != NO_ASSERTION_INDEX)
            {
                return duplicate;
            }
        }
        else
        {
            var mayHaveDuplicates = optAssertionHasAssertionsForVN(newAssertion.Op1.VN, canAddNewAssertions);
            if (newAssertion.Op2.KindIs(O2K_VN_ADD_CNS))
            {
                mayHaveDuplicates |= optAssertionHasAssertionsForVN(newAssertion.Op2.VN, canAddNewAssertions);
                assert(vnStore is not null);
                var addOpVN = ValueNumStore.NoVN;
                var constant = 0;
                if (vnStore.IsVNBinFuncWithConst(newAssertion.Op1.VN, VNF_ADD, ref addOpVN, ref constant))
                {
                    mayHaveDuplicates |= optAssertionHasAssertionsForVN(addOpVN, canAddNewAssertions);
                }
            }

            if (mayHaveDuplicates)
            {
                for (var index = optAssertionCount; index >= 1; index--)
                {
                    if (optGetAssertion(index).Equals(newAssertion, vnBased: true))
                    {
                        return index;
                    }
                }
            }
        }

        if (!canAddNewAssertions)
        {
            optAssertionOverflow++;
            return NO_ASSERTION_INDEX;
        }

        // Immutable descriptors can be shared without changing native value-copy semantics.
        optAssertionTabPrivate[optAssertionCount] = newAssertion;
        optAssertionCount++;
#if DEBUG
        if (verbose)
        {
            jitprintf("GenTreeNode creates assertion:\n");
            assert(optAssertionPropCurrentTree is not null);
            assert(compCurBB is not null);
            gtDispTree(optAssertionPropCurrentTree, topOnly: true);
            jitprintf($"In {FMT_BB(compCurBB.bbNum)} New {(optLocalAssertionProp ? "Local" : "Global")} ");
            optPrintAssertion(newAssertion, optAssertionCount);
        }
#endif
        optCanPropLclVar |= newAssertion.CanPropLclVar;
        if (optLocalAssertionProp)
        {
            BitVecOps.AddElemD(apTraits, GetAssertionDep(newAssertion.Op1.LclNum), optAssertionCount - 1);
            if (newAssertion.Op2.KindIs(O2K_LCLVAR_COPY))
            {
                BitVecOps.AddElemD(apTraits, GetAssertionDep(newAssertion.Op2.LclNum), optAssertionCount - 1);
            }
        }
#if DEBUG
        optDebugCheckAssertions(optAssertionCount);
#endif
        return optAssertionCount;
    }

    public void optCreateComplementaryAssertion(AssertionIndex assertionIndex)
    {
        if (assertionIndex == NO_ASSERTION_INDEX)
        {
            return;
        }

        var candidate = optGetAssertion(assertionIndex);
        if (candidate.KindIs(OAK_EQUAL))
        {
            if (candidate.Op1.KindIs(O1K_LCLVAR, O1K_VN))
            {
                if (candidate.Op2.KindIs(O2K_CONST_INT) && (candidate.Op2.IntConstant != 0) && (candidate.Op2.IntConstant != 1))
                {
                    return;
                }

                if (candidate.Op2.KindIs(O2K_LCLVAR_COPY))
                {
                    return;
                }
            }

            if (candidate.Op1.KindIs(O1K_EXACT_TYPE, O1K_SUBTYPE))
            {
                return;
            }

            optMapComplementary(optAddAssertion(candidate.Reverse()), assertionIndex);
        }
        else if (candidate.KindIs(OAK_LT_UN, OAK_LE_UN) && candidate.Op2.KindIs(O2K_VN_ADD_CNS))
        {
            return;
        }
        else if (AssertionDsc.IsReversible(candidate.Kind))
        {
            optMapComplementary(optAddAssertion(candidate.Reverse()), assertionIndex);
        }
    }

    public bool optAssertionHasAssertionsForVN(ValueNum vn, bool addIfNotFound)
    {
        assert(!optLocalAssertionProp);
        if (vn == ValueNumStore.NoVN)
        {
            assert(!addIfNotFound);
            return false;
        }

        if (addIfNotFound)
        {
            optAssertionVNsMap ??= [];
            ref var exists = ref CollectionsMarshal.GetValueRefOrAddDefault(optAssertionVNsMap, vn, out _);
            if (!exists)
            {
                exists = true;
                return false;
            }

            return true;
        }

        return (optAssertionVNsMap is not null) && optAssertionVNsMap.ContainsKey(vn);
    }

    public void optMapComplementary(AssertionIndex assertionIndex, AssertionIndex index)
    {
        if ((assertionIndex == NO_ASSERTION_INDEX) || (index == NO_ASSERTION_INDEX))
        {
            return;
        }

        assert((assertionIndex <= optMaxAssertionCount) && (index <= optMaxAssertionCount));
        optComplementaryAssertionMap[assertionIndex] = index;
        optComplementaryAssertionMap[index] = assertionIndex;
    }

    public AssertionIndex optFindComplementary(AssertionIndex assertIndex)
    {
        if (assertIndex == NO_ASSERTION_INDEX)
        {
            return NO_ASSERTION_INDEX;
        }

        var input = optGetAssertion(assertIndex);
        if (!AssertionDsc.IsReversible(input.Kind))
        {
            return NO_ASSERTION_INDEX;
        }

        var mapped = optComplementaryAssertionMap[assertIndex];
        if ((mapped != NO_ASSERTION_INDEX) && (mapped <= optAssertionCount))
        {
            return mapped;
        }

        for (AssertionIndex index = 1; index <= optAssertionCount; index++)
        {
            if (optGetAssertion(index).Complementary(input, !optLocalAssertionProp))
            {
                optMapComplementary(assertIndex, index);
                return index;
            }
        }

        return NO_ASSERTION_INDEX;
    }

    public void optAssertionReset()
    {
        assert(optLocalAssertionProp);
        assert(optAssertionCount <= optMaxAssertionCount);
        assert(apTraits is not null);

        while (optAssertionCount > 0)
        {
            var index = optAssertionCount;
            var assertion = optGetAssertion(index);
            optAssertionCount--;
            var lclNum = assertion.Op1.LclNum;
            assert(lclNum < lvaCount);
            BitVecOps.RemoveElemD(apTraits, GetAssertionDep(lclNum, mustExist: true), index - 1);
            if (assertion.IsCopyAssertion)
            {
                BitVecOps.RemoveElemD(apTraits, GetAssertionDep(assertion.Op2.LclNum, mustExist: true), index - 1);
            }
        }
    }

#if DEBUG
    public void optDebugCheckAssertion(AssertionDsc assertion)
    {
        switch (assertion.Op1.Kind)
        {
            case O1K_EXACT_TYPE:
            case O1K_SUBTYPE:
            case O1K_VN:
            {
                assert(!optLocalAssertionProp);
                break;
            }

            case O1K_LCLVAR:
            {
                assert(optLocalAssertionProp);
                break;
            }
        }

        switch (assertion.Op2.Kind)
        {
            case O2K_SUBRANGE:
            case O2K_LCLVAR_COPY:
            {
                assert(optLocalAssertionProp);
                break;
            }

            case O2K_VN_ADD_CNS:
            {
                assert(!optLocalAssertionProp);
                assert(assertion.Op1.KindIs(O1K_VN));
                assert(assertion.IsRelop || assertion.CanPropEqualOrNotEqual);
                break;
            }

            case O2K_ZEROOBJ:
            {
                assert(assertion.KindIs(OAK_EQUAL));
                break;
            }

            case O2K_CONST_DOUBLE:
            {
                assert(!double.IsNaN(assertion.Op2.DoubleConstant));
                break;
            }
        }
    }

    public void optDebugCheckAssertions(AssertionIndex index)
    {
        var start = index == NO_ASSERTION_INDEX ? (AssertionIndex)1 : index;
        var end = index == NO_ASSERTION_INDEX ? optAssertionCount : index;
        for (var current = start; current <= end; current++)
        {
            optDebugCheckAssertion(optGetAssertion(current));
        }
    }

    public unsafe void optPrintAssertion(AssertionDsc assertion, AssertionIndex assertionIndex = 0)
    {
        if (assertionIndex > 0)
        {
            optPrintAssertionIndex(assertionIndex);
            jitprintf(" ");
        }

        switch (assertion.Op1.Kind)
        {
            case O1K_LCLVAR:
            {
                jitprintf($"lclvar V{assertion.Op1.LclNum:D2}");
                break;
            }

            case O1K_VN:
            {
                jitprintf($"VN ${assertion.Op1.VN:x}");
                break;
            }

            case O1K_EXACT_TYPE:
            {
                jitprintf($"ExactType ${assertion.Op1.VN:x}");
                break;
            }

            case O1K_SUBTYPE:
            {
                jitprintf($"SubType ${assertion.Op1.VN:x}");
                break;
            }

            default:
            {
                throw new UnreachableException();
            }
        }

        jitprintf(assertion.Kind switch {
            OAK_EQUAL => " == ",
            OAK_NOT_EQUAL => " != ",
            OAK_LT => " < ",
            OAK_LT_UN => " u< ",
            OAK_LE => " <= ",
            OAK_LE_UN => " u<= ",
            OAK_GT => " > ",
            OAK_GT_UN => " u> ",
            OAK_GE => " >= ",
            OAK_GE_UN => " u>= ",
            OAK_SUBRANGE => " in ",
            _ => throw new UnreachableException(),
        });

        switch (assertion.Op2.Kind)
        {
            case O2K_LCLVAR_COPY:
            {
                jitprintf($"lclvar V{assertion.Op2.LclNum:D2}");
                break;
            }

            case O2K_CONST_INT:
            {
                if (assertion.Op1.KindIs(O1K_EXACT_TYPE, O1K_SUBTYPE))
                {
                    var icon = assertion.Op2.IntConstant;
                    if (IsAot)
                    {
                        jitprintf($"MT({FMT_PTR((void*)dspPtr((void*)icon))})");
                    }
                    else
                    {
                        jitprintf($"MT({eeGetClassName((CORINFO_CLASS_HANDLE)icon)})");
                    }
                }
                else if (assertion.Op2.IsNullConstant)
                {
                    jitprintf("null");
                }
                else if (assertion.Op2.HasIconFlag)
                {
                    jitprintf($"[{unchecked((nuint)dspPtr((void*)assertion.Op2.IntConstant)):x}]");
                }
                else
                {
                    jitprintf($"{(long)assertion.Op2.IntConstant}");
                }
                break;
            }

            case O2K_CONST_DOUBLE:
            {
                jitprintf(double.IsNegative(assertion.Op2.DoubleConstant) && (assertion.Op2.DoubleConstant == 0)
                    ? "-0.0" : formatFloatWithTrailingZeros(assertion.Op2.DoubleConstant, 6));
                break;
            }

            case O2K_CONST_VEC:
            {
                jitprintf("VecCns");
                break;
            }

            case O2K_ZEROOBJ:
            {
                jitprintf("ZeroObj");
                break;
            }

            case O2K_SUBRANGE:
            {
                IntegralRange.Print(assertion.Op2.Range);
                break;
            }

            case O2K_VN_ADD_CNS:
            {
                jitprintf($"(VN_ADD_CNS ${assertion.Op2.VN:x} + {assertion.Op2.Cns})");
                break;
            }

            default:
            {
                throw new UnreachableException();
            }
        }

        jitprintf("\n");
    }

    public static void optPrintAssertionIndex(AssertionIndex index)
    {
        jitprintf(index == NO_ASSERTION_INDEX ? "#NA" : $"#{index:D2}");
    }

    public void optPrintAssertionIndices(ASSERT_TP assertions)
    {
        assert(apTraits is not null);
        if (BitVecOps.IsEmpty(apTraits, assertions))
        {
            optPrintAssertionIndex(NO_ASSERTION_INDEX);
            return;
        }

        var first = true;
        _ = BitVecOps.VisitBits(apTraits, assertions, bitIndex => {
            if (!first)
            {
                jitprintf(" ");
            }

            first = false;
            optPrintAssertionIndex(GetAssertionIndex((ushort)bitIndex));
            return true;
        });
    }
#endif

    [Conditional("DEBUG")]
    public static void optDumpAssertionIndices(string header, ASSERT_TP assertions, string? footer = null)
    {
#if DEBUG
        var compiler = JitTls.Compiler;
        assert(compiler is not null);
        if (compiler.verbose)
        {
            jitprintf(header);
            compiler.optPrintAssertionIndices(assertions);
            if (footer is not null)
            {
                jitprintf(footer);
            }
        }
#endif
    }

    [Conditional("DEBUG")]
    public static void optDumpAssertionIndices(ASSERT_TP assertions, string? footer = null)
        => optDumpAssertionIndices("", assertions, footer);
}
