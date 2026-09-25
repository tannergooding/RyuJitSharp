// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterStackMoveElisionTests
{
    private static readonly JitFlags[] s_jitFlags = GC.AllocateArray<JitFlags>(1, pinned: true);

    [TestCase(INS_mov, EA_8BYTE, false, false, false)]
    [TestCase(INS_mov, EA_4BYTE, false, false, true)]
    [TestCase(INS_movaps, EA_16BYTE, false, false, false)]
    [TestCase(INS_movaps, EA_16BYTE, true, false, true)]
    [TestCase(INS_movaps, EA_32BYTE, true, false, false)]
    [TestCase(INS_movaps, EA_32BYTE, true, true, true)]
    [TestCase(INS_movaps, EA_64BYTE, true, true, false)]
    [TestCase(INS_vmovdqu8, EA_16BYTE, true, true, true)]
    [TestCase(INS_vmovdqa64, EA_64BYTE, true, true, false)]
    [TestCase(INS_movss, EA_4BYTE, false, false, false)]
    [TestCase(INS_movss, EA_4BYTE, true, false, true)]
    [TestCase(INS_movd32, EA_4BYTE, false, false, true)]
    [TestCase(INS_movd64, EA_8BYTE, true, false, true)]
    [TestCase(INS_vmovsh, EA_2BYTE, true, true, true)]
    [TestCase(INS_movq, EA_8BYTE, false, false, true)]
    [TestCase(INS_movsxd, EA_8BYTE, false, false, true)]
    [TestCase(INS_movzx, EA_1BYTE, false, false, true)]
    [TestCase(INS_kmovd_msk, EA_4BYTE, true, true, true)]
    [TestCase(INS_kmovq_msk, EA_8BYTE, true, true, false)]
    [TestCase(INS_kmovq_gpr, EA_8BYTE, true, true, true)]
    public static void MoveSideEffectsRetainUpperBitAndRegisterClassSemantics(
        instruction ins, emitAttr size, bool vex, bool evex, bool expected)
    {
        var emitter = CreateEmitter(ins, size, IF_SWR_RRD);
        emitter.UseVexEncodings = vex;
        emitter.UseEvexEncodings = evex;

        Assert.That(Emitter.IsMovInstruction(ins), Is.True);
        Assert.That(emitter.HasSideEffect(ins, size), Is.EqualTo(expected));
    }

    [TestCase(INS_add)]
    [TestCase(INS_lea)]
    [TestCase(INS_cmovo)]
    [TestCase(INS_movsb)]
    public static void NonElidableMoveLikeInstructionsRemainOutsideTheClassification(instruction ins)
    {
        Assert.That(Emitter.IsMovInstruction(ins), Is.False);
    }

    [TestCase(IF_SWR_RRD, IF_SWR_RRD, EA_8BYTE, true)]
    [TestCase(IF_RWR_SRD, IF_RWR_SRD, EA_4BYTE, true)]
    [TestCase(IF_SWR_RRD, IF_RWR_SRD, EA_8BYTE, true)]
    [TestCase(IF_RWR_SRD, IF_SWR_RRD, EA_8BYTE, true)]
    [TestCase(IF_SWR_RRD, IF_RWR_SRD, EA_4BYTE, false)]
    [TestCase(IF_RWR_SRD, IF_SWR_RRD, EA_4BYTE, false)]
    [TestCase(IF_SWR_RRD, IF_SWR_RRD, EA_GCREF, false)]
    [TestCase(IF_RWR_SRD, IF_RWR_SRD, EA_BYREF, false)]
    public static void RepeatedAndReversedMovesRespectSideEffectsAndGcBirth(
        Emitter.insFormat previous, Emitter.insFormat current, emitAttr size, bool expected)
    {
        var emitter = CreateEmitter(INS_mov, size, previous);
        Assert.That(emitter.IsRedundantStackMov(INS_mov, current, size, REG_RAX, 7, 3), Is.EqualTo(expected));
    }

    [Test]
    public static void OperandIdentityAndInstructionKindMustMatch()
    {
        var emitter = CreateEmitter(INS_mov, EA_8BYTE, IF_SWR_RRD);

        Assert.That(emitter.IsRedundantStackMov(INS_mov, IF_SWR_RRD, EA_8BYTE, REG_RCX, 7, 3), Is.False);
        Assert.That(emitter.IsRedundantStackMov(INS_mov, IF_SWR_RRD, EA_8BYTE, REG_RAX, 8, 3), Is.False);
        Assert.That(emitter.IsRedundantStackMov(INS_mov, IF_SWR_RRD, EA_8BYTE, REG_RAX, 7, 4), Is.False);
        Assert.That(emitter.IsRedundantStackMov(INS_mov, IF_SWR_RRD, EA_4BYTE, REG_RAX, 7, 3), Is.False);
        Assert.That(emitter.IsRedundantStackMov(INS_movq, IF_SWR_RRD, EA_8BYTE, REG_RAX, 7, 3), Is.False);

        var previous = LastInstruction(emitter) ?? throw new AssertionException("Missing previous instruction.");
        previous.idInsFmt(IF_RWR_RRD);
        Assert.That(emitter.IsRedundantStackMov(INS_mov, IF_SWR_RRD, EA_8BYTE, REG_RAX, 7, 3), Is.False);
    }

    [TestCase(false, false, false, false, false)]
    [TestCase(false, true, false, false, true)]
    [TestCase(false, true, true, false, false)]
    [TestCase(false, true, false, true, false)]
    [TestCase(false, true, true, true, true)]
    [TestCase(true, false, false, false, true)]
    public static void GroupCrossingRequiresAnExtensionAndMatchingGcInterruptibility(
        bool sameGroup, bool extension, bool previousNoGc, bool currentNoGc, bool expected)
    {
        var emitter = CreateEmitter(INS_mov, EA_8BYTE, IF_SWR_RRD);
        var previous = LastGroup(emitter) ?? throw new AssertionException("Missing previous group.");
        previous.igFlags = previousNoGc ? InsGroupFlags.NoGCInterrupt : InsGroupFlags.None;
        var current = sameGroup ? previous : new insGroup();
        current.igFlags = (extension ? InsGroupFlags.Extend : InsGroupFlags.None) |
            (currentNoGc ? InsGroupFlags.NoGCInterrupt : InsGroupFlags.None);
        emitter.emitCurIG = current;

        Assert.That(emitter.IsRedundantStackMov(INS_mov, IF_SWR_RRD, EA_8BYTE, REG_RAX, 7, 3), Is.EqualTo(expected));
        ForceNewGroup(emitter) = true;
        Assert.That(emitter.IsRedundantStackMov(INS_mov, IF_SWR_RRD, EA_8BYTE, REG_RAX, 7, 3), Is.False);
    }

    [Test]
    public static void MinoptsAndAbsentPreviousInstructionsNeverElideMoves()
    {
        var emitter = CreateEmitter(INS_mov, EA_8BYTE, IF_SWR_RRD, minopts: true);
        Assert.That(emitter.IsRedundantStackMov(INS_mov, IF_SWR_RRD, EA_8BYTE, REG_RAX, 7, 3), Is.False);

        emitter = CreateEmitter(INS_mov, EA_8BYTE, IF_SWR_RRD);
        LastInstruction(emitter) = null;
        LastGroup(emitter) = null;
        Assert.That(emitter.IsRedundantStackMov(INS_mov, IF_SWR_RRD, EA_8BYTE, REG_RAX, 7, 3), Is.False);
    }

    private static Emitter CreateEmitter(instruction ins, emitAttr size, Emitter.insFormat format, bool minopts = false)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.opts.jitFlags = (JitFlags*)Unsafe.AsPointer(ref s_jitFlags[0]);
        compiler.opts.SetMinOpts(minopts);
        var emitter = new CodeGen(compiler).Emitter;
        emitter.emitBegCG(compiler, default);
        var type = typeof(Emitter).GetNestedType("instrDescBasic", BindingFlags.NonPublic)
            ?? throw new AssertionException("Missing basic instruction descriptor.");
        var descriptor = (Emitter.instrDesc)(Activator.CreateInstance(type, nonPublic: true)
            ?? throw new AssertionException("Could not create a basic instruction descriptor."));
        descriptor.idIns(ins);
        descriptor.idInsFmt(format);
        descriptor.idOpSize(size is EA_GCREF or EA_BYREF ? EA_8BYTE : size);
        descriptor.idReg1(REG_RAX);
        descriptor.idAddr().iiaLclVar.initLclVarAddr(7, 3);
        emitter.emitCurIG = new insGroup();
        LastInstruction(emitter) = descriptor;
        LastGroup(emitter) = emitter.emitCurIG;
        return emitter;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastInsIG")]
    private static extern ref insGroup? LastGroup(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitForceNewIG")]
    private static extern ref bool ForceNewGroup(Emitter emitter);
}
