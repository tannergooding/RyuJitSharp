// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

namespace RyuJitSharp;

#if DEBUG
public partial class Compiler
{
    private struct PostGlobalMorphChecksVisitor : IGenTreeVisitor<PostGlobalMorphChecksVisitor>
    {
        private readonly GenTreeStack _ancestors;

        public PostGlobalMorphChecksVisitor()
        {
            _ancestors = [];
        }

        public static bool DoPostOrder => true;

        public readonly fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user)
        {
            assert(use.WasMorphed);
            assert(use._morphCount <= 5);
            return WALK_CONTINUE;
        }

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<PostGlobalMorphChecksVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }
}
#endif
