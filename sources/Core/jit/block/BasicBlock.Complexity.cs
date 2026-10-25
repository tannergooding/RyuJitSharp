// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class BasicBlock
{
    private struct ComplexityCounter(Func<GenTree, uint> getTreeComplexity) : IGenTreeComplexity<ComplexityCounter>
    {
        public uint TotalComplexity;

        public uint GetComplexity(GenTree tree)
        {
            var complexity = getTreeComplexity(tree);
            TotalComplexity = unchecked(TotalComplexity + complexity);
            return complexity;
        }
    }

    public bool StatementCountExceeds(uint limit) => StatementCountExceeds(limit, out _);

    public bool StatementCountExceeds(uint limit, out uint count)
    {
        var localCount = 0u;
        var overLimit = false;
        foreach (var statement in Statements)
        {
            localCount = unchecked(localCount + 1);
            if (localCount > limit)
            {
                overLimit = true;
                break;
            }
        }

        count = localCount;
        return overLimit;
    }

    public bool ComplexityExceeds(Compiler compiler, uint limit, Func<GenTree, uint> getTreeComplexity)
    {
        var complexity = new ComplexityCounter(getTreeComplexity);

        foreach (var statement in Statements)
        {
            var slack = unchecked(limit - complexity.TotalComplexity);
            if (compiler.gtComplexityExceeds(statement.RootNode, slack, ref complexity))
            {
                return true;
            }
        }

        return false;
    }
}
