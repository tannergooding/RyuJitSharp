// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
#if DEBUG
    public void fgDispBBLiveness()
    {
        foreach (var block in Blocks)
        {
            fgDispBBLiveness(block);
        }
    }

    public void fgDispBBLiveness(BasicBlock block)
    {
        var allVars = VarSetOps.Union(this, block.bbLiveIn, block.bbLiveOut);
        jitprintf($"{FMT_BB(block.bbNum)} IN ({VarSetOps.Count(this, block.bbLiveIn)})=");
        lvaDispVarSet(block.bbLiveIn, allVars);
        for (var memoryKind = ByrefExposed; memoryKind < MemoryKindCount; memoryKind++)
        {
            if ((block.bbMemoryLiveIn & (1 << (int)memoryKind)) != 0)
            {
                jitprintf($" + {memoryKind}");
            }
        }

        jitprintf($"\n     OUT({VarSetOps.Count(this, block.bbLiveOut)})=");
        lvaDispVarSet(block.bbLiveOut, allVars);
        for (var memoryKind = ByrefExposed; memoryKind < MemoryKindCount; memoryKind++)
        {
            if ((block.bbMemoryLiveOut & (1 << (int)memoryKind)) != 0)
            {
                jitprintf($" + {memoryKind}");
            }
        }
        jitprintf("\n\n");
    }
#endif
}
