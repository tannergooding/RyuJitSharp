// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

internal struct LsraBlockInfo
{
    public uint predBBNum;
    public weight_t weight;
    public bool hasCriticalInEdge;
    public bool hasCriticalOutEdge;
    public bool hasEHBoundaryIn;
    public bool hasEHBoundaryOut;
    public bool hasEHPred;

#if TRACK_LSRA_STATS
    public uint[]? stats;
#endif
}
