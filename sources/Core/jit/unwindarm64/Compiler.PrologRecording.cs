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
            REG_R0 => 0,
            REG_R1 => 1,
            REG_R2 => 2,
            REG_R3 => 3,
            REG_R4 => 4,
            REG_R5 => 5,
            REG_R6 => 6,
            REG_R7 => 7,
            REG_R8 => 8,
            REG_R9 => 9,
            REG_R10 => 10,
            REG_R11 => 11,
            REG_R12 => 12,
            REG_R13 => 13,
            REG_R14 => 14,
            REG_R15 => 15,
            REG_R16 => 16,
            REG_R17 => 17,
            REG_R18 => 18,
            REG_R19 => 19,
            REG_R20 => 20,
            REG_R21 => 21,
            REG_R22 => 22,
            REG_R23 => 23,
            REG_R24 => 24,
            REG_R25 => 25,
            REG_R26 => 26,
            REG_R27 => 27,
            REG_R28 => 28,
            REG_R29 => 29,
            REG_R30 => 30,
            REG_SP => 31,
            REG_V0 => 64,
            REG_V1 => 65,
            REG_V2 => 66,
            REG_V3 => 67,
            REG_V4 => 68,
            REG_V5 => 69,
            REG_V6 => 70,
            REG_V7 => 71,
            REG_V8 => 72,
            REG_V9 => 73,
            REG_V10 => 74,
            REG_V11 => 75,
            REG_V12 => 76,
            REG_V13 => 77,
            REG_V14 => 78,
            REG_V15 => 79,
            REG_V16 => 80,
            REG_V17 => 81,
            REG_V18 => 82,
            REG_V19 => 83,
            REG_V20 => 84,
            REG_V21 => 85,
            REG_V22 => 86,
            REG_V23 => 87,
            REG_V24 => 88,
            REG_V25 => 89,
            REG_V26 => 90,
            REG_V27 => 91,
            REG_V28 => 92,
            REG_V29 => 93,
            REG_V30 => 94,
            REG_V31 => 95,
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
            assert(reg == REG_FP);
            // set_fp: 11100001, mov r29, sp.
            pu.AddCode(0xE1);
        }
        else
        {
            // add_fp: 11100010 | xxxxxxxx, add r29, sp, #x * 8.
            assert(reg == REG_FP);
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

        if (reg1 == REG_FP)
        {
            // save_fplr: 01zzzzzz.
            assert(reg2 == REG_LR);
            pu.AddCode(unchecked((byte)(0x40 | (byte)z)));
        }
        else if (reg2 == REG_LR)
        {
            // save_lrpair: 1101011x | xxzzzzzz.
            assert(REG_R19 <= reg1 && reg1 <= REG_R27);
            var x = unchecked((byte)(reg1 - REG_R19));
            assert((x % 2) == 0);
            x /= 2;
            assert(0 <= x && x <= 0x7);

            pu.AddCode(unchecked((byte)(0xD6 | (byte)(x >> 2))), unchecked((byte)((byte)(x << 6) | (byte)z)));
        }
        else if ((reg1 >= REG_INT_FIRST) && (reg1 <= REG_LR))
        {
            // save_regp: 110010xx | xxzzzzzz.
            assert(reg1 + 1 == reg2);
            assert(REG_R19 <= reg1 && reg1 <= REG_R27);
            var x = unchecked((byte)(reg1 - REG_R19));
            assert(0 <= x && x <= 0xF);

            pu.AddCode(unchecked((byte)(0xC8 | (byte)(x >> 2))), unchecked((byte)((byte)(x << 6) | (byte)z)));
        }
        else
        {
            // save_fregp: 1101100x | xxzzzzzz.
            assert(reg1 + 1 == reg2);
            assert(REG_V8 <= reg1 && reg1 <= REG_V14);
            var x = unchecked((byte)(reg1 - REG_V8));
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
        if (reg1 == REG_FP)
        {
            // save_fplr_x: 10zzzzzz.
            assert(-512 <= offset);
            var z = unchecked((-offset) / 8 - 1);
            assert(0 <= z && z <= 0x3F);
            assert(reg2 == REG_LR);

            pu.AddCode(unchecked((byte)(0x80 | (byte)z)));
        }
        else if ((reg1 == REG_R19) && (-256 <= offset))
        {
            // save_r19r20_x: 001zzzzz. Unlike other pre-indexed codes, Z is not biased.
            // Upstream includes -256 in this branch even though the asserted Z limit is 31.
            var z = unchecked(-offset) / 8;
            assert(0 <= z && z <= 0x1F);
            assert(reg2 == REG_R20);

            pu.AddCode(unchecked((byte)(0x20 | (byte)z)));
        }
        else if ((reg1 >= REG_INT_FIRST) && (reg1 <= REG_LR))
        {
            // save_regp_x: 110011xx | xxzzzzzz.
            assert(-512 <= offset);
            var z = unchecked((-offset) / 8 - 1);
            assert(0 <= z && z <= 0x3F);
            assert(reg1 + 1 == reg2);
            assert(REG_R19 <= reg1 && reg1 <= REG_R27);
            var x = unchecked((byte)(reg1 - REG_R19));
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
            assert(REG_V8 <= reg1 && reg1 <= REG_V14);
            var x = unchecked((byte)(reg1 - REG_V8));
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

        if ((reg >= REG_INT_FIRST) && (reg <= REG_LR))
        {
            // save_reg: 110100xx | xxzzzzzz.
            assert(REG_R19 <= reg && reg <= REG_LR);
            var x = unchecked((byte)(reg - REG_R19));
            assert(0 <= x && x <= 0xF);

            pu.AddCode(unchecked((byte)(0xD0 | (byte)(x >> 2))), unchecked((byte)((byte)(x << 6) | (byte)z)));
        }
        else
        {
            // save_freg: 1101110x | xxzzzzzz.
            assert(REG_V8 <= reg && reg <= REG_V15);
            var x = unchecked((byte)(reg - REG_V8));
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

        if ((reg >= REG_INT_FIRST) && (reg <= REG_LR))
        {
            // save_reg_x: 1101010x | xxxzzzzz.
            assert(REG_R19 <= reg && reg <= REG_LR);
            var x = unchecked((byte)(reg - REG_R19));
            assert(0 <= x && x <= 0xF);

            pu.AddCode(unchecked((byte)(0xD4 | (byte)(x >> 3))), unchecked((byte)((byte)(x << 5) | (byte)z)));
        }
        else
        {
            // save_freg_x: 11011110 | xxxzzzzz.
            assert(REG_V8 <= reg && reg <= REG_V15);
            var x = unchecked((byte)(reg - REG_V8));
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
