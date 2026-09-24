// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class GenTree
{
    // A false result does not promise to preserve ZF. Callers consuming the
    // result's zero flag must mark the producer with GTF_SET_FLAGS.
    public bool SupportsSettingZeroFlag()
    {
        if (SupportsSettingFlagsAsCompareToZero())
        {
            return true;
        }

#if TARGET_XARCH
        if (Oper is GT_LSH or GT_RSH or GT_RSZ)
        {
            // A zero shift count leaves the flags unchanged.
            return AsOp().Op2.IsNeverZero();
        }

        // ROL/ROR leave ZF unchanged even for a nonzero rotate count.
        if (Oper is GT_AND or GT_OR or GT_XOR or GT_ADD or GT_SUB or GT_NEG)
        {
            return true;
        }

#if FEATURE_HW_INTRINSICS
        if ((Oper is GT_HWINTRINSIC) &&
            Emitter.DoesWriteZeroFlagForResult(HWIntrinsicInfo.lookupIns(AsHWIntrinsic(), null)))
        {
            return true;
        }
#endif
#elif TARGET_ARM64
        // Fused multiply-add/subtract operations cannot set the zero flag.
        if ((Oper is GT_NEG) && ((AsUnOp().Op1.Oper is not GT_MUL) || !AsUnOp().Op1.IsContained))
        {
            return true;
        }

        if ((Oper is GT_ADD or GT_SUB) && ((AsOp().Op2.Oper is not GT_MUL) || !AsOp().Op2.IsContained))
        {
            return true;
        }
#endif

        return false;
    }

    public bool SupportsSettingFlagsAsCompareToZero()
    {
#if TARGET_ARM64
        return Oper is GT_AND or GT_AND_NOT;
#else
        return false;
#endif
    }
}
