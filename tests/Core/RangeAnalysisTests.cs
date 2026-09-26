// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Compiler.optAssertionKind;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.VNFunc;
using static RyuJitSharp.var_types;
using BitOps = RyuJitSharp.BitSetOps<RyuJitSharp.BitVecTraits, RyuJitSharp.BitVecTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class RangeAnalysisTests
{
    [TestCase(TYP_BYTE, -128, 127)]
    [TestCase(TYP_UBYTE, 0, 255)]
    [TestCase(TYP_SHORT, -32768, 32767)]
    [TestCase(TYP_USHORT, 0, 65535)]
    [TestCase(TYP_INT, int.MinValue, int.MaxValue)]
    public static void TypeBoundsRetainSourceSignedness(var_types type, int lower, int upper)
    {
        var range = RangeCheck.GetRangeFromType(type);
        Assert.That(range.IsConstantRange(), Is.True);
        Assert.That(range.LowerLimit.Constant, Is.EqualTo(lower));
        Assert.That(range.UpperLimit.Constant, Is.EqualTo(upper));
    }

    [Test]
    public static void CheckedEndpointsAndSymbolicMergeRespectOverflowAndArrayLength()
    {
        var bound = new Limit(LimitType.BinOpArray, 5, -3);
        var merged = RangeOps.Merge(new(new Limit(LimitType.Constant, 0)),
            new(bound, new(LimitType.BinOpArray, 5, 0)), false);
        Assert.That(merged.LowerLimit.IsConstant, Is.True);
        Assert.That(merged.LowerLimit.Constant, Is.EqualTo(-3));
        Assert.That(merged.UpperLimit.IsBinOpArray, Is.True);
        Assert.That(merged.UpperLimit.VN, Is.EqualTo(5));

        Assert.That(new Limit(LimitType.Constant, int.MaxValue).AddConstant(1), Is.False);
        Assert.That(RangeOps.Add(new(new Limit(LimitType.Constant, int.MaxValue)),
            new(new Limit(LimitType.Constant, 1))).IsConstantRange(), Is.False);
        Assert.That(RangeOps.Subtract(new(new Limit(LimitType.Constant, 0)),
            new(new Limit(LimitType.Constant, int.MinValue))).IsConstantRange(), Is.False);
        Assert.That(RangeOps.Multiply(new(new Limit(LimitType.Constant, int.MaxValue)),
            new(new Limit(LimitType.Constant, 2))).IsConstantRange(), Is.False);
        Assert.That(RangeOps.Add(new(new Limit(LimitType.Constant, -1),
            new Limit(LimitType.Constant, 1)), new(new Limit(LimitType.Constant, 2)), true).IsConstantRange(),
            Is.False);
    }

    [Test]
    public static void RangeOperationsPreserveConservativeBitAndDivisionBounds()
    {
        var unknown = new Range(new Limit(LimitType.Unknown));
        var mask = new Range(new Limit(LimitType.Constant, 7));
        var andRange = RangeOps.And(unknown, mask);
        Assert.That((andRange.LowerLimit.Constant, andRange.UpperLimit.Constant), Is.EqualTo((0, 7)));
        var modulo = RangeOps.UnsignedMod(unknown, new(new Limit(LimitType.Constant, 5)));
        Assert.That((modulo.LowerLimit.Constant, modulo.UpperLimit.Constant), Is.EqualTo((0, 4)));
        Assert.That(RangeOps.UnsignedDivide(new(new Limit(LimitType.Constant, 8),
            new Limit(LimitType.Constant, 15)), new(new Limit(LimitType.Constant, 2),
            new Limit(LimitType.Constant, 4))).UpperLimit.Constant, Is.EqualTo(7));
        Assert.That(RangeOps.ShiftRight(unknown, new(new Limit(LimitType.Constant, 1)), true)
            .UpperLimit.Constant, Is.EqualTo(int.MaxValue));
    }

    [Test]
    public static void ReachingAssertionsConstrainVNExpressionsAndRespectBudget()
    {
        WithCompiler((compiler, store) =>
        {
            var vn = store.VNForExpr(null, TYP_INT);
            var lowerIndex = compiler.optAddAssertion(
                Compiler.AssertionDsc.CreateConstantBound(compiler, VNF_GE, vn, store.VNForIntCon(10)));
            var upperIndex = compiler.optAddAssertion(
                Compiler.AssertionDsc.CreateConstantBound(compiler, VNF_LT, vn, store.VNForIntCon(20)));
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var active = BitOps.MakeEmpty(traits);
            BitOps.AddElemD(traits, active, lowerIndex - 1);
            BitOps.AddElemD(traits, active, upperIndex - 1);

            var direct = RangeCheck.GetRangeFromAssertions(compiler, vn, active);
            Assert.That((direct.LowerLimit.Constant, direct.UpperLimit.Constant), Is.EqualTo((10, 19)));

            var added = store.VNForFunc(TYP_INT, VNF_ADD, vn, store.VNForIntCon(3));
            var arithmetic = RangeCheck.GetRangeFromAssertions(compiler, added, active);
            Assert.That((arithmetic.LowerLimit.Constant, arithmetic.UpperLimit.Constant), Is.EqualTo((13, 22)));

            var exhausted = RangeCheck.GetRangeFromAssertions(compiler, vn, active, budget: 0);
            Assert.That(exhausted.IsFullRange(), Is.True);
        });
    }

    [TestCase(32, 0, int.MaxValue)]
    [TestCase(63, 0, int.MaxValue)]
    [TestCase(31, null, null)]
    [TestCase(64, null, null)]
    public static void SignedLongShiftRefinesResultAfterComputingTypeBounds(
        int shiftAmount, int? lower, int? upper)
    {
        WithCompiler((compiler, store) =>
        {
            var source = store.VNForExpr(null, TYP_LONG);
            var shifted = store.VNForFuncNoFolding(TYP_LONG, VNF_RSH, source,
                store.VNForIntCon(shiftAmount));
            var index = compiler.optAddAssertion(
                Compiler.AssertionDsc.CreateConstantBound(compiler, VNF_GE, shifted, store.VNForLongCon(0)));
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var active = BitOps.MakeEmpty(traits);
            BitOps.AddElemD(traits, active, index - 1);

            var range = RangeCheck.GetRangeFromAssertions(compiler, shifted, active);
            Assert.That(upper.HasValue, Is.EqualTo(lower.HasValue));
            if (lower is null)
            {
                Assert.That(range.IsConstantRange(), Is.False);
            }
            else if (upper is int expectedUpper)
            {
                Assert.That(range.IsConstantRange(), Is.True);
                Assert.That((range.LowerLimit.Constant, range.UpperLimit.Constant),
                    Is.EqualTo((lower.Value, expectedUpper)));
            }
        });
    }

    [Test]
    public static void CheckedBoundAssertionsRetainUnsignedNonnegativeProofWithoutSymbolicLimits()
    {
        WithCompiler((compiler, store) =>
        {
            var indexVN = store.VNForExpr(null, TYP_INT);
            var boundVN = store.VNForExpr(null, TYP_INT);
            store.SetVNIsCheckedBound(boundVN);
            var assertion = Compiler.AssertionDsc.CreateCompareCheckedBound(
                compiler, VNF_LT_UN, indexVN, boundVN, 0, isVNNeverNegative: true);
            var index = compiler.optAddAssertion(assertion);
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var active = BitOps.MakeEmpty(traits);
            BitOps.AddElemD(traits, active, index - 1);

            var constrained = RangeCheck.GetRangeFromAssertions(compiler, indexVN, active);
            Assert.That(constrained.IsConstantRange(), Is.True);
            Assert.That(constrained.LowerLimit.Constant, Is.Zero);
            Assert.That(constrained.UpperLimit.Constant, Is.EqualTo(int.MaxValue - 1));

            var withoutAssertion = RangeCheck.GetRangeFromAssertions(compiler, indexVN, BitOps.MakeEmpty(traits));
            Assert.That(withoutAssertion.IsFullRange(), Is.True);
        });
    }

    [Test]
    public static void GetRangeSelectsIncomingAssertionsAndReportsFailedSsaWalkAsUnknown()
    {
        WithCompiler((compiler, store) =>
        {
            var block = new BasicBlock(null, null);
            var vn = store.VNForExpr(null, TYP_INT);
            var index = compiler.optAddAssertion(
                Compiler.AssertionDsc.CreateConstantBound(compiler, VNF_LT, vn, store.VNForIntCon(8)));
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            block.bbAssertionIn = BitOps.MakeEmpty(traits);
            BitOps.AddElemD(traits, block.bbAssertionIn, index - 1);
            var tree = compiler.gtNewLclvNode(TYP_INT, 0);
            tree._vnPair.SetBoth(vn);

            var fast = RangeCheck.GetRange(compiler, tree, block, null);
            Assert.That(fast.UpperLimit.Constant, Is.EqualTo(7));

            var slow = RangeCheck.GetRange(compiler, tree, block, null, fast: false);
            Assert.That(slow.UpperLimit.IsUnknown, Is.True);
        });
    }

    [Test]
    public static void SsaDefinitionYieldsRangeAndBudgetExhaustionFails()
    {
        WithCompiler((compiler, store) =>
        {
            var block = new BasicBlock(null, null);
            var constant = compiler.gtNewIconNode(TYP_INT, 42);
            constant._vnPair.SetBoth(store.VNForIntCon(42));
            var definition = compiler.gtNewStoreLclVarNode(0, constant);
            ref var descriptor = ref compiler.lvaTable[0];
            var number = descriptor.lvPerSsaData.AllocSsaNum();
            definition.SsaNum = number;
            descriptor.GetPerSsaData(number) = new LclSsaVarDsc(block, definition);

            var use = compiler.gtNewLclvNode(TYP_INT, 0);
            use.SsaNum = number;
            use._vnPair.SetBoth(store.VNForExpr(null, TYP_INT));
            var analysis = compiler.GetRangeCheck();
            Assert.That(analysis.TryGetRange(block, use, out var result), Is.True);
            Assert.That(result.IsSingleValueConstant(out var value), Is.True);
            Assert.That(value, Is.EqualTo(42));

            analysis.SetBudget(1);
            Assert.That(analysis.TryGetRange(block, use, out _), Is.False);
        });
    }

    [Test]
    public static void PreferredCheckedBoundRetainsSymbolicIdentityForParameter()
    {
        WithCompiler((compiler, store) =>
        {
            var block = new BasicBlock(null, null);
            compiler.fgFirstBB = block;
            ref var descriptor = ref compiler.lvaTable[0];
            descriptor.lvIsParam = true;
            var number = descriptor.lvPerSsaData.AllocSsaNum();
            descriptor.GetPerSsaData(number).Block = block;
            var boundVN = store.VNForExpr(null, TYP_INT);
            var use = compiler.gtNewLclvNode(TYP_INT, 0);
            use.SsaNum = number;
            use._vnPair.SetBoth(boundVN);

            Assert.That(compiler.GetRangeCheck().TryGetRange(block, use, out var range, boundVN), Is.True);
            Assert.That(range.LowerLimit.IsBinOpArray, Is.True);
            Assert.That(range.UpperLimit.IsBinOpArray, Is.True);
            Assert.That(range.LowerLimit.VN, Is.EqualTo(boundVN));
            Assert.That(range.UpperLimit.Constant, Is.Zero);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void PhiMergesSsaDefinitionRangesAndChecksTheirOverflow(bool loopsBack)
    {
        WithCompiler((compiler, store) =>
        {
            var first = new BasicBlock(null, null);
            var second = new BasicBlock(null, null);
            var join = new BasicBlock(null, null);
            ref var descriptor = ref compiler.lvaTable[0];
            var firstNumber = descriptor.lvPerSsaData.AllocSsaNum();
            var secondNumber = descriptor.lvPerSsaData.AllocSsaNum();
            var phiNumber = descriptor.lvPerSsaData.AllocSsaNum();

            var firstValue = compiler.gtNewIconNode(TYP_INT, 10);
            firstValue._vnPair.SetBoth(store.VNForIntCon(10));
            var firstStore = compiler.gtNewStoreLclVarNode(0, firstValue);
            firstStore.SsaNum = firstNumber;
            descriptor.GetPerSsaData(firstNumber) = new LclSsaVarDsc(first, firstStore);

            var secondValue = compiler.gtNewIconNode(TYP_INT, 20);
            secondValue._vnPair.SetBoth(store.VNForIntCon(20));
            var secondStore = compiler.gtNewStoreLclVarNode(0, secondValue);
            secondStore.SsaNum = secondNumber;
            descriptor.GetPerSsaData(secondNumber) = new LclSsaVarDsc(second, secondStore);

            var firstArg = new GenTreePhiArg(TYP_INT, 0, firstNumber, first);
            var secondArg = new GenTreePhiArg(TYP_INT, 0,
                loopsBack ? phiNumber : secondNumber, loopsBack ? join : second);
            var phi = new GenTreePhi(TYP_INT)
            {
                FirstUse = new GenTreePhi.Use(firstArg)
                {
                    Next = new GenTreePhi.Use(secondArg),
                },
            };
            var phiStore = compiler.gtNewStoreLclVarNode(0, phi);
            phiStore.SsaNum = phiNumber;
            descriptor.GetPerSsaData(phiNumber) = new LclSsaVarDsc(join, phiStore);
            var use = compiler.gtNewLclvNode(TYP_INT, 0);
            use.SsaNum = phiNumber;
            use._vnPair.SetBoth(store.VNForExpr(null, TYP_INT));

            Assert.That(compiler.GetRangeCheck().TryGetRange(join, use, out var result), Is.True);
            Assert.That(result.LowerLimit.Constant, Is.EqualTo(10));
            if (loopsBack)
            {
                Assert.That(result.UpperLimit.IsDependent, Is.True);
            }
            else
            {
                Assert.That(result.UpperLimit.Constant, Is.EqualTo(20));
            }
        });
    }

    private static void WithCompiler(Action<Compiler, ValueNumStore> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.lvaCount = 1;
        compiler.lvaTable = [new LclVarDsc { Type = TYP_INT }];
        JitTls.Compiler = compiler;
        try
        {
            compiler.optAssertionInit(isLocalProp: false);
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            action(compiler, store);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
