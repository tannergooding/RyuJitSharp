#if TARGET_WASM && FEATURE_READYTORUN
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using unsafe CORINFO_CLASS_HANDLE = RyuJitSharp.CORINFO_CLASS_STRUCT_*;
using unsafe CORINFO_CONTEXT_HANDLE = RyuJitSharp.CORINFO_CONTEXT_STRUCT_*;
using unsafe CORINFO_METHOD_HANDLE = RyuJitSharp.CORINFO_METHOD_STRUCT_*;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.gtCallTypes;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class FlowGraphDelegateConstructorTests
{
    [Test]
    public static void WasmReadyToRunDelegateCtorUsesTheAlternateEntryPoint()
    {
        WithCompiler(compiler => {
            var originalConstructor = (CORINFO_METHOD_HANDLE)0x1110;
            var alternateConstructor = (CORINFO_METHOD_HANDLE)0x2220;
            var targetMethod = (CORINFO_METHOD_HANDLE)0x3330;
            var delegateClass = (CORINFO_CLASS_HANDLE)0x4440;
            var replacementEntryPoint = new CORINFO_CONST_LOOKUP {
                accessType = InfoAccessType.IAT_PVALUE,
                addr = (void*)0x5550,
            };

            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.getEEInfo = &GetEEInfo;
            vtable.Base.Base.getMethodClass = &GetMethodClass;
            vtable.Base.GetDelegateCtor = &GetDelegateCtor;
            vtable.Base.getFunctionEntryPoint = &GetFunctionEntryPoint;

            var ee = new DelegateConstructorEE {
                Interface = new ICorJitInfo { lpVtbl = &vtable },
                TargetAbi = CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI,
                DelegateClass = delegateClass,
                AlternateConstructor = alternateConstructor,
                AlternateEntryPoint = replacementEntryPoint,
            };
            compiler.info.compCompHnd = &ee.Interface;
            compiler.info.compMethodHnd = (CORINFO_METHOD_HANDLE)0x6660;
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_AOT);
            Assert.That(compiler.IsReadyToRun, Is.True);

            var call = new GenTreeCall(TYP_VOID) {
                _callType = CT_USER_FUNC,
                _callMethHnd = originalConstructor,
                _entryPoint = new CORINFO_CONST_LOOKUP {
                    accessType = InfoAccessType.IAT_VALUE,
                    addr = (void*)0x7770,
                },
            };
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(new GenTreeLclVar(TYP_REF, 0))
                .WithWellKnownArg(WellKnownArg.ThisPointer));
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(new GenTreeLclVar(TYP_REF, 1)));
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(new GenTreeFptrVal(TYP_I_IMPL, targetMethod)));

            var exactContext = default(CORINFO_CONTEXT_HANDLE);
            var result = compiler.fgOptimizeDelegateConstructor(call, ref exactContext, ldftnToken: null);

            Assert.That(result, Is.SameAs(call));
            Assert.That(ee.MethodClassQueries, Is.EqualTo(1));
            Assert.That((nint)ee.MethodClassMethod, Is.EqualTo((nint)originalConstructor));
            Assert.That(ee.DelegateCtorQueries, Is.EqualTo(1));
            Assert.That((nint)ee.DelegateCtorMethod, Is.EqualTo((nint)originalConstructor));
            Assert.That((nint)ee.DelegateCtorClass, Is.EqualTo((nint)delegateClass));
            Assert.That((nint)ee.DelegateCtorTargetMethod, Is.EqualTo((nint)targetMethod));
            Assert.That(ee.EntryPointQueries, Is.EqualTo(1));
            Assert.That((nint)ee.EntryPointMethod, Is.EqualTo((nint)alternateConstructor));
            Assert.That((nint)call._callMethHnd, Is.EqualTo((nint)alternateConstructor));
            Assert.That(call._entryPoint.accessType, Is.EqualTo(replacementEntryPoint.accessType));
            Assert.That((nint)call._entryPoint.addr, Is.EqualTo((nint)replacementEntryPoint.addr));
        });
    }

    private struct DelegateConstructorEE
    {
        public ICorJitInfo Interface;
        public CORINFO_RUNTIME_ABI TargetAbi;
        public CORINFO_CLASS_HANDLE DelegateClass;
        public CORINFO_METHOD_HANDLE AlternateConstructor;
        public CORINFO_CONST_LOOKUP AlternateEntryPoint;
        public CORINFO_METHOD_HANDLE MethodClassMethod;
        public CORINFO_METHOD_HANDLE DelegateCtorMethod;
        public CORINFO_CLASS_HANDLE DelegateCtorClass;
        public CORINFO_METHOD_HANDLE DelegateCtorTargetMethod;
        public CORINFO_METHOD_HANDLE EntryPointMethod;
        public int MethodClassQueries;
        public int DelegateCtorQueries;
        public int EntryPointQueries;
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

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetEEInfo(ICorJitInfo* self, CORINFO_EE_INFO* info)
    {
        *info = default;
        info->targetAbi = ((DelegateConstructorEE*)self)->TargetAbi;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_HANDLE GetMethodClass(ICorJitInfo* self, CORINFO_METHOD_HANDLE method)
    {
        var ee = (DelegateConstructorEE*)self;
        ee->MethodClassQueries++;
        ee->MethodClassMethod = method;
        return ee->DelegateClass;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_METHOD_HANDLE GetDelegateCtor(ICorJitInfo* self, CORINFO_METHOD_HANDLE method,
        CORINFO_CLASS_HANDLE delegateClass, CORINFO_METHOD_HANDLE targetMethod, DelegateCtorArgs* _)
    {
        var ee = (DelegateConstructorEE*)self;
        ee->DelegateCtorQueries++;
        ee->DelegateCtorMethod = method;
        ee->DelegateCtorClass = delegateClass;
        ee->DelegateCtorTargetMethod = targetMethod;
        return ee->AlternateConstructor;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetFunctionEntryPoint(ICorJitInfo* self, CORINFO_METHOD_HANDLE method,
        CORINFO_CONST_LOOKUP* lookup, CORINFO_ACCESS_FLAGS _)
    {
        var ee = (DelegateConstructorEE*)self;
        ee->EntryPointQueries++;
        ee->EntryPointMethod = method;
        *lookup = ee->AlternateEntryPoint;
    }
}
#endif
