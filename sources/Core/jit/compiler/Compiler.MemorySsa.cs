// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public int AllocMemorySsaNum()
    {
        var ssaNum = lvMemoryPerSsaData.AllocSsaNum();
        GetMemoryPerSsaData(ssaNum) = new SsaMemDef();
        return ssaNum;
    }

    public ref SsaMemDef GetMemoryPerSsaData(int ssaNum) => ref lvMemoryPerSsaData.GetSsaDef(ssaNum);

    public NodeToUnsignedMap GetMemorySsaMap(MemoryKind memoryKind)
    {
        if ((memoryKind is MemoryKind.GcHeap) && byrefStatesMatchGcHeapStates)
        {
            memoryKind = MemoryKind.ByrefExposed;
        }

        assert(memoryKind < MemoryKind.MemoryKindCount);
        var root = impInlineRoot;

        return root._memorySsaMap[(int)memoryKind] ??= [];
    }
}
