// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if EMIT_GENERATE_GCINFO && !JIT32_GCENCODER
using System.Collections.Generic;
#endif

namespace RyuJitSharp;

public partial struct GCInfo
{
    private readonly CodeGen _codeGen;

    public unsafe GCInfo(CodeGen codeGen)
    {
        _codeGen = codeGen;
        gcVarPtrList = null;
        gcVarPtrLast = null;
        gcRegPtrList = null;
        gcRegPtrLast = null;
        gcPtrArgCnt = 0;
        gcCallDescList = null;
        gcCallDescLast = null;
#if EMIT_GENERATE_GCINFO
#if JIT32_GCENCODER
        gcEpilogTable = null;
#else
        _regSlotMap = null;
        _stackSlotMap = null;
#endif
#endif
    }

    public readonly Compiler Compiler => _codeGen.Compiler;

    public readonly ref RegSet RegSet => ref _codeGen.RegSet;

    internal varPtrDsc? gcVarPtrList;
    internal varPtrDsc? gcVarPtrLast;
    private regPtrDsc? gcRegPtrList;
    private regPtrDsc? gcRegPtrLast;
    private uint gcPtrArgCnt;
    private CallDsc? gcCallDescList;
    private CallDsc? gcCallDescLast;

#if EMIT_GENERATE_GCINFO
#if JIT32_GCENCODER
    private unsafe byte* gcEpilogTable;
#else
    private Dictionary<RegSlotIdKey, uint>? _regSlotMap;
    private Dictionary<StackSlotIdKey, uint>? _stackSlotMap;
#endif
#endif
}
