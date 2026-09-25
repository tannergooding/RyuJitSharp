// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if LATE_DISASM
using System.Collections.Generic;

namespace RyuJitSharp;

public partial struct Disassembler
{
    private Dictionary<nuint, Pointer<CORINFO_METHOD_STRUCT_>> GetAddrToMethodHandleMap()
    {
        _addressToMethodHandleMap ??= [];
        return _addressToMethodHandleMap;
    }

    private Dictionary<nuint, Pointer<CORINFO_METHOD_STRUCT_>> GetHelperAddrToMethodHandleMap()
    {
        _helperAddressToMethodHandleMap ??= [];
        return _helperAddressToMethodHandleMap;
    }

    public unsafe void disSetMethod(nuint addr, CORINFO_METHOD_HANDLE methHnd)
    {
        assert(_compiler is not null);
        if (!_compiler.opts.doLateDisasm)
        {
            return;
        }

        if (Compiler.eeGetHelperNum(methHnd) != CORINFO_HELP_UNDEF)
        {
#if DISASM_DEBUG
#if DEBUG
            if (_compiler.verbose)
#endif
            {
                jitprintf($"Helper function: {addr:X16} => {(nuint)methHnd:X16}\n");
            }
#endif
            GetHelperAddrToMethodHandleMap()[addr] = methHnd;
        }
        else
        {
#if DISASM_DEBUG
#if DEBUG
            if (_compiler.verbose)
#endif
            {
                jitprintf($"Function: {addr:X16} => {(nuint)methHnd:X16}\n");
            }
#endif
            GetAddrToMethodHandleMap()[addr] = methHnd;
        }
    }
}
#endif
