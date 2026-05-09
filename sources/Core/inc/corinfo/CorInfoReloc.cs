// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public enum CorInfoReloc
{
    NONE,

    //
    // General relocation types
    //

    /// <summary>Direct/absolute pointer sized address</summary>
    DIRECT,

    /// <summary>32-bit relative address from byte following reloc</summary>
    RELATIVE32,

    //
    // Arm64 relocs
    //

    /// <summary>Arm64: B, BL</summary>
    ARM64_BRANCH26,

    /// <summary>ADRP</summary>
    ARM64_PAGEBASE_REL21,

    /// <summary>ADD/ADDS (immediate) with zero shift, for page offset</summary>
    ARM64_PAGEOFFSET_12A,

    //
    // Linux arm64
    //

    ARM64_LIN_TLSDESC_ADR_PAGE21,

    ARM64_LIN_TLSDESC_LD64_LO12,

    ARM64_LIN_TLSDESC_ADD_LO12,

    ARM64_LIN_TLSDESC_CALL,

    //
    // Windows arm64
    //

    /// <summary>ADD high 12-bit offset for tls</summary>
    ARM64_WIN_TLS_SECREL_HIGH12A,

    /// <summary>ADD low 12-bit offset for tls</summary>
    ARM64_WIN_TLS_SECREL_LOW12A,

    //
    // Windows x64
    //

    AMD64_WIN_SECREL,

    //
    // Linux x64
    //

    /// <summary>GD model</summary>
    AMD64_LIN_TLSGD,

    //
    // Arm32 relocs
    //

    /// <summary>Thumb2: B, BL</summary>
    ARM32_THUMB_BRANCH24,

    /// <summary>Thumb2: MOVW/MOVT</summary>
    ARM32_THUMB_MOV32,

    // The identifier for ARM32-specific PC-relative address
    // computation corresponds to the following instruction
    // sequence:
    //  l0: movw rX, #imm_lo  // 4 byte
    //  l4: movt rX, #imm_hi  // 4 byte
    //  l8: add  rX, pc <- after this instruction rX = relocTarget
    //
    // Program counter at l8 is address of l8 + 4
    // Address of relocated movw/movt is l0
    // So, imm should be calculated as the following:
    //  imm = relocTarget - (l8 + 4) = relocTarget - (l0 + 8 + 4) = relocTarget - (l_0 + 12)
    // So, the value of offset correction == 12

    /// <summary>Thumb2: MOVW/MOVT</summary>
    ARM32_THUMB_MOV32_PCREL,

    //
    // LoongArch64 relocs
    //

    /// <summary>LoongArch64: pcalau12i+imm12</summary>
    LOONGARCH64_PC,

    /// <summary>LoongArch64: pcaddu18i+jirl</summary>
    LOONGARCH64_JIR,

    //
    // RISCV64 relocs
    //

    /// <summary>RiscV64: auipc + jalr</summary>
    RISCV64_CALL_PLT,

    /// <summary>RiscV64: auipc + I-type</summary>
    RISCV64_PCREL_I,

    /// <summary>RiscV64: auipc + S-type</summary>
    RISCV64_PCREL_S,

    //
    // Wasm relocs
    //

    /// <summary>Wasm: a function index encoded as a 5-byte varuint32. Used for the immediate argument of a call instruction.</summary>
    WASM_FUNCTION_INDEX_LEB,

    /// <summary>Wasm: a function table index encoded as a 5-byte varint32. Used to refer to the immediate argument of a i32.const instruction, e.g. taking the address of a function.</summary>
    WASM_TABLE_INDEX_SLEB,

    /// <summary>Wasm: a linear memory index encoded as a 5-byte varuint32. Used for the immediate argument of a load or store instruction, e.g. directly loading from or storing to a C++ global.</summary>
    WASM_MEMORY_ADDR_LEB,

    /// <summary>Wasm: a linear memory index encoded as a 5-byte varint32. Used for the immediate argument of a i32.const instruction, e.g. taking the address of a C++ global.</summary>
    WASM_MEMORY_ADDR_SLEB,

    /// <summary>Wasm: a relative linear memory index encoded as a 5-byte varint32. Used as the immediate argument of an i32.const instruction, e.g. in R2R scenarios as an offset from __image_base</summary>
    WASM_MEMORY_ADDR_REL_SLEB,

    /// <summary>Wasm: a type index encoded as a 5-byte varuint32, e.g. the type immediate in a call_indirect.</summary>
    WASM_TYPE_INDEX_LEB,

    /// <summary>Wasm: a global index encoded as a 5-byte varuint32, e.g. the index immediate in a get_global.</summary>
    WASM_GLOBAL_INDEX_LEB,

    /// <summary>Wasm: a relative linear memory index encoded as a 5-byte varuint32. Used as the immediate argument of a load or store instruction, e.g. in R2R scenarios as an offset from __image_base</summary>
    WASM_MEMORY_ADDR_REL_LEB,
}
