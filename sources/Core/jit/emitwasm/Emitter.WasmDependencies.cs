// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_WASM
namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_MemargLane(instruction ins, emitAttr attr, nint offset, byte laneIdx)
    {
        var id = emitAllocAnyInstr<instrDescMemargLane>(DescriptorSizes.WasmMemargLane, attr);
        var format = emitInsFormat(ins);
        var elementSize = CodeGen.instSimdElemSize(ins);
        assert(format == insFormat.IF_MEMARG_LANE);
        assert(offset >= 0);
        assert(isValidVectorIndex(elementSize, laneIdx));

        id.idInsFmt(format);
        id.idIns(ins);
        id.idcCnsVal = offset;
        id.idSetIsLargeCns();
        id.idLaneIdx(laneIdx);

        dispIns(id);
        appendToCurIG(id);
    }
}
#endif
