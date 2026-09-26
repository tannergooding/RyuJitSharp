// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ValueNumArrayAddressTests
{
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(7)]
    public static void ConstantOffsetsRecoverElementIndices(int index)
    {
        WithStore((compiler, store) =>
        {
            var array = compiler.gtNewLclvNode(TYP_REF, 0);
            var address = compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF, array,
                compiler.gtNewIconNode(TYP_I_IMPL, 16 + (4 * index)));
            var wrapper = new GenTreeArrAddr(address, TYP_INT, null, 16);
            var result = ValueNumStore.NoVN;
            wrapper.ParseArrayAddress(compiler, out var parsedArray, ref result);
            Assert.That(parsedArray, Is.SameAs(array));
            Assert.That(store.CoercedConstantValue<long>(result), Is.EqualTo(index));
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public static void ScalingAndTransparentCommasRecoverTheOriginalIndex(int shape)
    {
        WithStore((compiler, store) =>
        {
            var array = compiler.gtNewLclvNode(TYP_REF, 0);
            var index = compiler.gtNewLclvNode(TYP_I_IMPL, 1);
            var indexVN = store.VNForExpr(null, TYP_I_IMPL);
            index._vnPair.SetBoth(indexVN);
            var scale = compiler.gtNewIconNode(TYP_I_IMPL, shape == 2 ? 2 : 4);
            GenTree scaled = shape == 1
                ? compiler.gtNewBinaryNode(GT_MUL, TYP_I_IMPL, scale, index)
                : compiler.gtNewBinaryNode(shape == 2 ? GT_LSH : GT_MUL, TYP_I_IMPL, index, scale);
            if (shape == 3)
            {
                scaled = compiler.gtNewBinaryNode(GT_COMMA, TYP_I_IMPL, compiler.gtNewNothingNode(), scaled);
            }
            var offset = compiler.gtNewBinaryNode(GT_ADD, TYP_I_IMPL, scaled,
                compiler.gtNewIconNode(TYP_I_IMPL, 16));
            var address = compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF, array, offset);
            var wrapper = new GenTreeArrAddr(address, TYP_INT, null, 16);
            var result = ValueNumStore.NoVN;
            wrapper.ParseArrayAddress(compiler, out var parsedArray, ref result);
            Assert.That(parsedArray, Is.SameAs(array));
            Assert.That(result, Is.EqualTo(indexVN));
        });
    }

    [Test]
    public static void UnscaledVariableOffsetRequiresDivisionAndRetainsConstantContribution()
    {
        WithStore((compiler, store) =>
        {
            var array = compiler.gtNewLclvNode(TYP_REF, 0);
            var offset = compiler.gtNewLclvNode(TYP_I_IMPL, 1);
            var offsetVN = store.VNForExpr(null, TYP_I_IMPL);
            offset._vnPair.SetBoth(offsetVN);
            var address = compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF, array,
                compiler.gtNewBinaryNode(GT_ADD, TYP_I_IMPL, offset,
                    compiler.gtNewIconNode(TYP_I_IMPL, 24)));
            var wrapper = new GenTreeArrAddr(address, TYP_INT, null, 16);
            var result = ValueNumStore.NoVN;
            wrapper.ParseArrayAddress(compiler, out var parsedArray, ref result);
            var quotient = store.VNForFunc(TYP_I_IMPL, VNFunc.VNF_DIV, offsetVN, store.VNForLongCon(4));
            var expected = store.VNForFunc(TYP_I_IMPL, VNFunc.VNF_ADD, quotient, store.VNForLongCon(2));
            Assert.That(parsedArray, Is.SameAs(array));
            Assert.That(result, Is.EqualTo(expected));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void FailedParsingPreservesTheCallerIndex(bool hasArray)
    {
        WithStore((compiler, store) =>
        {
            GenTree address = compiler.gtNewIconNode(TYP_I_IMPL, 8);
            if (hasArray)
            {
                address = compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF,
                    compiler.gtNewLclvNode(TYP_REF, 0), address);
            }
            var wrapper = new GenTreeArrAddr(address, TYP_INT, null, 16);
            var sentinel = store.VNForIntCon(1234);
            var result = sentinel;
            wrapper.ParseArrayAddress(compiler, out var array, ref result);
            Assert.That(array, Is.Null);
            Assert.That(result, Is.EqualTo(sentinel));
        });
    }

    private static void WithStore(Action<Compiler, ValueNumStore> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.lvaCount = 2;
        compiler.lvaTable = new LclVarDsc[2];
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        JitTls.Compiler = compiler;
        try
        {
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
