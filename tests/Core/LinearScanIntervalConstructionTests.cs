// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.LsraGlobals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.gtCallTypes;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinearScanIntervalConstructionTests
{
    [TestCase(false)]
#if DEBUG
    [TestCase(true)]
#endif
    public static void ConstructedIntervalsAllocateAndResolveWithNativeSpillPolicy(bool stressSpills)
    {
        WithAllocator((compiler, allocator) => {
            var returnType = new ReturnTypeDesc();
            returnType.InitializeReturnType(compiler, TYP_INT, null, CorInfoCallConvExtension.Managed);
            compiler.compRetTypeDesc = returnType;
            var block = CreateBlocks(compiler, BBJ_RETURN)[0];
            var left = compiler.gtNewIconNode(TYP_INT, 3);
            var right = compiler.gtNewIconNode(TYP_INT, 5);
            var add = new GenTreeOp(GT_ADD, TYP_INT, left, right);
            var ret = new GenTreeUnOp(GT_RETURN, TYP_INT, add);
            block.InsertAtEnd(left);
            block.InsertAtEnd(right);
            block.InsertAtEnd(add);
            block.InsertAtEnd(ret);
#if DEBUG
            if (stressSpills)
            {
                StressMask(allocator) = 0xc00;
            }
#endif

            var status = allocator.DoRegisterAllocation();

            Assert.That(status, Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.compRegAllocDone, Is.True);
            Assert.That(compiler.mostRecentlyActivePhase, Is.EqualTo(Phases.PHASE_LINEAR_SCAN_RESOLVE));
            Assert.That(AllocationPassComplete(allocator), Is.True);
            Assert.That(left.RegNum, Is.Not.EqualTo(REG_NA));
            Assert.That(right.RegNum, Is.Not.EqualTo(REG_NA));
            Assert.That(add.RegNum, Is.Not.EqualTo(REG_NA));
            var spillCount = 0;
            var reloadCount = 0;
            foreach (var node in block)
            {
                if ((node.Flags & GTF_SPILL) != 0)
                {
                    spillCount++;
                    Assert.That(node.IsReuseRegVal, Is.False);
                }
                if (node.Oper is GT_RELOAD)
                {
                    reloadCount++;
                }
            }
            Assert.That(spillCount > 0, Is.EqualTo(stressSpills));
            Assert.That(reloadCount > 0, Is.EqualTo(stressSpills));
            Assert.That(compiler.codeGen!.RegSet.HasComputedTmpSize, Is.True);
            Assert.That(compiler.codeGen.RegSet.tmpTotalSize > 0, Is.EqualTo(stressSpills));
        });
    }

    [TestCase(TYP_FLOAT)]
    [TestCase(TYP_DOUBLE)]
    public static void ResolutionRecordsInternalRegistersWithoutOverwritingFloatingResults(var_types type)
    {
        WithAllocator((compiler, allocator) => {
            var returnType = new ReturnTypeDesc();
            returnType.InitializeReturnType(compiler, type, null, CorInfoCallConvExtension.Managed);
            compiler.compRetTypeDesc = returnType;
            var block = CreateBlocks(compiler, BBJ_RETURN)[0];
            var value = compiler.gtNewDconNode(type, 3.5);
            var finite = new GenTreeUnOp(GT_CKFINITE, type, value);
            var ret = new GenTreeUnOp(GT_RETURN, type, finite);
            block.InsertAtEnd(value);
            block.InsertAtEnd(finite);
            block.InsertAtEnd(ret);

            _ = allocator.DoRegisterAllocation();

            var internalRegisters = compiler.codeGen!.InternalRegisters.GetAll(finite);
            Assert.That(internalRegisters.IsEmpty, Is.False);
            Assert.That((internalRegisters & new regMaskTP(SRBM_ALLFLOAT_INIT)).IsEmpty, Is.True);
            Assert.That(finite.RegNum, Is.EqualTo(REG_XMM0));
            Assert.That(compiler.codeGen.InternalRegisters.GetAll(value).IsEmpty, Is.True);
        });
    }

    [TestCase(TYP_INT)]
    [TestCase(TYP_LONG)]
    public static void AllocationResolvesASingleRegisterCandidate(var_types type)
    {
        WithAllocator((compiler, allocator) => {
            var returnType = new ReturnTypeDesc();
            returnType.InitializeReturnType(compiler, type, null, CorInfoCallConvExtension.Managed);
            compiler.compRetTypeDesc = returnType;
            var block = CreateBlocks(compiler, BBJ_RETURN)[0];
            var value = compiler.gtNewIconNode(type, 3);
            var ret = new GenTreeUnOp(GT_RETURN, type, value);
            block.InsertAtEnd(value);
            block.InsertAtEnd(ret);
#if DEBUG
            compiler.verbose = true;
#endif

            _ = allocator.DoRegisterAllocation();

            Assert.That(value.RegNum, Is.EqualTo(REG_INTRET));
            Assert.That(compiler.compRegAllocDone, Is.True);
        });
    }

    [Test]
    public static void AllocationDisablesEnregistrationWhenThereAreNoTrackedLocals()
    {
        WithAllocator((compiler, allocator) => {
            _ = CreateBlocks(compiler, BBJ_RETURN);
            compiler.codeGen!.RegSet.rsSetRegsModified(RBM_RBX);

            _ = allocator.DoRegisterAllocation();

            Assert.That(compiler.compRegAllocDone, Is.True);
            Assert.That(EnregisterLocalVars(allocator), Is.False);
            Assert.That(compiler.codeGen.RegSet.rsRegsModified(RBM_RBX), Is.False);
        }, enregister: true);
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public static void UnsupportedAllocationModeRejectsBeforeMutation(bool optimized, bool trackedLocals)
    {
        WithAllocator((compiler, allocator) => {
            compiler.lvaTrackedCount = trackedLocals ? 1 : 0;
            compiler.codeGen!.RegSet.rsSetRegsModified(RBM_RBX);
            var previousPhase = compiler.mostRecentlyActivePhase;

            var exception = Assert.Throws<FatalJitException>(() => allocator.DoRegisterAllocation());

            Assert.That(exception!.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
            Assert.That(compiler.compRegAllocDone, Is.False);
            Assert.That(compiler.mostRecentlyActivePhase, Is.EqualTo(previousPhase));
            Assert.That(EnregisterLocalVars(allocator), Is.True);
            Assert.That(allocator.refPositions, Is.Empty);
            Assert.That(compiler.codeGen.RegSet.rsRegsModified(RBM_RBX), Is.True);
        }, enregister: true, minOpts: !optimized);
    }

    [Test]
    public static void EnregisteredModeRejectsBeforeRegisterResolution()
    {
        WithAllocator((compiler, allocator) => {
            _ = Assert.Throws<FatalJitException>(() => ResolveRegisters(allocator));
            Assert.That(compiler.codeGen!.RegSet.HasComputedTmpSize, Is.False);
        }, enregister: true);
    }

    [Test]
    public static void InternalRegistersAccumulateBothMaskBanksForTheOwningNode()
    {
        WithAllocator((compiler, allocator) => {
            var first = compiler.gtNewIconNode(TYP_INT, 1);
            var second = compiler.gtNewIconNode(TYP_INT, 1);
            ref var registers = ref compiler.codeGen!.InternalRegisters;
            var maskRegister = regMaskTP.CreateFromRegNum(REG_K1, genSingleTypeRegMask(REG_K1));
            registers.Add(first, RBM_RAX);
            registers.Add(first, maskRegister);
            registers.Add(second, RBM_RDX);

            Assert.That(registers.GetAll(first), Is.EqualTo(RBM_RAX | maskRegister));
            Assert.That(registers.GetAll(second), Is.EqualTo(RBM_RDX));
        });
    }

    [Test]
    public static void MinimalIntervalsRetainBlockLocationsAndStackLocalLoads()
    {
        WithAllocator((compiler, allocator) => {
            compiler.lvaTable = [new() { Type = TYP_INT, lvLRACandidate = true, lvOnFrame = true }];
            compiler.lvaCount = 1;
            compiler.lvaTable[0].setLvRefCnt(2);
            var returnType = new ReturnTypeDesc();
            returnType.InitializeReturnType(compiler, TYP_INT, null, CorInfoCallConvExtension.Managed);
            compiler.compRetTypeDesc = returnType;
            var blocks = CreateBlocks(compiler, BBJ_ALWAYS, BBJ_RETURN);
            blocks[0].SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(blocks[1], blocks[0]));
            blocks[1].bbSetRunRarely();
            var value = compiler.gtNewIconNode(TYP_INT, 7);
            var store = new GenTreeLclVar(TYP_INT, 0, value);
            blocks[0].InsertAtEnd(value);
            blocks[0].InsertAtEnd(store);
            var load = compiler.gtNewLclvNode(TYP_INT, 0);
            var ret = new GenTreeUnOp(GT_RETURN, TYP_INT, load);
            blocks[1].InsertAtEnd(load);
            blocks[1].InsertAtEnd(ret);

            BuildIntervals(allocator);

            Assert.That(compiler.lvaTable[0].lvLRACandidate, Is.False);
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
            Assert.That(allocator.localVarIntervals, Is.Null);
            var boundaries = allocator.refPositions.FindAll(reference => reference.refType is RefType.RefTypeBB);
            Assert.That(boundaries, Has.Count.EqualTo(2));
            Assert.That(boundaries[0].nodeLocation, Is.Zero);
            Assert.That(boundaries[1].nodeLocation, Is.EqualTo(6));
            Assert.That(FirstColdLocation(allocator), Is.EqualTo(8));
            Assert.That(ReferenceBuildLocation(allocator), Is.EqualTo(12));
            Assert.That(allocator.intervals, Has.Count.EqualTo(2));
            Assert.That(allocator.intervals[0].firstRefPosition?.nodeLocation, Is.EqualTo(3));
            Assert.That(allocator.intervals[0].lastRefPosition?.nodeLocation, Is.EqualTo(4));
            Assert.That(allocator.intervals[1].firstRefPosition?.nodeLocation, Is.EqualTo(9));
            Assert.That(allocator.intervals[1].lastRefPosition?.registerAssignment, Is.EqualTo(SRBM_INTRET));
            Assert.That(ActualRegistersMask(allocator).IsSet(REG_XMM0), Is.False);
            Assert.That(ActualRegistersMask(allocator).IsSet(REG_R15), Is.True);
            Assert.That(ActualRegistersMask(allocator).IsSet(REG_R16), Is.False);
            Assert.That(compiler.compCurBB, Is.SameAs(blocks[1]));

            allocator.initVarRegMaps();
            AllocateRegisters(allocator);
            AllocationPassComplete(allocator) = true;
            ResolveRegisters(allocator);
            Assert.That(compiler.lvaTable[0].lvOnFrame, Is.True);
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
            Assert.That(load.RegNum, Is.EqualTo(REG_INTRET));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void AFinalBackedgeGetsItsOwnBoundaryAndPreservesRegisterClasses(bool floating)
    {
        WithAllocator((compiler, allocator) => {
            var block = CreateBlocks(compiler, BBJ_ALWAYS)[0];
            block.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(block, block));
            GenTree value = floating
                ? compiler.gtNewDconNode(TYP_DOUBLE, 3.5)
                : compiler.gtNewIconNode(TYP_INT, 7);
            value.IsUnusedValue = true;
            block.InsertAtEnd(value);

            BuildIntervals(allocator);

            var boundaries = allocator.refPositions.FindAll(reference => reference.refType is RefType.RefTypeBB);
            Assert.That(boundaries, Has.Count.EqualTo(2));
            Assert.That(boundaries[1].nodeLocation, Is.EqualTo(4));
            Assert.That(ActualRegistersMask(allocator).IsSet(REG_XMM0), Is.EqualTo(floating));
            Assert.That(ActualRegistersMask(allocator).IsSet(REG_K0), Is.False);

            allocator.initVarRegMaps();
            AllocateRegisters(allocator);
            AllocationPassComplete(allocator) = true;
            ResolveRegisters(allocator);
            Assert.That(value.RegNum, Is.Not.EqualTo(REG_NA));
        });
    }

    [TestCase(true, false, false, false)]
    [TestCase(true, true, false, true)]
    [TestCase(false, false, false, true)]
    [TestCase(true, false, true, true)]
    public static void IncomingMasksFollowMappedLivenessAndPhysicalRegisterNumbers(
        bool mappedIsField, bool mappedIsUsed, bool jumpUsed, bool expectedIntegerLive)
    {
        WithAllocator((compiler, allocator) => {
            compiler.info.compArgsCount = 2;
            compiler.info.compPublishStubParam = true;
            compiler.compJmpOpUsed = jumpUsed;
            compiler.lvaTable = [
                new() { Type = TYP_LONG, lvIsParam = true, lvTracked = true, _varIndex = 0 },
                new() { Type = TYP_FLOAT, lvIsParam = true },
                new() { Type = TYP_LONG, lvTracked = true, _varIndex = 1, lvIsStructField = mappedIsField },
            ];
            compiler.lvaCount = 3;
            compiler.lvaTrackedCount = 2;
            compiler.lvaTrackedCountInSizeTUnits = 1;
            compiler.lvaTable[0].setLvRefCnt(1);
            compiler.lvaTable[2].setLvRefCnt((ushort)(mappedIsUsed ? 1 : 0));
            compiler.lvaParameterPassingInfo = [
                AbiPassingInformation.FromSegment(compiler, false, AbiPassingSegment.InRegister(REG_RCX, 0, 8)),
                AbiPassingInformation.FromSegment(compiler, false, AbiPassingSegment.InRegister(REG_XMM1, 0, 4)),
            ];
            compiler._paramRegLocalMappings = [
                new ParameterRegisterLocalMapping(AbiPassingSegment.InRegister(REG_RCX, 0, 8), 2, 0),
            ];
            _ = CreateBlocks(compiler, BBJ_RETURN);

            BuildIntervals(allocator);

            var registers = compiler.codeGen!.CalleeRegArgMaskLiveIn;
            Assert.That(registers.IsSet(REG_RCX), Is.EqualTo(expectedIntegerLive));
            Assert.That(registers.IsSet(REG_XMM1), Is.True);
            Assert.That(registers.IsSet(REG_RDX), Is.False);
            Assert.That(registers.IsSet(REG_SECRET_STUB_PARAM), Is.True);
            Assert.That(allocator.refPositions.Exists(reference => reference.refType is RefType.RefTypeParamDef), Is.False);
        });
    }

    [TestCase(false, false, false)]
    [TestCase(true, false, false)]
    [TestCase(true, false, true)]
    [TestCase(true, true, false)]
    public static void PrologPoisonAndReturnCookieKillsHaveSeparateLocations(bool debugCode, bool tailJump, bool osr)
    {
        WithAllocator((compiler, allocator) => {
            compiler.opts.compDbgCode = debugCode;
            if (osr)
            {
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
            }
            compiler.info.compInitMem = false;
            compiler.compNeedsGSSecurityCookie = true;
            var block = CreateBlocks(compiler, BBJ_RETURN)[0];
            if (tailJump)
            {
                block.SetFlags(BBF_HAS_JMP);
                block.InsertAtEnd(new GenTreeVal(GT_JMP, TYP_VOID, 0));
            }
            else
            {
                block.InsertAtEnd(new GenTreeUnOp(GT_RETURN, TYP_VOID, null));
            }

            BuildIntervals(allocator);

            var poison = debugCode && !osr;
            var kills = allocator.refPositions.FindAll(reference => reference.refType is RefType.RefTypeKill);
            Assert.That(kills, Has.Count.EqualTo(poison ? 2 : 1));
            if (poison)
            {
                Assert.That(kills[0].nodeLocation, Is.EqualTo(3));
                Assert.That(kills[0].getKilledRegisters(), Is.EqualTo(RBM_EDI | RBM_ECX | RBM_EAX));
            }
            Assert.That(kills[^1].nodeLocation, Is.EqualTo(poison ? 7 : 5));
            Assert.That(kills[^1].getKilledRegisters(), Is.EqualTo(tailJump ? RBM_R10 : RBM_R9));
        });
    }

    [TestCase(false, false, REG_R9)]
    [TestCase(true, false, REG_R10)]
    [TestCase(true, true, REG_R11)]
    public static void CookieRegistersPreserveReturnAndTailcallContracts(
        bool tailCall, bool secretParameter, regNumber expected)
    {
        WithAllocator((compiler, allocator) => {
            var call = compiler.gtNewCallNode(TYP_VOID, CT_USER_FUNC, null);
            if (secretParameter)
            {
                _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(
                    compiler.gtNewIconNode(TYP_I_IMPL, 1)).WithWellKnownArg(WellKnownArg.SecretStubParam));
            }
            Assert.That(compiler.codeGen!.genGetGSCookieTempRegs(tailCall, call),
                Is.EqualTo(regMaskTP.CreateFromRegNum(expected, genSingleTypeRegMask(expected))));
        });
    }

    [Test]
    public static void EnregisteredModeRejectsBeforeBuildingOrChangingCodegenState()
    {
        WithAllocator((compiler, allocator) => {
            compiler.codeGen!.CalleeRegArgMaskLiveIn = RBM_RCX;
            _ = Assert.Throws<FatalJitException>(() => BuildIntervals(allocator));
            Assert.That(allocator.refPositions, Is.Empty);
            Assert.That(compiler.codeGen.CalleeRegArgMaskLiveIn, Is.EqualTo(RBM_RCX));
        }, enregister: true);
    }

    private static BasicBlock[] CreateBlocks(Compiler compiler, params BBKinds[] kinds)
    {
        var blocks = new BasicBlock[kinds.Length];
        for (var index = 0; index < blocks.Length; index++)
        {
            var block = BasicBlock.New(compiler, kinds[index]);
            blocks[index] = block;
            block.SetFlags(BBF_IS_LIR);
            block.bbRefs = index == 0 ? 1 : 0;
            if (index > 0)
            {
                blocks[index - 1].Next = block;
                block.Prev = blocks[index - 1];
            }
        }
        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];
        compiler.fgBBcount = blocks.Length;
        compiler.fgBBNumMax = blocks[^1].bbNum;
        return blocks;
    }

    private static void WithAllocator(Action<Compiler, LinearScan> action, bool enregister = false, bool minOpts = true)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minOpts);
        compiler.opts.compFlags = (minOpts ? CLFLG_MINOPT : 0) | (enregister ? CLFLG_REGVAR : 0);
        compiler.fgPredsComputed = true;
        compiler.lvaTable = [];
        compiler.compHndBBtab = [];
        compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        CompilerAllIntRegs(compiler) = SRBM_ALLINT_INIT;
        CompilerAllFloatRegs(compiler) = SRBM_ALLFLOAT_INIT;
        CompilerAllMaskRegs(compiler) = SRBM_ALLMASK_INIT;
        CompilerFloatCalleeTrash(compiler) = SRBM_FLT_CALLEE_TRASH_INIT;
        CompilerLastIntReg(compiler) = REG_R15;
        JitTls.Compiler = compiler;
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        codeGen.CopyRegisterInfo();
        codeGen.IsFramePointerRequired = false;
        codeGen.IsFramePointerUsed = false;
        codeGen.IsFrameRequired = false;
        codeGen.RegSet.rsClearRegsModified();
        try
        {
            var allocator = new LinearScan(compiler);
            compiler.rpMustCreateEBPCalled = true;
            var returnType = new ReturnTypeDesc();
            returnType.InitializeReturnType(compiler, TYP_VOID, null, CorInfoCallConvExtension.Managed);
            compiler.compRetTypeDesc = returnType;
            action(compiler, allocator);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildIntervalsMinimal")]
    private static extern void BuildIntervals(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "allocateRegistersMinimal")]
    private static extern void AllocateRegisters(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "resolveRegistersMinimal")]
    private static extern void ResolveRegisters(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_allocationPassComplete")]
    private static extern ref bool AllocationPassComplete(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_enregisterLocalVars")]
    private static extern ref bool EnregisterLocalVars(LinearScan allocator);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_lsraStressMask")]
    private static extern ref int StressMask(LinearScan allocator);
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_firstColdLocation")]
    private static extern ref uint FirstColdLocation(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_referenceBuildLocation")]
    private static extern ref uint ReferenceBuildLocation(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_actualRegistersMask")]
    private static extern ref regMaskTP ActualRegistersMask(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmFltCalleeTrash")]
    private static extern ref regMask CompilerFloatCalleeTrash(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "regIntLast")]
    private static extern ref regNumber CompilerLastIntReg(Compiler compiler);
}
