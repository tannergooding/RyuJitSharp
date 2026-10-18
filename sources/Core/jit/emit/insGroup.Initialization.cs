// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class insGroup
{
    public insGroup? igNext;

    public insGroup? igPrev;

    public insGroup? igSelf;

    public weight_t igWeight;
    public double igPerfScore;

    private uint igNum;

    public uint igOffs;
    public uint igFuncIdx;
    public ushort igSize;

    public insGroup? igLoopBackEdge;

    public regMask igGCregs;
    public Emitter.instrDesc[]? igData;

    internal nuint igStorageSize;
    internal nuint igDataOffset;

    public Emitter.instrDesc? igLastIns;

    public uint igStkLvl;

    public byte igInsCnt;

    public void InitializeNum(uint num)
    {
        igNum = num;
    }

    public uint GetDisplayId()
    {
        return igNum;
    }
}
