// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Frozen;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static RyuJitSharp.JitConfigValues.ConfigMethodSet;

namespace RyuJitSharp;

public partial struct JitConfigValues
{
    public enum ConfigMethodSet
    {
#if DEBUG
        // <summary>Forces MinOpts for a named function</summary>
        JitMinOptsName, 

        // <summary>Prints a tree of inlinees for a specific method (use '*' for all methods)</summary>
        JitPrintInlinedMethods, 

        // <summary></summary>
        JitPrintDevirtualizedMethods, 

        // <summary>Stops in the importer when compiling a specified method</summary>
        JitBreak, 

        // <summary></summary>
        JitDebugBreak, 

        // <summary>Dumps trees for specified method</summary>
        JitDump, 

        // <summary>Dump the EH table for the method, as reported to the VM</summary>
        JitEHDump, 

        // <summary></summary>
        JitExclude, 

        // <summary></summary>
        JitForceProcedureSplitting, 

        // <summary></summary>
        JitGCDump,

        // <summary></summary>
        JitDebugDump,

        // <summary>Emits break instruction into jitted code</summary>
        JitHalt, 

        // <summary></summary>
        JitInclude,

        // <summary>Generate late disassembly for the specified methods.</summary>
        JitLateDisasm, 

        // <summary>Disallow procedure splitting for specified methods</summary>
        JitNoProcedureSplitting, 

        // <summary>Disallow procedure splitting for specified methods if they contain exception handling</summary>
        JitNoProcedureSplittingEH, 

        // <summary>Internal Jit stress mode: stress only the specified method(s)</summary>
        JitStressOnly, 

        // <summary>Dump the unwind codes for the method</summary>
        JitUnwindDump, 

        // <summary>Dumps Xml/Dot Flowgraph for specified method</summary>
        JitDumpFg,            

        // <summary>Generate emitter unit tests in the specified functions</summary>
        JitEmitUnitTests,
#endif

        // <summary>Print codegen for given methods</summary>
        JitDisasm,                  

        // <summary>Runs optimizer multiple times on specified methods</summary>
        JitOptRepeat,            

        // <summary>Enables AltJit and selectively limits it to the specified methods.</summary>
        AltJit,         

        // <summary>Enables AltJit for AOT and selectively limits it to the specified methods.</summary>
        AltJitNgen,

#if DEBUG
        // <summary></summary>
        JitRawHexCode,
#endif
    }

    private static readonly unsafe FrozenDictionary<ConfigMethodSet, nuint> ConfigMethodSetMetadata = new Dictionary<ConfigMethodSet, nuint> {
#if DEBUG
        [JitMinOptsName] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JITMinOptsName"u8)),
        [JitPrintInlinedMethods] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitPrintInlinedMethods"u8)),
        [JitPrintDevirtualizedMethods] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitPrintDevirtualizedMethods"u8)),
        [JitBreak] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitBreak"u8)),
        [JitDebugBreak] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDebugBreak"u8)),
        [JitDump] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDump"u8)),
        [JitEHDump] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEHDump"u8)),
        [JitExclude] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitExclude"u8)),
        [JitForceProcedureSplitting] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitForceProcedureSplitting"u8)),
        [JitGCDump] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitGCDump"u8)),
        [JitDebugDump] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDebugDump"u8)),
        [JitHalt] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitHalt"u8)),
        [JitInclude] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInclude"u8)),
        [JitLateDisasm] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitLateDisasm"u8)),
        [JitNoProcedureSplitting] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoProcedureSplitting"u8)),
        [JitNoProcedureSplittingEH] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoProcedureSplittingEH"u8)),
        [JitStressOnly] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressOnly"u8)),
        [JitUnwindDump] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitUnwindDump"u8)),
        [JitDumpFg] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFg"u8)),
        [JitEmitUnitTests] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEmitUnitTests"u8)),
#endif

        [JitDisasm] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasm"u8)),
        [JitOptRepeat] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitOptRepeat"u8)),
        [AltJit] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("AltJit"u8)),
        [AltJitNgen] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("AltJitNgen"u8)),

#if DEBUG
        [JitRawHexCode] = (nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRawHexCode"u8)),
#endif
    }.ToFrozenDictionary();
}
