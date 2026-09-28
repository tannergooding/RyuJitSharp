// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.SimdScalableKind;
using static RyuJitSharp.var_types;
using AssertionDsc = RyuJitSharp.Compiler.AssertionDsc;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class Arm64ScalableAssertionTests
{
    [TestCase(SimdScalableRepeated)]
    [TestCase(SimdScalableSequence)]
    [TestCase(SimdScalableScalar)]
    public static void AssertionOwnsTheCompleteScalablePayload(SimdScalableKind kind)
    {
        WithCompiler(compiler => {
            var node = Constant(TYP_DOUBLE, kind, 0x8000_0000_0000_0000, 0x7FF8_0000_0000_1234);
            var expected = node.SimdScalableVal;
            var assertion = Assertion(compiler, node);
            node.SimdScalableVal = simdscalable_t.Zero;

            Assert.That(assertion.Op2.SimdSize, Is.EqualTo(Unsafe.SizeOf<simdscalable_t>()));
            Assert.That(MemoryMarshal.Read<simdscalable_t>(assertion.Op2.SimdConstant), Is.EqualTo(expected));
            Assert.That(assertion.Reverse().Op2.SimdConstant.ToArray(), Is.EqualTo(assertion.Op2.SimdConstant.ToArray()));
        });
    }

    [TestCase(TYP_BYTE, SimdScalableRepeated, 0UL, 0UL, true)]
    [TestCase(TYP_DOUBLE, SimdScalableRepeated, 0UL, 0UL, false)]
    [TestCase(TYP_BYTE, SimdScalableScalar, 0UL, 0UL, false)]
    [TestCase(TYP_BYTE, SimdScalableRepeated, 0UL, 1UL, false)]
    [TestCase(TYP_BYTE, SimdScalableRepeated, 1UL, 0UL, false)]
    public static void AssertionEqualityDoesNotCanonicalizeZeroEncodings(
        var_types type, SimdScalableKind kind, ulong index, ulong step, bool equal)
    {
        WithCompiler(compiler => {
            var first = Assertion(compiler, Constant(TYP_BYTE, SimdScalableRepeated, 0, 0));
            var second = Assertion(compiler, Constant(type, kind, index, step));

            Assert.That(first.Equals(second, false), Is.EqualTo(equal));
        });
    }

    [TestCase(SimdScalableRepeated, true)]
    [TestCase(SimdScalableSequence, true)]
    [TestCase(SimdScalableScalar, true)]
    [TestCase(SimdScalableRepeated, false)]
    [TestCase(SimdScalableSequence, false)]
    [TestCase(SimdScalableScalar, false)]
    public static void PropagationCreatesAnIndependentConstantAndPreservesGlobalVNs(SimdScalableKind kind, bool local)
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var constant = Constant(TYP_LONG, kind, ulong.MaxValue, 2);
            compiler.fgValueNumberTreeConst(constant);
            var constantVN = constant._vnPair.Liberal;
            var operandVN = store.VNForExpr(null, TYP_SIMD);
            var assertion = AssertionDsc.CreateConstLclVarAssertion(
                compiler, 0, operandVN, constant, constantVN, true);
            var use = compiler.gtNewLclvNode(TYP_SIMD, 0);
            use._vnPair.SetBoth(operandVN);
            var statement = local ? null : compiler.gtNewStmt(use);
            var result = compiler.optConstantAssertionProp(assertion, use, statement)
                ?? throw new InvalidOperationException("Expected a propagated vector constant.");

            Assert.That(result, Is.Not.SameAs(use));
            Assert.That(result.Type, Is.EqualTo(TYP_SIMD));
            Assert.That(result.AsVecCon().SimdScalableVal, Is.EqualTo(constant.SimdScalableVal));
            if (!local)
            {
                Assert.That(statement?.RootNode, Is.SameAs(result));
                Assert.That(result._vnPair.Liberal, Is.EqualTo(constantVN));
                Assert.That(result._vnPair.BothEqual(), Is.True);
            }

            result.AsVecCon().SimdScalableVal.Index.u64[0] = 0;
            Assert.That(MemoryMarshal.Read<simdscalable_t>(assertion.Op2.SimdConstant).Index.u64[0],
                Is.EqualTo(ulong.MaxValue));
        }, local);
    }

    [TestCase(TYP_INT)]
    [TestCase(TYP_SIMD16)]
    public static void MismatchedLocalTypesStillRejectVectorPropagation(var_types type)
    {
        WithCompiler(compiler => {
            var fixedVector = new GenTreeVecCon(TYP_SIMD16);
            var assertion = Assertion(compiler, fixedVector);
            var use = compiler.gtNewLclvNode(type, 0);

            Assert.That(compiler.optConstantAssertionProp(assertion, use, null), Is.Null);
        });
    }

    private static AssertionDsc Assertion(Compiler compiler, GenTreeVecCon value) =>
        AssertionDsc.CreateConstLclVarAssertion(
            compiler, 0, ValueNumStore.NoVN, value, ValueNumStore.NoVN, true);

    private static GenTreeVecCon Constant(var_types type, SimdScalableKind kind, ulong index, ulong step)
    {
        var node = new GenTreeVecCon(TYP_SIMD);
        node.SimdScalableVal.BaseType = type;
        node.SimdScalableVal.Kind = kind;
        node.SimdScalableVal.Index.u64[0] = index;
        node.SimdScalableVal.Step.u64[0] = step;

        return node;
    }

    private static void WithCompiler(Action<Compiler> action, bool local = true)
    {
        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            compiler.lvaTable = [new LclVarDsc { Type = TYP_SIMD }];
            compiler.lvaCount = 1;
            compiler.optAssertionInit(local);
            action(compiler);
        });
    }
}
#endif
