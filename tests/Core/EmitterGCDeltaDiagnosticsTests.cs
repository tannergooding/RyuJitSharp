// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG && TARGET_AMD64
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.GCInfo.GCtype;
using static RyuJitSharp.GCInfo.rpdArgType_t;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regMask;

using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterGCDeltaDiagnosticsTests
{
    [TestCase(false, 28)]
    [TestCase(true, 7)]
    public static void RegisterDeltasIndentAndAdvanceTheirPreviousState(bool diffable, int indent)
    {
        var emitter = CreateEmitter(out var compiler);
        compiler.opts.disDiffable = diffable;
        CurrentGCrefRegs(emitter) = SRBM_RAX;

        var first = Capture(() => DisplayGCInfoDelta(emitter));
        var unchanged = Capture(() => DisplayGCInfoDelta(emitter));
        CurrentGCrefRegs(emitter) = SRBM_RCX;
        CurrentByrefRegs(emitter) = SRBM_RDX;
        var second = Capture(() => DisplayGCInfoDelta(emitter));

        var prefix = new string(' ', indent);
        Assert.That(first, Is.EqualTo($"{prefix}; gcrRegs +[rax]{Environment.NewLine}"));
        Assert.That(unchanged, Is.Empty);
        Assert.That(second, Is.EqualTo(
            $"{prefix}; gcrRegs -[rax] +[rcx]{Environment.NewLine}" +
            $"{prefix}; byrRegs +[rdx]{Environment.NewLine}"));
    }

    [Test]
    public static void VariableDeltasPrintConvertedLocalNumbersOnce()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.lvaCount = 6;
        compiler.lvaTrackedCount = 2;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        compiler.lvaTrackedToVarNum = [5, 2];
        var emitter = new CodeGen(compiler).Emitter;
        emitter.emitBegCG(compiler, default);
        emitter.Init();
        VarSetOps.AddElemD(compiler, PreviousVars(emitter), 0);
        VarSetOps.AddElemD(compiler, CurrentVars(emitter), 1);

        var first = Capture(() => DisplayGCVarDelta(emitter));
        var unchanged = Capture(() => DisplayGCVarDelta(emitter));

        Assert.That(first, Is.EqualTo(
            $"{new string(' ', 28)}; GC ptr vars -{{V05}} +{{V02}}{Environment.NewLine}"));
        Assert.That(unchanged, Is.Empty);
    }

    [Test]
    public static void OutgoingArgumentDeltasSkipRegisterEntriesAndAdvanceDescriptorCursor()
    {
        var emitter = CreateEmitter(out _);
        var outgoing = new GCInfo.regPtrDsc
        {
            rpdArg = true,
            rpdArgType = rpdARG_PUSH,
            rpdGCtype = GCT_BYREF,
        };
        outgoing.rpdCallData.rpdPtrArg = 4;
        var register = new GCInfo.regPtrDsc { rpdNext = outgoing };
        RegPtrList(ref emitter.GCInfo) = register;
        RegPtrLast(ref emitter.GCInfo) = outgoing;

        var first = Capture(() => DisplayGCInfoDelta(emitter));
        var unchanged = Capture(() => DisplayGCInfoDelta(emitter));
        var killed = new GCInfo.regPtrDsc
        {
            rpdArg = true,
            rpdArgType = rpdARG_KILL,
            rpdGCtype = GCT_GCREF,
        };
        killed.rpdCallData.rpdPtrArg = 2;
        outgoing.rpdNext = killed;
        RegPtrLast(ref emitter.GCInfo) = killed;
        var second = Capture(() => DisplayGCInfoDelta(emitter));

        var indent = new string(' ', 28);
        Assert.That(first, Is.EqualTo($"{indent}; byr arg write{Environment.NewLine}"));
        Assert.That(unchanged, Is.Empty);
        Assert.That(second, Is.EqualTo($"{indent}; gcr arg kill 2{Environment.NewLine}"));
    }

    private static Emitter CreateEmitter(out Compiler compiler)
    {
        compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var emitter = new CodeGen(compiler).Emitter;
        emitter.emitBegCG(compiler, default);
        emitter.Init();
        return emitter;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitDispGCInfoDelta")]
    private static extern void DisplayGCInfoDelta(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitDispGCVarDelta")]
    private static extern void DisplayGCVarDelta(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisGCrefRegs")]
    private static extern ref regMask CurrentGCrefRegs(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisByrefRegs")]
    private static extern ref regMask CurrentByrefRegs(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "debugPrevGCrefVars")]
    private static extern ref nint[] PreviousVars(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "debugThisGCrefVars")]
    private static extern ref nint[] CurrentVars(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "gcRegPtrList")]
    private static extern ref GCInfo.regPtrDsc? RegPtrList(ref GCInfo info);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "gcRegPtrLast")]
    private static extern ref GCInfo.regPtrDsc? RegPtrLast(ref GCInfo info);

    private static string Capture(Action action)
    {
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previous = s_jitstdout;
        try
        {
            s_jitstdout = writer;
            action();
            writer.Flush();
        }
        finally
        {
            s_jitstdout = previous;
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
#endif
