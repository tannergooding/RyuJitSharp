// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
#if TARGET_ARM
    public void unwindBegProlog()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM32 unwind prolog recording is not ported.");
    }

    public void unwindEndProlog()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM32 unwind prolog recording is not ported.");
    }

    public void unwindSetFrameReg(regNumber reg, uint offset)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM32 frame-register unwind recording is not ported.");
    }

    public void unwindReserve()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM32 unwind reservation is not ported.");
    }

    public unsafe void unwindEmit(void* pHotCode, void* pColdCode)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM32 unwind emission is not ported.");
    }

    public void unwindNop(uint codeSizeInBytes)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM32 unwind NOP encoding is not ported.");
    }
#elif TARGET_ARM64
    public void unwindBegProlog()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM64 unwind prolog recording is not ported.");
    }

    public void unwindEndProlog()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM64 unwind prolog recording is not ported.");
    }

    public void unwindReserve()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM64 unwind reservation is not ported.");
    }

    public unsafe void unwindEmit(void* pHotCode, void* pColdCode)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM64 unwind emission is not ported.");
    }
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
        if (func.uwiCold is not null)
        {
            func.uwiCold.Allocate((CorJitFuncKind)func.funKind, pHotCode, pColdCode, false);
        }
    }
#endif
}
