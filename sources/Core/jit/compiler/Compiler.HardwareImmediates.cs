// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
#if FEATURE_HW_INTRINSICS
    public bool CheckHWIntrinsicImmRange(NamedIntrinsic intrinsic, var_types baseType, GenTree immediate,
        bool mustExpand, int lowerBound, int upperBound, bool fullRange, out bool useFallback)
    {
        useFallback = false;
        if (!fullRange && immediate.Oper.IsCnsIntOrI)
        {
            var value = unchecked((int)immediate.AsIntCon().IconValue);
#if TARGET_XARCH
            var outOfRange = HWIntrinsicInfo.isAVX2GatherIntrinsic(intrinsic)
                ? value is not 1 and not 2 and not 4 and not 8
                : (value < lowerBound) || (value > upperBound);
#else
            var outOfRange = (value < lowerBound) || (value > upperBound);
#endif
            if (outOfRange)
            {
#if TARGET_ARM64
                useFallback = intrinsic is NI_AdvSimd_ShiftLeftLogical or NI_AdvSimd_ShiftLeftLogicalScalar or
                    NI_AdvSimd_ShiftRightLogical or NI_AdvSimd_ShiftRightLogicalScalar or
                    NI_AdvSimd_ShiftRightArithmetic or NI_AdvSimd_ShiftRightArithmeticScalar;
#endif
                return false;
            }
        }
        else if (!immediate.Oper.IsCnsIntOrI)
        {
            var flags = HWIntrinsicInfo.lookupFlags(intrinsic);
            if ((flags & HW_Flag_NoJmpTableIMM) != 0)
            {
                useFallback = true;
                return false;
            }
#if TARGET_XARCH
            if ((flags & HW_Flag_MaybeNoJmpTableIMM) != 0)
            {
#if TARGET_X86
                if (varTypeIsLong(baseType))
                {
                    return mustExpand;
                }
#endif
                useFallback = true;
                return false;
            }
#endif
            if (!mustExpand)
            {
                return false;
            }
        }
        return true;
    }
#endif
}
