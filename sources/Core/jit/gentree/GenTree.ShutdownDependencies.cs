// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if NODEBASH_STATS || MEASURE_NODE_SIZE || COUNT_AST_OPERS
#if NODEBASH_STATS
using System.IO;
#endif

namespace RyuJitSharp;

public partial class GenTree
{
#if MEASURE_NODE_SIZE
    internal static void DumpNodeSizes()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "GenTree::DumpNodeSizes is not ported.");
    }
#endif

#if NODEBASH_STATS
    internal static void ReportOperBashing(StreamWriter output)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "GenTree::ReportOperBashing is not ported.");
    }
#endif

#if COUNT_AST_OPERS
    // Native s_gtNodeCounts storage must be shared by the constructor and report.
    internal static ref uint GetNodeCount(uint op)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "GenTree::s_gtNodeCounts storage is not ported.");
    }
#endif

#if NODEBASH_STATS || MEASURE_NODE_SIZE || COUNT_AST_OPERS
    internal static byte GetTrueSize(uint op)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "GenTree::s_gtTrueSizes native logical sizes are not ported.");
    }
#endif
}
#endif
