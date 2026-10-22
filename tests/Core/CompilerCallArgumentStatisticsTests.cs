// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using CallArgumentStatisticsData = RyuJitSharp.Compiler.MeasurementData.CallArgumentStatistics;
using NUnit.Framework;
using static RyuJitSharp.Compiler.MeasurementData.MethodCounts;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CompilerCallArgumentStatisticsTests
{
    [SetUp]
    public static void RequireCallArgumentStatistics()
    {
        Assume.That(Configuration.CALL_ARG_STATS, "Call-argument statistics are disabled.");
    }

    [Test]
    public static void CallStatisticsCountTheLirCallKinds()
    {
        WithStatistics(() => WithCompiler(compiler => {
            TestConfiguration.EnableSafeBasicBlockCreation(compiler);
            compiler.fgNodeThreading = NodeThreading.LIR;

            var block = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            block.MakeLir(null, null);

            var staticCall = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC,
                (CORINFO_METHOD_STRUCT_*)1);
            var helperCall = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_HELPER,
                Compiler.eeFindHelper(CORINFO_HELP_ISINSTANCEOFANY));
            var nonVirtualCall = NewInstanceCall(compiler, isVirtual: false);
            var virtualCall = NewInstanceCall(compiler, isVirtual: true);

            block.InsertAtEnd(staticCall);
            block.InsertAtEnd(helperCall);
            block.InsertAtEnd(nonVirtualCall);
            block.InsertAtEnd(virtualCall);

            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            compiler.compJitStats();

            Assert.That(CallArgumentStatisticsData.argTotalCalls, Is.EqualTo(4));
            Assert.That(CallArgumentStatisticsData.argStaticCalls, Is.EqualTo(1));
            Assert.That(CallArgumentStatisticsData.argHelperCalls, Is.EqualTo(1));
            Assert.That(CallArgumentStatisticsData.argNonVirtualCalls, Is.EqualTo(1));
            Assert.That(CallArgumentStatisticsData.argVirtualCalls, Is.EqualTo(1));
            Assert.That(CallArgumentStatisticsData.argTotalObjPtr, Is.EqualTo(2));
            Assert.That(CallArgumentStatisticsData.argTotalArgs, Is.Zero);
            Assert.That(CallArgumentStatisticsData.argTotalRegArgs, Is.Zero);
        }));
    }

    [Test]
    public static void CallStatisticsReportPreservesNativeLabelsAndPercentages()
    {
        WithStatistics(() => {
            genMethodCnt = 2;
            CallArgumentStatisticsData.argTotalCalls = 4;
            CallArgumentStatisticsData.argHelperCalls = 1;
            CallArgumentStatisticsData.argStaticCalls = 1;
            CallArgumentStatisticsData.argVirtualCalls = 1;
            CallArgumentStatisticsData.argNonVirtualCalls = 1;
            CallArgumentStatisticsData.argTotalArgs = 4;
            CallArgumentStatisticsData.argTotalDWordArgs = 1;
            CallArgumentStatisticsData.argTotalLongArgs = 1;
            CallArgumentStatisticsData.argTotalFloatArgs = 1;
            CallArgumentStatisticsData.argTotalDoubleArgs = 1;
            CallArgumentStatisticsData.argTotalRegArgs = 4;
            CallArgumentStatisticsData.argTotalTemps = 1;
            CallArgumentStatisticsData.argTotalLclVar = 1;
            CallArgumentStatisticsData.argTotalDeferred = 1;
            CallArgumentStatisticsData.argTotalConst = 1;
            CallArgumentStatisticsData.argTotalObjPtr = 2;
            CallArgumentStatisticsData.argMaxTempsPerMethod = 3;

            using var output = new MemoryStream();
            using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);

            Compiler.compDispCallArgStats(writer);
            writer.Flush();

            var text = Encoding.UTF8.GetString(output.ToArray());
            Assert.That(text, Does.Contain("Total # of calls = 4, calls / method = 2.000"));
            Assert.That(text, Does.Contain("Percentage of      helper calls = 25.00 %"));
            Assert.That(text, Does.Contain("Average # of arguments per call = 1.00%"));
            Assert.That(text, Does.Contain("Percentage of DWORD  arguments   = 25.00 %"));
            Assert.That(text, Does.Contain("Maximum # of temps per method    = 3"));
            Assert.That(text, Does.Contain("DWORD argument count frequency table (w/o LONG):"));
            Assert.That(text, Does.Not.Contain("uint argument count frequency table"));
        });
    }

    [Test]
    public static void CallStatisticsReportPreservesNativeSignedCounterFormatting()
    {
        WithStatistics(() => {
            genMethodCnt = 1;
            CallArgumentStatisticsData.argTotalCalls = uint.MaxValue;
            CallArgumentStatisticsData.argTotalArgs = 1;
            CallArgumentStatisticsData.argTotalRegArgs = 1;
            CallArgumentStatisticsData.argMaxTempsPerMethod = uint.MaxValue;

            using var output = new MemoryStream();
            using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);

            Compiler.compDispCallArgStats(writer);
            writer.Flush();

            var text = Encoding.UTF8.GetString(output.ToArray());
            Assert.That(text, Does.Contain("Total # of calls = -1, calls / method = 4294967296.000"));
            Assert.That(text, Does.Contain("Maximum # of temps per method    = -1"));
        });
    }

    private static GenTreeCall NewInstanceCall(Compiler compiler, bool isVirtual)
    {
        var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC,
            (CORINFO_METHOD_STRUCT_*)1);
        var thisArg = NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_REF, 1))
            .WithWellKnownArg(WellKnownArg.ThisPointer);
        _ = call.Args.PushBack(thisArg);

        if (isVirtual)
        {
            call.Flags |= GTF_CALL_VIRT_VTABLE;
        }

        return call;
    }

    private static void WithStatistics(Action action)
    {
        var saved = (
            genMethodCnt,
            CallArgumentStatisticsData.argTotalCalls,
            CallArgumentStatisticsData.argHelperCalls,
            CallArgumentStatisticsData.argStaticCalls,
            CallArgumentStatisticsData.argNonVirtualCalls,
            CallArgumentStatisticsData.argVirtualCalls,
            CallArgumentStatisticsData.argTotalArgs,
            CallArgumentStatisticsData.argTotalDWordArgs,
            CallArgumentStatisticsData.argTotalLongArgs,
            CallArgumentStatisticsData.argTotalFloatArgs,
            CallArgumentStatisticsData.argTotalDoubleArgs,
            CallArgumentStatisticsData.argTotalRegArgs,
            CallArgumentStatisticsData.argTotalTemps,
            CallArgumentStatisticsData.argTotalLclVar,
            CallArgumentStatisticsData.argTotalDeferred,
            CallArgumentStatisticsData.argTotalConst,
            CallArgumentStatisticsData.argTotalObjPtr,
            CallArgumentStatisticsData.argMaxTempsPerMethod);

        try
        {
            genMethodCnt = 1;
            CallArgumentStatisticsData.argTotalCalls = 0;
            CallArgumentStatisticsData.argHelperCalls = 0;
            CallArgumentStatisticsData.argStaticCalls = 0;
            CallArgumentStatisticsData.argNonVirtualCalls = 0;
            CallArgumentStatisticsData.argVirtualCalls = 0;
            CallArgumentStatisticsData.argTotalArgs = 0;
            CallArgumentStatisticsData.argTotalDWordArgs = 0;
            CallArgumentStatisticsData.argTotalLongArgs = 0;
            CallArgumentStatisticsData.argTotalFloatArgs = 0;
            CallArgumentStatisticsData.argTotalDoubleArgs = 0;
            CallArgumentStatisticsData.argTotalRegArgs = 0;
            CallArgumentStatisticsData.argTotalTemps = 0;
            CallArgumentStatisticsData.argTotalLclVar = 0;
            CallArgumentStatisticsData.argTotalDeferred = 0;
            CallArgumentStatisticsData.argTotalConst = 0;
            CallArgumentStatisticsData.argTotalObjPtr = 0;
            CallArgumentStatisticsData.argMaxTempsPerMethod = 0;

            action();
        }
        finally
        {
            (genMethodCnt,
                CallArgumentStatisticsData.argTotalCalls,
                CallArgumentStatisticsData.argHelperCalls,
                CallArgumentStatisticsData.argStaticCalls,
                CallArgumentStatisticsData.argNonVirtualCalls,
                CallArgumentStatisticsData.argVirtualCalls,
                CallArgumentStatisticsData.argTotalArgs,
                CallArgumentStatisticsData.argTotalDWordArgs,
                CallArgumentStatisticsData.argTotalLongArgs,
                CallArgumentStatisticsData.argTotalFloatArgs,
                CallArgumentStatisticsData.argTotalDoubleArgs,
                CallArgumentStatisticsData.argTotalRegArgs,
                CallArgumentStatisticsData.argTotalTemps,
                CallArgumentStatisticsData.argTotalLclVar,
                CallArgumentStatisticsData.argTotalDeferred,
                CallArgumentStatisticsData.argTotalConst,
                CallArgumentStatisticsData.argTotalObjPtr,
                CallArgumentStatisticsData.argMaxTempsPerMethod) = saved;
        }
    }

    private static void WithCompiler(Action<Compiler> action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.isValueClass = &False;
        vtable.Base.Base.canAllocateOnStack = &False;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        using var tls = TestConfiguration.CreateJitTls(&jitInfo);

        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compCompHnd = &jitInfo;
        compiler.eeInfoInitialized = true;
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
    private static byte False(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* clsHnd) => 0;
}
