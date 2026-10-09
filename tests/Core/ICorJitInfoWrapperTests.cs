// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if MEASURE_CLRAPI_CALLS && FEATURE_JIT_METHOD_PERF
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.API_ICorJitInfo_Names;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.Globals;
using static RyuJitSharp.InfoAccessType;
using static RyuJitSharp.Phases;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ICorJitInfoWrapperTests
{
    private static JitTimer s_timer = new JitTimer(0);
    private static API_ICorJitInfo_Names s_observedApi;
    private static nint s_observedReceiver;
    private static nint s_observedArgument;
    private static nint s_observedOutput;
    private static nint s_observedMethodOutput;

    [Test]
    public static void ForwardingPreservesReceiverArgumentsResultsAndExactApiAccounting()
    {
        var previousConfig = JitConfig;
        JitConfig = new JitConfigValues();
        TimingEnabled(ref JitConfig) = 1;

        try
        {
            var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
            s_timer = new JitTimer(0);
            CompilerTimer(compiler) = s_timer;
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.isIntrinsic = &IsIntrinsic;
            vtable.Base.getHelperFtn = &GetHelperFtn;
            vtable.setEHcount = &SetEHCount;
            vtable.allocGCInfo = &AllocGCInfo;
            ICorJitInfo jitInfo = new ICorJitInfo { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;
            using var wrapper = new WrapICorJitInfo(compiler, &jitInfo);
            wrapper.Install();
            var proxy = compiler.info.compCompHnd;

            var slots = (nint*)proxy->lpVtbl;
            for (var index = 0; index < (int)API_COUNT; index++)
            {
                Assert.That(slots[index], Is.Not.Zero, $"Missing callback for ICorJitInfo API slot {index}.");
            }

            for (var index = 0; index < 2; index++)
            {
                Assert.That(proxy->isIntrinsic((CORINFO_METHOD_STRUCT_*)(123 + index)), Is.True);
                Assert.That(s_observedApi, Is.EqualTo(API_isIntrinsic));
                Assert.That(s_observedReceiver, Is.EqualTo((nint)(&jitInfo)));
                Assert.That(s_observedArgument, Is.EqualTo((nint)(123 + index)));
                Assert.That(ActiveApi(s_timer), Is.EqualTo((API_ICorJitInfo_Names)(-1)));
            }

            proxy->setEHcount(17);
            Assert.That(s_observedApi, Is.EqualTo(API_setEHcount));
            Assert.That(s_observedArgument, Is.EqualTo((nint)17));
            Assert.That(ActiveApi(s_timer), Is.EqualTo((API_ICorJitInfo_Names)(-1)));
            Assert.That((nint)proxy->allocGCInfo(29), Is.EqualTo((nint)456));
            Assert.That(s_observedApi, Is.EqualTo(API_allocGCInfo));
            Assert.That(s_observedArgument, Is.EqualTo((nint)29));
            Assert.That(ActiveApi(s_timer), Is.EqualTo((API_ICorJitInfo_Names)(-1)));

            CORINFO_CONST_LOOKUP lookup = default;
            CORINFO_METHOD_STRUCT_* method = null;
            proxy->getHelperFtn(CORINFO_HELP_MEMCPY, &lookup, &method);
            Assert.That(s_observedApi, Is.EqualTo(API_getHelperFtn));
            Assert.That(s_observedReceiver, Is.EqualTo((nint)(&jitInfo)));
            Assert.That(s_observedArgument, Is.EqualTo((nint)CORINFO_HELP_MEMCPY));
            Assert.That(s_observedOutput, Is.EqualTo((nint)(&lookup)));
            Assert.That(s_observedMethodOutput, Is.EqualTo((nint)(&method)));
            Assert.That(lookup.accessType, Is.EqualTo(IAT_PVALUE));
            Assert.That((nint)lookup.addr, Is.EqualTo((nint)0x1234));
            Assert.That((nint)method, Is.EqualTo((nint)0x5678));
            Assert.That(ActiveApi(s_timer), Is.EqualTo((API_ICorJitInfo_Names)(-1)));

            proxy->getHelperFtn(CORINFO_HELP_MEMCPY, &lookup);
            Assert.That(s_observedApi, Is.EqualTo(API_getHelperFtn));
            Assert.That(s_observedMethodOutput, Is.EqualTo((nint)0));
            Assert.That(ActiveApi(s_timer), Is.EqualTo((API_ICorJitInfo_Names)(-1)));

            proxy->getHelperFtn(CORINFO_HELP_MEMCPY, null, &method);
            Assert.That(s_observedOutput, Is.EqualTo((nint)0));
            Assert.That(s_observedMethodOutput, Is.EqualTo((nint)(&method)));
            Assert.That((nint)method, Is.EqualTo((nint)0x5678));
            Assert.That(ActiveApi(s_timer), Is.EqualTo((API_ICorJitInfo_Names)(-1)));

            ref var info = ref TimerInfo(s_timer);
            Assert.That(info._allClrApiCalls, Is.EqualTo(7));
            Assert.That(info._invokesByPhase[(int)PHASE_CLR_API], Is.EqualTo(7));
            Assert.That(info._perClrApiCalls[(int)API_isIntrinsic], Is.EqualTo(2));
            Assert.That(info._perClrApiCalls[(int)API_setEHcount], Is.EqualTo(1));
            Assert.That(info._perClrApiCalls[(int)API_allocGCInfo], Is.EqualTo(1));
            Assert.That(info._perClrApiCalls[(int)API_getHelperFtn], Is.EqualTo(3));

            uint calls = 0;
            ulong cycles = 0;

            for (var index = 0; index < (int)API_COUNT; index++)
            {
                calls += info._perClrApiCalls[index];
                cycles += info._perClrApiCycles[index];
                Assert.That((ulong)info._maxClrApiCycles[index], Is.InRange(0UL, info._perClrApiCycles[index]));
            }

            Assert.That(calls, Is.EqualTo(7));
            Assert.That(cycles, Is.EqualTo(info._allClrApiCycles));
            Assert.That(info._cyclesByPhase[(int)PHASE_CLR_API], Is.EqualTo((ulong)cycles));
            Assert.That((nint)compiler.info.compCompHnd, Is.EqualTo((nint)proxy));
            wrapper.Dispose();
            Assert.That((nint)compiler.info.compCompHnd, Is.EqualTo((nint)(&jitInfo)));
        }
        finally
        {
            JitConfig = previousConfig;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsIntrinsic(ICorJitInfo* receiver, CORINFO_METHOD_STRUCT_* method)
    {
        Observe(receiver, (nint)method);

        return 127;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void SetEHCount(ICorJitInfo* receiver, int count)
    {
        Observe(receiver, count);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetHelperFtn(ICorJitInfo* receiver, CorInfoHelpFunc helper,
        CORINFO_CONST_LOOKUP* lookup, CORINFO_METHOD_STRUCT_** method)
    {
        s_observedApi = ActiveApi(s_timer);
        s_observedReceiver = (nint)receiver;
        s_observedArgument = (nint)helper;
        s_observedOutput = (nint)lookup;
        s_observedMethodOutput = (nint)method;
        if (lookup is not null)
        {
            lookup->accessType = IAT_PVALUE;
            lookup->addr = (void*)0x1234;
        }

        if (method is not null)
        {
            *method = (CORINFO_METHOD_STRUCT_*)0x5678;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void* AllocGCInfo(ICorJitInfo* receiver, nint size)
    {
        Observe(receiver, size);

        return (void*)456;
    }

    private static void Observe(ICorJitInfo* receiver, nint argument)
    {
        s_observedApi = ActiveApi(s_timer);
        s_observedReceiver = (nint)receiver;
        s_observedArgument = argument;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitEECallTimingInfo")]
    private static extern ref int TimingEnabled(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "compJitTimer")]
    private static extern ref JitTimer? CompilerTimer(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_clrCallApiNum")]
    private static extern ref API_ICorJitInfo_Names ActiveApi(JitTimer timer);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_info")]
    private static extern ref CompTimeInfo TimerInfo(JitTimer timer);
}
#endif
