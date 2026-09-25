// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class EmitterJumpDistanceBindingTests
{
    [TestCase(INS_jmp, 124, 2u)]
    [TestCase(INS_jmp, 125, 5u)]
    [TestCase(INS_jne, 123, 2u)]
    [TestCase(INS_jne, 124, 6u)]
    public static void ForwardBoundaryUsesSmallEncodingOffset(
        instruction instruction, int padding, uint expectedSize)
    {
        WithEmitter((_, emitter) =>
        {
            var label = Label();
            var source = emitter.emitCurIG ?? throw new AssertionException("Missing source group.");
            emitter.emitIns_J(instruction, label);
            var jump = Last(emitter);
            EmitNops(emitter, padding);
            var target = emitter.emitAddInlineLabel();
            label.bbEmitCookie = target;
            View.Total(emitter) = unchecked((int)target.igOffs);

            emitter.emitJumpDistBind();

            Assert.That(jump.idIsBound(), Is.True);
            Assert.That(View.Target(jump), Is.SameAs(target));
            Assert.That(jump.idCodeSize(), Is.EqualTo(expectedSize));
            Assert.That(source.igSize, Is.EqualTo((ushort)(padding + expectedSize)));
            Assert.That(target.igOffs, Is.EqualTo((uint)(padding + expectedSize)));
            Assert.That(View.Total(emitter), Is.EqualTo(padding + (int)expectedSize));
            Assert.That(source.igData?[0], Is.SameAs(jump));
        });
    }

    [TestCase(126, 2u)]
    [TestCase(127, 5u)]
    public static void BackwardBoundaryIncludesTheShortEncodingWidth(int padding, uint expectedSize)
    {
        WithEmitter((_, emitter) =>
        {
            var label = Label();
            var target = emitter.emitCurIG ?? throw new AssertionException("Missing target group.");
            label.bbEmitCookie = target;
            EmitNops(emitter, padding);
            var source = emitter.emitAddInlineLabel();
            emitter.emitIns_J(INS_jmp, label);
            var jump = Last(emitter);
            var end = emitter.emitAddInlineLabel();
            View.Total(emitter) = unchecked((int)end.igOffs);

            emitter.emitJumpDistBind();

            Assert.That(jump.idIsBound(), Is.True);
            Assert.That(View.Target(jump), Is.SameAs(target));
            Assert.That(jump.idCodeSize(), Is.EqualTo(expectedSize));
            Assert.That(source.igSize, Is.EqualTo((ushort)expectedSize));
            Assert.That(end.igOffs, Is.EqualTo((uint)(padding + expectedSize)));
        });
    }

    [Test]
    public static void ShorteningLaterJumpEnablesAnEarlierForwardJumpOnSecondPass()
    {
        WithEmitter((_, emitter) =>
        {
            var label = Label();
            var firstGroup = emitter.emitCurIG ?? throw new AssertionException("Missing first group.");
            emitter.emitIns_J(INS_jne, label);
            var firstJump = Last(emitter);
            EmitNops(emitter, 119);
            var secondGroup = emitter.emitAddInlineLabel();
            emitter.emitIns_J(INS_jne, label);
            var secondJump = Last(emitter);
            var target = emitter.emitAddInlineLabel();
            label.bbEmitCookie = target;
            View.Total(emitter) = unchecked((int)target.igOffs);

            emitter.emitJumpDistBind();

            Assert.That(firstJump.idCodeSize(), Is.EqualTo(2u));
            Assert.That(secondJump.idCodeSize(), Is.EqualTo(2u));
            Assert.That(firstGroup.igSize, Is.EqualTo(121));
            Assert.That(secondGroup.igOffs, Is.EqualTo(121u));
            Assert.That(secondGroup.igSize, Is.EqualTo(2));
            Assert.That(target.igOffs, Is.EqualTo(123u));
            Assert.That(View.Total(emitter), Is.EqualTo(123));
            Assert.That(View.Target(firstJump), Is.SameAs(target));
            Assert.That(View.Target(secondJump), Is.SameAs(target));
        });
    }

    [Test]
    public static void MultipleJumpsInOneGroupUpdateLaterDescriptorOffsets()
    {
        WithEmitter((_, emitter) =>
        {
            var label = Label();
            var source = emitter.emitCurIG ?? throw new AssertionException("Missing source group.");
            emitter.emitIns_J(INS_jne, label);
            var first = Last(emitter);
            emitter.emitIns_J(INS_jne, label);
            var second = Last(emitter);
            var target = emitter.emitAddInlineLabel();
            label.bbEmitCookie = target;
            View.Total(emitter) = unchecked((int)target.igOffs);

            emitter.emitJumpDistBind();

            Assert.That(View.Offset(first), Is.Zero);
            Assert.That(View.Offset(second), Is.EqualTo(2u));
            Assert.That(source.igSize, Is.EqualTo(4));
            Assert.That(target.igOffs, Is.EqualTo(4u));
            Assert.That(View.Total(emitter), Is.EqualTo(4));
            Assert.That(source.igData?[0], Is.SameAs(first));
            Assert.That(source.igData?[1], Is.SameAs(second));
        });
    }

    [Test]
    public static void CallsBindWithoutChangingTheirFixedInstructionWidth()
    {
        WithEmitter((_, emitter) =>
        {
            var label = Label();
            emitter.emitIns_J(INS_call, label);
            var call = Last(emitter);
            var target = emitter.emitAddInlineLabel();
            label.bbEmitCookie = target;
            View.Total(emitter) = unchecked((int)target.igOffs);

            emitter.emitJumpDistBind();

            Assert.That(call.idIsBound(), Is.True);
            Assert.That(View.Target(call), Is.SameAs(target));
            Assert.That(call.idCodeSize(), Is.EqualTo(5u));
            Assert.That(View.Total(emitter), Is.EqualTo(5));
        });
    }

    [Test]
    public static void CrossRegionJumpRetainsLongFormAndColdBoundary()
    {
        WithEmitter((compiler, emitter) =>
        {
            var label = Label();
            label.SetFlags(BBF_COLD);
            compiler.fgFirstColdBlock = label;
            emitter.emitIns_J(INS_jmp, label);
            var jump = Last(emitter);
            var cold = emitter.emitAddInlineLabel();
            label.bbEmitCookie = cold;
            emitter.emitSetFirstColdIGCookie(cold);
            View.Total(emitter) = unchecked((int)cold.igOffs);

            emitter.emitJumpDistBind();

            Assert.That(View.KeepLong(jump), Is.True);
            Assert.That(jump.idIsBound(), Is.True);
            Assert.That(jump.idCodeSize(), Is.EqualTo(5u));
            Assert.That(cold.igOffs, Is.EqualTo(5u));
            Assert.That(View.Total(emitter), Is.EqualTo(5));
        });
    }

    private static void EmitNops(Emitter emitter, int count)
    {
        while (count > 0)
        {
            var size = int.Min(count, 15);
            emitter.emitIns_Nop(unchecked((uint)size));
            count -= size;
        }
    }

    private static BasicBlock Label()
    {
        var block = new BasicBlock(null, null);
        block.SetFlags(BBF_HAS_LABEL);
        return block;
    }

    private static void WithEmitter(Action<Compiler, Emitter> action)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX,
            (compiler, codeGen, _) => action(compiler, codeGen.Emitter));
    }

    private static Emitter.instrDesc Last(Emitter emitter)
    {
        return View.LastInstruction(emitter) ?? throw new AssertionException("No instruction was recorded.");
    }

    private abstract class View(CodeGen codeGen) : Emitter(codeGen)
    {
        public static ref int Total(Emitter emitter) => ref TotalSize(emitter);
        public static insGroup? Target(instrDesc descriptor) => ((instrDescJmp)descriptor).idjTargetIG;
        public static bool KeepLong(instrDesc descriptor) => ((instrDescJmp)descriptor).idjKeepLong;
        public static uint Offset(instrDesc descriptor) => ((instrDescJmp)descriptor).idjOffs;
        public static instrDesc? LastInstruction(Emitter emitter) => Last(emitter);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitTotalCodeSize")]
        private static extern ref int TotalSize(Emitter emitter);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
        private static extern ref instrDesc? Last(Emitter emitter);
    }
}
