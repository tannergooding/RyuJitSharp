// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class SsaBuilder
{
    private void BlockRenameVariables(BasicBlock block)
    {
        foreach (var kind in new AllMemoryKinds())
        {
            if ((kind is GcHeap) && _compiler.byrefStatesMatchGcHeapStates)
            {
                assert(block.bbMemorySsaPhiFunc[(int)kind] == block.bbMemorySsaPhiFunc[(int)ByrefExposed]);
                assert(kind > ByrefExposed);
                block.bbMemorySsaNumIn[(int)kind] = _renameStack.TopMemory(ByrefExposed);
            }
            else if (block.bbMemorySsaPhiFunc[(int)kind] is not null)
            {
                var ssaNum = _compiler.AllocMemorySsaNum();
                _renameStack.PushMemory(kind, block, ssaNum);
#if DEBUG
                if (_compiler.verboseSsa)
                {
                    logf($"Ssa # for {kind} phi on entry to {FMT_BB(block.bbNum)} is {ssaNum}.\n");
                }
#endif
                block.bbMemorySsaNumIn[(int)kind] = ssaNum;
            }
            else
            {
                block.bbMemorySsaNumIn[(int)kind] = _renameStack.TopMemory(kind);
            }
        }

        foreach (var statement in block.Statements)
        {
            foreach (var tree in statement.TreeList)
            {
                if (tree.Oper.IsStore || tree.Oper.IsCall)
                {
                    RenameDef(tree, block);
                }
                else if (tree.Oper is GT_LCL_VAR or GT_LCL_FLD)
                {
                    RenameLclUse(tree.AsLclVarCommon(), block);
                }
            }
        }

        foreach (var kind in new AllMemoryKinds())
        {
            var kindSet = 1 << (int)kind;
            if ((kind is GcHeap) && _compiler.byrefStatesMatchGcHeapStates)
            {
                assert(kind > ByrefExposed);
                assert(((block.bbMemoryDef & kindSet) != 0) ==
                       ((block.bbMemoryDef & (1 << (int)ByrefExposed)) != 0));
                block.bbMemorySsaNumOut[(int)kind] = _renameStack.TopMemory(ByrefExposed);
            }
            else if ((block.bbMemoryDef & kindSet) != 0)
            {
                var ssaNum = _compiler.AllocMemorySsaNum();
                _renameStack.PushMemory(kind, block, ssaNum);
                if (block.HasPotentialEHSuccs(_compiler))
                {
                    AddMemoryDefToEHSuccessorPhis(kind, block, ssaNum);
                }

                block.bbMemorySsaNumOut[(int)kind] = ssaNum;
            }
            else
            {
                block.bbMemorySsaNumOut[(int)kind] = _renameStack.TopMemory(kind);
            }

#if DEBUG
            if (_compiler.verboseSsa)
            {
                logf($"Ssa # for {kind} on entry to {FMT_BB(block.bbNum)} is {block.bbMemorySsaNumIn[(int)kind]}; on exit is {block.bbMemorySsaNumOut[(int)kind]}.\n");
            }
#endif
        }
    }

    private struct SsaRenameDomTreeVisitor : IDomTreeVisitor<SsaRenameDomTreeVisitor>
    {
        private readonly Compiler _compiler;
        private readonly SsaBuilder _builder;
        private readonly SsaRenameState _renameStack;

        public SsaRenameDomTreeVisitor(Compiler compiler, SsaBuilder builder, SsaRenameState renameStack)
        {
            _compiler = compiler;
            _builder = builder;
            _renameStack = renameStack;
        }

        public readonly void Begin()
        {
        }

        public readonly void PreOrderVisit(BasicBlock block)
        {
            _builder.BlockRenameVariables(block);
            _builder.AddPhiArgsToSuccessors(block);
        }

        public readonly void PostOrderVisit(BasicBlock block)
        {
            _renameStack.PopBlockStacks(block);
        }

        public readonly void End()
        {
        }

        public void WalkTree(FlowGraphDominatorTree tree)
            => IDomTreeVisitor<SsaRenameDomTreeVisitor>.WalkTree(ref this, _compiler, tree);
    }

    private void RenameVariables()
    {
        JITDUMP("*************** In SsaBuilder::RenameVariables()\n");

        _compiler.Metrics.VarsInSsa = 0;
        var firstBlock = _compiler.fgFirstBB;
        assert(firstBlock is not null);

        // Parameters and initialized or live-in locals have virtual definitions before entry.
        for (var lclNum = 0; lclNum < _compiler.lvaCount; lclNum++)
        {
            ref var varDsc = ref _compiler.lvaGetDesc(lclNum);
            if (!varDsc.lvInSsa)
            {
                continue;
            }

            _compiler.Metrics.VarsInSsa++;
            assert(varDsc.lvTracked);
            if (varDsc.lvIsParam || _compiler.info.compInitMem || varDsc.lvMustInit ||
                (varTypeIsGC(varDsc.Type) && !varDsc.lvHasExplicitInit) ||
                VarSetOps.IsMember(_compiler, firstBlock.bbLiveIn, varDsc._varIndex))
            {
                var ssaNum = varDsc.lvPerSsaData.AllocSsaNum();
                varDsc.GetPerSsaData(ssaNum) = new LclSsaVarDsc();
                assert(ssaNum == SsaConfig.FIRST_SSA_NUM);
                _renameStack.Push(firstBlock, lclNum, ssaNum);
            }
        }

        var initMemorySsaNum = _compiler.AllocMemorySsaNum();
        assert(initMemorySsaNum == SsaConfig.FIRST_SSA_NUM);
        foreach (var kind in new AllMemoryKinds())
        {
            if ((kind is GcHeap) && _compiler.byrefStatesMatchGcHeapStates)
            {
                continue;
            }
            _renameStack.PushMemory(kind, firstBlock, initMemorySsaNum);
        }

        // Value numbering expects an initial memory name even on unreachable blocks.
        var dfsTree = _compiler._dfsTree;
        assert(dfsTree is not null);
        foreach (var block in _compiler.Blocks)
        {
            if (!dfsTree.Contains(block))
            {
                foreach (var kind in new AllMemoryKinds())
                {
                    block.bbMemorySsaNumIn[(int)kind] = initMemorySsaNum;
                    block.bbMemorySsaNumOut[(int)kind] = initMemorySsaNum;
                }
            }
        }

        var domTree = _compiler._domTree;
        assert(domTree is not null);
        var visitor = new SsaRenameDomTreeVisitor(_compiler, this, _renameStack);
        visitor.WalkTree(domTree);
    }
}
