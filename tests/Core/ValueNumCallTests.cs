// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using ValueNum = System.Int32;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.GenTreeCallFlags;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.MemoryKind;
using static RyuJitSharp.VNFunc;
using static RyuJitSharp.gtCallTypes;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ValueNumCallTests
{
    [TestCase(CORINFO_HELP_LDIV, VNF_DIV)]
    [TestCase(CORINFO_HELP_DBLREM, VNF_MOD)]
    [TestCase(CORINFO_HELP_NEWARR_1_PTR, VNF_JitNewArr)]
    [TestCase(CORINFO_HELP_CHKCASTINTERFACE, VNF_CastClass)]
    [TestCase(CORINFO_HELP_GETDYNAMIC_GCTHREADSTATIC_BASE_NOCTOR, VNF_GetdynamicGcthreadstaticBaseNoctor)]
    [TestCase(CORINFO_HELP_READYTORUN_VIRTUAL_FUNC_PTR, VNF_ReadyToRunVirtualFuncPtr)]
    public static void HelperMappingPreservesNativeFunction(CorInfoHelpFunc helper, VNFunc expected)
    {
        WithStore((compiler, _) =>
            Assert.That(compiler.fgValueNumberJitHelperMethodVNFunc(helper), Is.EqualTo(expected)));
    }

    [TestCase(CORINFO_HELP_LNG2FLT, TYP_FLOAT, VNF_Cast)]
    [TestCase(CORINFO_HELP_DBL2INT_OVF, TYP_INT, VNF_CastOvf)]
    [TestCase(CORINFO_HELP_DBL2ULNG_OVF, TYP_ULONG, VNF_CastOvf)]
    public static void CastHelpersPreserveCastAndOverflow(CorInfoHelpFunc helper, var_types result, VNFunc func)
    {
        WithStore((compiler, store) =>
        {
            var sourceType = helper is CORINFO_HELP_LNG2FLT ? TYP_LONG : TYP_DOUBLE;
            var call = Helper(helper, result);
            var source = Argument(compiler, store, sourceType, store.VNForExpr(null, sourceType));
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(source));
            _ = compiler.fgValueNumberHelperCall(call);

            store.VNUnpackExc(call._vnPair.Liberal, out var normal, out var exceptions);
            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(normal, ref app), Is.True);
            Assert.That(app.Func, Is.EqualTo(func));
            Assert.That(exceptions != ValueNumStore.VNForEmptyExcSet(),
                Is.EqualTo(func is VNF_CastOvf));
        });
    }

    [Test]
    public static void PureHelperKeepsArgumentExceptionsInLogicalOrder()
    {
        WithStore((compiler, store) =>
        {
            var call = Helper(CORINFO_HELP_LLSH, TYP_LONG);
            var leftVN = store.VNForExpr(null, TYP_LONG);
            var rightVN = store.VNForExpr(null, TYP_INT);
            var firstExc = store.VNExcSetSingleton(store.VNForFunc(TYP_REF, VNF_OverflowExc, leftVN));
            var secondExc = store.VNExcSetSingleton(store.VNForFunc(TYP_REF, VNF_HelperOpaqueExc, rightVN));
            var first = Argument(compiler, store, TYP_LONG, store.VNWithExc(leftVN, firstExc));
            var second = Argument(compiler, store, TYP_INT, store.VNWithExc(rightVN, secondExc));
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(first));
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(second));

            compiler.fgValueNumberHelperCallFunc(call, VNF_LSH, ValueNumStore.VNPForEmptyExcSet());

            store.VNUnpackExc(call._vnPair.Liberal, out var normal, out var exceptions);
            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(normal, ref app), Is.True);
            Assert.That(app.Func, Is.EqualTo(VNF_LSH));
            Assert.That(app.GetArg(0), Is.EqualTo(leftVN));
            Assert.That(app.GetArg(1), Is.EqualTo(rightVN));
            Assert.That(exceptions, Is.EqualTo(store.VNExcSetUnion(firstExc, secondExc)));
        });
    }

    [Test]
    public static void AllocationIsUniqueAndReplacesNewArrayExceptionWithLengthOverflow()
    {
        WithStore((compiler, store) =>
        {
            var first = Helper(CORINFO_HELP_NEWARR_1_DIRECT, TYP_REF);
            var second = Helper(CORINFO_HELP_NEWARR_1_DIRECT, TYP_REF);
            var typeHandle = store.VNForHandle(123, GTF_ICON_CLASS_HDL);
            var lengthVN = store.VNForExpr(null, TYP_INT);
            var argExc = store.VNExcSetSingleton(store.VNForFunc(TYP_REF, VNF_HelperOpaqueExc, lengthVN));
            foreach (var call in new[] { first, second })
            {
                _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(
                    Argument(compiler, store, TYP_I_IMPL, typeHandle)));
                _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(
                    Argument(compiler, store, TYP_INT, store.VNWithExc(lengthVN, argExc))));
                compiler.fgValueNumberHelperCallFunc(call, VNF_JitNewArr, ValueNumStore.VNPForEmptyExcSet());
            }

            store.VNUnpackExc(first._vnPair.Liberal, out var normal, out var exceptions);
            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(normal, ref app), Is.True);
            Assert.That(app.Func, Is.EqualTo(VNF_JitNewArr));
            Assert.That(app.GetArg(0), Is.EqualTo(typeHandle));
            Assert.That(app.GetArg(1), Is.EqualTo(lengthVN));
            Assert.That(first._vnPair.Liberal, Is.Not.EqualTo(second._vnPair.Liberal));
            Assert.That(exceptions, Is.EqualTo(store.VNExcSetUnion(argExc,
                store.VNExcSetSingleton(store.VNForFunc(TYP_REF, VNF_NewArrOverflowExc, lengthVN)))));
        });
    }

#if FEATURE_READYTORUN
    [Test]
    public static void ReadyToRunAllocationUsesEntryPointIdentity()
    {
        WithStore((compiler, store) =>
        {
            var call = Helper(CORINFO_HELP_READYTORUN_NEW, TYP_REF);
            call._entryPoint.addr = (void*)0x321;
            compiler.fgValueNumberHelperCallFunc(call, VNF_JitReadyToRunNew,
                ValueNumStore.VNPForEmptyExcSet());

            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(call._vnPair.Liberal, ref app), Is.True);
            Assert.That(app.Func, Is.EqualTo(VNF_JitReadyToRunNew));
            Assert.That(app.GetArg(0), Is.EqualTo(store.VNForHandle(0x321, GTF_ICON_FTN_ADDR)));
            Assert.That(store.TypeOfVN(app.GetArg(1)), Is.EqualTo(TYP_REF));
        });
    }
#endif

    [Test]
    public static void StackArrayUsesLocalAllocationFunction()
    {
        WithStore((compiler, store) =>
        {
            var call = Helper(CORINFO_HELP_NEWARR_1_DIRECT, TYP_REF);
            call._callMoreFlags |= GTF_CALL_M_STACK_ARRAY;
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(
                Argument(compiler, store, TYP_I_IMPL, store.VNForHandle(123, GTF_ICON_CLASS_HDL))));
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(
                Argument(compiler, store, TYP_INT, store.VNForIntCon(5))));
            _ = compiler.fgValueNumberHelperCall(call);
            var normal = store.VNNormalValue(call._vnPair.Liberal);
            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(normal, ref app), Is.True);
            Assert.That(app.Func, Is.EqualTo(VNF_JitNewLclArr));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CctorHelperMutatesHeapUnlessHoistable(bool hoistable)
    {
        WithStore((compiler, store) =>
        {
            Assert.That(CORINFO_HELP_GET_GCSTATIC_BASE.MayRunCctor, Is.True);
            var call = Helper(CORINFO_HELP_GET_GCSTATIC_BASE, TYP_BYREF);
            if (hoistable)
            {
                call.Flags |= GTF_CALL_HOISTABLE;
            }
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(
                Argument(compiler, store, TYP_I_IMPL, store.VNForHandle(123, GTF_ICON_CLASS_HDL))));

            var modHeap = compiler.fgValueNumberHelperCall(call);
            Assert.That(modHeap, Is.EqualTo(CORINFO_HELP_GET_GCSTATIC_BASE.MutatesHeap || !hoistable));
            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(store.VNNormalValue(call._vnPair.Liberal), ref app), Is.True);
            Assert.That(app.Func, Is.EqualTo(VNF_GetGcstaticBase));
        });
    }

    [Test]
    public static void ImpureHelperHasUniqueValueAndOpaqueException()
    {
        WithStore((compiler, store) =>
        {
            var call = Helper(CORINFO_HELP_GETCURRENTMANAGEDTHREADID, TYP_INT);
            Assert.That(CORINFO_HELP_GETCURRENTMANAGEDTHREADID.IsPure, Is.False);
            var mutates = compiler.fgValueNumberHelperCall(call);
            Assert.That(mutates, Is.EqualTo(CORINFO_HELP_GETCURRENTMANAGEDTHREADID.MutatesHeap));
            store.VNUnpackExc(call._vnPair.Liberal, out var normal, out var exceptions);
            Assert.That(store.TypeOfVN(normal), Is.EqualTo(TYP_INT));
            Assert.That(exceptions != ValueNumStore.VNForEmptyExcSet(),
                Is.EqualTo(!CORINFO_HELP_GETCURRENTMANAGEDTHREADID.NoThrow));
        });
    }

    [Test]
    public static void OrdinaryCallMutatesHeapAndAssignsVoid()
    {
        WithStore((compiler, store) =>
        {
            var call = new GenTreeCall(TYP_VOID) { _callType = CT_USER_FUNC };
            compiler.fgValueNumberCall(call);
            Assert.That(call._vnPair.Liberal, Is.EqualTo(ValueNumStore.VNForVoid()));
            Assert.That(compiler.fgCurMemoryVN[(int)GcHeap], Is.Not.EqualTo(ValueNumStore.NoVN));
            Assert.That(store.TypeOfVN(compiler.fgCurMemoryVN[(int)GcHeap]), Is.EqualTo(TYP_HEAP));
        });
    }

    [TestCase(true, false, false, true, true)]
    [TestCase(false, true, true, true, true)]
    [TestCase(false, true, false, true, false)]
    [TestCase(false, false, false, false, false)]
    public static void AsyncCallPropagatesOnlyKnownResumedKinds(bool alwaysSuspends,
        bool liberalOne, bool conservativeOne, bool expectedLiberal, bool expectedConservative)
    {
        WithStore((compiler, store) =>
        {
            compiler.lvaTable = [new LclVarDsc { Type = TYP_I_IMPL }];
            compiler.lvaCount = 1;
            ref var local = ref compiler.lvaTable[0];
#if DEBUG
            local.IsDefinedViaAddress = true;
#endif
            var ssa = local.lvPerSsaData.AllocSsaNum();
            var call = new GenTreeCall(TYP_VOID) { _callType = CT_USER_FUNC };
            call._callMoreFlags |= GTF_CALL_M_ASYNC;
            call.GetAsyncInfo().AlwaysSuspends = alwaysSuspends;
            var def = compiler.gtNewLclVarAddrNode(TYP_BYREF, 0);
            def.SsaNum = ssa;
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(def)
                .WithWellKnownArg(WellKnownArg.AsyncResumedDef));
            var use = compiler.gtNewIconNode(TYP_INT, 0);
            use._vnPair = new(liberalOne ? store.VNForIntCon(1) : store.VNForExpr(null, TYP_INT),
                conservativeOne ? store.VNForIntCon(1) : store.VNForExpr(null, TYP_INT));
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(use)
                .WithWellKnownArg(WellKnownArg.AsyncResumedUse));

            compiler.fgValueNumberCall(call);

            var value = local.GetPerSsaData(ssa)._vnPair;
            Assert.That(value.Liberal == store.VNForIntPtrCon(1), Is.EqualTo(expectedLiberal));
            Assert.That(value.Conservative == store.VNForIntPtrCon(1), Is.EqualTo(expectedConservative));
            Assert.That(value.BothDefined(), Is.True);
        });
    }

    private static GenTreeCall Helper(CorInfoHelpFunc helper, var_types type) => new(type)
    {
        _callType = CT_HELPER,
        _callMethHnd = Compiler.eeFindHelper(helper),
    };

    private static GenTreeIntCon Argument(Compiler compiler, ValueNumStore store, var_types type, ValueNum value)
    {
        var node = compiler.gtNewIconNode(type, 0);
        node._vnPair.SetBoth(value);
        return node;
    }

    private static void WithStore(Action<Compiler, ValueNumStore> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
#endif
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        JitTls.Compiler = compiler;
        try
        {
            compiler.compCurBB = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            compiler.compCurBB.bbMemoryDef = (1 << (int)GcHeap) | (1 << (int)ByrefExposed);
            compiler.compCurBB.bbRefs = 1;
            compiler.fgFirstBB = compiler.compCurBB;
            compiler.fgLastBB = compiler.compCurBB;
            compiler.fgPredsComputed = true;
            compiler._dfsTree = compiler.fgComputeDfs();
            compiler._loops = FlowGraphNaturalLoops.Find(compiler._dfsTree);
            compiler._blockToLoop = BlockToNaturalLoopMap.Build(compiler._loops);
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
