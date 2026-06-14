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

    public readonly bool IsSet(regNumber regNum)
    {
        if (regNum.IsIntReg)
        {
            return (_intMask & (regMaskInt)(1 << (regNum - REG_INT_FIRST))) is not 0;
        }

#if FEATURE_MASKED_HW_INTRINSICS
        if (regNum.IsMskReg)
        {
            assert(regNum.IsMskReg);
            return (_mskMask & (regMaskMsk)(1 << (regNum - REG_MASK_FIRST))) is not 0;
        }
#endif

        assert(regNum.IsFltReg);
        return (_fltMask & (regMaskFlt)(1 << (regNum - REG_FP_FIRST))) is not 0;
    }
}
