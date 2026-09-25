// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CodeGenFrameInitializationTests
{
    [TestCase(TYP_INT, false, false, false, false, 0)]
    [TestCase(TYP_INT, true, false, false, false, 0)]
    [TestCase(TYP_INT, true, true, false, true, 2)]
    [TestCase(TYP_INT, false, false, true, true, 2)]
    [TestCase(TYP_REF, false, false, false, false, 0)]
    [TestCase(TYP_REF, false, true, false, true, 2)]
    [TestCase(TYP_BYREF, false, true, false, true, 2)]
    public static void TrackedLocalsRequireLiveEntryOrExplicitInitialization(
        var_types type, bool initMem, bool live, bool priorMustInit, bool expectedInit, int slots)
    {
        WithFrame((compiler, codeGen) =>
        {
            compiler.info.compInitMem = initMem;
            ref var local = ref compiler.lvaTable[0];
            local.Type = type;
            local.lvMustInit = priorMustInit;
            assert(compiler.fgFirstBB is not null);
            compiler.fgFirstBB.bbLiveIn = live ? VarSetOps.MakeSingleton(compiler, 0) : VarSetOps.MakeEmpty(compiler);

            codeGen.genCheckUseBlockInit();

            Assert.That(local.lvMustInit, Is.EqualTo(expectedInit));
            Assert.That(codeGen.InitStkLclCnt, Is.EqualTo((uint)slots));
            Assert.That(codeGen.UseBlockInit, Is.False);
        });
    }

    [TestCase(false, false, false, 0)]
    [TestCase(false, false, true, 6)]
    [TestCase(false, true, false, 6)]
    [TestCase(true, false, false, 6)]
    [TestCase(true, true, false, 6)]
    public static void StructInitializationCountsTheHomeOnceEvenWhenGcAndLivenessBothRequireIt(
        bool tracked, bool live, bool initMem, int slots)
    {
        WithFrame((compiler, codeGen) =>
        {
            compiler.info.compInitMem = initMem;
            ref var local = ref compiler.lvaTable[0];
            local.Type = TYP_STRUCT;
            local.lvTracked = tracked;
            var builder = new ClassLayoutBuilder(compiler, 24);
            if (tracked || live)
            {
                builder.SetGCPtrType(1, TYP_REF);
            }
            local.Layout = ClassLayout.Create(compiler, in builder);
            assert(compiler.fgFirstBB is not null);
            compiler.fgFirstBB.bbLiveIn = live ? VarSetOps.MakeSingleton(compiler, 0) : VarSetOps.MakeEmpty(compiler);

            codeGen.genCheckUseBlockInit();

            Assert.That(local.lvMustInit, Is.EqualTo(slots != 0));
            Assert.That(codeGen.InitStkLclCnt, Is.EqualTo((uint)slots));
            Assert.That(codeGen.UseBlockInit, Is.EqualTo(slots > 4));
        });
    }

    [TestCase(8, false)]
    [TestCase(16, false)]
    [TestCase(17, true)]
    [TestCase(24, true)]
    public static void BlockThresholdUsesRoundedHomesAndFourByteSlots(int size, bool blockInit)
    {
        WithFrame((compiler, codeGen) =>
        {
            compiler.info.compInitMem = true;
            ref var local = ref compiler.lvaTable[0];
            local.Type = TYP_STRUCT;
            local.Layout = new ClassLayout(size);
            local.lvTracked = false;

            codeGen.genCheckUseBlockInit();

            Assert.That(codeGen.InitStkLclCnt, Is.EqualTo((uint)(roundUp(size, TARGET_POINTER_SIZE) / 4)));
            Assert.That(codeGen.UseBlockInit, Is.EqualTo(blockInit));
        });
    }

    [TestCase(false, 0)]
    [TestCase(true, 2)]
    public static void EntryRegistersNeedStackInitializationOnlyWhenLiveAcrossHandlers(bool handlerLive, int slots)
    {
        WithFrame((compiler, codeGen) =>
        {
            ref var local = ref compiler.lvaTable[0];
            local.Type = TYP_REF;
            local.lvLRACandidate = true;
            local.RegNum = REG_RBX;
            local._lvLiveInOutOfHandler = handlerLive;
            assert(compiler.fgFirstBB is not null);
            compiler.fgFirstBB.bbLiveIn = VarSetOps.MakeSingleton(compiler, 0);

            codeGen.genCheckUseBlockInit();

            Assert.That(local.lvMustInit, Is.True);
            Assert.That(codeGen.InitStkLclCnt, Is.EqualTo((uint)slots));
        });
    }

    [Test]
    public static void PointerSpillTempsEachContributeOneSlot()
    {
        WithFrame((compiler, codeGen) =>
        {
            codeGen.RegSet.tmpBeginPreAllocateTemps();
            codeGen.RegSet.tmpPreAllocateTemps(TYP_REF, 3);
            codeGen.RegSet.tmpPreAllocateTemps(TYP_BYREF, 2);
            codeGen.RegSet.tmpPreAllocateTemps(TYP_LONG, 4);

            codeGen.genCheckUseBlockInit();

            Assert.That(codeGen.InitStkLclCnt, Is.EqualTo(5u));
            Assert.That(codeGen.UseBlockInit, Is.True);
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    public static void ExcludedLocalsClearStaleInitializationRequirements(int exclusion)
    {
        WithFrame((compiler, codeGen) =>
        {
            compiler.info.compInitMem = true;
            ref var local = ref compiler.lvaTable[0];
            local.lvMustInit = true;
            switch (exclusion)
            {
                case 0:
                {
                    local.lvHasExplicitInit = true;
                    break;
                }
                case 1:
                {
                    local.lvIsTemp = true;
                    break;
                }
                case 2:
                {
                    local.lvIsParam = true;
                    break;
                }
                case 3:
                {
                    local.lvIsParamRegTarget = true;
                    break;
                }
                case 4:
                {
                    compiler.lvaGSSecurityCookie = 0;
                    break;
                }
                case 5:
                {
                    local.lvOnFrame = false;
                    break;
                }
            }

            codeGen.genCheckUseBlockInit();

            Assert.That(local.lvMustInit, Is.False);
            Assert.That(codeGen.InitStkLclCnt, Is.Zero);
        });
    }

    [Test]
    public static void HomingCandidatesIncludeLiveArgumentsAndModifiedRegistersButExcludeReservedRegisters()
    {
        WithFrame((compiler, codeGen) =>
        {
            compiler.lvaDoneFrameLayout = Compiler.INITIAL_FRAME_LAYOUT;
            compiler.srbmIntCalleeTrash = SRBM_INT_CALLEE_TRASH_INIT;
            compiler.srbmFltCalleeTrash = SRBM_FLT_CALLEE_TRASH_INIT;
            compiler.srbmMskCalleeTrash = SRBM_MSK_CALLEE_TRASH_INIT;
            codeGen.CopyRegisterInfo();
            codeGen.CalleeRegArgMaskLiveIn = RBM_R12;
            codeGen.RegSet.rsSetRegsModified(RBM_RBX);
            codeGen.RegSet.rsMaskResvd = RBM_RAX | RBM_R12;

            var trash = new regMaskTP(SRBM_INT_CALLEE_TRASH_INIT | SRBM_FLT_CALLEE_TRASH_INIT,
                SRBM_MSK_CALLEE_TRASH_INIT);
            Assert.That(codeGen.genGetParameterHomingTempRegisterCandidates(),
                Is.EqualTo((trash | RBM_R12 | RBM_RBX) & ~(RBM_RAX | RBM_R12)));
        });
    }

    private static void WithFrame(Action<Compiler, CodeGen> action)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaGSSecurityCookie = BAD_VAR_NUM;
            compiler.lvaInlinedPInvokeFrameVar = BAD_VAR_NUM;
            compiler.lvaStubArgumentVar = BAD_VAR_NUM;
            compiler.lvaRetAddrVar = BAD_VAR_NUM;
            compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
            compiler.fgFirstBB = new BasicBlock(null, null) { bbLiveIn = VarSetOps.MakeEmpty(compiler) };
            compiler.lvaTable[0].RegNum = REG_STK;
            action(compiler, codeGen);
        });
    }
}
