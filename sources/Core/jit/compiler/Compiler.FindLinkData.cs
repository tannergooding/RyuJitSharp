// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Compiler
{
    public ref struct FindLinkData(GenTree node, ref GenTree edge, GenTree? user)
    {
        public GenTree nodeToFind = node;
        public ref GenTree result = ref edge;
        public GenTree? parent = user;
    }

    private struct FindLinkWalker : IGenTreeVisitor<FindLinkWalker>
    {
        public static bool DoPreOrder => true;

        private readonly GenTreeStack _ancestors;
        private readonly GenTree _node;
        private GenTree? _parent;
        private bool _found;
        private int _edgeIndex;

        public FindLinkWalker(GenTree node)
        {
            _ancestors = [];
            _node = node;
        }

        public readonly FindLinkData GetResult(Statement statement)
        {
            if (!_found)
            {
                return new(_node, ref Unsafe.NullRef<GenTree>(), null);
            }

            if (_parent is null)
            {
                return new(_node, ref statement.RootNodeRef, null);
            }

            var index = 0;
            foreach (ref var edge in _parent.UseEdges)
            {
                if (index++ == _edgeIndex)
                {
                    return new(_node, ref edge, _parent);
                }
            }

            throw new System.InvalidOperationException("The recorded operand is no longer present.");
        }

        public fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            if (use == _node)
            {
                _found = true;
                _parent = user;
                if (user is not null)
                {
                    // Visitor refs cannot escape into its state. Record the exact
                    // slot, not just node identity: multiple uses can share a node.
                    _edgeIndex = 0;
                    foreach (ref var edge in user.UseEdges)
                    {
                        if (Unsafe.AreSame(ref edge, ref use))
                        {
                            return WALK_ABORT;
                        }

                        _edgeIndex++;
                    }

                    throw new System.InvalidOperationException("The visited operand is not owned by its parent.");
                }

                return WALK_ABORT;
            }

            return WALK_CONTINUE;
        }

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<FindLinkWalker>.WalkTree(ref this, ref use, user, _ancestors);
    }
}
