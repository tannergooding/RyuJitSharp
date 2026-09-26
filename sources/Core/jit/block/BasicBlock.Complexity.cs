// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class BasicBlock
{
    public bool ComplexityExceeds(Compiler compiler, uint limit, Func<GenTree, uint> getTreeComplexity)
    {
        var localCount = 0u;
        uint GetComplexity(GenTree tree)
        {
            var complexity = getTreeComplexity(tree);
            localCount = unchecked(localCount + complexity);
            return complexity;
        }

        foreach (var statement in Statements)
        {
            var slack = unchecked(limit - localCount);
            if (compiler.gtComplexityExceeds(statement.RootNode, slack, GetComplexity))
            {
                return true;
            }
        }

        return false;
    }
}
