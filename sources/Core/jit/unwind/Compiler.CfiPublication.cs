// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_CFI_SUPPORT
using System;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public partial class Compiler
{
    private unsafe void unwindEmitFuncCFI(in FuncInfoDsc func, void* pHotCode, void* pColdCode)
    {
        var emitter = UnwindEmitter();
        var startOffset = func.startLoc?.CodeOffset(emitter) ?? 0;
        var endOffset = func.endLoc?.CodeOffset(emitter) ?? unchecked((uint)info.compNativeCodeSize);
        noway_assert(func.cfiCodes is not null);
        var codes = CollectionsMarshal.AsSpan(func.cfiCodes);
        var unwindCodeBytes = unchecked(codes.Length * 8);

#if DEBUG
        if (opts.dspUnwind)
        {
            DumpCfiInfo(true, startOffset, endOffset, codes);
        }
#endif

        assert(endOffset <= info.compTotalHotCodeSize);
        fixed (CFI_CODE* pCodes = codes)
        {
            CfiAllocateUnwindInfo((byte*)pHotCode, null, startOffset, endOffset, unwindCodeBytes,
                (byte*)pCodes, (CorJitFuncKind)func.funKind);
        }

        if (pColdCode != null)
        {
            assert(fgFirstColdBlock is not null);
            startOffset = func.coldStartLoc?.CodeOffset(emitter) ?? 0;
            endOffset = func.coldEndLoc?.CodeOffset(emitter) ?? unchecked((uint)info.compNativeCodeSize);

#if DEBUG
            if (opts.dspUnwind)
            {
                DumpCfiInfo(false, startOffset, endOffset, default);
            }
#endif

            assert(startOffset >= info.compTotalHotCodeSize);
            startOffset = unchecked(startOffset - (uint)info.compTotalHotCodeSize);
            endOffset = unchecked(endOffset - (uint)info.compTotalHotCodeSize);
            CfiAllocateUnwindInfo((byte*)pHotCode, (byte*)pColdCode, startOffset, endOffset, 0, null,
                (CorJitFuncKind)func.funKind);
        }
    }

    private unsafe void CfiAllocateUnwindInfo(byte* hotCode, byte* coldCode, uint startOffset, uint endOffset,
        int unwindSize, byte* unwindBlock, CorJitFuncKind funcKind)
    {
#if TARGET_AMD64
        eeAllocUnwindInfo(hotCode, coldCode, unchecked((int)startOffset), unchecked((int)endOffset),
            unwindSize, unwindBlock, funcKind);
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Unwind EE publication for this target is not ported.");
#endif
    }

#if DEBUG
    private void DumpCfiInfo(bool isHotCode, uint startOffset, uint endOffset, ReadOnlySpan<CFI_CODE> codes)
    {
        jitprintf($"Cfi Info{(isHotCode ? "" : " COLD")}:\n");
        jitprintf($"  >> Start offset   : 0x{dspOffset((nint)startOffset):x6} \n");
        jitprintf($"  >>   End offset   : 0x{dspOffset((nint)endOffset):x6} \n");

        foreach (ref readonly var code in codes)
        {
            switch (code.CfiOpCode)
            {
                case CFI_REL_OFFSET:
                {
                    jitprintf($"    CodeOffset: 0x{code.CodeOffset:X2} Op: RelOffset DwarfReg:0x{unchecked((uint)code.DwarfReg):x} Offset:0x{unchecked((uint)code.Offset):X}\n");
                    break;
                }
                case CFI_DEF_CFA_REGISTER:
                {
                    assert(code.Offset == 0);
                    jitprintf($"    CodeOffset: 0x{code.CodeOffset:X2} Op: DefCfaRegister DwarfReg:0x{unchecked((uint)code.DwarfReg):X}\n");
                    break;
                }
                case CFI_ADJUST_CFA_OFFSET:
                {
                    assert(code.DwarfReg == DWARF_REG_ILLEGAL);
                    jitprintf($"    CodeOffset: 0x{code.CodeOffset:X2} Op: AdjustCfaOffset Offset:0x{unchecked((uint)code.Offset):X}\n");
                    break;
                }
                case CFI_NEGATE_RA_STATE:
                {
                    assert(code.DwarfReg == DWARF_REG_ILLEGAL);
                    assert(code.Offset == 0);
                    jitprintf($"    CodeOffset: 0x{code.CodeOffset:X2} Op: NegateRAState\n");
                    break;
                }
                default:
                {
                    var raw = MemoryMarshal.Read<ulong>(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(in code, 1)));
                    jitprintf($"    Unrecognized CFI_CODE: 0x{raw:X}\n");
                    break;
                }
            }
        }
    }
#endif
}
#endif
