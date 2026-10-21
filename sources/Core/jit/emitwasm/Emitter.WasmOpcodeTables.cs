// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;

namespace RyuJitSharp;

public partial class Emitter
{
    private static void RequireWasmTarget()
    {
        if (!Configuration.TARGET_WASM)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                "WebAssembly emitter operations require WebAssembly.");
            return;
        }
    }

    public void emitInsWasm(instruction ins)
    {
        RequireWasmTarget();
        var id = emitNewInstrSmall(EA_8BYTE);
        id.idIns(ins);
        id.idInsFmt(emitInsFormatWasm(ins));

        dispIns(id);
        appendToCurIG(id);
    }

    internal static insFormat emitInsFormatWasm(instruction ins)
    {
        RequireWasmTarget();

        assert((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins) < (uint)s_wasmInstructionFormats.Length);
        assert(s_wasmInstructionFormats[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)] != IF_NONE);
        return s_wasmInstructionFormats[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)];
    }

    internal static uint GetInsOpcode(instruction ins)
    {
        RequireWasmTarget();
        assert((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins) < (uint)s_wasmOpcodes.Length);
        return s_wasmOpcodes[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)];
    }

    internal static byte GetOpcodePrefix(instruction ins)
    {
        RequireWasmTarget();
        assert((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins) < (uint)s_wasmOpcodePrefixes.Length);
        return s_wasmOpcodePrefixes[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)];
    }

    private static bool HasOpcodePrefix(instruction ins)
    {
        return GetOpcodePrefix(ins) != 0;
    }
}
