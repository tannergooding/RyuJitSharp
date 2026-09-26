// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class SsaBuilder
{
    private void AddPhiArgsToSuccessors(BasicBlock block)
    {
        _ = block.VisitAllSuccs(_compiler, succ => {
            foreach (var statement in succ.Statements)
            {
                if (!statement.IsPhiDefnStmt)
                {
                    break;
                }

                var store = statement.RootNode.AsLclVar();
                var phi = store.Data.AsPhi();
                var lclNum = store.LclNum;
                var ssaNum = _renameStack.Top(lclNum);
                AddPhiArg(succ, statement, phi, lclNum, ssaNum, block);
            }

            foreach (var kind in new AllMemoryKinds())
            {
                ref var succMemoryPhi = ref succ.bbMemorySsaPhiFunc[(int)kind];
                if (succMemoryPhi is null)
                {
                    continue;
                }

                if ((kind is GcHeap) && _compiler.byrefStatesMatchGcHeapStates)
                {
                    assert(kind > ByrefExposed);
                    assert(block.bbMemorySsaNumOut[(int)kind] == block.bbMemorySsaNumOut[(int)ByrefExposed]);
                    var byrefPhi = succ.bbMemorySsaPhiFunc[(int)ByrefExposed];
                    assert((byrefPhi == succMemoryPhi) ||
                           (byrefPhi?._nextArg == (succMemoryPhi == BasicBlock.EmptyMemoryPhiDef ? null : succMemoryPhi)));
                    succMemoryPhi = byrefPhi;
                    continue;
                }

                var ssaNum = block.bbMemorySsaNumOut[(int)kind];
                if (succMemoryPhi == BasicBlock.EmptyMemoryPhiDef)
                {
                    succMemoryPhi = new BasicBlock.MemoryPhiArg(ssaNum);
                }
                else
                {
                    var found = false;
                    for (var current = succMemoryPhi; current is not null; current = current._nextArg)
                    {
                        if (current.SsaNum == ssaNum)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        succMemoryPhi = new BasicBlock.MemoryPhiArg(ssaNum, succMemoryPhi);
                    }
                }

#if DEBUG
                if (_compiler.verboseSsa)
                {
                    logf($"  Added phi arg for {kind} u:{ssaNum} from {FMT_BB(block.bbNum)} in {FMT_BB(succ.bbNum)}.\n");
                }
#endif
            }

            if (_compiler.bbIsTryBeg(succ))
            {
                // Entering a try brings the predecessor's live-out names into its handlers.
                assert(succ.hasTryIndex);
                var tryIndex = succ.TryIndex;
                while (tryIndex != EHblkDsc.NO_ENCLOSING_INDEX)
                {
                    if (block.hasTryIndex)
                    {
                        for (var blockTryIndex = block.TryIndex;
                             blockTryIndex != EHblkDsc.NO_ENCLOSING_INDEX;
                             blockTryIndex = _compiler.ehGetEnclosingTryIndex(blockTryIndex))
                        {
                            if (blockTryIndex == tryIndex)
                            {
                                tryIndex = EHblkDsc.NO_ENCLOSING_INDEX;
                                break;
                            }
                        }

                        if (tryIndex == EHblkDsc.NO_ENCLOSING_INDEX)
                        {
                            break;
                        }
                    }

                    ref var succTry = ref _compiler.ehGetDsc(tryIndex);
                    if (succTry.ebdTryBeg != succ)
                    {
                        break;
                    }

                    if (succTry.HasFilter)
                    {
                        AddPhiArgsToNewlyEnteredHandler(block, succ, succTry.ebdFilter);
                    }
                    AddPhiArgsToNewlyEnteredHandler(block, succ, succTry.ebdHndBeg);

                    tryIndex = succTry.ebdEnclosingTryIndex;
                }
            }

            return BasicBlockVisit.Continue;
        });
    }

    private void AddPhiArgsToNewlyEnteredHandler(BasicBlock predEnterBlock, BasicBlock enterBlock,
                                                 BasicBlock handlerStart)
    {
        foreach (var statement in handlerStart.Statements)
        {
            var tree = statement.RootNode;
            if (!tree.IsPhiDefn)
            {
                break;
            }

            var lclNum = tree.AsLclVar().LclNum;
            ref var varDsc = ref _compiler.lvaGetDesc(lclNum);
            if (!varDsc.lvTracked || !VarSetOps.IsMember(_compiler, predEnterBlock.bbLiveOut, varDsc._varIndex))
            {
                continue;
            }

            var phi = tree.AsLclVar().Data.AsPhi();
            var ssaNum = _renameStack.Top(lclNum);
            AddPhiArg(handlerStart, statement, phi, lclNum, ssaNum, enterBlock);
        }

        foreach (var kind in new AllMemoryKinds())
        {
            ref var handlerMemoryPhi = ref handlerStart.bbMemorySsaPhiFunc[(int)kind];
            if (handlerMemoryPhi is null)
            {
                continue;
            }

            var ssaNum = predEnterBlock.bbMemorySsaNumOut[(int)kind];
            if ((kind is GcHeap) && _compiler.byrefStatesMatchGcHeapStates)
            {
                assert(kind > ByrefExposed);
                assert(ssaNum == predEnterBlock.bbMemorySsaNumOut[(int)ByrefExposed]);
                var byrefPhi = handlerStart.bbMemorySsaPhiFunc[(int)ByrefExposed];
                assert((byrefPhi is not null) && (byrefPhi.SsaNum == ssaNum));
                handlerMemoryPhi = byrefPhi;
                continue;
            }

            // Entry edges can repeat a memory name; avoid a quadratic scan of handler phis.
            handlerMemoryPhi = handlerMemoryPhi == BasicBlock.EmptyMemoryPhiDef
                ? new BasicBlock.MemoryPhiArg(ssaNum)
                : new BasicBlock.MemoryPhiArg(ssaNum, handlerMemoryPhi);

#if DEBUG
            if (_compiler.verboseSsa)
            {
                logf($"  Added phi arg for {kind} u:{ssaNum} from {FMT_BB(predEnterBlock.bbNum)} in {FMT_BB(handlerStart.bbNum)}.\n");
            }
#endif
        }
    }
}
