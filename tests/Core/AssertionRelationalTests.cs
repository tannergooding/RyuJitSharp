// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
#if DEBUG
using System.IO;
#endif
using System.Reflection;
using System.Runtime.CompilerServices;
#if DEBUG
using System.Text;
#endif
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.VNFunc;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;
using AssertionDsc = RyuJitSharp.Compiler.AssertionDsc;
using BitOps = RyuJitSharp.BitSetOps<RyuJitSharp.BitVecTraits, RyuJitSharp.BitVecTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class AssertionRelationalTests
{
    [TestCase(true, true)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    public static void GlobalEqualityLookupRequiresActiveMatchingConservativeVNs(bool equals, bool matches)
    {
        WithCompiler((compiler, store) =>
        {
            var leftVN = store.VNForExpr(null, TYP_INT);
            var constantVN = store.VNForIntCon(17);
            var left = compiler.gtNewLclvNode(TYP_INT, 0);
            left._vnPair.SetBoth(leftVN);
            var right = compiler.gtNewIconNode(TYP_INT, 17);
            right._vnPair.SetBoth(matches ? constantVN : store.VNForIntCon(18));
            var assertion = AssertionDsc.CreateInt32ConstantVNAssertion(compiler, leftVN, constantVN, equals);
            var index = compiler.optAddAssertion(assertion);
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var active = BitOps.MakeEmpty(traits);

            Assert.That(compiler.optGlobalAssertionIsEqualOrNotEqual(active, left, right), Is.Zero);
            BitOps.AddElemD(traits, active, index - 1);
            Assert.That(compiler.optGlobalAssertionIsEqualOrNotEqual(active, left, right),
                Is.EqualTo(matches ? index : 0));
        });
    }

    [TestCase(true, 1)]
    [TestCase(false, 0)]
    public static void ExactTypeLookupRecognizesInvariantNonNullVtableLoad(bool exact, int expected)
    {
        WithCompiler((compiler, store) =>
        {
            var objectVN = store.VNForExpr(null, TYP_REF);
            var typeVN = store.VNForHandle(0x1000, GTF_ICON_CLASS_HDL);
            store.AddToEmbeddedHandleMap(0x1000, 0x2000);
            var assertion = AssertionDsc.CreateSubtype(compiler, objectVN, typeVN, exact);
            var index = compiler.optAddAssertion(assertion);
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var active = BitOps.MakeEmpty(traits);
            BitOps.AddElemD(traits, active, index - 1);

            var left = compiler.gtNewIconNode(TYP_I_IMPL, 0);
            left._vnPair.SetBoth(store.VNForFunc(TYP_I_IMPL, VNF_InvariantNonNullLoad, objectVN));
            var right = compiler.gtNewIconNode(TYP_I_IMPL, 0);
            right._vnPair.SetBoth(typeVN);

            Assert.That(compiler.optGlobalAssertionIsEqualOrNotEqual(active, left, right),
                Is.EqualTo(expected));
        });
    }

    [TestCase(GT_GE, VNF_GE, false, false, 1)]
    [TestCase(GT_LT, VNF_GE, false, false, 0)]
    [TestCase(GT_GE, VNF_GE_UN, true, false, 1)]
    [TestCase(GT_GE, VNF_GE_UN, false, false, -1)]
    [TestCase(GT_GE, VNF_GE, false, true, -1)]
    public static void GlobalRelopMatchesOnlyEquivalentSignednessAndActiveAssertions(
        genTreeOps comparison, VNFunc asserted, bool unsigned, bool empty, int expected)
    {
        WithCompiler((compiler, store) =>
        {
            var leftVN = store.VNForExpr(null, TYP_INT);
            var rightVN = store.VNForExpr(null, TYP_INT);
            var left = compiler.gtNewLclvNode(TYP_INT, 0);
            left._vnPair.SetBoth(leftVN);
            var right = compiler.gtNewLclvNode(TYP_INT, 1);
            right._vnPair.SetBoth(rightVN);
            var relop = compiler.gtNewBinaryNode(comparison, TYP_INT, left, right);
            relop.AsOp().IsUnsigned = unsigned;
            relop._vnPair.SetBoth(store.VNForExpr(null, TYP_INT));
            var statement = compiler.gtNewStmt(relop);
            var block = new BasicBlock(null, null);
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var assertions = BitOps.MakeEmpty(traits);
            var index = compiler.optAddAssertion(AssertionDsc.CreateRelopVN(compiler, asserted, leftVN, rightVN));
            if (!empty)
            {
                BitOps.AddElemD(traits, assertions, index - 1);
            }

            var result = compiler.optAssertionProp_RelOp(assertions, relop, statement, block);
            if (expected < 0)
            {
                Assert.That(result, Is.Null);
                Assert.That(statement.RootNode, Is.SameAs(relop));
            }
            else
            {
                Assert.That(result?.AsIntCon().IconValue, Is.EqualTo((nint)expected));
                Assert.That(statement.RootNode, Is.SameAs(result));
                Assert.That(relop.Oper, Is.EqualTo(comparison));
            }
        });
    }

    [TestCase(true, 0)]
    [TestCase(false, 1)]
    public static void AssertedJtrueRelopResultFoldsWithoutReevaluatingOperands(bool equalsZero, int expected)
    {
        WithCompiler((compiler, store) =>
        {
            var left = compiler.gtNewLclvNode(TYP_INT, 0);
            left._vnPair.SetBoth(store.VNForExpr(null, TYP_INT));
            var right = compiler.gtNewLclvNode(TYP_INT, 1);
            right._vnPair.SetBoth(store.VNForExpr(null, TYP_INT));
            var relop = compiler.gtNewBinaryNode(GT_GE, TYP_INT, left, right);
            var relopVN = store.VNForExpr(null, TYP_INT);
            relop._vnPair.SetBoth(relopVN);
            var statement = compiler.gtNewStmt(relop);
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var assertions = BitOps.MakeEmpty(traits);
            var index = compiler.optAddAssertion(
                AssertionDsc.CreateInt32ConstantVNAssertion(compiler, relopVN, store.VNForIntCon(0), equalsZero));
            BitOps.AddElemD(traits, assertions, index - 1);

            var result = compiler.optAssertionProp_RelOp(assertions, relop, statement, new BasicBlock(null, null));
            Assert.That(result?.AsIntCon().IconValue, Is.EqualTo((nint)expected));
            Assert.That(statement.RootNode, Is.SameAs(result));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void GlobalRelopReplacesRootOrParentOperandWithoutRetagging(bool nested)
    {
        WithCompiler((compiler, store) =>
        {
            var leftVN = store.VNForExpr(null, TYP_INT);
            var rightVN = store.VNForExpr(null, TYP_INT);
            var left = compiler.gtNewLclvNode(TYP_INT, 0);
            left._vnPair.SetBoth(leftVN);
            var right = compiler.gtNewLclvNode(TYP_INT, 1);
            right._vnPair.SetBoth(rightVN);
            var comparison = compiler.gtNewBinaryNode(GT_LE, TYP_INT, left, right);
            comparison._vnPair.SetBoth(store.VNForExpr(null, TYP_INT));
            var parent = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, comparison,
                compiler.gtNewIconNode(TYP_INT, 2));
            var statement = compiler.gtNewStmt(nested ? parent : comparison);
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var assertions = BitOps.MakeEmpty(traits);
            var index = compiler.optAddAssertion(
                AssertionDsc.CreateRelopVN(compiler, VNF_LE, leftVN, rightVN));
            BitOps.AddElemD(traits, assertions, index - 1);

            var result = compiler.optAssertionProp_RelOp(assertions, comparison, statement,
                new BasicBlock(null, null));
            Assert.That(result?.AsIntCon().IconValue, Is.EqualTo((nint)1));
            Assert.That(nested ? parent.AsOp().Op1 : statement.RootNode, Is.SameAs(result));
            Assert.That(comparison.Oper, Is.EqualTo(GT_LE));
            Assert.That(left.Oper, Is.EqualTo(GT_LCL_VAR));
        });
    }

    [Test]
    public static void ExactRelopMatchKeepsOperandEffects()
    {
        WithCompiler((compiler, store) =>
        {
            var leftVN = store.VNForExpr(null, TYP_INT);
            var rightVN = store.VNForExpr(null, TYP_INT);
            var effect = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 42));
            var left = compiler.gtNewCommaNode(TYP_INT, effect, compiler.gtNewLclvNode(TYP_INT, 0));
            left._vnPair.SetBoth(leftVN);
            var right = compiler.gtNewLclvNode(TYP_INT, 1);
            right._vnPair.SetBoth(rightVN);
            var comparison = compiler.gtNewBinaryNode(GT_GE, TYP_INT, left, right);
            comparison._vnPair.SetBoth(store.VNForExpr(null, TYP_INT));
            var statement = compiler.gtNewStmt(comparison);
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var assertions = BitOps.MakeEmpty(traits);
            var index = compiler.optAddAssertion(
                AssertionDsc.CreateRelopVN(compiler, VNF_GE, leftVN, rightVN));
            BitOps.AddElemD(traits, assertions, index - 1);

            var result = compiler.optAssertionProp_RelOp(assertions, comparison, statement,
                new BasicBlock(null, null));
            Assert.That(result?.Oper, Is.EqualTo(GT_COMMA));
            Assert.That(result?.AsOp().Op1, Is.SameAs(effect));
            Assert.That(result?.AsOp().Op2.AsIntCon().IconValue, Is.EqualTo((nint)1));
            Assert.That(statement.RootNode, Is.SameAs(result));
        });
    }

    [TestCase(TYP_FLOAT, true, 1)]
    [TestCase(TYP_FLOAT, false, 0)]
    [TestCase(TYP_DOUBLE, true, 1)]
    [TestCase(TYP_DOUBLE, false, 0)]
    public static void FloatingEqualityPreservesSignedZeroRules(var_types type, bool equals, int expected)
    {
        WithCompiler((compiler, store) =>
        {
            compiler.lvaTable = [new LclVarDsc { Type = type }, new LclVarDsc { Type = type }];
            var value = -0.0;
            var constantVN = type is TYP_FLOAT
                ? store.VNForFloatCon((float)value) : store.VNForDoubleCon(value);
            var localVN = store.VNForExpr(null, type);
            var left = compiler.gtNewLclvNode(type, 0);
            left._vnPair.SetBoth(localVN);
            var right = new GenTreeDblCon(type, value);
            right._vnPair.SetBoth(constantVN);
            var relop = compiler.gtNewBinaryNode(GT_EQ, TYP_INT, left, right);
            relop._vnPair.SetBoth(store.VNForExpr(null, TYP_INT));
            var statement = compiler.gtNewStmt(relop);
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var assertions = BitOps.MakeEmpty(traits);
            var index = compiler.optAddAssertion(
                AssertionDsc.CreateConstLclVarAssertion(compiler, BAD_VAR_NUM, localVN,
                    value, constantVN, equals));
            BitOps.AddElemD(traits, assertions, index - 1);

            var result = compiler.optAssertionProp_RelOp(assertions, relop, statement,
                new BasicBlock(null, null));
            Assert.That(result?.AsIntCon().IconValue, Is.EqualTo((nint)expected));
            Assert.That(statement.RootNode, Is.SameAs(result));
            Assert.That(relop.Oper, Is.EqualTo(equals ? GT_EQ : GT_NE));
        });
    }

#if DEBUG
    [TestCase(TYP_FLOAT, true, "inf")]
    [TestCase(TYP_FLOAT, false, "-inf")]
    [TestCase(TYP_DOUBLE, true, "inf")]
    [TestCase(TYP_DOUBLE, false, "-inf")]
    public static void VerboseFloatingAssertionUsesNativeInfinitySpelling(
        var_types type, bool positive, string spelling)
    {
        WithCompiler((compiler, store) =>
        {
            compiler.lvaTable = [new LclVarDsc { Type = type }, new LclVarDsc { Type = type }];
            var value = positive ? double.PositiveInfinity : double.NegativeInfinity;
            var constantVN = type is TYP_FLOAT
                ? store.VNForFloatCon((float)value) : store.VNForDoubleCon(value);
            var localVN = store.VNForExpr(null, type);
            var left = compiler.gtNewLclvNode(type, 0);
            left._vnPair.SetBoth(localVN);
            var right = new GenTreeDblCon(type, value);
            right._vnPair.SetBoth(constantVN);
            var relop = compiler.gtNewBinaryNode(GT_EQ, TYP_INT, left, right);
            relop._vnPair.SetBoth(store.VNForExpr(null, TYP_INT));
            var statement = compiler.gtNewStmt(relop);
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var assertions = BitOps.MakeEmpty(traits);
            var index = compiler.optAddAssertion(
                AssertionDsc.CreateConstLclVarAssertion(compiler, BAD_VAR_NUM, localVN,
                    value, constantVN, true));
            BitOps.AddElemD(traits, assertions, index - 1);

            var originalStream = Globals.s_jitstdout;
            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, Encoding.UTF8, 1024, leaveOpen: true) { AutoFlush = true };
            object config = JitConfig;
            var breakMorphTree = typeof(JitConfigValues).GetField("_jitBreakMorphTree",
                BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException();
            breakMorphTree.SetValue(config, -1);
            JitConfig = (JitConfigValues)config;
            try
            {
                Globals.s_jitstdout = writer;
                compiler.compCurBB = new BasicBlock(null, null);
                compiler.verbose = true;
                _ = compiler.optAssertionProp_RelOp(assertions, relop, statement,
                    compiler.compCurBB);
            }
            finally
            {
                Globals.s_jitstdout = originalStream;
                compiler.verbose = false;
            }

            Assert.That(Encoding.UTF8.GetString(stream.ToArray()),
                Does.Contain($" == {spelling}\n"));
        });
    }
#endif

    [TestCase(TYP_FLOAT, 0x7FC01234L)]
    [TestCase(TYP_FLOAT, 0xFFC05678L)]
    [TestCase(TYP_DOUBLE, 0x7FF8000000001234L)]
    [TestCase(TYP_DOUBLE, unchecked((long)0xFFF8000000005678UL))]
    public static void NaNComparisonCannotMatchAnOrdinaryFloatingAssertion(var_types type, long bits)
    {
        WithCompiler((compiler, store) =>
        {
            compiler.lvaTable = [new LclVarDsc { Type = type }, new LclVarDsc { Type = type }];
            var localVN = store.VNForExpr(null, type);
            var left = compiler.gtNewLclvNode(type, 0);
            left._vnPair.SetBoth(localVN);
            var value = type is TYP_FLOAT
                ? BitConverter.Int32BitsToSingle(unchecked((int)bits))
                : BitConverter.Int64BitsToDouble(bits);
            var nanVN = type is TYP_FLOAT
                ? store.VNForFloatCon((float)value) : store.VNForDoubleCon(value);
            var right = new GenTreeDblCon(type, value);
            var originalBits = BitConverter.DoubleToInt64Bits(right.DconVal);
            right._vnPair.SetBoth(nanVN);
            var relop = compiler.gtNewBinaryNode(GT_EQ, TYP_INT, left, right);
            relop._vnPair.SetBoth(store.VNForExpr(null, TYP_INT));
            var statement = compiler.gtNewStmt(relop);
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var assertions = BitOps.MakeEmpty(traits);
            var zeroVN = type is TYP_FLOAT ? store.VNForFloatCon(0f) : store.VNForDoubleCon(0d);
            var index = compiler.optAddAssertion(
                AssertionDsc.CreateConstLclVarAssertion(compiler, BAD_VAR_NUM, localVN,
                    0d, zeroVN, true));
            BitOps.AddElemD(traits, assertions, index - 1);

            Assert.That(compiler.optAssertionProp_RelOp(assertions, relop, statement,
                new BasicBlock(null, null)), Is.Null);
            Assert.That(statement.RootNode, Is.SameAs(relop));
            Assert.That(BitConverter.DoubleToInt64Bits(right.DconVal), Is.EqualTo(originalBits));
        });
    }

    [TestCase(true, 1)]
    [TestCase(false, -1)]
    public static void PhiNullComparisonRequiresEveryIncomingEdgeToProveNonNull(
        bool incomingFact, int expected)
    {
        WithCompiler((compiler, store) =>
        {
            compiler.lvaTable = [new LclVarDsc { Type = TYP_REF }, new LclVarDsc { Type = TYP_INT }];
            ref var definitions = ref compiler.lvaTable[0].lvPerSsaData;
            var argumentSsa = definitions.AllocSsaNum();
            var phiSsa = definitions.AllocSsaNum();
            var argumentVN = store.VNForExpr(null, TYP_REF);
            definitions.GetSsaDef(argumentSsa)._vnPair.SetBoth(argumentVN);
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var knownNonNull = BitOps.MakeEmpty(traits);
            var index = compiler.optAddAssertion(
                AssertionDsc.CreateVNNonNullAssertion(compiler, argumentVN));
            BitOps.AddElemD(traits, knownNonNull, index - 1);
#if DEBUG
            compiler.fgSafeBasicBlockCreation = true;
#endif
            var entry = BasicBlock.New(compiler, BBKinds.BBJ_COND);
            var join = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            var other = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            compiler.fgFirstBB = entry;
            entry.SetCond(new FlowEdge(entry, join, null), new FlowEdge(entry, other, null));
            join.bbPreds = entry.TrueEdge;
            var outgoing = new nint[compiler.fgBBNumMax + 1][];
            outgoing[entry.bbNum] = incomingFact ? knownNonNull : BitOps.MakeEmpty(traits);
            JtrueAssertionOut(compiler) = outgoing;
            var phiArgument = new GenTreePhiArg(TYP_REF, 0, argumentSsa, entry);
            var phi = new GenTreePhi(TYP_REF) { FirstUse = new GenTreePhi.Use(phiArgument) };
            var definition = compiler.gtNewStoreLclVarNode(0, phi);
            definition.SsaNum = phiSsa;
            definitions.GetSsaDef(phiSsa) = new LclSsaVarDsc(join, definition);
            var phiVN = store.VNForPhiDef(TYP_REF, 0, phiSsa, [argumentSsa]);
            var left = compiler.gtNewLclvNode(TYP_REF, 0);
            left._vnPair.SetBoth(phiVN);
            var right = compiler.gtNewIconNode(TYP_REF, 0);
            right._vnPair.SetBoth(ValueNumStore.VNForNull());
            var comparison = compiler.gtNewBinaryNode(GT_NE, TYP_INT, left, right);
            comparison._vnPair.SetBoth(store.VNForExpr(null, TYP_INT));
            var statement = compiler.gtNewStmt(comparison);

            var result = compiler.optAssertionProp_RelOp(BitOps.MakeEmpty(traits),
                comparison, statement, join);
            if (expected < 0)
            {
                Assert.That(result, Is.Null);
                Assert.That(statement.RootNode, Is.SameAs(comparison));
            }
            else
            {
                Assert.That(result?.AsIntCon().IconValue, Is.EqualTo((nint)expected));
                Assert.That(statement.RootNode, Is.SameAs(result));
            }
        });
    }

    [TestCase(GT_EQ, 0)]
    [TestCase(GT_NE, 1)]
    public static void NonZeroAssertionFoldsZeroComparison(genTreeOps oper, int expected)
    {
        WithCompiler((compiler, store) =>
        {
            var leftVN = store.VNForExpr(null, TYP_INT);
            var left = compiler.gtNewLclvNode(TYP_INT, 0);
            left._vnPair.SetBoth(leftVN);
            var zero = compiler.gtNewIconNode(TYP_INT, 0);
            zero._vnPair.SetBoth(store.VNForIntCon(0));
            var comparison = compiler.gtNewBinaryNode(oper, TYP_INT, left, zero);
            comparison._vnPair.SetBoth(store.VNForExpr(null, TYP_INT));
            var statement = compiler.gtNewStmt(comparison);
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var assertions = BitOps.MakeEmpty(traits);
            var index = compiler.optAddAssertion(
                AssertionDsc.CreateInt32ConstantVNAssertion(compiler, leftVN, store.VNForIntCon(0), false));
            BitOps.AddElemD(traits, assertions, index - 1);

            var result = compiler.optAssertionProp_RelOp(assertions, comparison, statement, new BasicBlock(null, null));
            Assert.That(result?.AsIntCon().IconValue, Is.EqualTo((nint)expected));
            Assert.That(statement.RootNode, Is.SameAs(result));
        });
    }

    [TestCase(GT_GT, VNF_GT, 3, 1)]
    [TestCase(GT_LT, VNF_LT, 3, 0)]
    public static void AssertionRangeFoldsRelatedComparison(genTreeOps oper, VNFunc func,
        int bound, int expected)
    {
        WithCompiler((compiler, store) =>
        {
            var localVN = store.VNForExpr(null, TYP_INT);
            var local = compiler.gtNewLclvNode(TYP_INT, 0);
            local._vnPair.SetBoth(localVN);
            var boundVN = store.VNForIntCon(bound);
            var constant = compiler.gtNewIconNode(TYP_INT, bound);
            constant._vnPair.SetBoth(boundVN);
            var comparison = compiler.gtNewBinaryNode(oper, TYP_INT, local, constant);
            comparison._vnPair.SetBoth(store.VNForFunc(TYP_INT, func, localVN, boundVN));
            var statement = compiler.gtNewStmt(comparison);
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var assertions = BitOps.MakeEmpty(traits);
            var index = compiler.optAddAssertion(AssertionDsc.CreateConstantBound(
                compiler, VNF_GE, localVN, store.VNForIntCon(5)));
            BitOps.AddElemD(traits, assertions, index - 1);

            var result = compiler.optAssertionProp_RelOp(assertions, comparison, statement,
                new BasicBlock(null, null));
            Assert.That(result?.AsIntCon().IconValue, Is.EqualTo((nint)expected));
            Assert.That(statement.RootNode, Is.SameAs(result));
        });
    }

    [TestCase(GT_EQ, 1)]
    [TestCase(GT_NE, 0)]
    [TestCase(GT_GE, -1)]
    public static void LocalRelopDispatcherRetainsExistingEqualityBehavior(genTreeOps oper, int expected)
    {
        WithCompiler((compiler, _) =>
        {
            var local = compiler.gtNewLclvNode(TYP_INT, 0);
            var constant = compiler.gtNewIconNode(TYP_INT, 17);
            var comparison = compiler.gtNewBinaryNode(oper, TYP_INT, local, constant);
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var assertions = BitOps.MakeEmpty(traits);
            var index = compiler.optAddAssertion(
                AssertionDsc.CreateConstLclVarAssertion(compiler, 0, ValueNumStore.NoVN,
                    17, ValueNumStore.NoVN, true));
            BitOps.AddElemD(traits, assertions, index - 1);

            var result = compiler.optAssertionProp_RelOp(assertions, comparison, null, null);
            Assert.That(result?.AsIntCon().IconValue, expected < 0 ? Is.Null : Is.EqualTo((nint)expected));
        }, local: true);
    }

    private static void WithCompiler(Action<Compiler, ValueNumStore> action, bool local = false)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previousCompiler = JitTls.Compiler;
        var previousConfig = JitConfig;
        object config = default(JitConfigValues);
        var maxLocals = typeof(JitConfigValues).GetField("_jitMaxLocalsToTrack",
            BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException();
        maxLocals.SetValue(config, 1024);
        JitConfig = (JitConfigValues)config;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.lvaCount = 2;
        compiler.lvaTable = [new LclVarDsc { Type = TYP_INT }, new LclVarDsc { Type = TYP_INT }];
#if DEBUG
        compiler.info.compFullName = nameof(AssertionRelationalTests);
#endif
        JitTls.Compiler = compiler;
        try
        {
            compiler.optAssertionInit(isLocalProp: local);
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            action(compiler, store);
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
            JitConfig = previousConfig;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "bbJtrueAssertionOut")]
    private static extern ref nint[][]? JtrueAssertionOut(Compiler compiler);
}
