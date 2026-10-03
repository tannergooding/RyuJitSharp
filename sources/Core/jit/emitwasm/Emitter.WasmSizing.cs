// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_WASM
using System;
using System.Numerics;

namespace RyuJitSharp;

public partial class Emitter
{
    public static instruction emitJumpKindToIns(emitJumpKind jumpKind)
    {
        return jumpKind switch
        {
            EJ_NONE => INS_nop,
            EJ_jmp => INS_br,
            EJ_jmpif => INS_br_if,
            _ => throw new FatalJitException(CORJIT_INTERNALERROR, $"Unexpected Wasm jump kind {jumpKind}."),
        };
    }

    public static int SizeOfSLEB128(long value)
    {
        var signAdjustedValue = unchecked((ulong)(value ^ (value >> 63))) | 1UL;
        var significantBits = 1 + 6 + 64 - BitOperations.LeadingZeroCount(signAdjustedValue);
        return (significantBits * 37) >> 8;
    }

    public void emitIns_I_Ty(instruction ins, uint immediate, WasmValueType valueType, int localIndex)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Wasm local declaration emission is not ported.");
    }

    public void emitIns_S(instruction ins, emitAttr attr, int localNum, int offset)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Wasm stack-slot instruction emission is not ported.");
    }

    public void emitFuncletAddressConstant(nint funcletId)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Wasm funclet address emission is not ported.");
    }

    public void emitIns_BlockTy(instruction ins)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Wasm block-type emission is not ported.");
    }

    public void emitIns_I(instruction ins, emitAttr attr, nint immediate)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Wasm instruction-immediate emission is not ported.");
    }

    public void emitIns_BlockTy(instruction ins, WasmValueType blockType)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Wasm typed block emission is not ported.");
    }

    public void emitIns_Ty_I(instruction ins, WasmValueType type, uint immediate)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Wasm typed immediate emission is not ported.");
    }

    public void emitIns_J(instruction ins, emitAttr attr, uint depth, BasicBlock target)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Wasm depth-indexed instruction emission is not ported.");
    }

    public void emitIns_V128Imm(instruction ins, ReadOnlySpan<byte> immediate)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Wasm v128 immediate emission is not ported.");
    }

    public void emitDataOffsetConstant(nuint dataOffset)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Wasm JIT data-offset constant emission is not ported.");
    }
}
#endif
