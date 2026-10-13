// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_MemargLane(instruction ins, emitAttr attr, nint offset, byte laneIdx)
    {
        RequireWasmTarget();
        var id = emitAllocAnyInstr<instrDescMemargLane>((nuint)DescriptorSizes.WasmMemargLane, attr);
        var format = emitInsFormatWasm(ins);
        var elementSize = CodeGen.instSimdElemSize(ins);
        assert(format == insFormat.IF_WASM_MEMARG_LANE);
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
