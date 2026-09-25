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
#if TARGET_AMD64 && WINDOWS_AMD64_ABI
    private const int Amd64UnwindCodeStorageSize = 4 + (0xFF * 2);
    private const byte UWOP_PUSH_NONVOL = 0;
    private const byte UWOP_ALLOC_LARGE = 1;
    private const byte UWOP_ALLOC_SMALL = 2;
    private const byte UWOP_SET_FPREG = 3;
    private const byte UWOP_SAVE_NONVOL = 4;
    private const byte UWOP_SAVE_NONVOL_FAR = 5;
    private const byte UWOP_SAVE_XMM128 = 8;
    private const byte UWOP_SAVE_XMM128_FAR = 9;

    public void unwindBegProlog()
    {
        noway_assert(!compGeneratingUnwindProlog);
        compGeneratingUnwindProlog = true;
        unwindBegPrologWindows();
    }

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
        unwindPushWindows(reg);
    }

    public void unwindPush2(regNumber reg1, regNumber reg2)
    {
        unwindPush2Windows(reg1, reg2);
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
        unwindAllocStackWindows(size);
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
        unwindSetFrameRegWindows(reg, offset);
    }

    private void unwindSetFrameRegWindows(regNumber reg, uint offset)
    {
        ref var func = ref UnwindCurrentProlog();
        var cbProlog = unwindGetCurrentOffset(in func);
        noway_assert(offset <= 240 && offset % 16 == 0);
        noway_assert(genIsValidIntReg(reg) && (uint)reg <= 15);

        func.unwindHeader.FrameRegister = (byte)reg;
        var code = UnwindReserveCode(ref func, 0);
        UnwindWriteCode(ref func, code, cbProlog, UWOP_SET_FPREG, 0);
        func.unwindHeader.FrameOffset = (byte)(offset / 16);
    }

    public void unwindSaveReg(regNumber reg, uint offset)
    {
        unwindSaveRegWindows(reg, offset);
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

    private void unwindGetFuncLocations(
        in FuncInfoDsc func, bool getHotSectionData, out emitLocation? startLoc, out emitLocation? endLoc)
    {
        if (func.funKind == FuncKind.FUNC_ROOT)
        {
            if (getHotSectionData)
            {
                startLoc = null;

#if DEBUG
                if ((fgFirstColdBlock is not null) && (JitConfig.JitFakeProcedureSplitting != 0))
                {
                    endLoc = UnwindBlockLocation(fgFirstFuncletBB);
                    return;
                }
#endif

                endLoc = UnwindBlockLocation(fgFirstColdBlock ?? fgFirstFuncletBB);
            }
            else
            {
                noway_assert(fgFirstColdBlock is not null);
                startLoc = UnwindBlockLocation(fgFirstColdBlock);
                endLoc = UnwindBlockLocation(fgFirstFuncletBB);
            }
        }
        else
        {
            var startBlock = func.GetStartBlock(this);
            var lastBlock = func.GetLastBlock(this);
            startLoc = UnwindBlockLocation(startBlock);
            endLoc = UnwindBlockLocation(lastBlock.Next);
        }
    }

    private static emitLocation? UnwindBlockLocation(BasicBlock? block)
    {
        if (block is null)
        {
            return null;
        }

        noway_assert(block.bbEmitCookie is not null);
        return new emitLocation(block.bbEmitCookie);
    }

    private uint unwindGetCurrentOffset(in FuncInfoDsc func)
    {
        var emitter = codeGen?.Emitter;
        noway_assert(emitter is not null && emitter.emitGeneratingPrologOrFuncletProlog());

        var location = func.startLoc;
        noway_assert(location is null || location.Value.GetInsOffset() == 0);
        return emitter.emitGetCurrentCodeOffsetFrom(location?.GetIG());
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
