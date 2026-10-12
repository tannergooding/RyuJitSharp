// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

public partial class Compiler
{
#if FEATURE_CFI_SUPPORT
    public short mapRegNumToDwarfReg(regNumber reg)
    {
        short dwarfReg = reg switch
        {
            REG_R0_ARM64 => 0,
            REG_R1_ARM64 => 1,
            REG_R2_ARM64 => 2,
            REG_R3_ARM64 => 3,
            REG_R4_ARM64 => 4,
            REG_R5_ARM64 => 5,
            REG_R6_ARM64 => 6,
            REG_R7_ARM64 => 7,
            REG_R8_ARM64 => 8,
            REG_R9_ARM64 => 9,
            REG_R10_ARM64 => 10,
            REG_R11_ARM64 => 11,
            REG_R12_ARM64 => 12,
            REG_R13_ARM64 => 13,
            REG_R14_ARM64 => 14,
            REG_R15_ARM64 => 15,
            REG_R16_ARM64 => 16,
            REG_R17_ARM64 => 17,
            REG_R18_ARM64 => 18,
            REG_R19_ARM64 => 19,
            REG_R20_ARM64 => 20,
            REG_R21_ARM64 => 21,
            REG_R22_ARM64 => 22,
            REG_R23_ARM64 => 23,
            REG_R24_ARM64 => 24,
            REG_R25_ARM64 => 25,
            REG_R26_ARM64 => 26,
            REG_R27_ARM64 => 27,
            REG_R28_ARM64 => 28,
            REG_R29_ARM64 => 29,
            REG_R30_ARM64 => 30,
            REG_SP_ARM64 => 31,
            REG_V0_ARM64 => 64,
            REG_V1_ARM64 => 65,
            REG_V2_ARM64 => 66,
            REG_V3_ARM64 => 67,
            REG_V4_ARM64 => 68,
            REG_V5_ARM64 => 69,
            REG_V6_ARM64 => 70,
            REG_V7_ARM64 => 71,
            REG_V8_ARM64 => 72,
            REG_V9_ARM64 => 73,
            REG_V10_ARM64 => 74,
            REG_V11_ARM64 => 75,
            REG_V12_ARM64 => 76,
            REG_V13_ARM64 => 77,
            REG_V14_ARM64 => 78,
            REG_V15_ARM64 => 79,
            REG_V16_ARM64 => 80,
            REG_V17_ARM64 => 81,
            REG_V18_ARM64 => 82,
            REG_V19_ARM64 => 83,
            REG_V20_ARM64 => 84,
            REG_V21_ARM64 => 85,
            REG_V22_ARM64 => 86,
            REG_V23_ARM64 => 87,
            REG_V24_ARM64 => 88,
            REG_V25_ARM64 => 89,
            REG_V26_ARM64 => 90,
            REG_V27_ARM64 => 91,
            REG_V28_ARM64 => 92,
            REG_V29_ARM64 => 93,
            REG_V30_ARM64 => 94,
            REG_V31_ARM64 => 95,
            _ => DWARF_REG_ILLEGAL,
        };

        if (dwarfReg == DWARF_REG_ILLEGAL)
        {
            NYI("CFI codes");
        }

        return dwarfReg;
    }
#endif

    public void unwindPush(regNumber reg)
    {
        unreached(); // Use one of the unwindSaveReg* functions instead.
    }

    public void unwindAllocStack(uint size)
    {
#if FEATURE_CFI_SUPPORT
        if (generateCFIUnwindCodes())
        {
            if (UnwindEmitter().emitGeneratingPrologOrFuncletProlog())
            {
                unwindAllocStackCFI(size);
            }

            return;
        }
#endif

        var pu = funCurrentFunc().GetUnwindInfo();
        assert(size % 16 == 0);
        var x = size / 16;

        if (x <= 0x1F)
        {
            // alloc_s: 000xxxxx. The encoded size is in units of 16 bytes.
            pu.AddCode(unchecked((byte)x));
        }
        else if (x <= 0x7FF)
        {
            // alloc_m: 11000xxx | xxxxxxxx.
            pu.AddCode(unchecked((byte)(0xC0 | (byte)(x >> 8))), unchecked((byte)x));
        }
        else
        {
            // alloc_l: 11100000 | xxxxxxxx | xxxxxxxx | xxxxxxxx.
            // The unwind specification stores the most significant size bits first.
            pu.AddCode(0xE0, unchecked((byte)(x >> 16)), unchecked((byte)(x >> 8)), unchecked((byte)x));
        }
    }

    public void unwindSetFrameReg(regNumber reg, uint offset)
    {
#if FEATURE_CFI_SUPPORT
        if (generateCFIUnwindCodes())
        {
            if (UnwindEmitter().emitGeneratingPrologOrFuncletProlog())
            {
                unwindSetFrameRegCFI(reg, offset);
            }

            return;
        }
#endif

        var pu = funCurrentFunc().GetUnwindInfo();
        if (offset == 0)
        {
            assert(reg == REG_FP_ARM64);
            // set_fp: 11100001, mov r29, sp.
            pu.AddCode(0xE1);
        }
        else
        {
            // add_fp: 11100010 | xxxxxxxx, add r29, sp, #x * 8.
            assert(reg == REG_FP_ARM64);
            assert((offset % 8) == 0);
            var x = offset / 8;
            assert(x <= 0xFF);

            pu.AddCode(0xE2, unchecked((byte)x));
        }
    }

    public void unwindSaveReg(regNumber reg, uint offset)
    {
        unreached();
    }

    public void unwindNop()
    {
        var pu = funCurrentFunc().GetUnwindInfo();
#if DEBUG
        if (verbose)
        {
            logf("unwindNop: adding NOP\n");
        }

        pu.uwiAddingNOP = true;
#endif
        // nop: 11100011, no unwind operation is required.
        pu.AddCode(0xE3);
#if DEBUG
        pu.uwiAddingNOP = false;
#endif
    }

    // The second register must immediately follow the first, except for pairs ending in LR.
    // Those pairs may start with FP or an odd register from R19 through R27.
    public void unwindSaveRegPair(regNumber reg1, regNumber reg2, int offset)
    {
        assert(0 <= offset && offset <= 504);
        assert((offset % 8) == 0);
#if FEATURE_CFI_SUPPORT
        if (generateCFIUnwindCodes())
        {
            if (UnwindEmitter().emitGeneratingPrologOrFuncletProlog())
            {
                ref var func = ref funCurrentFunc();
                var cbProlog = unwindGetCurrentOffset(in func);

                createCfiCode(in func, cbProlog, CFI_REL_OFFSET, mapRegNumToDwarfReg(reg1), offset);
                createCfiCode(in func, cbProlog, CFI_REL_OFFSET, mapRegNumToDwarfReg(reg2), offset + 8);
            }

            return;
        }
#endif

        var pu = funCurrentFunc().GetUnwindInfo();
        var z = offset / 8;
        assert(0 <= z && z <= 0x3F);

        if (reg1 == REG_FP_ARM64)
        {
            // save_fplr: 01zzzzzz.
            assert(reg2 == REG_LR_ARM64);
            pu.AddCode(unchecked((byte)(0x40 | (byte)z)));
        }
        else if (reg2 == REG_LR_ARM64)
        {
            // save_lrpair: 1101011x | xxzzzzzz.
            assert(REG_R19_ARM64 <= reg1 && reg1 <= REG_R27_ARM64);
            var x = unchecked((byte)(reg1 - REG_R19_ARM64));
            assert((x % 2) == 0);
            x /= 2;
            assert(0 <= x && x <= 0x7);

            pu.AddCode(unchecked((byte)(0xD6 | (byte)(x >> 2))), unchecked((byte)((byte)(x << 6) | (byte)z)));
        }
        else if ((reg1 >= REG_INT_FIRST) && (reg1 <= REG_LR_ARM64))
        {
            // save_regp: 110010xx | xxzzzzzz.
            assert(reg1 + 1 == reg2);
            assert(REG_R19_ARM64 <= reg1 && reg1 <= REG_R27_ARM64);
            var x = unchecked((byte)(reg1 - REG_R19_ARM64));
            assert(0 <= x && x <= 0xF);

            pu.AddCode(unchecked((byte)(0xC8 | (byte)(x >> 2))), unchecked((byte)((byte)(x << 6) | (byte)z)));
        }
        else
        {
            // save_fregp: 1101100x | xxzzzzzz.
            assert(reg1 + 1 == reg2);
            assert(REG_V8_ARM64 <= reg1 && reg1 <= REG_V14_ARM64);
            var x = unchecked((byte)(reg1 - REG_V8_ARM64));
            assert(0 <= x && x <= 0x7);

            pu.AddCode(unchecked((byte)(0xD8 | (byte)(x >> 2))), unchecked((byte)((byte)(x << 6) | (byte)z)));
        }
    }

    public void unwindSaveRegPairPreindexed(regNumber reg1, regNumber reg2, int offset)
    {
        assert(offset < 0);
        assert((offset % 8) == 0);
#if FEATURE_CFI_SUPPORT
        if (generateCFIUnwindCodes())
        {
            if (UnwindEmitter().emitGeneratingPrologOrFuncletProlog())
            {
                ref var func = ref funCurrentFunc();
                var cbProlog = unwindGetCurrentOffset(in func);

                createCfiCode(in func, cbProlog, CFI_ADJUST_CFA_OFFSET, DWARF_REG_ILLEGAL, unchecked(-offset));
                createCfiCode(in func, cbProlog, CFI_REL_OFFSET, mapRegNumToDwarfReg(reg1), 0);
                createCfiCode(in func, cbProlog, CFI_REL_OFFSET, mapRegNumToDwarfReg(reg2), 8);
            }

            return;
        }
#endif

        var pu = funCurrentFunc().GetUnwindInfo();
        if (reg1 == REG_FP_ARM64)
        {
            // save_fplr_x: 10zzzzzz.
            assert(-512 <= offset);
            var z = unchecked((-offset) / 8 - 1);
            assert(0 <= z && z <= 0x3F);
            assert(reg2 == REG_LR_ARM64);

            pu.AddCode(unchecked((byte)(0x80 | (byte)z)));
        }
        else if ((reg1 == REG_R19_ARM64) && (-256 <= offset))
        {
            // save_r19r20_x: 001zzzzz. Unlike other pre-indexed codes, Z is not biased.
            // Upstream includes -256 in this branch even though the asserted Z limit is 31.
            var z = unchecked(-offset) / 8;
            assert(0 <= z && z <= 0x1F);
            assert(reg2 == REG_R20_ARM64);

            pu.AddCode(unchecked((byte)(0x20 | (byte)z)));
        }
        else if ((reg1 >= REG_INT_FIRST) && (reg1 <= REG_LR_ARM64))
        {
            // save_regp_x: 110011xx | xxzzzzzz.
            assert(-512 <= offset);
            var z = unchecked((-offset) / 8 - 1);
            assert(0 <= z && z <= 0x3F);
            assert(reg1 + 1 == reg2);
            assert(REG_R19_ARM64 <= reg1 && reg1 <= REG_R27_ARM64);
            var x = unchecked((byte)(reg1 - REG_R19_ARM64));
            assert(0 <= x && x <= 0xF);

            pu.AddCode(unchecked((byte)(0xCC | (byte)(x >> 2))), unchecked((byte)((byte)(x << 6) | (byte)z)));
        }
        else
        {
            // save_fregp_x: 1101101x | xxzzzzzz.
            assert(-512 <= offset);
            var z = unchecked((-offset) / 8 - 1);
            assert(0 <= z && z <= 0x3F);
            assert(reg1 + 1 == reg2);
            assert(REG_V8_ARM64 <= reg1 && reg1 <= REG_V14_ARM64);
            var x = unchecked((byte)(reg1 - REG_V8_ARM64));
            assert(0 <= x && x <= 0x7);

            pu.AddCode(unchecked((byte)(0xDA | (byte)(x >> 2))), unchecked((byte)((byte)(x << 6) | (byte)z)));
        }
    }

    public void unwindSaveReg(regNumber reg, int offset)
    {
        assert(0 <= offset && offset <= 504);
        assert((offset % 8) == 0);
#if FEATURE_CFI_SUPPORT
        if (generateCFIUnwindCodes())
        {
            if (UnwindEmitter().emitGeneratingPrologOrFuncletProlog())
            {
                ref var func = ref funCurrentFunc();
                var cbProlog = unwindGetCurrentOffset(in func);

                createCfiCode(in func, cbProlog, CFI_REL_OFFSET, mapRegNumToDwarfReg(reg), offset);
            }

            return;
        }
#endif

        var z = offset / 8;
        assert(0 <= z && z <= 0x3F);
        var pu = funCurrentFunc().GetUnwindInfo();

        if ((reg >= REG_INT_FIRST) && (reg <= REG_LR_ARM64))
        {
            // save_reg: 110100xx | xxzzzzzz.
            assert(REG_R19_ARM64 <= reg && reg <= REG_LR_ARM64);
            var x = unchecked((byte)(reg - REG_R19_ARM64));
            assert(0 <= x && x <= 0xF);

            pu.AddCode(unchecked((byte)(0xD0 | (byte)(x >> 2))), unchecked((byte)((byte)(x << 6) | (byte)z)));
        }
        else
        {
            // save_freg: 1101110x | xxzzzzzz.
            assert(REG_V8_ARM64 <= reg && reg <= REG_V15_ARM64);
            var x = unchecked((byte)(reg - REG_V8_ARM64));
            assert(0 <= x && x <= 0x7);

            pu.AddCode(unchecked((byte)(0xDC | (byte)(x >> 2))), unchecked((byte)((byte)(x << 6) | (byte)z)));
        }
    }

    public void unwindSaveRegPreindexed(regNumber reg, int offset)
    {
        assert(-256 <= offset && offset < 0);
        assert((offset % 8) == 0);
#if FEATURE_CFI_SUPPORT
        if (generateCFIUnwindCodes())
        {
            if (UnwindEmitter().emitGeneratingPrologOrFuncletProlog())
            {
                ref var func = ref funCurrentFunc();
                var cbProlog = unwindGetCurrentOffset(in func);

                createCfiCode(in func, cbProlog, CFI_ADJUST_CFA_OFFSET, DWARF_REG_ILLEGAL, unchecked(-offset));
                createCfiCode(in func, cbProlog, CFI_REL_OFFSET, mapRegNumToDwarfReg(reg), 0);
            }

            return;
        }
#endif

        var pu = funCurrentFunc().GetUnwindInfo();
        var z = unchecked((-offset) / 8 - 1);
        assert(0 <= z && z <= 0x1F);

        if ((reg >= REG_INT_FIRST) && (reg <= REG_LR_ARM64))
        {
            // save_reg_x: 1101010x | xxxzzzzz.
            assert(REG_R19_ARM64 <= reg && reg <= REG_LR_ARM64);
            var x = unchecked((byte)(reg - REG_R19_ARM64));
            assert(0 <= x && x <= 0xF);

            pu.AddCode(unchecked((byte)(0xD4 | (byte)(x >> 3))), unchecked((byte)((byte)(x << 5) | (byte)z)));
        }
        else
        {
            // save_freg_x: 11011110 | xxxzzzzz.
            assert(REG_V8_ARM64 <= reg && reg <= REG_V15_ARM64);
            var x = unchecked((byte)(reg - REG_V8_ARM64));
            assert(0 <= x && x <= 0x7);

            pu.AddCode(0xDE, unchecked((byte)((byte)(x << 5) | (byte)z)));
        }
    }

    public void unwindSaveNext()
    {
#if FEATURE_CFI_SUPPORT
        // CFI has no save-next opcode.
        assert(!generateCFIUnwindCodes());
#endif
        var pu = funCurrentFunc().GetUnwindInfo();
        // The caller is responsible for ensuring that the next register pair is correct.
        pu.AddCode(0xE6);
    }

    public void unwindPacSignLR()
    {
        if (JitConfig.JitPacEnabled == 0)
        {
            return;
        }
#if FEATURE_CFI_SUPPORT
        if (generateCFIUnwindCodes())
        {
            if (!UnwindEmitter().emitGeneratingPrologOrFuncletProlog())
            {
                return;
            }

            ref var func = ref funCurrentFunc();
            var cbProlog = unwindGetCurrentOffset(in func);
            assert(func.cfiCodes is not null && func.cfiCodes.Count == 0);
            // Maps to DW_CFA_AARCH64_negate_ra_state.
            createCfiCode(in func, cbProlog, CFI_NEGATE_RA_STATE, DWARF_REG_ILLEGAL);

            return;
        }
#endif
        // pac_sign_lr: 11111100, sign LR with the platform PAC key.
        funCurrentFunc().GetUnwindInfo().AddCode(0xFC);
    }

    public void unwindReturn(regNumber reg)
    {
        // Padding always contains at least one trailing "end" opcode.
    }
}
#endif
