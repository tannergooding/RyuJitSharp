// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class SsaBuilder
{
    public void Build()
    {
        JITDUMP("*************** In SsaBuilder::Build()\n");
        _compiler.fgSsaLiveness();
        _compiler.EndPhase(PHASE_BUILD_SSA_LIVENESS);
        _compiler.optRemoveRedundantZeroInits();
        _compiler.EndPhase(PHASE_ZERO_INITS);

        for (var localNum = 0; localNum < _compiler.lvaCount; localNum++)
        {
            _compiler.lvaTable[localNum].lvInSsa = _compiler.lvaGetDesc(localNum).lvTracked;
        }

        InsertPhiFunctions();
        RenameVariables();
        _compiler.EndPhase(PHASE_BUILD_SSA_RENAME);

#if DEBUG
        if (_compiler.verbose)
        {
            _compiler.DumpSsaSummary();
        }
#endif
    }
}
