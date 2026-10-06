// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.bbCatchType;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;

namespace RyuJitSharp.UnitTests;

internal static class Arm64FuncletPrologEpilogTests
{
    [TestCase(0)]
    [TestCase(16)]
    [TestCase(512)]
    public static void FuncletPrologPreservesArm64FrameShapes(int outgoingArgSpaceSize)
    {
        WithFunclet(outgoingArgSpaceSize, (compiler, codeGen, block) =>
        {
            codeGen.genCaptureFuncletPrologEpilogInfo();
            var prolog = codeGen.Emitter.emitGetFirstPrologIG();
            CurrentGroup(codeGen.Emitter) = prolog;
            prolog.igFlags = InsGroupFlags.FuncletProlog;

            codeGen.genFuncletProlog(block);

            var expected = outgoingArgSpaceSize switch
            {
                0 => (instruction[])[INS_stp],
                16 => [INS_sub, INS_stp],
                _ => [INS_stp, INS_sub],
            };
            Assert.That(Descriptors(codeGen.Emitter).Select(descriptor => descriptor.idIns()),
                Is.EqualTo(expected));
        });
    }

    [TestCase(0)]
    [TestCase(16)]
    [TestCase(512)]
    public static void FuncletEpilogRestoresArm64FrameShapes(int outgoingArgSpaceSize)
    {
        WithFunclet(outgoingArgSpaceSize, (_, codeGen, _) =>
        {
            codeGen.genCaptureFuncletPrologEpilogInfo();
            var group = CurrentGroup(codeGen.Emitter)
                ?? throw new AssertionException("Missing current instruction group.");
            group.igFlags = InsGroupFlags.FuncletEpilog | InsGroupFlags.OutOfOrderHead;

            codeGen.genFuncletEpilog(new BasicBlock(null, null));

            var expected = outgoingArgSpaceSize switch
            {
                0 => (instruction[])[INS_ldp, INS_ret],
                16 => [INS_ldp, INS_add, INS_ret],
                _ => [INS_add, INS_ldp, INS_ret],
            };
            Assert.That(Descriptors(codeGen.Emitter).Select(descriptor => descriptor.idIns()),
                Is.EqualTo(expected));
        });
    }

    private static void WithFunclet(
        int outgoingArgSpaceSize, Action<Compiler, CodeGen, BasicBlock> action)
    {
        Arm64CalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
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
        });
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
