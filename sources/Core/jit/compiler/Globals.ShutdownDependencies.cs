// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.IO;

namespace RyuJitSharp;

public partial class Globals
{
    internal static nuint TREE_NODE_SZ_SMALL
    {
        get
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.NativeSizeofGenTreeLclFldIsNotPorted);
            return default!;
        }
    }

    internal static nuint TREE_NODE_SZ_LARGE
    {
        get
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.NativeSizeofGenTreeCallIsNotPorted);
            return default!;
        }
    }

    internal static ref NodeSizeStats genNodeSizeStats
    {
        get
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.GenNodeSizeStatsCollectionIsNotPorted);
            return ref System.Runtime.CompilerServices.Unsafe.NullRef<NodeSizeStats>();
        }
    }

    internal static ref NodeSizeStats genNodeSizeStatsPerFunc
    {
        get
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.GenNodeSizeStatsPerFuncCollectionIsNotPorted);
            return ref System.Runtime.CompilerServices.Unsafe.NullRef<NodeSizeStats>();
        }
    }

    internal static Histogram genTreeNcntHist
    {
        get
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.GenTreeNcntHistCollectionIsNotPorted);
            return default!;
        }
    }

    internal static Histogram genTreeNsizHist
    {
        get
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.GenTreeNsizHistCollectionIsNotPorted);
            return default!;
        }
    }

    internal static nuint NativeBasicBlockSize
    {
        get
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.NativeSizeofBasicBlockIsNotPorted);
            return default!;
        }
    }

    internal static nuint NativeFlowEdgeSize
    {
        get
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.NativeSizeofFlowEdgeIsNotPorted);
            return default!;
        }
    }

    internal static Histogram memAllocHist => Compiler.MeasurementData.ManagedAllocation.memAllocHist;

    internal static void emitterStats(StreamWriter output)
    {
        Emitter.emitterStats(output);
    }
}
