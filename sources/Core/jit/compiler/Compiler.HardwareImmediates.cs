// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public bool CheckHWIntrinsicImmRange(NamedIntrinsic intrinsic, var_types baseType, GenTree immediate,
        bool mustExpand, int lowerBound, int upperBound, bool fullRange, out bool useFallback)
    {
        if (!Configuration.FEATURE_HW_INTRINSICS)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.HardwareIntrinsicRangeCheckingIsNotEnabled);
            useFallback = default!;
            return default!;
        }

        useFallback = false;
        if (!fullRange && immediate.Oper.IsCnsIntOrI)
        {
            var value = unchecked((int)immediate.AsIntCon().IconValue);
            var outOfRange = Configuration.TARGET_XARCH && HWIntrinsicInfo.isAVX2GatherIntrinsic(intrinsic)
                ? value is not 1 and not 2 and not 4 and not 8
                : (value < lowerBound) || (value > upperBound);
            if (outOfRange)
            {
                useFallback = Configuration.TARGET_ARM64
                    && (intrinsic is NI_AdvSimd_ShiftLeftLogical or NI_AdvSimd_ShiftLeftLogicalScalar or
                        NI_AdvSimd_ShiftRightLogical or NI_AdvSimd_ShiftRightLogicalScalar or
                        NI_AdvSimd_ShiftRightArithmetic or NI_AdvSimd_ShiftRightArithmeticScalar);
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
            if (Configuration.TARGET_XARCH && (flags & HW_Flag_MaybeNoJmpTableIMM) != 0)
            {
                if (Configuration.TARGET_X86 && varTypeIsLong(baseType))
                {
                    return mustExpand;
                }

                useFallback = true;
                return false;
            }
            if (!mustExpand)
            {
                return false;
            }
        }
        return true;
    }
}
