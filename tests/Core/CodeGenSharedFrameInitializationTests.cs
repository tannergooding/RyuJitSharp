// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CodeGenSharedFrameInitializationTests
{
    [TestCase(4)]
    [TestCase(16)]
    [TestCase(17)]
    [TestCase(32)]
    [TestCase(33)]
    public static void BlockThresholdRetainsTheTargetStoreWidth(int homeSize)
    {
        WithFrame((compiler, codeGen) =>
        {
            compiler.info.compInitMem = true;
            compiler.lvaTable[0] = new LclVarDsc
            {
                Type = TYP_STRUCT, Layout = new ClassLayout(homeSize),
                RegNum = REG_STK, lvOnFrame = true,
            };
            var slots = (uint)(roundUp(homeSize, TARGET_POINTER_SIZE) / sizeof(int));
#if TARGET_WASM
            var expectedBlock = slots > 0;
#elif TARGET_64BIT && !TARGET_AMD64
            var expectedBlock = slots > 8;
#else
            var expectedBlock = slots > 4;
#endif

#if TARGET_ARM
            if (expectedBlock)
            {
                var error = Assert.Throws<FatalJitException>(codeGen.genCheckUseBlockInit);
                Assert.That(error?.Message, Is.EqualTo("ARM prespilled unmapped register selection is not ported."));
            }
            else
#endif
            {
                codeGen.genCheckUseBlockInit();
            }

            Assert.That(compiler.lvaTable[0].lvMustInit, Is.True);
            Assert.That(codeGen.InitStkLclCnt, Is.EqualTo(slots));
            Assert.That(codeGen.UseBlockInit, Is.EqualTo(expectedBlock));
        });
    }

#if TARGET_WASM
    [TestCase(TYP_REF)]
    [TestCase(TYP_BYREF)]
    public static void WasmTrackedGcHomesAreInitializedEvenWhenDeadAtEntry(var_types type)
    {
        WithFrame((compiler, codeGen) =>
        {
            compiler.lvaTable[0] = new LclVarDsc
            {
                Type = type, RegNum = REG_STK, lvOnFrame = true, lvTracked = true,
            };
            var firstBlock = compiler.fgFirstBB ?? throw new AssertionException("Missing entry block.");
            firstBlock.bbLiveIn = VarSetOps.MakeEmpty(compiler);

            codeGen.genCheckUseBlockInit();

            Assert.That(compiler.lvaTable[0].lvMustInit, Is.True);
            Assert.That(codeGen.InitStkLclCnt, Is.EqualTo((uint)(TARGET_POINTER_SIZE / sizeof(int))));
            Assert.That(codeGen.UseBlockInit, Is.True);
        });
    }
#endif

#if TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64
    [TestCase(false)]
    [TestCase(true)]
    public static void ArchitecturalZeroRegisterDoesNotTouchScratchState(bool initiallyZero)
    {
        WithFrame((_, codeGen) =>
        {
            var zeroed = initiallyZero;
#if TARGET_ARM64
            var expected = REG_ZR;
#else
            var expected = REG_R0;
#endif

            Assert.That(codeGen.genGetZeroReg(REG_INT_FIRST, ref zeroed), Is.EqualTo(expected));
            Assert.That(zeroed, Is.EqualTo(initiallyZero));
        });
    }
#endif

#if TARGET_AMD64
    [Test]
    public static void OverlappingFloatingMasksInitializeTheRegisterOnlyOnce()
    {
        WithFrame((_, codeGen) =>
        {
            BeginProlog(codeGen);
            var first = regMaskTP.CreateFromRegNum(REG_XMM0, REG_XMM0.SingleTypeMask);
            var second = regMaskTP.CreateFromRegNum(REG_XMM1, REG_XMM1.SingleTypeMask);

            codeGen.genZeroInitFltRegs(first, first | second, REG_RAX);

            var ids = Descriptors(codeGen.Emitter);
            Assert.That(ids.Select(id => id.idIns()), Is.EqualTo((instruction[])[INS_xorps, INS_movaps]));
            Assert.That(ids[1].idReg2(), Is.EqualTo(REG_XMM0));
        });
    }

    [Test]
    public static void FourByteScalarHomeDoesNotUseAPointerSizedStore()
    {
        WithFrame((compiler, codeGen) =>
        {
            compiler.info.compInitMem = true;
            compiler.lvaTable[0] = new LclVarDsc
            {
                Type = TYP_INT, RegNum = REG_STK,
                lvOnFrame = true, lvFramePointerBased = true, StackOffset = -32,
            };
            codeGen.genCheckUseBlockInit();
            BeginProlog(codeGen);
            var zeroed = false;

            codeGen.genZeroInitFrame(-28, -32, REG_RAX, ref zeroed);

            var stores = Descriptors(codeGen.Emitter).Where(id => id.idIns() == INS_mov).ToArray();
            Assert.That(stores.Select(id => id.idOpSize()), Is.EqualTo((emitAttr[])[EA_4BYTE]));
            Assert.That(stores.Select(id => id.idAddr().iiaLclVar.lvaOffset()), Is.EqualTo((uint[])[0]));
            Assert.That(zeroed, Is.True);
        });
    }
#endif

#if TARGET_AMD64 && SWIFT_SUPPORT
    [TestCase(1, true, EA_1BYTE, INS_movzx)]
    [TestCase(2, true, EA_2BYTE, INS_movzx)]
    [TestCase(3, true, EA_4BYTE, INS_mov)]
    [TestCase(4, true, EA_4BYTE, INS_mov)]
    [TestCase(5, true, EA_8BYTE, INS_mov)]
    [TestCase(6, true, EA_8BYTE, INS_mov)]
    [TestCase(7, true, EA_8BYTE, INS_mov)]
    [TestCase(8, true, EA_8BYTE, INS_mov)]
    [TestCase(1, false, EA_1BYTE, INS_movzx)]
    [TestCase(2, false, EA_2BYTE, INS_movzx)]
    [TestCase(3, false, EA_4BYTE, INS_mov)]
    [TestCase(4, false, EA_4BYTE, INS_mov)]
    [TestCase(5, false, EA_8BYTE, INS_mov)]
    [TestCase(6, false, EA_8BYTE, INS_mov)]
    [TestCase(7, false, EA_8BYTE, INS_mov)]
    [TestCase(8, false, EA_8BYTE, INS_mov)]
    public static void StackSegmentsRetainRoundedLoadWidthAndFrameBase(
        int segmentSize, bool useFramePointer, emitAttr size, instruction loadIns)
    {
        WithFrame((compiler, codeGen) =>
        {
            codeGen.resetFramePointerUsedWritePhase();
            codeGen.IsFramePointerUsed = useFramePointer;
            compiler.compCalleeRegsPushed = 0;
            compiler.compLclFrameSize = 32;
            compiler.lvaTable[0] = new LclVarDsc
            {
                Type = TYP_STRUCT, Layout = new ClassLayout(16), RegNum = REG_STK,
                lvOnFrame = true, lvFramePointerBased = useFramePointer, StackOffset = -16,
            };
            BeginProlog(codeGen);
            var segment = AbiPassingSegment.OnStack(24, 8, segmentSize);
            var expectedOffset = 24 - (useFramePointer
                ? codeGen.genCallerSPtoFPdelta : codeGen.genCallerSPtoInitialSPdelta);
            var zeroed = true;

            codeGen.genHomeStackSegment(0, in segment, REG_R10, ref zeroed);

            var ids = Descriptors(codeGen.Emitter);
            Assert.That(ids, Has.Count.EqualTo(2));
            Assert.That(ids[0].idIns(), Is.EqualTo(loadIns));
            Assert.That(ids[0].idOpSize(), Is.EqualTo(size));
            Assert.That(ids[0].idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(useFramePointer ? REG_RBP : REG_RSP));
            Assert.That(Displacement(codeGen.Emitter, ids[0]), Is.EqualTo((nint)expectedOffset));
            Assert.That(ids[1].idIns(), Is.EqualTo(INS_mov));
            Assert.That(ids[1].idOpSize(), Is.EqualTo(size));
            Assert.That(ids[1].idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(8u));
            Assert.That(zeroed, Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void StackSegmentHomingUpdatesOnlyExplicitlyPassedScratchState(bool trackState)
    {
        WithFrame((compiler, codeGen) =>
        {
            compiler.lvaTable[0] = new LclVarDsc
            {
                Type = TYP_STRUCT, Layout = new ClassLayout(8), RegNum = REG_STK,
                lvOnFrame = true, lvFramePointerBased = true, StackOffset = -16,
            };
            BeginProlog(codeGen);
            var segment = AbiPassingSegment.OnStack(8, 0, 8);
            var zeroed = true;

            if (trackState)
            {
                codeGen.genHomeStackSegment(0, in segment, REG_R10, ref zeroed);
            }
            else
            {
                codeGen.genHomeStackSegment(0, in segment, REG_R10);
            }

            Assert.That(Descriptors(codeGen.Emitter), Has.Count.EqualTo(2));
            Assert.That(zeroed, Is.EqualTo(!trackState));
        });
    }
#endif

    private static void WithFrame(Action<Compiler, CodeGen> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info = new Compiler.Info();
        JitFlags flags = default;
        CORINFO_METHOD_INFO methodInfo = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.info.compMethodInfo = &methodInfo;
        compiler.eeInfoInitialized = true;
        compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI;
        compiler.info.compMatchedVM = true;
        JitTls.Compiler = compiler;
        try
        {
            PrepareFrame(compiler);
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
#if TARGET_AMD64
            codeGen.IsFramePointerUsed = true;
            compiler.compCalleeRegsPushed = 0;
            compiler.lvaDoneFrameLayout = Compiler.INITIAL_FRAME_LAYOUT;
            codeGen.RegSet.rsClearRegsModified();
            codeGen.RegSet.ClearMaskVars();
            compiler.lvaDoneFrameLayout = Compiler.FINAL_FRAME_LAYOUT;
            AllIntRegisters(compiler) = SRBM_ALLINT_INIT;
            LastIntRegister(compiler) = REG_R15;
            codeGen.CopyRegisterInfo();
            codeGen.GCInfo.gcTrkStkPtrLcls = VarSetOps.MakeEmpty(compiler);
            codeGen.GCInfo.gcVarPtrSetCur = VarSetOps.MakeEmpty(compiler);
            codeGen.Emitter.emitBegCG(compiler, default);
            codeGen.Emitter.Init();
            codeGen.Emitter.emitBegFN(true
#if DEBUG
                , false
#endif
                );
            codeGen.Emitter.UseVexEncodings = true;
            codeGen.Emitter.UseEvexEncodings = true;
#else
            CurrentGroup(codeGen.Emitter) = new insGroup();
#endif
            action(compiler, codeGen);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private static void PrepareFrame(Compiler compiler)
    {
        compiler.lvaCount = 1;
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        compiler.lvaTrackedToVarNum = [0];
        compiler.lvaTable = [new LclVarDsc { Type = TYP_INT, RegNum = REG_STK, lvOnFrame = true }];
        compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
        compiler.lvaGSSecurityCookie = BAD_VAR_NUM;
        compiler.lvaInlinedPInvokeFrameVar = BAD_VAR_NUM;
        compiler.lvaRetAddrVar = BAD_VAR_NUM;
#if SWIFT_SUPPORT
        compiler.lvaSwiftErrorArg = BAD_VAR_NUM;
#endif
#if FEATURE_FIXED_OUT_ARGS
        compiler.lvaOutgoingArgSpaceVar = BAD_VAR_NUM;
#endif
#if TARGET_ARM64
        compiler.lvaFfrRegister = BAD_VAR_NUM;
#endif
        compiler.fgFirstBB = new BasicBlock(null, null) { bbLiveIn = VarSetOps.MakeEmpty(compiler) };
        compiler.compCurBB = compiler.fgFirstBB;
    }

#if TARGET_AMD64
    private static void BeginProlog(CodeGen codeGen)
    {
        var group = codeGen.Emitter.emitCurIG ?? throw new AssertionException("Missing instruction group.");
        group.igFlags |= InsGroupFlags.Prolog;
    }

    private static List<Emitter.instrDesc> Descriptors(Emitter emitter)
    {
        return CurrentDescriptors(emitter) ?? throw new AssertionException("Missing descriptor buffer.");
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask AllIntRegisters(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "regIntLast")]
    private static extern ref regNumber LastIntRegister(Compiler compiler);

#if SWIFT_SUPPORT
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsAmdAny")]
    private static extern nint Displacement(Emitter emitter, Emitter.instrDesc descriptor);
#endif
#else
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIG")]
    private static extern ref insGroup? CurrentGroup(Emitter emitter);
#endif
}
