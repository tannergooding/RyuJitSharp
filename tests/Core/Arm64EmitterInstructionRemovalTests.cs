// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static unsafe class Arm64EmitterInstructionRemovalTests
{
    [Test]
    public static void LoadStoreOptimizationReturnsFalseWithoutMatchingPreviousInstruction()
    {
        var (_, emitter) = CreateEmitter();

        Assert.That(TryOptimizeLdrStr(emitter), Is.False);
        Assert.That(LastInstruction(emitter), Is.Null);

        emitter.emitIns(INS_nop);
        var previousInstruction = LastInstruction(emitter);

        Assert.That(TryOptimizeLdrStr(emitter), Is.False);
        Assert.That(LastInstruction(emitter), Is.SameAs(previousInstruction));
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RemovingTheLastInstructionUpdatesCurrentOrSavedStorage(bool saveBeforeRemoval)
    {
        var (compiler, emitter) = CreateEmitter();
        emitter.emitIns(INS_nop);
        var group = emitter.emitCurIG ?? throw new AssertionException("Missing instruction group.");
        var instructionCount = InstructionCount(emitter);

        if (saveBeforeRemoval)
        {
            _ = SaveGroup(emitter, emitAdd: false);
        }

#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previousCompiler = JitTls.Compiler;
        JitTls.Compiler = compiler;
        try
        {
            RemoveLastInstruction(emitter);
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
        }

        Assert.That(InstructionCount(emitter), Is.EqualTo(instructionCount - 1));
        Assert.That(LastInstruction(emitter), Is.Null);
        Assert.That(LastInstructionGroup(emitter), Is.Null);
        Assert.That(group.igFlags & InsGroupFlags.HasRemovedInstruction, Is.EqualTo(InsGroupFlags.HasRemovedInstruction));

        if (saveBeforeRemoval)
        {
            Assert.That(group.igInsCnt, Is.Zero);
            Assert.That(group.igSize, Is.Zero);
            Assert.That(group.igData, Is.Empty);
        }
        else
        {
            Assert.That(CurrentBuffer(emitter), Is.Empty);
            Assert.That(CurrentBufferOffset(emitter), Is.EqualTo((nuint)0));
            Assert.That(CurrentInstructionCount(emitter), Is.Zero);
            Assert.That(CurrentGroupSize(emitter), Is.Zero);
        }
    }

    private static (Compiler Compiler, Emitter Emitter) CreateEmitter()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        var emitter = new Emitter(new CodeGen(compiler));
        emitter.emitBegCG(compiler, default);
        emitter.Init();
        emitter.emitBegFN(false
#if DEBUG
            , true
#endif
            );

        return (compiler, emitter);
    }

    private static bool TryOptimizeLdrStr(Emitter emitter)
    {
        return OptimizeLdrStr(emitter, INS_ldr, EA_8BYTE, REG_R0, REG_SP, 0, EA_8BYTE, IF_LS_2A, false, -1, -1
#if DEBUG
            , false
#endif
        );
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "OptimizeLdrStr")]
    private static extern bool OptimizeLdrStr(Emitter emitter, instruction ins, emitAttr reg1Attr, regNumber reg1,
        regNumber reg2, nint imm, emitAttr size, Emitter.insFormat fmt, bool localVar, int varx, int offs
#if DEBUG
        , bool useRsvdReg
#endif
    );

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitRemoveLastInstruction")]
    private static extern void RemoveLastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitSavIG")]
    private static extern insGroup SaveGroup(Emitter emitter, bool emitAdd);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitInsCount")]
    private static extern ref int InstructionCount(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref System.Collections.Generic.List<Emitter.instrDesc>? CurrentBuffer(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeNext")]
    private static extern ref nuint CurrentBufferOffset(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGinsCnt")]
    private static extern ref int CurrentInstructionCount(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int CurrentGroupSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastInsIG")]
    private static extern ref insGroup? LastInstructionGroup(Emitter emitter);
}
#endif
