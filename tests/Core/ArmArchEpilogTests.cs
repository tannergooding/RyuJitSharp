// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARMARCH
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class ArmArchEpilogTests
{
    [Test]
    public static void RootEpilogEmitsTheArchitectureReturn()
    {
#if TARGET_ARM
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            var unwindInfo = new UnwindInfo();
            unwindInfo.InitUnwindInfo(compiler, null, null);
            compiler.compFuncInfos = [new FuncInfoDsc { funKind = FuncKind.FUNC_ROOT, uwi = unwindInfo }];
            compiler.compFuncInfoCount = 1;
            compiler.compCurrFuncIdx = 0;
            compiler.fgFuncletsCreated = true;

            AssertRootEpilog(compiler, codeGen, INS_pop);
        });
#else
        Arm64CalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) => AssertRootEpilog(compiler, codeGen, INS_ret));
#endif
    }

    private static void AssertRootEpilog(Compiler compiler, CodeGen codeGen, instruction expectedReturn)
    {
        codeGen.IsFramePointerUsed = false;
#if TARGET_ARM64
        compiler.compCalleeRegsPushed = 2;
        compiler.compFrameInfo = new Compiler.FrameInfo
        {
            frameType = 1,
            calleeSaveSpOffset = 16,
        };
#endif

        var group = CurrentGroup(codeGen.Emitter) ??
            throw new AssertionException("Missing epilog instruction group.");
        group.igFlags = InsGroupFlags.Epilog;

        var block = new BasicBlock(null, null);
        block.SetFlags(BasicBlockFlags.BBF_IS_LIR);
        block.InsertAtEnd(new GenTreeUnOp(GT_RETURN, TYP_VOID, null));
        codeGen.genFnEpilog(block);

        var descriptors = CurrentDescriptors(codeGen.Emitter) ??
            throw new AssertionException("Missing instruction descriptors.");

        Assert.That(descriptors, Is.Not.Empty);
        Assert.That(descriptors[^1].idIns(), Is.EqualTo(expectedReturn));
        Assert.That(compiler.compGeneratingUnwindEpilog, Is.False);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIG")]
    private static extern ref insGroup? CurrentGroup(Emitter emitter);
}
#endif
