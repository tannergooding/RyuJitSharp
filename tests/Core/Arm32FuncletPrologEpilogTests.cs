// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.bbCatchType;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;

namespace RyuJitSharp.UnitTests;

internal static class Arm32FuncletPrologEpilogTests
{
    [TestCase(0)]
    [TestCase(12)]
    public static void FuncletPrologUsesRegisterPushesOrLocalAllocation(int outgoingArgSpaceSize)
    {
        WithFunclet(outgoingArgSpaceSize, (compiler, codeGen, block) =>
        {
            codeGen.genCaptureFuncletPrologEpilogInfo();
            var prolog = codeGen.Emitter.emitGetFirstPrologIG();
            CurrentGroup(codeGen.Emitter) = prolog;
            prolog.igFlags = InsGroupFlags.FuncletProlog;

            codeGen.genFuncletProlog(block);

            instruction[] expected = outgoingArgSpaceSize == 0
                ? [INS_push]
                : [INS_push, INS_sub];
            Assert.That(Descriptors(codeGen.Emitter).Select(descriptor => descriptor.idIns()),
                Is.EqualTo(expected));
        });
    }

    [TestCase(0)]
    [TestCase(12)]
    public static void FuncletEpilogRestoresRegistersAndOutgoingFrame(int outgoingArgSpaceSize)
    {
        WithFunclet(outgoingArgSpaceSize, (compiler, codeGen, _) =>
        {
            codeGen.genCaptureFuncletPrologEpilogInfo();
            var group = CurrentGroup(codeGen.Emitter)
                ?? throw new AssertionException("Missing current instruction group.");
            group.igFlags = InsGroupFlags.FuncletEpilog | InsGroupFlags.OutOfOrderHead;

            codeGen.genFuncletEpilog(new BasicBlock(null, null));

            instruction[] expected = outgoingArgSpaceSize == 0
                ? [INS_pop]
                : [INS_add, INS_pop];
            Assert.That(Descriptors(codeGen.Emitter).Select(descriptor => descriptor.idIns()),
                Is.EqualTo(expected));
        });
    }

    private static void WithFunclet(
        int outgoingArgSpaceSize, Action<Compiler, CodeGen, BasicBlock> action)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaDoneFrameLayout = Compiler.FINAL_FRAME_LAYOUT;
            compiler.lvaMonAcquired = BAD_VAR_NUM;
            compiler.lvaResumedIndicator = BAD_VAR_NUM;
            compiler.lvaAsyncThreadObjectVar = BAD_VAR_NUM;
            compiler.lvaAsyncExecutionContextVar = BAD_VAR_NUM;
            compiler.lvaAsyncSynchronizationContextVar = BAD_VAR_NUM;
            compiler.lvaOutgoingArgSpaceSize.ResetWritePhase();
            compiler.lvaOutgoingArgSpaceSize.Value = outgoingArgSpaceSize;
            compiler.compHndBBtabCount = 1;

            var block = new BasicBlock(null, null) { CatchType = BBCT_FINALLY, HndIndex = 0 };
            compiler.compHndBBtab = [new EHblkDsc { ebdTyp = BBCT_FINALLY, ebdHndBeg = block, ebdHndLast = block }];
            compiler.compCurBB = block;
            codeGen.IsFramePointerUsed = true;
            action(compiler, codeGen, block);
        }, captureAssertions: true, minOpts: false);
    }

    private static List<Emitter.instrDesc> Descriptors(Emitter emitter)
    {
        return CurrentDescriptors(emitter) ?? throw new AssertionException("Missing descriptor buffer.");
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIG")]
    private static extern ref insGroup? CurrentGroup(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);
}
#endif
