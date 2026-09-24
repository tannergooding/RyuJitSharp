// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ComparisonMorphTests
{
    [Test]
    public static void ConstantOrderingPreservesComparisonValueNumbersAndPrefersHandles()
    {
        WithCompiler(compiler => {
            var constant = compiler.gtNewIconNode(TYP_I_IMPL, 42);
            var local = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            var comparison = compiler.gtNewBinaryNode(GT_LT, TYP_INT, constant, local);
            var numbers = new ValueNumPair(23, 24);
            comparison._vnPair = numbers;
            Compiler.fgPushConstantsRight(comparison);
            Assert.That(comparison.Oper, Is.EqualTo(GT_GT));
            Assert.That(comparison.Op1, Is.SameAs(local));
            Assert.That(comparison.Op2, Is.SameAs(constant));
            Assert.That(comparison._vnPair, Is.EqualTo(numbers));

            var other = compiler.gtNewIconNode(TYP_I_IMPL, 43);
            comparison.Op1 = constant;
            comparison.Op2 = other;
            Compiler.fgPushConstantsRight(comparison);
            Assert.That(comparison.Op1, Is.SameAs(constant));
            other.Flags |= GTF_ICON_CLASS_HDL;
            Compiler.fgPushConstantsRight(comparison);
            Assert.That(comparison.Op1, Is.SameAs(other));
            Compiler.fgPushConstantsRight(comparison);
            Assert.That(comparison.Op1, Is.SameAs(other));
        });
    }

    [Test]
    public static void DriverCanonicalizesPowerOfTwoComparandsWithoutChangingTheMask()
    {
        WithCompiler(compiler => {
            var mask = compiler.gtNewIconNode(TYP_INT, 8);
            var and = compiler.gtNewBinaryNode(GT_AND, TYP_INT, compiler.gtNewLclvNode(TYP_INT, 1), mask);
            var comparison = compiler.gtNewBinaryNode(GT_EQ, TYP_INT, and, compiler.gtNewIconNode(TYP_INT, 8));
            comparison._vnPair.SetBoth(42);
            Assert.That(compiler.fgOptimizeRelationalComparison(comparison), Is.SameAs(comparison));
            Assert.That(comparison.Oper, Is.EqualTo(GT_NE));
            Assert.That(comparison.Op2.IsIntegralConst(0), Is.True);
            Assert.That(and.Op2, Is.SameAs(mask));
            Assert.That(mask.IconValue, Is.EqualTo((nint)8));
            Assert.That(comparison._vnPair.Liberal, Is.EqualTo(42));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void EqualityConstantMotionWrapsAtIntWidthButRetainsCheckedArithmetic(bool overflow)
    {
        WithCompiler(compiler => {
            var local = compiler.gtNewLclvNode(TYP_INT, 1);
            var add = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, local, compiler.gtNewIconNode(TYP_INT, 1));
            if (overflow)
            {
                add.Flags |= GTF_OVERFLOW | GTF_EXCEPT;
            }

            var comparison = compiler.gtNewBinaryNode(GT_EQ, TYP_INT, add, compiler.gtNewIconNode(TYP_INT, int.MinValue));
            Assert.That(compiler.fgOptimizeEqualityComparisonWithConst(comparison), Is.SameAs(comparison));
            Assert.That(comparison.Op1, Is.SameAs(overflow ? add : local));
            Assert.That(comparison.Op2.AsIntCon().IconValue, Is.EqualTo((nint)(overflow ? int.MinValue : int.MaxValue)));
        });
    }

    [Test]
    public static void NestedComparisonReversalPreservesNaNBehaviorAndJumpValueNumbers()
    {
        WithCompiler(compiler => {
            var inner = compiler.gtNewBinaryNode(GT_LT, TYP_INT,
                compiler.gtNewLclvNode(TYP_DOUBLE, 2), compiler.gtNewLclvNode(TYP_DOUBLE, 2));
            var outer = compiler.gtNewBinaryNode(GT_EQ, TYP_INT, inner, compiler.gtNewIconNode(TYP_INT, 0));
            outer.Flags |= GTF_RELOP_JMP_USED | GTF_DONT_CSE;
            outer._vnPair.SetBoth(42);
            Assert.That(compiler.fgOptimizeEqualityComparisonWithConst(outer), Is.SameAs(inner));
            Assert.That(inner.Oper, Is.EqualTo(GT_GE));
            Assert.That(inner.Flags & GTF_RELOP_NAN_UN, Is.EqualTo(GTF_RELOP_NAN_UN));
            Assert.That(inner.Flags & (GTF_RELOP_JMP_USED | GTF_DONT_CSE), Is.EqualTo(GTF_RELOP_JMP_USED | GTF_DONT_CSE));
            Assert.That(inner._vnPair.Liberal, Is.EqualTo(42));
        });
    }

    [TestCase(TYP_INT, -1, false)]
    [TestCase(TYP_INT, 31, true)]
    [TestCase(TYP_INT, 32, false)]
    [TestCase(TYP_LONG, 63, true)]
    public static void ConstantBitTestRespectsShiftBoundsAndSignedHighBits(var_types type, int count, bool transformed)
    {
        WithCompiler(compiler => {
            var local = compiler.gtNewLclvNode(type, type is TYP_LONG ? 0 : 1);
            var shift = compiler.gtNewBinaryNode(GT_RSZ, type, local, compiler.gtNewIconNode(TYP_INT, count));
            var and = compiler.gtNewBinaryNode(GT_AND, type, shift, compiler.gtNewOneConNode(type));
            var comparison = compiler.gtNewBinaryNode(GT_EQ, TYP_INT, and, compiler.gtNewOneConNode(type));
            Assert.That(compiler.fgOptimizeEqualityComparisonWithConst(comparison), Is.SameAs(comparison));
            Assert.That(and.Op1, Is.SameAs(transformed ? local : shift));
            Assert.That(comparison.Oper, Is.EqualTo(transformed ? GT_NE : GT_EQ));
            Assert.That(comparison.Op2.IsIntegralConst(transformed ? 0 : 1), Is.True);
            var expectedMask = transformed ? (type is TYP_LONG ? long.MinValue : int.MinValue) : 1;
            Assert.That(and.Op2.AsIntConCommon().IntegralValue, Is.EqualTo(expectedMask));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void VariableNonzeroBitTestRewritesOnlyForJumpUse(bool jump)
    {
        WithCompiler(compiler => {
            var value = compiler.gtNewLclvNode(TYP_INT, 1);
            var count = compiler.gtNewLclvNode(TYP_INT, 1);
            var shift = compiler.gtNewBinaryNode(GT_RSH, TYP_INT, value, count);
            var mask = compiler.gtNewIconNode(TYP_INT, 1);
            var and = compiler.gtNewBinaryNode(GT_AND, TYP_INT, shift, mask);
            var comparison = compiler.gtNewBinaryNode(GT_NE, TYP_INT, and, compiler.gtNewIconNode(TYP_INT, 0));
            if (jump)
            {
                comparison.Flags |= GTF_RELOP_JMP_USED;
            }

            _ = compiler.fgOptimizeEqualityComparisonWithConst(comparison);
            Assert.That(and.Op1, Is.SameAs(jump ? value : shift));
            Assert.That(and.Op2, Is.SameAs(jump ? shift : mask));
            Assert.That(shift.Oper, Is.EqualTo(jump ? GT_LSH : GT_RSH));
            Assert.That(shift.Op1, Is.SameAs(jump ? mask : value));
            Assert.That(shift.Op2, Is.SameAs(count));
        });
    }

    [TestCase(4294967295L, false, true)]
    [TestCase(-1L, false, false)]
    [TestCase(4294967295L, true, false)]
    public static void WidenedUnsignedComparisonRequiresZeroUpperHalvesAndUncheckedCasts(long value, bool overflow, bool narrow)
    {
        WithCompiler(compiler => {
            var local = compiler.gtNewLclvNode(TYP_INT, 1);
            var cast = compiler.gtNewCastNode(TYP_LONG, local, true, TYP_LONG);
            if (overflow)
            {
                cast.Flags |= GTF_OVERFLOW | GTF_EXCEPT;
            }

            var constant = compiler.gtNewLconNode(value);
            var comparison = compiler.gtNewBinaryNode(GT_LT, TYP_INT, cast, constant);
            comparison._vnPair.SetBoth(42);
            _ = compiler.fgOptimizeRelationalComparisonWithCasts(comparison);
            Assert.That(comparison.Op1, Is.SameAs(narrow ? local : cast));
            Assert.That(comparison.Op2.Type, Is.EqualTo(narrow ? TYP_INT : TYP_LONG));
            Assert.That(comparison.Op2.AsIntConCommon().IntegralValue, Is.EqualTo(narrow ? unchecked((int)value) : value));
            Assert.That(comparison.IsUnsigned, Is.EqualTo(narrow));
            Assert.That(comparison._vnPair.Liberal, Is.EqualTo(42));
            if (narrow)
            {
                Assert.That(comparison.Op2, Is.Not.SameAs(constant));
                Assert.That(constant.Type, Is.EqualTo(TYP_LONG));
#if DEBUG
                Assert.That(comparison.Op2.TreeId, Is.EqualTo(constant.TreeId));
#endif
            }
        });
    }

    [Test]
    public static void LongMaskedEqualityInstallsAllNarrowedOperands()
    {
        WithCompiler(compiler => {
            var local = compiler.gtNewLclvNode(TYP_LONG, 0);
            var mask = compiler.gtNewLconNode(uint.MaxValue);
            var constant = compiler.gtNewLconNode(42);
            var and = compiler.gtNewBinaryNode(GT_AND, TYP_LONG, local, mask);
            var comparison = compiler.gtNewBinaryNode(GT_EQ, TYP_INT, and, constant);
            _ = compiler.fgOptimizeEqualityComparisonWithConst(comparison);
            Assert.That(and.Type, Is.EqualTo(TYP_INT));
            Assert.That(and.Op1, Is.SameAs(local));
            Assert.That(local.Type, Is.EqualTo(TYP_INT));
            Assert.That(and.Op2, Is.Not.SameAs(mask));
            Assert.That(and.Op2.Type, Is.EqualTo(TYP_INT));
            Assert.That(and.Op2.AsIntCon().IconValue, Is.EqualTo((nint)(-1)));
            Assert.That(comparison.Op2, Is.Not.SameAs(constant));
            Assert.That(comparison.Op2.Type, Is.EqualTo(TYP_INT));
            Assert.That(comparison.Op2.AsIntCon().IconValue, Is.EqualTo((nint)42));
            Assert.That(mask.Type, Is.EqualTo(TYP_LONG));
            Assert.That(constant.Type, Is.EqualTo(TYP_LONG));
        });
    }

    [TestCase(GT_GT, TYP_INT, 2147483647L, GT_LT)]
    [TestCase(GT_LE, TYP_LONG, 9223372036854775807L, GT_GE)]
    [TestCase(GT_GT, TYP_INT, 0L, GT_NE)]
    public static void UnsignedSignAndZeroTestsClearTheUnsignedFlag(genTreeOps operation, var_types type, long value, genTreeOps expected)
    {
        WithCompiler(compiler => {
            var local = compiler.gtNewLclvNode(type, type is TYP_LONG ? 0 : 1);
            var constant = type is TYP_LONG ? compiler.gtNewLconNode(value) : compiler.gtNewIconNode(type, (nint)value);
            var comparison = compiler.gtNewBinaryNode(operation, TYP_INT, local, constant);
            comparison.Flags |= GTF_UNSIGNED;
            comparison._vnPair.SetBoth(42);
            _ = compiler.fgOptimizeRelationalComparisonWithConst(comparison);
            Assert.That(comparison.Oper, Is.EqualTo(expected));
            Assert.That(comparison.IsUnsigned, Is.False);
            Assert.That(comparison.Op2.IsIntegralConst(0), Is.True);
            Assert.That(comparison._vnPair.Liberal, Is.EqualTo(42));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void FullRangeFoldingPreservesObservableEvaluation(bool sideEffects)
    {
        WithCompiler(compiler => {
            GenTree operand = sideEffects
                ? compiler.gtNewCallNode(TYP_INT, gtCallTypes.CT_USER_FUNC, null)
                : compiler.gtNewLclvNode(TYP_INT, 1);
            var comparison = compiler.gtNewBinaryNode(GT_LE, TYP_INT, operand, compiler.gtNewIconNode(TYP_INT, int.MaxValue));
            var result = compiler.fgOptimizeRelationalComparisonWithFullRangeConst(comparison);
            if (sideEffects)
            {
                Assert.That(result, Is.SameAs(comparison));
            }
            else
            {
                Assert.That(result.IsIntegralConst(1), Is.True);
#if DEBUG
                Assert.That(result.TreeId, Is.Not.EqualTo(comparison.TreeId));
#endif
            }
        });
    }

    [Test]
    public static void UnsignedFullRangeIncludesBothSidesOfSignedZero()
    {
        WithCompiler(compiler => {
            var comparison = compiler.gtNewBinaryNode(GT_LT, TYP_INT,
                compiler.gtNewLclvNode(TYP_INT, 1), compiler.gtNewIconNode(TYP_INT, int.MaxValue));
            comparison.Flags |= GTF_UNSIGNED;
            Assert.That(compiler.fgOptimizeRelationalComparisonWithFullRangeConst(comparison), Is.SameAs(comparison));
            comparison.Op2.AsIntCon().IconValue = 0;
            Assert.That(compiler.fgOptimizeRelationalComparisonWithFullRangeConst(comparison).IsIntegralConst(0), Is.True);
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.opts.compFlags |= CLFLG_TREETRANS;
        compiler.fgGlobalMorph = true;
        compiler.lvaTable = [
            new LclVarDsc { Type = TYP_LONG },
            new LclVarDsc { Type = TYP_INT },
            new LclVarDsc { Type = TYP_DOUBLE },
        ];
        compiler.lvaCount = 3;
        JitTls.Compiler = compiler;
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
