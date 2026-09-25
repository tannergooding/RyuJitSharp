// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

public sealed partial class insGroup
{
    public insGroup? igNext;

#if TARGET_XARCH
    public insGroup? igPrev;
#endif

#if DEBUG
    public insGroup? igSelf;
    public BasicBlock? lastGeneratedBlock;
    public List<BasicBlock> igBlocks = [];
    public nuint igDataSize;
#endif

#if DEBUG || LATE_DISASM
    public weight_t igWeight;
    public double igPerfScore;
#endif

    private uint igNum;

    public uint igOffs;
    public uint igFuncIdx;
    public ushort igSize;

#if FEATURE_LOOP_ALIGN
    public insGroup? igLoopBackEdge;
#endif

    public regMask igGCregs;
    public Emitter.instrDesc[]? igData;

    internal nuint igStorageSize;
    internal nuint igDataOffset;

#if TARGET_XARCH
    public Emitter.instrDesc? igLastIns;
#endif

#if EMIT_TRACK_STACK_DEPTH
    public uint igStkLvl;
#endif

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
