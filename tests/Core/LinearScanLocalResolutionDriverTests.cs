// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
#if DEBUG
using static RyuJitSharp.GenTreeDebugFlags;
#endif
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using SetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class LinearScanLocalResolutionDriverTests
{
    [TestCase(RefType.RefTypeParamDef, REG_RCX, false)]
    [TestCase(RefType.RefTypeParamDef, REG_STK, true)]
    [TestCase(RefType.RefTypeZeroInit, REG_RCX, false)]
    public static void EntryDefinitionsRebuildHomesAndFinalizeLocalDescriptors(
        RefType kind, regNumber expected, bool onStack)
    {
        WithDriver((compiler, allocator, block, interval) =>
        {
            ref var local = ref compiler.lvaTable[0];
            if (kind is RefType.RefTypeParamDef)
            {
                local.lvIsParam = true;
                local.lvIsRegArg = true;
            }
            var reference = new RefPosition(0, 0, null, kind)
            {
                registerAssignment = onStack ? SRBM_NONE : SRBM_RCX,
                regOptional = onStack,
            };
            reference.setInterval(interval);
            interval.firstRefPosition = reference;
            interval.lastRefPosition = reference;
            interval.isSpilled = onStack;
            if (onStack)
            {
                local.lvOnFrame = true;
            }
            allocator.refPositions.Add(reference);
            allocator.refPositions.Add(new RefPosition((uint)block.bbNum, 1, null, RefType.RefTypeBB));

            allocator.resolveRegistersWithLocals();

            Assert.That(allocator.getInVarToRegMap((uint)block.bbNum)![0], Is.EqualTo(expected));
            Assert.That(local.RegNum, Is.EqualTo(expected));
            Assert.That(local.lvRegister, Is.EqualTo(!onStack));
            if (kind is RefType.RefTypeParamDef)
            {
                Assert.That(local.ArgInitReg, Is.EqualTo(expected));
            }
            Assert.That(allocator.getOutVarToRegMap((uint)block.bbNum)![0], Is.EqualTo(REG_STK));
        });
    }

    [Test]
    public static void RejectsDisabledLocalAllocationBeforeChangingRegisterAssignments()
    {
        WithDriver((_, allocator, _, interval) =>
        {
            EnregisterLocals(allocator) = false;
            interval.physReg = REG_RCX;
            var exception = Assert.Throws<FatalJitException>(() => allocator.resolveRegistersWithLocals());
            Assert.That(exception!.Message, Does.Contain("requires enregistered locals"));
            Assert.That(interval.physReg, Is.EqualTo(REG_RCX));
        });
    }

    [Test]
    public static void DeadCandidateWithoutReferencesLosesRegisterAndStackHome()
    {
        WithDriver((compiler, allocator, block, _) =>
        {
            compiler.lvaTable[0].setLvRefCnt(0);
            allocator.refPositions.Add(new RefPosition((uint)block.bbNum, 1, null, RefType.RefTypeBB));

            allocator.resolveRegistersWithLocals();

            Assert.That(compiler.lvaTable[0].lvLRACandidate, Is.False);
            Assert.That(compiler.lvaTable[0].lvOnFrame, Is.False);
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
        });
    }

#if DEBUG
    [Test]
    public static void VerboseResolutionReportsBoundaryMapsAndFinalVerification()
    {
        WithDriver((compiler, allocator, block, interval) =>
        {
            compiler.verbose = true;
            compiler.lvaTable[0].lvIsParam = true;
            compiler.lvaTable[0].lvIsRegArg = true;
            var parameter = new RefPosition(0, 0, null, RefType.RefTypeParamDef)
            {
                registerAssignment = SRBM_RCX,
            };
            parameter.setInterval(interval);
            interval.firstRefPosition = parameter;
            interval.lastRefPosition = parameter;
            allocator.refPositions.Add(parameter);
            allocator.refPositions.Add(new RefPosition((uint)block.bbNum, 1, null, RefType.RefTypeBB));

            var output = CodeGenLifeTransitionTests.Capture(allocator.resolveRegistersWithLocals);

            Assert.That(output, Does.Contain("RESOLVING BB BOUNDARIES"));
            Assert.That(output, Does.Contain("Resolution Candidates:"));
            Assert.That(output, Does.Contain("Trees after linear scan register allocator (LSRA)"));
            Assert.That(output, Does.Contain("Final allocation"));
        });
    }
#endif

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public static void ConsecutiveBlocksCarryAParameterHomeThroughARealLocalUse(
        bool lastUse, bool dummyDefinition)
    {
        WithDriver((compiler, allocator, entry, interval) =>
        {
            var successor = entry.Next!;
            SetOps.AddElemD(compiler, entry.bbLiveOut, 0);
            SetOps.AddElemD(compiler, successor.bbLiveIn, 0);
            var parameter = new RefPosition(0, 0, null, RefType.RefTypeParamDef)
            {
                registerAssignment = SRBM_RCX,
            };
            parameter.setInterval(interval);
            var localUse = compiler.gtNewLclvNode(TYP_INT, 0);
            successor.InsertAtEnd(localUse);
            var use = new RefPosition((uint)successor.bbNum, 5, localUse, RefType.RefTypeUse)
            {
                registerAssignment = SRBM_RCX,
                lastUse = lastUse,
            };
            use.setInterval(interval);
            interval.firstRefPosition = parameter;
            interval.lastRefPosition = use;
            allocator.refPositions.Add(parameter);
            allocator.refPositions.Add(new RefPosition((uint)entry.bbNum, 1, null, RefType.RefTypeBB));
            if (dummyDefinition)
            {
                var dummy = new RefPosition((uint)successor.bbNum, 3, null, RefType.RefTypeDummyDef)
                {
                    registerAssignment = SRBM_RCX,
                    reload = true,
                };
                dummy.setInterval(interval);
                parameter.nextRefPosition = dummy;
                dummy.nextRefPosition = use;
                allocator.refPositions.Add(dummy);
            }
            else
            {
                parameter.nextRefPosition = use;
            }
            allocator.refPositions.Add(new RefPosition((uint)successor.bbNum, 4, null, RefType.RefTypeBB));
            allocator.refPositions.Add(use);
            compiler.lvaTable[0].lvIsParam = true;
            compiler.lvaTable[0].lvIsRegArg = true;

            allocator.resolveRegistersWithLocals();

            Assert.That(allocator.getOutVarToRegMap((uint)entry.bbNum)![0], Is.EqualTo(REG_RCX));
            Assert.That(allocator.getInVarToRegMap((uint)successor.bbNum)![0], Is.EqualTo(REG_RCX));
            Assert.That(localUse.RegNum, Is.EqualTo(REG_RCX));
            Assert.That(localUse.IsLastUse(0), Is.EqualTo(lastUse));
            Assert.That(compiler.lvaTable[0].lvRegister, Is.True);
        }, twoBlocks: true);
    }

#if DEBUG
    [TestCase(REG_RCX, REG_RDX, GT_COPY)]
    [TestCase(REG_STK, REG_RCX, GT_LCL_VAR)]
    [TestCase(REG_RCX, REG_STK, GT_LCL_VAR)]
    public static void FinalVerificationReplaysInsertedRegisterAndStackMoves(
        regNumber from, regNumber to, genTreeOps oper)
    {
        WithDriver((compiler, allocator, _, interval) =>
        {
            interval.physReg = from == REG_STK ? REG_NA : from;
            if (from != REG_STK)
            {
                interval.assignedReg = allocator.physRegs[(int)from];
                interval.assignedReg.assignedInterval = interval;
            }
            var source = compiler.gtNewLclvNode(TYP_INT, 0);
            source.RegNum = from == REG_STK ? to : from;
            GenTree destination = source;
            if (oper is GT_COPY)
            {
                destination = new GenTreeCopyOrReload(GT_COPY, TYP_INT, source)
                {
                    RegNum = to,
                };
            }
            else if (from == REG_STK)
            {
                source.Flags |= GTF_SPILLED;
            }
            else
            {
                source.Flags |= GTF_SPILL;
            }
            destination._debugFlags |= GTF_DEBUG_NODE_LSRA_ADDED;
            destination.IsUnusedValue = true;

            VerifyMove(allocator, destination, 3);

            Assert.That(interval.physReg, Is.EqualTo(to == REG_STK ? REG_NA : to));
            Assert.That(interval.isActive, Is.EqualTo(to != REG_STK));
            if (from != REG_STK)
            {
                Assert.That(allocator.physRegs[(int)from].assignedInterval,
                    to == from ? Is.SameAs(interval) : Is.Null);
            }
            if (to != REG_STK)
            {
                Assert.That(allocator.physRegs[(int)to].assignedInterval, Is.SameAs(interval));
            }
        });
    }

    [Test]
    public static void FinalVerificationReplaysAnInsertedRegisterSwap()
    {
        WithDriver((compiler, allocator, _, firstInterval) =>
        {
            var secondInterval = new Interval(TYP_INT, SRBM_RDX)
            {
                isLocalVar = true,
                varNum = 1,
                physReg = REG_RDX,
            };
            allocator.localVarIntervals![1] = secondInterval;
            firstInterval.physReg = REG_RCX;
            firstInterval.assignedReg = allocator.physRegs[(int)REG_RCX];
            secondInterval.assignedReg = allocator.physRegs[(int)REG_RDX];
            firstInterval.assignedReg.assignedInterval = firstInterval;
            secondInterval.assignedReg.assignedInterval = secondInterval;
            var first = compiler.gtNewLclvNode(TYP_INT, 0);
            var second = compiler.gtNewLclvNode(TYP_INT, 1);
            first.RegNum = REG_RCX;
            second.RegNum = REG_RDX;
            var swap = new GenTreeOp(GT_SWAP, TYP_VOID, first, second)
            {
                RegNum = REG_NA,
            };
            swap._debugFlags |= GTF_DEBUG_NODE_LSRA_ADDED;

            VerifyMove(allocator, swap, 3);

            Assert.That(firstInterval.physReg, Is.EqualTo(REG_RDX));
            Assert.That(secondInterval.physReg, Is.EqualTo(REG_RCX));
            Assert.That(allocator.physRegs[(int)REG_RDX].assignedInterval, Is.SameAs(firstInterval));
            Assert.That(allocator.physRegs[(int)REG_RCX].assignedInterval, Is.SameAs(secondInterval));
        }, localCount: 2);
    }
#endif

#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
    [TestCase(REG_NA, true)]
    [TestCase(REG_XMM8, false)]
    public static void UpperVectorSavesAndRestoresPreservePlacementAndMemoryHome(
        regNumber spillRegister, bool onStack)
    {
        WithDriver((compiler, allocator, block, _) =>
        {
            compiler.lvaTable[0].Type = TYP_SIMD32;
            var local = new Interval(TYP_FLOAT, SRBM_XMM6)
            {
                isLocalVar = true,
                varNum = 0,
                isSpilled = onStack,
                physReg = REG_XMM6,
            };
            var upper = new Interval(TYP_FLOAT, SRBM_XMM8)
            {
                isUpperVector = true,
                relatedInterval = local,
                physReg = REG_NA,
            };
            var anchor = compiler.gtNewIconNode(TYP_INT, 1);
            block.InsertAtEnd(anchor);
            var save = new RefPosition((uint)block.bbNum, 2, anchor, RefType.RefTypeUpperVectorSave)
            {
                registerAssignment = spillRegister == REG_NA ? SRBM_NONE : SRBM_XMM8,
            };
            save.setInterval(upper);

            SaveUpper(allocator, anchor, save, upper, block);

            var saveNode = anchor.Prev!;
            Assert.That(saveNode.Oper, Is.EqualTo(GT_INTRINSIC));
            Assert.That(saveNode.AsIntrinsic().IntrinsicName, Is.EqualTo(NI_SIMD_UpperSave));
            Assert.That(saveNode.RegNum, Is.EqualTo(spillRegister));
            Assert.That(upper.physReg, Is.EqualTo(spillRegister));
            Assert.That((saveNode.Flags & GTF_SPILL) != 0, Is.EqualTo(onStack));

            var use = compiler.gtNewLclvNode(TYP_SIMD32, 0);
            var parent = new GenTreeUnOp(GT_NEG, TYP_SIMD32, use);
            block.InsertAtEnd(use);
            block.InsertAtEnd(parent);
            var restore = new RefPosition((uint)block.bbNum, 3, use, RefType.RefTypeUpperVectorRestore);
            restore.setInterval(upper);
            RestoreUpper(allocator, use, restore, upper, block);

            var restoreNode = parent.Prev!;
            Assert.That(restoreNode.Oper, Is.EqualTo(GT_INTRINSIC));
            Assert.That(restoreNode.AsIntrinsic().IntrinsicName, Is.EqualTo(NI_SIMD_UpperRestore));
            Assert.That(restoreNode.Next, Is.SameAs(parent));
            Assert.That((restoreNode.Flags & GTF_NOREG_AT_USE) != 0, Is.EqualTo(onStack));
        });
    }
#endif

    private static void WithDriver(Action<Compiler, LinearScan, BasicBlock, Interval> action,
        bool twoBlocks = false, int localCount = 1)
    {
        LinearScanLocalCandidatesTests.WithCandidates(localCount, (compiler, allocator) =>
        {
            compiler.compRationalIRForm = true;
            compiler.fgPredsComputed = true;
            compiler.fgLocalVarLivenessDone = true;
            compiler.fgNodeThreading = NodeThreading.LIR;
            compiler.compHndBBtab = [];
#if DEBUG
            compiler.fgSafeFlowEdgeCreation = true;
#endif
            compiler.lvaTrackedFixed = true;
            compiler.srbmIntCalleeTrash = SRBM_INT_CALLEE_TRASH_INIT;
            compiler.srbmFltCalleeTrash = SRBM_FLT_CALLEE_TRASH_INIT;
            compiler.codeGen!.RegSet.rsClearRegsModified();
            compiler.codeGen.IsFramePointerUsed = false;
            var block = BasicBlock.New(compiler, twoBlocks ? BBJ_ALWAYS : BBJ_RETURN);
            compiler.fgFirstBB = block;
            if (twoBlocks)
            {
                var successor = BasicBlock.New(compiler, BBJ_RETURN);
                block.Next = successor;
                compiler.fgLastBB = successor;
                block.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(successor, block));
            }
            else
            {
                compiler.fgLastBB = block;
            }
            EnregisterLocals(allocator) = true;
            BuildPhysicalRegisters(allocator);
            SetBlockSequence(allocator);
            allocator.initVarRegMaps();
            var interval = new Interval(TYP_INT, SRBM_RCX)
            {
                isLocalVar = true,
                varNum = 0,
            };
            allocator.localVarIntervals = new Interval?[localCount];
            allocator.localVarIntervals[0] = interval;
            allocator.intervals.Add(interval);
            compiler.lvaTable[0].lvLRACandidate = true;
            compiler.lvaTable[0].RegNum = REG_STK;
            RegisterCandidates(allocator) = SetOps.MakeEmpty(compiler);
            ResolutionCandidates(allocator) = SetOps.MakeEmpty(compiler);
            SplitOrSpilled(allocator) = SetOps.MakeEmpty(compiler);
            ExceptVars(allocator) = SetOps.MakeEmpty(compiler);
            SetOps.AddElemD(compiler, RegisterCandidates(allocator), 0);
            action(compiler, allocator, block, interval);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "setBlockSequence")]
    private static extern void SetBlockSequence(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildPhysRegRecords")]
    private static extern void BuildPhysicalRegisters(LinearScan allocator);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "verifyResolutionMoveWithLocals")]
    private static extern void VerifyMove(LinearScan allocator, GenTree destination, uint location);
#endif

#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "insertUpperVectorSave")]
    private static extern void SaveUpper(LinearScan allocator, GenTree tree, RefPosition reference,
        Interval upper, BasicBlock block);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "insertUpperVectorRestore")]
    private static extern void RestoreUpper(LinearScan allocator, GenTree? tree, RefPosition reference,
        Interval upper, BasicBlock block);
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_enregisterLocalVars")]
    private static extern ref bool EnregisterLocals(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_registerCandidateVars")]
    private static extern ref nint[] RegisterCandidates(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_resolutionCandidateVars")]
    private static extern ref nint[] ResolutionCandidates(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_splitOrSpilledVars")]
    private static extern ref nint[] SplitOrSpilled(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_exceptVars")]
    private static extern ref nint[] ExceptVars(LinearScan allocator);
}
