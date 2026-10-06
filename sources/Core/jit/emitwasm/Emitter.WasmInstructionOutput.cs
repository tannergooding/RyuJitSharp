// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_WASM
using System;
using System.Runtime.CompilerServices;
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    private unsafe nuint emitOutputInstrWasm(insGroup ig, instrDesc id, byte** dp)
    {
        const bool SIGNED = true;
        const bool UNSIGNED = false;

        var dst = *dp;
        var size = emitSizeOfInsDsc(id);
        var ins = id.idIns();
        var format = id.idInsFmt();

        switch (format)
        {
            case IF_OPCODE:
            {
                dst += emitOutputOpcode(dst, ins);
                break;
            }

            case IF_BLOCK:
            {
                dst += emitOutputOpcode(dst, ins);
                dst += emitOutputValtypeSig(dst, (WasmValueType)emitGetInsSC(id));
                break;
            }

            case IF_ULEB128:
            {
                assert(!id.idIsCnsReloc());
                dst += emitOutputOpcode(dst, ins);
                dst += emitOutputULEB128(dst, unchecked((ulong)emitGetInsSC(id)));
                break;
            }

            case IF_SLEB128:
            {
                assert(!id.idIsCnsReloc());
                dst += emitOutputOpcode(dst, ins);
                dst += emitOutputSLEB128(dst, emitGetInsSC(id));
                break;
            }

            case IF_MEMADDR:
            {
                dst += emitOutputOpcode(dst, ins);
                dst += emitOutputConstant(dst, id, SIGNED, CorInfoReloc.WASM_MEMORY_ADDR_REL_SLEB);
                break;
            }

            case IF_FUNCLETIDX:
            {
                dst += emitOutputOpcode(dst, ins);
                dst += emitOutputConstantFunclet(dst, id, CorInfoReloc.WASM_FUNCTION_INDEX_LEB);
                break;
            }

            case IF_FUNCLETPTR:
            {
                dst += emitOutputOpcode(dst, ins);
                dst += emitOutputConstantFunclet(dst, id, CorInfoReloc.WASM_TABLE_INDEX_SLEB);
                break;
            }

            case IF_DATAOFFS:
            {
                dst += emitOutputOpcode(dst, ins);
                var dataOffset = unchecked((uint)emitGetInsSC(id));
                var target = emitDataOffsetToPtr(dataOffset);
                emitRecordRelocation(dst, target, CorInfoReloc.WASM_MEMORY_ADDR_REL_SLEB);
                dst += emitOutputPaddedReloc(dst);
                break;
            }

            case IF_FUNCPTR:
            {
                dst += emitOutputOpcode(dst, ins);
                dst += emitOutputConstant(dst, id, SIGNED, CorInfoReloc.WASM_TABLE_INDEX_SLEB);
                break;
            }

            case IF_FUNCIDX:
            {
                dst += emitOutputOpcode(dst, ins);
                dst += emitOutputConstant(dst, id, UNSIGNED, CorInfoReloc.WASM_FUNCTION_INDEX_LEB);
                break;
            }

            case IF_GLOBALIDX:
            {
                dst += emitOutputOpcode(dst, ins);
                dst += emitOutputConstant(dst, id, UNSIGNED, CorInfoReloc.WASM_GLOBAL_INDEX_LEB);
                break;
            }

            case IF_CALL_INDIRECT:
            {
                dst += emitOutputOpcode(dst, ins);
                dst += emitOutputConstant(dst, id, UNSIGNED, CorInfoReloc.WASM_TYPE_INDEX_LEB);
                dst += emitOutputULEB128(dst, 0);
                break;
            }

            case IF_F32:
            {
                dst += emitOutputOpcode(dst, ins);
                var bits = emitGetInsSC(id);
                var value = BitConverter.Int64BitsToDouble(bits);
                var truncated = FloatingPointUtils.convertToSingle(value);
                dst += emitOutputLong(dst, BitConverter.SingleToInt32Bits(truncated));
                break;
            }

            case IF_F64:
            {
                dst += emitOutputOpcode(dst, ins);
                var bits = unchecked((ulong)emitGetInsSC(id));
                Unsafe.WriteUnaligned(unchecked(dst + writeableOffset), bits);
                dst += sizeof(ulong);
                break;
            }

            case IF_RAW_ULEB128:
            {
                assert(!id.idIsCnsReloc());
                dst += emitOutputULEB128(dst, unchecked((ulong)emitGetInsSC(id)));
                break;
            }

            case IF_MEMARG:
            {
                dst += emitOutputOpcode(dst, ins);
                var align = emitGetAlignHintLog2(id);
                assert(align <= uint.MaxValue);
                assert(align < 64);
                dst += emitOutputULEB128(dst, align);
                dst += emitOutputConstant(dst, id, UNSIGNED, CorInfoReloc.WASM_MEMORY_ADDR_REL_LEB);
                break;
            }

            case IF_LOCAL_DECL:
            {
                assert(id.idIsLclVarDecl());
                dst += emitOutputULEB128(dst, emitGetLclVarDeclCount(id));
                dst += emitOutputByte(dst, GetWasmValueTypeCode(emitGetLclVarDeclType(id)));
                break;
            }

            case IF_MEMIDX_MEMIDX:
            {
                dst += emitOutputOpcode(dst, ins);
                var constant = emitGetInsSC(id);
                dst += emitOutputULEB128(dst, unchecked((ulong)constant));
                dst += emitOutputULEB128(dst, unchecked((ulong)constant));
                break;
            }

            case IF_TRY_TABLE:
            {
                dst += emitOutputOpcode(dst, ins);
                assert(id.idIsValTypeImm());
                dst += emitOutputValtypeSig(dst, emitGetValTypeImmType(id));
                dst += emitOutputULEB128(dst, emitGetValTypeImmImm(id));
                break;
            }

            case IF_CATCH_DECL:
            {
                dst += emitOutputByte(dst, 1);
                emitRecordRelocation(dst, emitCodeBlock, CorInfoReloc.WASM_CLR_RESTORE_CONTEXT_EXCEPTION_TAG_LEB);
                dst += emitOutputPaddedReloc(dst);
                dst += emitOutputULEB128(dst, unchecked((ulong)emitGetInsSC(id)));
                break;
            }

            case IF_CODE_SIZE:
            {
                assert(_compiler is not null);
                assert(emitCurIG is not null);
                var func = _compiler.funGetFunc(emitCurIG.igFuncIdx);
                var startLocation = func.startLoc
                    ?? throw new FatalJitException(CORJIT_INTERNALERROR, "Wasm function start location is missing.");
                var endLocation = func.endLoc
                    ?? throw new FatalJitException(CORJIT_INTERNALERROR, "Wasm function end location is missing.");
                var startOffset = startLocation.CodeOffset(this);
                var endOffset = endLocation.CodeOffset(this);
                assert(endOffset >= startOffset + PADDED_RELOC_SIZE);
                var codeSize = endOffset - startOffset - PADDED_RELOC_SIZE;
                dst += emitOutputULEB128Padded(dst, codeSize);
                break;
            }

            case IF_V128:
            {
                dst += emitOutputOpcode(dst, ins);
                var immediate = emitGetV128ImmValue(id);
                for (var index = 0; index < 16; index++)
                {
                    dst += emitOutputByte(dst, immediate[index]);
                }
                break;
            }

            case IF_LANE:
            {
                dst += emitOutputOpcode(dst, ins);
                dst += emitOutputByte(dst, emitGetLaneImmValue(id));
                break;
            }

            case IF_MEMARG_LANE:
            {
                dst += emitOutputOpcode(dst, ins);
                var lane = emitGetLaneImmValue(id);
                var align = emitGetAlignHintLog2(id);
                var offset = emitGetInsSC(id);
                assert(align < 64);
                dst += emitOutputULEB128(dst, align);
                dst += emitOutputULEB128(dst, unchecked((ulong)offset));
                dst += emitOutputByte(dst, lane);
                break;
            }

            default:
            {
                throw new FatalJitException(CORJIT_INTERNALERROR,
                    $"Unexpected Wasm instruction output format {format}.");
            }
        }

#if DEBUG
        assert(_compiler is not null);
        if (_compiler.opts.disAsm || _compiler.verbose)
        {
            emitDispIns(id, false, _compiler.opts.dspGCtbls, true, emitCurCodeOffs(*dp), *dp,
                unchecked((nuint)(dst - *dp)), ig);
        }
#else
        assert(_compiler is not null);
        if (_compiler.opts.disAsm)
        {
            emitDispIns(id, false, false, true, emitCurCodeOffs(*dp), *dp,
                unchecked((nuint)(dst - *dp)), ig);
        }
#endif

        *dp = dst;
        return unchecked((nuint)size);
    }

    private unsafe nuint emitOutputULEB128(byte* destination, ulong value)
    {
        var buffer = unchecked(destination + writeableOffset);
        var position = 0;
        do
        {
            var next = unchecked((byte)(value & 0x7F));
            value >>= 7;
            if (value != 0)
            {
                next |= 0x80;
            }
            buffer[position++] = next;
        }
        while (value != 0);

        return unchecked((nuint)position);
    }

    private unsafe nuint emitOutputULEB128Padded(byte* destination, ulong value)
    {
        var buffer = unchecked(destination + writeableOffset);
        var index = 0;
        for (; index < PADDED_RELOC_SIZE - 1; index++)
        {
            buffer[index] = unchecked((byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }

        buffer[index] = unchecked((byte)value);
        return (nuint)PADDED_RELOC_SIZE;
    }

    private unsafe nuint emitOutputSLEB128(byte* destination, long value)
    {
        var buffer = unchecked(destination + writeableOffset);
        var position = 0;
        bool isFinal;
        do
        {
            var next = unchecked((byte)(value & 0x7F));
            value >>= 7;
            var signBitSet = (next & 0x40) != 0;
            isFinal = ((value == 0) && !signBitSet) || ((value == -1) && signBitSet);
            if (!isFinal)
            {
                next |= 0x80;
            }
            buffer[position++] = next;
        }
        while (!isFinal);

        return unchecked((nuint)position);
    }

    private unsafe nuint emitOutputOpcode(byte* destination, instruction ins)
    {
        var opcode = GetInsOpcode(ins);
        var prefix = GetOpcodePrefix(ins);
        if (prefix == 0)
        {
            noway_assert(opcode <= byte.MaxValue);
            return emitOutputByte(destination, opcode);
        }

        var size = (nuint)emitOutputByte(destination, prefix);
        size += emitOutputULEB128(unchecked(destination + size), opcode);
        return size;
    }

    private unsafe nuint emitOutputPaddedReloc(byte* destination)
    {
        for (var index = 0; index < PADDED_RELOC_SIZE - 1; index++)
        {
            _ = emitOutputByte(destination, 0x80);
            destination++;
        }

        _ = emitOutputByte(destination, 0);
        return (nuint)PADDED_RELOC_SIZE;
    }

    private unsafe nuint emitOutputConstantFunclet(byte* destination, instrDesc id, CorInfoReloc relocationType)
    {
        emitRecordRelocation(destination, emitCodeBlock, relocationType, unchecked((int)emitGetInsSC(id)));
        return (nuint)PADDED_RELOC_SIZE;
    }

    private unsafe nuint emitOutputConstant(byte* destination, instrDesc id, bool isSigned, CorInfoReloc relocationType)
    {
        if (id.idIsCnsReloc())
        {
            emitRecordRelocation(destination, unchecked((void*)emitGetInsSC(id)), relocationType);
            return emitOutputPaddedReloc(destination);
        }

        return isSigned
            ? emitOutputSLEB128(destination, emitGetInsSC(id))
            : emitOutputULEB128(destination, unchecked((ulong)emitGetInsSC(id)));
    }

    private unsafe nuint emitOutputValtypeSig(byte* destination, WasmValueType valueType)
    {
        var typeCode = valueType == WasmValueType.Invalid ? (byte)0x40 : GetWasmValueTypeCode(valueType);
        return emitOutputByte(destination, typeCode);
    }
}
#endif
