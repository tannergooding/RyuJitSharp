// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct VarScopeDsc
{
    /// <summary>(remapped) LclVarDsc number</summary>
    public int vsdVarNum;

    /// <summary>'which' in eeGetLVinfo().</summary>
    /// <remarks>Also, it is the index of this entry in the <see cref="Compiler.Info.compVarScopes" /> array, which is useful since the array is also accessed via the compEnterScopeList and compExitScopeList sorted arrays.</remarks>
    public int vsdLVnum;

    /// <summary>instr offset of beg of life</summary>
    public IL_OFFSET vsdLifeBeg;

    /// <summary>instr offset of end of life</summary>
    public IL_OFFSET vsdLifeEnd;

#if DEBUG
    /// <summary>name of the var</summary>
    public VarName vsdName;
#endif
}
