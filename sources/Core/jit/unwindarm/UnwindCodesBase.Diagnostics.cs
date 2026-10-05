// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM && DEBUG
namespace RyuJitSharp;

public abstract unsafe class UnwindCodesBase
{
    public uint GetCodeSizeFromUnwindCodes(bool isProlog)
    {
        var codesStart = GetCodes();
        var codes = codesStart;
        var size = 0u;

        for (;;)
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
#endif
