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
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Native sizeof(GenTreeLclFld) is not ported.");
            return default!;
        }
    }

    internal static nuint TREE_NODE_SZ_LARGE
    {
        get
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Native sizeof(GenTreeCall) is not ported.");
            return default!;
        }
    }

    internal static ref NodeSizeStats genNodeSizeStats
    {
        get
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "genNodeSizeStats collection is not ported.");
            return ref System.Runtime.CompilerServices.Unsafe.NullRef<NodeSizeStats>();
        }
    }

    internal static ref NodeSizeStats genNodeSizeStatsPerFunc
    {
        get
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "genNodeSizeStatsPerFunc collection is not ported.");
            return ref System.Runtime.CompilerServices.Unsafe.NullRef<NodeSizeStats>();
        }
    }

    internal static Histogram genTreeNcntHist
    {
        get
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "genTreeNcntHist collection is not ported.");
            return default!;
        }
    }

    internal static Histogram genTreeNsizHist
    {
        get
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "genTreeNsizHist collection is not ported.");
            return default!;
        }
    }

    private static nuint s_genFlowNodeCnt;

    private static nuint s_genFlowNodeSize;

    internal static ref nuint genFlowNodeCnt => ref s_genFlowNodeCnt;

    internal static ref nuint genFlowNodeSize => ref s_genFlowNodeSize;

    internal static nuint NativeBasicBlockSize
    {
        get
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Native sizeof(BasicBlock) is not ported.");
            return default!;
        }
    }

    internal static nuint NativeFlowEdgeSize
    {
        get
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Native sizeof(FlowEdge) is not ported.");
            return default!;
        }
    }

    private static class MemAllocHistogram
    {
        internal static readonly Histogram Instance = new([64, 128, 192, 256, 512, 1024, 4096, 8192, 0]);
    }

    internal static Histogram memAllocHist => MemAllocHistogram.Instance;

    internal static void emitterStats(StreamWriter output)
    {
        Emitter.emitterStats(output);
    }
}
