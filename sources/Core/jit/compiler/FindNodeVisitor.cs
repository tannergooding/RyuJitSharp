// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public struct FindNodeVisitor : IGenTreeVisitor<FindNodeVisitor>
{
    public static bool DoPreOrder => true;

    private readonly GenTreeStack _ancestors;
    private readonly Func<GenTree, bool> _predicate;
    private readonly GenTreeFlags _requiredFlagsToDescendIntoTree;

    private GenTree? _result;

    public FindNodeVisitor(Func<GenTree, bool> predicate, GenTreeFlags requiredFlagsToDescendIntoTree)
    {
        _ancestors = [];
        _predicate = predicate;
    }

    public readonly GenTree? Result => _result;

    public readonly Compiler.fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user)
        => Compiler.WALK_CONTINUE;

    public Compiler.fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
    {
        if ((use.Flags & _requiredFlagsToDescendIntoTree) != _requiredFlagsToDescendIntoTree)
        {
            return Compiler.WALK_SKIP_SUBTREES;
        }

        if (_predicate(use))
        {
            _result = use;
            return Compiler.WALK_ABORT;
        }

        return Compiler.WALK_CONTINUE;
    }

    public Compiler.fgWalkResult WalkTree(ref GenTree use, GenTree? user)
        => IGenTreeVisitor<FindNodeVisitor>.WalkTree(ref this, ref use, user, _ancestors);
}
