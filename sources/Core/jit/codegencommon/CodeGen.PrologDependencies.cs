// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM || TARGET_ARM64
namespace RyuJitSharp;

public sealed partial class CodeGen
{
#if TARGET_ARM
    private regMaskTP genStackAllocRegisterMask(uint frameSize, regMaskTP maskCalleeSavedFloat)
    {
        return genStackAllocRegisterMaskArmCore(frameSize, maskCalleeSavedFloat);
    }
#endif
}
#endif
