// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if FEATURE_HW_INTRINSICS && (TARGET_ARM64 || TARGET_WASM)
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;
#if DEBUG
using System.Runtime.InteropServices;
#endif

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class SimdAlignedMemoryCompletionTests
{
#if DEBUG
    private static int s_assertions;
#endif

    [TestCase(false)]
    [TestCase(true)]
    public static void OptimizedAlignedMemoryDelegatesToOrdinaryLoadAndStore(bool nonTemporal)
    {
        WithCompiler(false, compiler =>
        {
            var address = Local(compiler, TYP_BYREF);
            var value = compiler.gtNewVconNode(TYP_SIMD16);
            const ulong bits = 0x8000_0000_7FC0_1234;
            value.SimdVal.u64[0] = bits;
            var load = nonTemporal
                ? compiler.gtNewSimdLoadNonTemporalNode(TYP_SIMD16, address, TYP_FLOAT, 16)
                : compiler.gtNewSimdLoadAlignedNode(TYP_SIMD16, address, TYP_FLOAT, 16);
            var store = nonTemporal
                ? compiler.gtNewSimdStoreNonTemporalNode(address, value, TYP_FLOAT, 16)
                : compiler.gtNewSimdStoreAlignedNode(address, value, TYP_FLOAT, 16);
            Assert.That(load.Oper, Is.EqualTo(GT_IND));
            Assert.That(load.Type, Is.EqualTo(TYP_SIMD16));
            Assert.That(load.AsOp().Op1, Is.SameAs(address));
            Assert.That(store.Oper, Is.EqualTo(GT_STOREIND));
            Assert.That(store.AsOp().Op1, Is.SameAs(address));
            Assert.That(store.AsOp().Op2, Is.SameAs(value));
            Assert.That(value.SimdVal.u64[0], Is.EqualTo(bits));
        });
    }

    [Test]
    public static void StoreDelegationPreservesReverseOperandEvaluation(
        [Values(false, true)] bool nonTemporal,
        [Values(false, true)] bool reverseOps)
    {
        WithCompiler(false, compiler =>
        {
            var value = Local(compiler, TYP_SIMD16);
            var overwrite = compiler.gtNewStoreLclVarNode(value.LclNum, compiler.gtNewVconNode(TYP_SIMD16));
            var address = compiler.gtNewBinaryNode(GT_COMMA, TYP_BYREF, overwrite, Local(compiler, TYP_BYREF));
            var store = nonTemporal
                ? compiler.gtNewSimdStoreNonTemporalNode(address, value, TYP_INT, 16, reverseOps)
                : compiler.gtNewSimdStoreAlignedNode(address, value, TYP_INT, 16, reverseOps);
            Assert.That(store.Oper, Is.EqualTo(GT_STOREIND));
            if (!reverseOps)
            {
                Assert.That(store.AsOp().Op1, Is.SameAs(address));
                Assert.That(store.AsOp().Op2, Is.SameAs(value));
            }
            else
            {
                var sequencedAddress = store.AsOp().Op1.AsOp();
                Assert.That(sequencedAddress.Oper, Is.EqualTo(GT_COMMA));
                var capture = sequencedAddress.Op1;
                Assert.That(capture.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
                Assert.That(capture.AsLclVarCommon().Data, Is.SameAs(value));
                Assert.That(sequencedAddress.Op2, Is.SameAs(address));
                Assert.That(store.AsOp().Op2.Oper, Is.EqualTo(GT_LCL_VAR));
                Assert.That(store.AsOp().Op2.AsLclVarCommon().LclNum,
                    Is.EqualTo(capture.AsLclVarCommon().LclNum));
            }
        });
    }

#if DEBUG
    [TestCase(false)]
    [TestCase(true)]
    public static void MinOptsFallbackRetainsOptimizationAssertion(bool nonTemporal)
    {
        WithCompiler(true, compiler =>
        {
            var address = Local(compiler, TYP_BYREF);
            var value = compiler.gtNewVconNode(TYP_SIMD16);
            _ = nonTemporal
                ? compiler.gtNewSimdLoadNonTemporalNode(TYP_SIMD16, address, TYP_FLOAT, 16)
                : compiler.gtNewSimdLoadAlignedNode(TYP_SIMD16, address, TYP_FLOAT, 16);
            _ = nonTemporal
                ? compiler.gtNewSimdStoreNonTemporalNode(address, value, TYP_FLOAT, 16)
                : compiler.gtNewSimdStoreAlignedNode(address, value, TYP_FLOAT, 16);
            Assert.That(s_assertions, Is.EqualTo(2));
        });
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertions++;

        return 0;
    }
#endif

    private static GenTreeLclVar Local(Compiler compiler, var_types type)
    {
        var index = compiler.lvaCount++;
        compiler.lvaTable[index] = new LclVarDsc { Type = type };

        return compiler.gtNewLclvNode(type, index);
    }

    private static void WithCompiler(bool minOpts, Action<Compiler> action)
    {
#if DEBUG
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        using var tls = new JitTls(&jitInfo);
        s_assertions = 0;
#endif
        var previous = JitTls.Compiler;
        var previousConfig = JitConfig;
        JitConfig = new JitConfigValues();
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minOpts);
        compiler.info = new Compiler.Info();
        compiler.lvaTable = new LclVarDsc[128];
        JitTls.Compiler = compiler;
        try
        {
            action(compiler);
#if DEBUG
            if (!minOpts)
            {
                Assert.That(s_assertions, Is.Zero);
            }
#endif
        }
        finally
        {
            JitTls.Compiler = previous;
            JitConfig = previousConfig;
        }
    }
}
#endif
