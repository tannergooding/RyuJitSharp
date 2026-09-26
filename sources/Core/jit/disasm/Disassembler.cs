// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if LATE_DISASM
using System.Collections.Generic;
using System.IO;

namespace RyuJitSharp;

public partial struct Disassembler
{
    private Compiler? _compiler;
    private bool _hasName;
    private byte[]? _labels;
    private Dictionary<nuint, Pointer<CORINFO_METHOD_STRUCT_>>? _addressToMethodHandleMap;
    private Dictionary<nuint, Pointer<CORINFO_METHOD_STRUCT_>>? _helperAddressToMethodHandleMap;
    private Dictionary<nuint, nuint>? _relocationMap;
    private bool _diffable;
    private StreamWriter? _disAsmFile;

    public void disInit(Compiler compiler)
    {
        assert(compiler is not null);
        _compiler = compiler;
        _hasName = false;
        _labels = null;
        _addressToMethodHandleMap = null;
        _helperAddressToMethodHandleMap = null;
        _relocationMap = null;
        _diffable = false;
        _disAsmFile = null;
    }

    public readonly void disDone()
    {
    }

    public readonly unsafe void disOpenForLateDisAsm(string curMethodName, string curClassName, PCCOR_SIGNATURE sig)
    {
        var compiler = _compiler ?? throw new FatalJitException("Disassembler has not been initialized.");
        if (compiler.opts.doLateDisasm)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "Late disassembly requires the native CoreDisTools callback ABI.");
        }
    }

    public readonly unsafe void disAsmCode(byte* hotCodePtr, byte* hotCodePtrRW, uint hotCodeSize,
        byte* coldCodePtr, byte* coldCodePtrRW, uint coldCodeSize)
    {
        var compiler = _compiler ?? throw new FatalJitException("Disassembler has not been initialized.");
        if (compiler.opts.doLateDisasm)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "Late disassembly requires the native CoreDisTools callback ABI.");
        }
    }
}
#endif
