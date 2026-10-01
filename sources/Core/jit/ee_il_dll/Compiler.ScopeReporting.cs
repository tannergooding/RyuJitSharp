// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Compiler
{
    private uint eeVarsCapacity;

    public unsafe void eeAllocateLVs(uint count)
    {
        assert(opts.compScopeInfo);
#if DEBUG
        if (verbose)
        {
            jitprintf($"Allocating {unchecked((int)count)} VarLocInfo\n");
        }
#endif
        eeVarsCount = 0;
        eeVarsCapacity = count;
        eeVars = count > 0
            ? (ICorDebugInfo.NativeVarInfo*)info.compCompHnd->allocateArray(
                unchecked((nint)((nuint)count * (nuint)sizeof(ICorDebugInfo.NativeVarInfo))))
            : null;
    }

    public unsafe void eeSetLVinfo(uint which, uint startOffs, uint endOffs,
        uint callReturnValueILOffset, int varNum, in CodeGen.siVarLoc varLoc)
    {
        assert(opts.compScopeInfo);
        assert(which < eeVarsCapacity);

        if (eeVars != null)
        {
            var location = varLoc;
            eeVars[which].startOffset = startOffs;
            eeVars[which].endOffset = endOffs;
            eeVars[which].callReturnValueILOffset = callReturnValueILOffset;
            eeVars[which].varNumber = unchecked((uint)varNum);
            eeVars[which].loc = Unsafe.As<CodeGen.siVarLoc, ICorDebugInfo.VarLoc>(ref location);
        }
    }

    public unsafe void eeSetLVdone()
    {
        assert(opts.compScopeInfo);
#if DEBUG
        if (verbose || opts.dspDebugInfo)
        {
            eeDispVars(info.compMethodHnd, eeVarsCount, eeVars);
        }
#endif
        if ((eeVarsCount == 0) && (eeVars != null))
        {
            // The EE still receives a null table for zero records; release the
            // unused allocation before transferring ownership.
            info.compCompHnd->freeArray(eeVars);
            eeVars = null;
        }

        info.compCompHnd->setVars(info.compMethodHnd, eeVarsCount, eeVars);
        eeVars = null; // We give up ownership after setVars().
    }

#if DEBUG
    public unsafe void eeDispVar(ICorDebugInfo.NativeVarInfo* variable)
    {
        var name = variable->varNumber switch
        {
            unchecked((uint)ICorDebugInfo.VARARGS_HND_ILNUM) => "varargsHandle",
            unchecked((uint)ICorDebugInfo.RETBUF_ILNUM) => "retBuff",
            unchecked((uint)ICorDebugInfo.TYPECTXT_ILNUM) => "typeCtx",
            _ => null,
        };

        if (variable->varNumber == unchecked((uint)ICorDebugInfo.CALL_RETURN_ILNUM))
        {
            jitprintf($"(call {variable->callReturnValueILOffset:D3})");
        }
        else if (variable->varNumber < (uint)lvaCount)
        {
            jitprintf("(");
            gtDispLclVar(unchecked((int)variable->varNumber), false);
            jitprintf(")");
        }
        else
        {
            jitprintf($"({name ?? "UNKNOWN",8})");
        }

        jitprintf($" : From {variable->startOffset:X8}h to {variable->endOffset:X8}h, in ");
        var location = Unsafe.As<ICorDebugInfo.VarLoc, CodeGen.siVarLoc>(ref variable->loc);
        assert(codeGen is not null);
        codeGen.dumpSiVarLoc(in location);
        jitprintf("\n");
    }

    public unsafe void eeDispVars(CORINFO_METHOD_HANDLE ftn, int count, ICorDebugInfo.NativeVarInfo* variables)
    {
        var uniqueVars = AllVarSetOps.MakeEmpty(this);
        for (var index = 0; index < count; index++)
        {
            var varNum = unchecked((int)variables[index].varNumber);
            if ((varNum >= 0) && (varNum < lclMAX_ALLSET_TRACKED))
            {
                AllVarSetOps.AddElemD(this, uniqueVars, varNum);
            }
        }

        jitprintf($"; Variable debug info: {count} live ranges, {AllVarSetOps.Count(this, uniqueVars)} vars for method {info.compFullName}\n");
        for (var index = 0; index < count; index++)
        {
            eeDispVar(&variables[index]);
        }
    }
#endif
}
