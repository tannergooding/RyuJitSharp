// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
namespace RyuJitSharp;

public partial class Compiler
{
    public void unwindPush(regNumber reg)
    {
        throw new FatalJitException(
            CORJIT_SKIPPED, "unwindPush is unreachable; use one of the unwindSaveReg* functions instead.");
    }

    public void unwindAllocStack(uint size)
    {
#if FEATURE_CFI_SUPPORT
        if (generateCFIUnwindCodes())
        {
            if (GetEmitter().emitGeneratingPrologOrFuncletProlog())
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
            unwindInfo.AddCode(unchecked((byte)x));
        }
        else if (x <= 0x7F)
        {
            unwindInfo.AddCode((byte)(0xC0 | (byte)(x >> 8)), unchecked((byte)x));
        }
        else
        {
            unwindInfo.AddCode(0xE0, unchecked((byte)(x >> 16)), unchecked((byte)(x >> 8)), unchecked((byte)x));
        }
    }

    public void unwindSetFrameReg(regNumber reg, uint offset)
    {
#if FEATURE_CFI_SUPPORT
        if (generateCFIUnwindCodes())
        {
            if (GetEmitter().emitGeneratingPrologOrFuncletProlog())
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
            unwindInfo.AddCode(0xE1);
        }
        else
        {
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
            jitprintf("unwindNop: adding NOP\n");
        }

        unwindInfo.uwiAddingNOP = true;
#endif
        unwindInfo.AddCode(0xE3);
#if DEBUG
        unwindInfo.uwiAddingNOP = false;
#endif
    }

    public void unwindSaveReg(regNumber reg, int offset)
    {
        assert(0 <= offset && offset <= 2047);
        assert((offset % 8) == 0);

#if FEATURE_CFI_SUPPORT
        if (generateCFIUnwindCodes())
        {
            if (GetEmitter().emitGeneratingPrologOrFuncletProlog())
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

        if (reg >= REG_INT_FIRST && reg <= REG_INT_LAST)
        {
            assert(reg == REG_RA || (REG_FP <= reg && reg <= REG_S11));
            var x = unchecked((byte)(reg - REG_RA));
            assert(x <= 0x1B);
            assert(z <= 0xFF);

            unwindInfo.AddCode(0xD0, x, unchecked((byte)z));
        }
        else
        {
            assert(REG_FS0 == reg || REG_FS1 == reg || (REG_FS2 <= reg && reg <= REG_FS11));
            var x = unchecked((byte)(reg - REG_FS0));
            assert(x <= 0x13);
            assert(z <= 0xFFF);

            unwindInfo.AddCode(unchecked((byte)(0xDC | (x >> 4))),
                unchecked((byte)(((uint)x << 4) | (unchecked((uint)z) >> 8))), unchecked((byte)z));
        }
    }

    public void unwindSaveRegPair(regNumber reg1, regNumber reg2, int offset)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "unwindSaveRegPair-----unused on RISCV64 yet----");
    }

    public void unwindReturn(regNumber reg)
    {
    }

    public void unwindBegProlog()
    {
        assert(GetEmitter().emitGeneratingPrologOrFuncletProlog());
        assert(!compGeneratingUnwindProlog);
        compGeneratingUnwindProlog = true;

#if FEATURE_CFI_SUPPORT
        if (generateCFIUnwindCodes())
        {
            unwindBegPrologCFI();
            return;
        }
#endif

        ref var func = ref funCurrentFunc();
        unwindGetFuncLocations(in func, true, out var startLoc, out var endLoc);
        var unwindInfo = func.GetUnwindInfo();
        unwindInfo.InitUnwindInfo(this, startLoc, endLoc);
        unwindInfo.CaptureLocation();
        func.uwiCold = null;
    }

    public void unwindEndProlog()
    {
        assert(GetEmitter().emitGeneratingPrologOrFuncletProlog());
        assert(compGeneratingUnwindProlog);
        compGeneratingUnwindProlog = false;
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

    public void unwindPadding()
    {
#if FEATURE_CFI_SUPPORT
        if (generateCFIUnwindCodes())
        {
            return;
        }
#endif

        var unwindInfo = funCurrentFunc().GetUnwindInfo();
        var currentLocation = unwindInfo.GetCurrentEmitterLocation();
        assert(currentLocation.HasValue);
        GetEmitter().emitUnwindNopPadding(currentLocation.GetValueOrDefault(), this);
    }

    public void unwindReserve()
    {
        assert(!GetEmitter().emitGeneratingPrologOrFuncletProlog());
        assert(!GetEmitter().emitGeneratingEpilogOrFuncletEpilog());

        for (var index = 0; index < compFuncInfoCount; index++)
        {
            ref var func = ref compFuncInfos[index];
            unwindReserveFunc(ref func);
        }
    }

    private void unwindReserveFunc(ref FuncInfoDsc func)
    {
        var isFunclet = func.funKind != FuncKind.FUNC_ROOT;
        var funcHasColdSection = false;

#if FEATURE_CFI_SUPPORT
        if (generateCFIUnwindCodes())
        {
            uint unwindCodeBytes = 0;
            if (fgFirstColdBlock is not null)
            {
                unwindReserveEEInfo(isFunclet, true, unchecked((int)unwindCodeBytes));
            }

            noway_assert(func.cfiCodes is not null);
            unwindCodeBytes = unchecked((uint)(func.cfiCodes.Count * 8));
            unwindReserveEEInfo(isFunclet, false, unchecked((int)unwindCodeBytes));
            return;
        }
#endif

        if (fgFirstColdBlock is not null)
        {
            assert(!isFunclet);
            unwindGetFuncLocations(in func, false, out var startLoc, out var endLoc);

            var coldUnwindInfo = new UnwindInfo();
            func.uwiCold = coldUnwindInfo;
            coldUnwindInfo.InitUnwindInfo(this, startLoc, endLoc);
            coldUnwindInfo.HotColdSplitCodes(func.GetUnwindInfo());
            funcHasColdSection = true;
        }

        var unwindInfo = func.GetUnwindInfo();
        unwindInfo.Split();
        unwindInfo.Reserve(isFunclet, true);

        if (funcHasColdSection)
        {
            noway_assert(func.uwiCold is not null);
            func.uwiCold.Split();
            func.uwiCold.Reserve(isFunclet, false);
        }
    }

    public unsafe void unwindEmit(void* pHotCode, void* pColdCode)
    {
        for (var index = 0; index < compFuncInfoCount; index++)
        {
            unwindEmitFunc(in compFuncInfos[index], pHotCode, pColdCode);
        }
    }

    private unsafe void unwindEmitFunc(in FuncInfoDsc func, void* pHotCode, void* pColdCode)
    {
        assert((int)FuncKind.FUNC_ROOT == (int)CorJitFuncKind.CORJIT_FUNC_ROOT);
        assert((int)FuncKind.FUNC_HANDLER == (int)CorJitFuncKind.CORJIT_FUNC_HANDLER);
        assert((int)FuncKind.FUNC_FILTER == (int)CorJitFuncKind.CORJIT_FUNC_FILTER);

#if FEATURE_CFI_SUPPORT
        if (generateCFIUnwindCodes())
        {
            unwindEmitFuncCFI(in func, pHotCode, pColdCode);
            return;
        }
#endif

        func.GetUnwindInfo().Allocate((CorJitFuncKind)func.funKind, pHotCode, pColdCode, true);
        func.uwiCold?.Allocate((CorJitFuncKind)func.funKind, pHotCode, pColdCode, false);
    }
}
#endif
