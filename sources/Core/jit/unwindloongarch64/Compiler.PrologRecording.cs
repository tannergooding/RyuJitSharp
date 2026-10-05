// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
using static RyuJitSharp.Globals;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public partial class Compiler
{
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

        var unwindInfo = funCurrentFunc().GetUnwindInfo();
        assert(size % 16 == 0);
        var x = size / 16;

        if (x <= 0x1F)
        {
            // alloc_s: 000xxxxx stores the allocation size in units of 16 bytes.
            // TODO-Review: should say size < 512.
            unwindInfo.AddCode(unchecked((byte)x));
        }
        else if (x <= 0x7F)
        {
            // alloc_m: 11000xxx | xxxxxxxx stores the allocation size in units of 16 bytes.
            unwindInfo.AddCode(unchecked((byte)(0xC0 | (byte)(x >> 8))), unchecked((byte)x));
        }
        else
        {
            // alloc_l stores sizes below 256 MiB with the most significant bits first.
            unwindInfo.AddCode(0xE0, unchecked((byte)(x >> 16)), unchecked((byte)(x >> 8)), unchecked((byte)x));
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

        var unwindInfo = funCurrentFunc().GetUnwindInfo();
        if (offset == 0)
        {
            assert(reg == REG_FP);
            // set_fp: move fp, sp.
            unwindInfo.AddCode(0xE1);
        }
        else
        {
            // add_fp: addi.d fp, sp, #x * 8.
            assert(reg == REG_FP);
            assert((offset % 8) == 0);
            var x = offset / 8;
            assert(x <= 0x1FF);

            unwindInfo.AddCode(0xE2, unchecked((byte)(x >> 8)), unchecked((byte)x));
        }
    }

    public void unwindSaveReg(regNumber reg, uint offset)
    {
        unwindSaveReg(reg, unchecked((int)offset));
    }

    public void unwindNop()
    {
        var unwindInfo = funCurrentFunc().GetUnwindInfo();

#if DEBUG
        if (verbose)
        {
            logf("unwindNop: adding NOP\n");
        }

        unwindInfo.uwiAddingNOP = true;
#endif
        // nop: no unwind operation is required.
        unwindInfo.AddCode(0xE3);
#if DEBUG
        unwindInfo.uwiAddingNOP = false;
#endif
    }

    public void unwindSaveReg(regNumber reg, int offset)
    {
        // st.d reg, sp, offset; prolog offsets are positive multiples of 8.
        assert(0 <= offset && offset <= 2047);
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
        var unwindInfo = funCurrentFunc().GetUnwindInfo();

        if (Emitter.isGeneralRegister(reg))
        {
            // save_reg encodes the register relative to RA and the SP offset in 8-byte units.
            assert(reg == REG_RA || reg == REG_FP
                   || ((int)reg >= (int)REG_S0 && (int)reg <= (int)REG_S8));
            var x = unchecked((byte)((int)reg - (int)REG_RA));
            assert(x <= 0x1E);

            unwindInfo.AddCode(0xD0, x, unchecked((byte)z));
        }
        else
        {
            // save_freg encodes F24-F31 and the SP offset in 8-byte units.
            assert((int)reg >= (int)REG_F24 && (int)reg <= (int)REG_F31);
            var x = unchecked((byte)((int)reg - (int)REG_F24));
            assert(x <= 0x7);

            unwindInfo.AddCode(0xDC, unchecked((byte)((x << 4) | (z >> 8))), unchecked((byte)z));
        }
    }

    public void unwindSaveRegPair(regNumber reg1, regNumber reg2, int offset)
    {
        assert(false, "unused on LOONGARCH64 yet");
    }

    public void unwindReturn(regNumber reg)
    {
        // A trailing end opcode is always present in the unwind padding.
    }

    public void unwindBegEpilog()
    {
        assert(GetEmitter().emitGeneratingEpilogOrFuncletEpilog());
        assert(!compGeneratingUnwindEpilog);
        compGeneratingUnwindEpilog = true;

#if FEATURE_CFI_SUPPORT
        if (generateCFIUnwindCodes())
        {
            return;
        }
#endif

        funCurrentFunc().GetUnwindInfo().AddEpilog();
    }

    public void unwindEndEpilog()
    {
        assert(GetEmitter().emitGeneratingEpilogOrFuncletEpilog());
        assert(compGeneratingUnwindEpilog);
        compGeneratingUnwindEpilog = false;
    }
}
#endif
