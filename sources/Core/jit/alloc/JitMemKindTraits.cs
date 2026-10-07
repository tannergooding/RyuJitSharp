// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics.CodeAnalysis;
#if DEBUG
using System.Runtime.InteropServices;
#endif

namespace RyuJitSharp;

internal static unsafe class JitMemKindTraits
{
    internal const int Count = (int)CompMemKind.CMK_Count;

    private static readonly string[] s_names =
    [
        "ABI",
        "AssertionProp",
        "ASTNode",
        "InstDesc",
        "ImpStack",
        "BasicBlock",
        "CallArgs",
        "FlowEdge",
        "DepthFirstSearch",
        "Loops",
        "TreeStatementList",
        "SiScope",
        "DominatorMemory",
        "Lower",
        "LSRA",
        "LSRA_Interval",
        "LSRA_RefPosition",
        "Reachability",
        "RedundantBranch",
        "SSA",
        "ValueNumber",
        "LvaTable",
        "UnwindInfo",
        "hashBv",
        "bitset",
        "FixedBitVect",
        "Generic",
        "LocalAddressVisitor",
        "FieldSeqStore",
        "MemorySsaMap",
        "MemoryPhiArg",
        "CSE",
        "GC",
        "CorTailCallInfo",
        "Inlining",
        "ArrayStack",
        "DebugInfo",
        "DebugOnly",
        "Codegen",
        "LoopOpt",
        "LoopClone",
        "LoopUnroll",
        "LoopHoist",
        "LoopIVOpts",
        "Unknown",
        "RangeCheck",
        "CopyProp",
        "Promotion",
        "SideEffects",
        "ObjectAllocator",
        "VariableLiveRanges",
        "ClassLayout",
        "EarlyProp",
        "ZeroInit",
        "Pgo",
        "MaskConversionOpt",
        "TryRegionClone",
        "Async",
        "RangeCheckCloning",
        "WasmSccTransform",
        "WasmCfgLowering",
        "WasmEH",
        "WasmSpillRefs",
    ];

    internal static ReadOnlySpan<string> Names => s_names;

    internal static bool bypassHostAllocator()
    {
#if DEBUG
        // Direct allocations bypass host slab pooling so pageheap can diagnose overruns.
        return Globals.JitConfig.JitDirectAlloc != 0;
#else
        return false;
#endif
    }

    internal static bool shouldInjectFault()
    {
#if DEBUG
        return Globals.JitConfig.ShouldInjectFault != 0;
#else
        return false;
#endif
    }

    internal static void* allocateHostMemory(nuint size, nuint* pActualSize)
    {
#if DEBUG
        if (bypassHostAllocator())
        {
            // Account for the request, not the one-byte backing allocation for a zero-size slab.
            *pActualSize = size;
            if (size == 0)
            {
                size = 1;
            }

            try
            {
                var result = NativeMemory.Alloc(size);
                if (result == null)
                {
                    outOfMemory();
                }

                return result;
            }
            catch (OutOfMemoryException)
            {
                outOfMemory();
                throw;
            }
        }
#endif

        Globals.assert(CILJit.s_jitHost != null);
        // The existing host ABI uses nint for size_t; preserve all unsigned size bits.
        return CILJit.s_jitHost->allocateSlab(unchecked((nint)size), (nint*)pActualSize);
    }

    internal static void freeHostMemory(void* block, nuint size)
    {
#if DEBUG
        if (bypassHostAllocator())
        {
            NativeMemory.Free(block);
            return;
        }
#endif

        Globals.assert(CILJit.s_jitHost != null);
        CILJit.s_jitHost->freeSlab(block, unchecked((nint)size));
    }

    internal static void fillWithUninitializedPattern(void* block, nuint size)
    {
#if DEBUG
        NativeMemory.Fill(block, size, 0xCD);
#endif
    }

    [DoesNotReturn]
    internal static void outOfMemory()
    {
        Globals.NOMEM();
    }
}
