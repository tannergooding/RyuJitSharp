// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    private struct ReplaceInlineArgumentVisitor(GenTree target, GenTree replacement) : IGenTreeVisitor<ReplaceInlineArgumentVisitor>
    {
        private readonly GenTreeStack _ancestors = [];

        public static bool DoPreOrder => true;

        public readonly fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            if (use == target)
            {
                use = replacement;
                return fgWalkResult.WALK_SKIP_SUBTREES;
            }

            return fgWalkResult.WALK_CONTINUE;
        }

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => fgWalkResult.WALK_CONTINUE;

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<ReplaceInlineArgumentVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }
}
