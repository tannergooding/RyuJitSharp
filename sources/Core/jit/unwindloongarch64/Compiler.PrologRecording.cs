// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public partial class Compiler
{
    public void unwindPushLoongArch64(regNumber reg)
    {
        unreached(); // Use one of the unwindSaveReg* functions instead.
    }

    public void unwindAllocStackLoongArch64(uint size)
    {
        if (generateCFIUnwindCodes())
        {
            if (UnwindEmitter().emitGeneratingPrologOrFuncletProlog())
            {
                unwindAllocStackCFI(size);
            }
            return;
        }

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

    public void unwindSetFrameRegLoongArch64(regNumber reg, uint offset)
    {
        if (generateCFIUnwindCodes())
        {
            if (UnwindEmitter().emitGeneratingPrologOrFuncletProlog())
            {
                unwindSetFrameRegCFI(reg, offset);
            }
            return;
        }

        var unwindInfo = funCurrentFunc().GetUnwindInfo();
        if (offset == 0)
        {
            assert(reg == REG_LOONGARCH64_FP);
            // set_fp: move fp, sp.
            unwindInfo.AddCode(0xE1);
        }
        else
        {
            // add_fp: addi.d fp, sp, #x * 8.
            assert(reg == REG_LOONGARCH64_FP);
            assert((offset % 8) == 0);
            var x = offset / 8;
            assert(x <= 0x1FF);

            unwindInfo.AddCode(0xE2, unchecked((byte)(x >> 8)), unchecked((byte)x));
        }
    }

    public void unwindSaveRegLoongArch64(regNumber reg, uint offset)
    {
        unwindSaveRegLoongArch64(reg, unchecked((int)offset));
    }

    public void unwindNopLoongArch64()
    {
        RecordUnwindPadding(4);
    }

    public void unwindSaveRegLoongArch64(regNumber reg, int offset)
    {
        // st.d reg, sp, offset; prolog offsets are positive multiples of 8.
        assert(0 <= offset && offset <= 2047);
        assert((offset % 8) == 0);

        if (generateCFIUnwindCodes())
        {
            if (UnwindEmitter().emitGeneratingPrologOrFuncletProlog())
            {
                ref var func = ref funCurrentFunc();
                var cbProlog = unwindGetCurrentOffset(in func);

                createCfiCode(in func, cbProlog, CFI_REL_OFFSET, MapCfiRegNumToDwarfReg(reg), offset);
            }
            return;
        }

        var z = offset / 8;
        var unwindInfo = funCurrentFunc().GetUnwindInfo();

        if (Emitter.isGeneralRegister(reg))
        {
            // save_reg encodes the register relative to RA and the SP offset in 8-byte units.
            assert(reg == REG_LOONGARCH64_RA || reg == REG_LOONGARCH64_FP
                   || ((int)reg >= (int)REG_LOONGARCH64_S0 && (int)reg <= (int)REG_LOONGARCH64_S8));
            var x = unchecked((byte)((int)reg - (int)REG_LOONGARCH64_RA));
            assert(x <= 0x1E);

            unwindInfo.AddCode(0xD0, x, unchecked((byte)z));
        }
        else
        {
            // save_freg encodes F24-F31 and the SP offset in 8-byte units.
            assert((int)reg >= (int)REG_LOONGARCH64_F24 && (int)reg <= (int)REG_LOONGARCH64_F31);
            var x = unchecked((byte)((int)reg - (int)REG_LOONGARCH64_F24));
            assert(x <= 0x7);

            unwindInfo.AddCode(0xDC, unchecked((byte)((x << 4) | (z >> 8))), unchecked((byte)z));
        }
    }

    public void unwindSaveRegPairLoongArch64(regNumber reg1, regNumber reg2, int offset)
    {
        FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.UnwindSaveRegPairIsNotImplementedForLoongArch64
    );
        return;
    }

    public void unwindReturnLoongArch64(regNumber reg)
    {
        // A trailing end opcode is always present in the unwind padding.
    }

    public void unwindBegEpilogLoongArch64()
    {
        assert(GetEmitter().emitGeneratingEpilogOrFuncletEpilog());
        assert(!compGeneratingUnwindEpilog);
        compGeneratingUnwindEpilog = true;

        if (generateCFIUnwindCodes())
        {
            return;
        }

        funCurrentFunc().GetUnwindInfo().AddEpilog();
    }

    public void unwindEndEpilogLoongArch64()
    {
        assert(GetEmitter().emitGeneratingEpilogOrFuncletEpilog());
        assert(compGeneratingUnwindEpilog);
        compGeneratingUnwindEpilog = false;
    }
}
