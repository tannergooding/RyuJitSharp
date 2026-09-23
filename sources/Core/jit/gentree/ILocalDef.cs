// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public interface ILocalDef
{
    GenTreeLclVarCommon DefNode { get; }

    int LclNum { get; }

    int MultiDefIndex { get; }

    bool IsEntire(Compiler compiler);

    nint GetOffset(Compiler compiler);

    ValueSize GetSize(Compiler compiler);

    nint GetValueOffset(Compiler compiler);

    ValueSize GetStoreSize(Compiler compiler);
}

public interface ILocalDefVisitor
{
    GenTree.VisitResult Visit<TDef>(TDef def) where TDef : struct, ILocalDef;
}

public static class LocalDefExtensions
{
    public static bool HasMultiDefIndex<TDef>(this TDef def) where TDef : struct, ILocalDef
        => def.MultiDefIndex != BAD_VAR_NUM;

    public static int GetSsaNum<TDef>(this TDef def, Compiler compiler) where TDef : struct, ILocalDef
        => def.MultiDefIndex == BAD_VAR_NUM ? def.DefNode.SsaNum : def.DefNode.GetSsaNum(compiler, def.MultiDefIndex);

    public static void SetSsaNum<TDef>(this TDef def, Compiler compiler, int ssaNum) where TDef : struct, ILocalDef
    {
        if (def.MultiDefIndex == BAD_VAR_NUM)
        {
            def.DefNode.SsaNum = ssaNum;
        }
        else
        {
            def.DefNode.SetSsaNum(compiler, def.MultiDefIndex, ssaNum);
        }
    }
}
