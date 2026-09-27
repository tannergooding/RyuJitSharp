// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, optcse.cpp.

#if DEBUG
using System;
#endif
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Compiler
{
    private void optValnumCSE_InitDataFlow()
    {
        assert(optCSEtab is not null);
        cseLivenessTraits = new BitVecTraits(this, (optCSECandidateCount * 2) + 1);
        cseCallKillsMask = BitVecOps.MakeEmpty(cseLivenessTraits);
        for (var index = 1; index <= optCSECandidateCount; index++)
        {
            BitVecOps.AddElemD(cseLivenessTraits, cseCallKillsMask, getCSEAvailBit(index));
        }

        foreach (var block in Blocks)
        {
            var initializeEmpty = (block == fgFirstBB) || bbIsHandlerBeg(block);
            block.bbCseIn = initializeEmpty
                ? BitVecOps.MakeEmpty(cseLivenessTraits)
                : BitVecOps.MakeFull(cseLivenessTraits);
            block.bbCseOut = BitVecOps.MakeFull(cseLivenessTraits);
            block.bbCseGen = BitVecOps.MakeEmpty(cseLivenessTraits);
        }

        foreach (var descriptor in optCSEtab)
        {
            assert(descriptor is not null);
            var index = descriptor.csdIndex;
            for (var occurrence = descriptor.csdTreeList; occurrence is not null; occurrence = occurrence.tslNext)
            {
                var block = occurrence.tslBlock;
                assert(block.bbCseGen is not null);
                BitVecOps.AddElemD(cseLivenessTraits, block.bbCseGen, getCSEAvailBit(index));
                if (!block.HasFlag(BBF_HAS_CALL))
                {
                    BitVecOps.AddElemD(cseLivenessTraits, block.bbCseGen, getCSEAvailCrossCallBit(index));
                }
            }
        }

        if (compIsAsync)
        {
            optValnumCSE_SetUpAsyncByrefKills();
        }

        foreach (var block in Blocks)
        {
            if (!block.HasFlag(BBF_HAS_CALL) || BitVecOps.IsEmpty(cseLivenessTraits, block.bbCseGen))
            {
                continue;
            }

            var stmt = block.LastStmt;
            assert(stmt is not null);
            var foundCall = false;
            while (!foundCall)
            {
                for (var tree = stmt.RootNode; tree is not null; tree = tree.Prev)
                {
                    if (IS_CSE_INDEX(tree._cseNum))
                    {
                        BitVecOps.AddElemD(cseLivenessTraits, block.bbCseGen,
                            getCSEAvailCrossCallBit(GET_CSE_INDEX(tree._cseNum)));
                    }
                    if (tree.Oper is GT_CALL)
                    {
                        foundCall = true;
                        break;
                    }
                }
                if (stmt == block.FirstStmt)
                {
                    break;
                }
                stmt = stmt.PrevStmt!;
            }
        }

#if DEBUG
        if (verbose)
        {
            var headerPrinted = false;

            foreach (var block in Blocks)
            {
                if (!BitVecOps.IsEmpty(cseLivenessTraits, block.bbCseGen))
                {
                    if (!headerPrinted)
                    {
                        jitprintf("\nBlocks that generate CSE def/uses\n");
                        headerPrinted = true;
                    }

                    jitprintf($"{FMT_BB(block.bbNum)} cseGen = ");
                    optPrintCSEDataFlowSet(block.bbCseGen);
                    jitprintf("\n");
                }
            }
        }
#endif
    }

    private void optValnumCSE_SetUpAsyncByrefKills()
    {
        assert(cseLivenessTraits is not null);
        cseAsyncKillsMask = BitVecOps.MakeFull(cseLivenessTraits);
        var anyAsyncKills = false;
        for (var index = 1; index <= optCSECandidateCount; index++)
        {
            var descriptor = optCSEfindDsc(index);
            var tree = descriptor.csdTreeList.tslTree;
            var isByRef = (tree.Type is TYP_BYREF) ||
                ((tree.Type is TYP_STRUCT) && tree.GetLayout(this).HasGCByRef());
            if (isByRef)
            {
                BitVecOps.RemoveElemD(cseLivenessTraits, cseAsyncKillsMask, getCSEAvailBit(index));
                BitVecOps.RemoveElemD(cseLivenessTraits, cseAsyncKillsMask, getCSEAvailCrossCallBit(index));
                anyAsyncKills = true;
            }
        }
        if (!anyAsyncKills)
        {
            return;
        }

        foreach (var block in Blocks)
        {
            Statement? asyncStmt = null;
            GenTree? asyncCall = null;
            var stmt = block.LastStmt;
            if (stmt is null)
            {
                continue;
            }

            while (asyncCall is null)
            {
                if ((stmt.RootNode.Flags & GTF_CALL) != 0)
                {
                    for (var tree = stmt.RootNode; tree is not null; tree = tree.Prev)
                    {
                        if ((tree.Oper is GT_CALL) && tree.AsCall().IsAsync)
                        {
                            asyncStmt = stmt;
                            asyncCall = tree;
                            break;
                        }
                    }
                }
                if (stmt == block.FirstStmt)
                {
                    break;
                }
                stmt = stmt.PrevStmt!;
            }
            if (asyncCall is null)
            {
                continue;
            }

            assert(block.bbCseGen is not null);
            assert(block.bbCseOut is not null);
            BitVecOps.IntersectionD(cseLivenessTraits, block.bbCseGen, cseAsyncKillsMask);
            BitVecOps.IntersectionD(cseLivenessTraits, block.bbCseOut, cseAsyncKillsMask);

            for (var currentStmt = asyncStmt; currentStmt is not null; currentStmt = currentStmt.NextStmt)
            {
                for (var tree = currentStmt == asyncStmt ? asyncCall : currentStmt.TreeListBegin;
                     tree is not null; tree = tree.Next)
                {
                    if (IS_CSE_INDEX(tree._cseNum))
                    {
                        var index = GET_CSE_INDEX(tree._cseNum);
                        BitVecOps.AddElemD(cseLivenessTraits, block.bbCseGen, getCSEAvailBit(index));
                        BitVecOps.AddElemD(cseLivenessTraits, block.bbCseOut, getCSEAvailBit(index));
                    }
                }
            }
        }
    }

    private struct CSE_DataFlow(Compiler compiler) : DataFlow.ICallback
    {
        private nint[] _preMergeOut = BitVecOps.UninitVal();

        public void StartMerge(BasicBlock block)
        {
            assert(compiler.cseLivenessTraits is not null);
            assert(block.bbCseOut is not null);
            BitVecOps.Assign(compiler.cseLivenessTraits, ref _preMergeOut, block.bbCseOut);
        }

        public readonly void Merge(BasicBlock block, BasicBlock predecessor, int duplicateCount)
        {
            assert(compiler.cseLivenessTraits is not null);
            assert(block.bbCseIn is not null);
            assert(predecessor.bbCseOut is not null);
            BitVecOps.IntersectionD(compiler.cseLivenessTraits, block.bbCseIn, predecessor.bbCseOut);
        }

        public readonly void MergeHandler(BasicBlock block, BasicBlock firstTryBlock, BasicBlock lastTryBlock)
        {
            // CSE_INTO_HANDLERS is disabled in the native configuration.
        }

        public readonly bool EndMerge(BasicBlock block)
        {
            assert(compiler.cseLivenessTraits is not null);
            assert(block.bbCseIn is not null);
            assert(block.bbCseGen is not null);
            assert(block.bbCseOut is not null);
            if (block.HasFlag(BBF_NO_CSE_IN))
            {
                BitVecOps.ClearD(compiler.cseLivenessTraits, block.bbCseIn);
            }

            if (!block.HasFlag(BBF_HAS_CALL) || BitVecOps.IsEmpty(compiler.cseLivenessTraits, block.bbCseIn))
            {
                BitVecOps.DataFlowD(compiler.cseLivenessTraits, block.bbCseOut, block.bbCseGen, block.bbCseIn);
            }
            else
            {
                assert(compiler.cseCallKillsMask is not null);
                var killed = BitVecOps.MakeCopy(compiler.cseLivenessTraits, block.bbCseIn);
                BitVecOps.IntersectionD(compiler.cseLivenessTraits, killed, compiler.cseCallKillsMask);
                BitVecOps.DataFlowD(compiler.cseLivenessTraits, block.bbCseOut, block.bbCseGen, killed);
            }

            return !BitVecOps.Equal(compiler.cseLivenessTraits, block.bbCseOut, _preMergeOut);
        }
    }

    private void optValnumCSE_DataFlow()
    {
        JITDUMP("\nPerforming DataFlow for ValnumCSE's\n");

        var callback = new CSE_DataFlow(this);
        new DataFlow(this).ForwardAnalysis(ref callback);

#if DEBUG
        if (verbose)
        {
            jitprintf("\nAfter performing DataFlow for ValnumCSE's\n");

            foreach (var block in Blocks)
            {
                jitprintf($"{FMT_BB(block.bbNum)}\n in: ");
                optPrintCSEDataFlowSet(block.bbCseIn);
                jitprintf("\ngen: ");
                optPrintCSEDataFlowSet(block.bbCseGen);
                jitprintf("\nout: ");
                optPrintCSEDataFlowSet(block.bbCseOut);
                jitprintf("\n");
            }

            jitprintf("\n");
        }
#endif
    }

    private void optValnumCSE_Availability()
    {
        assert(cseLivenessTraits is not null);
        assert(vnStore is not null);
        JITDUMP("Labeling the CSEs with Use/Def information\n");

        var available = BitVecOps.MakeEmpty(cseLivenessTraits);
        foreach (var block in Blocks)
        {
            compCurBB = block;
            assert(block.bbCseIn is not null);
            BitVecOps.Assign(cseLivenessTraits, ref available, block.bbCseIn);
            foreach (var stmt in block.Statements)
            {
                if (stmt.IsPhiDefnStmt)
                {
                    continue;
                }

                foreach (var tree in stmt.TreeList)
                {
                    var isUse = false;
                    var isDef = false;
                    if (IS_CSE_INDEX(tree._cseNum))
                    {
                        var index = GET_CSE_INDEX(tree._cseNum);
                        var availBit = getCSEAvailBit(index);
                        var crossCallBit = getCSEAvailCrossCallBit(index);
                        var descriptor = optCSEfindDsc(index);
                        var weight = block.getBBWeight(this);

                        isUse = BitVecOps.IsMember(cseLivenessTraits, available, availBit);
                        isDef = !isUse;
#if DEBUG
                        var madeLiveAcrossCall = false;
#endif
                        if (isUse && !descriptor.csdLiveAcrossCall &&
                            !BitVecOps.IsMember(cseLivenessTraits, available, crossCallBit))
                        {
                            descriptor.csdLiveAcrossCall = true;
#if DEBUG
                            madeLiveAcrossCall = true;
#endif
                        }

#if DEBUG
                        if (isDef)
                        {
                            assert(!BitVecOps.IsMember(cseLivenessTraits, available, crossCallBit));
                        }

                        if (verbose)
                        {
                            jitprintf($"{FMT_BB(block.bbNum)} [{tree.TreeId:D6}] " +
                                $"{(isUse ? "Use" : "Def")} of {FMT_CSE(index)} [weight={refCntWtd2str(weight)}]" +
                                $"{(madeLiveAcrossCall ? " *** Now Live Across Call ***" : "")}\n");
                        }
#endif
                        if (descriptor.defExcSetPromise == ValueNumStore.NoVN)
                        {
                            tree._cseNum = NO_CSE;
                            JITDUMP(" Abandoned - CSE candidate has defs with different exception sets!\n");
                            continue;
                        }

                        var exceptions = vnStore.VNExceptionSet(tree._vnPair.Liberal);
                        if (isDef)
                        {
                            if (descriptor.defExcSetCurrent == ValueNumStore.VNForNull())
                            {
                                descriptor.defExcSetCurrent = exceptions;
                            }
                            else if (descriptor.defExcSetCurrent != exceptions)
                            {
                                var intersection = vnStore.VNExcSetIntersection(
                                    descriptor.defExcSetCurrent, exceptions);
#if DEBUG
                                if (verbose)
                                {
                                    jitprintf(">>> defExcSetCurrent is ");
                                    vnStore.vnDumpExc(this, descriptor.defExcSetCurrent);
                                    jitprintf("\n");
                                    jitprintf(">>> theLiberalExcSet is ");
                                    vnStore.vnDumpExc(this, exceptions);
                                    jitprintf("\n");
                                    jitprintf(">>> the intersectionExcSet is ");
                                    vnStore.vnDumpExc(this, intersection);
                                    jitprintf("\n");
                                }
#endif
                                assert(vnStore.VNExcIsSubset(descriptor.defExcSetCurrent, intersection));
                                descriptor.defExcSetCurrent = intersection;
                            }

                            if ((descriptor.defExcSetPromise != ValueNumStore.VNForEmptyExcSet()) &&
                                !vnStore.VNExcIsSubset(exceptions, descriptor.defExcSetPromise))
                            {
                                descriptor.defExcSetPromise = ValueNumStore.NoVN;
                                tree._cseNum = NO_CSE;
                                JITDUMP(" Abandon - CSE candidate has defs with exception sets that do not satisfy some CSE use\n");
                                continue;
                            }

                            descriptor.csdDefCount++;
                            descriptor.csdDefWtCnt += weight;
                            tree._cseNum = checked((sbyte)TO_CSE_DEF(tree._cseNum));
                            BitVecOps.AddElemD(cseLivenessTraits, available, availBit);
                            BitVecOps.AddElemD(cseLivenessTraits, available, crossCallBit);
                        }
                        else
                        {
                            if (exceptions != ValueNumStore.VNForEmptyExcSet())
                            {
                                if (descriptor.defExcSetCurrent == ValueNumStore.VNForNull() ||
                                    vnStore.VNExcIsSubset(descriptor.defExcSetCurrent, exceptions))
                                {
                                    descriptor.defExcSetPromise = vnStore.VNExcSetUnion(
                                        descriptor.defExcSetPromise, exceptions);
                                }
                                if (!vnStore.VNExcIsSubset(descriptor.defExcSetPromise, exceptions))
                                {
                                    tree._cseNum = NO_CSE;
                                    JITDUMP(" NO_CSE - This use has an exception set item that isn't contained in the defs!\n");
                                    continue;
                                }
                            }

                            descriptor.csdUseCount++;
                            descriptor.csdUseWtCnt += weight;
                        }
                    }

                    if (tree.Oper is GT_CALL && !BitVecOps.IsEmpty(cseLivenessTraits, available) && !isUse)
                    {
                        assert(cseCallKillsMask is not null);
                        BitVecOps.IntersectionD(cseLivenessTraits, available, cseCallKillsMask);
                        if (tree.AsCall().IsAsync && compIsAsync)
                        {
                            assert(cseAsyncKillsMask is not null);
                            BitVecOps.IntersectionD(cseLivenessTraits, available, cseAsyncKillsMask);
                        }
                        if (isDef)
                        {
                            BitVecOps.AddElemD(cseLivenessTraits, available,
                                getCSEAvailCrossCallBit(GET_CSE_INDEX(tree._cseNum)));
                        }
                    }
                }
            }
        }
    }

#if DEBUG
    private void optPrintCSEDataFlowSet(ReadOnlySpan<nint> set, bool includeBits = true)
    {
        assert(cseLivenessTraits is not null);

        if (includeBits)
        {
            jitprintf($"{BitVecOps.ToString(cseLivenessTraits, set)} ");
        }

        var first = true;

        for (var index = 1; index <= optCSECandidateCount; index++)
        {
            if (BitVecOps.IsMember(cseLivenessTraits, set, getCSEAvailBit(index)))
            {
                if (!first)
                {
                    jitprintf(", ");
                }

                var isAvailableAcrossCall = BitVecOps.IsMember(cseLivenessTraits, set, getCSEAvailCrossCallBit(index));
                jitprintf($"{FMT_CSE(index)}{(isAvailableAcrossCall ? ".c" : "")}");
                first = false;
            }
        }
    }
#endif
}
