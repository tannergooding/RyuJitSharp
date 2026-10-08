// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;
using static RyuJitSharp.instruction;

#if TARGET_LOONGARCH64
namespace RyuJitSharp;

public partial class Emitter
{
    private unsafe void emitDispInsLoongArch64(instrDesc id, uint offset, insGroup? ig)
    {
        if (ig is null)
        {
            return;
        }

        var address = emitCodeBlock + offset + writeableOffset;
        var remainingSize = unchecked((int)id.idCodeSize());

        while (remainingSize > 0)
        {
            emitDisInsNameLoongArch64(Unsafe.ReadUnaligned<uint>(address), address, id);
            address += 4;
            remainingSize -= 4;
        }
    }

    private void emitDispInstLoongArch64(instruction ins)
    {
        var name = codeGen.genInsName(ins);
        jitprintf(name);
        jitprintf(new string(' ', Math.Max(1, 16 - name.Length)));
    }

    private unsafe void emitDispInsHexLoongArch64(instrDesc id, byte* code, nuint size)
    {
#if DEBUG
        _ = id;
        var compiler = _compiler ?? throw new FatalJitException("LoongArch64 disassembly requires an active compiler.");
        if (!compiler.opts.disCodeBytes)
        {
            return;
        }

        if (!compiler.opts.disDiffable)
        {
            if (size == 4)
            {
                jitprintf($"  {Unsafe.ReadUnaligned<uint>(code):X8}    ");
            }
            else
            {
                assert(size == 0);
                jitprintf("              ");
            }
        }
#endif
    }

    private unsafe void emitDisInsNameLoongArch64(uint code, byte* address, instrDesc id)
    {
        var compiler = _compiler ?? throw new FatalJitException("LoongArch64 disassembly requires an active compiler.");
        var instructionAddress = address - writeableOffset;

#if DEBUG
        emitDispInsAddr(instructionAddress);
        jitprintf("  ");
        if (compiler.opts.disCodeBytes && !compiler.opts.disDiffable)
        {
            jitprintf($"{code:X8}  ");
        }
        else
        {
            jitprintf("          ");
        }
#else
        jitprintf("            ");
#endif

        var destination = (int)(code & 0x1f);
        var source = (int)((code >> 5) & 0x1f);
        var ins = INS_invalid;

        for (uint index = 1; index < (uint)INS_count; index++)
        {
            var candidate = (instruction)index;
            if ((code & emitGetInsMask(candidate)) == emitInsCode(candidate))
            {
                ins = candidate;
                break;
            }
        }

        if (ins == INS_invalid)
        {
            jitprintf($"LOONGARCH illegal instruction: {code:X8}\n");
            return;
        }

        if (id.idInsOpt() == INS_OPTS_NONE)
        {
            assert((ins == id.idIns()) || (emitGetInsFmt(ins) == insDisasmFmt.DF_G_ALIAS));
        }

        emitDispInstLoongArch64(ins);
        var format = emitGetInsFmt(ins);
        var thirdRegister = (int)((code >> 10) & 0x1f);
        var operands = string.Empty;

        switch (format)
        {
            case insDisasmFmt.DF_G_ALIAS:
            {
                if (ins == INS_nop)
                {
                    operands = string.Empty;
                }
                else if (ins is INS_mov or INS_not)
                {
                    operands = $"{RegName(destination)}, {RegName(source)}";
                }
                else if (ins is INS_neg or INS_dneg)
                {
                    operands = $"{RegName(destination)}, {RegName(thirdRegister)}";
                }
                else
                {
                    jitprintf($"LOONGARCH illegal instruction: {code:X8}\n");
                    return;
                }

                break;
            }
            case insDisasmFmt.DF_G_B2:
            {
                var offset = unchecked((short)((code >> 10) & 0xffff)) << 2;
#if DEBUG
                var debugInfo = id.idDebugOnlyInfo();
                if ((debugInfo is not null) && (debugInfo.idMemCookie != 0))
                {
                    assert(debugInfo.idMemCookie > 0);
                    operands = $"{RegName(destination)}, {RegName(source)}, " +
                        $"#{compiler.eeGetMethodFullName((CORINFO_METHOD_HANDLE)debugInfo.idMemCookie)}";
                    break;
                }
#endif
                if (ins == INS_jirl)
                {
                    operands = $"{RegName(destination)}, {RegName(source)}, 0x{offset:x}";
                }
                else if (IsInProlog(address))
                {
                    operands = $"{RegName(source)}, {RegName(destination)}, {FormatInstructionOffset(offset >> 2)}";
                }
                else
                {
                    operands = $"{RegName(source)}, {RegName(destination)}, " +
                        $"0x{unchecked((ulong)((long)(nint)instructionAddress + offset)):x}";
                }

                break;
            }
            case insDisasmFmt.DF_G_B1:
            {
                var encodedOffset = unchecked((int)(((code >> 10) & 0xffff) | ((code & 0x1f) << 16)) << 11);
                var offset = encodedOffset >> 9;
                operands = IsInProlog(address)
                    ? $"{RegName(source)}, {FormatInstructionOffset(offset >> 2)}"
                    : $"{RegName(source)}, 0x{unchecked((ulong)((long)(nint)instructionAddress + offset)):x}";
                break;
            }
            case insDisasmFmt.DF_G_B0:
            {
                var encodedOffset = unchecked((int)(((code >> 10) & 0xffff) | ((code & 0x3ff) << 16)) << 6);
                var offset = encodedOffset >> 4;
#if DEBUG
                var debugInfo = id.idDebugOnlyInfo();
                if ((debugInfo is not null) && (debugInfo.idMemCookie != 0))
                {
                    assert(debugInfo.idMemCookie > 0);
                    operands = $"# {compiler.eeGetMethodFullName((CORINFO_METHOD_HANDLE)debugInfo.idMemCookie)}";
                    break;
                }
#endif
                operands = IsInProlog(address)
                    ? FormatInstructionOffset(offset >> 2)
                    : $"0x{unchecked((ulong)((long)(nint)instructionAddress + offset)):x}";
                break;
            }
            case insDisasmFmt.DF_G_15I:
            {
                operands = $"0x{code & 0x7fff:x}";
                break;
            }
            case insDisasmFmt.DF_G_R20I:
            {
                operands = $"{RegName(destination)}, 0x{(code >> 5) & 0xfffff:x}";
                break;
            }
            case insDisasmFmt.DF_G_2R:
            {
                operands = $"{RegName(destination)}, {RegName(source)}";
                break;
            }
            case insDisasmFmt.DF_G_2R5IU:
            {
                operands = $"{RegName(destination)}, {RegName(source)}, {(code >> 10) & 0x1f}";
                break;
            }
            case insDisasmFmt.DF_G_2R6IU:
            {
                operands = $"{RegName(destination)}, {RegName(source)}, {(code >> 10) & 0x3f}";
                break;
            }
            case insDisasmFmt.DF_G_2R5IW:
            {
                operands = $"{RegName(destination)}, {RegName(source)}, {(code >> 16) & 0x1f}, {(code >> 10) & 0x1f}";
                break;
            }
            case insDisasmFmt.DF_G_2R6ID:
            {
                operands = $"{RegName(destination)}, {RegName(source)}, {(code >> 16) & 0x3f}, {(code >> 10) & 0x3f}";
                break;
            }
            case insDisasmFmt.DF_G_2R12I:
            {
                var immediate = unchecked((int)((code >> 10) & 0xfff) << 20) >> 20;
                operands = ins switch
                {
                    INS_preld => $"0x{destination:x}, {RegName(source)}, {immediate}",
                    INS_lu52i_d => $"{RegName(destination)}, {RegName(source)}, 0x{immediate & 0xfff:x}",
                    _ => $"{RegName(destination)}, {RegName(source)}, {immediate}",
                };
                break;
            }
            case insDisasmFmt.DF_G_2R12IU:
            {
                operands = $"{RegName(destination)}, {RegName(source)}, 0x{(code >> 10) & 0xfff:x}";
                break;
            }
            case insDisasmFmt.DF_G_2R14I:
            {
                var immediate = unchecked((int)((code >> 10) & 0x3fff) << 18) >> 16;
                operands = $"{RegName(destination)}, {RegName(source)}, 0x{immediate:x}";
                break;
            }
            case insDisasmFmt.DF_G_2R16I:
            {
                operands = $"{RegName(destination)}, {RegName(source)}, 0x{(code >> 10) & 0xffff:x}";
                break;
            }
            case insDisasmFmt.DF_G_3R:
            {
                operands = ins == INS_preldx
                    ? $"0x{destination:x}, {RegName(source)}, {RegName(thirdRegister)}"
                    : $"{RegName(destination)}, {RegName(source)}, {RegName(thirdRegister)}";
                break;
            }
            case insDisasmFmt.DF_G_3R2IU:
            {
                operands = $"{RegName(destination)}, {RegName(source)}, {RegName(thirdRegister)}, {((code >> 15) & 0x3) + 1}";
                break;
            }
            case insDisasmFmt.DF_G_3RX:
            {
                var mask = ins == INS_bytepick_d ? 0x7u : 0x3u;
                operands = $"{RegName(destination)}, {RegName(source)}, {RegName(thirdRegister)}, {(code >> 15) & mask}";
                break;
            }
            case insDisasmFmt.DF_F_B1:
            {
                var encodedOffset = unchecked((int)(((code >> 10) & 0xffff) | ((code & 0x1f) << 16)) << 11);
                var offset = encodedOffset >> 9;
                operands = $"fcc{(code >> 5) & 0x7}, 0x{unchecked((ulong)((long)(nint)instructionAddress + offset)):x}";
                break;
            }
            case insDisasmFmt.DF_F_GR:
            {
                operands = $"{RegName(destination)}, {RegName(source + 32)}";
                break;
            }
            case insDisasmFmt.DF_F_RG:
            {
                operands = $"{RegName(destination + 32)}, {RegName(source)}";
                break;
            }
            case insDisasmFmt.DF_F_RG12I:
            {
                var immediate = unchecked((int)(code << 10)) >> 20;
                operands = $"{RegName(destination + 32)}, {RegName(source)}, {immediate}";
                break;
            }
            case insDisasmFmt.DF_F_FG:
            {
                operands = $"fcsr{destination}, {RegName(source)}";
                break;
            }
            case insDisasmFmt.DF_F_GF:
            {
                operands = $"{RegName(destination)}, fcsr{source}";
                break;
            }
            case insDisasmFmt.DF_F_CR:
            {
                operands = $"fcc{destination & 0x7}, {RegName(source + 32)}";
                break;
            }
            case insDisasmFmt.DF_F_RC:
            {
                operands = $"{RegName(destination + 32)}, fcc{source & 0x7}";
                break;
            }
            case insDisasmFmt.DF_F_CG:
            {
                operands = $"fcc{destination & 0x7}, {RegName(source)}";
                break;
            }
            case insDisasmFmt.DF_F_GC:
            {
                operands = $"{RegName(destination)}, fcc{source & 0x7}";
                break;
            }
            case insDisasmFmt.DF_F_C2R:
            {
                operands = $"fcc{(code >> 10) & 0x7}, {RegName(destination + 32)}, {RegName(source + 32)}";
                break;
            }
            case insDisasmFmt.DF_F_2R:
            {
                operands = $"{RegName(destination + 32)}, {RegName(source + 32)}";
                break;
            }
            case insDisasmFmt.DF_F_R2G:
            {
                operands = $"{RegName(destination + 32)}, {RegName(source)}, {RegName(thirdRegister)}";
                break;
            }
            case insDisasmFmt.DF_F_3R:
            {
                operands = $"{RegName(destination + 32)}, {RegName(source + 32)}, {RegName(thirdRegister + 32)}";
                break;
            }
            case insDisasmFmt.DF_F_4R:
            {
                var fourthRegister = (int)((code >> 15) & 0x1f);
                operands = $"{RegName(destination + 32)}, {RegName(source + 32)}, {RegName(thirdRegister + 32)}, {RegName(fourthRegister + 32)}";
                break;
            }
            case insDisasmFmt.DF_F_3RX3:
            {
                operands = $"{RegName(destination + 32)}, {RegName(source + 32)}, {RegName(thirdRegister + 32)}, fcc{(code >> 15) & 0x7}";
                break;
            }
#if FEATURE_SIMD
            case insDisasmFmt.DF_S_4R:
            {
                operands = $"vr{destination}, vr{source}, vr{thirdRegister}, vr{(code >> 15) & 0x1f}";
                break;
            }
            case insDisasmFmt.DF_S_3R:
            {
                operands = $"vr{destination}, vr{source}, vr{thirdRegister}";
                break;
            }
            case insDisasmFmt.DF_S_RG:
            {
                operands = $"vr{destination}, {RegName(source)}";
                break;
            }
            case insDisasmFmt.DF_S_2R:
            {
                operands = $"vr{destination}, vr{source}";
                break;
            }
            case insDisasmFmt.DF_S_CR:
            {
                operands = $"fcc{destination}, vr{source}";
                break;
            }
            case insDisasmFmt.DF_S_RGX:
            {
                var immediate = (int)(code >> 10);
                if (ins == INS_vldrepl_h)
                {
                    immediate = unchecked((int)((uint)(immediate & 0x7ff) << 21)) >> 20;
                }
                else if (ins == INS_vldrepl_w)
                {
                    immediate = unchecked((int)((uint)(immediate & 0x3ff) << 22)) >> 20;
                }
                else if (ins == INS_vldrepl_d)
                {
                    immediate = unchecked((int)((uint)(immediate & 0x1ff) << 23)) >> 20;
                }
                else if (ins == INS_vinsgr2vr_b)
                {
                    immediate &= 0xf;
                }
                else if (ins == INS_vinsgr2vr_h)
                {
                    immediate &= 0x7;
                }
                else if (ins == INS_vinsgr2vr_w)
                {
                    immediate &= 0x3;
                }
                else if (ins == INS_vinsgr2vr_d)
                {
                    immediate &= 0x1;
                }
                else
                {
                    immediate = unchecked((int)((uint)(immediate & 0xfff) << 20)) >> 20;
                }

                operands = $"vr{destination}, {RegName(source)}, 0x{immediate:x}";
                break;
            }
            case insDisasmFmt.DF_S_GRX:
            {
                var immediate = (int)((code >> 10) & 0xf);
                if (ins < INS_vpickve2gr_w)
                {
                    immediate &= 1;
                }
                else if (ins < INS_vpickve2gr_h)
                {
                    immediate &= 3;
                }
                else if (ins < INS_vpickve2gr_b)
                {
                    immediate &= 7;
                }

                operands = $"{RegName(destination)}, vr{source}, {immediate}";
                break;
            }
            case insDisasmFmt.DF_S_2RG:
            {
                operands = $"vr{destination}, vr{source}, {RegName(thirdRegister)}";
                break;
            }
            case insDisasmFmt.DF_S_2R3IU:
            {
                operands = $"vr{destination}, vr{source}, {(code >> 10) & 0x7}";
                break;
            }
            case insDisasmFmt.DF_S_2R4IU:
            {
                operands = $"vr{destination}, vr{source}, {(code >> 10) & 0xf}";
                break;
            }
            case insDisasmFmt.DF_S_2R5I:
            {
                var immediate = unchecked((int)((code >> 10) & 0x1f) << 27) >> 27;
                operands = $"vr{destination}, vr{source}, {immediate}";
                break;
            }
            case insDisasmFmt.DF_S_2R5IU:
            {
                operands = $"vr{destination}, vr{source}, {(code >> 10) & 0x1f}";
                break;
            }
            case insDisasmFmt.DF_S_2R6IU:
            {
                operands = $"vr{destination}, vr{source}, {(code >> 10) & 0x3f}";
                break;
            }
            case insDisasmFmt.DF_S_2R7IU:
            {
                operands = $"vr{destination}, vr{source}, {(code >> 10) & 0x7f}";
                break;
            }
            case insDisasmFmt.DF_S_2R8IU:
            {
                operands = $"vr{destination}, vr{source}, 0x{(code >> 10) & 0xff:x}";
                break;
            }
            case insDisasmFmt.DF_S_2R8IX:
            {
                var immediate = (int)(((code >> 10) & 0xff) << 24) >> 24;
                var index = (int)(code >> 18);
                switch (ins - INS_vstelm_b)
                {
                    case 0:
                    {
                        index &= 0xf;
                        break;
                    }
                    case 1:
                    {
                        immediate <<= 1;
                        index &= 0x7;
                        break;
                    }
                    case 2:
                    {
                        immediate <<= 2;
                        index &= 0x3;
                        break;
                    }
                    case 3:
                    {
                        immediate <<= 3;
                        index &= 0x1;
                        break;
                    }
                    default:
                    {
                        jitprintf($"LOONGARCH illegal instruction: {code:X8}\n");
                        return;
                    }
                }

                operands = $"vr{destination}, {RegName(source)}, {immediate}, {index}";
                break;
            }
            case insDisasmFmt.DF_S_R13IU:
            {
                operands = $"vr{destination}, 0x{(code >> 5) & 0x1fff:x}";
                break;
            }
            case insDisasmFmt.DF_S_2RX:
            {
                var mask = ins == INS_vreplvei_d ? 1u : 3u;
                operands = $"vr{destination}, vr{source}, {(code >> 10) & mask}";
                break;
            }
            case insDisasmFmt.DF_A_4R:
            {
                operands = $"xr{destination}, xr{source}, xr{thirdRegister}, xr{(code >> 15) & 0x1f}";
                break;
            }
            case insDisasmFmt.DF_A_3R:
            {
                operands = $"xr{destination}, xr{source}, xr{thirdRegister}";
                break;
            }
            case insDisasmFmt.DF_A_RG:
            {
                operands = $"xr{destination}, {RegName(source)}";
                break;
            }
            case insDisasmFmt.DF_A_2R:
            {
                operands = $"xr{destination}, xr{source}";
                break;
            }
            case insDisasmFmt.DF_A_CR:
            {
                operands = $"fcc{destination}, xr{source}";
                break;
            }
            case insDisasmFmt.DF_A_RGX:
            {
                var immediate = (int)(code >> 10);
                if (ins == INS_xvldrepl_h)
                {
                    immediate = unchecked((int)((uint)(immediate & 0x7ff) << 21)) >> 20;
                }

                if (ins == INS_xvldrepl_w)
                {
                    immediate = unchecked((int)((uint)(immediate & 0x3ff) << 22)) >> 20;
                }
                else if (ins == INS_xvldrepl_d)
                {
                    immediate = unchecked((int)((uint)(immediate & 0x1ff) << 23)) >> 20;
                }
                else if (ins == INS_xvinsgr2vr_w)
                {
                    immediate &= 0x7;
                }
                else if (ins == INS_xvinsgr2vr_d)
                {
                    immediate &= 0x3;
                }
                else
                {
                    immediate = unchecked((int)((uint)(immediate & 0xfff) << 20)) >> 20;
                }

                operands = $"xr{destination}, {RegName(source)}, {immediate}";
                break;
            }
            case insDisasmFmt.DF_A_GRX:
            {
                var mask = ins < INS_xvpickve2gr_w ? 3u : 7u;
                operands = $"{RegName(destination)}, xr{source}, {(code >> 10) & mask}";
                break;
            }
            case insDisasmFmt.DF_A_2RG:
            {
                operands = $"xr{destination}, xr{source}, {RegName(thirdRegister)}";
                break;
            }
            case insDisasmFmt.DF_A_2RX:
            {
                var mask = ins == INS_xvrepl128vei_d ? 1u : 3u;
                operands = $"xr{destination}, xr{source}, {(code >> 10) & mask}";
                break;
            }
            case insDisasmFmt.DF_A_2R3IU:
            {
                operands = $"xr{destination}, xr{source}, {(code >> 10) & 0x7}";
                break;
            }
            case insDisasmFmt.DF_A_2R4IU:
            {
                operands = $"xr{destination}, xr{source}, {(code >> 10) & 0xf}";
                break;
            }
            case insDisasmFmt.DF_A_2R5I:
            {
                var immediate = unchecked((int)((code >> 10) & 0x1f) << 27) >> 27;
                operands = $"xr{destination}, xr{source}, {immediate}";
                break;
            }
            case insDisasmFmt.DF_A_2R5IU:
            {
                operands = $"xr{destination}, xr{source}, {(code >> 10) & 0x1f}";
                break;
            }
            case insDisasmFmt.DF_A_2R6IU:
            {
                operands = $"xr{destination}, xr{source}, {(code >> 10) & 0x3f}";
                break;
            }
            case insDisasmFmt.DF_A_2R7IU:
            {
                operands = $"xr{destination}, xr{source}, {(code >> 10) & 0x7f}";
                break;
            }
            case insDisasmFmt.DF_A_2R8IU:
            {
                operands = $"xr{destination}, xr{source}, {(code >> 10) & 0xff}";
                break;
            }
            case insDisasmFmt.DF_A_R13IU:
            {
                operands = $"xr{destination}, xr{source}, 0x{(code >> 10) & 0x1fff:x}";
                break;
            }
            case insDisasmFmt.DF_A_RG8IX:
            {
                var immediate = (int)(((code >> 10) & 0xff) << 24) >> 24;
                var index = (int)(code >> 18);
                switch (ins - INS_xvstelm_b)
                {
                    case 0:
                    {
                        index &= 0xf;
                        break;
                    }
                    case 1:
                    {
                        immediate <<= 1;
                        index &= 0x7;
                        break;
                    }
                    case 2:
                    {
                        immediate <<= 2;
                        index &= 0x3;
                        break;
                    }
                    case 3:
                    {
                        immediate <<= 3;
                        index &= 0x1;
                        break;
                    }
                    default:
                    {
                        jitprintf($"LOONGARCH illegal instruction: {code:X8}\n");
                        return;
                    }
                }

                operands = $"xr{destination}, {RegName(source)}, {immediate}, {index}";
                break;
            }
#endif
            default:
            {
                jitprintf($"LOONGARCH illegal instruction: {code:X8}\n");
                return;
            }
        }

        jitprintf(operands);
        jitprintf("\n");

        string RegName(int register)
        {
            assert((uint)register < (uint)REG_COUNT);
            return ((regNumber)register).Name;
        }

        bool IsInProlog(byte* instructionPointer)
        {
            return emitPrologEndPos.Valid() &&
                ((uint)(instructionPointer - emitCodeBlock) < emitPrologEndPos.CodeOffset(this));
        }
    }

    private string FormatInstructionOffset(int offset)
    {
        var prefix = offset < 0 ? "-" : "+";
        return $"{prefix}{Math.Abs(offset)} ins";
    }
}
#endif
