// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
namespace RyuJitSharp;

public struct CheckTransformableIndirectCallsVisitor : IGenTreeVisitor<CheckTransformableIndirectCallsVisitor>
{
    public static bool DoPreOrder => true;
    private readonly GenTreeStack _ancestors;

    public CheckTransformableIndirectCallsVisitor()
    {
        _ancestors = [];
    }

    public readonly Compiler.fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
    {
        if (use.Oper.IsCall)
        {
            var call = use.AsCall();
            assert(!call.IsFatPointerCandidate);
            assert(!call.IsGuardedDevirtualizationCandidate);
        }

        return Compiler.WALK_CONTINUE;
    }

    public readonly Compiler.fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => Compiler.WALK_CONTINUE;

    public Compiler.fgWalkResult WalkTree(ref GenTree use, GenTree? user)
        => IGenTreeVisitor<CheckTransformableIndirectCallsVisitor>.WalkTree(ref this, ref use, user, _ancestors);
}
#endif
