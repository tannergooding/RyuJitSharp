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

#if USE_COREDISTOOLS
    private static StreamWriter? s_disAsmFileCorDisTools;
    private nint _corDisasm;
#endif

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
        s_disAsmFileCorDisTools = null;
        _corDisasm = 0;
#endif
    }

    public readonly void disDone()
    {
        // TODO: Port Disassembler.disDone
    }

    public readonly unsafe void disOpenForLateDisAsm(string curMethodName, string curClassName, PCCOR_SIGNATURE sig)
    {
        // TODO: Port Disassembler.disOpenForLateDisAsm
    }
}
#endif
