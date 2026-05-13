// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct MeasureIRVisitor : IGenTreeVisitor<MeasureIRVisitor>
{
    public static bool DoPreOrder => true;

    public static bool UseExecutionOrder => true;

    private readonly GenTreeStack _ancestors;

    private int _nodeCount;

    public MeasureIRVisitor()
    {
        _ancestors = [];
    }

    public readonly int NodeCount => _nodeCount;

    public readonly Compiler.fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user)
        => Compiler.WALK_CONTINUE;

    public Compiler.fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
    {
        _nodeCount++;
        return Compiler.WALK_CONTINUE;
    }

    public Compiler.fgWalkResult WalkTree(ref GenTree use, GenTree? user)
        => IGenTreeVisitor<MeasureIRVisitor>.WalkTree(ref this, ref use, user, _ancestors);
}
