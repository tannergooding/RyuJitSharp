// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct RegSet
{
#if TARGET_ARMARCH || TARGET_RISCV64
    internal void rsSetCalleeSavedRegsMask(regMaskTP mask)
    {
        _rsMaskCalleeSaved = mask;
    }
#endif

#if TARGET_RISCV64
    internal readonly regMaskTP rsGetCalleeSavedRegsMask()
    {
        return _rsMaskCalleeSaved;
    }
#endif
}
