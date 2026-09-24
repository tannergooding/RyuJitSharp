// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public void fgAddHandlerLiveVars(BasicBlock block, nint[] ehHandlerLiveVars, ref MemoryKindSet memoryLiveness)
    {
        assert(block.HasPotentialEHSuccs(this));
        var memory = memoryLiveness;
        _ = block.VisitEHSuccs(this, successor => {
            VarSetOps.UnionD(this, ehHandlerLiveVars, successor.bbLiveIn);
            memory |= successor.bbMemoryLiveIn;
            return BasicBlockVisit.Continue;
        });
        memoryLiveness = memory;
    }
}
