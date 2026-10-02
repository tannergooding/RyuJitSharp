// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if CALL_ARG_STATS || COUNT_AST_OPERS
#if CALL_ARG_STATS
using System.IO;
#endif

namespace RyuJitSharp;

public partial class Compiler
{
#if CALL_ARG_STATS
    internal static void compDispCallArgStats(StreamWriter output)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Compiler::compDispCallArgStats is not ported.");
    }
#endif

#if COUNT_AST_OPERS
    internal readonly record struct OperInfo(uint Count, uint Size, genTreeOps Oper);

    internal readonly struct OperInfoLess : INativeLess<OperInfo>
    {
        public bool Less(OperInfo first, OperInfo second)
        {
            // Preserve the native non-strict predicate, including equal counts.
            return first.Count >= second.Count;
        }
    }
#endif
}
#endif
