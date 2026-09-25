// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if DEBUG
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.CorJitResult;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regMask;

using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterGroupDiagnosticsTests
{
    [Test]
    public static void LabelUsesUnsignedMethodAndGroupNumbers()
    {
        var emitter = CreateEmitter(out var compiler);
        compiler.compMethodID = -1;
        var group = new insGroup();
        group.InitializeNum(uint.MaxValue);

        Assert.That(emitter.emitLabelString(group), Is.EqualTo("G_M4294967295_IG4294967295"));
        Assert.That(Capture(() => emitter.emitPrintLabel(group)), Is.EqualTo("G_M4294967295_IG4294967295"));
    }

    [Test]
    public static void GroupFlagsFollowNativeDisplayOrder()
    {
        var emitter = CreateEmitter(out _);
        var flags =
            InsGroupFlags.GCVars | InsGroupFlags.ByrefRegs | InsGroupFlags.Prolog |
            InsGroupFlags.Epilog | InsGroupFlags.FuncletProlog | InsGroupFlags.FuncletEpilog |
            InsGroupFlags.NoGCInterrupt | InsGroupFlags.UpdatedInstructionSize |
            InsGroupFlags.Extend | InsGroupFlags.HasAlign | InsGroupFlags.Placeholder;

        var output = Capture(() => emitter.emitDispIGflags(flags));

        Assert.That(output, Is.EqualTo(
            ", gcvars, byref, prolog, epilog, funclet prolog, funclet epilog, nogc, isz, extend, align"));
    }

    [Test]
    public static void ConvertedTrackedVariablesArePrintedInLocalNumberOrder()
    {
        _ = CreateEmitter(out var compiler);
        compiler.lvaCount = 10;
        compiler.lvaTrackedCount = 3;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        compiler.lvaTrackedToVarNum = [8, 2, 5];
        var vars = VarSetOps.MakeEmpty(compiler);
        VarSetOps.AddElemD(compiler, vars, 0);
        VarSetOps.AddElemD(compiler, vars, 1);

        var output = Capture(() => dumpConvertedVarSet(compiler, vars));

        Assert.That(output, Is.EqualTo("{V02 V08}"));
    }

    [Test]
    public static void OrdinaryGroupReportsNativeMetadataAndFlags()
    {
        var emitter = CreateEmitter(out var compiler);
        compiler.compMethodID = 7;
        compiler.compCodeGenDone = true;
        compiler.verbose = true;
        compiler.lvaCount = 9;
        compiler.lvaTrackedCount = 3;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        compiler.lvaTrackedToVarNum = [8, 2, 5];
        var group = new insGroup
        {
            igFlags = InsGroupFlags.GCVars | InsGroupFlags.ByrefRegs,
            igOffs = 42,
            igSize = 5,
            igFuncIdx = 3,
            igWeight = 100,
            igPerfScore = 1.25,
            igGCregs = SRBM_RAX,
            SavedByrefRegs = (uint)SRBM_RCX,
        };
        group.InitializeNum(2);
        group.SavedGcVars = VarSetOps.MakeEmpty(compiler);
        VarSetOps.AddElemD(compiler, group.SavedGcVars, 0);
        VarSetOps.AddElemD(compiler, group.SavedGcVars, 1);
        emitter.emitCurIG = group;

        var output = Capture(() => emitter.emitDispIG(group, displayFunc: true));

        Assert.That(output, Is.EqualTo(
            "G_M007_IG02:        ; func=03, offs=0x00002A, size=0x0005, bbWeight=1, " +
            "PerfScore 1.25, gcVars=0000000000000003 {V02 V08}, " +
            "gcrefRegs=0001 {rax}, byrefRegs=0002 {rcx}, gcvars, byref <-- Current IG" +
            Environment.NewLine));
    }

    [Test]
    public static void PlaceholderGroupReportsPreviousAndInitialGCStates()
    {
        var emitter = CreateEmitter(out var compiler);
        compiler.compMethodID = 42;
        compiler.lvaCount = 4;
        compiler.lvaTrackedCount = 2;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        compiler.lvaTrackedToVarNum = [3, 1];
        var previousVars = VarSetOps.MakeEmpty(compiler);
        VarSetOps.AddElemD(compiler, previousVars, 0);
        var initialVars = VarSetOps.MakeEmpty(compiler);
        VarSetOps.AddElemD(compiler, initialVars, 1);
        var next = new insGroup();
        next.InitializeNum(9);
        var placeholder = new insGroup
        {
            igFlags = InsGroupFlags.Placeholder | InsGroupFlags.FuncletEpilog,
            igPhData = new insPlaceholderGroupData
            {
                igPhNext = next,
                igPhType = insGroupPlaceholderType.IGPT_FUNCLET_EPILOG,
                igPhPrevGCrefVars = previousVars,
                igPhPrevGCrefRegs = new regMaskTP(SRBM_RAX),
                igPhPrevByrefRegs = new regMaskTP(SRBM_RCX),
                igPhInitGCrefVars = initialVars,
                igPhInitGCrefRegs = new regMaskTP(SRBM_RDX),
                igPhInitByrefRegs = new regMaskTP(SRBM_NONE),
            },
        };
        placeholder.InitializeNum(4);
        emitter.emitCurIG = placeholder;
        FirstPlaceholder(emitter) = placeholder;
        LastPlaceholder(emitter) = placeholder;

        var output = Capture(() => emitter.emitDispIG(placeholder));
        var indentation = new string(' ', "G_M042_IG04:        ".Length);

        Assert.That(output, Is.EqualTo(
            "G_M042_IG04:        ; funclet epilog placeholder, next placeholder=IG09 " +
            ", funclet epilog <-- Current IG <-- First placeholder <-- Last placeholder" +
            Environment.NewLine +
            indentation + ";   PrevGCVars=0000000000000001 {V03}, PrevGCrefRegs=0001 {rax}, " +
            "PrevByrefRegs=0002 {rcx}" + Environment.NewLine +
            indentation + ";   InitGCVars=0000000000000002 {V01}, InitGCrefRegs=0004 {rdx}, " +
            "InitByrefRegs=0000 {}" + Environment.NewLine));
    }

    [Test]
    public static void NonVerboseExtendedGroupOmitsOffsetsGCRegistersAndBlockList()
    {
        var emitter = CreateEmitter(out var compiler);
        compiler.compMethodID = 5;
        var group = new insGroup
        {
            igFlags = InsGroupFlags.Extend | InsGroupFlags.ByrefRegs,
            igOffs = 64,
            igWeight = 50,
            igGCregs = SRBM_RAX,
        };
        group.InitializeNum(1);
        group.igBlocks.Add(new BasicBlock(null, null));

        var output = Capture(() => emitter.emitDispIG(group, displayFunc: true, displayLocation: false));

        Assert.That(output, Is.EqualTo(
            "G_M005_IG01:        ; bbWeight=0.50, byrefRegs=0000 {}, byref, extend" +
            Environment.NewLine));
    }

    [Test]
    public static void InstructionDisassemblyRejectsBeforePrintingAnyGroup()
    {
        var emitter = CreateEmitter(out _);
        var group = new insGroup();

        var output = Capture(() =>
        {
            var error = Assert.Throws<FatalJitException>(() =>
                emitter.emitDispIG(group, displayInstructions: true));
            Assert.That(error?.Result, Is.EqualTo(CORJIT_SKIPPED));

            error = Assert.Throws<FatalJitException>(() =>
                emitter.emitDispIGlist(displayInstructions: true));
            Assert.That(error?.Result, Is.EqualTo(CORJIT_SKIPPED));
        });

        Assert.That(output, Is.Empty);
    }

    [Test]
    public static void GroupListMarksOnlyFuncletTransitions()
    {
        var emitter = CreateEmitter(out var compiler);
        compiler.verbose = true;
        var first = new insGroup { igFuncIdx = 0, igWeight = 100 };
        var second = new insGroup { igFuncIdx = 0, igWeight = 100 };
        var third = new insGroup { igFuncIdx = 1, igWeight = 100 };
        first.igNext = second;
        second.igNext = third;
#if EMIT_BACKWARDS_NAVIGATION
        second.igPrev = first;
        third.igPrev = second;
#endif
        FirstGroup(emitter) = first;

        var output = Capture(() => emitter.emitDispIGlist());

        Assert.That(output.Split("func=00", StringSplitOptions.None), Has.Length.EqualTo(2));
        Assert.That(output.Split("func=01", StringSplitOptions.None), Has.Length.EqualTo(2));
        Assert.That(output.Split("gcrefRegs=", StringSplitOptions.None), Has.Length.EqualTo(4));
    }

    private static Emitter CreateEmitter(out Compiler compiler)
    {
        compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var emitter = new CodeGen(compiler).Emitter;
        emitter.emitBegCG(compiler, default);
        return emitter;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitPlaceholderList")]
    private static extern ref insGroup? FirstPlaceholder(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitIGlist")]
    private static extern ref insGroup? FirstGroup(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitPlaceholderLast")]
    private static extern ref insGroup? LastPlaceholder(Emitter emitter);

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
