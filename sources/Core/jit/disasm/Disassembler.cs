// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if LATE_DISASM
using System.Collections.Generic;
using System.Globalization;
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
    private nuint _totalCodeSize;
    private nuint _startAddr;
#if USE_COREDISTOOLS
    private nuint _corDisasm;
#endif

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

#if USE_COREDISTOOLS
        _corDisasm = 0;
#endif
    }

    public void disDone()
    {
#if USE_COREDISTOOLS
        DoneCoredistoolsDisasm();
#endif
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

    public unsafe void disAsmCode(byte* hotCodePtr, byte* hotCodePtrRW, nuint hotCodeSize,
        byte* coldCodePtr, byte* coldCodePtrRW, nuint coldCodeSize)
    {
        var compiler = _compiler ?? throw new FatalJitException("Disassembler has not been initialized.");
        if (!compiler.opts.doLateDisasm)
        {
            return;
        }

#if USE_COREDISTOOLS
        _ = InitCoredistoolsDisasm();
#endif

#if DEBUG
        _diffable = compiler.opts.dspDiffable;
#else
        _diffable = true;
#endif

#if DEBUG
        var fileName = JitConfig.JitLateDisasmTo;
        if (fileName is not null)
        {
            _disAsmFile = OpenLateDisassemblyFile(fileName);
        }
#else
        _disAsmFile = jitstdout();
#endif

        var output = _disAsmFile ??= jitstdout();

        // Like the native common output file, this is not reentrant.
        assert(hotCodeSize > 0);
        if (coldCodeSize == 0)
        {
            output.Write($"************************** {_curClassName}:{_curMethodName} " +
                $"size 0x{hotCodeSize:X4} **************************\n\n");
            output.Write($"Base address : {FormatAddress(hotCodePtr)}h (RW: {FormatAddress(hotCodePtrRW)}h)\n");
        }
        else
        {
            output.Write($"************************** {_curClassName}:{_curMethodName} " +
                $"hot size 0x{hotCodeSize:X4} cold size 0x{coldCodeSize:X4} **************************\n\n");
            output.Write($"Hot  address : {FormatAddress(hotCodePtr)}h (RW: {FormatAddress(hotCodePtrRW)}h)\n");
            output.Write($"Cold address : {FormatAddress(coldCodePtr)}h (RW: {FormatAddress(coldCodePtrRW)}h)\n");
        }

        _startAddr = 0;
        _hotCodeBlock = (nuint)hotCodePtrRW;
        _hotCodeSize = hotCodeSize;
        _coldCodeBlock = (nuint)coldCodePtrRW;
        _coldCodeSize = coldCodeSize;
        _totalCodeSize = unchecked(_hotCodeSize + _coldCodeSize);
        _labels = new byte[_totalCodeSize];

        DisasmBuffer(output, printit: true);
        output.Write("\n");

        if (output != jitstdout())
        {
            output.Dispose();
        }
        else
        {
            output.Flush();
        }
    }

    private readonly unsafe string FormatAddress(void* address)
    {
        return ((nuint)dspAddr(address)).ToString($"X{sizeof(nuint) * 2}", CultureInfo.InvariantCulture);
    }
}
#endif
