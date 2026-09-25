// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.IO;

namespace RyuJitSharp;

public interface IRegAlloc
{
    PhaseStatus DoRegisterAllocation();

    bool IsContainableMemoryOp(GenTree node);

    bool IsRegCandidate(in LclVarDsc varDsc);

    bool WillEnregisterLocalVars();

    void recordVarLocationsAtStartOfBB(BasicBlock block);

#if TRACK_LSRA_STATS
    void dumpLsraStatsCsv(StreamWriter streamWriter);

    void dumpLsraStatsSummary(StreamWriter streamWriter);
#endif
}
