// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public void optRecordLoopMemoryDependence(GenTree tree, BasicBlock block, ValueNum memoryVN)
    {
        var store = vnStore
            ?? throw new FatalJitException("Loop memory dependence requires a value number store.");
        var updateLoop = store.LoopOfVN(memoryVN);
        if (updateLoop is null)
        {
#if DEBUG
            JITDUMP($"      ==> Not updating loop memory dependence of [{tree.TreeId:D6}], memory {memoryVN} not defined in a loop\n");
#endif
            return;
        }

        var definingLoop = updateLoop;
        while ((updateLoop is not null) && !updateLoop.ContainsBlock(block))
        {
            updateLoop = updateLoop.Parent;
        }

        if (updateLoop is null)
        {
#if DEBUG
            assert(_blockToLoop is not null);
            var blockLoop = _blockToLoop.GetLoop(block);
            assert(blockLoop is not null);
            JITDUMP($"      ==> Not updating loop memory dependence of [{tree.TreeId:D6}]/L{blockLoop.Index:D2}, " +
                $"memory definition {memoryVN}/L{definingLoop.Index:D2} is not dependent on an ancestor loop\n");
#endif
            return;
        }

        var map = NodeToLoopMemoryBlockMap;
        if (map.TryGetValue(tree, out var mapBlock) && updateLoop.ContainsBlock(mapBlock))
        {
#if DEBUG
            assert(_blockToLoop is not null);
            var mapLoop = _blockToLoop.GetLoop(mapBlock);
            assert(mapLoop is not null);
            JITDUMP($"      ==> Not updating loop memory dependence of [{tree.TreeId:D6}]; " +
                $"already constrained to L{mapLoop.Index:D2} nested in L{updateLoop.Index:D2}\n");
#endif
            return;
        }

#if DEBUG
        JITDUMP($"      ==> Updating loop memory dependence of [{tree.TreeId:D6}] to L{updateLoop.Index:D2}\n");
#endif
        map[tree] = updateLoop.Header;
    }
}
