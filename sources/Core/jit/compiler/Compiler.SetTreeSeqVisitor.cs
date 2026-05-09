// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    private partial struct SetTreeSeqVisitor : IGenTreeVisitor<SetTreeSeqVisitor>
    {
        public static bool DoPostOrder => true;

        public static bool UseExecutionOrder => true;

        private GenTree _prevNode;
        private readonly Compiler _compiler;
        private readonly GenTreeStack _ancestors;
        private readonly bool _isLIR;

        public SetTreeSeqVisitor(Compiler compiler, GenTree tree, bool isLIR)
        {
            _prevNode = tree;
            _compiler = compiler;
            _ancestors = [];
            _isLIR = isLIR;

#if DEBUG
            tree._seqNum = 0;
#endif
        }

        public readonly fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user)
        {
            var node = use;

            if (_isLIR)
            {
                node.IsReverseOp = false;
            }

            var prevNode = _prevNode;
            node.Prev = prevNode;
            prevNode.Next = node;

#if DEBUG
            node._seqNum = prevNode._seqNum + 1;
#endif

            _prevNode = node;
            return WALK_CONTINUE;
        }

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user) => IGenTreeVisitor<SetTreeSeqVisitor>.WalkTree(ref this, ref use, user, _ancestors);

        public GenTree Sequence()
        {
            // We have set "_prevNode" to "tree" in the constructor
            // this will give us a circular list here:
            //   ("_prevNode.Next == firstNode", "firstNode.Prev == _prevNode").

            var tree = _prevNode;
            _ = WalkTree(ref tree, user: null);
            assert(tree == _prevNode);

            // Extract the first node in the sequence and break the circularity.
            var lastNode = tree;
            var firstNode = lastNode.Next;

            assert(firstNode is not null);

            lastNode.Next = null;
            firstNode.Prev = null;

            return firstNode;
        }
    }
}
