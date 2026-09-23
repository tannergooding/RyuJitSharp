// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
namespace RyuJitSharp;

/// <summary>Visitor to ensure that a tree node that is not an inline candidate is noted as a failed inline.</summary>
/// <remarks>Invokes fgNoteNonInlineCandidate on the nodes it finds.</remarks>
public struct FindNonInlineCandidateVisitor : IGenTreeVisitor<FindNonInlineCandidateVisitor>
{
    public static bool DoPreOrder => true;

    public static bool UseExecutionOrder => true;

    private readonly Compiler _compiler;
    private readonly Statement _stmt;
    private readonly GenTreeStack _ancestors;

    public FindNonInlineCandidateVisitor(Compiler compiler, Statement stmt)
    {
        _compiler = compiler;
        _stmt = stmt;
        _ancestors = [];
    }

    public readonly Compiler.fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user)
        => Compiler.fgWalkResult.WALK_CONTINUE;

    public readonly Compiler.fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
    {
        var tree = use;

        if (tree.Oper is GT_CALL)
        {
            _compiler.fgNoteNonInlineCandidate(_stmt, tree.AsCall());
        }

        return Compiler.fgWalkResult.WALK_CONTINUE;
    }

    public Compiler.fgWalkResult WalkTree(ref GenTree use, GenTree? user)
        => IGenTreeVisitor<FindNonInlineCandidateVisitor>.WalkTree(ref this, ref use, user, _ancestors);
}
#endif
