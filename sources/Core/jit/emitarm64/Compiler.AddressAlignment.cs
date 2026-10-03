// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

public partial class Compiler
{
    public unsafe uint eeGetAddressAlignment(void* address)
    {
        if (info.compMatchedVM)
        {
            return info.compCompHnd->getAddressAlignment(address);
        }

        // A mismatched VM cannot guarantee target-specific data alignment.
        return 1;
    }
}
#endif
