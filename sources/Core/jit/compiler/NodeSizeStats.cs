// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if MEASURE_NODE_SIZE
namespace RyuJitSharp;

public struct NodeSizeStats
{
    public ulong genTreeNodeCnt;
    public ulong genTreeNodeSize;
    public ulong genTreeNodeActualSize;

    public void Init()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "NodeSizeStats::Init is not ported.");
    }
}
#endif
