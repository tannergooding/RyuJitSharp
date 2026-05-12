// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct FindCatchArgVisitor : IGenTreeVisitor<FindCatchArgVisitor>
{
    public static bool DoPreOrder => true;

    public static bool UseExecutionOrder => true;

    private readonly GenTreeStack _ancestors;

    public FindCatchArgVisitor()
    {
        _ancestors = [];
    }

    public readonly Compiler.fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user)
        => Compiler.WALK_CONTINUE;

    public readonly Compiler.fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        => (use.Oper is GT_CATCH_ARG) ? Compiler.WALK_ABORT : Compiler.WALK_CONTINUE;

    public Compiler.fgWalkResult WalkTree(ref GenTree use, GenTree? user)
        => IGenTreeVisitor<FindCatchArgVisitor>.WalkTree(ref this, ref use, user, _ancestors);
}
