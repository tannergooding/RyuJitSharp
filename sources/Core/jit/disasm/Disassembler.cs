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
    private string? _curMethodName;
    private string? _curClassName;
    private nuint _hotCodeBlock;
    private nuint _coldCodeBlock;
    private nuint _hotCodeSize;
    private nuint _coldCodeSize;

    private readonly nuint dspAddr(nuint addr)
    {
        return (addr == 0) ? 0 : (_diffable ? (nuint)0xD1FFAB1E : addr);
    }

    private readonly unsafe void* dspAddr(void* addr)
    {
        return (addr == null) ? null : (_diffable ? (void*)(nuint)0xD1FFAB1E : addr);
    }

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

    private readonly unsafe byte* disGetLinearAddr(nuint offset)
    {
        if (offset < _hotCodeSize)
        {
            return (byte*)unchecked(_hotCodeBlock + offset);
        }

        return (byte*)unchecked(_coldCodeBlock + offset - _hotCodeSize);
    }

    private readonly nuint disGetBufferSize(nuint offset)
    {
        if (offset < _hotCodeSize)
        {
            return _hotCodeSize - offset;
        }

        return unchecked(_hotCodeSize + _coldCodeSize - offset);
    }

    public unsafe void disOpenForLateDisAsm(string curMethodName, string curClassName, PCCOR_SIGNATURE sig)
    {
        var compiler = _compiler ?? throw new FatalJitException("Disassembler has not been initialized.");
        if (!compiler.opts.doLateDisasm)
        {
            return;
        }

        _curMethodName = curMethodName;
        _curClassName = curClassName;
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
