// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64
namespace RyuJitSharp;

public partial class Compiler
{
    internal unsafe void unwindReserveEEInfo(bool isFunclet, bool isColdCode, int unwindSize)
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

    internal unsafe void unwindAllocEEInfo(byte* hotCode, byte* coldCode, int startOffset, int endOffset,
        int unwindSize, byte* unwindBlock, CorJitFuncKind functionKind)
    {
#if DEBUG
        if (verbose)
        {
            var functionDescription = functionKind switch
            {
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
#endif
        if (info.compMatchedVM)
        {
            info.compCompHnd->allocUnwindInfo(hotCode, coldCode, startOffset, endOffset, unwindSize,
                unwindBlock, functionKind);
        }
    }
}
#endif
