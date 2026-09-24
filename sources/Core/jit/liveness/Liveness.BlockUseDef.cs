// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Liveness<TLiveness>
    where TLiveness : ILivenessPolicy
{
    private nint[] _curUseSet = [];
    private nint[] _curDefSet = [];
    private MemoryKindSet _curMemoryUse;
    private MemoryKindSet _curMemoryDef;
    private MemoryKindSet _curMemoryHavoc;

    public unsafe void PerBlockLocalVarLiveness()
    {
        JITDUMP("*************** In Liveness::PerBlockLocalVarLiveness()\n");

        var livenessVarEpoch = _compiler.CurLVEpoch;
        VarSetOps.AssignNoCopy(_compiler, ref _curUseSet, VarSetOps.MakeEmpty(_compiler));
        VarSetOps.AssignNoCopy(_compiler, ref _curDefSet, VarSetOps.MakeEmpty(_compiler));

        // The states can share numbering until a byref-exposed def is not also a GC heap def.
        _compiler.byrefStatesMatchGcHeapStates = true;

        var dfs = _compiler._dfsTree;
        assert(dfs is not null);
        for (var index = dfs.PostOrderCount; index != 0; index--)
        {
            var block = dfs.GetPostOrder(index - 1);
            VarSetOps.ClearD(_compiler, _curUseSet);
            VarSetOps.ClearD(_compiler, _curDefSet);
            if (TLiveness.ComputeMemoryLiveness)
            {
                _curMemoryUse = 0;
                _curMemoryDef = 0;
                _curMemoryHavoc = 0;
            }

            _compiler.compCurBB = block;
            if (TLiveness.IsLIR)
            {
                assert(block.IsLIR);
                foreach (var node in block)
                {
                    PerNodeLocalVarLiveness(node);
                }
            }
            else if (!TLiveness.IsEarly)
            {
                assert(_compiler.fgNodeThreading is NodeThreading.AllTrees);
                for (var statement = block.GetFirstNonPhiDef(); statement is not null; statement = statement.NextStmt)
                {
                    _compiler.compCurStmt = statement;
                    foreach (var node in statement.TreeList)
                    {
                        PerNodeLocalVarLiveness(node);
                    }
                }
            }
            else
            {
                assert(_compiler.fgNodeThreading is NodeThreading.AllLocals);
                if (_compiler.compQmarkUsed)
                {
                    foreach (var statement in block.Statements)
                    {
                        var qmark = _compiler.fgGetTopLevelQmark(statement.RootNode, out var destination);
                        if (qmark is null)
                        {
                            foreach (var local in statement.LocalsTreeList)
                            {
                                MarkUseDef(local);
                            }
                        }
                        else
                        {
                            assert((destination is null) ||
                                ((statement.TreeListEnd == destination) && ((destination.Flags & GTF_VAR_DEF) != 0)));

                            // Conditional defs must not hide exposed uses in either qmark arm.
                            // Only the unconditional destination at the end can kill a local.
                            foreach (var local in statement.LocalsTreeList)
                            {
                                var isUse = (local.Flags & GTF_VAR_DEF) == 0;
                                var conditional = local != destination;
                                if (isUse || !conditional)
                                {
                                    MarkUseDef(local);
                                }
                            }
                        }
                    }
                }
                else
                {
                    foreach (var statement in block.Statements)
                    {
                        foreach (var local in statement.LocalsTreeList)
                        {
                            MarkUseDef(local);
                        }
                    }
                }
            }

            if ((block.Kind is BBJ_RETURN) && _compiler.compMethodRequiresPInvokeFrame)
            {
                assert(!_compiler.opts.ShouldUsePInvokeHelpers || (_compiler.info.compLvFrameListRoot == BAD_VAR_NUM));
                if (!_compiler.opts.ShouldUsePInvokeHelpers)
                {
                    // On 64-bit, only IL stubs pop the frame in the epilog.
#if TARGET_64BIT
                    if (_compiler.opts.jitFlags->IsSet(JitFlags.JIT_FLAG_IL_STUB))
#endif
                    {
                        MarkFrameRootUse();
                    }
                }
            }

            VarSetOps.Assign(_compiler, ref block.bbVarUse, _curUseSet);
            VarSetOps.Assign(_compiler, ref block.bbVarDef, _curDefSet);
            VarSetOps.AssignNoCopy(_compiler, ref block.bbLiveIn, VarSetOps.MakeEmpty(_compiler));
            if (TLiveness.ComputeMemoryLiveness)
            {
                block.bbMemoryUse = _curMemoryUse;
                block.bbMemoryDef = _curMemoryDef;
                block.bbMemoryHavoc = _curMemoryHavoc;
                block.bbMemoryLiveIn = 0;
            }
        }

        noway_assert(livenessVarEpoch == _compiler.CurLVEpoch);
#if DEBUG
        if (_compiler.verbose)
        {
            foreach (var block in _compiler.Blocks)
            {
                var allVars = VarSetOps.Union(_compiler, block.bbVarUse, block.bbVarDef);
                jitprintf($"{FMT_BB(block.bbNum)} USE({VarSetOps.Count(_compiler, block.bbVarUse)})=");
                _compiler.lvaDispVarSet(block.bbVarUse, allVars);
                if (TLiveness.ComputeMemoryLiveness)
                {
                    for (var memoryKind = ByrefExposed; memoryKind < MemoryKindCount; memoryKind++)
                    {
                        if ((block.bbMemoryUse & (1 << (int)memoryKind)) != 0)
                        {
                            jitprintf($" + {memoryKind}");
                        }
                    }
                }

                jitprintf($"\n     DEF({VarSetOps.Count(_compiler, block.bbVarDef)})=");
                _compiler.lvaDispVarSet(block.bbVarDef, allVars);
                if (TLiveness.ComputeMemoryLiveness)
                {
                    for (var memoryKind = ByrefExposed; memoryKind < MemoryKindCount; memoryKind++)
                    {
                        if ((block.bbMemoryDef & (1 << (int)memoryKind)) != 0)
                        {
                            jitprintf($" + {memoryKind}");
                        }
                        if ((block.bbMemoryHavoc & (1 << (int)memoryKind)) != 0)
                        {
                            jitprintf("*");
                        }
                    }
                }
                jitprintf("\n\n");
            }

            if (TLiveness.ComputeMemoryLiveness)
            {
                jitprintf($"** Memory liveness computed, GcHeap states and ByrefExposed states {(_compiler.byrefStatesMatchGcHeapStates ? "match" : "diverge")}\n");
            }
        }
#endif
    }

    private void MarkFrameRootUse()
    {
        ref var descriptor = ref _compiler.lvaGetDesc(_compiler.info.compLvFrameListRoot);
        if (descriptor.lvTracked && !VarSetOps.IsMember(_compiler, _curDefSet, descriptor._varIndex))
        {
            VarSetOps.AddElemD(_compiler, _curUseSet, descriptor._varIndex);
        }
    }
}
