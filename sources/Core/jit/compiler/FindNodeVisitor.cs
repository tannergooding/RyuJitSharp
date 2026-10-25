// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public ref struct FindNodeVisitor<TPredicate> : IGenTreeVisitor<FindNodeVisitor<TPredicate>>
    where TPredicate : struct, IGenTreePredicate<TPredicate>
{
    public static bool DoPreOrder => true;

    private GenTreeStack _ancestors;
    private ref TPredicate _predicate;
    private readonly GenTreeFlags _requiredFlagsToDescendIntoTree;

    private GenTree? _result;

    public FindNodeVisitor(ref TPredicate predicate, GenTreeFlags requiredFlagsToDescendIntoTree)
    {
        _ancestors = [];
        _predicate = ref predicate;
        _requiredFlagsToDescendIntoTree = requiredFlagsToDescendIntoTree;
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

        if (_predicate.IsMatch(use))
        {
            _result = use;
            return Compiler.WALK_ABORT;
        }

        return Compiler.WALK_CONTINUE;
    }

    public Compiler.fgWalkResult WalkTree(ref GenTree use, GenTree? user)
        => IGenTreeVisitor<FindNodeVisitor<TPredicate>>.WalkTree(ref this, ref use, user, _ancestors);
}
