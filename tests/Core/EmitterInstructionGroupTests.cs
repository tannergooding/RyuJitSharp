// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterInstructionGroupTests
{
    [Test]
    public static void GroupNumberPreservesUnsignedWidth()
    {
        var group = new insGroup();
        group.InitializeNum(uint.MaxValue);

        Assert.That(group.GetDisplayId(), Is.EqualTo(uint.MaxValue));
    }

    [Test]
    public static void AllocationInitializesNumberOffsetFuncletAndGroupState()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compCurrFuncIdx = 3;
        var emitter = new CodeGen(compiler).Emitter;
        emitter.emitBegCG(compiler, default);
        NextNumber(emitter) = 7;
        CodeOffset(emitter) = 24;

        var group = Allocate(emitter);
        Assert.That(group.GetDisplayId(), Is.EqualTo(7u));
        Assert.That(NextNumber(emitter), Is.EqualTo(8));
        Assert.That(group.igOffs, Is.EqualTo(24u));
        Assert.That(group.igFuncIdx, Is.EqualTo(3u));
        Assert.That(group.igFlags, Is.EqualTo(InsGroupFlags.None));
        Assert.That(group.igData, Is.Null);
        Assert.That(group.igSize, Is.Zero);
        Assert.That(group.igGCregs, Is.EqualTo(regMask.SRBM_NONE));
        Assert.That(group.igInsCnt, Is.Zero);
#if TARGET_XARCH
        Assert.That(group.igLastIns, Is.Null);
#endif
#if DEBUG
        Assert.That(group.igSelf, Is.SameAs(group));
        Assert.That(group.igBlocks, Is.Empty);
#endif
#if DEBUG || LATE_DISASM
        Assert.That(group.igWeight, Is.EqualTo(100.0));
        Assert.That(group.igPerfScore, Is.Zero);
#endif
    }

    [Test]
    public static void InsertingAfterMiddlePreservesSuccessorAndTail()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var emitter = new CodeGen(compiler).Emitter;
        emitter.emitBegCG(compiler, default);
        var head = new insGroup();
        var tail = new insGroup { igFlags = InsGroupFlags.Prolog };
        head.igNext = tail;
        FirstGroup(emitter) = head;
        LastGroup(emitter) = tail;
        emitter.emitCurIG = tail;

        var middle = Allocate(emitter);
        InsertAfter(emitter, head, middle);

        Assert.That(head.igNext, Is.SameAs(middle));
        Assert.That(middle.igNext, Is.SameAs(tail));
#if TARGET_XARCH
        Assert.That(middle.igPrev, Is.SameAs(head));
        Assert.That(tail.igPrev, Is.SameAs(middle));
#endif
        Assert.That(LastGroup(emitter), Is.SameAs(tail));
        Assert.That(emitter.emitCurIG, Is.SameAs(tail));

        var final = Allocate(emitter);
        InsertAfter(emitter, tail, final);
        Assert.That(tail.igNext, Is.SameAs(final));
        Assert.That(LastGroup(emitter), Is.SameAs(final));
#if TARGET_XARCH
        Assert.That(final.igPrev, Is.SameAs(tail));
#endif
    }

    [TestCase(InsGroupFlags.Prolog)]
    [TestCase(InsGroupFlags.Epilog)]
    [TestCase(InsGroupFlags.FuncletProlog)]
    [TestCase(InsGroupFlags.FuncletEpilog)]
    public static void AllocationAfterCurrentGroupPropagatesOnlyPrologAndEpilogFlags(InsGroupFlags flag)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var emitter = new CodeGen(compiler).Emitter;
        emitter.emitBegCG(compiler, default);
        var head = new insGroup
        {
            igFlags = flag | InsGroupFlags.Extend | InsGroupFlags.NoGCInterrupt | InsGroupFlags.OutOfOrderHead,
        };
#if DEBUG || LATE_DISASM
        head.igWeight = 42.0;
#endif
        var tail = new insGroup();
        head.igNext = tail;
        FirstGroup(emitter) = head;
        LastGroup(emitter) = tail;
        emitter.emitCurIG = head;

        var next = AllocateAndLink(emitter);

        Assert.That(head.igNext, Is.SameAs(next));
        Assert.That(next.igNext, Is.SameAs(tail));
        Assert.That(LastGroup(emitter), Is.SameAs(tail));
        Assert.That(emitter.emitCurIG, Is.SameAs(next));
        Assert.That(next.igFlags, Is.EqualTo(flag));
#if DEBUG || LATE_DISASM
        Assert.That(next.igWeight, Is.EqualTo(42.0));
#endif
    }

    [Test]
    public static void ActiveBlockDeterminesGroupWeight()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var block = new BasicBlock(null, null) { bbWeight = 0 };
        compiler.compCurBB = block;
        var emitter = new CodeGen(compiler).Emitter;
        emitter.emitBegCG(compiler, default);

        var group = Allocate(emitter);

#if DEBUG || LATE_DISASM
        Assert.That(group.igWeight, Is.EqualTo(block.getBBWeight(compiler)));
#endif
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ReinitializingGroupClearsItsPreviousStorage(bool placeholder)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var emitter = new CodeGen(compiler).Emitter;
        emitter.emitBegCG(compiler, default);
        var group = new insGroup();

        if (placeholder)
        {
            group.igFlags = InsGroupFlags.Placeholder;
            group.igPhData = new insPlaceholderGroupData();
        }
        else
        {
            group.igFlags = InsGroupFlags.GCVars | InsGroupFlags.ByrefRegs;
            group.igData = [];
            group.igStorageSize = 12;
            group.igDataOffset = 12;
            group.SavedGcVars = [1];
            group.SavedByrefRegs = 1;
        }

        Initialize(emitter, group);

        Assert.That(group.igFlags, Is.EqualTo(InsGroupFlags.None));
        Assert.That(group.igData, Is.Null);
        Assert.That(group.igPhData, Is.Null);
        Assert.That(group.igStorageSize, Is.EqualTo((nuint)0));
        Assert.That(group.igDataOffset, Is.EqualTo((nuint)0));
        Assert.That(group.SavedGcVars, Is.Empty);
        Assert.That(group.SavedByrefRegs, Is.Zero);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitInitIG")]
    private static extern void Initialize(Emitter emitter, insGroup group);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitAllocIG")]
    private static extern insGroup Allocate(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitInsertIGAfter")]
    private static extern void InsertAfter(Emitter emitter, insGroup after, insGroup group);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitAllocAndLinkIG")]
    private static extern insGroup AllocateAndLink(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitNxtIGnum")]
    private static extern ref int NextNumber(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurCodeOffset")]
    private static extern ref int CodeOffset(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitIGlist")]
    private static extern ref insGroup? FirstGroup(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitIGlast")]
    private static extern ref insGroup? LastGroup(Emitter emitter);
}
