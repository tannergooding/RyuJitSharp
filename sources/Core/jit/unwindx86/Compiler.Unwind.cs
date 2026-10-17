// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Compiler
{
    // x86 UNWIND_INFO contains only its ULONG FunctionLength field.
    private const int X86UnwindInfoSize = sizeof(uint);

    public void unwindBegPrologX86()
    {
    }

    public void unwindEndPrologX86()
    {
    }

    public void unwindBegEpilogX86()
    {
    }

    public void unwindEndEpilogX86()
    {
    }

    public void unwindPushX86(regNumber reg)
    {
    }

    public void unwindAllocStackX86(uint size)
    {
    }

    public void unwindSetFrameRegX86(regNumber reg, uint offset)
    {
    }

    public void unwindSaveRegX86(regNumber reg, uint offset)
    {
    }

    public void unwindReserveX86()
    {
        var emitter = UnwindEmitter();
        assert(!emitter.emitGeneratingPrologOrFuncletProlog());
        assert(!emitter.emitGeneratingEpilogOrFuncletEpilog());

        foreach (ref readonly var func in Funcs)
        {
            unwindReserveFuncX86(in func);
        }
    }

    private void unwindReserveFuncX86(in FuncInfoDsc func)
    {
        unwindReserveFuncHelperX86(in func, true);

        if (fgFirstColdBlock is not null)
        {
            if (!Configuration.DEBUG || JitConfig.JitFakeProcedureSplitting == 0)
            {
                unwindReserveFuncHelperX86(in func, false);
            }
        }
    }

    private void unwindReserveFuncHelperX86(in FuncInfoDsc func, bool isHotCode)
    {
        var isFunclet = func.funKind != FuncKind.FUNC_ROOT;
        var isColdCode = !isHotCode;

        eeReserveUnwindInfoX86(isFunclet, isColdCode, X86UnwindInfoSize);
    }

    public unsafe void unwindEmitX86(void* pHotCode, void* pColdCode)
    {
        var emitter = UnwindEmitter();
        assert(!emitter.emitGeneratingPrologOrFuncletProlog());
        assert(!emitter.emitGeneratingEpilogOrFuncletEpilog());

        foreach (ref readonly var func in Funcs)
        {
            unwindEmitFuncX86(in func, pHotCode, pColdCode);
        }
    }

    private unsafe void unwindEmitFuncX86(in FuncInfoDsc func, void* pHotCode, void* pColdCode)
    {
        unwindEmitFuncHelperX86(in func, pHotCode, pColdCode, true);

        if (pColdCode != null)
        {
            if (!Configuration.DEBUG || JitConfig.JitFakeProcedureSplitting == 0)
            {
                unwindEmitFuncHelperX86(in func, pHotCode, pColdCode, false);
            }
        }
    }

    private unsafe void unwindEmitFuncHelperX86(in FuncInfoDsc func, void* pHotCode, void* pColdCode, bool isHotCode)
    {
        var emitter = UnwindEmitter();
        uint startOffset;
        uint endOffset;

        if (isHotCode)
        {
            unwindGetFuncLocations(in func, true, out var startLoc, out var endLoc);
            startOffset = startLoc is null ? 0 : startLoc.Value.CodeOffset(emitter);
            endOffset = endLoc is null
                ? unchecked((uint)info.compNativeCodeSize)
                : endLoc.Value.CodeOffset(emitter);
        }
        else
        {
            assert(fgFirstColdBlock is not null);
            assert(func.funKind == FuncKind.FUNC_ROOT);

            unwindGetFuncLocations(in func, false, out var coldStartLoc, out var coldEndLoc);
            startOffset = coldStartLoc is null ? 0 : coldStartLoc.Value.CodeOffset(emitter);
            endOffset = coldEndLoc is null
                ? unchecked((uint)info.compNativeCodeSize)
                : coldEndLoc.Value.CodeOffset(emitter);
        }

        if (isHotCode)
        {
            if (Configuration.DEBUG && (JitConfig.JitFakeProcedureSplitting != 0) &&
                (fgFirstColdBlock is not null))
            {
                assert(endOffset <= unchecked((uint)info.compNativeCodeSize));
            }
            else
            {
                assert(endOffset <= unchecked((uint)info.compTotalHotCodeSize));
            }

            pColdCode = null;
        }
        else
        {
            assert(startOffset >= unchecked((uint)info.compTotalHotCodeSize));
            startOffset -= unchecked((uint)info.compTotalHotCodeSize);
            endOffset -= unchecked((uint)info.compTotalHotCodeSize);
        }

        var functionLength = unchecked(endOffset - startOffset);
        eeAllocUnwindInfo(
            (byte*)pHotCode,
            (byte*)pColdCode,
            unchecked((int)startOffset),
            unchecked((int)endOffset),
            X86UnwindInfoSize,
            (byte*)&functionLength,
            (CorJitFuncKind)func.funKind);
    }

    private unsafe void eeReserveUnwindInfoX86(bool isFunclet, bool isColdCode, int unwindSize)
    {
        if (Configuration.DEBUG && verbose)
        {
            jitprintf(
                $"reserveUnwindInfo(isFunclet={(isFunclet ? "true" : "false")}, " +
                $"isColdCode={(isColdCode ? "true" : "false")}, unwindSize=0x{unwindSize:x})\n");
        }
        if (info.compMatchedVM)
        {
            info.compCompHnd->reserveUnwindInfo(isFunclet, isColdCode, unwindSize);
        }
    }
}
