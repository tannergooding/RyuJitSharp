// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64 && DEBUG
namespace RyuJitSharp;

public abstract unsafe class UnwindCodesBase
{
    public abstract void AddCode(byte b1);
    public abstract void AddCode(byte b1, byte b2);
    public abstract void AddCode(byte b1, byte b2, byte b3);
    public abstract void AddCode(byte b1, byte b2, byte b3, byte b4);
    public abstract byte* GetCodes();

    public bool IsEndCode(byte code)
    {
        return code == 0xE4;
    }

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

            size += 4;
            pCodes += Globals.GetUnwindSizeFromUnwindHeader(b1);
            assert(pCodes - pCodesStart < 256);
        }

        return size;
    }
}
#endif
