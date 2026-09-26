// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_AMD64 && WINDOWS_AMD64_ABI
using System;
using System.Runtime.InteropServices;
#endif

namespace RyuJitSharp;

public partial class Compiler
{
#if TARGET_AMD64 && WINDOWS_AMD64_ABI
    public void unwindReserve()
    {
        var emitter = codeGen?.Emitter ?? throw new InvalidOperationException("An emitter is required to reserve unwind information.");
        assert(!emitter.emitGeneratingPrologOrFuncletProlog());
        assert(!emitter.emitGeneratingEpilogOrFuncletEpilog());

        var funcs = Funcs;
        for (var index = 0; index < funcs.Length; index++)
        {
            unwindReserveFunc(ref compFuncInfos[index]);
        }
    }

    private void unwindReserveFunc(ref FuncInfoDsc func)
    {
#if DEBUG
        if (JitConfig.JitFakeProcedureSplitting != 0)
        {
            unwindReserveFuncHelper(ref func, true);
            return;
        }
#endif

        if (func.funKind == FuncKind.FUNC_ROOT)
        {
            unwindReserveFuncHelper(ref func, true);

            if ((fgFirstColdBlock is not null) && (fgFirstColdBlock != fgFirstFuncletBB))
            {
                unwindReserveFuncHelper(ref func, false);
            }
        }
        else
        {
            unwindReserveFuncHelper(ref func, fgFirstColdBlock is null);
        }
    }

    private void unwindReserveFuncHelper(ref FuncInfoDsc func, bool isHotCode)
    {
        var isFunclet = func.funKind != FuncKind.FUNC_ROOT;
        var unwindCodeBytes = 0;

        if (isHotCode || isFunclet)
        {
            var codes = func.unwindCodes ?? throw new InvalidOperationException("The prolog unwind codes have not been recorded.");
            noway_assert(func.unwindHeader.Version == 1);
            noway_assert(func.unwindHeader.CountOfUnwindCodes == 0);

            if (func.unwindCodeSlot < codes.Length)
            {
                func.unwindHeader.SizeOfProlog = codes[func.unwindCodeSlot];
            }
            else
            {
                func.unwindHeader.SizeOfProlog = 0;
            }

            noway_assert(func.unwindCodeSlot >= 4);
            noway_assert((codes.Length - func.unwindCodeSlot) % 2 == 0);
            func.unwindHeader.CountOfUnwindCodes = checked((byte)((codes.Length - func.unwindCodeSlot) / 2));
            func.unwindCodeSlot -= 4;

            var header = func.unwindHeader;
            MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref header, 1)).CopyTo(codes.AsSpan((int)func.unwindCodeSlot, 4));
            unwindCodeBytes = codes.Length - (int)func.unwindCodeSlot;
        }

        eeReserveUnwindInfo(isFunclet, !isHotCode, unwindCodeBytes);
    }

    public unsafe void unwindEmit(void* pHotCode, void* pColdCode)
    {
        var emitter = codeGen?.Emitter ?? throw new InvalidOperationException("An emitter is required to publish unwind information.");
        assert(!emitter.emitGeneratingPrologOrFuncletProlog());
        assert(!emitter.emitGeneratingEpilogOrFuncletEpilog());

        foreach (ref readonly var func in Funcs)
        {
            unwindEmitFunc(in func, pHotCode, pColdCode);
        }
    }

    private unsafe void unwindEmitFunc(in FuncInfoDsc func, void* pHotCode, void* pColdCode)
    {
#if DEBUG
        if (JitConfig.JitFakeProcedureSplitting != 0)
        {
            unwindEmitFuncHelper(in func, pHotCode, pColdCode, true);
            return;
        }
#endif

        if (func.funKind == FuncKind.FUNC_ROOT)
        {
            unwindEmitFuncHelper(in func, pHotCode, pColdCode, true);

            if ((fgFirstColdBlock is not null) && (fgFirstColdBlock != fgFirstFuncletBB))
            {
                unwindEmitFuncHelper(in func, pHotCode, pColdCode, false);
            }
        }
        else
        {
            unwindEmitFuncHelper(in func, pHotCode, pColdCode, fgFirstColdBlock is null);
        }
    }

    private unsafe void unwindEmitFuncHelper(in FuncInfoDsc func, void* pHotCode, void* pColdCode, bool isHotCode)
    {
        var emitter = codeGen?.Emitter ?? throw new InvalidOperationException("An emitter is required to resolve unwind locations.");
        var startLocation = isHotCode ? func.startLoc : func.coldStartLoc;
        var endLocation = isHotCode ? func.endLoc : func.coldEndLoc;
        var startOffset = startLocation?.CodeOffset(emitter) ?? 0u;
        var endOffset = endLocation?.CodeOffset(emitter) ?? checked((uint)info.compNativeCodeSize);
        var unwindCodeBytes = 0;
        byte[]? unwindCodes = null;

        if (isHotCode || (func.funKind != FuncKind.FUNC_ROOT))
        {
            unwindCodes = func.unwindCodes ?? throw new InvalidOperationException("The prolog unwind codes have not been reserved.");
            noway_assert(func.unwindCodeSlot <= unwindCodes.Length - 4);
            unwindCodeBytes = unwindCodes.Length - (int)func.unwindCodeSlot;
            noway_assert(unwindCodeBytes == 4 + (unwindCodes[func.unwindCodeSlot + 2] * 2));
        }

#if DEBUG
        if (opts.dspUnwind)
        {
            DumpUnwindInfo(isHotCode, startOffset, endOffset, unwindCodes, func.unwindCodeSlot);
        }
#endif

        if (isHotCode)
        {
#if DEBUG
            if ((JitConfig.JitFakeProcedureSplitting != 0) && (fgFirstColdBlock is not null))
            {
                noway_assert(endOffset <= info.compNativeCodeSize);
            }
            else
#endif
            {
                noway_assert(endOffset <= info.compTotalHotCodeSize);
            }

            pColdCode = null;
        }
        else
        {
            noway_assert(fgFirstColdBlock is not null);
            noway_assert(startOffset >= info.compTotalHotCodeSize);
            startOffset -= checked((uint)info.compTotalHotCodeSize);
            endOffset -= checked((uint)info.compTotalHotCodeSize);
        }

        // The EE consumes the unwind block during this call; pin the managed storage for its full duration.
        fixed (byte* pStorage = unwindCodes)
        {
            var pUnwindBlock = unwindCodes is null ? null : pStorage + func.unwindCodeSlot;
            eeAllocUnwindInfo((byte*)pHotCode, (byte*)pColdCode, unchecked((int)startOffset), unchecked((int)endOffset),
                unwindCodeBytes, pUnwindBlock, (CorJitFuncKind)func.funKind);
        }
    }

    private unsafe void eeReserveUnwindInfo(bool isFunclet, bool isColdCode, int unwindSize)
    {
#if DEBUG
        if (verbose)
        {
            jitprintf($"reserveUnwindInfo(isFunclet={(isFunclet ? "true" : "false")}, isColdCode={(isColdCode ? "true" : "false")}, unwindSize=0x{unwindSize:x})\n");
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
            jitprintf($"allocUnwindInfo(pHotCode=0x{(nuint)dspPtr(pHotCode):X16}, pColdCode=0x{(nuint)dspPtr(pColdCode):X16}, " +
                      $"startOffset=0x{startOffset:x}, endOffset=0x{endOffset:x}, unwindSize=0x{unwindSize:x}, " +
                      $"pUnwindBlock=0x{(nuint)dspPtr(pUnwindBlock):X16}, funKind={(int)funcKind} ({functionDescription}))\n");
        }
#endif
        if (info.compMatchedVM)
        {
            info.compCompHnd->allocUnwindInfo(pHotCode, pColdCode, startOffset, endOffset, unwindSize, pUnwindBlock, funcKind);
        }
    }
#else
    public void unwindReserve()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Windows AMD64 unwind reservation is not implemented for this target.");
    }

    public unsafe void unwindEmit(void* pHotCode, void* pColdCode)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Windows AMD64 unwind publication is not implemented for this target.");
    }
#endif
}
