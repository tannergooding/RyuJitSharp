// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class GenTree
{
    private partial struct SetMorphedVisitor : IGenTreeVisitor<SetMorphedVisitor>
    {
        public static bool DoPostOrder => true;

        private readonly GenTreeStack _ancestors;

        public SetMorphedVisitor()
        {
            _ancestors = [];
        }

        public readonly Compiler.fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user) => Compiler.WALK_CONTINUE;

        public readonly Compiler.fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user)
        {
            var node = use;

#if DEBUG
            if (!node.WasMorphed)
            {
                node._debugFlags |= GTF_DEBUG_NODE_MORPHED;
                node._morphCount++;
            }
#endif
            return Compiler.WALK_CONTINUE;
        }

        public Compiler.fgWalkResult WalkTree(ref GenTree use, GenTree? user) => IGenTreeVisitor<SetMorphedVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }
}
