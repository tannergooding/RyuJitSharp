// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public short MapArm64RegNumToDwarfReg(regNumber reg)
    {
        return MapCfiRegNumToDwarfReg(reg);
    }

    public void unwindPushArm64(regNumber reg)
    {
        unreached(); // Use one of the unwindSaveReg* functions instead.
    }

    public void unwindAllocStackArm64(uint size)
    {
        if (generateCFIUnwindCodes())
        {
            if (UnwindEmitter().emitGeneratingPrologOrFuncletProlog())
            {
                unwindAllocStackCFI(size);
            }
            return;
        }

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

    public void unwindSetFrameRegArm64(regNumber reg, uint offset)
    {
        if (generateCFIUnwindCodes())
        {
            if (UnwindEmitter().emitGeneratingPrologOrFuncletProlog())
            {
                unwindSetFrameRegCFI(reg, offset);
            }
            return;
        }

        var pu = funCurrentFunc().GetUnwindInfo();
        if (offset == 0)
        {
            assert(reg == REG_ARM64_FP);
            // set_fp: 11100001, mov r29, sp.
            pu.AddCode(0xE1);
        }
        else
        {
            // add_fp: 11100010 | xxxxxxxx, add r29, sp, #x * 8.
            assert(reg == REG_ARM64_FP);
            assert((offset % 8) == 0);
            var x = offset / 8;
            assert(x <= 0xFF);

            pu.AddCode(0xE2, unchecked((byte)x));
        }
    }

    public void unwindSaveRegArm64(regNumber reg, uint offset)
    {
        unreached();
    }

    public void unwindNopArm64()
    {
        RecordUnwindPadding(4);
    }

    // The second register must immediately follow the first, except for pairs ending in LR.
    // Those pairs may start with FP or an odd register from R19 through R27.
    public void unwindSaveRegPairArm64(regNumber reg1, regNumber reg2, int offset)
    {
        assert(0 <= offset && offset <= 504);
        assert((offset % 8) == 0);
        if (generateCFIUnwindCodes())
        {
            if (UnwindEmitter().emitGeneratingPrologOrFuncletProlog())
            {
                ref var func = ref funCurrentFunc();
                var cbProlog = unwindGetCurrentOffset(in func);

                createCfiCode(in func, cbProlog, CFI_REL_OFFSET, MapArm64RegNumToDwarfReg(reg1), offset);
                createCfiCode(in func, cbProlog, CFI_REL_OFFSET, MapArm64RegNumToDwarfReg(reg2), offset + 8);
            }

            return;
        }

        var pu = funCurrentFunc().GetUnwindInfo();
        var z = offset / 8;
        assert(0 <= z && z <= 0x3F);

        if (reg1 == REG_ARM64_FP)
        {
            // save_fplr: 01zzzzzz.
            assert(reg2 == REG_ARM64_LR);
            pu.AddCode(unchecked((byte)(0x40 | (byte)z)));
        }
        else if (reg2 == REG_ARM64_LR)
        {
            // save_lrpair: 1101011x | xxzzzzzz.
            assert(REG_ARM64_R19 <= reg1 && reg1 <= REG_ARM64_R27);
            var x = unchecked((byte)(reg1 - REG_ARM64_R19));
            assert((x % 2) == 0);
            x /= 2;
            assert(0 <= x && x <= 0x7);

            pu.AddCode(unchecked((byte)(0xD6 | (byte)(x >> 2))), unchecked((byte)((byte)(x << 6) | (byte)z)));
        }
        else if ((reg1 >= TargetRuntimeMetadata.IntegerRegisterFirst) && (reg1 <= REG_ARM64_LR))
        {
            // save_regp: 110010xx | xxzzzzzz.
            assert(reg1 + 1 == reg2);
            assert(REG_ARM64_R19 <= reg1 && reg1 <= REG_ARM64_R27);
            var x = unchecked((byte)(reg1 - REG_ARM64_R19));
            assert(0 <= x && x <= 0xF);

            pu.AddCode(unchecked((byte)(0xC8 | (byte)(x >> 2))), unchecked((byte)((byte)(x << 6) | (byte)z)));
        }
        else
        {
            // save_fregp: 1101100x | xxzzzzzz.
            assert(reg1 + 1 == reg2);
            assert(REG_ARM64_V8 <= reg1 && reg1 <= REG_ARM64_V14);
            var x = unchecked((byte)(reg1 - REG_ARM64_V8));
            assert(0 <= x && x <= 0x7);

            pu.AddCode(unchecked((byte)(0xD8 | (byte)(x >> 2))), unchecked((byte)((byte)(x << 6) | (byte)z)));
        }
    }

    public void unwindSaveRegPairPreindexedArm64(regNumber reg1, regNumber reg2, int offset)
    {
        assert(offset < 0);
        assert((offset % 8) == 0);
        if (generateCFIUnwindCodes())
        {
            if (UnwindEmitter().emitGeneratingPrologOrFuncletProlog())
            {
                ref var func = ref funCurrentFunc();
                var cbProlog = unwindGetCurrentOffset(in func);

                createCfiCode(in func, cbProlog, CFI_ADJUST_CFA_OFFSET, DWARF_REG_ILLEGAL, unchecked(-offset));
                createCfiCode(in func, cbProlog, CFI_REL_OFFSET, MapArm64RegNumToDwarfReg(reg1), 0);
                createCfiCode(in func, cbProlog, CFI_REL_OFFSET, MapArm64RegNumToDwarfReg(reg2), 8);
            }

            return;
        }

        var pu = funCurrentFunc().GetUnwindInfo();
        if (reg1 == REG_ARM64_FP)
        {
            // save_fplr_x: 10zzzzzz.
            assert(-512 <= offset);
            var z = unchecked((-offset) / 8 - 1);
            assert(0 <= z && z <= 0x3F);
            assert(reg2 == REG_ARM64_LR);

            pu.AddCode(unchecked((byte)(0x80 | (byte)z)));
        }
        else if ((reg1 == REG_ARM64_R19) && (-256 <= offset))
        {
            // save_r19r20_x: 001zzzzz. Unlike other pre-indexed codes, Z is not biased.
            // Upstream includes -256 in this branch even though the asserted Z limit is 31.
            var z = unchecked(-offset) / 8;
            assert(0 <= z && z <= 0x1F);
            assert(reg2 == REG_ARM64_R20);

            pu.AddCode(unchecked((byte)(0x20 | (byte)z)));
        }
        else if ((reg1 >= TargetRuntimeMetadata.IntegerRegisterFirst) && (reg1 <= REG_ARM64_LR))
        {
            // save_regp_x: 110011xx | xxzzzzzz.
            assert(-512 <= offset);
            var z = unchecked((-offset) / 8 - 1);
            assert(0 <= z && z <= 0x3F);
            assert(reg1 + 1 == reg2);
            assert(REG_ARM64_R19 <= reg1 && reg1 <= REG_ARM64_R27);
            var x = unchecked((byte)(reg1 - REG_ARM64_R19));
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
            assert(REG_ARM64_V8 <= reg1 && reg1 <= REG_ARM64_V14);
            var x = unchecked((byte)(reg1 - REG_ARM64_V8));
            assert(0 <= x && x <= 0x7);

            pu.AddCode(unchecked((byte)(0xDA | (byte)(x >> 2))), unchecked((byte)((byte)(x << 6) | (byte)z)));
        }
    }

    public void unwindSaveRegArm64(regNumber reg, int offset)
    {
        assert(0 <= offset && offset <= 504);
        assert((offset % 8) == 0);
        if (generateCFIUnwindCodes())
        {
            if (UnwindEmitter().emitGeneratingPrologOrFuncletProlog())
            {
                ref var func = ref funCurrentFunc();
                var cbProlog = unwindGetCurrentOffset(in func);

                createCfiCode(in func, cbProlog, CFI_REL_OFFSET, MapArm64RegNumToDwarfReg(reg), offset);
            }

            return;
        }

        var z = offset / 8;
        assert(0 <= z && z <= 0x3F);
        var pu = funCurrentFunc().GetUnwindInfo();

        if ((reg >= TargetRuntimeMetadata.IntegerRegisterFirst) && (reg <= REG_ARM64_LR))
        {
            // save_reg: 110100xx | xxzzzzzz.
            assert(REG_ARM64_R19 <= reg && reg <= REG_ARM64_LR);
            var x = unchecked((byte)(reg - REG_ARM64_R19));
            assert(0 <= x && x <= 0xF);

            pu.AddCode(unchecked((byte)(0xD0 | (byte)(x >> 2))), unchecked((byte)((byte)(x << 6) | (byte)z)));
        }
        else
        {
            // save_freg: 1101110x | xxzzzzzz.
            assert(REG_ARM64_V8 <= reg && reg <= REG_ARM64_V15);
            var x = unchecked((byte)(reg - REG_ARM64_V8));
            assert(0 <= x && x <= 0x7);

            pu.AddCode(unchecked((byte)(0xDC | (byte)(x >> 2))), unchecked((byte)((byte)(x << 6) | (byte)z)));
        }
    }

    public void unwindSaveRegPreindexedArm64(regNumber reg, int offset)
    {
        assert(-256 <= offset && offset < 0);
        assert((offset % 8) == 0);
        if (generateCFIUnwindCodes())
        {
            if (UnwindEmitter().emitGeneratingPrologOrFuncletProlog())
            {
                ref var func = ref funCurrentFunc();
                var cbProlog = unwindGetCurrentOffset(in func);

                createCfiCode(in func, cbProlog, CFI_ADJUST_CFA_OFFSET, DWARF_REG_ILLEGAL, unchecked(-offset));
                createCfiCode(in func, cbProlog, CFI_REL_OFFSET, MapArm64RegNumToDwarfReg(reg), 0);
            }

            return;
        }

        var pu = funCurrentFunc().GetUnwindInfo();
        var z = unchecked((-offset) / 8 - 1);
        assert(0 <= z && z <= 0x1F);

        if ((reg >= TargetRuntimeMetadata.IntegerRegisterFirst) && (reg <= REG_ARM64_LR))
        {
            // save_reg_x: 1101010x | xxxzzzzz.
            assert(REG_ARM64_R19 <= reg && reg <= REG_ARM64_LR);
            var x = unchecked((byte)(reg - REG_ARM64_R19));
            assert(0 <= x && x <= 0xF);

            pu.AddCode(unchecked((byte)(0xD4 | (byte)(x >> 3))), unchecked((byte)((byte)(x << 5) | (byte)z)));
        }
        else
        {
            // save_freg_x: 11011110 | xxxzzzzz.
            assert(REG_ARM64_V8 <= reg && reg <= REG_ARM64_V15);
            var x = unchecked((byte)(reg - REG_ARM64_V8));
            assert(0 <= x && x <= 0x7);

            pu.AddCode(0xDE, unchecked((byte)((byte)(x << 5) | (byte)z)));
        }
    }

    public void unwindSaveNext()
    {
        // CFI has no save-next opcode.
        assert(!generateCFIUnwindCodes());
        var pu = funCurrentFunc().GetUnwindInfo();
        // The caller is responsible for ensuring that the next register pair is correct.
        pu.AddCode(0xE6);
    }

    public void unwindPacSignLRArm64()
    {
        if (JitConfig.JitPacEnabled == 0)
        {
            return;
        }
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
        // pac_sign_lr: 11111100, sign LR with the platform PAC key.
        funCurrentFunc().GetUnwindInfo().AddCode(0xFC);
    }

    public void unwindReturnArm64(regNumber reg)
    {
        // Padding always contains at least one trailing "end" opcode.
    }
}
