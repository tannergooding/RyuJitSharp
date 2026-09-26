// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

public sealed partial class SsaBuilder
{
    private readonly Compiler _compiler;
    private readonly SsaRenameState _renameStack;

    public SsaBuilder(Compiler compiler)
    {
        _compiler = compiler;
        _renameStack = new SsaRenameState(compiler);
    }

    private static Statement? GetPhiNode(BasicBlock block, int lclNum)
    {
        foreach (var statement in block.Statements)
        {
            if (!statement.IsPhiDefnStmt)
            {
                break;
            }

            if (statement.RootNode.AsLclVar().LclNum == lclNum)
            {
                return statement;
            }
        }

        return null;
    }

    private static Statement InsertPhi(Compiler compiler, BasicBlock block, int lclNum)
    {
        var type = compiler.lvaGetDesc(lclNum).Type;
        var phi = new GenTreePhi(type);
        phi.SetCosts(0, 0);

        var store = compiler.gtNewStoreLclVarNode(lclNum, phi);
        store.SetCosts(0, 0);
        // Preserve the native type quirk to avoid costing-induced tail-duplication differences.
        store.Type = type;

        var statement = compiler.gtNewStmt(store);
        statement.TreeListBegin = phi;
        phi.Next = store;
        store.Prev = phi;

#if DEBUG
        var seqNum = 1;
        foreach (var node in statement.TreeList)
        {
            node._seqNum = seqNum++;
        }
#endif

        compiler.fgInsertStmtAtBeg(block, statement);
        JITDUMP($"Added PHI definition for V{lclNum:D2} at start of {FMT_BB(block.bbNum)}.\n");
        return statement;
    }

    private void AddPhiArg(BasicBlock block, Statement statement, GenTreePhi phi, int lclNum, int ssaNum, BasicBlock pred)
    {
        var isHandlerEntry = _compiler.bbIsHandlerBeg(block);
        // Several definitions can reach a handler from the same predecessor.
        foreach (var use in phi.Uses)
        {
            var phiArg = use.Node.AsPhiArg();
            if (phiArg.PredBB == pred)
            {
                if (phiArg.SsaNum == ssaNum)
                {
                    return;
                }

                noway_assert(isHandlerEntry);
            }
        }

        AddNewPhiArg(_compiler, block, statement, phi, lclNum, ssaNum, pred);
    }

    private static void AddNewPhiArg(Compiler compiler, BasicBlock block, Statement statement,
                                    GenTreePhi phi, int lclNum, int ssaNum, BasicBlock pred)
    {
        var type = compiler.lvaGetDesc(lclNum).Type;
        var phiArg = new GenTreePhiArg(type, lclNum, ssaNum, pred);
        phiArg.SetCosts(0, 0);
        phi.FirstUse = new GenTreePhi.Use(phiArg, phi.FirstUse);

        var head = statement.TreeListBegin;
        assert((head is not null) && (head.Oper is GT_PHI or GT_PHI_ARG));
        statement.TreeListBegin = phiArg;
        phiArg.Next = head;
        head.Prev = phiArg;

        ref var ssaDesc = ref compiler.lvaGetDesc(lclNum).GetPerSsaData(ssaNum);
        ssaDesc.AddPhiUse(block);

#if DEBUG
        var seqNum = 1;
        foreach (var node in statement.TreeList)
        {
            node._seqNum = seqNum++;
        }
        if (compiler.verboseSsa)
        {
            logf($"Added PHI arg u:{ssaNum} for V{lclNum:D2} from {FMT_BB(pred.bbNum)} in {FMT_BB(block.bbNum)}.\n");
        }
#endif
    }

    private void InsertPhiFunctions()
    {
        JITDUMP("*************** In SsaBuilder::InsertPhiFunctions()\n");

        var dfsTree = _compiler._dfsTree;
        assert(dfsTree is not null);
        var postOrder = dfsTree.GetPostOrder();
        var count = dfsTree.PostOrderCount;

        var domTree = _compiler._domTree;
        assert(domTree is not null);
        _compiler._domFrontiers = FlowGraphDominanceFrontiers.Build(domTree);
        _compiler.EndPhase(PHASE_BUILD_SSA_DF);

#if DEBUG
        if (_compiler.verboseSsa)
        {
            domTree.Dump();
        }
#endif

        List<BasicBlock> blockIDF = [];
        JITDUMP("Inserting phi functions:\n");

        for (var i = 0; i < count; i++)
        {
            var block = postOrder[i];
#if DEBUG
            if (_compiler.verboseSsa)
            {
                logf($"Considering dominance frontier of block {FMT_BB(block.bbNum)}:\n");
            }
#endif
            blockIDF.Clear();
            _compiler._domFrontiers.ComputeIteratedDominanceFrontier(block, blockIDF);

#if DEBUG
            if (_compiler.verboseSsa)
            {
                jitprintf($"IDF({FMT_BB(block.bbNum)}) := {{");
                for (var index = 0; index < blockIDF.Count; index++)
                {
                    jitprintf($"{(index == 0 ? "" : ",")}{FMT_BB(blockIDF[index].bbNum)}");
                }
                jitprintf("}\n");
            }
#endif
            if (blockIDF.Count == 0)
            {
                continue;
            }

            _ = VarSetOps.VisitBits(_compiler, block.bbVarDef, varIndex => {
                assert(_compiler.lvaTrackedToVarNum is not null);
                var lclNum = _compiler.lvaTrackedToVarNum[varIndex];
#if DEBUG
                if (_compiler.verboseSsa)
                {
                    logf($"  Considering local var V{lclNum:D2}:\n");
                }
#endif
                if (!_compiler.lvaGetDesc(lclNum).lvInSsa)
                {
#if DEBUG
                    if (_compiler.verboseSsa)
                    {
                        logf("  Skipping because it is excluded.\n");
                    }
#endif
                    return true;
                }

                foreach (var frontierBlock in blockIDF)
                {
#if DEBUG
                    if (_compiler.verboseSsa)
                    {
                        logf($"     Considering {FMT_BB(frontierBlock.bbNum)} in dom frontier of {FMT_BB(block.bbNum)}:\n");
                    }
#endif
                    if (!VarSetOps.IsMember(_compiler, frontierBlock.bbLiveIn, varIndex))
                    {
                        continue;
                    }

                    if (GetPhiNode(frontierBlock, lclNum) is null)
                    {
                        InsertPhi(_compiler, frontierBlock, lclNum);
                    }
                }
                return true;
            });

            if (block.bbMemoryDef != 0)
            {
                foreach (var frontierBlock in blockIDF)
                {
#if DEBUG
                    if (_compiler.verboseSsa)
                    {
                        logf($"     Considering {FMT_BB(frontierBlock.bbNum)} in dom frontier of {FMT_BB(block.bbNum)} for Memory phis:\n");
                    }
#endif
                    foreach (var kind in new AllMemoryKinds())
                    {
                        if ((kind is GcHeap) && _compiler.byrefStatesMatchGcHeapStates)
                        {
                            assert(kind > ByrefExposed);
                            frontierBlock.bbMemorySsaPhiFunc[(int)kind] =
                                frontierBlock.bbMemorySsaPhiFunc[(int)ByrefExposed];
                            continue;
                        }

                        var kindSet = 1 << (int)kind;
                        if (((block.bbMemoryDef & kindSet) == 0) ||
                            ((frontierBlock.bbMemoryLiveIn & kindSet) == 0))
                        {
                            continue;
                        }

                        if (frontierBlock.bbMemorySsaPhiFunc[(int)kind] is null)
                        {
                            JITDUMP($"Inserting phi definition for {kind} at start of {FMT_BB(frontierBlock.bbNum)}.\n");
                            frontierBlock.bbMemorySsaPhiFunc[(int)kind] = BasicBlock.EmptyMemoryPhiDef;
                        }
                    }
                }
            }
        }

        _compiler.EndPhase(PHASE_BUILD_SSA_INSERT_PHIS);
    }
}
