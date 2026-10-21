// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

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
        if (!Configuration.LATE_DISASM)
        {
            return;
        }

        var compiler = _compiler;
        if (compiler is null)
        {
            FatalJitException.ThrowFatalJitException("Disassembler has not been initialized.");
            return;
        }
        if (!compiler.opts.doLateDisasm)
        {
            return;
        }

        if (Configuration.DISASM_DEBUG)
        {
            if (!Configuration.DEBUG || compiler.verbose)
            {
                jitprintf($"Relocation {relocAddr:X16} => {targetAddr:X16}\n");
            }
        }

        GetRelocationMap()[relocAddr] = targetAddr;
    }
}
