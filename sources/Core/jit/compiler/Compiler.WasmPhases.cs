// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public unsafe PhaseStatus fgWasmProfInstrument()
    {
        assert(Configuration.TARGET_WASM);

        if (!JitConfig.WasmPerformanceInstrumentation.contains(
                info.compMethodHnd,
                info.compClassHnd,
                &info.compMethodInfo->args))
        {
            return PhaseStatus.MODIFIED_NOTHING;
        }

        void InsertSamplepoint(BasicBlock block)
        {
            GenTree samplepoint = gtNewHelperCallNode(
                TYP_VOID,
                CORINFO_HELP_WASM_PROF_SAMPLEPOINT);
            samplepoint = fgMorphCall(samplepoint.AsCall());
            _ = gtSetEvalOrder(samplepoint);

            var range = block;
            if ((block == fgFirstBB) && opts.IsReversePInvoke)
            {
                GenTree? insertAfter = null;
                foreach (var node in range)
                {
                    if (node.Oper.IsCall &&
                        (node.AsCall().IsHelperCall(CORINFO_HELP_JIT_REVERSE_PINVOKE_ENTER) ||
                         node.AsCall().IsHelperCall(CORINFO_HELP_JIT_REVERSE_PINVOKE_ENTER_TRACK_TRANSITIONS)))
                    {
                        insertAfter = node;
                        break;
                    }
                }

                noway_assert(insertAfter is not null);
                range.InsertAfter(insertAfter, LIR.SeqTree(this, samplepoint));
            }
            else
            {
                range.InsertBefore(range.FirstNonCatchArgNode(), LIR.SeqTree(this, samplepoint));
            }
        }

        noway_assert(fgFirstBB is not null);
        var firstBlock = fgFirstBB!;
        var fgWasm = new FgWasm(this);
        var dfsTree = fgWasm.WasmDfs(out var hasBlocksOnlyReachableViaEH);
        if (hasBlocksOnlyReachableViaEH)
        {
            return PhaseStatus.MODIFIED_NOTHING;
        }

        var traits = dfsTree.PostOrderTraits();
        var samplepointBlocks = BitVecOps.MakeEmpty(traits);
        BitVecOps.AddElemD(traits, samplepointBlocks, firstBlock.bbPostorderNum);

        for (var i = 0; i < dfsTree.PostOrderCount; i++)
        {
            var source = dfsTree.GetPostOrder(i);
            _ = FgWasm.VisitWasmSuccs(this, source, target => {
                assert(dfsTree.Contains(target));
                if (dfsTree.IsAncestor(target, source))
                {
                    BitVecOps.AddElemD(traits, samplepointBlocks, target.bbPostorderNum);
                }

                return BasicBlockVisit.Continue;
            });
        }

        var samplepointsAdded = 0;
        _ = BitVecOps.VisitBits(traits, samplepointBlocks, postorderNum => {
            InsertSamplepoint(dfsTree.GetPostOrder(postorderNum));
            samplepointsAdded++;
            return true;
        });

        JITDUMP($"Added {samplepointsAdded} Wasm profiler samplepoint(s)\n");
        return samplepointsAdded > 0
            ? PhaseStatus.MODIFIED_EVERYTHING
            : PhaseStatus.MODIFIED_NOTHING;
    }

    public PhaseStatus fgWasmTransformSccs()
    {
        assert(Configuration.TARGET_WASM);
        assert(fgNodeThreading == NodeThreading.LIR);

        var fgWasm = new FgWasm(this);
        var dfsTree = fgWasm.WasmDfs(out _);
        fgWasm.SetDfsAndTraits(dfsTree);

        var tryRegions = FlowGraphTryRegions.Build(this, dfsTree);
        if (Configuration.DEBUG && verbose)
        {
            FlowGraphTryRegions.Dump(tryRegions);
        }

        if (tryRegions.HasSideEntry())
        {
            NYI_WASM("Try region side entry survived fgWasmRepairTryEntries");
            FatalJitException.ThrowFatalJitException(
        CorJitResult.CORJIT_SKIPPED    );
            return default!;
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

            if (Configuration.DEBUG)
            {
                var dfsTree2 = fgWasm.WasmDfs(out var hasBlocksOnlyReachableViaEH2);
                assert(!hasBlocksOnlyReachableViaEH2);
                var loops2 = FlowGraphNaturalLoops.Find(dfsTree2);
                assert(loops2.ImproperLoopHeaders == 0);
            }
        }

        return transformed ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }
}
