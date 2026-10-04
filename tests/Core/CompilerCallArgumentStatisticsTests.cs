// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

#if CALL_ARG_STATS
[NonParallelizable]
internal static unsafe class CompilerCallArgumentStatisticsTests
{
    [Test]
    public static void CallStatisticsCountTheLirCallKinds()
    {
        WithStatistics(() => WithCompiler(compiler => {
#if DEBUG
            compiler.fgSafeBasicBlockCreation = true;
#endif
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

            Assert.That(Compiler.argTotalCalls, Is.EqualTo(4));
            Assert.That(Compiler.argStaticCalls, Is.EqualTo(1));
            Assert.That(Compiler.argHelperCalls, Is.EqualTo(1));
            Assert.That(Compiler.argNonVirtualCalls, Is.EqualTo(1));
            Assert.That(Compiler.argVirtualCalls, Is.EqualTo(1));
            Assert.That(Compiler.argTotalObjPtr, Is.EqualTo(2));
            Assert.That(Compiler.argTotalArgs, Is.Zero);
            Assert.That(Compiler.argTotalRegArgs, Is.Zero);
        }));
    }

    [Test]
    public static void CallStatisticsReportPreservesNativeLabelsAndPercentages()
    {
        WithStatistics(() => {
            Compiler.genMethodCnt = 2;
            Compiler.argTotalCalls = 4;
            Compiler.argHelperCalls = 1;
            Compiler.argStaticCalls = 1;
            Compiler.argVirtualCalls = 1;
            Compiler.argNonVirtualCalls = 1;
            Compiler.argTotalArgs = 4;
            Compiler.argTotalDWordArgs = 1;
            Compiler.argTotalLongArgs = 1;
            Compiler.argTotalFloatArgs = 1;
            Compiler.argTotalDoubleArgs = 1;
            Compiler.argTotalRegArgs = 4;
            Compiler.argTotalTemps = 1;
            Compiler.argTotalLclVar = 1;
            Compiler.argTotalDeferred = 1;
            Compiler.argTotalConst = 1;
            Compiler.argTotalObjPtr = 2;
            Compiler.argMaxTempsPerMethod = 3;

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
            Compiler.genMethodCnt = 1;
            Compiler.argTotalCalls = uint.MaxValue;
            Compiler.argTotalArgs = 1;
            Compiler.argTotalRegArgs = 1;
            Compiler.argMaxTempsPerMethod = uint.MaxValue;

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
            Compiler.genMethodCnt,
            Compiler.argTotalCalls,
            Compiler.argHelperCalls,
            Compiler.argStaticCalls,
            Compiler.argNonVirtualCalls,
            Compiler.argVirtualCalls,
            Compiler.argTotalArgs,
            Compiler.argTotalDWordArgs,
            Compiler.argTotalLongArgs,
            Compiler.argTotalFloatArgs,
            Compiler.argTotalDoubleArgs,
            Compiler.argTotalRegArgs,
            Compiler.argTotalTemps,
            Compiler.argTotalLclVar,
            Compiler.argTotalDeferred,
            Compiler.argTotalConst,
            Compiler.argTotalObjPtr,
            Compiler.argMaxTempsPerMethod);

        try
        {
            Compiler.genMethodCnt = 1;
            Compiler.argTotalCalls = 0;
            Compiler.argHelperCalls = 0;
            Compiler.argStaticCalls = 0;
            Compiler.argNonVirtualCalls = 0;
            Compiler.argVirtualCalls = 0;
            Compiler.argTotalArgs = 0;
            Compiler.argTotalDWordArgs = 0;
            Compiler.argTotalLongArgs = 0;
            Compiler.argTotalFloatArgs = 0;
            Compiler.argTotalDoubleArgs = 0;
            Compiler.argTotalRegArgs = 0;
            Compiler.argTotalTemps = 0;
            Compiler.argTotalLclVar = 0;
            Compiler.argTotalDeferred = 0;
            Compiler.argTotalConst = 0;
            Compiler.argTotalObjPtr = 0;
            Compiler.argMaxTempsPerMethod = 0;

            action();
        }
        finally
        {
            (Compiler.genMethodCnt,
                Compiler.argTotalCalls,
                Compiler.argHelperCalls,
                Compiler.argStaticCalls,
                Compiler.argNonVirtualCalls,
                Compiler.argVirtualCalls,
                Compiler.argTotalArgs,
                Compiler.argTotalDWordArgs,
                Compiler.argTotalLongArgs,
                Compiler.argTotalFloatArgs,
                Compiler.argTotalDoubleArgs,
                Compiler.argTotalRegArgs,
                Compiler.argTotalTemps,
                Compiler.argTotalLclVar,
                Compiler.argTotalDeferred,
                Compiler.argTotalConst,
                Compiler.argTotalObjPtr,
                Compiler.argMaxTempsPerMethod) = saved;
        }
    }

    private static void WithCompiler(Action<Compiler> action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.isValueClass = &False;
        vtable.Base.Base.canAllocateOnStack = &False;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
#if DEBUG
        using var tls = new JitTls(&jitInfo);
#endif

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
#endif
