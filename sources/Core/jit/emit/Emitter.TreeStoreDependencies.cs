// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if CPU_LOAD_STORE_ARCH || TARGET_WASM
namespace RyuJitSharp;

public partial class Emitter
{
#if CPU_LOAD_STORE_ARCH
    public bool emitInsIsStore(instruction ins)
    {
#if TARGET_ARM64
        // The encoder-only predicate uses a checked unsigned conversion.
        return ((int)ins >= 0) && emitInsIsStoreArm64(ins);
#elif TARGET_RISCV64
        // Pseudo instructions such as LEA are not represented in the instruction table.
        return ((uint)ins < (uint)CodeGen.instInfo.Length) &&
               ((CodeGen.instInfo[(int)ins] & RISCV64_ST) != 0);
#elif TARGET_LOONGARCH64
        // Pseudo instructions such as LEA are not represented in the instruction table.
        return ((uint)ins < (uint)CodeGen.instInfo.Length) &&
               ((CodeGen.instInfo[(int)ins] & LOONGARCH64_ST) != 0);
#elif TARGET_ARM
        const byte ST = 4;
        return ((int)ins >= 0) &&
               ((uint)ins < (uint)CodeGen.instInfo.Length) &&
               ((CodeGen.instInfo[(int)ins] & ST) != 0);
#elif TARGET_WASM
        throw new FatalJitException(CORJIT_INTERNALERROR,
            $"Wasm store classification is unreachable for instruction {ins}.");
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Target instruction store classification is not implemented.");
#endif
    }
#endif

#if TARGET_WASM
    public void emitIns_S_R(instruction ins, emitAttr attr, regNumber reg, int varNum, int offset)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "WebAssembly local-stack store recording is not implemented.");
    }
#endif
}
#endif
