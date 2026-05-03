// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct regMaskTP
{
    private regMaskInt _intMask;
    private regMaskFlt _fltMask;

#if FEATURE_MASKED_HW_INTRINSICS
    private regMaskMsk _mskMask;
#endif
}
