// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct ExceptionsWalker : IGenTreeVisitor<ExceptionsWalker>
{
    public static bool DoPreOrder => true;

    private readonly GenTreeStack _ancestors;
    private readonly Compiler _compiler;

    private ExceptionSetFlags _preciseExceptions;

    public ExceptionsWalker(Compiler compiler)
    {
        _ancestors = [];
        _compiler = compiler;
    }

    public readonly ExceptionSetFlags Flags => _preciseExceptions;

    public readonly Compiler.fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user)
        => Compiler.WALK_CONTINUE;

    public Compiler.fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
    {
        if ((use.Flags & GTF_EXCEPT) is 0)
        {
            return Compiler.WALK_SKIP_SUBTREES;
        }
        else
        {
            _preciseExceptions |= use.Exceptions(_compiler);
            return Compiler.WALK_CONTINUE;
        }
    }

    public Compiler.fgWalkResult WalkTree(ref GenTree use, GenTree? user)
        => IGenTreeVisitor<ExceptionsWalker>.WalkTree(ref this, ref use, user, _ancestors);
}
