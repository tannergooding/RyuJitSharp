// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
#if CALL_ARG_STATS
    internal static void compDispCallArgStats(System.IO.StreamWriter output)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Call argument statistics are not implemented.");
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
