// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if LATE_DISASM
namespace RyuJitSharp;

public partial struct Disassembler
{
    private unsafe nuint disCchRegMember(void* pdis, int reg, char* wz, nuint cchMax)
    {
        // DIS::REGA is not regNumber (ARM64's W and X registers have separate ranges).
        // The pinned callback intentionally declines annotation until that mapping is fixed.
        _hasName = false;

        return 0;
    }
}
#endif
