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
    private const uint PADDED_RELOC_SIZE = 5;

    public void emitIns(instruction ins)
    {
        var id = emitNewInstrSmall(EA_8BYTE);
        id.idIns(ins);
        id.idInsFmt(emitInsFormat(ins));

        dispIns(id);
        appendToCurIG(id);
    }

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

    public static int SizeOfULEB128(ulong value)
    {
        var adjustedBitCount = 6 + 64 - BitOperations.LeadingZeroCount(value | 1UL);

        // Adding six rounds up for division by seven. Multiplication by ceil(256 / 7)
        // followed by an eight-bit shift is exact for this value, which is at most 70.
        return (adjustedBitCount * 37) >> 8;
    }

    public static int SizeOfSLEB128(long value)
    {
        var signAdjustedValue = unchecked((ulong)(value ^ (value >> 63))) | 1UL;
        var significantBits = 1 + 6 + 64 - BitOperations.LeadingZeroCount(signAdjustedValue);
        return (significantBits * 37) >> 8;
    }

    public static byte GetWasmValueTypeCode(WasmValueType type)
    {
        ReadOnlySpan<byte> typeCodeMapping =
        [
            0x00, // Invalid
            0x7F, // I32
            0x7E, // I64
            0x7D, // F32
            0x7C, // F64
            0x7B, // V128
            0x69, // ExnRef
        ];

        assert((uint)typeCodeMapping.Length == (uint)WasmValueType.Count);

        return typeCodeMapping[unchecked((int)type)];
    }

    public void emitIns_I_Ty(instruction ins, uint immediate, WasmValueType valueType, int localIndex)
    {
        var id = emitNewInstrLclVarDecl(EA_8BYTE, immediate, valueType, localIndex);
        id.idIns(ins);
        id.idInsFmt(emitInsFormat(ins));

        dispIns(id);
        appendToCurIG(id);
    }

    public void emitIns_S(instruction ins, emitAttr attr, int localNum, int offset)
    {
        assert(_compiler is not null);
        var localOffset = _compiler.lvaFrameAddress(localNum, out _);
        var addressOffset = unchecked(localOffset + offset);
        noway_assert(addressOffset >= 0);

        var id = emitNewInstrSC(attr, addressOffset);
        id.idIns(ins);
        id.idInsFmt(emitInsFormat(ins));

        if (_debugInfoSize > 0)
        {
            var debugInfo = id.idDebugOnlyInfo();
            assert(debugInfo is not null);
            debugInfo.idLclNum = localNum;
            debugInfo.idLclOffset = unchecked((uint)offset);
        }

        dispIns(id);
        appendToCurIG(id);
    }

    public void emitFuncletAddressConstant(nint funcletId)
    {
        assert(_compiler is not null);
        nint tableBase;
        unsafe
        {
            tableBase = unchecked((nint)_compiler.eeGetWasmWellKnownGlobals().tableBase);
        }
        emitIns_I(INS_global_get, EA_HANDLE_CNS_RELOC, tableBase);
        emitIns_I(INS_i32_const_funcletptr, EA_PTRSIZE, funcletId);
        emitIns(INS_i32_add);
    }

    public void emitAddressConstant(nint address)
    {
        emitImageBase();
        emitIns_I(INS_i32_const_address, EA_SET_FLG(EA_PTRSIZE, EA_CNS_RELOC_FLG), address);
        emitIns(INS_i32_add);
    }

    public void emitIns_BlockTy(instruction ins)
    {
        emitIns_BlockTy(ins, WasmValueType.Invalid);
    }

    public void emitIns_I(instruction ins, emitAttr attr, nint immediate)
    {
        if ((ins == INS_local_get) && (_compiler is not null) && _compiler.opts.OptimizationEnabled
            && emitCanPeepholeLastIns())
        {
            var lastIns = emitLastIns;
            if ((lastIns is not null) && (lastIns.idIns() == INS_local_set)
                && (emitGetInsSC(lastIns) == immediate))
            {
                JITDUMP($"\n -- rewriting 'local.set {immediate}' as 'local.tee {immediate}' since it is followed by a get of the same local.\n");
                lastIns.idIns(INS_local_tee);
                return;
            }
        }

        var id = emitNewInstrSC(attr, immediate);
        id.idIns(ins);
        id.idInsFmt(emitInsFormat(ins));

        dispIns(id);
        appendToCurIG(id);
    }

    public void emitIns_BlockTy(instruction ins, WasmValueType blockType)
    {
        emitIns_I(ins, EA_4BYTE, unchecked((nint)blockType));
    }

    public void emitIns_Ty_I(instruction ins, WasmValueType type, uint immediate)
    {
        var id = emitNewInstrValTypeImm(EA_8BYTE, type, immediate);
        id.idIns(ins);
        id.idInsFmt(emitInsFormat(ins));

        dispIns(id);
        appendToCurIG(id);
    }

    public void emitIns_J(instruction ins, emitAttr attr, uint depth, BasicBlock? target)
    {
        var id = emitNewInstrSC(attr, unchecked((nint)depth));
        id.idIns(ins);
        id.idInsFmt(emitInsFormat(ins));

        if ((_debugInfoSize > 0) && (target is not null))
        {
            var debugInfo = id.idDebugOnlyInfo();
            assert(debugInfo is not null);
            debugInfo.idTargetBlock = target;
        }

        dispIns(id);
        appendToCurIG(id);
    }

    public void emitIns_V128Imm(instruction ins, ReadOnlySpan<byte> immediate)
    {
        var id = emitAllocAnyInstr<instrDescV128Imm>(DescriptorSizes.WasmV128Imm, EA_16BYTE);
        var format = emitInsFormat(ins);
        assert(format == insFormat.IF_V128);

        id.idInsFmt(format);
        id.idIns(ins);
        id.idV128Const(immediate);

        dispIns(id);
        appendToCurIG(id);
    }

    public void emitIns_Lane(instruction ins, byte laneIndex)
    {
        var elementSize = CodeGen.instSimdElemSize(ins);
        assert(isValidVectorIndex(elementSize, laneIndex));

        var id = emitNewInstrSC((emitAttr)elementSize, laneIndex);
        var format = emitInsFormat(ins);
        assert(format == insFormat.IF_LANE);
        id.idInsFmt(format);
        id.idIns(ins);

        dispIns(id);
        appendToCurIG(id);
    }

    public void emitDataOffsetConstant(nuint dataOffset)
    {
        assert(_compiler is not null);
        nint imageBase;
        unsafe
        {
            imageBase = unchecked((nint)_compiler.eeGetWasmWellKnownGlobals().imageBase);
        }
        emitIns_I(INS_global_get, EA_HANDLE_CNS_RELOC, imageBase);

        var id = emitNewInstrSC(EA_SET_FLG(EA_PTRSIZE, EA_CNS_RELOC_FLG), unchecked((nint)dataOffset));
        id.idIns(INS_i32_const_dataoffs);
        id.idInsFmt(insFormat.IF_DATAOFFS);
        dispIns(id);
        appendToCurIG(id);

        emitIns(INS_i32_add);
    }

    private void emitImageBase()
    {
        assert(_compiler is not null);
        nint imageBase;
        unsafe
        {
            imageBase = unchecked((nint)_compiler.eeGetWasmWellKnownGlobals().imageBase);
        }
        emitIns_I(INS_global_get, EA_HANDLE_CNS_RELOC, imageBase);
    }

    public void emitIns_MemargAddress(instruction ins, emitAttr attr, nint address)
    {
        assert(emitInsFormat(ins) == insFormat.IF_MEMARG);
        assert(_compiler is not null);
        assert(_compiler.opts.compReloc);
        emitIns_I(ins, EA_SET_FLG(attr, EA_CNS_RELOC_FLG), address);
    }

    public static uint emitGetAlignHintLog2(instrDesc id)
    {
        _ = id;
        // The native implementation is still a FIXME and returns zero for every memarg.
        return 0;
    }

    private static uint GetWasmCodeSize(instrDesc id)
    {
        var instruction = id.idIns();
        var opcode = GetInsOpcode(instruction);
        var size = HasOpcodePrefix(instruction) ? 1u + (uint)SizeOfULEB128(opcode) : 1u;

        switch (id.idInsFmt())
        {
            case insFormat.IF_OPCODE:
            {
                break;
            }

            case insFormat.IF_BLOCK:
            {
                size++;
                break;
            }

            case insFormat.IF_RAW_ULEB128:
            {
                assert(!id.idIsCnsReloc());
                size = (uint)SizeOfULEB128(unchecked((ulong)emitGetInsSC(id)));
                break;
            }

            case insFormat.IF_CODE_SIZE:
            {
                assert(!id.idIsCnsReloc());
                size = PADDED_RELOC_SIZE;
                break;
            }

            case insFormat.IF_LOCAL_DECL:
            {
                assert(id.idIsLclVarDecl());
                size = (uint)SizeOfULEB128(emitGetLclVarDeclCount(id)) + 1;
                break;
            }

            case insFormat.IF_FUNCIDX:
            case insFormat.IF_ULEB128:
            case insFormat.IF_GLOBALIDX:
            {
                size += id.idIsCnsReloc()
                    ? PADDED_RELOC_SIZE
                    : (uint)SizeOfULEB128(unchecked((ulong)emitGetInsSC(id)));
                break;
            }

            case insFormat.IF_MEMADDR:
            case insFormat.IF_FUNCPTR:
            case insFormat.IF_SLEB128:
            {
                size += id.idIsCnsReloc()
                    ? PADDED_RELOC_SIZE
                    : (uint)SizeOfSLEB128(emitGetInsSC(id));
                break;
            }

            case insFormat.IF_FUNCLETPTR:
            case insFormat.IF_FUNCLETIDX:
            case insFormat.IF_DATAOFFS:
            {
                size += PADDED_RELOC_SIZE;
                break;
            }

            case insFormat.IF_CALL_INDIRECT:
            {
                size += id.idIsCnsReloc()
                    ? PADDED_RELOC_SIZE
                    : (uint)SizeOfULEB128(unchecked((ulong)emitGetInsSC(id)));
                size += (uint)SizeOfULEB128(0);
                break;
            }

            case insFormat.IF_F32:
            {
                size += sizeof(float);
                break;
            }

            case insFormat.IF_F64:
            {
                size += sizeof(double);
                break;
            }

            case insFormat.IF_MEMARG:
            {
                var align = emitGetAlignHintLog2(id);
                assert(align < 64);
                size += (uint)SizeOfULEB128(align);
                size += id.idIsCnsReloc()
                    ? PADDED_RELOC_SIZE
                    : (uint)SizeOfULEB128(unchecked((ulong)emitGetInsSC(id)));
                break;
            }

            case insFormat.IF_MEMIDX_MEMIDX:
            {
                var offsetSize = id.idIsCnsReloc()
                    ? PADDED_RELOC_SIZE
                    : (uint)SizeOfULEB128(unchecked((ulong)emitGetInsSC(id)));
                size += 2 * offsetSize;
                break;
            }

            case insFormat.IF_TRY_TABLE:
            {
                size += 1 + (uint)SizeOfULEB128(emitGetValTypeImmImm(id));
                break;
            }

            case insFormat.IF_CATCH_DECL:
            {
                size = 1 + PADDED_RELOC_SIZE + (uint)SizeOfULEB128(unchecked((ulong)emitGetInsSC(id)));
                break;
            }

            case insFormat.IF_V128:
            {
                size += 16;
                break;
            }

            case insFormat.IF_LANE:
            {
                size++;
                break;
            }

            case insFormat.IF_MEMARG_LANE:
            {
                var align = emitGetAlignHintLog2(id);
                assert(align < 64);
                size += (uint)SizeOfULEB128(align);
                size += id.idIsCnsReloc()
                    ? PADDED_RELOC_SIZE
                    : (uint)SizeOfULEB128(unchecked((ulong)emitGetInsSC(id)));
                size++;
                break;
            }

            default:
            {
                throw new FatalJitException(CORJIT_INTERNALERROR,
                    $"Unexpected Wasm instruction descriptor format {id.idInsFmt()}.");
            }
        }

        return size;
    }
}
#endif
