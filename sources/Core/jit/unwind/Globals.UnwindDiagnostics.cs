// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class Globals
{
    // start is a zero-based bit index from the least significant bit.
    public static uint ExtractBits(uint value, uint start, uint length)
    {
        if (Configuration.TARGET_LOONGARCH64)
        {
            return (value >> unchecked((int)start)) & unchecked((1u << unchecked((int)length)) - 1);
        }

        return (value >> (int)start) & unchecked((uint)((1 << (int)length) - 1));
    }

    private static regNumber GetUnwindRegisterAtOffset(regNumber first, uint offset)
    {
        return unchecked((regNumber)((uint)first + offset));
    }

    public static uint GetUnwindSizeFromUnwindHeader(byte header)
    {
        if (Configuration.TARGET_ARM64)
        {
            return GetArm64UnwindSize(header);
        }

        if (Configuration.TARGET_RISCV64)
        {
            return GetRiscV64UnwindSize(header);
        }

        if (Configuration.TARGET_ARM || Configuration.TARGET_LOONGARCH64)
        {
            return UnwindInfo.GetUnwindCodeSize(header);
        }

        FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.UnwindOpcodeSizeDecodingForThisTargetIsNotPorted
    );
        return default!;
    }

    [System.Diagnostics.Conditional("DEBUG")]
    public static unsafe void DumpUnwindInfo(Compiler comp, bool isHotCode, uint startOffset,
        uint endOffset, byte* pHeader, uint unwindBlockSize)
    {
        if (Configuration.TARGET_ARM)
        {
            DumpArmUnwindInfo(comp, isHotCode, startOffset, endOffset, pHeader, unwindBlockSize);
        }
        else if (Configuration.TARGET_ARM64)
        {
            DumpArm64UnwindInfo(comp, isHotCode, startOffset, endOffset, pHeader, unwindBlockSize);
        }
        else if (Configuration.TARGET_LOONGARCH64)
        {
            DumpLoongArch64UnwindInfo(comp, isHotCode, startOffset, endOffset, pHeader, unwindBlockSize);
        }
        else if (Configuration.TARGET_RISCV64)
        {
            DumpRiscV64UnwindInfo(comp, isHotCode, startOffset, endOffset, pHeader, unwindBlockSize);
        }
        else
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.UnwindDiagnosticDecodingForThisTargetIsNotPorted
    );
            return;
        }
    }
}
