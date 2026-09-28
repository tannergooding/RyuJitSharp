// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.LsraGlobals;
using static RyuJitSharp.RefCountState;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using SetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64LinearScanLocalResolutionTests
{
    [Test]
    public static void LocalCandidatesCreateIntegerAndFloatingIntervals()
    {
        WithLocals(2, (compiler, allocator, _) => {
            compiler.lvaTable[1].Type = TYP_DOUBLE;
            IdentifyCandidates(allocator);

            Assert.Multiple(() => {
                Assert.That(allocator.localVarIntervals, Has.Length.EqualTo(2));
                Assert.That(allocator.localVarIntervals![0]!.registerType, Is.EqualTo(TYP_INT));
                Assert.That(allocator.localVarIntervals[1]!.registerType, Is.EqualTo(TYP_DOUBLE));
                Assert.That(compiler.compFloatingPointUsed, Is.True);
                Assert.That(SetOps.IsMember(compiler, CandidateVars(allocator), 0), Is.True);
                Assert.That(SetOps.IsMember(compiler, CandidateVars(allocator), 1), Is.True);
            });
        });
    }

    [Test]
    public static void WideVectorLocalCreatesUpperInterval()
    {
        WithLocals(1, (compiler, allocator, _) => {
            compiler.lvaTable[0].Type = TYP_SIMD16;
            IdentifyCandidates(allocator);

            var local = allocator.localVarIntervals![0];
            Assert.That(local, Is.Not.Null);
            Assert.That(allocator.intervals.Exists(interval =>
                interval.isUpperVector && ReferenceEquals(interval.relatedInterval, local)), Is.True);
        });
    }

    [Test]
    public static void LocalIntervalConstructionBuildsTrackedLocalReferences()
    {
        WithLocals(1, (compiler, allocator, block) => {
            var constant = compiler.gtNewIconNode(TYP_INT, 12);
            var store = new GenTreeLclVar(TYP_INT, 0, constant);
            block.InsertAtEnd(constant);
            block.InsertAtEnd(store);

            BuildIntervalsWithLocals(allocator);

            Assert.That(allocator.localVarIntervals![0], Is.Not.Null);
            Assert.That(allocator.refPositions.Exists(position =>
                (position.treeNode == store) && position.isIntervalRef()), Is.True);
            Assert.That(allocator.refPositions.Exists(position =>
                position.refType is RefType.RefTypeBB), Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void UpperVectorSaveUsesDoubleRegisterAndExplicitMemorySpill(bool spillToMemory)
    {
        WithLocals(1, (compiler, allocator, block) => {
            compiler.lvaTable[0].Type = TYP_SIMD16;
            var local = new Interval(TYP_SIMD16, SRBM_V8) {
                isLocalVar = true,
                varNum = 0,
                physReg = REG_V8,
            };
            var upper = new Interval(TYP_DOUBLE, SRBM_V9) {
                isUpperVector = true,
                relatedInterval = local,
            };
            var call = new GenTreeCall(TYP_VOID);
            block.InsertAtEnd(call);
            var reference = new RefPosition((uint)block.bbNum, 2, call, RefType.RefTypeUpperVectorSave) {
                registerAssignment = SRBM_V9,
                spillAfter = spillToMemory,
            };
            reference.setInterval(upper);

            InsertUpperSave(allocator, call, reference, upper, block);

            var save = call.Prev ?? throw new AssertionException("Missing upper-vector save.");
            Assert.Multiple(() => {
                Assert.That(save.Oper, Is.EqualTo(GT_INTRINSIC));
                Assert.That(save.Type, Is.EqualTo(TYP_DOUBLE));
                Assert.That(save.RegNum, Is.EqualTo(REG_V9));
                Assert.That((save.Flags & GTF_SPILL) != 0, Is.EqualTo(spillToMemory));
                Assert.That(upper.physReg, Is.EqualTo(spillToMemory ? REG_NA : REG_V9));
            });
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void UpperVectorRestoreReloadsMemoryThroughAssignedRegister(bool spillToMemory)
    {
        WithLocals(1, (compiler, allocator, block) => {
            compiler.lvaTable[0].Type = TYP_SIMD16;
            var local = new Interval(TYP_SIMD16, SRBM_V8) {
                isLocalVar = true,
                isSpilled = spillToMemory,
                varNum = 0,
                physReg = REG_V8,
            };
            var upper = new Interval(TYP_DOUBLE, SRBM_V9) {
                isUpperVector = true,
                relatedInterval = local,
                physReg = spillToMemory ? REG_NA : REG_V9,
            };
            var load = compiler.gtNewLclvNode(TYP_SIMD16, 0);
            var user = new GenTreeLclVar(TYP_SIMD16, 0, load);
            block.InsertAtEnd(load);
            block.InsertAtEnd(user);
            var reference = new RefPosition((uint)block.bbNum, 2, load, RefType.RefTypeUpperVectorRestore) {
                registerAssignment = SRBM_V9,
            };
            reference.setInterval(upper);

            InsertUpperRestore(allocator, load, reference, upper, block);

            var restore = user.Prev ?? throw new AssertionException("Missing upper-vector restore.");
            Assert.Multiple(() => {
                Assert.That(restore.Oper, Is.EqualTo(GT_INTRINSIC));
                Assert.That(restore.Type, Is.EqualTo(TYP_SIMD16));
                Assert.That(restore.RegNum, Is.EqualTo(REG_V9));
                Assert.That((restore.Flags & GTF_SPILLED) != 0, Is.EqualTo(spillToMemory));
                Assert.That(restore.Flags & GTF_NOREG_AT_USE, Is.EqualTo(GTF_EMPTY));
            });
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void IntegerEdgeCycleUsesScratchOrSpillInsteadOfSwap(bool noScratch)
    {
        WithLocals(2, (compiler, allocator, source) => {
            var target = source.Next ?? throw new AssertionException("Missing resolution successor.");
            allocator.initVarRegMaps();
            CandidateVars(allocator) = SetOps.MakeEmpty(compiler);
            SplitOrSpilled(allocator) = SetOps.MakeEmpty(compiler);
            allocator.localVarIntervals = new Interval?[2];
            var live = SetOps.MakeEmpty(compiler);
            for (var index = 0; index < 2; index++)
            {
                SetOps.AddElemD(compiler, target.bbLiveIn, index);
                SetOps.AddElemD(compiler, CandidateVars(allocator), index);
                SetOps.AddElemD(compiler, live, index);
                compiler.lvaTable[index].lvLRACandidate = true;
                allocator.localVarIntervals[index] = new Interval(TYP_INT, SRBM_R0 | SRBM_R1) {
                    isLocalVar = true,
                    isSplit = true,
                    varNum = (uint)index,
                };
                allocator.setOutVarRegForBB((uint)source.bbNum, (uint)index, index == 0 ? REG_R0 : REG_R1);
                allocator.setInVarRegForBB((uint)target.bbNum, (uint)index, index == 0 ? REG_R1 : REG_R0);
            }

            ResolveEdge(allocator, source, target, LinearScan.ResolveType.ResolveJoin, live,
                noScratch ? new regMaskTP(SRBM_ALLINT) : RBM_NONE);

            var copies = 0;
            var spills = 0;
            var reloads = 0;
            foreach (var node in source)
            {
                Assert.That(node.Oper, Is.Not.EqualTo(GT_SWAP));
                copies += node.Oper == GT_COPY ? 1 : 0;
                spills += (node.Flags & GTF_SPILL) != 0 ? 1 : 0;
                reloads += (node.Flags & GTF_SPILLED) != 0 ? 1 : 0;
            }
            Assert.Multiple(() => {
                Assert.That(copies, Is.EqualTo(noScratch ? 1 : 3));
                Assert.That(spills, Is.EqualTo(noScratch ? 1 : 0));
                Assert.That(reloads, Is.EqualTo(noScratch ? 1 : 0));
            });
        }, twoBlocks: true);
    }

    [TestCase(false)]
#if DEBUG
    [TestCase(true)]
#endif
    public static void ConstructedLocalAllocatesAndResolvesWithinOneBlock(bool stressSpills)
    {
        WithLocals(1, (compiler, allocator, block) => {
            var returnType = new ReturnTypeDesc();
            returnType.InitializeReturnType(compiler, TYP_INT, null, CorInfoCallConvExtension.Managed);
            compiler.compRetTypeDesc = returnType;
            var constant = compiler.gtNewIconNode(TYP_INT, 12);
            var store = new GenTreeLclVar(TYP_INT, 0, constant);
            var load = compiler.gtNewLclvNode(TYP_INT, 0);
            var ret = new GenTreeUnOp(GT_RETURN, TYP_INT, load);
            block.InsertAtEnd(constant);
            block.InsertAtEnd(store);
            block.InsertAtEnd(load);
            block.InsertAtEnd(ret);

#if DEBUG
            if (stressSpills)
            {
                StressMask(allocator) = 0xc00;
            }
#endif
            InitMaxSpill(allocator);
            BuildIntervalsWithLocals(allocator);
            allocator.initVarRegMaps();
            AllocateRegisters(allocator);
            AllocationPassComplete(allocator) = true;
            allocator.resolveRegistersWithLocals();

            Assert.That(allocator.localVarIntervals![0], Is.Not.Null);
            Assert.That(load.RegNum, Is.EqualTo(REG_INTRET));
            Assert.That(compiler.lvaTable[0].lvLRACandidate, Is.True);
            if (stressSpills)
            {
                Assert.That(allocator.localVarIntervals[0]!.isSpilled, Is.True);
            }
        });
    }

    [TestCase(TYP_INT, REG_STK, REG_R0, GT_LCL_VAR)]
    [TestCase(TYP_INT, REG_R0, REG_STK, GT_LCL_VAR)]
    [TestCase(TYP_INT, REG_R0, REG_R1, GT_COPY)]
    [TestCase(TYP_REF, REG_R0, REG_R1, GT_COPY)]
    [TestCase(TYP_DOUBLE, REG_V0, REG_V1, GT_COPY)]
    [TestCase(TYP_MASK, REG_STK, REG_P1, GT_LCL_VAR)]
    [TestCase(TYP_MASK, REG_P0, REG_STK, GT_LCL_VAR)]
    [TestCase(TYP_MASK, REG_P0, REG_P1, GT_COPY)]
    public static void JoinResolutionMovesLocalBetweenStackAndRegisters(
        var_types type, regNumber from, regNumber to, genTreeOps expected)
    {
        WithLocals(1, (compiler, allocator, source) => {
            var target = source.Next ?? throw new AssertionException("Missing resolution successor.");
            allocator.initVarRegMaps();
            SetOps.AddElemD(compiler, target.bbLiveIn, 0);
            compiler.lvaTable[0].Type = type;
            compiler.compFloatingPointUsed = type is TYP_DOUBLE or TYP_MASK;
            var interval = new Interval(type, genSingleTypeRegMask(from == REG_STK ? to : from)) {
                isLocalVar = true,
                isSpilled = true,
                varNum = 0,
            };
            allocator.localVarIntervals = [interval];
            compiler.lvaTable[0].lvLRACandidate = true;
            CandidateVars(allocator) = SetOps.MakeEmpty(compiler);
            SetOps.AddElemD(compiler, CandidateVars(allocator), 0);
            SplitOrSpilled(allocator) = SetOps.MakeEmpty(compiler);
            allocator.setOutVarRegForBB((uint)source.bbNum, 0, from);
            allocator.setInVarRegForBB((uint)target.bbNum, 0, to);
            var live = SetOps.MakeEmpty(compiler);
            SetOps.AddElemD(compiler, live, 0);

            ResolveEdge(allocator, source, target, LinearScan.ResolveType.ResolveJoin, live, RBM_NONE);

            Assert.That(source.LastNode?.Oper, Is.EqualTo(expected));
            Assert.That(allocator.getOutVarToRegMap((uint)source.bbNum)![0], Is.EqualTo(to));
        }, twoBlocks: true);
    }

    [Test]
    public static void ResolutionWritesSecondRegisterOfArm64Call()
    {
        WithLocals(1, (compiler, allocator, _) => {
            var call = new GenTreeCall(TYP_STRUCT);
            var reference = new RefPosition(1, 1, call, RefType.RefTypeDef) {
                registerAssignment = SRBM_R1,
                multiRegIdx = 1,
            };
            var interval = new Interval(TYP_LONG, SRBM_R1);
            reference.setInterval(interval);
            call._returnTypeDesc.InitializeReturnType(compiler, TYP_LONG, null,
                CorInfoCallConvExtension.Managed);
            ReturnTypes(ref call._returnTypeDesc)[1] = TYP_LONG;
            Assert.That(call.HasMultiRegRetVal, Is.True);

            WriteRegisters(allocator, reference, call);

            Assert.That(call.GetRegNumByIdx(1), Is.EqualTo(REG_R1));
        });
    }

    [Test]
    public static void LocalResolutionRebuildsEntryParameterHome()
    {
        WithLocals(1, (compiler, allocator, block) => {
            compiler.lvaTable[0].lvIsParam = true;
            compiler.lvaTable[0].lvIsRegArg = true;
            compiler.lvaTable[0].lvLRACandidate = true;
            var interval = new Interval(TYP_INT, SRBM_R0) {
                isLocalVar = true,
                varNum = 0,
            };
            allocator.localVarIntervals = [interval];
            CandidateVars(allocator) = SetOps.MakeEmpty(compiler);
            ResolutionCandidates(allocator) = SetOps.MakeEmpty(compiler);
            SplitOrSpilled(allocator) = SetOps.MakeEmpty(compiler);
            ExceptVars(allocator) = SetOps.MakeEmpty(compiler);
            SetOps.AddElemD(compiler, CandidateVars(allocator), 0);
            allocator.initVarRegMaps();
            var parameter = new RefPosition(0, 0, null, RefType.RefTypeParamDef) {
                registerAssignment = SRBM_R0,
            };
            parameter.setInterval(interval);
            interval.firstRefPosition = parameter;
            interval.lastRefPosition = parameter;
            allocator.refPositions.Add(parameter);
            allocator.refPositions.Add(new RefPosition((uint)block.bbNum, 1, null, RefType.RefTypeBB));

            allocator.resolveRegistersWithLocals();

            Assert.Multiple(() => {
                Assert.That(allocator.getInVarToRegMap((uint)block.bbNum)![0], Is.EqualTo(REG_R0));
                Assert.That(compiler.lvaTable[0].ArgInitReg, Is.EqualTo(REG_R0));
                Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_R0));
            });
        });
    }

    [Test]
    public static void MinimalResolutionWritesArm64TemporaryDefinition()
    {
        WithLocals(0, (compiler, allocator, block) => {
            var constant = compiler.gtNewIconNode(TYP_INT, 7);
            block.InsertAtEnd(constant);
            var interval = new Interval(TYP_INT, SRBM_R0);
            var definition = new RefPosition((uint)block.bbNum, 2, constant, RefType.RefTypeDef) {
                registerAssignment = SRBM_R0,
            };
            definition.setInterval(interval);
            interval.firstRefPosition = definition;
            interval.lastRefPosition = definition;
            allocator.refPositions.Add(new RefPosition((uint)block.bbNum, 1, null, RefType.RefTypeBB));
            allocator.refPositions.Add(definition);

            ResolveMinimal(allocator);

            Assert.That(constant.RegNum, Is.EqualTo(REG_R0));
        }, enableLocals: false);
    }

    private static void WithLocals(int count, Action<Compiler, LinearScan, BasicBlock> action,
        bool twoBlocks = false, bool enableLocals = true)
    {
        Arm64LinearScanConstructionTests.WithCompiler(false, false, (compiler, codeGen) => {
            CORINFO_METHOD_INFO methodInfo = default;
            compiler.info.compMethodInfo = &methodInfo;
#if DEBUG
            compiler.fgSafeBasicBlockCreation = true;
            compiler.fgSafeFlowEdgeCreation = true;
#endif
            if (enableLocals)
            {
                compiler.opts.compFlags |= CLFLG_REGVAR;
            }
            compiler.lvaRefCountState = RCS_NORMAL;
            compiler.lvaCount = count;
            compiler.lvaTrackedCount = count;
            compiler.lvaTrackedCountInSizeTUnits = 1;
            compiler.lvaTable = new LclVarDsc[count];
            compiler.lvaTrackedToVarNum = new int[count];
            compiler.fgBBVarSetsInited = true;
            compiler.fgPredsComputed = true;
            compiler.compRationalIRForm = true;
            compiler.fgLocalVarLivenessDone = true;
            compiler.fgNodeThreading = NodeThreading.LIR;
            compiler.compHndBBtab = [];
            compiler.lvaTrackedFixed = true;
            compiler.lvaOutgoingArgSpaceSize.Value = 0;
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI;
            codeGen.IsFramePointerRequired = true;
            codeGen.IsFramePointerUsed = true;
            codeGen.IsFrameRequired = true;
            codeGen.RegSet.rsClearRegsModified();
            for (var index = 0; index < count; index++)
            {
                compiler.lvaTrackedToVarNum[index] = index;
                ref var local = ref compiler.lvaTable[index];
                local.Type = TYP_INT;
                local.lvTracked = true;
                local._varIndex = checked((ushort)index);
                local.lvMustInit = true;
                local.lvOnFrame = true;
                local.setLvRefCnt(3);
                local.setLvRefCntWtd(3 * BB_UNITY_WEIGHT);
            }
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            block.bbRefs = 1;
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            compiler.fgBBcount = 1;
            compiler.fgBBNumMax = block.bbNum;
            if (twoBlocks)
            {
                var successor = BasicBlock.New(compiler, BBJ_RETURN);
                block.Next = successor;
                compiler.fgLastBB = successor;
                compiler.fgBBcount = 2;
                compiler.fgBBNumMax = successor.bbNum;
            }
            var allocator = new LinearScan(compiler);
            compiler.rpMustCreateEBPCalled = true;
            BuildPhysicalRegisters(allocator);
            SetBlockSequence(allocator);
            action(compiler, allocator, block);
        }, minOpts: false);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "identifyCandidatesWithLocals")]
    private static extern void IdentifyCandidates(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildIntervalsWithLocals")]
    private static extern void BuildIntervalsWithLocals(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "resolveRegistersMinimal")]
    private static extern void ResolveMinimal(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "allocateRegisters")]
    private static extern void AllocateRegisters(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "initMaxSpill")]
    private static extern void InitMaxSpill(LinearScan allocator);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_lsraStressMask")]
    private static extern ref int StressMask(LinearScan allocator);
#endif
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_allocationPassComplete")]
    private static extern ref bool AllocationPassComplete(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "writeRegisters")]
    private static extern void WriteRegisters(LinearScan allocator, RefPosition reference, GenTree tree);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "insertUpperVectorSave")]
    private static extern void InsertUpperSave(LinearScan allocator, GenTree tree, RefPosition reference,
        Interval upper, BasicBlock block);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "insertUpperVectorRestore")]
    private static extern void InsertUpperRestore(LinearScan allocator, GenTree? tree, RefPosition reference,
        Interval upper, BasicBlock block);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "resolveEdge")]
    private static extern void ResolveEdge(LinearScan allocator, BasicBlock source, BasicBlock target,
        LinearScan.ResolveType kind, nint[] live, regMaskTP consumed);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildPhysRegRecords")]
    private static extern void BuildPhysicalRegisters(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "setBlockSequence")]
    private static extern void SetBlockSequence(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_registerCandidateVars")]
    private static extern ref nint[] CandidateVars(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_resolutionCandidateVars")]
    private static extern ref nint[] ResolutionCandidates(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_splitOrSpilledVars")]
    private static extern ref nint[]? SplitOrSpilled(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_exceptVars")]
    private static extern ref nint[] ExceptVars(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regType")]
    private static extern ref InlineArrayMaxRetRegCount<var_types> ReturnTypes(ref ReturnTypeDesc descriptor);
}
#endif
