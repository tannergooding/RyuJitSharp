// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class EmitterSharedDiagnosticsAndCostTests
{
    [ThreadStatic]
    private static MemoryStream? t_captureStream;

    [Test]
    public static void FormatNamesIncludeEveryTargetFormat()
    {
        for (uint format = 0; format < (uint)Emitter.insFormat.IF_COUNT; format++)
        {
            Assert.That(Emitter.emitIfName(format), Is.EqualTo(((Emitter.insFormat)format).ToString()));
        }
    }

    [TestCase(uint.MaxValue, "??4294967295??")]
    [TestCase(0x80000000u, "??2147483648??")]
    public static void InvalidFormatNamesKeepUnsignedDecimalWidth(uint format, string expected)
    {
        Assert.That(Emitter.emitIfName(format), Is.EqualTo(expected));
        Assert.That(Emitter.emitIfName(Emitter.insFormat.IF_COUNT),
            Is.EqualTo($"??{(uint)Emitter.insFormat.IF_COUNT}??"));
    }

    [TestCase(0u, true, "000000")]
    [TestCase(42u, true, "00002A")]
    [TestCase(0xFFFFFFu, true, "FFFFFF")]
    [TestCase(0x1000000u, true, "1000000")]
    [TestCase(uint.MaxValue, true, "FFFFFFFF")]
    [TestCase(uint.MaxValue, false, "      ")]
    public static void InstructionOffsetsUseMinimumWidthWithoutTruncation(uint offset, bool display, string expected)
    {
        var emitter = new View(CreateCompiler());
        Assert.That(Capture(() => DisplayOffset(emitter, offset, display)), Is.EqualTo(expected));
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void InstructionAddressesPreserveRawHostPointerFormatting(bool display, bool diffable)
    {
        var compiler = CreateCompiler();
#if DEBUG
        compiler.opts.disAddr = display;
#endif
        compiler.opts.disDiffable = diffable;
        var emitter = new View(compiler);
#if HOST_64BIT
        var address = unchecked((nuint)0x12345678ABCDEF01ul);
#if DEBUG
        const string formatted = " 12345678`abcdef01 ";
#endif
#else
        var address = unchecked((nuint)0xABCDEF01u);
#if DEBUG
        const string formatted = " abcdef01 ";
#endif
#endif
#if DEBUG
        var expected = display ? formatted : "";
#else
        const string expected = "";
#endif
        Assert.That(Capture(() => DisplayAddress(emitter, (byte*)address)), Is.EqualTo(expected));
    }

#if DEBUG || LATE_DISASM
    [TestCase(0.25f, 0.0f, Emitter.PerfScoreMemoryAccessKind.None, 0.25f, 0.25f)]
    [TestCase(0.25f, 0.75f, Emitter.PerfScoreMemoryAccessKind.Read, 0.75f, 0.75f)]
    [TestCase(0.25f, 1.0f, Emitter.PerfScoreMemoryAccessKind.None, 0.25f, 0.25f)]
    [TestCase(0.25f, 1.25f, Emitter.PerfScoreMemoryAccessKind.Read, 0.25f, 0.25f)]
    [TestCase(0.25f, 2.0f, Emitter.PerfScoreMemoryAccessKind.Write, 0.25f, 1.0f)]
    [TestCase(0.25f, 3.0f, Emitter.PerfScoreMemoryAccessKind.Write, 0.25f, 2.0f)]
    [TestCase(0.25f, 4.0f, Emitter.PerfScoreMemoryAccessKind.Write, 1.0f, 3.0f)]
    [TestCase(0.25f, 6.0f, Emitter.PerfScoreMemoryAccessKind.ReadWrite, 3.0f, 5.0f)]
    [TestCase(8.0f, 6.0f, Emitter.PerfScoreMemoryAccessKind.ReadWrite, 8.0f, 8.0f)]
    public static void CostAppliesNativeSpeculationAndTargetWriteLatency(float throughput, float latency,
        Emitter.PerfScoreMemoryAccessKind memoryAccess, float xarchCost, float otherCost)
    {
        var characteristics = new Emitter.insExecutionCharacteristics
        {
            insThroughput = throughput,
            insLatency = latency,
            insMemoryAccessKind = memoryAccess,
        };
#if TARGET_XARCH
        var expected = xarchCost;
#else
        var expected = otherCost;
#endif
        Assert.That(Emitter.insEvaluateExecutionCost(characteristics), Is.EqualTo(expected));
    }

    [TestCase(Emitter.PerfScoreMemoryAccessKind.None)]
    [TestCase(Emitter.PerfScoreMemoryAccessKind.Read)]
    [TestCase(Emitter.PerfScoreMemoryAccessKind.Write)]
    [TestCase(Emitter.PerfScoreMemoryAccessKind.ReadWrite)]
    public static void EqualZeroCostsRetainThroughputSignLikeNativeMax(Emitter.PerfScoreMemoryAccessKind memoryAccess)
    {
        var characteristics = new Emitter.insExecutionCharacteristics
        {
            insThroughput = BitConverter.Int32BitsToSingle(int.MinValue),
            insLatency = 0.0f,
            insMemoryAccessKind = memoryAccess,
        };
        Assert.That(BitConverter.SingleToInt32Bits(Emitter.insEvaluateExecutionCost(characteristics)),
            Is.EqualTo(int.MinValue));
    }

#if !TARGET_XARCH
    [Test]
    public static void UnportedCharacteristicsTerminateBeforeCostEvaluation()
    {
        var emitter = new View(CreateCompiler());
        _ = Assert.Throws<FatalJitException>(() => emitter.insEvaluateExecutionCost(View.Descriptor()));
    }
#endif

#if DEBUG
    [Test]
    public static void UnhandledInstructionPrintsBeforeAssertionAndOnlyDefaultsCosts()
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        var context = new AssertionContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
        using var tls = new JitTls(&context.JitInfo);
        var emitter = new View(CreateCompiler());
        var id = View.Descriptor();
        var result = new Emitter.insExecutionCharacteristics
        {
            insThroughput = -2.0f,
            insLatency = -3.0f,
            insMemoryAccessKind = Emitter.PerfScoreMemoryAccessKind.ReadWrite,
        };

        var text = Capture(() => UnhandledInstruction(emitter, id, ref result));

        Assert.That(text, Is.EqualTo("PerfScore: unhandled instruction: nop, format IF_NONE"));
        Assert.That(context.Assertions, Is.EqualTo(1));
        Assert.That(context.CostsPrintedBeforeAssertion, Is.True);
        Assert.That(result.insThroughput, Is.EqualTo(1.0f));
        Assert.That(result.insLatency, Is.EqualTo(1.0f));
        Assert.That(result.insMemoryAccessKind, Is.EqualTo(Emitter.PerfScoreMemoryAccessKind.ReadWrite));
    }

    private struct AssertionContext
    {
        public ICorJitInfo JitInfo;
        public int Assertions;
        public bool CostsPrintedBeforeAssertion;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        var context = (AssertionContext*)self;
        context->Assertions++;
        context->CostsPrintedBeforeAssertion = t_captureStream is MemoryStream output && output.Length > 0;
        return 0;
    }
#else
    [Test]
    public static void UnhandledInstructionOnlyDefaultsCostsWithoutDebugOutput()
    {
        var emitter = new View(CreateCompiler());
        var result = new Emitter.insExecutionCharacteristics
        {
            insThroughput = -2.0f,
            insLatency = -3.0f,
            insMemoryAccessKind = Emitter.PerfScoreMemoryAccessKind.ReadWrite,
        };

        Assert.That(Capture(() => UnhandledInstruction(emitter, View.Descriptor(), ref result)), Is.Empty);
        Assert.That(result.insThroughput, Is.EqualTo(1.0f));
        Assert.That(result.insLatency, Is.EqualTo(1.0f));
        Assert.That(result.insMemoryAccessKind, Is.EqualTo(Emitter.PerfScoreMemoryAccessKind.ReadWrite));
    }
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "perfScoreUnhandledInstruction")]
    private static extern void UnhandledInstruction(Emitter emitter, Emitter.instrDesc id,
        ref Emitter.insExecutionCharacteristics result);
#endif

    private static Compiler CreateCompiler()
    {
        return (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
    }

    private sealed class View : Emitter
    {
        public View(Compiler compiler) : base(new CodeGen(compiler))
        {
            _compiler = compiler;
        }

        public static instrDesc Descriptor()
        {
            var id = new instrDescBasic();
            id.idIns(instruction.INS_nop);
            id.idInsFmt(insFormat.IF_NONE);
            return id;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitDispInsAddr")]
    private static extern void DisplayAddress(Emitter emitter, byte* address);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitDispInsOffs")]
    private static extern void DisplayOffset(Emitter emitter, uint offset, bool display);

    private static string Capture(Action action)
    {
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previous = s_jitstdout;
        var previousCapture = t_captureStream;
        try
        {
            s_jitstdout = writer;
            t_captureStream = stream;
            action();
            writer.Flush();
        }
        finally
        {
            s_jitstdout = previous;
            t_captureStream = previousCapture;
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
