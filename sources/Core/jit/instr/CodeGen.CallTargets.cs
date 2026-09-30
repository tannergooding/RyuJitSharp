// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
#if TARGET_ARMARCH
    public unsafe bool validImmForBL(nint address)
    {
#if TARGET_ARM
        return (!_compiler.info.compMatchedVM && _compiler.IsAot) ||
            (_compiler.eeGetRelocTypeHint((void*)address) is CorInfoReloc.ARM32_THUMB_BRANCH24);
#else
        // ARM64_BRANCH26 relocations use a VM-provided jump stub if the target is out of range.
        return true;
#endif
    }
#endif
}
