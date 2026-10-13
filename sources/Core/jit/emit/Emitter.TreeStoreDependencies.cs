// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    public bool emitInsIsStore(instruction ins)
    {
        if (!Configuration.CPU_LOAD_STORE_ARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "Target instruction store classification is not implemented.");
        }

        if (Configuration.TARGET_ARM64)
        {
        // The encoder-only predicate uses a checked unsigned conversion.
            return InstructionOrdinalMaps.TryGetNativeInstructionOrdinal(ins, out _) && emitInsIsStoreArm64(ins);
        }

        if (Configuration.TARGET_RISCV64)
        {
        // Pseudo instructions such as LEA are not represented in the instruction table.
            return InstructionOrdinalMaps.TryGetNativeInstructionOrdinal(ins, out var nativeOrdinal) &&
                   (uint)nativeOrdinal < (uint)CodeGen.instInfo.Length &&
                   (((byte)CodeGen.instInfo[nativeOrdinal] & RISCV64_ST) != 0);
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
        // Pseudo instructions such as LEA are not represented in the instruction table.
            return InstructionOrdinalMaps.TryGetNativeInstructionOrdinal(ins, out var nativeOrdinal) &&
                   (uint)nativeOrdinal < (uint)CodeGen.instInfo.Length &&
                   (((byte)CodeGen.instInfo[nativeOrdinal] & LOONGARCH64_ST) != 0);
        }

        if (Configuration.TARGET_ARM)
        {
            const byte storeFlag = 4;
            return InstructionOrdinalMaps.TryGetNativeInstructionOrdinal(ins, out var nativeOrdinal) &&
                   (uint)nativeOrdinal < (uint)CodeGen.instInfo.Length &&
                   (((byte)CodeGen.instInfo[nativeOrdinal] & storeFlag) != 0);
        }

        if (Configuration.TARGET_WASM)
        {
            throw new FatalJitException(CORJIT_INTERNALERROR,
                $"Wasm store classification is unreachable for instruction {ins}.");
        }

        throw new FatalJitException(CORJIT_SKIPPED, "Target instruction store classification is not implemented.");
    }

}
