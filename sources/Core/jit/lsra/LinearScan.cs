// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.IO;

namespace RyuJitSharp;

public sealed class LinearScan : IRegAlloc
{
    private Compiler _compiler;

    public LinearScan(Compiler compiler)
    {
        // TODO: Port LinearScan.ctor
        _compiler = compiler;
    }

    // TODO: Port LinearScan.DoRegisterAllocation
    public PhaseStatus DoRegisterAllocation()
    {
        var codeGen = _compiler.codeGen;

        assert(codeGen is not null);
        codeGen.IsFramePointerUsed = false;

        return PhaseStatus.MODIFIED_NOTHING;
    }

#if TRACK_LSRA_STATS
    public void dumpLsraStatsCsv(StreamWriter streamWriter)
    {
        // TODO: Port LinearScan.dumpLsraStatsCsv
    }
#endif
}
