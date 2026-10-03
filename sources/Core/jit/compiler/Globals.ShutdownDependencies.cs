// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if COUNT_AST_OPERS || MEASURE_NODE_SIZE || MEASURE_BLOCK_SIZE || MEASURE_MEM_ALLOC || EMITTER_STATS
#if EMITTER_STATS
using System.IO;
#endif

namespace RyuJitSharp;

public partial class Globals
{
#if COUNT_AST_OPERS
    internal static nuint TREE_NODE_SZ_SMALL => throw new FatalJitException(CORJIT_SKIPPED, "Native sizeof(GenTreeLclFld) is not ported.");
#endif

#if MEASURE_NODE_SIZE
    internal static ref NodeSizeStats genNodeSizeStats => throw new FatalJitException(CORJIT_SKIPPED, "genNodeSizeStats collection is not ported.");

    internal static ref NodeSizeStats genNodeSizeStatsPerFunc => throw new FatalJitException(CORJIT_SKIPPED, "genNodeSizeStatsPerFunc collection is not ported.");

    internal static Histogram genTreeNcntHist => throw new FatalJitException(CORJIT_SKIPPED, "genTreeNcntHist collection is not ported.");

    internal static Histogram genTreeNsizHist => throw new FatalJitException(CORJIT_SKIPPED, "genTreeNsizHist collection is not ported.");
#endif

#if MEASURE_BLOCK_SIZE
    internal static ref nuint genFlowNodeCnt => throw new FatalJitException(CORJIT_SKIPPED, "genFlowNodeCnt collection is not ported.");

    internal static ref nuint genFlowNodeSize => throw new FatalJitException(CORJIT_SKIPPED, "genFlowNodeSize collection is not ported.");

    internal static nuint NativeBasicBlockSize => throw new FatalJitException(CORJIT_SKIPPED, "Native sizeof(BasicBlock) is not ported.");

    internal static nuint NativeFlowEdgeSize => throw new FatalJitException(CORJIT_SKIPPED, "Native sizeof(FlowEdge) is not ported.");
#endif

#if MEASURE_MEM_ALLOC
    internal static readonly Histogram memAllocHist = new([64, 128, 192, 256, 512, 1024, 4096, 8192, 0]);
#endif

#if EMITTER_STATS
    internal static void emitterStats(StreamWriter output)
    {
        Emitter.emitterStats(output);
    }
#endif
}
#endif
