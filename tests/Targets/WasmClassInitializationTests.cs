// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_WASM && FEATURE_READYTORUN
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class WasmClassInitializationTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void SharedClassConstructorUsesCoreClrOnlyWasmFallback(bool nativeAot)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.info.compMethodHnd = (CORINFO_METHOD_STRUCT_*)0x1000;
        compiler.info.compClassHnd = (CORINFO_CLASS_STRUCT_*)0x2000;
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.embedClassHandle = &EmbedClassHandle;
        vtable.Base.Base.getClassAttribs = &GetClassAttribs;
        vtable.Base.Base.getReadyToRunHelper = &GetReadyToRunHelper;
        vtable.Base.Base.getEEInfo = &GetEEInfo;
        ClassInitializationContext context = new()
        {
            Interface = new ICorJitInfo { lpVtbl = &vtable },
            Abi = CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI,
        };
        compiler.info.compCompHnd = &context.Interface;
        JitTls.Compiler = compiler;

        try
        {
            if (nativeAot)
            {
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_AOT);
                context.Abi = CORINFO_RUNTIME_ABI.CORINFO_NATIVEAOT_ABI;
            }

            var cls = (CORINFO_CLASS_STRUCT_*)0x3000;
            var call = compiler.fgGetSharedCCtor(cls) ?? throw new InvalidOperationException();

            Assert.That(call.HelperNum, Is.EqualTo(nativeAot
                ? CorInfoHelpFunc.CORINFO_HELP_READYTORUN_NONGCSTATIC_BASE
                : CorInfoHelpFunc.CORINFO_HELP_INITCLASS));
            Assert.That(context.ReadyToRunRequests, Is.EqualTo(nativeAot ? 1 : 0));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private struct ClassInitializationContext
    {
        public ICorJitInfo Interface;
        public CORINFO_RUNTIME_ABI Abi;
        public int ReadyToRunRequests;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* EmbedClassHandle(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* cls,
        void** indirection)
    {
        *indirection = null;
        return cls;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoFlag GetClassAttribs(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* cls)
        => 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetEEInfo(ICorJitInfo* self, CORINFO_EE_INFO* info)
    {
        *info = default;
        info->targetAbi = ((ClassInitializationContext*)self)->Abi;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte GetReadyToRunHelper(ICorJitInfo* self, CORINFO_RESOLVED_TOKEN* token,
        CorInfoHelpFunc helper, CORINFO_METHOD_STRUCT_* method, CORINFO_CONST_LOOKUP* lookup)
    {
        var context = (ClassInitializationContext*)self;
        context->ReadyToRunRequests++;
        *lookup = default;
        lookup->accessType = InfoAccessType.IAT_VALUE;
        lookup->addr = (void*)0x4000;
        return 1;
    }
}
#endif
