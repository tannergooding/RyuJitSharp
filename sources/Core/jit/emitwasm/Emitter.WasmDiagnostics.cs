// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_WASM
using System;
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
#if DEBUG
    private static void emitInsSanityCheck(instrDesc id)
    {
    }
#endif

    public unsafe void emitDispIns(instrDesc id, bool isNew, bool doffs, bool asmfm,
        uint offset = 0, byte* code = null, nuint size = 0, insGroup? ig = null)
    {
        assert(_compiler is not null);
#if DEBUG
        if (_compiler.verbose)
        {
            var debugInfo = id.idDebugOnlyInfo();
            assert(debugInfo is not null);
            jitprintf($"IN{debugInfo.idNum:x4}: ");
        }
#endif

        if (code == null)
        {
            size = 0;
        }
        if (!isNew && !asmfm && (size != 0))
        {
            doffs = true;
        }

        emitDispInsAddr(code);
        emitDispInsOffs(offset, doffs);

        if (code != null)
        {
            assert(((code >= emitCodeBlock) && (code < emitCodeBlock + unchecked((uint)emitTotalHotCodeSize)))
                || ((code >= emitColdCodeBlock) && (code < emitColdCodeBlock + unchecked((uint)emitTotalColdCodeSize))));
        }

        if (_compiler.opts.disCodeBytes && !_compiler.opts.disDiffable && (size != 0))
        {
            var codeRW = unchecked(code + writeableOffset);
            var length = 0;
            for (nuint index = 0; index < size; index++)
            {
                jitprintf($" {codeRW[index]:X2}");
                length += 3;
            }

            const int PAD_WIDTH = 28;
            if (length < PAD_WIDTH)
            {
                jitprintf(new string(' ', PAD_WIDTH - length));
            }
            jitprintf(" | ");
        }

        jitprintf("      ");
        jitprintf(codeGen.genInsName(id.idIns()));

        var format = id.idInsFmt();
        switch (format)
        {
            case IF_OPCODE:
            {
                break;
            }

            case IF_BLOCK:
            {
                var type = (WasmValueType)emitGetInsSC(id);
                if (type != WasmValueType.Invalid)
                {
                    jitprintf($" {WasmValueTypeName(type)}");
                }
                break;
            }

            case IF_RAW_ULEB128:
            case IF_ULEB128:
            case IF_FUNCIDX:
            case IF_GLOBALIDX:
            {
                jitprintf($" {unchecked((ulong)emitGetInsSC(id))}");
                dispJumpTargetIfAny(id);
                dispHandleIfAny(id);
                dispLocalInfoIfAny(id);
                break;
            }

            case IF_CALL_INDIRECT:
            {
                jitprintf($" {unchecked((ulong)emitGetInsSC(id))} 0");
                dispHandleIfAny(id);
                break;
            }

            case IF_MEMIDX_MEMIDX:
            {
                var immediate = unchecked((ulong)emitGetInsSC(id));
                jitprintf($" {immediate} {immediate}");
                break;
            }

            case IF_LOCAL_DECL:
            {
                var count = emitGetLclVarDeclCount(id);
                var type = emitGetLclVarDeclType(id);
                assert(count > 0);
                var debugInfo = id.idDebugOnlyInfo();
                if ((_debugInfoSize > 0) && (debugInfo is not null))
                {
                    var first = ((instrDescLclVarDecl)id).lclBaseIndex;
                    if (count > 1)
                    {
                        jitprintf($"[{first}..{first + count - 1}] type={WasmValueTypeName(type)}");
                    }
                    else
                    {
                        jitprintf($"[{first}] type={WasmValueTypeName(type)}");
                    }
                }
                else
                {
                    jitprintf($" count={count} type={WasmValueTypeName(type)}");
                }
                break;
            }

            case IF_MEMADDR:
            case IF_FUNCPTR:
            case IF_SLEB128:
            {
                jitprintf($" {emitGetInsSC(id)}");
                dispLocalInfoIfAny(id);
                break;
            }

            case IF_FUNCLETPTR:
            case IF_FUNCLETIDX:
            {
                jitprintf($"funclet {emitGetInsSC(id)}");
                dispLocalInfoIfAny(id);
                break;
            }

            case IF_DATAOFFS:
            {
                jitprintf($"data 0x{unchecked((ulong)emitGetInsSC(id)):x}");
                dispLocalInfoIfAny(id);
                break;
            }

            case IF_F32:
            case IF_F64:
            {
                var value = BitConverter.Int64BitsToDouble(emitGetInsSC(id));
                jitprintf($" {Globals.formatFloat(value, "F6")}");
                break;
            }

            case IF_MEMARG:
            {
                var alignment = emitGetAlignHintLog2(id);
                var immediate = emitGetInsSC(id);
                if (id.idIsCnsReloc())
                {
                    jitprintf($" {alignment} reloc 0x{unchecked((ulong)immediate):x}");
                }
                else
                {
                    jitprintf($" {alignment} {unchecked((ulong)immediate)}");
                }
                dispLocalInfoIfAny(id);
                break;
            }

            case IF_TRY_TABLE:
            {
                var type = emitGetValTypeImmType(id);
                if (type != WasmValueType.Invalid)
                {
                    jitprintf($" {WasmValueTypeName(type)}");
                }
                break;
            }

            case IF_CATCH_DECL:
            {
                jitprintf(" RtlRestoreContextTag");
                dispJumpTargetIfAny(id);
                break;
            }

            case IF_CODE_SIZE:
            {
                var currentGroup = ig ?? emitCurIG;
                assert(currentGroup is not null);
                var func = _compiler.funGetFunc(currentGroup.igFuncIdx);
                if (func.startLoc is { } startLocation)
                {
                    assert(func.endLoc is not null);
                    var endLocation = func.endLoc.Value;
                    var codeSize = endLocation.CodeOffset(this) - startLocation.CodeOffset(this) - PADDED_RELOC_SIZE;
                    jitprintf($" {codeSize}");
                }
                else
                {
                    jitprintf(" <not yet determined>");
                }
                break;
            }

            case IF_V128:
            {
                var immediate = emitGetV128ImmValue(id);
                for (var index = 0; index < immediate.Length; index++)
                {
                    jitprintf($" 0x{immediate[index]:x2}");
                }
                break;
            }

            case IF_LANE:
            {
                jitprintf($" [{emitGetLaneImmValue(id)}]");
                break;
            }

            case IF_MEMARG_LANE:
            {
                var alignment = emitGetAlignHintLog2(id);
                var immediate = unchecked((ulong)emitGetInsSC(id));
                jitprintf($" {alignment} {immediate}");
                dispLocalInfoIfAny(id);
                jitprintf($" [{emitGetLaneImmValue(id)}]");
                break;
            }

            default:
            {
                throw new FatalJitException(CORJIT_INTERNALERROR,
                    $"Unexpected Wasm instruction display format {format}.");
            }
        }

        jitprintf("\n");
    }

    private void dispJumpTargetIfAny(instrDesc id)
    {
        if (_debugInfoSize == 0)
        {
            return;
        }

        var targetBlock = id.idDebugOnlyInfo()?.idTargetBlock;
        if (targetBlock is null)
        {
            return;
        }

        jitprintf("      ;; ");
        var targetGroup = emitCodeGetCookie(targetBlock)
            ?? throw new FatalJitException(CORJIT_INTERNALERROR, "Wasm jump target has no emitter label.");
        emitPrintLabel(targetGroup);
    }

    private void dispHandleIfAny(instrDesc id)
    {
        if (_debugInfoSize == 0)
        {
            return;
        }

        var debugInfo = id.idDebugOnlyInfo();
        assert(debugInfo is not null);
        emitDispCommentForHandle(debugInfo.idMemCookie, 0, debugInfo.idFlags);
    }

    private void dispLocalInfoIfAny(instrDesc id)
    {
        if (_debugInfoSize == 0)
        {
            return;
        }

        var debugInfo = id.idDebugOnlyInfo();
        assert(debugInfo is not null);
        if (debugInfo.idLclNum == BAD_VAR_NUM)
        {
            return;
        }

        jitprintf($"      ;; V{debugInfo.idLclNum:D2}");
        if (debugInfo.idLclOffset != 0)
        {
            jitprintf($"+{debugInfo.idLclOffset}");
        }
    }
}
#endif
