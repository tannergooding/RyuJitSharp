// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    internal unsafe void unwindReserveEEInfo(bool isFunclet, bool isColdCode, int unwindSize)
    {
        RequireUnwindEETarget();

        if (Configuration.DEBUG && verbose)
        {
            jitprintf($"reserveUnwindInfo(isFunclet={(isFunclet ? "true" : "false")}, isColdCode={(isColdCode ? "true" : "false")}, unwindSize=0x{unwindSize:x})\n");
        }
        if (info.compMatchedVM)
        {
            info.compCompHnd->reserveUnwindInfo(isFunclet, isColdCode, unwindSize);
        }
    }

    internal unsafe void unwindAllocEEInfo(byte* hotCode, byte* coldCode, int startOffset, int endOffset,
        int unwindSize, byte* unwindBlock, CorJitFuncKind functionKind)
    {
        RequireUnwindEETarget();
        AllocateUnwindEEInfo(hotCode, coldCode, startOffset, endOffset, unwindSize, unwindBlock, functionKind);
    }

    private unsafe void eeAllocUnwindInfo(byte* hotCode, byte* coldCode, int startOffset, int endOffset,
        int unwindSize, byte* unwindBlock, CorJitFuncKind functionKind)
    {
        if (!Configuration.TARGET_XARCH && !Configuration.TARGET_WASM)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Xarch/Wasm unwind EE publication requires xarch or Wasm.");
            return;
        }

        AllocateUnwindEEInfo(hotCode, coldCode, startOffset, endOffset, unwindSize, unwindBlock, functionKind);
    }

    private unsafe void AllocateUnwindEEInfo(byte* hotCode, byte* coldCode, int startOffset, int endOffset,
        int unwindSize, byte* unwindBlock, CorJitFuncKind functionKind)
    {
        if (Configuration.DEBUG && verbose)
        {
            var functionDescription = functionKind switch {
                CorJitFuncKind.CORJIT_FUNC_ROOT => "main function",
                CorJitFuncKind.CORJIT_FUNC_HANDLER => "handler",
                CorJitFuncKind.CORJIT_FUNC_FILTER => "filter",
                _ => "ILLEGAL",
            };
            jitprintf($"allocUnwindInfo(pHotCode=0x{(nuint)dspPtr(hotCode):X16}, " +
                      $"pColdCode=0x{(nuint)dspPtr(coldCode):X16}, startOffset=0x{startOffset:x}, " +
                      $"endOffset=0x{endOffset:x}, unwindSize=0x{unwindSize:x}, " +
                      $"pUnwindBlock=0x{(nuint)dspPtr(unwindBlock):X16}, funKind={(int)functionKind} ({functionDescription}))\n");
        }
        if (info.compMatchedVM)
        {
            info.compCompHnd->allocUnwindInfo(hotCode, coldCode, startOffset, endOffset, unwindSize,
                unwindBlock, functionKind);
        }
    }

    private static void RequireUnwindEETarget()
    {
        if (!Configuration.TARGET_ARMARCH && !Configuration.TARGET_LOONGARCH64 && !Configuration.TARGET_RISCV64)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Unwind EE publication for this target is not ported.");
            return;
        }
    }
}
