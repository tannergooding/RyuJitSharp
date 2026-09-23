// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public abstract class SpanningTreeVisitor
{
    // Non-tree edge placement also describes later probe relocation and duplication.
    public enum EdgeKind
    {
        Unknown,
        PostdominatesSource,
        Pseudo,
        DominatesTarget,
        CriticalEdge,
        Deleted,
        Relocated,
        Leader,
        Duplicate,
    }

    public abstract void Badcode();

    public abstract void VisitBlock(BasicBlock block);

    public abstract void VisitTreeEdge(BasicBlock source, BasicBlock target);

    public abstract void VisitNonTreeEdge(BasicBlock source, BasicBlock target, EdgeKind kind);
}
