// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
#if TARGET_WASM
    public PhaseStatus fgWasmTransformSccs()
    {
        assert(fgNodeThreading == NodeThreading.LIR);

        var fgWasm = new FgWasm(this);
        var dfsTree = fgWasm.WasmDfs(out _);
        fgWasm.SetDfsAndTraits(dfsTree);

        var tryRegions = FlowGraphTryRegions.Build(this, dfsTree);
#if DEBUG
        if (verbose)
        {
            FlowGraphTryRegions.Dump(tryRegions);
        }
#endif

        if (tryRegions.HasSideEntry())
        {
            NYI_WASM("Try region side entry survived fgWasmRepairTryEntries");
            throw new FatalJitException(CorJitResult.CORJIT_SKIPPED);
        }

        var loops = FlowGraphNaturalLoops.Find(dfsTree);
        var transformed = false;

        if (loops.ImproperLoopHeaders > 0)
        {
            JITDUMP("\nThere are irreducible loops.\n");

            var sccs = new ArrayStack<Scc>();
            fgWasm.WasmFindSccs(sccs);
            assert(!sccs.Empty());

            transformed = fgWasm.WasmTransformSccs(sccs);
            assert(transformed);

#if DEBUG
            var dfsTree2 = fgWasm.WasmDfs(out var hasBlocksOnlyReachableViaEH2);
            assert(!hasBlocksOnlyReachableViaEH2);
            var loops2 = FlowGraphNaturalLoops.Find(dfsTree2);
            assert(loops2.ImproperLoopHeaders == 0);
#endif
        }

        return transformed ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }
#endif
}
