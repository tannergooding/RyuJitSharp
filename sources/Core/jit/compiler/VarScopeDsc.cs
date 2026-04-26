// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct VarScopeDsc
{
    // (remapped) LclVarDsc number
    public uint vsdVarNum;

    // 'which' in eeGetLVinfo().
    // Also, it is the index of this entry in the info.compVarScopes array, which is useful since the array is also accessed via the compEnterScopeList and compExitScopeList sorted arrays.
    public uint vsdLVnum;

    // instr offset of beg of life
    public IL_OFFSET vsdLifeBeg;

    // instr offset of end of life
    public IL_OFFSET vsdLifeEnd;

#if DEBUG
    // name of the var
    public unsafe VarName vsdName;
#endif
}
