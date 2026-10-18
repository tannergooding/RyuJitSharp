// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Compiler
{
    public void unwindBegProlog()
    {
        if (Configuration.TARGET_X86)
        {
            unwindBegPrologX86();
            return;
        }
        if (Configuration.TARGET_AMD64)
        {
            unwindBegPrologAmd64();
            return;
        }
        if (Configuration.TARGET_WASM)
        {
            unwindBegPrologWasm();
            return;
        }
        if (Configuration.TARGET_RISCV64)
        {
            unwindBegPrologRiscV64();
            return;
        }

        EnsureUnwindTargetSupported();

        assert(GetEmitter().emitGeneratingPrologOrFuncletProlog());
        assert(!compGeneratingUnwindProlog);
        compGeneratingUnwindProlog = true;

        if (generateCFIUnwindCodes())
        {
            unwindBegPrologCFI();
            return;
        }

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
        if (Configuration.TARGET_X86)
        {
            unwindEndPrologX86();
            return;
        }
        if (Configuration.TARGET_AMD64)
        {
            unwindEndPrologAmd64();
            return;
        }
        if (Configuration.TARGET_WASM)
        {
            unwindEndPrologWasm();
            return;
        }
        if (Configuration.TARGET_RISCV64)
        {
            unwindEndPrologRiscV64();
            return;
        }

        EnsureUnwindTargetSupported();

        assert(GetEmitter().emitGeneratingPrologOrFuncletProlog());
        assert(compGeneratingUnwindProlog);
        compGeneratingUnwindProlog = false;
    }

    public void unwindBegEpilog()
    {
        if (Configuration.TARGET_X86)
        {
            unwindBegEpilogX86();
            return;
        }
        if (Configuration.TARGET_AMD64)
        {
            unwindBegEpilogAmd64();
            return;
        }
        if (Configuration.TARGET_RISCV64)
        {
            unwindBegEpilogRiscV64();
            return;
        }
        if (Configuration.TARGET_LOONGARCH64)
        {
            unwindBegEpilogLoongArch64();
            return;
        }

        EnsureArmUnwindTargetSupported();

        assert(GetEmitter().emitGeneratingEpilogOrFuncletEpilog());
        assert(!compGeneratingUnwindEpilog);
        compGeneratingUnwindEpilog = true;

        if (generateCFIUnwindCodes())
        {
            return;
        }

        funCurrentFunc().GetUnwindInfo().AddEpilog();
    }

    public void unwindEndEpilog()
    {
        if (Configuration.TARGET_X86)
        {
            unwindEndEpilogX86();
            return;
        }
        if (Configuration.TARGET_AMD64)
        {
            unwindEndEpilogAmd64();
            return;
        }
        if (Configuration.TARGET_RISCV64)
        {
            unwindEndEpilogRiscV64();
            return;
        }
        if (Configuration.TARGET_LOONGARCH64)
        {
            unwindEndEpilogLoongArch64();
            return;
        }

        EnsureArmUnwindTargetSupported();

        assert(GetEmitter().emitGeneratingEpilogOrFuncletEpilog());
        assert(compGeneratingUnwindEpilog);
        compGeneratingUnwindEpilog = false;
    }

    public void unwindReserve()
    {
        if (Configuration.TARGET_X86)
        {
            unwindReserveX86();
            return;
        }
        if (Configuration.TARGET_AMD64)
        {
            unwindReserveAmd64();
            return;
        }
        if (Configuration.TARGET_WASM)
        {
            unwindReserveWasm();
            return;
        }
        if (Configuration.TARGET_RISCV64)
        {
            unwindReserveRiscV64();
            return;
        }

        EnsureUnwindTargetSupported();

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

        if (generateCFIUnwindCodes())
        {
            var funcHasColdSection = fgFirstColdBlock is not null;
            if (Configuration.TARGET_ARMARCH)
            {
                if (Configuration.DEBUG && (JitConfig.JitFakeProcedureSplitting != 0) && funcHasColdSection)
                {
                    funcHasColdSection = false;
                }

                var splitAtFirstFunclet = funcHasColdSection && (fgFirstColdBlock == fgFirstFuncletBB);
                if (!isFunclet && splitAtFirstFunclet)
                {
                    funcHasColdSection = false;
                }
            }

            if (funcHasColdSection)
            {
                unwindReserveEEInfo(isFunclet, true, 0);
            }

            noway_assert(func.cfiCodes is not null);
            var unwindCodeBytes = unchecked(func.cfiCodes.Count * 8);
            unwindReserveEEInfo(isFunclet, false, unwindCodeBytes);
            return;
        }

        if (Configuration.TARGET_ARMARCH)
        {
            var funcHasColdSection = fgFirstColdBlock is not null;
            UnwindInfo? coldUnwindInfo = null;

            if (Configuration.DEBUG && (JitConfig.JitFakeProcedureSplitting != 0) && funcHasColdSection)
            {
                funcHasColdSection = false;
            }

            var splitAtFirstFunclet = funcHasColdSection && (fgFirstColdBlock == fgFirstFuncletBB);
            if (!isFunclet && splitAtFirstFunclet)
            {
                funcHasColdSection = false;
            }

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

            return;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
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
                    ?? throw new InvalidOperationException("Cold unwind information was not initialized.");
                coldUnwindInfo.Split();
                coldUnwindInfo.Reserve(isFunclet, false);
            }

            return;
        }

        throw new FatalJitException(CORJIT_SKIPPED, "Target unwind reservation is not ported.");
    }

    public unsafe void unwindEmit(void* pHotCode, void* pColdCode)
    {
        if (Configuration.TARGET_X86)
        {
            unwindEmitX86(pHotCode, pColdCode);
            return;
        }
        if (Configuration.TARGET_AMD64)
        {
            unwindEmitAmd64(pHotCode, pColdCode);
            return;
        }
        if (Configuration.TARGET_WASM)
        {
            unwindEmitWasm(pHotCode, pColdCode);
            return;
        }
        if (Configuration.TARGET_RISCV64)
        {
            unwindEmitRiscV64(pHotCode, pColdCode);
            return;
        }

        EnsureUnwindTargetSupported();

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

        if (generateCFIUnwindCodes())
        {
            unwindEmitFuncCFI(in func, pHotCode, pColdCode);
            return;
        }

        if (Configuration.TARGET_ARMARCH)
        {
            if ((func.funKind == FuncKind.FUNC_ROOT) || (func.uwiCold is null))
            {
                func.GetUnwindInfo().Allocate((CorJitFuncKind)func.funKind, pHotCode, pColdCode, true);
            }

            func.uwiCold?.Allocate((CorJitFuncKind)func.funKind, pHotCode, pColdCode, false);
            return;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            func.GetUnwindInfo().Allocate((CorJitFuncKind)func.funKind, pHotCode, pColdCode, true);
            func.uwiCold?.Allocate((CorJitFuncKind)func.funKind, pHotCode, pColdCode, false);
            return;
        }

        throw new FatalJitException(CORJIT_SKIPPED, "Target unwind emission is not ported.");
    }

    private static void EnsureUnwindTargetSupported()
    {
        if (!Configuration.TARGET_ARMARCH && !Configuration.TARGET_LOONGARCH64)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "Target unwind recording is not ported.");
        }
    }

    private static void EnsureArmUnwindTargetSupported()
    {
        if (!Configuration.TARGET_ARMARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "Target epilog unwind recording is not ported.");
        }
    }
}
