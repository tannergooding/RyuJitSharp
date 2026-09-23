// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public readonly struct CallLocalDef(GenTreeLclVarCommon def, bool isEntire, nint offset, ValueSize size) : ILocalDef
{
    public GenTreeLclVarCommon DefNode => def;
    public int LclNum => def.LclNum;
    public int MultiDefIndex => BAD_VAR_NUM;
    public bool IsEntire(Compiler compiler) => isEntire;
    public nint GetOffset(Compiler compiler) => offset;
    public ValueSize GetSize(Compiler compiler) => size;
    public nint GetValueOffset(Compiler compiler) => 0;
    public ValueSize GetStoreSize(Compiler compiler) => size;
}
