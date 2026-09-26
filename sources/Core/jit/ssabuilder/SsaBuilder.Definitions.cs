// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public sealed partial class SsaBuilder
{
    private struct RenameDefVisitor : ILocalDefVisitor
    {
        private readonly SsaBuilder _builder;
        private readonly GenTree _defNode;
        private readonly BasicBlock _block;

        public bool AnyDefs { get; private set; }

        public RenameDefVisitor(SsaBuilder builder, GenTree defNode, BasicBlock block)
        {
            _builder = builder;
            _defNode = defNode;
            _block = block;
        }

        public GenTree.VisitResult Visit<TDef>(TDef def) where TDef : struct, ILocalDef
        {
            AnyDefs = true;
            var localDefNode = def.DefNode;
            assert((localDefNode.Flags & GTF_VAR_DEF) != 0);
            var isEntire = def.IsEntire(_builder._compiler);
            assert(isEntire || ((localDefNode.Flags & GTF_VAR_USEASG) != 0));

            var lclNum = def.LclNum;
            ref var varDsc = ref _builder._compiler.lvaGetDesc(lclNum);
            if (varDsc.lvInSsa)
            {
                var ssaNum = _builder.RenamePushDef(_defNode, _block, lclNum, isEntire);
                def.SetSsaNum(_builder._compiler, ssaNum);
                assert(!varDsc.IsAddressExposed);
            }

            return GenTree.VisitResult.Continue;
        }
    }

    private void RenameDef(GenTree defNode, BasicBlock block)
    {
        assert(defNode.Oper.IsStore || defNode.Oper.IsCall);

        var visitor = new RenameDefVisitor(this, defNode, block);
        _ = defNode.VisitLogicalLocalDefs(_compiler, ref visitor);
        _ = defNode.VisitPhysicalLocalDefNodes(_compiler, lcl => {
            if (_compiler.lvaGetDesc(lcl.AsLclVarCommon().LclNum).IsAddressExposed)
            {
                RenamePushMemoryDef(lcl, block);
            }

            return GenTree.VisitResult.Continue;
        });

        if (!visitor.AnyDefs)
        {
            if (defNode.Oper.IsCall)
            {
                // Calls are pure or have arbitrary memory effects captured by the block's live-out state.
                return;
            }

            RenamePushMemoryDef(defNode, block);
        }
    }

    private int RenamePushDef(GenTree defNode, BasicBlock block, int lclNum, bool isFullDef)
    {
        ref var varDsc = ref _compiler.lvaGetDesc(lclNum);
        assert(varDsc.lvInSsa && !varDsc.lvPromoted);

        var ssaNum = varDsc.lvPerSsaData.AllocSsaNum();
        ref var ssaDesc = ref varDsc.GetPerSsaData(ssaNum);
        ssaDesc = defNode.Oper.IsCall
            ? new LclSsaVarDsc(block)
            : new LclSsaVarDsc(block, defNode.AsLclVarCommon());

        if (!isFullDef)
        {
            // The node names the new definition; its old value is recorded in the descriptor.
            var useSsaNum = _renameStack.Top(lclNum);
            ssaDesc.UseDefSsaNum = useSsaNum;
            varDsc.GetPerSsaData(useSsaNum).AddUse(block);
        }

        _renameStack.Push(block, lclNum, ssaNum);
        if (!defNode.IsPhiDefn && block.HasPotentialEHSuccs(_compiler))
        {
            AddDefToEHSuccessorPhis(block, lclNum, ssaNum);
        }

        return ssaNum;
    }

    private void RenamePushMemoryDef(GenTree defNode, BasicBlock block)
    {
        if (((block.bbMemoryHavoc & (1 << (int)GcHeap)) != 0) || !_compiler.ehBlockHasExnFlowDsc(block))
        {
            return;
        }

        var hasByrefHavoc = (block.bbMemoryHavoc & (1 << (int)ByrefExposed)) != 0;
        if (defNode.Oper.IsAnyLocal && hasByrefHavoc)
        {
            return;
        }

        var ssaNum = _compiler.AllocMemorySsaNum();
        if (!hasByrefHavoc)
        {
            _renameStack.PushMemory(ByrefExposed, block, ssaNum);
            _compiler.GetMemorySsaMap(ByrefExposed)[defNode] = ssaNum;
#if DEBUG
            if (_compiler.verboseSsa)
            {
                jitprintf("Node ");
                Compiler.printTreeId(defNode);
                jitprintf($" (in try block) may define memory; ssa # = {ssaNum}.\n");
            }
#endif
            AddMemoryDefToEHSuccessorPhis(ByrefExposed, block, ssaNum);
        }

        if (!defNode.Oper.IsAnyLocal)
        {
            if (_compiler.byrefStatesMatchGcHeapStates)
            {
                assert(!hasByrefHavoc);
                assert(_compiler.GetMemorySsaMap(GcHeap)[defNode] == ssaNum);
                assert(block.bbMemorySsaPhiFunc[(int)GcHeap] == block.bbMemorySsaPhiFunc[(int)ByrefExposed]);
            }
            else
            {
                if (!hasByrefHavoc)
                {
                    ssaNum = _compiler.AllocMemorySsaNum();
                }

                _renameStack.PushMemory(GcHeap, block, ssaNum);
                _compiler.GetMemorySsaMap(GcHeap)[defNode] = ssaNum;
                AddMemoryDefToEHSuccessorPhis(GcHeap, block, ssaNum);
            }
        }
    }

    private void RenameLclUse(GenTreeLclVarCommon lclNode, BasicBlock block)
    {
        assert((lclNode.Flags & GTF_VAR_DEF) == 0);
        var lclNum = lclNode.LclNum;
        ref var lclVar = ref _compiler.lvaGetDesc(lclNum);
        int ssaNum;

        if (!lclVar.lvInSsa)
        {
            ssaNum = SsaConfig.RESERVED_SSA_NUM;
        }
        else
        {
            assert(!lclVar.lvPromoted);
            ssaNum = _renameStack.Top(lclNum);
            lclVar.GetPerSsaData(ssaNum).AddUse(block);
        }

        lclNode.SsaNum = ssaNum;
    }

    private void AddDefToEHSuccessorPhis(BasicBlock block, int lclNum, int ssaNum)
    {
        assert(block.HasPotentialEHSuccs(_compiler));
        ref var varDsc = ref _compiler.lvaTable[lclNum];
        assert(varDsc.lvTracked);

#if DEBUG
        if (_compiler.verboseSsa)
        {
            logf($"Definition of local V{lclNum:D2}/d:{ssaNum} in block {FMT_BB(block.bbNum)} has potential EH successors; adding as phi arg to EH successors\n");
        }
#endif
        var lclIndex = varDsc._varIndex;
        _ = block.VisitEHSuccs(_compiler, succ => {
            if (!VarSetOps.IsMember(_compiler, succ.bbLiveIn, lclIndex))
            {
                return BasicBlockVisit.Continue;
            }

#if DEBUG
            var phiFound = false;
#endif
            foreach (var statement in succ.Statements)
            {
                if (!statement.IsPhiDefnStmt)
                {
                    break;
                }

                var phiDef = statement.RootNode.AsLclVar();
                assert(phiDef.IsPhiDefn);
                if (phiDef.LclNum == lclNum)
                {
                    AddPhiArg(succ, statement, phiDef.Data.AsPhi(), lclNum, ssaNum, block);
#if DEBUG
                    phiFound = true;
#endif
                    break;
                }
            }

#if DEBUG
            ref var ehDsc = ref _compiler.ehGetBlockHndDsc(succ);
            var dfsTree = _compiler._dfsTree;
            assert(phiFound || (!Unsafe.IsNullRef(in ehDsc) && (dfsTree is not null) &&
                                !dfsTree.Contains(ehDsc.ebdTryBeg)));
#endif
            return BasicBlockVisit.Continue;
        });
    }

    private void AddMemoryDefToEHSuccessorPhis(MemoryKind kind, BasicBlock block, int ssaNum)
    {
        assert(block.HasPotentialEHSuccs(_compiler));
        if (block.isBBCallFinallyPairTail)
        {
            return;
        }

#if DEBUG
        if (_compiler.verboseSsa)
        {
            logf($"Definition of {kind}/d:{ssaNum} in block {FMT_BB(block.bbNum)} has potential EH successors; adding as phi arg to EH successors.\n");
        }
#endif
        _ = block.VisitEHSuccs(_compiler, succ => {
            if ((succ.bbMemoryLiveIn & (1 << (int)kind)) == 0)
            {
                return BasicBlockVisit.Continue;
            }

            ref var handlerMemoryPhi = ref succ.bbMemorySsaPhiFunc[(int)kind];
#if DEBUG
            if (_compiler.byrefStatesMatchGcHeapStates)
            {
                assert(kind is not GcHeap);
                if (kind is ByrefExposed)
                {
                    assert(handlerMemoryPhi == succ.bbMemorySsaPhiFunc[(int)GcHeap]);
                }
            }
#endif
            if (handlerMemoryPhi == BasicBlock.EmptyMemoryPhiDef)
            {
                handlerMemoryPhi = new BasicBlock.MemoryPhiArg(ssaNum);
            }
            else
            {
#if DEBUG
                for (var current = handlerMemoryPhi; current is not null; current = current._nextArg)
                {
                    assert(current.SsaNum != ssaNum);
                }
#endif
                handlerMemoryPhi = new BasicBlock.MemoryPhiArg(ssaNum, handlerMemoryPhi);
            }

#if DEBUG
            if (_compiler.verboseSsa)
            {
                logf($"   Added phi arg u:{ssaNum} for {kind} to phi defn in handler block {FMT_BB(succ.bbNum)}.\n");
            }
#endif
            if ((kind is ByrefExposed) && _compiler.byrefStatesMatchGcHeapStates)
            {
                succ.bbMemorySsaPhiFunc[(int)GcHeap] = handlerMemoryPhi;
            }

            return BasicBlockVisit.Continue;
        });
    }
}
