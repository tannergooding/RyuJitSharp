// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64 && DEBUG
namespace RyuJitSharp;

public unsafe class UnwindCodesBase
{
    public uint GetCodeSizeFromUnwindCodes(bool isProlog)
    {
        var pCodesStart = GetCodes();
        var pCodes = pCodesStart;
        var size = 0u;
        for (;;)
        {
            var b1 = *pCodes;
            if (IsEndCode(b1))
            {
                break;
            }

            size += 4; // Each ARM64 unwind code represents one four-byte instruction.
            pCodes += Globals.GetUnwindSizeFromUnwindHeader(b1);
            assert(pCodes - pCodesStart < 256);
        }

        return size;
    }

    public virtual byte* GetCodes()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "UnwindCodesBase::GetCodes storage is not ported.");
    }

    public bool IsEndCode(byte code)
    {
        // The pinned ARM64 branch does not recognize end_c as a terminal code.
        return code == 0xE4;
    }
}
#endif
