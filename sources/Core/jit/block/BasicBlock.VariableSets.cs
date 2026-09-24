// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class BasicBlock
{
    public void InitVarSets(Compiler compiler)
    {
        VarSetOps.AssignNoCopy(compiler, ref bbVarUse, VarSetOps.MakeEmpty(compiler));
        VarSetOps.AssignNoCopy(compiler, ref bbVarDef, VarSetOps.MakeEmpty(compiler));
        VarSetOps.AssignNoCopy(compiler, ref bbLiveIn, VarSetOps.MakeEmpty(compiler));
        VarSetOps.AssignNoCopy(compiler, ref bbLiveOut, VarSetOps.MakeEmpty(compiler));

        bbMemoryUse = 0;
        bbMemoryDef = 0;
        bbMemoryLiveIn = 0;
        bbMemoryLiveOut = 0;
    }
}
