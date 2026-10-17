// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Compiler.optAssertionKind;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.VNFunc;
using static RyuJitSharp.var_types;
using BitOps = RyuJitSharp.BitSetOps<RyuJitSharp.BitVecTraits, RyuJitSharp.BitVecTraits>;
using ValueNum = int;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class RangeAnalysisTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "DoesVarDefOverflow")]
    private static extern bool DoesVarDefOverflow(RangeCheck analysis, BasicBlock block,
        GenTreeLclVarCommon local, Range range);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "Widen")]
    private static extern void Widen(RangeCheck analysis, BasicBlock block, GenTree tree, ref Range range);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "IsMonotonicallyIncreasing")]
    private static extern bool IsMonotonicallyIncreasing(
        RangeCheck analysis, GenTree tree, bool rejectNegativeConstant);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "DoesBinOpOverflow")]
    private static extern bool DoesBinOpOverflow(RangeCheck analysis, BasicBlock block, GenTreeOp binary, Range range);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "MergeAssertion")]
    private static extern void MergeAssertion(RangeCheck analysis, BasicBlock block, GenTree tree, ref Range range);

    [Test]
    public static void WidenDiagnosticPrintsBlockAndTree()
    {
        WithCompiler((compiler, _) =>
        {
            var block = new BasicBlock(null, null) { bbNum = 12 };
            var tree = compiler.gtNewLclvNode(TYP_INT, 0);
            var analysis = compiler.GetRangeCheck();
            var range = new Range(new Limit(LimitType.Constant, 0), new Limit(LimitType.Constant, 1));

            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true);
            var previous = Globals.s_jitstdout;
            Globals.s_jitstdout = writer;
            compiler.verbose = true;

            try
            {
                Widen(analysis, block, tree, ref range);
                writer.Flush();

                Assert.That(Encoding.UTF8.GetString(stream.ToArray()),
                    Is.EqualTo($"[RangeCheck::Widen] {FMT_BB(block.bbNum)}, \n[{tree.TreeId:D6}]\n"));
            }
            finally
            {
                Globals.s_jitstdout = previous;
            }
        });
    }

    [Test]
    public static void BetweenBoundsDiagnosticFormatsValueNumberAsHexadecimal()
    {
        WithCompiler((compiler, store) =>
        {
            var upper = compiler.gtNewLclvNode(TYP_INT, 0);
            var upperVN = store.VNNormalValue(upper._vnPair.Conservative);
            var range = new Range(new Limit(LimitType.Constant, 0), new Limit(LimitType.Constant, 1));

            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true);
            var previous = Globals.s_jitstdout;
            Globals.s_jitstdout = writer;
            compiler.verbose = true;

            try
            {
                _ = compiler.GetRangeCheck().BetweenBounds(range, upper, 0);
                writer.Flush();

                Assert.That(Encoding.UTF8.GetString(stream.ToArray()),
                    Does.Contain($"${upperVN:x} upper bound is: "));
            }
            finally
            {
                Globals.s_jitstdout = previous;
            }
        });
    }

    [Test]
    public static void BinaryOverflowDiagnosticPrintsOperatorAndOperandRanges()
    {
        WithCompiler((compiler, _) =>
        {
            var block = new BasicBlock(null, null);
            var left = compiler.gtNewLclvNode(TYP_INT, 0);
            var right = compiler.gtNewLclvNode(TYP_INT, 1);
            var binary = compiler.gtNewBinaryNode(genTreeOps.GT_ADD, TYP_INT, left, right);
            var analysis = compiler.GetRangeCheck();
            var rangeMapField = typeof(RangeCheck).GetField("_rangeMap", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("RangeCheck range map was not found.");
            var searchPathField = typeof(RangeCheck).GetField("_searchPath", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("RangeCheck search path was not found.");
            var rangeMap = (Dictionary<GenTree, Range>)(rangeMapField.GetValue(analysis)
                ?? throw new InvalidOperationException("RangeCheck range map was not initialized."));
            var searchPath = (Dictionary<GenTree, BasicBlock?>)(searchPathField.GetValue(analysis)
                ?? throw new InvalidOperationException("RangeCheck search path was not initialized."));
            var leftRange = new Range(new Limit(LimitType.Constant, 0), new Limit(LimitType.Constant, 10));
            var rightRange = new Range(new Limit(LimitType.Constant, 0), new Limit(LimitType.Constant, 5));
            rangeMap[left] = leftRange;
            rangeMap[right] = rightRange;
            searchPath[left] = block;
            searchPath[right] = block;

            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true);
            var previous = Globals.s_jitstdout;
            Globals.s_jitstdout = writer;
            compiler.verbose = true;

            try
            {
                var range = new Range(new Limit(LimitType.Constant, 0), new Limit(LimitType.Constant, 15));
                Assert.That(DoesBinOpOverflow(analysis, block, binary, range), Is.False);
                writer.Flush();

                Assert.That(Encoding.UTF8.GetString(stream.ToArray()),
                    Is.EqualTo($"Checking bin op overflow ADD {leftRange} {rightRange}\n"));
            }
            finally
            {
                Globals.s_jitstdout = previous;
            }
        });
    }

    [TestCase(false, false, false)]
    [TestCase(false, true, false)]
    [TestCase(true, false, false)]
    [TestCase(true, true, false)]
    [TestCase(true, true, true)]
    public static void AssertionMergeDiagnosticsPrintConservativeNormalValueAndPhiEdge(
        bool phi,
        bool verbose,
        bool emptyPhiEdgeAssertions)
    {
        WithCompiler((compiler, store) =>
        {
            var block = new BasicBlock(null, null) { bbNum = 12 };
            var predecessor = new BasicBlock(null, null) { bbNum = 7 };
            var normal = store.VNForIntCon(42);
            var exception = store.VNForFunc(TYP_REF, VNF_OverflowExc, normal);
            var conservative = store.VNWithExc(normal, store.VNExcSetSingleton(exception));
            GenTree tree;

            if (phi)
            {
                compiler.fgFirstBB = predecessor;
                var assertionIndex = compiler.optAddAssertion(
                    Compiler.AssertionDsc.CreateConstantBound(compiler, VNF_GE, normal, store.VNForIntCon(0)));
                Assert.That(assertionIndex, Is.EqualTo(1));
                var traits = compiler.apTraits ?? throw new InvalidOperationException();
                predecessor.bbAssertionOut = BitOps.MakeEmpty(traits);
                if (!emptyPhiEdgeAssertions)
                {
                    BitOps.AddElemD(traits, predecessor.bbAssertionOut, assertionIndex - 1);
                }

                block.bbPreds = new FlowEdge(predecessor, block, null);
                tree = new GenTreePhiArg(TYP_INT, 0, SsaConfig.RESERVED_SSA_NUM, predecessor);
            }
            else
            {
                tree = compiler.gtNewLclvNode(TYP_INT, 0);
            }

            tree._vnPair = new(store.VNForIntCon(7), conservative);
            var range = new Range(new Limit(LimitType.Unknown));
            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true);
            var previous = Globals.s_jitstdout;
            Globals.s_jitstdout = writer;
            compiler.verbose = verbose;

            try
            {
                MergeAssertion(compiler.GetRangeCheck(), block, tree, ref range);
                writer.Flush();
                var expected = "";

                if (verbose)
                {
                    expected = $"Merging assertions from pred edges of BB12 for op [{tree.TreeId:D6}] ${normal:x}\n";

                    if (phi && !emptyPhiEdgeAssertions)
                    {
                        expected += "Merge assertions created by BB07 for BB12\n#01\n";
                    }
                }

                Assert.That(Encoding.UTF8.GetString(stream.ToArray()), Is.EqualTo(expected));
                Assert.That(range.LowerLimit.IsUnknown && range.UpperLimit.IsUnknown, Is.True);
            }
            finally
            {
                Globals.s_jitstdout = previous;
            }
        });
    }
#endif

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
    public static void RelationalRangesCompareSymbolicBoundsWithMatchingValueNumbers()
    {
        var boundVN = 17;
        var left = new Range(new Limit(LimitType.BinOpArray, boundVN, -4),
            new Limit(LimitType.BinOpArray, boundVN, -2));
        var right = new Range(new Limit(LimitType.BinOpArray, boundVN, -1),
            new Limit(LimitType.BinOpArray, boundVN, 0));
        var symbolic = new Range(new Limit(LimitType.BinOpArray, boundVN, 0));

        var ordered = RangeOps.EvalRelop(genTreeOps.GT_LT, isUnsigned: false, left, right);
        var unsignedEqual = RangeOps.EvalRelop(genTreeOps.GT_GE, isUnsigned: true, symbolic, symbolic);
        var unsignedGreater = RangeOps.EvalRelop(genTreeOps.GT_GT, isUnsigned: true, symbolic, symbolic);
        var unsignedNegative = RangeOps.EvalRelop(genTreeOps.GT_GE, isUnsigned: true, left, right);

        Assert.That(ordered.IsSingleValueConstant(out var orderedValue), Is.True);
        Assert.That(orderedValue, Is.EqualTo(1));
        Assert.That(unsignedEqual.IsSingleValueConstant(out var equalValue), Is.True);
        Assert.That(equalValue, Is.EqualTo(1));
        Assert.That(unsignedGreater.IsSingleValueConstant(out var greaterValue), Is.True);
        Assert.That(greaterValue, Is.Zero);
        Assert.That(unsignedNegative.IsSingleValueConstant(out _), Is.False);
    }

    [Test]
    public static void PreferredCheckedBoundSubstitutionFollowsShortAssertionChains()
    {
        WithCompiler((compiler, store) =>
        {
            var indexVN = store.VNForExpr(null, TYP_INT);
            var checkedBoundVN = store.VNForExpr(null, TYP_INT);
            var preferredBoundVN = store.VNForExpr(null, TYP_INT);
            store.SetVNIsCheckedBound(checkedBoundVN);
            store.SetVNIsCheckedBound(preferredBoundVN);

            var nonnegativeBound = compiler.optAddAssertion(Compiler.AssertionDsc.CreateConstantBound(
                compiler, VNF_GE, checkedBoundVN, store.VNForIntCon(0)));
            var nonnegativePreferred = compiler.optAddAssertion(Compiler.AssertionDsc.CreateConstantBound(
                compiler, VNF_GE, preferredBoundVN, store.VNForIntCon(0)));
            var boundRelationship = compiler.optAddAssertion(Compiler.AssertionDsc.CreateCompareCheckedBound(
                compiler, VNF_LE, checkedBoundVN, preferredBoundVN, 0));
            var indexLimit = compiler.optAddAssertion(Compiler.AssertionDsc.CreateCompareCheckedBound(
                compiler, VNF_LE, indexVN, checkedBoundVN, -3));
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var active = BitOps.MakeEmpty(traits);
            foreach (var index in new[] { nonnegativeBound, nonnegativePreferred, boundRelationship, indexLimit })
            {
                BitOps.AddElemD(traits, active, index - 1);
            }

            var range = MergeEdgeAssertionsWorker(compiler, indexVN, preferredBoundVN, active);

            Assert.That(range.UpperLimit.IsBinOpArray, Is.True);
            Assert.That(range.UpperLimit.VN, Is.EqualTo(preferredBoundVN));
            Assert.That(range.UpperLimit.Constant, Is.EqualTo(-3));
        });
    }

    [Test]
    public static void BoundMinusIndexUsesPreferredCheckedBoundRange()
    {
        WithCompiler((compiler, store) =>
        {
            var block = new BasicBlock(null, null);
            compiler.fgFirstBB = block;
            var indexVN = store.VNForExpr(null, TYP_INT);
            var boundVN = store.VNForExpr(null, TYP_INT);
            store.SetVNIsCheckedBound(boundVN);

            var lower = compiler.optAddAssertion(
                Compiler.AssertionDsc.CreateConstantBound(compiler, VNF_GE, indexVN, store.VNForIntCon(0)));
            var boundNonnegative = compiler.optAddAssertion(
                Compiler.AssertionDsc.CreateConstantBound(compiler, VNF_GE, boundVN, store.VNForIntCon(0)));
            var upper = compiler.optAddAssertion(Compiler.AssertionDsc.CreateCompareCheckedBound(
                compiler, VNF_LE, indexVN, boundVN, -2));
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var active = BitOps.MakeEmpty(traits);
            BitOps.AddElemD(traits, active, lower - 1);
            BitOps.AddElemD(traits, active, boundNonnegative - 1);
            BitOps.AddElemD(traits, active, upper - 1);
            block.bbAssertionIn = active;

            ref var descriptor = ref compiler.lvaTable[0];
            descriptor.lvIsParam = true;
            var ssaNumber = descriptor.lvPerSsaData.AllocSsaNum();
            descriptor.GetPerSsaData(ssaNumber).Block = block;
            descriptor.GetPerSsaData(ssaNumber)._vnPair.SetBoth(indexVN);

            var index = compiler.gtNewLclvNode(TYP_INT, 0);
            index.SsaNum = ssaNumber;
            index._vnPair.SetBoth(indexVN);
            var bound = compiler.gtNewLclvNode(TYP_INT, 0);
            bound._vnPair.SetBoth(boundVN);
            var subtraction = compiler.gtNewBinaryNode(genTreeOps.GT_SUB, TYP_INT, bound, index);

            var range = GetBoundMinusXRange(compiler, subtraction, block, boundVN);

            Assert.That(range.LowerLimit.IsConstant, Is.True);
            Assert.That(range.LowerLimit.Constant, Is.EqualTo(2));
            Assert.That(range.UpperLimit.IsBinOpArray, Is.True);
            Assert.That(range.UpperLimit.VN, Is.EqualTo(boundVN));
        });
    }

    [Test]
    public static void WidenImprovesNegativeConstantLowerBoundAndKeepsUpperBound()
    {
        WithCompiler((compiler, store) =>
        {
            var block = new BasicBlock(null, null);
            compiler.fgFirstBB = block;

            var value = compiler.gtNewIconNode(TYP_INT, 5);
            value._vnPair.SetBoth(store.VNForIntCon(5));
            var definition = compiler.gtNewStoreLclVarNode(0, value);
            ref var descriptor = ref compiler.lvaTable[0];
            var ssaNumber = descriptor.lvPerSsaData.AllocSsaNum();
            definition.SsaNum = ssaNumber;
            descriptor.GetPerSsaData(ssaNumber) = new LclSsaVarDsc(block, definition);

            var local = compiler.gtNewLclvNode(TYP_INT, 0);
            local.SsaNum = ssaNumber;
            local._vnPair.SetBoth(store.VNForExpr(null, TYP_INT));
            var range = new Range(new Limit(LimitType.Constant, -10), new Limit(LimitType.Constant, 10));

            Widen(compiler.GetRangeCheck(), block, local, ref range);

            Assert.That(range.LowerLimit.Constant, Is.EqualTo(5));
            Assert.That(range.UpperLimit.Constant, Is.EqualTo(10));
        });
    }

    [Test]
    public static void MonotonicityPropagatesNegativeConstantRejectionThroughSsaAdds()
    {
        WithCompiler((compiler, store) =>
        {
            var block = new BasicBlock(null, null);
            compiler.fgFirstBB = block;

            var negative = compiler.gtNewIconNode(TYP_INT, -1);
            negative._vnPair.SetBoth(store.VNForIntCon(-1));
            var definition = compiler.gtNewStoreLclVarNode(0, negative);
            ref var descriptor = ref compiler.lvaTable[0];
            var ssaNumber = descriptor.lvPerSsaData.AllocSsaNum();
            definition.SsaNum = ssaNumber;
            descriptor.GetPerSsaData(ssaNumber) = new LclSsaVarDsc(block, definition);

            var local = compiler.gtNewLclvNode(TYP_INT, 0);
            local.SsaNum = ssaNumber;
            local._vnPair.SetBoth(store.VNForExpr(null, TYP_INT));
            var positive = compiler.gtNewIconNode(TYP_INT, 1);
            positive._vnPair.SetBoth(store.VNForIntCon(1));
            var addition = compiler.gtNewBinaryNode(genTreeOps.GT_ADD, TYP_INT, local, positive);

            var analysis = compiler.GetRangeCheck();
            Assert.That(IsMonotonicallyIncreasing(analysis, addition, rejectNegativeConstant: true), Is.False);
            Assert.That(IsMonotonicallyIncreasing(analysis, addition, rejectNegativeConstant: false), Is.True);
        });
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

    [TestCase(-1, -1, false)]
    [TestCase(-1, -1, true)]
    [TestCase(int.MinValue, -1, false)]
    [TestCase(int.MinValue, -1, true)]
    [TestCase(-1, 31, false)]
    [TestCase(-1, 31, true)]
    [TestCase(0, 32, false)]
    [TestCase(0, 32, true)]
    [TestCase(32, int.MaxValue, false)]
    [TestCase(32, int.MaxValue, true)]
    public static void RightShiftRejectsOutOfRangeCounts(int low, int high, bool logical)
    {
        var left = new Range(new Limit(LimitType.Constant, 64), new Limit(LimitType.Constant, 1024));
        var right = new Range(new Limit(LimitType.Constant, low), new Limit(LimitType.Constant, high));

        var result = RangeOps.ShiftRight(left, right, logical);

        Assert.That(result.LowerLimit.IsUnknown, Is.True);
        Assert.That(result.UpperLimit.IsUnknown, Is.True);
    }

    [TestCase(0, 0, 64, 1024, false)]
    [TestCase(0, 0, 64, 1024, true)]
    [TestCase(0, 31, 0, 1024, false)]
    [TestCase(0, 31, 0, 1024, true)]
    [TestCase(1, 3, 8, 512, false)]
    [TestCase(1, 3, 8, 512, true)]
    [TestCase(31, 31, 0, 0, false)]
    [TestCase(31, 31, 0, 0, true)]
    public static void RightShiftPreservesValidCountBounds(int low, int high, int expectedLow, int expectedHigh,
        bool logical)
    {
        var left = new Range(new Limit(LimitType.Constant, 64), new Limit(LimitType.Constant, 1024));
        var right = new Range(new Limit(LimitType.Constant, low), new Limit(LimitType.Constant, high));

        var result = RangeOps.ShiftRight(left, right, logical);

        Assert.That(result.IsConstantRange(), Is.True);
        Assert.That((result.LowerLimit.Constant, result.UpperLimit.Constant), Is.EqualTo((expectedLow, expectedHigh)));
    }

    [TestCase(false, 0)]
    [TestCase(false, 31)]
    [TestCase(true, 0)]
    [TestCase(true, 31)]
    public static void LogicalShiftIncludingZeroDoesNotInferBoundsForPossiblyNegativeInput(
        bool unknownLower, int maximumShift)
    {
        var lower = unknownLower ? new Limit(LimitType.Unknown) : new Limit(LimitType.Constant, -1);
        var left = new Range(lower, new Limit(LimitType.Constant, 1024));
        var counts = new Range(new Limit(LimitType.Constant, 0),
            new Limit(LimitType.Constant, maximumShift));

        var result = RangeOps.ShiftRight(left, counts, logical: true);

        Assert.That(result.LowerLimit.IsUnknown, Is.True);
        Assert.That(result.UpperLimit.IsUnknown, Is.True);
    }

    [TestCase(1, int.MaxValue)]
    [TestCase(31, 1)]
    public static void PositiveLogicalShiftCountsUseUnsignedTypeBounds(int shift, int maximum)
    {
        var left = new Range(new Limit(LimitType.Constant, -1), new Limit(LimitType.Constant, 1024));

        var result = RangeOps.ShiftRight(left, new(new Limit(LimitType.Constant, shift)), logical: true);

        Assert.That(result.IsConstantRange(), Is.True);
        Assert.That((result.LowerLimit.Constant, result.UpperLimit.Constant), Is.EqualTo((0, maximum)));
    }

    [TestCase(TYP_FLOAT)]
    [TestCase(TYP_DOUBLE)]
    public static void IntegerAssertionBoundsDoNotPassThroughFloatingPointCasts(var_types floatingType)
    {
        WithCompiler((compiler, store) =>
        {
            var source = store.VNForExpr(null, TYP_INT);
            var lowerIndex = compiler.optAddAssertion(
                Compiler.AssertionDsc.CreateConstantBound(compiler, VNF_GE, source, store.VNForIntCon(16_777_217)));
            var upperIndex = compiler.optAddAssertion(
                Compiler.AssertionDsc.CreateConstantBound(compiler, VNF_LE, source, store.VNForIntCon(16_777_217)));
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var active = BitOps.MakeEmpty(traits);
            BitOps.AddElemD(traits, active, lowerIndex - 1);
            BitOps.AddElemD(traits, active, upperIndex - 1);

            var floating = store.VNForFuncNoFolding(floatingType, VNF_Cast, source,
                store.VNForCastOper(floatingType, srcIsUnsigned: false));
            var integer = store.VNForFuncNoFolding(TYP_INT, VNF_Cast, floating,
                store.VNForCastOper(TYP_INT, srcIsUnsigned: false));
            var floatingRange = RangeCheck.GetRangeFromAssertions(compiler, floating, active);
            var integerRange = RangeCheck.GetRangeFromAssertions(compiler, integer, active);

            Assert.That(floatingRange.LowerLimit.IsUnknown && floatingRange.UpperLimit.IsUnknown, Is.True);
            Assert.That(integerRange.IsFullRange(), Is.True);
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void DependentLimitsStillRequireDefinitionOverflowChecks(bool dependentLower, bool dependentUpper)
    {
        WithCompiler((compiler, store) =>
        {
            var block = new BasicBlock(null, null);
            var vn = store.VNForExpr(null, TYP_INT);
            var lowerIndex = compiler.optAddAssertion(
                Compiler.AssertionDsc.CreateConstantBound(compiler, VNF_GE, vn, store.VNForIntCon(0)));
            var upperIndex = compiler.optAddAssertion(
                Compiler.AssertionDsc.CreateConstantBound(compiler, VNF_LE, vn, store.VNForIntCon(9)));
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            block.bbAssertionIn = BitOps.MakeEmpty(traits);
            BitOps.AddElemD(traits, block.bbAssertionIn, lowerIndex - 1);
            BitOps.AddElemD(traits, block.bbAssertionIn, upperIndex - 1);

            var left = compiler.gtNewIconNode(TYP_INT, int.MaxValue);
            left._vnPair.SetBoth(store.VNForIntCon(int.MaxValue));
            var right = compiler.gtNewIconNode(TYP_INT, 1);
            right._vnPair.SetBoth(store.VNForIntCon(1));
            var addition = compiler.gtNewBinaryNode(genTreeOps.GT_ADD, TYP_INT, left, right);
            addition._vnPair.SetBoth(store.VNForFuncNoFolding(TYP_INT, VNF_ADD,
                left._vnPair.Conservative, right._vnPair.Conservative));
            var definition = compiler.gtNewStoreLclVarNode(0, addition);
            ref var descriptor = ref compiler.lvaTable[0];
            var number = descriptor.lvPerSsaData.AllocSsaNum();
            definition.SsaNum = number;
            descriptor.GetPerSsaData(number) = new LclSsaVarDsc(block, definition);
            descriptor.GetPerSsaData(number)._vnPair.SetBoth(vn);

            var local = compiler.gtNewLclvNode(TYP_INT, 0);
            local.SsaNum = number;
            local._vnPair.SetBoth(vn);
            var range = new Range(
                dependentLower ? new Limit(LimitType.Dependent) : new Limit(LimitType.Constant, 0),
                dependentUpper ? new Limit(LimitType.Dependent) : new Limit(LimitType.Constant, 9));
            var analysis = compiler.GetRangeCheck();
            Assert.That(analysis.TryGetRange(block, addition, out _), Is.False);

            Assert.That(DoesVarDefOverflow(analysis, block, local, range),
                Is.EqualTo(dependentLower || dependentUpper));
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

    [TestCase(false, false, false)]
    [TestCase(true, false, true)]
    [TestCase(false, true, true)]
    public static void CheckedBoundRelationalAssertionsRequireNonnegativeOrPreferredProof(
        bool nonnegative, bool preferred, bool expected)
    {
        WithCompiler((compiler, store) =>
        {
            var indexVN = store.VNForExpr(null, TYP_INT);
            var boundVN = store.VNForExpr(null, TYP_INT);
            store.SetVNIsCheckedBound(boundVN);
            var assertion = Compiler.AssertionDsc.CreateCompareCheckedBound(
                compiler, VNF_LT, indexVN, boundVN, 0, isVNNeverNegative: nonnegative);
            var index = compiler.optAddAssertion(assertion);
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var active = BitOps.MakeEmpty(traits);
            BitOps.AddElemD(traits, active, index - 1);
            var preferredBound = preferred ? boundVN : ValueNumStore.NoVN;

            var range = MergeEdgeAssertionsWorker(compiler, indexVN, preferredBound, active);

            Assert.That(range.UpperLimit.IsUnknown, Is.EqualTo(!expected));
        });
    }

    [TestCase(false, false, false)]
    [TestCase(true, false, true)]
    [TestCase(false, true, true)]
    public static void CheckedBoundEqualityAssertionsRequireNonnegativeOrPreferredProof(
        bool nonnegative, bool preferred, bool expected)
    {
        WithCompiler((compiler, store) =>
        {
            var indexVN = store.VNForExpr(null, TYP_INT);
            var boundVN = store.VNForExpr(null, TYP_INT);
            store.SetVNIsCheckedBound(boundVN);
            var assertion = Compiler.AssertionDsc.CreateCompareCheckedBound(
                compiler, VNF_EQ, indexVN, boundVN, 0, isVNNeverNegative: nonnegative);
            var index = compiler.optAddAssertion(assertion);
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var active = BitOps.MakeEmpty(traits);
            BitOps.AddElemD(traits, active, index - 1);
            var preferredBound = preferred ? boundVN : ValueNumStore.NoVN;

            var range = MergeEdgeAssertionsWorker(compiler, indexVN, preferredBound, active);

            Assert.That(range.LowerLimit.IsUnknown, Is.EqualTo(!expected));
            Assert.That(range.UpperLimit.IsUnknown, Is.EqualTo(!expected));
        });
    }

    [TestCase(false, false, false)]
    [TestCase(true, false, true)]
    [TestCase(false, true, true)]
    public static void CheckedBoundOnLeftRequiresNonnegativeOrPreferredProof(
        bool nonnegative, bool preferred, bool expected)
    {
        WithCompiler((compiler, store) =>
        {
            var leftBoundVN = nonnegative
                ? store.VNForFunc(TYP_INT, VNF_ARR_LENGTH, store.VNForExpr(null, TYP_REF))
                : store.VNForExpr(null, TYP_INT);
            var rightBoundVN = store.VNForExpr(null, TYP_INT);
            store.SetVNIsCheckedBound(leftBoundVN);
            store.SetVNIsCheckedBound(rightBoundVN);
            var assertion = Compiler.AssertionDsc.CreateCompareCheckedBound(
                compiler, VNF_LT, leftBoundVN, rightBoundVN, 0);
            var index = compiler.optAddAssertion(assertion);
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var active = BitOps.MakeEmpty(traits);
            BitOps.AddElemD(traits, active, index - 1);
            var preferredBound = preferred ? leftBoundVN : ValueNumStore.NoVN;

            var range = MergeEdgeAssertionsWorker(compiler, rightBoundVN, preferredBound, active);

            Assert.That(range.LowerLimit.IsUnknown, Is.EqualTo(!expected));
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

    private static Range MergeEdgeAssertionsWorker(Compiler compiler, ValueNum vn,
        ValueNum preferredBound, object assertions)
    {
        var method = typeof(RangeCheck).GetMethod("MergeEdgeAssertionsWorker",
            BindingFlags.NonPublic | BindingFlags.Static) ??
            throw new AssertionException("Missing edge assertion worker.");
        object?[] arguments =
        [
            compiler,
            vn,
            preferredBound,
            assertions,
            new Range(new Limit(LimitType.Unknown)),
            true,
            3,
            new HashSet<ValueNum>(),
        ];

        _ = method.Invoke(null, arguments);
        return arguments[4] is Range range
            ? range
            : throw new AssertionException("Range worker did not update its range.");
    }

    private static Range GetBoundMinusXRange(Compiler compiler, GenTree tree, BasicBlock block, ValueNum boundVN)
    {
        var method = typeof(Compiler).GetMethod("GetBoundMinusXRange",
            BindingFlags.NonPublic | BindingFlags.Static) ??
            throw new AssertionException("Missing bound-minus-index range helper.");
        return method.Invoke(null, [compiler, tree, block, boundVN]) is Range range
            ? range
            : throw new AssertionException("Bound-minus-index helper did not return a range.");
    }
}
