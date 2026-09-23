// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
namespace RyuJitSharp;

/// <summary>Visitor to make sure there is no more GT_RET_EXPR and GTF_CALL_INLINE_CANDIDATE nodes.</summary>
public struct DebugCheckInlineCandidatesVisitor : IGenTreeVisitor<DebugCheckInlineCandidatesVisitor>
{
    public static bool DoPreOrder => true;

    private readonly GenTreeStack _ancestors;

    public DebugCheckInlineCandidatesVisitor()
    {
        _ancestors = [];
    }

    public readonly Compiler.fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user)
        => Compiler.fgWalkResult.WALK_CONTINUE;

    public readonly Compiler.fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
    {
        var tree = use;

        if (tree.Oper is GT_CALL)
        {
            assert((tree.Flags & GTF_CALL_INLINE_CANDIDATE) is 0);
        }
        else
        {
            assert(tree.Oper is not GT_RET_EXPR);
        }

        return Compiler.fgWalkResult.WALK_CONTINUE;
    }

    public Compiler.fgWalkResult WalkTree(ref GenTree use, GenTree? user)
        => IGenTreeVisitor<DebugCheckInlineCandidatesVisitor>.WalkTree(ref this, ref use, user, _ancestors);
}
#endif
