// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.VNFunc;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;
using static RyuJitSharp.SymbolicIntegerValue;
using AssertionDsc = RyuJitSharp.Compiler.AssertionDsc;
using BitOps = RyuJitSharp.BitSetOps<RyuJitSharp.BitVecTraits, RyuJitSharp.BitVecTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class AssertionArithmeticTests
{
    [TestCase(GT_ADD, false, true)]
    [TestCase(GT_SUB, false, true)]
    [TestCase(GT_MUL, false, true)]
    [TestCase(GT_SUB, true, false)]
    public static void CheckedArithmeticClearsOverflowOnlyForProvedRange(
        genTreeOps oper, bool unsigned, bool expected)
    {
        WithCompiler((compiler, store) =>
        {
            var lhsVN = store.VNForExpr(null, TYP_INT);
            var rhsVN = store.VNForExpr(null, TYP_INT);
            var lhs = compiler.gtNewLclvNode(TYP_INT, 0);
            lhs._vnPair.SetBoth(lhsVN);
            var rhs = compiler.gtNewLclvNode(TYP_INT, 1);
            rhs._vnPair.SetBoth(rhsVN);
            var node = compiler.gtNewBinaryNode(oper, TYP_INT, lhs, rhs);
            node.Flags |= GTF_OVERFLOW;
            node.AsOp().IsUnsigned = unsigned;
            var statement = compiler.gtNewStmt(node);
            var assertions = NewAssertions(compiler,
                AssertionDsc.CreateConstantBound(compiler, VNF_GE, lhsVN, store.VNForIntCon(1)),
                AssertionDsc.CreateConstantBound(compiler, VNF_LE, lhsVN, store.VNForIntCon(2)),
                AssertionDsc.CreateConstantBound(compiler, VNF_GE, rhsVN, store.VNForIntCon(3)),
                AssertionDsc.CreateConstantBound(compiler, VNF_LE, rhsVN, store.VNForIntCon(4)));

            var result = compiler.optAssertionProp_AddMulSub(assertions, node.AsOp(), statement,
                new BasicBlock(null, null));
            Assert.That(result is not null, Is.EqualTo(expected));
            Assert.That(node.HasOverflowCheck, Is.EqualTo(!expected));
            Assert.That(statement.RootNode, Is.SameAs(node));
        });
    }

    [TestCase(GT_DIV, true, true, GT_UDIV, true, true)]
    [TestCase(GT_MOD, true, true, GT_UMOD, true, true)]
    [TestCase(GT_DIV, false, true, GT_DIV, true, false)]
    [TestCase(GT_DIV, false, false, GT_DIV, false, false)]
    public static void DivisionPreservesVNAndSetsOnlyProvedExceptionFlags(
        genTreeOps oper, bool nonnegative, bool nonzero, genTreeOps expectedOper,
        bool noZero, bool noOverflow)
    {
        WithCompiler((compiler, store) =>
        {
            var lhsVN = store.VNForExpr(null, TYP_INT);
            var rhsVN = store.VNForExpr(null, TYP_INT);
            var lhs = compiler.gtNewLclvNode(TYP_INT, 0);
            lhs._vnPair.SetBoth(lhsVN);
            var rhs = compiler.gtNewLclvNode(TYP_INT, 1);
            rhs._vnPair.SetBoth(rhsVN);
            var node = compiler.gtNewBinaryNode(oper, TYP_INT, lhs, rhs);
            var operationVN = store.VNForExpr(null, TYP_INT);
            node._vnPair.SetBoth(operationVN);
            var statement = compiler.gtNewStmt(node);
            var assertions = NewAssertions(compiler,
                nonnegative
                    ? AssertionDsc.CreateConstantBound(compiler, VNF_GE, lhsVN, store.VNForIntCon(0))
                    : AssertionDsc.CreateConstantBound(compiler, VNF_LE, lhsVN, store.VNForIntCon(-1)),
                nonnegative
                    ? AssertionDsc.CreateConstantBound(compiler, VNF_GE, rhsVN, store.VNForIntCon(1))
                    : AssertionDsc.CreateConstantBound(compiler, VNF_LE, lhsVN, store.VNForIntCon(-1)),
                nonzero
                    ? AssertionDsc.CreateConstantBound(compiler, VNF_NE, rhsVN, store.VNForIntCon(0))
                    : AssertionDsc.CreateConstantBound(compiler, VNF_LE, lhsVN, store.VNForIntCon(-1)));

            var result = compiler.optAssertionProp_ModDiv(assertions, node.AsOp(), statement,
                new BasicBlock(null, null));
            Assert.That(result is not null, Is.EqualTo(nonnegative || nonzero));
            Assert.That(node.Oper, Is.EqualTo(expectedOper));
            Assert.That((node.Flags & GTF_DIV_MOD_NO_BY_ZERO) != 0, Is.EqualTo(noZero));
            Assert.That((node.Flags & GTF_DIV_MOD_NO_OVERFLOW) != 0, Is.EqualTo(noOverflow));
            Assert.That(node._vnPair.Conservative, Is.EqualTo(operationVN));
            Assert.That(node.HasOrderingSideEffect, Is.EqualTo(noZero || noOverflow));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void GlobalCastRemovesValueNoOpOrClearsCheckedNarrowing(bool narrowing)
    {
        WithCompiler((compiler, store) =>
        {
            var sourceVN = store.VNForExpr(null, TYP_INT);
            var operand = compiler.gtNewLclvNode(TYP_INT, 0);
            operand._vnPair.SetBoth(sourceVN);
            var cast = new GenTreeCast(TYP_INT, operand, false, narrowing ? TYP_BYTE : TYP_INT);
            if (narrowing)
            {
                cast.Flags |= GTF_OVERFLOW;
            }
            var statement = compiler.gtNewStmt(cast);
            var assertions = NewAssertions(compiler,
                AssertionDsc.CreateConstantBound(compiler, VNF_GE, sourceVN, store.VNForIntCon(1)),
                AssertionDsc.CreateConstantBound(compiler, VNF_LE, sourceVN, store.VNForIntCon(10)));

            var result = compiler.optAssertionProp_Cast(assertions, cast, statement,
                new BasicBlock(null, null));
            Assert.That(result, Is.SameAs(operand));
            Assert.That(statement.RootNode, Is.SameAs(result));
            Assert.That(cast.HasOverflowCheck, Is.EqualTo(narrowing));
        });
    }

    [Test]
    public static void CheckedSmallCastOfExpressionRetainsCodegenHintAndClearsOverflow()
    {
        WithCompiler((compiler, store) =>
        {
            var sourceVN = store.VNForExpr(null, TYP_INT);
            var source = compiler.gtNewLclvNode(TYP_INT, 0);
            source._vnPair.SetBoth(sourceVN);
            var operand = compiler.gtNewBinaryNode(GT_AND, TYP_INT, source,
                compiler.gtNewIconNode(TYP_INT, 15));
            operand._vnPair.SetBoth(store.VNForFunc(TYP_INT, VNF_AND,
                sourceVN, store.VNForIntCon(15)));
            var cast = new GenTreeCast(TYP_INT, operand, false, TYP_BYTE);
            cast.Flags |= GTF_OVERFLOW;
            var statement = compiler.gtNewStmt(cast);
            var assertions = NewAssertions(compiler,
                AssertionDsc.CreateConstantBound(compiler, VNF_GE, sourceVN, store.VNForIntCon(0)));

            Assert.That(compiler.optAssertionProp_Cast(assertions, cast, statement,
                new BasicBlock(null, null)), Is.SameAs(cast));
            Assert.That(cast.HasOverflowCheck, Is.False);
            Assert.That(statement.RootNode, Is.SameAs(cast));
        });
    }

    [Test]
    public static void WideningCastLearnsUnsignedInputWithoutRemovingRepresentationChange()
    {
        WithCompiler((compiler, store) =>
        {
            var sourceVN = store.VNForExpr(null, TYP_INT);
            var operand = compiler.gtNewLclvNode(TYP_INT, 0);
            operand._vnPair.SetBoth(sourceVN);
            var cast = new GenTreeCast(TYP_LONG, operand, false, TYP_LONG);
            var statement = compiler.gtNewStmt(cast);
            var assertions = NewAssertions(compiler,
                AssertionDsc.CreateConstantBound(compiler, VNF_GE, sourceVN, store.VNForIntCon(0)));

            Assert.That(compiler.optAssertionProp_Cast(assertions, cast, statement,
                new BasicBlock(null, null)), Is.Null);
            Assert.That(cast.IsUnsigned, Is.True);
            Assert.That(statement.RootNode, Is.SameAs(cast));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void PreviousBoundsAssertionDropsCheckWithoutLosingIndexEffects(bool sideEffect)
    {
        WithCompiler((compiler, store) =>
        {
            var indexVN = store.VNForExpr(null, TYP_INT);
            var lengthVN = store.VNForExpr(null, TYP_INT);
            var index = compiler.gtNewLclvNode(TYP_INT, 0);
            index._vnPair.SetBoth(indexVN);
            GenTree actualIndex = index;
            if (sideEffect)
            {
                var effect = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 7));
                actualIndex = compiler.gtNewCommaNode(TYP_INT, effect, index);
                actualIndex._vnPair.SetBoth(indexVN);
                actualIndex.Flags |= GTF_ASG;
            }
            var length = compiler.gtNewLclvNode(TYP_INT, 1);
            length._vnPair.SetBoth(lengthVN);
            var check = new GenTreeBoundsChk(actualIndex, length, SpecialCodeKind.SCK_RNGCHK_FAIL);
            var statement = compiler.gtNewStmt(check);
            var assertions = NewAssertions(compiler,
                AssertionDsc.CreateCompareCheckedBound(compiler, VNF_LT_UN, indexVN, lengthVN,
                    0, isVNNeverNegative: true));

            var result = compiler.optAssertionProp_BndsChk(assertions, check, statement,
                new BasicBlock(null, null));
            Assert.That(result?.Oper, Is.EqualTo(sideEffect ? GT_STORE_LCL_VAR : GT_NOP));
            Assert.That(statement.RootNode, Is.SameAs(result));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public static void RangeProofDropsBoundedCheckButKeepsUnprovedIndex(bool bounded)
    {
        WithCompiler((compiler, store) =>
        {
            var indexVN = store.VNForExpr(null, TYP_INT);
            var index = compiler.gtNewLclvNode(TYP_INT, 0);
            index._vnPair.SetBoth(indexVN);
            var length = compiler.gtNewIconNode(TYP_INT, 8);
            length._vnPair.SetBoth(store.VNForIntCon(8));
            var check = new GenTreeBoundsChk(index, length, SpecialCodeKind.SCK_RNGCHK_FAIL);
            var statement = compiler.gtNewStmt(check);
            var assertions = NewAssertions(compiler,
                AssertionDsc.CreateConstantBound(compiler, VNF_GE, indexVN,
                    store.VNForIntCon(bounded ? 0 : -10)),
                AssertionDsc.CreateConstantBound(compiler, VNF_LE, indexVN, store.VNForIntCon(7)));

            var result = compiler.optAssertionProp_BndsChk(assertions, check, statement,
                new BasicBlock(null, null));
            Assert.That(result?.Oper, bounded ? Is.EqualTo(GT_NOP) : Is.Null);
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public static void ModuloIndexRequiresTheSameLengthVN(bool matchingLength)
    {
        WithCompiler((compiler, store) =>
        {
            var lengthVN = store.VNForExpr(null, TYP_INT);
            var divisorVN = matchingLength ? lengthVN : store.VNForExpr(null, TYP_INT);
            var indexVN = store.VNForFunc(TYP_INT, VNF_UMOD,
                store.VNForExpr(null, TYP_INT), divisorVN);
            var index = compiler.gtNewLclvNode(TYP_INT, 0);
            index._vnPair.SetBoth(indexVN);
            var length = compiler.gtNewLclvNode(TYP_INT, 1);
            length._vnPair.SetBoth(lengthVN);
            var check = new GenTreeBoundsChk(index, length, SpecialCodeKind.SCK_RNGCHK_FAIL);
            var statement = compiler.gtNewStmt(check);

            var result = compiler.optAssertionProp_BndsChk(NewAssertions(compiler), check,
                statement, new BasicBlock(null, null));
            Assert.That(result?.Oper, matchingLength ? Is.EqualTo(GT_NOP) : Is.Null);
        });
    }

    [Test]
    public static void PreviousCheckedIndexLowerBoundCoversCurrentUpperBound()
    {
        WithCompiler((compiler, store) =>
        {
            var sourceVN = store.VNForExpr(null, TYP_INT);
            var previousVN = store.VNForFunc(TYP_INT, VNF_ADD,
                store.VNForFunc(TYP_INT, VNF_AND, sourceVN, store.VNForIntCon(31)),
                store.VNForIntCon(5));
            var indexVN = store.VNForExpr(null, TYP_INT);
            var lengthVN = store.VNForExpr(null, TYP_INT);
            var index = compiler.gtNewLclvNode(TYP_INT, 0);
            index._vnPair.SetBoth(indexVN);
            var length = compiler.gtNewLclvNode(TYP_INT, 1);
            length._vnPair.SetBoth(lengthVN);
            var check = new GenTreeBoundsChk(index, length, SpecialCodeKind.SCK_RNGCHK_FAIL);
            var statement = compiler.gtNewStmt(check);
            var assertions = NewAssertions(compiler,
                AssertionDsc.CreateCompareCheckedBound(compiler, VNF_LT_UN,
                    previousVN, lengthVN, 0, isVNNeverNegative: true),
                AssertionDsc.CreateConstantBound(compiler, VNF_GE, indexVN, store.VNForIntCon(0)),
                AssertionDsc.CreateConstantBound(compiler, VNF_LE, indexVN, store.VNForIntCon(4)));

            Assert.That(compiler.optAssertionProp_BndsChk(assertions, check, statement,
                new BasicBlock(null, null))?.Oper, Is.EqualTo(GT_NOP));
        });
    }

    [Test]
    public static void ArrayLengthMinusConstantNeedsSufficientLengthLowerBound()
    {
        WithCompiler((compiler, store) =>
        {
            var lengthVN = store.VNForExpr(null, TYP_INT);
            var indexVN = store.VNForFunc(TYP_INT, VNF_ADD, lengthVN, store.VNForIntCon(-2));
            var index = compiler.gtNewLclvNode(TYP_INT, 0);
            index._vnPair.SetBoth(indexVN);
            var length = compiler.gtNewLclvNode(TYP_INT, 1);
            length._vnPair.SetBoth(lengthVN);
            var check = new GenTreeBoundsChk(index, length, SpecialCodeKind.SCK_RNGCHK_FAIL);
            var statement = compiler.gtNewStmt(check);
            var assertions = NewAssertions(compiler,
                AssertionDsc.CreateConstantBound(compiler, VNF_GE, lengthVN, store.VNForIntCon(2)));

            Assert.That(compiler.optAssertionProp_BndsChk(assertions, check, statement,
                new BasicBlock(null, null))?.Oper, Is.EqualTo(GT_NOP));
        });
    }

    [Test]
    public static void ZeroIndexAndNonzeroLengthSatisfyImplicitLengthContract()
    {
        WithCompiler((compiler, store) =>
        {
            var index = compiler.gtNewIconNode(TYP_INT, 0);
            index._vnPair.SetBoth(store.VNForIntCon(0));
            var lengthVN = store.VNForExpr(null, TYP_INT);
            var length = compiler.gtNewLclvNode(TYP_INT, 1);
            length._vnPair.SetBoth(lengthVN);
            var check = new GenTreeBoundsChk(index, length, SpecialCodeKind.SCK_RNGCHK_FAIL);
            var statement = compiler.gtNewStmt(check);
            var assertions = NewAssertions(compiler,
                AssertionDsc.CreateInt32ConstantVNAssertion(compiler,
                    lengthVN, store.VNForIntCon(0), equals: false));

            Assert.That(compiler.optAssertionProp_BndsChk(assertions, check, statement,
                new BasicBlock(null, null))?.Oper, Is.EqualTo(GT_NOP));
        });
    }

    [Test]
    public static void LocalCastReusesExistingSubrangeAndRetypesNormalizeOnLoadLocal()
    {
        WithCompiler((compiler, _) =>
        {
            compiler.lvaTable[0].Type = TYP_UBYTE;
            compiler.lvaTable[0].lvIsParam = true;
            var operand = compiler.gtNewLclvNode(TYP_INT, 0);
            var cast = new GenTreeCast(TYP_INT, operand, false, TYP_UBYTE);
            var assertions = NewAssertions(compiler,
                AssertionDsc.CreateSubrange(compiler, 0, new(Zero, UByteMax)));

            Assert.That(compiler.optAssertionProp_Cast(assertions, cast, null, null), Is.SameAs(operand));
            Assert.That(operand.Type, Is.EqualTo(TYP_UBYTE));
        }, local: true);
    }

    private static nint[] NewAssertions(Compiler compiler, params AssertionDsc[] assertions)
    {
        var indices = new ushort[assertions.Length];
        for (var i = 0; i < assertions.Length; i++)
        {
            indices[i] = compiler.optAddAssertion(assertions[i]);
        }

        var traits = compiler.apTraits ?? throw new InvalidOperationException();
        var active = BitOps.MakeEmpty(traits);
        foreach (var index in indices)
        {
            BitOps.AddElemD(traits, active, index - 1);
        }

        return active;
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
        compiler.compCurBB = new BasicBlock(null, null) { bbNum = 1 };
#if DEBUG
        compiler.info.compFullName = nameof(AssertionArithmeticTests);
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
}
