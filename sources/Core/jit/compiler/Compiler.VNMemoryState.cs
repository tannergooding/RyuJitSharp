// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public void fgMutateGcHeap(GenTree tree, string message)
    {
        assert(vnStore is not null);
        recordGcHeapStore(tree, vnStore.VNForExpr(compCurBB, TYP_HEAP), message);
    }

    public void fgMutateAddressExposedLocal(GenTree tree, string message)
    {
        assert(vnStore is not null);
        recordAddressExposedLocalStore(tree, vnStore.VNForExpr(compCurBB, TYP_HEAP), message);
    }

    public void recordGcHeapStore(GenTree tree, ValueNum gcHeapVN, string message)
    {
        assert(compCurBB is not null);
        assert(vnStore is not null);
        const MemoryKindSet memoryKinds = (1 << (int)GcHeap) | (1 << (int)ByrefExposed);
        assert((compCurBB.bbMemoryDef & memoryKinds) == memoryKinds);
        fgSetCurrentMemoryVN(GcHeap, gcHeapVN);

        if (byrefStatesMatchGcHeapStates)
        {
            // Shared SSA definitions must also share value numbers.
            fgSetCurrentMemoryVN(ByrefExposed, gcHeapVN);
        }
        else
        {
            // A heap store can alias any byref access, so discard its precise map.
            fgSetCurrentMemoryVN(ByrefExposed, vnStore.VNForExpr(compCurBB, TYP_HEAP));
        }

#if DEBUG
        if (verbose)
        {
            jitprintf($"  fgCurMemoryVN[GcHeap] assigned for {message} at [{tree.TreeId:D6}] to VN: ${gcHeapVN:x}.\n");
        }
#endif

        // Shared memory states also share their node-to-SSA map entries.
        fgValueNumberRecordMemorySsa(GcHeap, tree);
    }

    public void recordAddressExposedLocalStore(GenTree tree, ValueNum memoryVN, string message)
    {
        assert(!byrefStatesMatchGcHeapStates);
        assert(compCurBB is not null);
        assert((compCurBB.bbMemoryDef & (1 << (int)ByrefExposed)) != 0);
        fgSetCurrentMemoryVN(ByrefExposed, memoryVN);

#if DEBUG
        if (verbose)
        {
            jitprintf($"  fgCurMemoryVN[ByrefExposed] assigned for {message} at [{tree.TreeId:D6}] to VN: ${memoryVN:x}.\n");
        }
#endif

        fgValueNumberRecordMemorySsa(ByrefExposed, tree);
    }

    public void fgSetCurrentMemoryVN(MemoryKind memoryKind, ValueNum newMemoryVN)
    {
        assert(vnStore is not null);
        assert(vnStore.VNIsValid(newMemoryVN));
        assert(vnStore.TypeOfVN(newMemoryVN) == TYP_HEAP);
        fgCurMemoryVN[(int)memoryKind] = newMemoryVN;
    }

    public void fgValueNumberRecordMemorySsa(MemoryKind memoryKind, GenTree tree)
    {
        if (GetMemorySsaMap(memoryKind).TryGetValue(tree, out var ssaNum))
        {
            GetMemoryPerSsaData(ssaNum)._vnPair.Liberal = fgCurMemoryVN[(int)memoryKind];
#if DEBUG
            if (verbose)
            {
                assert(vnStore is not null);
                jitprintf($"Node [{tree.TreeId:D6}] sets {memoryKind} SSA # {ssaNum} to VN ${fgCurMemoryVN[(int)memoryKind]:x}: ");
                vnStore.vnDump(this, fgCurMemoryVN[(int)memoryKind]);
                jitprintf("\n");
            }
#endif
        }
    }
}
