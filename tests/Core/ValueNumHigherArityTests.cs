// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ValueNumHigherArityTests
{
    [TestCase(VNFunc.VNF_MapPhysicalStore, TYP_REF)]
    [TestCase(VNFunc.VNF_PtrToStatic, TYP_BYREF)]
    [TestCase(VNFunc.VNF_LdElemA, TYP_BYREF)]
    public static void TernaryInternsExactFunctionAndOrderedArguments(VNFunc func, var_types type)
    {
        WithStore(store => {
            var a = store.VNForExpr(null, TYP_INT);
            var b = store.VNForExpr(null, TYP_INT);
            var c = store.VNForExpr(null, TYP_INT);
            var d = store.VNForExpr(null, TYP_INT);
            var result = store.VNForFunc(type, func, a, b, c);
            Assert.That(result, Is.Not.EqualTo(ValueNumStore.NoVN));
            Assert.That(store.VNForFunc(type, func, a, b, c), Is.EqualTo(result));
            Assert.That(store.VNForFunc(type, func, d, b, c), Is.Not.EqualTo(result));
            Assert.That(store.VNForFunc(type, func, a, d, c), Is.Not.EqualTo(result));
            Assert.That(store.VNForFunc(type, func, a, b, d), Is.Not.EqualTo(result));
            Assert.That(store.VNForFunc(type, func, b, a, c), Is.Not.EqualTo(result));

            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(result, ref app), Is.True);
            Assert.That(app.Func, Is.EqualTo(func));
            Assert.That(app.Arity, Is.EqualTo(3));
            Assert.That(app.GetArg(0), Is.EqualTo(a));
            Assert.That(app.GetArg(1), Is.EqualTo(b));
            Assert.That(app.GetArg(2), Is.EqualTo(c));
        });
    }

    [Test]
    public static void TernaryFunctionAndResultTypeDoNotChangeExistingKey()
    {
        WithStore(store => {
            var a = store.VNForExpr(null, TYP_INT);
            var b = store.VNForExpr(null, TYP_INT);
            var c = store.VNForExpr(null, TYP_INT);
            var initial = store.VNForFunc(TYP_REF, VNFunc.VNF_MapPhysicalStore, a, b, c);
            var distinct = store.VNForFunc(TYP_REF, VNFunc.VNF_LdElemA, a, b, c);
            Assert.That(distinct, Is.Not.EqualTo(initial));
            Assert.That(store.VNForFunc(TYP_INT, VNFunc.VNF_MapPhysicalStore, a, b, c),
                Is.EqualTo(initial));
            Assert.That(store.TypeOfVN(initial), Is.EqualTo(TYP_REF));
        });
    }

    [TestCase(VNFunc.VNF_MapStore, TYP_REF)]
    [TestCase(VNFunc.VNF_PtrToArrElem, TYP_BYREF)]
    public static void QuaternaryInternsExactFunctionAndOrderedArguments(VNFunc func, var_types type)
    {
        WithStore(store => {
            var a = store.VNForExpr(null, TYP_INT);
            var b = store.VNForExpr(null, TYP_INT);
            var c = store.VNForExpr(null, TYP_INT);
            var d = store.VNForExpr(null, TYP_INT);
            var e = store.VNForExpr(null, TYP_INT);
            var result = store.VNForFunc(type, func, a, b, c, d);
            Assert.That(result, Is.Not.EqualTo(ValueNumStore.NoVN));
            Assert.That(store.VNForFunc(type, func, a, b, c, d), Is.EqualTo(result));
            Assert.That(store.VNForFunc(type, func, e, b, c, d), Is.Not.EqualTo(result));
            Assert.That(store.VNForFunc(type, func, a, e, c, d), Is.Not.EqualTo(result));
            Assert.That(store.VNForFunc(type, func, a, b, e, d), Is.Not.EqualTo(result));
            Assert.That(store.VNForFunc(type, func, a, b, c, e), Is.Not.EqualTo(result));
            Assert.That(store.VNForFunc(type, func, b, a, c, d), Is.Not.EqualTo(result));

            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(result, ref app), Is.True);
            Assert.That(app.Func, Is.EqualTo(func));
            Assert.That(app.Arity, Is.EqualTo(4));
            Assert.That(app.GetArg(0), Is.EqualTo(a));
            Assert.That(app.GetArg(1), Is.EqualTo(b));
            Assert.That(app.GetArg(2), Is.EqualTo(c));
            Assert.That(app.GetArg(3), Is.EqualTo(d));
        });
    }

    [Test]
    public static void QuaternaryFunctionAndResultTypeDoNotChangeExistingKey()
    {
        WithStore(store => {
            var a = store.VNForExpr(null, TYP_INT);
            var b = store.VNForExpr(null, TYP_INT);
            var c = store.VNForExpr(null, TYP_INT);
            var d = store.VNForExpr(null, TYP_INT);
            var initial = store.VNForFunc(TYP_REF, VNFunc.VNF_MapStore, a, b, c, d);
            var distinct = store.VNForFunc(TYP_REF, VNFunc.VNF_PtrToArrElem, a, b, c, d);
            Assert.That(distinct, Is.Not.EqualTo(initial));
            Assert.That(store.VNForFunc(TYP_INT, VNFunc.VNF_MapStore, a, b, c, d),
                Is.EqualTo(initial));
            Assert.That(store.TypeOfVN(initial), Is.EqualTo(TYP_REF));
        });
    }

    [Test]
    public static void MapStoreAllowsNoVNOrExceptionBearingFourthArgument()
    {
        WithStore(store => {
            var map = store.VNForExpr(null, TYP_REF);
            var index = store.VNForExpr(null, TYP_INT);
            var value = store.VNForExpr(null, TYP_INT);
            var sentinel = store.VNForFunc(TYP_REF, VNFunc.VNF_MapStore,
                map, index, value, ValueNumStore.NoVN);
            Assert.That(sentinel, Is.EqualTo(store.VNForFunc(TYP_REF, VNFunc.VNF_MapStore,
                map, index, value, ValueNumStore.NoVN)));

            var exception = store.VNExcSetSingleton(
                store.VNForFunc(TYP_REF, VNFunc.VNF_NullPtrExc, ValueNumStore.VNForNull()));
            var withException = store.VNWithExc(value, exception);
            Assert.That(withException, Is.Not.EqualTo(store.VNNormalValue(withException)));
            var attributed = store.VNForFunc(TYP_REF, VNFunc.VNF_MapStore,
                map, index, value, withException);
            Assert.That(attributed, Is.Not.EqualTo(sentinel));

            var firstApp = new VNFuncApp();
            Assert.That(store.GetVNFunc(sentinel, ref firstApp), Is.True);
            Assert.That(firstApp.Arity, Is.EqualTo(4));
            Assert.That(firstApp.GetArg(3), Is.EqualTo(ValueNumStore.NoVN));
            var secondApp = new VNFuncApp();
            Assert.That(store.GetVNFunc(attributed, ref secondApp), Is.True);
            Assert.That(secondApp.GetArg(3), Is.EqualTo(withException));
        });
    }

    [Test]
    public static void ZeroArityAttributePermitsBothRecordedArgumentCounts()
    {
        WithStore(store => {
#if FEATURE_HW_INTRINSICS
            var func = VNFunc.VNF_HWI_Vector_Create;
#else
            var func = VNFunc.VNF_LoopCloneChoiceAddr;
#endif
            Assert.That(ValueNumStore.VNFuncArity(func), Is.Zero);
            var a = store.VNForExpr(null, TYP_INT);
            var b = store.VNForExpr(null, TYP_INT);
            var c = store.VNForExpr(null, TYP_INT);
            var d = store.VNForExpr(null, TYP_INT);
            var ternary = store.VNForFunc(TYP_INT, func, a, b, c);
            var quaternary = store.VNForFunc(TYP_INT, func, a, b, c, d);
            Assert.That(ternary, Is.Not.EqualTo(quaternary));
            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(ternary, ref app), Is.True);
            Assert.That(app.Arity, Is.EqualTo(3));
            Assert.That(store.GetVNFunc(quaternary, ref app), Is.True);
            Assert.That(app.Arity, Is.EqualTo(4));
        });
    }

    [Test]
    public static void HigherArityChunksRolloverAndRetainStableViews()
    {
        WithStore(store => {
            var a = store.VNForExpr(null, TYP_INT);
            var b = store.VNForExpr(null, TYP_INT);
            var c = store.VNForExpr(null, TYP_INT);
            var d = store.VNForExpr(null, TYP_INT);
            var ternary = new int[70];
            var quaternary = new int[70];
            for (var index = 0; index < ternary.Length; index++)
            {
                var varying = store.VNForIntCon(index + 100);
                ternary[index] = store.VNForFunc(TYP_INT, VNFunc.VNF_MapPhysicalStore, a, b, varying);
                quaternary[index] = store.VNForFunc(TYP_REF, VNFunc.VNF_MapStore, a, b, c, varying);
            }

            Assert.That(ternary[63] >>> 6, Is.Not.EqualTo(ternary[64] >>> 6));
            Assert.That(quaternary[63] >>> 6, Is.Not.EqualTo(quaternary[64] >>> 6));
            var ternaryApp = new VNFuncApp();
            var quaternaryApp = new VNFuncApp();
            Assert.That(store.GetVNFunc(ternary[0], ref ternaryApp), Is.True);
            Assert.That(store.GetVNFunc(quaternary[0], ref quaternaryApp), Is.True);
            Assert.That(ternaryApp.Func, Is.EqualTo(VNFunc.VNF_MapPhysicalStore));
            Assert.That(ternaryApp.GetArg(0), Is.EqualTo(a));
            Assert.That(ternaryApp.GetArg(1), Is.EqualTo(b));
            Assert.That(ternaryApp.GetArg(2), Is.EqualTo(store.VNForIntCon(100)));
            Assert.That(quaternaryApp.Func, Is.EqualTo(VNFunc.VNF_MapStore));
            Assert.That(quaternaryApp.GetArg(0), Is.EqualTo(a));
            Assert.That(quaternaryApp.GetArg(1), Is.EqualTo(b));
            Assert.That(quaternaryApp.GetArg(2), Is.EqualTo(c));
            Assert.That(quaternaryApp.GetArg(3), Is.EqualTo(store.VNForIntCon(100)));
            Assert.That(store.VNForFunc(TYP_INT, VNFunc.VNF_MapPhysicalStore, a, b, store.VNForIntCon(100)),
                Is.EqualTo(ternary[0]));
            Assert.That(store.VNForFunc(TYP_REF, VNFunc.VNF_MapStore, a, b, c, store.VNForIntCon(100)),
                Is.EqualTo(quaternary[0]));
        });
    }

    private static void WithStore(Action<ValueNumStore> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        JitTls.Compiler = compiler;
        try
        {
            action(new ValueNumStore(compiler));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
