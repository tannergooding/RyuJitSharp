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
        global::RyuJitSharp.Emitter __jitExceptionResult715;
        var __jitExceptionCoalesceLeft716 = codeGen?.Emitter;
        if (__jitExceptionCoalesceLeft716 != null)
        {
            __jitExceptionResult715 = __jitExceptionCoalesceLeft716;
        }
        else
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.EmitterIsRequiredToReserveUnwindInformation);
            __jitExceptionResult715 = default;
        }
        var emitter = __jitExceptionResult715;
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
        if (Configuration.DEBUG && (JitConfig.JitFakeProcedureSplitting != 0))
        {
            unwindReserveFuncHelper(ref func, true);
            return;
        }

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
                byte[] __jitExceptionResult717;
                var __jitExceptionCoalesceLeft718 = func.unwindCodes;
                if (__jitExceptionCoalesceLeft718 != null)
                {
                    __jitExceptionResult717 = __jitExceptionCoalesceLeft718;
                }
                else
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.PrologUnwindCodesHaveNotBeenRecorded);
                    __jitExceptionResult717 = default;
                }
                var codes = __jitExceptionResult717;
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
        global::RyuJitSharp.Emitter __jitExceptionResult719;
        var __jitExceptionCoalesceLeft720 = codeGen?.Emitter;
        if (__jitExceptionCoalesceLeft720 != null)
        {
            __jitExceptionResult719 = __jitExceptionCoalesceLeft720;
        }
        else
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.EmitterIsRequiredToPublishUnwindInformation);
            __jitExceptionResult719 = default;
        }
        var emitter = __jitExceptionResult719;
        assert(!emitter.emitGeneratingPrologOrFuncletProlog());
        assert(!emitter.emitGeneratingEpilogOrFuncletEpilog());

        foreach (ref readonly var func in Funcs)
        {
            unwindEmitFuncAmd64(in func, pHotCode, pColdCode);
        }
    }

    private unsafe void unwindEmitFuncAmd64(in FuncInfoDsc func, void* pHotCode, void* pColdCode)
    {
        if (Configuration.DEBUG && (JitConfig.JitFakeProcedureSplitting != 0))
        {
            unwindEmitFuncHelperAmd64(in func, pHotCode, pColdCode, true);
            return;
        }

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
        global::RyuJitSharp.Emitter __jitExceptionResult721;
        var __jitExceptionCoalesceLeft722 = codeGen?.Emitter;
        if (__jitExceptionCoalesceLeft722 != null)
        {
            __jitExceptionResult721 = __jitExceptionCoalesceLeft722;
        }
        else
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.EmitterIsRequiredToResolveUnwindLocations);
            __jitExceptionResult721 = default;
        }
        var emitter = __jitExceptionResult721;
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

            if (Configuration.DEBUG && dspUnwind)
            {
                DumpCfiInfo(isHotCode, startOffset, endOffset, cfiCodes);
            }

            if (isHotCode)
            {
                if (Configuration.DEBUG && (JitConfig.JitFakeProcedureSplitting != 0) &&
                    (fgFirstColdBlock is not null))
                {
                    assert(endOffset <= info.compNativeCodeSize);
                }
                else
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
                var __jitExceptionCoalesceLeft723 = func.unwindCodes;
                if (__jitExceptionCoalesceLeft723 != null)
                {
                    unwindCodes = __jitExceptionCoalesceLeft723;
                }
                else
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.PrologUnwindCodesHaveNotBeenReserved);
                    unwindCodes = default;
                }
                noway_assert(func.unwindCodeSlot <= unwindCodes.Length - 4);
                unwindCodeBytes = unwindCodes.Length - (int)func.unwindCodeSlot;
                noway_assert(unwindCodeBytes == 4 + (unwindCodes[func.unwindCodeSlot + 2] * 2));
            }

            if (Configuration.DEBUG && dspUnwind)
            {
                DumpUnwindInfo(isHotCode, startOffset, endOffset, unwindCodes, func.unwindCodeSlot);
            }

            if (isHotCode)
            {
                if (Configuration.DEBUG && (JitConfig.JitFakeProcedureSplitting != 0) &&
                    (fgFirstColdBlock is not null))
                {
                    noway_assert(endOffset <= info.compNativeCodeSize);
                }
                else
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
        if (Configuration.DEBUG && verbose)
        {
            jitprintf($"reserveUnwindInfo(isFunclet={(isFunclet ? "true" : "false")}, isColdCode={(isColdCode ? "true" : "false")}, unwindSize=0x{unwindSize:x})\n");
        }
        if (info.compMatchedVM)
        {
            info.compCompHnd->reserveUnwindInfo(isFunclet, isColdCode, unwindSize);
        }
    }
}
