// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public sealed partial class BasicBlock
{
    public BasicBlock? GetUniquePred(Compiler compiler)
    {
        assert(compiler.fgPredsComputed);

        // A backedge is never the entry block's unique predecessor: the prolog also enters it.
        if ((bbPreds is null) || (bbPreds.NextPredEdge is not null) || (this == compiler.fgFirstBB))
        {
            return null;
        }

        return bbPreds.SourceBlock;
    }
}
