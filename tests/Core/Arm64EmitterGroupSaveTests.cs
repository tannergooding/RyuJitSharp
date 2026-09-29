// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64 || EMITTER_STATS
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.instruction;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64EmitterGroupSaveTests
{
#if TARGET_ARMARCH
    [Test]
    public static void Arm64SaveClearsBarrierWithoutMarkingBranchRemovable()
    {
        var emitter = CreateEmitter();
        var group = new insGroup();
        var branch = SaveEmitter.Basic(INS_b);
        Prepare(emitter, group, branch, 8);
        LastBarrier(emitter) = branch;
        LastInstruction(emitter) = branch;
        LastInstructionGroup(emitter) = group;

        Assert.That(Save(emitter, false), Is.SameAs(group));
        Assert.That(LastBarrier(emitter), Is.Null);
        Assert.That(group.igFlags & InsGroupFlags.GCVars, Is.EqualTo(InsGroupFlags.GCVars));
        Assert.That(group.igFlags & InsGroupFlags.ByrefRegs, Is.EqualTo(InsGroupFlags.ByrefRegs));
        Assert.That(group.igFlags & InsGroupFlags.HasRemovableJump, Is.EqualTo(InsGroupFlags.None));
        Assert.That(group.igData, Has.Length.EqualTo(1));
        Assert.That(group.igData![0], Is.SameAs(branch));
        Assert.That(Buffer(emitter), Is.Empty);
        Assert.That(Used(emitter), Is.EqualTo((nuint)0));
        Assert.That(LastInstruction(emitter), Is.SameAs(branch));
        Assert.That(LastInstructionGroup(emitter), Is.SameAs(group));
#if EMIT_BACKWARDS_NAVIGATION
        Assert.That(group.igLastIns, Is.SameAs(branch));
#endif
    }
#endif

#if EMITTER_STATS
    [Test]
    public static void SaveAccumulatesNativeWidthCountersAndPrologMaxima()
    {
        var oldPointers = TotalPointers(null);
        var oldInstructions = TotalInstructions(null);
        var oldSize = TotalSize(null);
        var oldMethodSize = MethodSize(null);
        var oldPrologInstructions = PrologInstructions(null);
        var oldPrologSize = PrologSize(null);
        var oldMaxPrologInstructions = MaxPrologInstructions(null);
        var oldMaxPrologSize = MaxPrologSize(null);

        try
        {
            TotalPointers(null) = 0;
            TotalInstructions(null) = 0;
            TotalSize(null) = 0;
            MethodSize(null) = 0;
            PrologInstructions(null) = 0;
            PrologSize(null) = 0;
            MaxPrologInstructions(null) = 0;
            MaxPrologSize(null) = 0;

            var emitter = CreateEmitter();
            ForceGcState(emitter) = true;

            Prepare(emitter, new insGroup { igFlags = InsGroupFlags.Prolog }, SaveEmitter.Basic(INS_nop), 8);
            _ = Save(emitter, false);
            Prepare(emitter, new insGroup { igFlags = InsGroupFlags.Prolog }, SaveEmitter.Basic(INS_nop), 16);
            _ = Save(emitter, false);
            Prepare(emitter, new insGroup(), SaveEmitter.Basic(INS_nop), 8);
            _ = Save(emitter, false);

            Assert.That(TotalPointers(null), Is.EqualTo(3u));
            Assert.That(TotalInstructions(null), Is.EqualTo(3u));
            Assert.That(TotalSize(null), Is.EqualTo((nuint)32));
            Assert.That(MethodSize(null), Is.EqualTo((nuint)32));
            Assert.That(PrologInstructions(null), Is.EqualTo(2u));
            Assert.That(PrologSize(null), Is.EqualTo((nuint)24));
            Assert.That(MaxPrologInstructions(null), Is.EqualTo(2u));
            Assert.That(MaxPrologSize(null), Is.EqualTo((nuint)24));
        }
        finally
        {
            TotalPointers(null) = oldPointers;
            TotalInstructions(null) = oldInstructions;
            TotalSize(null) = oldSize;
            MethodSize(null) = oldMethodSize;
            PrologInstructions(null) = oldPrologInstructions;
            PrologSize(null) = oldPrologSize;
            MaxPrologInstructions(null) = oldMaxPrologInstructions;
            MaxPrologSize(null) = oldMaxPrologSize;
        }
    }
#endif

    private static SaveEmitter CreateEmitter()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var emitter = new SaveEmitter(new CodeGen(compiler));
        emitter.emitBegCG(compiler, default);
        emitter.Init();
        return emitter;
    }

    private static void Prepare(SaveEmitter emitter, insGroup group, Emitter.instrDesc descriptor, nuint size)
    {
        emitter.emitCurIG = group;
        descriptor.StorageGroup = group;
        descriptor.StorageIndex = 0;
        descriptor.StorageOffset = 0;
        descriptor.StorageSize = size;
        Buffer(emitter) = [descriptor];
        Used(emitter) = size;
        Capacity(emitter) = size;
        InstructionCount(emitter) = 1;
        ForceGcState(emitter) = true;
        LastInstruction(emitter) = null;
        LastInstructionGroup(emitter) = null;
    }

    private sealed class SaveEmitter(CodeGen codeGen) : Emitter(codeGen)
    {
        public static instrDesc Basic(instruction ins)
        {
            var descriptor = new instrDescBasic();
            descriptor.idIns(ins);
            return descriptor;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitSavIG")]
    private static extern insGroup Save(Emitter emitter, bool add);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? Buffer(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeNext")]
    private static extern ref nuint Used(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeEndp")]
    private static extern ref nuint Capacity(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGinsCnt")]
    private static extern ref int InstructionCount(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitForceStoreGCState")]
    private static extern ref bool ForceGcState(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastInsIG")]
    private static extern ref insGroup? LastInstructionGroup(Emitter emitter);

#if TARGET_ARMARCH
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastMemBarrier")]
    private static extern ref Emitter.instrDesc? LastBarrier(Emitter emitter);
#endif

#if EMITTER_STATS
    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitTotalIGptrs")]
    private static extern ref uint TotalPointers(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitTotalIGicnt")]
    private static extern ref uint TotalInstructions(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitTotalIGsize")]
    private static extern ref nuint TotalSize(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitSizeMethod")]
    private static extern ref nuint MethodSize(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitCurPrologInsCnt")]
    private static extern ref uint PrologInstructions(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitCurPrologIGSize")]
    private static extern ref nuint PrologSize(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitMaxPrologInsCnt")]
    private static extern ref uint MaxPrologInstructions(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitMaxPrologIGSize")]
    private static extern ref nuint MaxPrologSize(Emitter? emitter);
#endif
}
#endif
