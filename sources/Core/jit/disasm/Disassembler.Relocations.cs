// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if LATE_DISASM
using System.Collections.Generic;

namespace RyuJitSharp;

public partial struct Disassembler
{
    private Dictionary<nuint, nuint> GetRelocationMap()
    {
        _relocationMap ??= [];

        return _relocationMap;
    }

    public void disRecordRelocation(nuint relocAddr, nuint targetAddr)
    {
        assert(_compiler is not null);
        if (!_compiler.opts.doLateDisasm)
        {
            return;
        }

#if DISASM_DEBUG
#if DEBUG
        if (_compiler.verbose)
#endif
        {
            jitprintf($"Relocation {relocAddr:X16} => {targetAddr:X16}\n");
        }
#endif
        GetRelocationMap()[relocAddr] = targetAddr;
    }
}
#endif
