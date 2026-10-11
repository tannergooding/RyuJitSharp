// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_WASM
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    internal static insFormat emitInsFormat(instruction ins)
    {
        assert((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins) < (uint)s_wasmInstructionFormats.Length);
        assert(s_wasmInstructionFormats[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)] != IF_NONE);
        return s_wasmInstructionFormats[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)];
    }

    internal static uint GetInsOpcode(instruction ins)
    {
        assert((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins) < (uint)s_wasmOpcodes.Length);
        return s_wasmOpcodes[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)];
    }

    internal static byte GetOpcodePrefix(instruction ins)
    {
        assert((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins) < (uint)s_wasmOpcodePrefixes.Length);
        return s_wasmOpcodePrefixes[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)];
    }

    private static bool HasOpcodePrefix(instruction ins)
    {
        return GetOpcodePrefix(ins) != 0;
    }
}
#endif
