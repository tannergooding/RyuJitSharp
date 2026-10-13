// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public partial class Compiler
{
    public void unwindReserveAmd64()
    {
        var emitter = codeGen?.Emitter ?? throw new InvalidOperationException("An emitter is required to reserve unwind information.");
        assert(!emitter.emitGeneratingPrologOrFuncletProlog());
        assert(!emitter.emitGeneratingEpilogOrFuncletEpilog());

        var funcs = Funcs;
        for (var index = 0; index < funcs.Length; index++)
        {
            unwindReserveFuncAmd64(ref compFuncInfos[index]);
        }
    }

    private void unwindReserveFuncAmd64(ref FuncInfoDsc func)
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
            if (Configuration.UNIX_AMD64_ABI && generateCFIUnwindCodes())
            {
                noway_assert(func.cfiCodes is not null);
                unwindCodeBytes = unchecked(func.cfiCodes.Count * 8);
            }
            else
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
        }

        eeReserveUnwindInfoAmd64(isFunclet, !isHotCode, unwindCodeBytes);
    }

    public unsafe void unwindEmitAmd64(void* pHotCode, void* pColdCode)
    {
        var emitter = codeGen?.Emitter ?? throw new InvalidOperationException("An emitter is required to publish unwind information.");
        assert(!emitter.emitGeneratingPrologOrFuncletProlog());
        assert(!emitter.emitGeneratingEpilogOrFuncletEpilog());

        foreach (ref readonly var func in Funcs)
        {
            unwindEmitFuncAmd64(in func, pHotCode, pColdCode);
        }
    }

    private unsafe void unwindEmitFuncAmd64(in FuncInfoDsc func, void* pHotCode, void* pColdCode)
    {
#if DEBUG
        if (JitConfig.JitFakeProcedureSplitting != 0)
        {
            unwindEmitFuncHelperAmd64(in func, pHotCode, pColdCode, true);
            return;
        }
#endif

        if (func.funKind == FuncKind.FUNC_ROOT)
        {
            unwindEmitFuncHelperAmd64(in func, pHotCode, pColdCode, true);

            if ((fgFirstColdBlock is not null) && (fgFirstColdBlock != fgFirstFuncletBB))
            {
                unwindEmitFuncHelperAmd64(in func, pHotCode, pColdCode, false);
            }
        }
        else
        {
            unwindEmitFuncHelperAmd64(in func, pHotCode, pColdCode, fgFirstColdBlock is null);
        }
    }

    private unsafe void unwindEmitFuncHelperAmd64(in FuncInfoDsc func, void* pHotCode, void* pColdCode, bool isHotCode)
    {
        var emitter = codeGen?.Emitter ?? throw new InvalidOperationException("An emitter is required to resolve unwind locations.");
        if (!isHotCode)
        {
            assert(fgFirstColdBlock is not null);
        }

        var startLocation = isHotCode ? func.startLoc : func.coldStartLoc;
        var endLocation = isHotCode ? func.endLoc : func.coldEndLoc;
        var startOffset = startLocation?.CodeOffset(emitter) ?? 0u;
        var endOffset = endLocation?.CodeOffset(emitter) ?? checked((uint)info.compNativeCodeSize);

        if (Configuration.UNIX_AMD64_ABI && generateCFIUnwindCodes())
        {
            var codes = (isHotCode || (func.funKind != FuncKind.FUNC_ROOT)) ? func.cfiCodes : null;
            noway_assert(!(isHotCode || (func.funKind != FuncKind.FUNC_ROOT)) || codes is not null);
            var cfiCodes = codes is null ? default : CollectionsMarshal.AsSpan(codes);
            var unwindCodeBytes = unchecked(cfiCodes.Length * 8);

#if DEBUG
            if (opts.dspUnwind)
            {
                DumpCfiInfo(isHotCode, startOffset, endOffset, cfiCodes);
            }
#endif

            if (isHotCode)
            {
#if DEBUG
                if ((JitConfig.JitFakeProcedureSplitting != 0) && (fgFirstColdBlock is not null))
                {
                    assert(endOffset <= info.compNativeCodeSize);
                }
                else
#endif
                {
                    assert(endOffset <= info.compTotalHotCodeSize);
                }

                pColdCode = null;
            }
            else
            {
                assert(startOffset >= info.compTotalHotCodeSize);
                startOffset = unchecked(startOffset - (uint)info.compTotalHotCodeSize);
                endOffset = unchecked(endOffset - (uint)info.compTotalHotCodeSize);
            }

            // The EE consumes the vector during the call; keep its backing array pinned.
            fixed (CFI_CODE* pCfiCodes = cfiCodes)
            {
                eeAllocUnwindInfo((byte*)pHotCode, (byte*)pColdCode, unchecked((int)startOffset), unchecked((int)endOffset),
                    unwindCodeBytes, (byte*)pCfiCodes, (CorJitFuncKind)func.funKind);
            }
        }
        else
        {
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
    }

    private unsafe void eeReserveUnwindInfoAmd64(bool isFunclet, bool isColdCode, int unwindSize)
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

}
