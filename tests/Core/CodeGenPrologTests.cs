// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
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
internal static unsafe class CodeGenPrologTests
{
    [Test]
    public static void MaterializationRestoresEntryLocationsAndReplacesEveryEpilogReservation()
    {
        WithRoot((compiler, codeGen) =>
        {
            var blocks = CodeGenBlockDriverTests.Blocks(compiler, BBKinds.BBJ_RETURN, BBKinds.BBJ_RETURN);
            var emitter = codeGen.Emitter;
            var prolog = emitter.emitGetFirstPrologIG();
            emitter.emitCurIG = prolog.igNext ?? throw new AssertionException("Missing body instruction group.");
            emitter.emitIns(INS_nop);
            codeGen.genReserveEpilog(blocks[0]);
            var first = CodeGenBlockDriverTests.LastPlaceholder(emitter);
            emitter.emitIns(INS_nop);
            codeGen.genReserveEpilog(blocks[1]);
            var second = CodeGenBlockDriverTests.LastPlaceholder(emitter);
            var restored = 0;
            Allocator(compiler) = new CodeGenFrameFinalizationTests.EntryAllocator(block =>
            {
                Assert.That(block, Is.SameAs(blocks[0]));
                Assert.That(prolog.igInsCnt, Is.Zero);
                restored++;
            });

            codeGen.genGeneratePrologsAndEpilogs();

            Assert.That(restored, Is.EqualTo(1));
            Assert.That(compiler.compCurBB, Is.Null);
            Assert.That(emitter.emitCurIG, Is.Null);
            Assert.That(first?.igPhData, Is.Null);
            Assert.That(second?.igPhData, Is.Null);
            Assert.That(first?.igData?.Last().idIns(), Is.EqualTo(INS_ret));
            Assert.That(second?.igData?.Last().idIns(), Is.EqualTo(INS_ret));
            uint offset = 0;
            for (var group = (insGroup?)prolog; group is not null; group = group.igNext)
            {
                Assert.That(group.igOffs, Is.EqualTo(offset));
                Assert.That(group.igFlags & InsGroupFlags.Placeholder, Is.EqualTo(InsGroupFlags.None));
                offset += group.igSize;
            }
        });
    }

    [TestCase(false, 0)]
    [TestCase(false, 8)]
    [TestCase(false, 40)]
    [TestCase(true, 0)]
    [TestCase(true, 32)]
    public static void RootPrologPreservesFrameSetupOrderAndUnwindBoundary(bool framePointer, int frameSize)
    {
        WithRoot((compiler, codeGen) =>
        {
            codeGen.IsFramePointerUsed = framePointer;
            compiler.compLclFrameSize = frameSize;

            codeGen.genFnProlog();

            var ids = SavedProlog(codeGen);
            var expected = framePointer ? new[] { INS_push }.ToList() : [];
            if (frameSize != 0)
            {
                expected.Add(frameSize == 8 ? INS_push : INS_sub);
            }
            if (framePointer)
            {
                expected.Add(codeGen.genSPtoFPdelta == 0 ? INS_mov : INS_lea);
            }
            Assert.That(ids.Select(id => id.idIns()), Is.EqualTo(expected));
            Assert.That(compiler.genIPmappings.First?.Value.ipmdKind, Is.EqualTo(IPmappingDscKind.Prolog));
            Assert.That(PrologEnd(codeGen.Emitter).GetInsNum(), Is.EqualTo(ids.Length));
            Assert.That(NoGcRequests(codeGen.Emitter), Is.Zero);
            Assert.That(codeGen.Emitter.emitGetFirstPrologIG().igInsCnt, Is.EqualTo(ids.Length));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void GcPrologBoundaryMovesPastRegisterInitializationOnlyWhenInterruptible(bool interruptible)
    {
        WithRoot((compiler, codeGen) =>
        {
            codeGen.Interruptible = interruptible;
            codeGen.IsFramePointerUsed = false;
            compiler.lvaCount = 1;
            compiler.lvaTable = [new LclVarDsc
            {
                Type = TYP_LONG, RegNum = REG_RAX, lvRegister = true, lvTracked = true, lvLRACandidate = true,
                lvMustInit = true,
            }];
            compiler.fgFirstBB = new BasicBlock(null, null) { bbLiveIn = VarSetOps.MakeSingleton(compiler, 0) };

            codeGen.genFnProlog();

            var id = SavedProlog(codeGen).Single();
            Assert.That(id.idIns(), Is.EqualTo(INS_xor));
            Assert.That(id.idReg1(), Is.EqualTo(REG_RAX));
            Assert.That(PrologEnd(codeGen.Emitter).GetInsNum(), Is.EqualTo(interruptible ? 1 : 0));
        });
    }

    [Test]
    public static void StackGcInitializationAndRangeFollowFrameSetup()
    {
        WithRoot((compiler, codeGen) =>
        {
            compiler.compLclFrameSize = 32;
            compiler.lvaCount = 1;
            compiler.lvaTable = [new LclVarDsc
            {
                Type = TYP_REF, RegNum = REG_STK, lvTracked = true, lvOnFrame = true,
                lvFramePointerBased = true, StackOffset = -16, lvMustInit = true,
            }];
            compiler.fgFirstBB = new BasicBlock(null, null) { bbLiveIn = VarSetOps.MakeSingleton(compiler, 0) };
            var group = codeGen.Emitter.emitGetFirstPrologIG();
            group.igFlags &= ~InsGroupFlags.Prolog;
            codeGen.genCheckUseBlockInit();
            group.igFlags |= InsGroupFlags.Prolog;

            codeGen.genFnProlog();

            Assert.That(SavedProlog(codeGen).Select(id => id.idIns()),
                Is.EqualTo((instruction[])[INS_push, INS_sub, INS_lea, INS_xor, INS_mov]));
            Assert.That(FrameGcMin(codeGen.Emitter), Is.EqualTo(-16));
            Assert.That(FrameGcMax(codeGen.Emitter), Is.EqualTo(-8));
            Assert.That(FrameGcCount(codeGen.Emitter), Is.EqualTo(1));
        });
    }

    [Test]
    public static void VarargsShadowHomingPrecedesFramePointerPush()
    {
        WithRoot((compiler, codeGen) =>
        {
            compiler.info.compIsVarArgs = true;

            codeGen.genFnProlog();

            var ids = SavedProlog(codeGen);
            Assert.That(ids.Take(4).Select(id => id.idReg1()), Is.EqualTo((regNumber[])[REG_RCX, REG_RDX, REG_R8, REG_R9]));
            Assert.That(ids[4].idIns(), Is.EqualTo(INS_push));
            Assert.That(ids[4].idReg1(), Is.EqualTo(REG_RBP));
        });
    }

    internal static void WithRoot(Action<Compiler, CodeGen> action)
    {
        UnwindPrologRecordingTests.WithProlog((compiler, codeGen) =>
        {
            compiler.lvaCount = 0;
            compiler.lvaTable = [];
            compiler.info.compIsStatic = true;
            compiler.fgFirstBB = new BasicBlock(null, null) { bbLiveIn = VarSetOps.MakeEmpty(compiler) };
            compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
            compiler.lvaGSSecurityCookie = BAD_VAR_NUM;
            compiler.lvaInlinedPInvokeFrameVar = BAD_VAR_NUM;
            compiler.lvaStubArgumentVar = BAD_VAR_NUM;
            compiler.lvaRetAddrVar = BAD_VAR_NUM;
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.osPageSize = 4096;
            compiler.opts.compDbgInfo = true;
            compiler.genIPmappings = [];
            compiler.srbmIntCalleeTrash = SRBM_INT_CALLEE_TRASH_INIT;
            compiler.srbmFltCalleeTrash = SRBM_FLT_CALLEE_TRASH_INIT;
            AllIntegerRegisters(compiler) = SRBM_ALLINT_INIT;
            AllFloatRegisters(compiler) = SRBM_ALLFLOAT_INIT;
            LastIntegerRegister(compiler) = REG_R15;
            codeGen.CopyRegisterInfo();
            codeGen.RegSet.tmpInit();
            compiler.lvaDoneFrameLayout = Compiler.TENTATIVE_FRAME_LAYOUT;
            codeGen.RegSet.rsClearRegsModified();
            compiler.lvaDoneFrameLayout = Compiler.FINAL_FRAME_LAYOUT;
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

    private static Emitter.instrDesc[] SavedProlog(CodeGen codeGen)
    {
        return codeGen.Emitter.emitGetFirstPrologIG().igData ?? [];
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask AllIntegerRegisters(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask AllFloatRegisters(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "regIntLast")]
    private static extern ref regNumber LastIntegerRegister(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitPrologEndPos")]
    private static extern ref emitLocation PrologEnd(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitNoGCRequestCount")]
    private static extern ref int NoGcRequests(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitGCrFrameOffsMin")]
    private static extern ref int FrameGcMin(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitGCrFrameOffsMax")]
    private static extern ref int FrameGcMax(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitGCrFrameOffsCnt")]
    private static extern ref int FrameGcCount(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regAlloc")]
    private static extern ref IRegAlloc? Allocator(Compiler compiler);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitHalt")]
    private static extern ref JitConfigValues.MethodSet HaltSelection(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitHashHalt")]
    private static extern ref int HaltHash(ref JitConfigValues config);
#endif
}
