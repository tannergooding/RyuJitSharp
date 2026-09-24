// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class IndirectCallTransformer
{
    private readonly Compiler _compiler;

    public IndirectCallTransformer(Compiler compiler)
    {
        _compiler = compiler;
    }

    public int Run()
    {
        var count = 0;
        foreach (var block in _compiler.Blocks)
        {
            count += TransformBlock(block);
        }

        return count;
    }

    private int TransformBlock(BasicBlock block)
    {
        var count = 0;
        foreach (var stmt in block.Statements)
        {
            if (_compiler.MethodHasFatPointer && ContainsFatCalli(stmt))
            {
                var transformer = new FatPointerCallTransformer(_compiler, block, stmt);
                transformer.Run();
                count++;
            }
            else if (_compiler.MethodHasGuardedDevirtualization && ContainsGuardedDevirtualizationCandidate(stmt))
            {
                var transformer = new GuardedDevirtualizationTransformer(_compiler, block, stmt);
                transformer.Run();
                count++;
            }
        }

        return count;
    }

    private static bool ContainsFatCalli(Statement stmt)
    {
        var candidate = stmt.RootNode;
        if (candidate.Oper is GT_STORE_LCL_VAR)
        {
            candidate = candidate.AsLclVar().Data;
        }

        return candidate.Oper.IsCall && candidate.AsCall().IsFatPointerCandidate;
    }

    private static bool ContainsGuardedDevirtualizationCandidate(Statement stmt)
    {
        var candidate = stmt.RootNode;
        return candidate.Oper.IsCall && candidate.AsCall().IsGuardedDevirtualizationCandidate;
    }
}
