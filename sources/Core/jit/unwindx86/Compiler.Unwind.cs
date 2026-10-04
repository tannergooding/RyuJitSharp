// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Compiler
{
    // x86 UNWIND_INFO contains only its ULONG FunctionLength field.
    private const int X86UnwindInfoSize = sizeof(uint);

    public void unwindBegProlog()
    {
    }

    public void unwindEndProlog()
    {
    }

    public void unwindBegEpilog()
    {
    }

    public void unwindEndEpilog()
    {
    }

    public void unwindPush(regNumber reg)
    {
    }

    public void unwindAllocStack(uint size)
    {
    }

    public void unwindSetFrameReg(regNumber reg, uint offset)
    {
    }

    public void unwindSaveReg(regNumber reg, uint offset)
    {
    }

    public void unwindReserve()
    {
        var emitter = UnwindEmitter();
        assert(!emitter.emitGeneratingPrologOrFuncletProlog());
        assert(!emitter.emitGeneratingEpilogOrFuncletEpilog());

        foreach (ref readonly var func in Funcs)
        {
            unwindReserveFunc(in func);
        }
    }

    private void unwindReserveFunc(in FuncInfoDsc func)
    {
        unwindReserveFuncHelper(in func, true);

        if (fgFirstColdBlock is not null)
        {
#if DEBUG
            if (JitConfig.JitFakeProcedureSplitting == 0)
#endif
            {
                unwindReserveFuncHelper(in func, false);
            }
        }
    }

    private void unwindReserveFuncHelper(in FuncInfoDsc func, bool isHotCode)
    {
        var isFunclet = func.funKind != FuncKind.FUNC_ROOT;
        var isColdCode = !isHotCode;

        eeReserveUnwindInfo(isFunclet, isColdCode, X86UnwindInfoSize);
    }

    public unsafe void unwindEmit(void* pHotCode, void* pColdCode)
    {
        var emitter = UnwindEmitter();
        assert(!emitter.emitGeneratingPrologOrFuncletProlog());
        assert(!emitter.emitGeneratingEpilogOrFuncletEpilog());

        foreach (ref readonly var func in Funcs)
        {
            unwindEmitFunc(in func, pHotCode, pColdCode);
        }
    }

    private unsafe void unwindEmitFunc(in FuncInfoDsc func, void* pHotCode, void* pColdCode)
    {
        unwindEmitFuncHelper(in func, pHotCode, pColdCode, true);

        if (pColdCode != null)
        {
#if DEBUG
            if (JitConfig.JitFakeProcedureSplitting == 0)
#endif
            {
                unwindEmitFuncHelper(in func, pHotCode, pColdCode, false);
            }
        }
    }

    private unsafe void unwindEmitFuncHelper(in FuncInfoDsc func, void* pHotCode, void* pColdCode, bool isHotCode)
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
#if DEBUG
            if ((JitConfig.JitFakeProcedureSplitting != 0) && (fgFirstColdBlock is not null))
            {
                assert(endOffset <= unchecked((uint)info.compNativeCodeSize));
            }
            else
#endif
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

    private unsafe void eeReserveUnwindInfo(bool isFunclet, bool isColdCode, int unwindSize)
    {
#if DEBUG
        if (verbose)
        {
            jitprintf(
                $"reserveUnwindInfo(isFunclet={(isFunclet ? "true" : "false")}, " +
                $"isColdCode={(isColdCode ? "true" : "false")}, unwindSize=0x{unwindSize:x})\n");
        }
#endif
        if (info.compMatchedVM)
        {
            info.compCompHnd->reserveUnwindInfo(isFunclet, isColdCode, unwindSize);
        }
    }

    private unsafe void eeAllocUnwindInfo(byte* pHotCode, byte* pColdCode, int startOffset, int endOffset,
        int unwindSize, byte* pUnwindBlock, CorJitFuncKind funcKind)
    {
#if DEBUG
        if (verbose)
        {
            var functionDescription = funcKind switch
            {
                CorJitFuncKind.CORJIT_FUNC_ROOT => "main function",
                CorJitFuncKind.CORJIT_FUNC_HANDLER => "handler",
                CorJitFuncKind.CORJIT_FUNC_FILTER => "filter",
                _ => "ILLEGAL",
            };
            jitprintf(
                $"allocUnwindInfo(pHotCode=0x{(nuint)dspPtr(pHotCode):X16}, " +
                $"pColdCode=0x{(nuint)dspPtr(pColdCode):X16}, startOffset=0x{startOffset:x}, " +
                $"endOffset=0x{endOffset:x}, unwindSize=0x{unwindSize:x}, " +
                $"pUnwindBlock=0x{(nuint)dspPtr(pUnwindBlock):X16}, funKind={(int)funcKind} " +
                $"({functionDescription}))\n");
        }
#endif
        if (info.compMatchedVM)
        {
            info.compCompHnd->allocUnwindInfo(
                pHotCode, pColdCode, startOffset, endOffset, unwindSize, pUnwindBlock, funcKind);
        }
    }
}
#endif
