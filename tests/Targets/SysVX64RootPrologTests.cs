// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if UNIX_AMD64_ABI && FEATURE_SIMD
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class SysVX64RootPrologTests
{
    [Test]
    public static void StackVector3ClearsOnlyTheUpperLaneBeforeParameterHoming()
    {
        WithRoot((compiler, codeGen) =>
        {
            compiler.lvaTable =
            [
                new LclVarDsc { Type = TYP_SIMD12, lvIsParam = true, lvOnFrame = true,
                    StackOffset = 24, RegNum = REG_STK },
            ];
            compiler.lvaCount = 1;
            compiler.info.compArgsCount = 1;
            compiler.lvaParameterPassingInfo =
            [
                AbiPassingInformation.FromSegment(compiler, false, AbiPassingSegment.OnStack(8, 0, 16)),
            ];

            codeGen.genClearStackVec3ArgUpperBits();

            var ids = Descriptors(codeGen);
            Assert.That(ids, Has.Count.EqualTo(1));
            Assert.That(ids[0].idIns(), Is.EqualTo(INS_mov));
            Assert.That(ids[0].idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(12u));
        });
    }

    [Test]
    public static void RegisterVector3ClearsTheUpperLaneInItsSecondAbiRegister()
    {
        WithRoot((compiler, codeGen) =>
        {
            var first = AbiPassingSegment.InRegister(REG_XMM0, 0, 8);
            var second = AbiPassingSegment.InRegister(REG_XMM1, 8, 8);
            compiler.lvaTable =
            [
                new LclVarDsc { Type = TYP_SIMD12, lvIsParam = true, lvIsRegArg = true,
                    RegNum = REG_STK },
            ];
            compiler.lvaCount = 1;
            compiler.info.compArgsCount = 1;
            compiler.lvaParameterPassingInfo =
            [
                AbiPassingInformation.FromSegments(compiler, in first, in second),
            ];

            codeGen.genClearStackVec3ArgUpperBits();

            var ids = Descriptors(codeGen);
            Assert.That(ids, Has.Count.EqualTo(1));
            Assert.That(ids[0].idIns(), Is.EqualTo(INS_insertps));
            Assert.That(ids[0].idReg1(), Is.EqualTo(REG_XMM1));
        });
    }

    [Test]
    public static void RootPrologClearsStackVector3BeforeArgumentHoming()
    {
        WithRoot((compiler, codeGen) =>
        {
            compiler.lvaTable =
            [
                new LclVarDsc { Type = TYP_SIMD12, lvIsParam = true, lvOnFrame = true,
                    StackOffset = 24, RegNum = REG_STK },
            ];
            compiler.lvaCount = 1;
            compiler.info.compArgsCount = 1;
            compiler.lvaParameterPassingInfo =
            [
                AbiPassingInformation.FromSegment(compiler, false, AbiPassingSegment.OnStack(8, 0, 16)),
            ];

            codeGen.IsFramePointerUsed = false;
            codeGen.genFnProlog();

            var ids = codeGen.Emitter.emitGetFirstPrologIG().igData
                ?? throw new AssertionException("Missing saved prolog.");
            Assert.That(ids.Select(id => id.idIns()), Is.EqualTo((instruction[])[INS_mov]));
            Assert.That(ids[0].idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(12u));
        });
    }

    [TestCase(false, 0)]
    [TestCase(false, 8)]
    [TestCase(true, 32)]
    public static void RootPrologMaterializesSysVFrameAndUnwind(bool framePointer, int frameSize)
    {
        WithRoot((compiler, codeGen) =>
        {
            codeGen.IsFramePointerUsed = framePointer;
            compiler.compLclFrameSize = frameSize;

            codeGen.genFnProlog();

            var ids = codeGen.Emitter.emitGetFirstPrologIG().igData
                ?? throw new AssertionException("Missing saved prolog.");
            var expected = framePointer
                ? (instruction[])[INS_push, INS_sub, INS_lea]
                : frameSize == 0 ? [] : (instruction[])[INS_push];
            Assert.That(ids.Select(id => id.idIns()), Is.EqualTo(expected));
            Assert.That(compiler.funCurrentFunc().unwindCodes, Is.Not.Null);
        });
    }

    [Test]
    public static void OsrRootPrologReconstructsTierZeroFrameBeforeSavingFramePointer()
    {
        WithRoot((compiler, codeGen) =>
        {
            var storage = stackalloc byte[PatchpointInfo.ComputeSize(0)];
            var patchpoint = (PatchpointInfo*)storage;
            patchpoint->Initialize(0, 64);
            patchpoint->CalleeSaveRegisters = (long)SRBM_RBP;
            compiler.info.compPatchpointInfo = patchpoint;
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
            codeGen.IsFramePointerUsed = true;

            codeGen.genFnProlog();

            var ids = codeGen.Emitter.emitGetFirstPrologIG().igData
                ?? throw new AssertionException("Missing OSR prolog.");
            Assert.That(ids.Select(id => id.idIns()).Take(3),
                Is.EqualTo((instruction[])[INS_push, INS_mov, INS_push]));
            Assert.That(compiler.funCurrentFunc().unwindCodes, Is.Not.Null);
        });
    }

    [Test]
    public static void PrologAndEpilogMaterializationCompletesWithSysVUnwind()
    {
        WithRoot((compiler, codeGen) =>
        {
            codeGen.IsFramePointerUsed = true;
            compiler.compLclFrameSize = 8;
            var block = compiler.fgFirstBB ?? throw new AssertionException("Missing entry block.");
            codeGen.Emitter.emitCurIG = codeGen.Emitter.emitGetFirstPrologIG().igNext
                ?? throw new AssertionException("Missing body instruction group.");
            codeGen.Emitter.emitCurIG.igFlags &= ~(InsGroupFlags.Prolog | InsGroupFlags.OutOfOrderHead);
            codeGen.Emitter.emitIns(INS_nop);
            codeGen.genReserveEpilog(block);
            var placeholder = PlaceholderHead(codeGen.Emitter)
                ?? throw new AssertionException("Missing epilog placeholder.");
            Allocator(compiler) = new LinearScan(compiler);
            codeGen.genGeneratePrologsAndEpilogs();

            Assert.That(codeGen.Emitter.emitGetFirstPrologIG().igData, Is.Not.Null);
            Assert.That(placeholder.igPhData, Is.Null);
            Assert.That(placeholder.igData?.Last().idIns(), Is.EqualTo(INS_ret));
            Assert.That(compiler.compCurBB, Is.Null);
            Assert.That(codeGen.Emitter.emitCurIG, Is.Null);
            Assert.That(compiler.funCurrentFunc().unwindCodes, Is.Not.Null);
        });
    }

    private static void WithRoot(Action<Compiler, CodeGen> action)
    {
        SysVX64FrameCodeGenTests.WithProlog((compiler, codeGen) =>
        {
            CORINFO_METHOD_INFO methodInfo = default;
            compiler.info.compMethodInfo = &methodInfo;
            compiler.info.compIsStatic = true;
            compiler.lvaCount = 0;
            compiler.lvaTable = [];
            compiler.info.compArgsCount = 0;
            compiler.fgFirstBB = new BasicBlock(null, null) { bbLiveIn = VarSetOps.MakeEmpty(compiler) };
            compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
            compiler.lvaSecretStubArg = BAD_VAR_NUM;
            compiler.opts.compDbgInfo = true;
            compiler.genIPmappings = [];
            compiler.srbmIntCalleeTrash = SRBM_INT_CALLEE_TRASH_INIT;
            compiler.srbmFltCalleeTrash = SRBM_FLT_CALLEE_TRASH_INIT;
            AllFloatRegisters(compiler) = SRBM_ALLFLOAT_INIT;
            codeGen.CopyRegisterInfo();
            codeGen.RegSet.tmpInit();
            compiler.compCalleeRegsPushed = 0;
            compiler.compCalleeFPRegsSavedMask = SRBM_NONE;
            codeGen.resetFramePointerUsedWritePhase();
#if DEBUG
            var savedHalt = HaltSelection(ref JitConfig);
            var savedHash = HaltHash(ref JitConfig);
            HaltSelection(ref JitConfig) = new(null, null);
            HaltHash(ref JitConfig) = -1;
            try
            {
#endif
            action(compiler, codeGen);
#if DEBUG
            }
            finally
            {
                HaltSelection(ref JitConfig) = savedHalt;
                HaltHash(ref JitConfig) = savedHash;
            }
#endif
        });
    }

    private static List<Emitter.instrDesc> Descriptors(CodeGen codeGen)
    {
        return CurrentDescriptors(codeGen.Emitter)
            ?? throw new AssertionException("Missing instruction descriptors.");
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitPlaceholderList")]
    private static extern ref insGroup? PlaceholderHead(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask AllFloatRegisters(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regAlloc")]
    private static extern ref IRegAlloc? Allocator(Compiler compiler);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitHalt")]
    private static extern ref JitConfigValues.MethodSet HaltSelection(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitHashHalt")]
    private static extern ref int HaltHash(ref JitConfigValues config);
#endif
}
#endif
