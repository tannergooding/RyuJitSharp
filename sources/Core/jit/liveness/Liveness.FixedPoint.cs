// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Liveness<TLiveness>
    where TLiveness : ILivenessPolicy
{
    private nint[] _liveIn = [];
    private nint[] _liveOut = [];
    private nint[] _ehHandlerLiveVars = [];
    private MemoryKindSet _memoryLiveIn;
    private MemoryKindSet _memoryLiveOut;

    public void DoLiveVarAnalysis()
    {
        _liveIn = VarSetOps.MakeEmpty(_compiler);
        _liveOut = VarSetOps.MakeEmpty(_compiler);
        _ehHandlerLiveVars = VarSetOps.MakeEmpty(_compiler);

        var keepAliveThis = _compiler.lvaKeepAliveAndReportThis() &&
            _compiler.lvaTable[_compiler.info.compThisArg].lvTracked;
        var dfs = _compiler._dfsTree;
        assert(dfs is not null);

        bool changed;
        do
        {
            changed = false;
            VarSetOps.ClearD(_compiler, _liveIn);
            VarSetOps.ClearD(_compiler, _liveOut);
            _memoryLiveIn = 0;
            _memoryLiveOut = 0;

            for (var index = 0; index < dfs.PostOrderCount; index++)
            {
                if (PerBlockAnalysis(dfs.GetPostOrder(index), keepAliveThis))
                {
                    changed = true;
                }
            }
        }
        while (changed && dfs.HasCycle);

        assert(!_compiler.fgRngChkThrowAdded);
#if DEBUG
        if (_compiler.fgBBcount != dfs.PostOrderCount)
        {
            foreach (var block in _compiler.Blocks)
            {
                if (dfs.Contains(block))
                {
                    continue;
                }

                assert(!block.HasFlag(BBF_THROW_HELPER));
            }
        }

        if (_compiler.verbose)
        {
            jitprintf("\nBB liveness after DoLiveVarAnalysis():\n\n");
            _compiler.fgDispBBLiveness();
        }
#endif
    }

    private bool PerBlockAnalysis(BasicBlock block, bool keepAliveThis)
    {
        VarSetOps.ClearD(_compiler, _liveOut);
        _memoryLiveOut = 0;
        if (block.EndsWithJmpMethod(_compiler))
        {
            for (var local = 0; local < _compiler.info.compArgsCount; local++)
            {
                ref var descriptor = ref _compiler.lvaTable[local];
                noway_assert(!descriptor.lvPromoted);
                if (descriptor.lvTracked)
                {
                    VarSetOps.AddElemD(_compiler, _liveOut, descriptor._varIndex);
                }
            }
        }

        if (TLiveness.IsEarly && _compiler.opts.IsOSR && block.HasFlag(BBF_RECURSIVE_TAILCALL))
        {
            assert(_compiler.fgEntryBB is not null);
            // Before morph expands a tailcall-to-loop, model the OSR backedge:
            // the OSR state index need not appear as an explicit argument use.
            VarSetOps.UnionD(_compiler, _liveOut, _compiler.fgEntryBB.bbLiveIn);
        }

        _ = block.VisitRegularSuccs(_compiler, successor => {
            VarSetOps.UnionD(_compiler, _liveOut, successor.bbLiveIn);
            _memoryLiveOut |= successor.bbMemoryLiveIn;
            return BasicBlockVisit.Continue;
        });

        if (keepAliveThis)
        {
            VarSetOps.AddElemD(_compiler, _liveOut, _compiler.lvaTable[_compiler.info.compThisArg]._varIndex);
        }

        VarSetOps.LivenessD(_compiler, _liveIn, block.bbVarDef, block.bbVarUse, _liveOut);
        if (block.HasPotentialEHSuccs(_compiler))
        {
            VarSetOps.ClearD(_compiler, _ehHandlerLiveVars);
            _compiler.fgAddHandlerLiveVars(block, _ehHandlerLiveVars, ref _memoryLiveOut);
            VarSetOps.UnionD(_compiler, _liveIn, _ehHandlerLiveVars);
            VarSetOps.UnionD(_compiler, _liveOut, _ehHandlerLiveVars);
        }

        // A memory definition need not overwrite the memory used by a successor.
        _memoryLiveIn = _memoryLiveOut | block.bbMemoryUse;

        var liveInChanged = !VarSetOps.Equal(_compiler, block.bbLiveIn, _liveIn);
        if (liveInChanged || !VarSetOps.Equal(_compiler, block.bbLiveOut, _liveOut))
        {
            VarSetOps.Assign(_compiler, ref block.bbLiveIn, _liveIn);
            VarSetOps.Assign(_compiler, ref block.bbLiveOut, _liveOut);
        }

        var memoryLiveInChanged = block.bbMemoryLiveIn != _memoryLiveIn;
        if (memoryLiveInChanged || (block.bbMemoryLiveOut != _memoryLiveOut))
        {
            block.bbMemoryLiveIn = _memoryLiveIn;
            block.bbMemoryLiveOut = _memoryLiveOut;
        }

        return liveInChanged || memoryLiveInChanged;
    }
}
