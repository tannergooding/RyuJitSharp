// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public abstract unsafe class ArmUnwindCodesBase
{
    public uint GetCodeSizeFromUnwindCodes(bool isProlog)
    {
        if (!Configuration.DEBUG)
        {
            throw new System.InvalidOperationException("ARM32 unwind diagnostics are only available in Debug builds.");
        }

        if (!Configuration.TARGET_ARM)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "ARM32 unwind diagnostics require ARM32.");
            return default!;
        }

        var codesStart = GetCodes();
        var codes = codesStart;
        var size = 0u;

        for (; ; )
        {
            var header = *codes;
            if (header >= 0xFD)
            {
                if (!isProlog && (header == 0xFD || header == 0xFE))
                {
                    size += UnwindInfo.GetArmOpcodeSize(header);
                }

                break;
            }

            size += UnwindInfo.GetArmOpcodeSize(header);
            codes += UnwindInfo.GetUnwindCodeSize(header);
            assert(codes - codesStart < 256);
        }

        return size;
    }

    public abstract byte* GetCodes();
}
