// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public readonly struct StoreLclVarDef(GenTreeLclVarCommon def) : ILocalDef
{
    public GenTreeLclVarCommon DefNode => def;

    public int LclNum => def.LclNum;

    public int MultiDefIndex => BAD_VAR_NUM;

    public bool IsEntire(Compiler compiler) => true;

    public nint GetOffset(Compiler compiler) => 0;

    public ValueSize GetSize(Compiler compiler) => GetStoreSize(compiler);

    public nint GetValueOffset(Compiler compiler) => 0;

    public ValueSize GetStoreSize(Compiler compiler) => compiler.lvaLclValueSize(LclNum);
}
