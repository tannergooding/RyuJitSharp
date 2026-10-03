// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class Arm64CodeGenEHTransferTargetTests
{
    [TestCase(true, false)]
    [TestCase(false, false)]
    [TestCase(true, true)]
    public static void RetlessFinallyCallsBreakOnlyAtAnEhBoundaryOrCodeEnd(bool hasNextBlock, bool hasEhBoundary)
    {
        var (compiler, codeGen, emitter) = CreateEmitter();
        var target = Label();
        var block = Transfer(BBJ_CALLFINALLY, target);
        block.SetFlags(BBF_RETLESS_CALL);

        if (!hasNextBlock)
        {
            block.Next = null;
        }
        else if (hasEhBoundary)
        {
            var next = Label();
            next.TryIndex = 0;
            block.Next = next;
        }
        else
        {
            block.Next = Label();
        }

        compiler.compCurBB = block;

        codeGen.genCallFinally(block);

        var shouldBreak = !hasNextBlock || hasEhBoundary;
        var descriptors = Descriptors(emitter);
        Assert.That(descriptors.Select(descriptor => descriptor.idIns()),
            Is.EqualTo(shouldBreak ? new[] { INS_bl_local, INS_BREAKPOINT } : [INS_bl_local]));
        Assert.That(((Emitter.instrDescJmp)descriptors[0]).idjTarget, Is.SameAs(target));
        Assert.That(NoGcRequests(emitter), Is.Zero);
    }

    [TestCase(true)]
    [TestCase(false)]
    public static void ReturningFinallyCallsPreserveTheContinuationPath(bool fallsThrough)
    {
        var (compiler, codeGen, emitter) = CreateEmitter();
        var target = Label();
        var continuation = Label();
        var block = Transfer(BBJ_CALLFINALLY, target);
        var finallyReturn = Transfer(BBJ_CALLFINALLYRET, continuation);
        block.Next = finallyReturn;
        finallyReturn.Next = fallsThrough ? continuation : Label();
        compiler.compCurBB = block;

        codeGen.genCallFinally(block);

        var descriptors = Descriptors(emitter);
        Assert.That(descriptors.Select(descriptor => descriptor.idIns()),
            Is.EqualTo(fallsThrough ? new[] { INS_bl_local, INS_nop } : [INS_bl_local, INS_b]));
        Assert.That(((Emitter.instrDescJmp)descriptors[0]).idjTarget, Is.SameAs(target));
        if (!fallsThrough)
        {
            Assert.That(((Emitter.instrDescJmp)descriptors[1]).idjTarget, Is.SameAs(continuation));
        }

        Assert.That(NoGcRequests(emitter), Is.Zero);
        Assert.That(emitter.emitCurIG!.igFlags & InsGroupFlags.NoGCInterrupt,
            Is.EqualTo(InsGroupFlags.NoGCInterrupt));
    }

    [Test]
    public static void CatchReturnRecordsAnAdrIntoTheIntegerReturnRegister()
    {
        var (compiler, codeGen, emitter) = CreateEmitter();
        var target = Label();
        var block = Transfer(BBJ_EHCATCHRET, target);
        compiler.compCurBB = block;

        codeGen.genEHCatchRet(block);

        var descriptor = Descriptors(emitter).Single();
        Assert.That(descriptor.idIns(), Is.EqualTo(INS_adr));
        Assert.That(descriptor.idReg1(), Is.EqualTo(REG_INTRET));
        Assert.That(descriptor.idOpSize(), Is.EqualTo(EA_PTRSIZE));
        Assert.That(((Emitter.instrDescJmp)descriptor).idjTarget, Is.SameAs(target));
    }

    private static BasicBlock Label()
    {
        var block = new BasicBlock(null, null);
        block.SetFlags(BBF_HAS_LABEL);

        return block;
    }

    private static BasicBlock Transfer(BBKinds kind, BasicBlock target)
    {
        var block = new BasicBlock(null, null);
        block.SetKindAndTargetEdge(kind, new FlowEdge(block, target, null));

        return block;
    }

    private static (Compiler Compiler, CodeGen CodeGen, Emitter Emitter) CreateEmitter()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        compiler.compCurBB = new BasicBlock(null, null);
        var codeGen = new CodeGen(compiler);
        var emitter = codeGen.Emitter;
        emitter.emitBegCG(compiler, default);
        emitter.Init();
        emitter.emitBegFN(false
#if DEBUG
            , true
#endif
            );

        return (compiler, codeGen, emitter);
    }

    private static List<Emitter.instrDesc> Descriptors(Emitter emitter)
    {
        return CurrentDescriptors(emitter) ?? throw new AssertionException("No instruction descriptors were recorded.");
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitNoGCRequestCount")]
    private static extern ref int NoGcRequests(Emitter emitter);
}
#endif
