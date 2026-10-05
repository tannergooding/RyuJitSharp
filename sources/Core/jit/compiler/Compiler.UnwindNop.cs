// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Compiler
{
#if TARGET_ARMARCH
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

        // There is one prolog per function or funclet, so initialize its unwind data here.
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

    private unsafe void unwindReserveFunc(ref FuncInfoDsc func)
    {
        var isFunclet = func.funKind != FuncKind.FUNC_ROOT;
        var funcHasColdSection = fgFirstColdBlock is not null;
        UnwindInfo? coldUnwindInfo = null;

#if DEBUG
        if ((JitConfig.JitFakeProcedureSplitting != 0) && funcHasColdSection)
        {
            funcHasColdSection = false;
        }
#endif

        var splitAtFirstFunclet = funcHasColdSection && (fgFirstColdBlock == fgFirstFuncletBB);
        if (!isFunclet && splitAtFirstFunclet)
        {
            funcHasColdSection = false;
        }

#if FEATURE_CFI_SUPPORT
        if (generateCFIUnwindCodes())
        {
            if (funcHasColdSection)
            {
                unwindReserveEEInfo(isFunclet, true, 0);
            }

            noway_assert(func.cfiCodes is not null);
            var unwindCodeBytes = unchecked(func.cfiCodes.Count * 8);
            unwindReserveEEInfo(isFunclet, false, unwindCodeBytes);
            return;
        }
#endif

        if (funcHasColdSection)
        {
            unwindGetFuncLocations(in func, false, out var startLoc, out var endLoc);
            coldUnwindInfo = new UnwindInfo();
            func.uwiCold = coldUnwindInfo;
            coldUnwindInfo.InitUnwindInfo(this, startLoc, endLoc);
            coldUnwindInfo.HotColdSplitCodes(func.GetUnwindInfo());
        }

        var unwindInfo = func.GetUnwindInfo();
        unwindInfo.Split();
        if (!isFunclet || !funcHasColdSection)
        {
            unwindInfo.Reserve(isFunclet, true);
        }

        if (funcHasColdSection)
        {
            var coldInfo = coldUnwindInfo
                ?? throw new InvalidOperationException("Cold unwind information was not initialized.");
            coldInfo.Split();
            coldInfo.Reserve(isFunclet, false);
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

        if ((func.funKind == FuncKind.FUNC_ROOT) || (func.uwiCold is null))
        {
            func.GetUnwindInfo().Allocate((CorJitFuncKind)func.funKind, pHotCode, pColdCode, true);
        }

        func.uwiCold?.Allocate((CorJitFuncKind)func.funKind, pHotCode, pColdCode, false);
    }

#if TARGET_ARM
    public void unwindSetFrameReg(regNumber reg, uint offset)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM32 frame-register unwind recording is not ported.");
    }

    public void unwindNop(uint codeSizeInBytes)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM32 unwind NOP encoding is not ported.");
    }
#endif
#elif TARGET_LOONGARCH64
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

    private unsafe void unwindReserveFunc(ref FuncInfoDsc func)
    {
        var isFunclet = func.funKind != FuncKind.FUNC_ROOT;

#if FEATURE_CFI_SUPPORT
        if (generateCFIUnwindCodes())
        {
            if (fgFirstColdBlock is not null)
            {
                unwindReserveEEInfo(isFunclet, true, 0);
            }

            noway_assert(func.cfiCodes is not null);
            var unwindCodeBytes = unchecked(func.cfiCodes.Count * 8);
            unwindReserveEEInfo(isFunclet, false, unwindCodeBytes);
            return;
        }
#endif

        var funcHasColdSection = fgFirstColdBlock is not null;
        if (funcHasColdSection)
        {
            assert(!isFunclet); // TODO-CQ: support hot/cold splitting with EH.
            unwindGetFuncLocations(in func, false, out var startLoc, out var endLoc);
            var coldUnwindInfo = new UnwindInfo();
            func.uwiCold = coldUnwindInfo;
            coldUnwindInfo.InitUnwindInfo(this, startLoc, endLoc);
            coldUnwindInfo.HotColdSplitCodes(func.GetUnwindInfo());
        }

        var unwindInfo = func.GetUnwindInfo();
        unwindInfo.Split();
        unwindInfo.Reserve(isFunclet, true);

        if (funcHasColdSection)
        {
            var coldUnwindInfo = func.uwiCold
                ?? throw new System.InvalidOperationException("Cold unwind information was not initialized.");
            coldUnwindInfo.Split();
            coldUnwindInfo.Reserve(isFunclet, false);
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
#endif
}
