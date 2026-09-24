// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct ClearColonCondVisitor : IGenTreeVisitor<ClearColonCondVisitor>
{
    public static bool DoPreOrder => true;

    private readonly GenTreeStack _ancestors;

    public ClearColonCondVisitor()
    {
        _ancestors = [];
    }

    public readonly Compiler.fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
    {
        if (use.Oper == GT_COLON)
        {
            // Nested alternatives remain conditionally executed.
            return Compiler.WALK_SKIP_SUBTREES;
        }

        use.Flags &= ~GTF_COLON_COND;
        return Compiler.WALK_CONTINUE;
    }

    public readonly Compiler.fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => Compiler.WALK_CONTINUE;

    public Compiler.fgWalkResult WalkTree(ref GenTree use, GenTree? user)
        => IGenTreeVisitor<ClearColonCondVisitor>.WalkTree(ref this, ref use, user, _ancestors);
}
