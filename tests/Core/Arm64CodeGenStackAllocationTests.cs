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
using static RyuJitSharp.CorJitResult;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class Arm64CodeGenStackAllocationTests
{
    [TestCase(0u, false)]
    [TestCase(0u, true)]
    [TestCase(16u, true)]
    [TestCase(3584u, true)]
    [TestCase(3584u, false)]
    public static void PublicFrameAllocationOnlyProbesAndPreservesUntouchedScratch(uint frameSize, bool zeroed)
    {
        WithCodeGen((_, codeGen) =>
        {
            codeGen.Emitter.emitBegProlog();
            var actualZeroed = zeroed;
            codeGen.genAllocLclFrame(frameSize, REG_R9, ref actualZeroed, RBM_NONE);

            Assert.That(actualZeroed, Is.EqualTo(zeroed));
            Assert.That(Descriptors(codeGen), Is.Empty);
        });
    }

    [TestCase(3600u, false, 1)]
    [TestCase(4096u, false, 1)]
    [TestCase(8192u, false, 2)]
    public static void ProbeRefWritesRecordNativeUnwindPadding(uint frameSize, bool expectedZeroed, int probes)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            codeGen.Emitter.emitBegProlog();
            compiler.unwindBegProlog();
            var unwindInfo = compiler.funCurrentFunc().GetUnwindInfo();
            var initialLocation = unwindInfo.GetCurrentEmitterLocation()
                ?? throw new AssertionException("Unwind prolog location was not captured.");
            var actualZeroed = true;
            codeGen.genAllocLclFrame(frameSize, REG_R9, ref actualZeroed, RBM_NONE);

            Assert.That(actualZeroed, Is.EqualTo(expectedZeroed));
            var descriptors = Descriptors(codeGen);
            var loads = descriptors.Where(id => id.idIns() == INS_ldr).ToArray();
            Assert.That(loads, Has.Length.EqualTo(probes));
            Assert.That(descriptors.Count, Is.GreaterThan(probes));
            Assert.That(descriptors[^1].idIns(), Is.EqualTo(INS_ldr));
            Assert.That(loads.All(id => id.idOpSize() == EA_4BYTE && id.idReg1() == REG_ZR), Is.True);
            Assert.That(descriptors.Where(id => id.idIns() != INS_ldr)
                .All(id => id.idReg1() == REG_R9), Is.True);
            var currentLocation = unwindInfo.GetCurrentEmitterLocation()
                ?? throw new AssertionException("Unwind padding did not capture the emitter location.");
            Assert.That(initialLocation.IsCurrentLocation(codeGen.Emitter), Is.False);
            Assert.That(currentLocation.IsCurrentLocation(codeGen.Emitter), Is.True);
            compiler.unwindEndProlog();
        });
    }

    [TestCase(0, INS_mov)]
    [TestCase(16, INS_add)]
    public static void PublicFramePointerDispatchUsesArm64InstructionForms(int delta, instruction expected)
    {
        WithCodeGen((_, codeGen) =>
        {
            codeGen.Emitter.emitBegProlog();
            codeGen.genEstablishFramePointer(delta, reportUnwindData: false);

            var descriptor = Descriptors(codeGen).Single();
            Assert.That(descriptor.idIns(), Is.EqualTo(expected));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_FPBASE));
            Assert.That(descriptor.idOpSize(), Is.EqualTo(EA_PTRSIZE));
            if (delta != 0)
            {
                Assert.That(Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)delta));
            }
        });
    }

    [TestCase(0, INS_mov)]
    [TestCase(16, INS_add)]
    public static void FramePointerSetupRecordsTheRequestedUnwindData(
        int delta, instruction expectedInstruction)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            codeGen.Emitter.emitBegProlog();
            compiler.unwindBegProlog();
            var unwindInfo = compiler.funCurrentFunc().GetUnwindInfo();
            var initialLocation = unwindInfo.GetCurrentEmitterLocation()
                ?? throw new AssertionException("Unwind prolog location was not captured.");

            codeGen.genEstablishFramePointer(delta, reportUnwindData: true);

            var descriptor = Descriptors(codeGen).Single();
            Assert.That(descriptor.idIns(), Is.EqualTo(expectedInstruction));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_FPBASE));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_ZR));
            var currentLocation = unwindInfo.GetCurrentEmitterLocation()
                ?? throw new AssertionException("Frame-register unwind code did not capture the emitter location.");
            Assert.That(initialLocation.IsCurrentLocation(codeGen.Emitter), Is.False);
            Assert.That(currentLocation.IsCurrentLocation(codeGen.Emitter), Is.True);
            compiler.unwindEndProlog();
        });
    }

    [TestCase(0, INS_mov, 1)]
    [TestCase(16, INS_ldr, 2)]
    [TestCase(256, INS_ldr, 2)]
    [TestCase(257, INS_ldp, 2)]
    [TestCase(512, INS_ldp, 2)]
    public static void ContainedLocalHeapUsesNativePostIndexRangesWithoutZeroing(
        int amount, instruction expectedFirst, int count)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.compLocallocUsed = true;
            compiler.info.compInitMem = true;
            codeGen.resetFramePointerUsedWritePhase();
            codeGen.IsFramePointerUsed = true;
            var size = compiler.gtNewIconNode(TYP_I_IMPL, amount);
            size.IsContained = true;
            var tree = new GenTreeUnOp(GT_LCLHEAP, TYP_I_IMPL, size) { RegNum = REG_R0 };

            codeGen.genLclHeap(tree);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(count));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(expectedFirst));
            Assert.That(descriptors.Any(id => id.idIns() == INS_stp), Is.False);
            if (amount != 0)
            {
                var aligned = (amount + 15) & -16;
                var encodedOffset = expectedFirst == INS_ldp ? -aligned / 8 : -aligned;
                Assert.That(Emitter.emitGetInsSC(descriptors[0]), Is.EqualTo((nint)encodedOffset));
                Assert.That(descriptors[0].idInsOpt(), Is.EqualTo(insOpts.INS_OPTS_POST_INDEX));
                Assert.That(descriptors[^1].idIns(), Is.EqualTo(INS_mov));
                Assert.That(descriptors[^1].idReg1(), Is.EqualTo(REG_R0));
                if (expectedFirst == INS_ldp)
                {
                    Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R0));
                    Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_ZR));
                }
                else
                {
                    Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_ZR));
                }
            }
        });
    }

    [TestCase(16, 16, 1)]
    [TestCase(3584, 3584, 1)]
    [TestCase(3600, 0, 2)]
    [TestCase(4096, 0, 2)]
    [TestCase(4097, 1, 2)]
    [TestCase(8192, 0, 3)]
    public static void PageProbeChainRetainsArm64BoundaryAndProbeBeforeSubtract(
        int amount, int lastTouch, int probes)
    {
        WithCodeGen((_, codeGen) =>
        {
            var result = ProbeLoop(codeGen, -amount, REG_R9);
            var descriptors = Descriptors(codeGen);

            Assert.That(result, Is.EqualTo((nint)lastTouch));
            Assert.That(descriptors.Count(id => id.idIns() == INS_ldr), Is.EqualTo(probes));
            Assert.That(descriptors.Count(id => id.idIns() == INS_sub), Is.EqualTo((amount + 4095) / 4096));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_ldr));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R9));
            Assert.That(descriptors[^1].idIns(), Is.EqualTo(lastTouch == 0 ? INS_ldr : INS_sub));
        });
    }

    [TestCase(INS_add, -16, INS_sub, 16)]
    [TestCase(INS_sub, -16, INS_add, 16)]
    [TestCase(INS_add, 4095, INS_add, 4095)]
    public static void ConstantInstructionPreservesNegativeAliasAndImmediateFit(
        instruction ins, int immediate, instruction expected, int encoded)
    {
        WithCodeGen((_, codeGen) =>
        {
            var fits = InstructionWithConstant(codeGen, ins, EA_8BYTE,
                REG_R0, REG_R1, immediate, REG_R9, false);
            var descriptor = Descriptors(codeGen).Single();

            Assert.That(fits, Is.True);
            Assert.That(descriptor.idIns(), Is.EqualTo(expected));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R0));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_R1));
            Assert.That(Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)encoded));
        });
    }

    [Test]
    public static void ConstantInstructionMaterializesNonencodableImmediateBeforeRegisterForm()
    {
        WithCodeGen((_, codeGen) =>
        {
            var fits = InstructionWithConstant(codeGen, INS_add, EA_8BYTE,
                REG_R0, REG_R1, 4097, REG_R9, false);
            var descriptors = Descriptors(codeGen);

            Assert.That(fits, Is.False);
            Assert.That(descriptors, Has.Count.GreaterThan(1));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R9));
            Assert.That(descriptors[^1].idIns(), Is.EqualTo(INS_add));
            Assert.That(descriptors[^1].idReg1(), Is.EqualTo(REG_R0));
            Assert.That(descriptors[^1].idReg2(), Is.EqualTo(REG_R1));
            Assert.That(descriptors[^1].idReg3(), Is.EqualTo(REG_R9));
        });
    }

    [TestCase(28, REG_R9, true, 1, 2, 0)]
    [TestCase(32, REG_R11, false, 1, 0, 0)]
    [TestCase(192, REG_R11, false, 2, 0, 1)]
    public static void BlockInitializationUsesArm64ZeroAndPairStorePaths(
        int size, regNumber initReg, bool initRegZeroed, int pairStores, int scalarStores, int loopBranches)
    {
        WithCodeGen((_, codeGen) =>
        {
            codeGen.Emitter.emitBegProlog();
            SetUseBlockInit(codeGen, true);

            codeGen.genZeroInitFrameUsingBlockInit(size, 0, initReg, ref initRegZeroed);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors.Count(id => id.idIns() == INS_stp), Is.EqualTo(pairStores));
            Assert.That(descriptors.Count(id => id.idIns() == INS_str), Is.EqualTo(scalarStores));
            Assert.That(descriptors.Count(id => id.idIns() == INS_movi),
                Is.EqualTo(size == 28 ? 0 : 1));
            Assert.That(descriptors.Count(id => id.idIns() == INS_bge), Is.EqualTo(loopBranches));
            Assert.That(descriptors.Any(id => id.idIns() == INS_dczva), Is.False);
            Assert.That(initRegZeroed, Is.False);
        });
    }

    [TestCase(-512L, EA_8BYTE, true)]
    [TestCase(-520L, EA_8BYTE, false)]
    [TestCase(504L, EA_8BYTE, true)]
    [TestCase(512L, EA_8BYTE, false)]
    [TestCase(-4L, EA_8BYTE, false)]
    [TestCase(-1024L, EA_16BYTE, true)]
    [TestCase(1008L, EA_16BYTE, true)]
    [TestCase(1024L, EA_16BYTE, false)]
    public static void PairOffsetKeepsSignedScaledEndpoints(long immediate, emitAttr size, bool expected)
    {
        Assert.That(PairOffset(null, immediate, size), Is.EqualTo(expected));
    }

    [TestCase(REG_R0, REG_R9)]
    [TestCase(REG_R9, REG_R10)]
    [TestCase(REG_R19, REG_R29)]
    public static void ProbeLimitSelectionUsesLowestAvailableIntegerRegister(regNumber first, regNumber second)
    {
        var mask = regMaskTP.CreateFromRegNum(first, first.SingleTypeMask) |
            regMaskTP.CreateFromRegNum(second, second.SingleTypeMask);
        var selected = LowestBit(null, mask);

        Assert.That(selected, Is.EqualTo(regMaskTP.CreateFromRegNum(first, first.SingleTypeMask)));
        Assert.That(RegisterFromMask(null, selected), Is.EqualTo(first));
    }

    private static void WithCodeGen(Action<Compiler, CodeGen> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        compiler.eeInfoInitialized = true;
        compiler.eeInfo.osPageSize = 4096;
        compiler.compFuncInfos =
        [
            new FuncInfoDsc
            {
                funKind = FuncKind.FUNC_ROOT,
                uwi = new UnwindInfo(),
            },
        ];
        compiler.compFuncInfoCount = 1;
        compiler.fgFuncletsCreated = true;
        var currentBlock = new BasicBlock(null, null);
        compiler.fgFirstBB = currentBlock;
        compiler.fgLastBB = currentBlock;
        compiler.compCurBB = currentBlock;
        compiler.lvaOutgoingArgSpaceSize.Value = 0;
        compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
        JitTls.Compiler = compiler;
        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            codeGen.RegSet.rsClearRegsModified();
            codeGen.resetFramePointerUsedWritePhase();
            codeGen.IsFramePointerUsed = false;
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);
            codeGen.Emitter.emitBegCG(compiler, default);
            codeGen.Emitter.Init();
            codeGen.Emitter.emitBegFN(false
#if DEBUG
                , true
#endif
                );
            action(compiler, codeGen);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private static List<Emitter.instrDesc> Descriptors(CodeGen codeGen)
    {
        var emitter = codeGen.Emitter;
        var descriptors = new List<Emitter.instrDesc>();
        var currentGroup = emitter.emitCurIG;

        for (var group = FirstGroup(emitter); group is not null; group = group.igNext)
        {
            if (group == currentGroup)
            {
                descriptors.AddRange(CurrentDescriptors(emitter)
                    ?? throw new AssertionException("Missing current descriptor buffer."));
            }
            else if (group.igInsCnt > 0)
            {
                descriptors.AddRange(group.igData
                    ?? throw new AssertionException("Missing saved descriptor buffer."));
            }
        }

        return descriptors;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitIGlist")]
    private static extern ref insGroup? FirstGroup(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "treeLifeUpdater")]
    private static extern ref TreeLifeUpdater? LifeUpdater(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genStackPointerConstantAdjustmentLoopWithProbe")]
    private static extern nint ProbeLoop(CodeGen codeGen, nint delta, regNumber tmpReg);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genInstrWithConstant")]
    private static extern bool InstructionWithConstant(CodeGen codeGen, instruction ins, emitAttr attr,
        regNumber reg1, regNumber reg2, nint imm, regNumber tmpReg, bool inUnwindRegion);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "canEncodeLoadOrStorePairOffsetArm64")]
    private static extern bool PairOffset(CodeGen? codeGen, long immediate, emitAttr size);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "genFindLowestBitArm64")]
    private static extern regMaskTP LowestBit(CodeGen? codeGen, regMaskTP mask);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "genRegNumFromMaskArm64")]
    private static extern regNumber RegisterFromMask(CodeGen? codeGen, regMaskTP mask);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_UseBlockInit")]
    private static extern void SetUseBlockInit(CodeGen codeGen, bool value);
}
#endif
