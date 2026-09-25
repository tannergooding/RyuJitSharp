// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void siBeginBlock(BasicBlock block)
    {
        if (!_compiler.opts.compScopeInfo || (_compiler.info.compVarScopesCount == 0) || siInFuncletRegion)
        {
            return;
        }

        if (block == _compiler.fgFirstFuncletBB)
        {
            // Native codegen does not report scopes in funclets.
            siInFuncletRegion = true;
            JITDUMP($"Scope info: found beginning of funclet region at block {FMT_BB(block.bbNum)}; ignoring following blocks\n");
            return;
        }

#if DEBUG
        if (_compiler.verbose)
        {
            jitprintf($"\nScope info: begin block {FMT_BB(block.bbNum)}, IL range ");
            block.dspBlockILRange();
            jitprintf("\n");
        }
#endif
        if (block.bbCodeOffs == BAD_IL_OFFSET)
        {
            JITDUMP("Scope info: ignoring block beginning\n");
            return;
        }

        // With tracked locals, liveness is responsible for the debug state.
        if (_compiler.lvaTrackedCount <= 0)
        {
            siOpenScopesForNonTrackedVars(block, siLastEndOffs);
        }
    }

    private void siOpenScopesForNonTrackedVars(BasicBlock block, IL_OFFSET lastBlockILEndOffset)
    {
#if TARGET_WASM
        // Native Wasm defers scopes because structured control flow need not follow IL offset order.
        return;
#else
        var beginOffs = block.bbCodeOffs;
        if (_compiler.opts.OptimizationDisabled)
        {
            if (lastBlockILEndOffset != beginOffs)
            {
                assert(beginOffs > 0);
                assert(lastBlockILEndOffset < beginOffs);
                JITDUMP($"Scope info: found offset hole. lastOffs={lastBlockILEndOffset}, currOffs={beginOffs}\n");

                // Funclets moved out of line can leave matching scope boundaries in the gap.
                while (true)
                {
                    ref var scope = ref _compiler.compGetNextEnterScope(beginOffs - 1, scan: true);
                    if (Unsafe.IsNullRef(in scope))
                    {
                        break;
                    }
                    JITDUMP($"Scope info: skipping enter scope, LVnum={scope.vsdLVnum}\n");
                }

                while (true)
                {
                    ref var scope = ref _compiler.compGetNextExitScope(beginOffs - 1, scan: true);
                    if (Unsafe.IsNullRef(in scope))
                    {
                        break;
                    }
                    JITDUMP($"Scope info: skipping exit scope, LVnum={scope.vsdLVnum}\n");
                }
            }

            while (true)
            {
                ref var scope = ref _compiler.compGetNextEnterScope(beginOffs);
                if (Unsafe.IsNullRef(in scope))
                {
                    break;
                }
                ref var local = ref _compiler.lvaGetDesc(scope.vsdVarNum);

                if (_compiler.opts.compDbgCode || (local.lvRefCnt() > 0))
                {
                    JITDUMP($"Scope info: opening scope, LVnum={scope.vsdLVnum} [{scope.vsdLifeBeg:X3}..{scope.vsdLifeEnd:X3})\n");
                    getVariableLiveKeeper().siStartVariableLiveRange(in local, scope.vsdVarNum);
                    assert(!local.lvTracked || VarSetOps.IsMember(_compiler, block.bbLiveIn, local._varIndex));
                }
                else
                {
                    JITDUMP($"Skipping open scope for V{scope.vsdVarNum:D2}, unreferenced\n");
                }
            }
        }
#endif
    }

    public void siEndBlock(BasicBlock block)
    {
        assert(_compiler.opts.compScopeInfo && (_compiler.info.compVarScopesCount > 0));
        if (siInFuncletRegion)
        {
            return;
        }

        var endOffs = block.bbCodeOffsEnd;
        if (endOffs == BAD_IL_OFFSET)
        {
            JITDUMP("Scope info: ignoring block end\n");
            return;
        }

        siLastEndOffs = endOffs;
    }
}
