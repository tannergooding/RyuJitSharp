// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.

using System.Collections.Generic;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed class ArrIndex
{
    public int ArrLcl = BAD_VAR_NUM;
    public var_types ArrType = TYP_UNDEF;
    public List<int> IndLcls { get; } = [];
    public List<GenTree> BndsChks { get; } = [];
    public int Rank;
    public BasicBlock? UseBlock;

    // Native value copies share the populated index/check buffers. Consumers do not grow them.
    internal ArrIndex Copy() => (ArrIndex)MemberwiseClone();

#if DEBUG
    public void Print(int dim = -1)
    {
        jitprintf($"V{ArrLcl:D2}");
        for (var i = 0; i < ((dim < 0) ? Rank : dim); i++)
        {
            jitprintf($"[V{IndLcls[i]:D2}]");
        }
    }

    public void PrintBoundsCheckNodes(int dim = -1)
    {
        for (var i = 0; i < ((dim < 0) ? Rank : dim); i++)
        {
            jitprintf($"[{BndsChks[i].TreeId:D6}]");
        }
    }
#endif
}

public sealed class SpanIndex
{
    public int LenLcl = BAD_VAR_NUM;
    public int IndLcl = BAD_VAR_NUM;
    public GenTree? BndsChk;
    public BasicBlock? UseBlock;

#if DEBUG
    public void Print() => jitprintf($"V{LenLcl:D2}[V{IndLcl:D2}]");

    public void PrintBoundsCheckNode() => jitprintf($"[{BndsChk!.TreeId:D6}]");
#endif
}
