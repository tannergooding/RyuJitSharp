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
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using SetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class LinearScanLocalIntervalConstructionTests
{
    [Test]
    public static void EntryDefinitionPrecedesFirstRealLocationAndBackedgeExposedUse()
    {
        WithBuilder(1, (compiler, allocator) => {
            var block = CreateBlocks(compiler, BBJ_ALWAYS)[0];
            block.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(block, block));
            compiler.info.compInitMem = true;
            SetOps.AddElemD(compiler, block.bbLiveIn, 0);
            SetOps.AddElemD(compiler, block.bbLiveOut, 0);

            BuildIntervals(allocator);

            var references = allocator.refPositions;
            Assert.That(references.FindAll(position => position.refType is RefType.RefTypeZeroInit),
                Has.Count.EqualTo(1));
            Assert.That(references[0].nodeLocation, Is.Zero);
            Assert.That(references[0].refType, Is.EqualTo(RefType.RefTypeZeroInit));
            Assert.That(references[1].refType, Is.EqualTo(RefType.RefTypeBB));
            Assert.That(references[1].nodeLocation, Is.EqualTo(1));
            Assert.That(references[2].refType, Is.EqualTo(RefType.RefTypeExpUse));
            Assert.That(references[2].nodeLocation, Is.EqualTo(3));
            Assert.That(references[2].regOptional, Is.True);
            Assert.That(references[^1].refType, Is.EqualTo(RefType.RefTypeBB));
            Assert.That(references[^1].nodeLocation, Is.EqualTo(3));
            Assert.That(compiler.lvaTable[0].lvMustInit, Is.True);
            Assert.That(BlockInfo(allocator)![block.bbNum].predBBNum, Is.Zero);
        });
    }

    [Test]
    public static void SuccessorLiveInSuppressesExposedUseAndInheritsPredecessor()
    {
        WithBuilder(1, (compiler, allocator) => {
            var blocks = CreateBlocks(compiler, BBJ_ALWAYS, BBJ_RETURN);
            blocks[0].SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(blocks[1], blocks[0]));
            compiler.info.compInitMem = true;
            foreach (var block in blocks)
            {
                SetOps.AddElemD(compiler, block.bbLiveIn, 0);
                SetOps.AddElemD(compiler, block.bbLiveOut, 0);
            }

            BuildIntervals(allocator);

            Assert.That(allocator.refPositions.FindAll(position =>
                position.refType is RefType.RefTypeExpUse), Has.Count.EqualTo(1));
            Assert.That(allocator.refPositions.FindAll(position =>
                position.refType is RefType.RefTypeDummyDef), Is.Empty);
            Assert.That(BlockInfo(allocator)![blocks[1].bbNum].predBBNum,
                Is.EqualTo((uint)blocks[0].bbNum));
            Assert.That(allocator.refPositions.FindAll(position =>
                position.refType is RefType.RefTypeBB).ConvertAll(position => position.nodeLocation),
                Is.EqualTo(new uint[] { 1, 3 }));
        });
    }

    [Test]
    public static void ParameterDefinitionsFollowTrackedOrderInsteadOfLocalNumberOrder()
    {
        WithBuilder(2, (compiler, allocator) => {
            _ = CreateBlocks(compiler, BBJ_RETURN);
            compiler.info.compArgsCount = 2;
            compiler.lvaTrackedToVarNum = [1, 0];
            compiler.lvaTable[0]._varIndex = 1;
            compiler.lvaTable[1]._varIndex = 0;
            compiler.lvaTable[0].lvIsParam = true;
            compiler.lvaTable[1].lvIsParam = true;
            compiler.lvaParameterPassingInfo = [
                AbiPassingInformation.FromSegment(compiler, false,
                    AbiPassingSegment.InRegister(REG_RCX, 0, 4)),
                AbiPassingInformation.FromSegment(compiler, false,
                    AbiPassingSegment.InRegister(REG_RDX, 0, 4)),
            ];

            BuildIntervals(allocator);

            var positions = allocator.refPositions;
            Assert.That(positions[0].refType, Is.EqualTo(RefType.RefTypeParamDef));
            Assert.That(positions[1].refType, Is.EqualTo(RefType.RefTypeParamDef));
            Assert.That(positions[0].getInterval().varNum, Is.EqualTo(1));
            Assert.That(positions[1].getInterval().varNum, Is.Zero);
            Assert.That(positions[0].nodeLocation, Is.Zero);
            Assert.That(positions[1].nodeLocation, Is.Zero);
            Assert.That(positions[2].refType, Is.EqualTo(RefType.RefTypeBB));
            Assert.That(positions[2].nodeLocation, Is.EqualTo(1));
            Assert.That(compiler.codeGen!.CalleeRegArgMaskLiveIn.IsSet(REG_RCX), Is.True);
            Assert.That(compiler.codeGen.CalleeRegArgMaskLiveIn.IsSet(REG_RDX), Is.True);
        });
    }

    [TestCase(true, true, false, false)]
    [TestCase(true, false, true, true)]
    [TestCase(false, true, false, true)]
    [TestCase(false, false, true, true)]
    [TestCase(false, false, false, false)]
    public static void IncomingRegisterLivenessUsesMappedFieldOrUnionWithParameter(
        bool mappedField, bool parameterUsed, bool mappedUsed, bool expectedLive)
    {
        WithBuilder(2, (compiler, allocator) => {
            _ = CreateBlocks(compiler, BBJ_RETURN);
            compiler.info.compArgsCount = 1;
            compiler.lvaTable[0].lvIsParam = true;
            compiler.lvaTable[1].lvIsParamRegTarget = true;
            compiler.lvaTable[1].lvIsStructField = mappedField;
            compiler.lvaTable[1].lvParentLcl = 0;
            if (mappedField)
            {
                compiler.lvaTable[0].Type = TYP_STRUCT;
                compiler.lvaTable[0].lvPromoted = true;
                compiler.lvaTable[0].lvFieldCnt = 1;
                compiler.lvaTable[0].lvFieldLclStart = 1;
            }
            compiler.lvaTable[0].setLvRefCnt(checked((ushort)(parameterUsed ? 1 : 0)));
            compiler.lvaTable[1].setLvRefCnt(checked((ushort)(mappedUsed ? 1 : 0)));
            var segment = AbiPassingSegment.InRegister(REG_RCX, 0, 4);
            compiler.lvaParameterPassingInfo = [
                AbiPassingInformation.FromSegment(compiler, false, segment),
            ];
            compiler._paramRegLocalMappings = [new ParameterRegisterLocalMapping(segment, 1, 0)];

            BuildIntervals(allocator);

            Assert.That(compiler.codeGen!.CalleeRegArgMaskLiveIn.IsSet(REG_RCX),
                Is.EqualTo(expectedLive));
            Assert.That(compiler.codeGen.CalleeRegArgMaskLiveIn.IsSet(REG_RDX), Is.False);
        });
    }

#if DEBUG
    [Test]
    public static void ParameterPreferenceStressSelectsDistinctIncomingRegisters()
    {
        WithBuilder(2, (compiler, allocator) => {
            _ = CreateBlocks(compiler, BBJ_RETURN);
            compiler.info.compFullName = nameof(ParameterPreferenceStressSelectsDistinctIncomingRegisters);
            compiler.info.compArgsCount = 2;
            for (var index = 0; index < 2; index++)
            {
                compiler.lvaTable[index].lvIsParam = true;
            }
            compiler.lvaParameterPassingInfo = [
                AbiPassingInformation.FromSegment(compiler, false,
                    AbiPassingSegment.InRegister(REG_RCX, 0, 4)),
                AbiPassingInformation.FromSegment(compiler, false,
                    AbiPassingSegment.InRegister(REG_RDX, 0, 4)),
            ];

            BuildIntervals(allocator);
            StressParameterPreferences(allocator);

            var first = allocator.localVarIntervals?[0]
                ?? throw new AssertionException("First parameter interval is missing.");
            var second = allocator.localVarIntervals[1]
                ?? throw new AssertionException("Second parameter interval is missing.");
            var incoming = genSingleTypeRegMask(REG_RCX) | genSingleTypeRegMask(REG_RDX);
            Assert.That(first.registerPreferences, Is.EqualTo(incoming));
            Assert.That(second.registerPreferences, Is.EqualTo(incoming));
        });
    }
#endif

    [Test]
    public static void ExceptionalIncomingEdgeDoesNotInheritPredecessorLocations()
    {
        WithBuilder(1, (compiler, allocator) => {
            var blocks = CreateBlocks(compiler, BBJ_EHCATCHRET, BBJ_RETURN);
            blocks[0].SetKindAndTargetEdge(BBJ_EHCATCHRET,
                compiler.fgAddRefPred(blocks[1], blocks[0]));
            compiler.info.compInitMem = true;
            foreach (var block in blocks)
            {
                SetOps.AddElemD(compiler, block.bbLiveIn, 0);
                SetOps.AddElemD(compiler, block.bbLiveOut, 0);
            }

            BuildIntervals(allocator);

            Assert.That(BlockInfo(allocator)![blocks[1].bbNum].hasEHBoundaryIn, Is.True);
            Assert.That(BlockInfo(allocator)![blocks[1].bbNum].predBBNum, Is.Zero);
            Assert.That(allocator.refPositions.Exists(position =>
                position.refType is RefType.RefTypeDummyDef), Is.False);
        });
    }

    [Test]
    public static void UnreachableLiveInUsesPreviousLayoutBlockAndGetsDummyDefinition()
    {
        WithBuilder(1, (compiler, allocator) => {
            var blocks = CreateBlocks(compiler, BBJ_RETURN, BBJ_RETURN);
            SetOps.AddElemD(compiler, blocks[1].bbLiveIn, 0);
            SetOps.AddElemD(compiler, blocks[1].bbLiveOut, 0);

            BuildIntervals(allocator);

            Assert.That(BlockInfo(allocator)![blocks[1].bbNum].predBBNum,
                Is.EqualTo((uint)blocks[0].bbNum));
            var dummy = allocator.refPositions.FindAll(position =>
                position.refType is RefType.RefTypeDummyDef);
            Assert.That(dummy, Has.Count.EqualTo(1));
            Assert.That(dummy[0].bbNum, Is.EqualTo((uint)blocks[1].bbNum));
            Assert.That(dummy[0].nodeLocation, Is.EqualTo(3));
            Assert.That(dummy[0].regOptional, Is.True);
            var boundary = allocator.refPositions.Find(position =>
                (position.refType is RefType.RefTypeBB) &&
                (position.bbNum == blocks[1].bbNum))
                ?? throw new AssertionException("Unreachable block is missing its boundary reference.");
            Assert.That(boundary.nodeLocation, Is.EqualTo(dummy[0].nodeLocation));
            Assert.That(allocator.refPositions.IndexOf(dummy[0]),
                Is.LessThan(allocator.refPositions.IndexOf(boundary)));
        });
    }

    [Test]
    public static void ThrowWithoutPredecessorsDoesNotInheritPreviousLayoutLocations()
    {
        WithBuilder(1, (compiler, allocator) => {
            var blocks = CreateBlocks(compiler, BBJ_RETURN, BBJ_THROW);
            SetOps.AddElemD(compiler, blocks[1].bbLiveIn, 0);
            SetOps.AddElemD(compiler, blocks[1].bbLiveOut, 0);

            BuildIntervals(allocator);

            Assert.That(BlockInfo(allocator)![blocks[1].bbNum].predBBNum, Is.Zero);
            var dummy = allocator.refPositions.FindAll(position =>
                position.refType is RefType.RefTypeDummyDef);
            Assert.That(dummy, Has.Count.EqualTo(1));
            Assert.That(dummy[0].bbNum, Is.EqualTo((uint)blocks[1].bbNum));
        });
    }

    [TestCase(3.0, 1.0, 1)]
    [TestCase(1.0, 3.0, 2)]
    public static void MultipleVisitedPredecessorsSelectHighestWeight(
        double firstWeight, double secondWeight, int expectedPredecessorIndex)
    {
        WithBuilder(1, (compiler, allocator) => {
            var blocks = CreateBlocks(compiler, BBJ_COND, BBJ_ALWAYS, BBJ_ALWAYS, BBJ_RETURN);
            var firstEdge = compiler.fgAddRefPred(blocks[1], blocks[0]);
            var secondEdge = compiler.fgAddRefPred(blocks[2], blocks[0]);
            firstEdge.Likelihood = 0.5;
            secondEdge.Likelihood = 0.5;
            blocks[0].SetCond(firstEdge, secondEdge);
            blocks[1].SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(blocks[3], blocks[1]));
            blocks[2].SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(blocks[3], blocks[2]));
            blocks[1].bbWeight = firstWeight;
            blocks[2].bbWeight = secondWeight;

            BuildIntervals(allocator);

            Assert.That(BlockInfo(allocator)![blocks[3].bbNum].predBBNum,
                Is.EqualTo((uint)blocks[expectedPredecessorIndex].bbNum));
        });
    }

    [Test]
    public static void LocalStoreAndLastUseRetainNodeReferenceOrdering()
    {
        WithBuilder(1, (compiler, allocator) => {
            var block = CreateBlocks(compiler, BBJ_RETURN)[0];
            var returnType = new ReturnTypeDesc();
            returnType.InitializeReturnType(compiler, TYP_INT, null, CorInfoCallConvExtension.Managed);
            compiler.compRetTypeDesc = returnType;
            var value = compiler.gtNewIconNode(TYP_INT, 7);
            var store = new GenTreeLclVar(TYP_INT, 0, value);
            var load = compiler.gtNewLclvNode(TYP_INT, 0);
            load.SetLastUse(0, true);
            var ret = new GenTreeUnOp(GT_RETURN, TYP_INT, load);
            block.InsertAtEnd(value);
            block.InsertAtEnd(store);
            block.InsertAtEnd(load);
            block.InsertAtEnd(ret);
            SetOps.AddElemD(compiler, block.bbVarDef, 0);

            BuildIntervals(allocator);

            var interval = allocator.localVarIntervals?[0]
                ?? throw new AssertionException("Candidate local has no interval.");
            Assert.That(interval.firstRefPosition?.refType, Is.EqualTo(RefType.RefTypeDef));
            Assert.That(interval.lastRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(interval.lastRefPosition?.lastUse, Is.True);
            Assert.That(allocator.refPositions[0].nodeLocation, Is.EqualTo(1));
            Assert.That(ReferenceBuildLocation(allocator), Is.GreaterThan(3));
            Assert.That((load.Flags & GTF_VAR_DEATH) != 0, Is.True);
        });
    }

    [Test]
    public static void LiveOutLocalClearsLastUseBeforeFollowingBlockUse()
    {
        WithBuilder(1, (compiler, allocator) => {
            var blocks = CreateBlocks(compiler, BBJ_ALWAYS, BBJ_RETURN);
            blocks[0].SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(blocks[1], blocks[0]));
            var returnType = new ReturnTypeDesc();
            returnType.InitializeReturnType(compiler, TYP_INT, null, CorInfoCallConvExtension.Managed);
            compiler.compRetTypeDesc = returnType;

            var value = compiler.gtNewIconNode(TYP_INT, 7);
            var store = new GenTreeLclVar(TYP_INT, 0, value);
            var firstUse = compiler.gtNewLclvNode(TYP_INT, 0);
            firstUse.SetLastUse(0, true);
            var one = compiler.gtNewIconNode(TYP_INT, 1);
            var add = new GenTreeOp(GT_ADD, TYP_INT, firstUse, one) { IsUnusedValue = true };
            blocks[0].InsertAtEnd(value);
            blocks[0].InsertAtEnd(store);
            blocks[0].InsertAtEnd(firstUse);
            blocks[0].InsertAtEnd(one);
            blocks[0].InsertAtEnd(add);
            var finalUse = compiler.gtNewLclvNode(TYP_INT, 0);
            finalUse.SetLastUse(0, true);
            blocks[1].InsertAtEnd(finalUse);
            blocks[1].InsertAtEnd(new GenTreeUnOp(GT_RETURN, TYP_INT, finalUse));
            SetOps.AddElemD(compiler, blocks[0].bbLiveOut, 0);
            SetOps.AddElemD(compiler, blocks[1].bbLiveIn, 0);

            BuildIntervals(allocator);

            var interval = allocator.localVarIntervals?[0]
                ?? throw new AssertionException("Candidate local has no interval.");
            var firstReference = interval.firstRefPosition
                ?? throw new AssertionException("Candidate local has no store reference.");
            var firstUsePosition = firstReference.nextRefPosition;
            Assert.That(firstUsePosition?.bbNum, Is.EqualTo((uint)blocks[0].bbNum));
            Assert.That(firstUsePosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(firstUsePosition?.lastUse, Is.False);
            Assert.That(interval.lastRefPosition?.bbNum, Is.EqualTo((uint)blocks[1].bbNum));
            Assert.That(interval.lastRefPosition?.lastUse, Is.True);
        });
    }

    private static void WithBuilder(int candidateCount, Action<Compiler, LinearScan> action)
    {
        LinearScanLocalCandidatesTests.WithCandidates(candidateCount, (compiler, allocator) => {
#if DEBUG
            compiler.fgSafeFlowEdgeCreation = true;
#endif
            compiler.fgPredsComputed = true;
            compiler.compHndBBtab = [];
            var codeGen = compiler.codeGen ?? throw new AssertionException("Codegen state is missing.");
            codeGen.IsFramePointerRequired = false;
            codeGen.IsFramePointerUsed = false;
            codeGen.IsFrameRequired = false;
            codeGen.RegSet.rsClearRegsModified();
            compiler.rpMustCreateEBPCalled = true;
            action(compiler, allocator);
        });
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
            block.bbLiveIn = SetOps.MakeEmpty(compiler);
            block.bbLiveOut = SetOps.MakeEmpty(compiler);
            block.bbVarUse = SetOps.MakeEmpty(compiler);
            block.bbVarDef = SetOps.MakeEmpty(compiler);
            if (index != 0)
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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildIntervalsWithLocals")]
    private static extern void BuildIntervals(LinearScan allocator);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "stressSetRandomParameterPreferences")]
    private static extern void StressParameterPreferences(LinearScan allocator);
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_blockInfo")]
    private static extern ref LsraBlockInfo[]? BlockInfo(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_referenceBuildLocation")]
    private static extern ref uint ReferenceBuildLocation(LinearScan allocator);
}
