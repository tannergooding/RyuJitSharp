// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static class LinearScanBlockLocationTests
{
    [TestCase(false, true, REG_RCX)]
    [TestCase(true, true, REG_RCX)]
    [TestCase(true, false, REG_RCX)]
    [TestCase(false, false, REG_RCX)]
    [TestCase(false, true, REG_STK)]
    public static void LocationRestorationRehomesOnlyRangesLiveAtTheLastReportedBlock(
        bool callFinally, bool wasLive, regNumber destination)
    {
        WithAllocator((compiler, codeGen, allocator) =>
        {
            var previous = new BasicBlock(null, null) { bbNum = 1, bbID = 1 };
            var tail = new BasicBlock(null, null) { bbNum = 2, bbID = 2 };
            var block = new BasicBlock(null, null) { bbNum = 3, bbID = 3 };
            if (callFinally)
            {
                previous.SetKindAndTargetEdge(BBJ_CALLFINALLY, compiler.fgAddRefPred(block, previous));
                tail.SetKindAndTargetEdge(BBJ_CALLFINALLYRET, compiler.fgAddRefPred(block, tail));
                previous.Next = tail;
                tail.Next = block;
                tail.bbLiveOut = wasLive ? VarSetOps.MakeEmpty(compiler) : VarSetOps.MakeSingleton(compiler, 0);
            }
            else
            {
                previous.Next = block;
            }
            previous.bbLiveOut = wasLive ? VarSetOps.MakeSingleton(compiler, 0) : VarSetOps.MakeEmpty(compiler);
            block.bbLiveIn = VarSetOps.MakeSingleton(compiler, 0);
            allocator.setInVarRegForBB(3, 0, destination);
            var ranges = codeGen.getVariableLiveKeeper();
            ranges.siStartVariableLiveRange(in compiler.lvaTable[0], 0);
            codeGen.Emitter.emitIns(INS_nop);
            var life = VarSetOps.MakeSingleton(compiler, 0);
            compiler.compCurLife = life;
            var registers = codeGen.RegSet.GetMaskVars();
            codeGen.GCInfo.gcRegGCrefSetCur = registers;

            allocator.recordVarLocationsAtStartOfBB(block);

            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(destination));
            Assert.That(compiler.compCurLife, Is.SameAs(life));
            Assert.That(VarSetOps.IsMember(compiler, life, 0), Is.True);
            Assert.That(codeGen.RegSet.GetMaskVars(), Is.EqualTo(registers));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(registers));
            var variableRanges = ranges.getLiveRangesForVarForBody(0);
            Assert.That(variableRanges, Has.Count.EqualTo(wasLive ? 2 : 1));
            if (wasLive)
            {
                Assert.That(variableRanges[0].m_EndEmitLocation, Is.EqualTo(variableRanges[1].m_StartEmitLocation));
                Assert.That(variableRanges[0].m_StartEmitLocation, Is.Not.EqualTo(variableRanges[0].m_EndEmitLocation));
                Assert.That(CodeGen.siVarLoc.Equals(variableRanges[1].m_VarLocation,
                    codeGen.getSiVarLoc(in compiler.lvaTable[0], 0, 0)), Is.True);
            }
        });
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public static void RestorationRequiresBothCandidacyAndLiveInMembership(bool candidate, bool liveIn)
    {
        WithAllocator((compiler, _, allocator) =>
        {
            CandidateVars(allocator) = candidate ? VarSetOps.MakeSingleton(compiler, 0) : VarSetOps.MakeEmpty(compiler);
            var block = new BasicBlock(null, null)
            {
                bbNum = 1,
                bbLiveIn = liveIn ? VarSetOps.MakeSingleton(compiler, 0) : VarSetOps.MakeEmpty(compiler),
            };
            allocator.setInVarRegForBB(1, 0, REG_RCX);

            allocator.recordVarLocationsAtStartOfBB(block);

            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_RAX));
            Assert.That(VarSetOps.IsEmpty(compiler, CurrentLiveVars(allocator)), Is.True);
        });
    }

    [Test]
    public static void DisabledEnregistrationDoesNotAccessMapsOrLiveSets()
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_REF, REG_RAX, (compiler, _, _) =>
        {
            var allocator = new LinearScan(compiler);
            allocator.recordVarLocationsAtStartOfBB(new BasicBlock(null, null));
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_RAX));
        });
    }

    private static void WithAllocator(Action<Compiler, CodeGen, LinearScan> action)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_REF, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.opts.compFlags |= CLFLG_REGVAR;
            compiler.opts.compDbgInfo = true;
            compiler.lvaTrackedFixed = true;
            compiler.fgBBNumMax = 3;
            compiler.fgPredsComputed = true;
#if DEBUG
            compiler.fgSafeFlowEdgeCreation = true;
#endif
            var allocator = new LinearScan(compiler);
            MaxBlockBeforeResolution(allocator) = 3;
            allocator.initVarRegMaps();
            CandidateVars(allocator) = VarSetOps.MakeSingleton(compiler, 0);
            codeGen.initializeVariableLiveKeeper();
            action(compiler, codeGen, allocator);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_bbNumMaxBeforeResolution")]
    private static extern ref uint MaxBlockBeforeResolution(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_registerCandidateVars")]
    private static extern ref nint[] CandidateVars(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_currentLiveVars")]
    private static extern ref nint[] CurrentLiveVars(LinearScan allocator);
}
