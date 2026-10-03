// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Buffers.Binary;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Compiler
{
#if TARGET_AMD64
    private const int Amd64UnwindCodeStorageSize = 4 + (0xFF * 2);
    private const byte UWOP_PUSH_NONVOL = 0;
    private const byte UWOP_ALLOC_LARGE = 1;
    private const byte UWOP_ALLOC_SMALL = 2;
    private const byte UWOP_SET_FPREG = 3;
#if UNIX_AMD64_ABI
    private const byte UWOP_SET_FPREG_LARGE = 11;
#endif

    private const byte UWOP_SAVE_NONVOL = 4;
    private const byte UWOP_SAVE_NONVOL_FAR = 5;
    private const byte UWOP_SAVE_XMM128 = 8;
    private const byte UWOP_SAVE_XMM128_FAR = 9;

    public void unwindBegProlog()
    {
        assert(!compGeneratingUnwindProlog);
        compGeneratingUnwindProlog = true;
#if UNIX_AMD64_ABI
        if (generateCFIUnwindCodes())
        {
            unwindBegPrologCFI();
        }
        else
#endif
        {
            unwindBegPrologWindows();
        }
    }

#if UNIX_AMD64_ABI
    private static short mapRegNumToDwarfReg(regNumber reg)
    {
        short dwarfReg = DWARF_REG_ILLEGAL;

        switch (reg)
        {
            case regNumber.REG_RAX:
            {
                dwarfReg = 0;
                break;
            }
            case regNumber.REG_RCX:
            {
                dwarfReg = 2;
                break;
            }
            case regNumber.REG_RDX:
            {
                dwarfReg = 1;
                break;
            }
            case regNumber.REG_RBX:
            {
                dwarfReg = 3;
                break;
            }
            case regNumber.REG_RSP:
            {
                dwarfReg = 7;
                break;
            }
            case regNumber.REG_RBP:
            {
                dwarfReg = 6;
                break;
            }
            case regNumber.REG_RSI:
            {
                dwarfReg = 4;
                break;
            }
            case regNumber.REG_RDI:
            {
                dwarfReg = 5;
                break;
            }
            case >= regNumber.REG_R8 and <= regNumber.REG_R31:
            {
                dwarfReg = (short)reg;
                break;
            }
            default:
            {
                noway_assert(false, "!\"unexpected REG_NUM\"");
                break;
            }
        }

        return dwarfReg;
    }

    private void unwindSaveRegCFI(regNumber reg, uint offset)
    {
        assert(UnwindEmitter().emitGeneratingPrologOrFuncletProlog());

        if ((bool)(CfiCalleeSavedMask & genRegMask(reg)))
        {
            ref var func = ref funCurrentFunc();
            var cbProlog = unwindGetCurrentOffset(in func);
            createCfiCode(in func, cbProlog, CFI_REL_OFFSET, mapRegNumToDwarfReg(reg), unchecked((int)offset));
        }
    }
#endif

    private void unwindBegPrologWindows()
    {
        noway_assert(codeGen?.Emitter.emitGeneratingPrologOrFuncletProlog() == true);

        ref var func = ref funCurrentFunc();
        unwindGetFuncLocations(in func, true, out var startLoc, out var endLoc);
        func.startLoc = startLoc;
        func.endLoc = endLoc;

        if (fgFirstColdBlock is not null)
        {
            unwindGetFuncLocations(in func, false, out var coldStartLoc, out var coldEndLoc);
            func.coldStartLoc = coldStartLoc;
            func.coldEndLoc = coldEndLoc;
        }

        func.unwindCodes = new byte[Amd64UnwindCodeStorageSize];
        func.unwindCodeSlot = Amd64UnwindCodeStorageSize;
        func.unwindHeader.Version = 1;
        func.unwindHeader.Flags = 0;
        func.unwindHeader.CountOfUnwindCodes = 0;
        func.unwindHeader.FrameRegister = 0;
        func.unwindHeader.FrameOffset = 0;
    }

    public void unwindEndProlog()
    {
        noway_assert(codeGen?.Emitter.emitGeneratingPrologOrFuncletProlog() == true);
        noway_assert(compGeneratingUnwindProlog);
        compGeneratingUnwindProlog = false;
    }

    public void unwindBegEpilog()
    {
        noway_assert(codeGen?.Emitter.emitGeneratingEpilogOrFuncletEpilog() == true);
        noway_assert(!compGeneratingUnwindEpilog);
        compGeneratingUnwindEpilog = true;
    }

    public void unwindEndEpilog()
    {
        noway_assert(codeGen?.Emitter.emitGeneratingEpilogOrFuncletEpilog() == true);
        noway_assert(compGeneratingUnwindEpilog);
        compGeneratingUnwindEpilog = false;
    }

    public void unwindPush(regNumber reg)
    {
#if UNIX_AMD64_ABI
        if (generateCFIUnwindCodes())
        {
            unwindPushPopCFI(reg);
        }
        else
#endif
        {
            unwindPushWindows(reg);
        }
    }

    public void unwindPush2(regNumber reg1, regNumber reg2)
    {
#if UNIX_AMD64_ABI
        if (generateCFIUnwindCodes())
        {
            unwindPush2Pop2CFI(reg1, reg2);
        }
        else
#endif
        {
            unwindPush2Windows(reg1, reg2);
        }
    }

    private void unwindPush2Windows(regNumber reg1, regNumber reg2)
    {
        unwindPushWindows(reg1);
        unwindPushWindows(reg2);
    }

    private void unwindPushWindows(regNumber reg)
    {
        ref var func = ref UnwindCurrentProlog();
        noway_assert(genIsValidIntReg(reg));
        var code = UnwindReserveCode(ref func, 0);
        var cbProlog = unwindGetCurrentOffset(in func);

        if (UnwindIsCalleeSaved(reg)
#if ETW_EBP_FRAMED
            || reg == REG_FPBASE
#endif
            )
        {
            UnwindWriteCode(ref func, code, cbProlog, UWOP_PUSH_NONVOL, (byte)reg);
        }
        else
        {
            UnwindWriteCode(ref func, code, cbProlog, UWOP_ALLOC_SMALL, 0);
        }
    }

    public void unwindAllocStack(uint size)
    {
#if UNIX_AMD64_ABI
        if (generateCFIUnwindCodes())
        {
            unwindAllocStackCFI(size);
        }
        else
#endif
        {
            unwindAllocStackWindows(size);
        }
    }

    private void unwindAllocStackWindows(uint size)
    {
        ref var func = ref UnwindCurrentProlog();
        noway_assert(size >= 8 && (size % 8) == 0);

        int code;
        byte op;
        byte info;

        if (size <= 128)
        {
            code = UnwindReserveCode(ref func, 0);
            op = UWOP_ALLOC_SMALL;
            info = (byte)((size - 8) / 8);
        }
        else if (size <= 0x7FFF8)
        {
            code = UnwindReserveCode(ref func, 2);
            BinaryPrimitives.WriteUInt16LittleEndian(UnwindCodes(in func).AsSpan(code + 2, 2), (ushort)(size / 8));
            op = UWOP_ALLOC_LARGE;
            info = 0;
        }
        else
        {
            code = UnwindReserveCode(ref func, 4);
            BinaryPrimitives.WriteUInt32LittleEndian(UnwindCodes(in func).AsSpan(code + 2, 4), size);
            op = UWOP_ALLOC_LARGE;
            info = 1;
        }

        UnwindWriteCode(ref func, code, unwindGetCurrentOffset(in func), op, info);
    }

    public void unwindSetFrameReg(regNumber reg, uint offset)
    {
#if UNIX_AMD64_ABI
        if (generateCFIUnwindCodes())
        {
            unwindSetFrameRegCFI(reg, offset);
        }
        else
#endif
        {
            unwindSetFrameRegWindows(reg, offset);
        }
    }

    private void unwindSetFrameRegWindows(regNumber reg, uint offset)
    {
        ref var func = ref UnwindCurrentProlog();
        var cbProlog = unwindGetCurrentOffset(in func);
#if UNIX_AMD64_ABI
        noway_assert(offset % 16 == 0);
#else
        noway_assert(offset <= 240 && offset % 16 == 0);
#endif
        noway_assert(genIsValidIntReg(reg) && (uint)reg <= 15);

        func.unwindHeader.FrameRegister = (byte)reg;
#if UNIX_AMD64_ABI
        if (offset > 240)
        {
            var largeCode = UnwindReserveCode(ref func, 4);
            BinaryPrimitives.WriteUInt32LittleEndian(UnwindCodes(in func).AsSpan(largeCode + 2, 4), offset / 16);
            UnwindWriteCode(ref func, largeCode, cbProlog, UWOP_SET_FPREG_LARGE, 0);
            func.unwindHeader.FrameOffset = 15;
            return;
        }
#endif
        var code = UnwindReserveCode(ref func, 0);
        UnwindWriteCode(ref func, code, cbProlog, UWOP_SET_FPREG, 0);
        func.unwindHeader.FrameOffset = (byte)(offset / 16);
    }

    public void unwindSaveReg(regNumber reg, uint offset)
    {
#if UNIX_AMD64_ABI
        if (generateCFIUnwindCodes())
        {
            unwindSaveRegCFI(reg, offset);
        }
        else
#endif
        {
            unwindSaveRegWindows(reg, offset);
        }
    }

    private void unwindSaveRegWindows(regNumber reg, uint offset)
    {
        ref var func = ref UnwindCurrentProlog();

        if (UnwindIsCalleeSaved(reg))
        {
            var isFloat = genIsValidFloatReg(reg);
            int code;
            byte op;

            if (offset < 0x80000)
            {
                code = UnwindReserveCode(ref func, 2);
                BinaryPrimitives.WriteUInt16LittleEndian(UnwindCodes(in func).AsSpan(code + 2, 2),
                    (ushort)(offset / (isFloat ? 16 : 8)));
                op = isFloat ? UWOP_SAVE_XMM128 : UWOP_SAVE_NONVOL;
            }
            else
            {
                code = UnwindReserveCode(ref func, 4);
                BinaryPrimitives.WriteUInt32LittleEndian(UnwindCodes(in func).AsSpan(code + 2, 4), offset);
                op = isFloat ? UWOP_SAVE_XMM128_FAR : UWOP_SAVE_NONVOL_FAR;
            }

            var unwindRegNum = isFloat ? (uint)reg - XMMBASE : (uint)reg;
            noway_assert(unwindRegNum <= 15);
            UnwindWriteCode(ref func, code, unwindGetCurrentOffset(in func), op, (byte)unwindRegNum);
        }
    }

    private ref FuncInfoDsc UnwindCurrentProlog()
    {
        noway_assert(codeGen?.Emitter.emitGeneratingPrologOrFuncletProlog() == true);

        ref var func = ref funCurrentFunc();
        noway_assert(func.unwindHeader.Version == 1 && func.unwindHeader.CountOfUnwindCodes == 0);
        noway_assert(func.unwindCodes is not null);
        return ref func;
    }

    private static bool UnwindIsCalleeSaved(regNumber reg)
    {
        noway_assert(genIsValidIntReg(reg) || genIsValidFloatReg(reg));
        return (((regMask)(1L << (int)reg) & (SRBM_INT_CALLEE_SAVED | SRBM_FLT_CALLEE_SAVED)) != 0);
    }

    private static byte[] UnwindCodes(in FuncInfoDsc func)
    {
        noway_assert(func.unwindCodes is not null);
        return func.unwindCodes;
    }

    private static int UnwindReserveCode(ref FuncInfoDsc func, uint operandBytes)
    {
        var byteCount = 2 + operandBytes;
        noway_assert(func.unwindCodes is not null && func.unwindCodeSlot > byteCount);
        func.unwindCodeSlot -= byteCount;
        return checked((int)func.unwindCodeSlot);
    }

    private static void UnwindWriteCode(ref FuncInfoDsc func, int slot, uint offset, byte op, byte info)
    {
        noway_assert(offset <= byte.MaxValue && op < 16 && info < 16);
        var codes = UnwindCodes(in func);
        codes[slot] = (byte)offset;
        codes[slot + 1] = (byte)(op | (info << 4));
    }
#else
    public void unwindBegProlog() => throw new FatalJitException(CORJIT_SKIPPED, "Unwind recording requires Windows AMD64.");

    public void unwindEndProlog() => throw new FatalJitException(CORJIT_SKIPPED, "Unwind recording requires Windows AMD64.");

    public void unwindBegEpilog() => throw new FatalJitException(CORJIT_SKIPPED, "Unwind recording requires Windows AMD64.");

    public void unwindEndEpilog() => throw new FatalJitException(CORJIT_SKIPPED, "Unwind recording requires Windows AMD64.");

    public void unwindPush(regNumber reg) => throw new FatalJitException(CORJIT_SKIPPED, "Unwind recording requires Windows AMD64.");

    public void unwindPush2(regNumber reg1, regNumber reg2) => throw new FatalJitException(CORJIT_SKIPPED, "Unwind recording requires Windows AMD64.");

    public void unwindAllocStack(uint size) => throw new FatalJitException(CORJIT_SKIPPED, "Unwind recording requires Windows AMD64.");

    public void unwindSetFrameReg(regNumber reg, uint offset) => throw new FatalJitException(CORJIT_SKIPPED, "Unwind recording requires Windows AMD64.");

    public void unwindSaveReg(regNumber reg, uint offset) => throw new FatalJitException(CORJIT_SKIPPED, "Unwind recording requires Windows AMD64.");
#endif
}
