// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public readonly struct PromotedStoreLclVarDef : ILocalDef
{
    private readonly byte _index;

    public PromotedStoreLclVarDef(GenTreeLclVarCommon def, int lclNum, int index)
    {
        assert((uint)index < byte.MaxValue);
        DefNode = def;
        LclNum = lclNum;
        _index = (byte)index;
    }

    public GenTreeLclVarCommon DefNode { get; }

    public int LclNum { get; }

    public int MultiDefIndex => _index;

    public bool IsEntire(Compiler compiler) => true;

    public nint GetOffset(Compiler compiler) => 0;

    public ValueSize GetSize(Compiler compiler) => compiler.lvaGetDesc(LclNum).lvValueSize;

    public nint GetValueOffset(Compiler compiler) => compiler.lvaGetDesc(LclNum).lvFldOffset;

    public ValueSize GetStoreSize(Compiler compiler) => compiler.lvaLclValueSize(DefNode.LclNum);
}
