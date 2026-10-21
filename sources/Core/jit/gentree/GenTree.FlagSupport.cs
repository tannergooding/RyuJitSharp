// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class GenTree
{
    public void SetOverflow()
    {
        assert(_oper.MayOverflow);
        Flags |= GTF_OVERFLOW;
    }

    public void ClearOverflow()
    {
        assert(_oper.MayOverflow);
        Flags &= ~GTF_OVERFLOW;
    }

    public void Set64RsltMul()
    {
        Flags |= GTF_MUL_64RSLT;
    }

    public void Clear64RsltMul()
    {
        Flags &= ~GTF_MUL_64RSLT;
    }

    public void ClearIconHandleMask()
    {
        assert(Oper is GT_CNS_INT);
        Flags &= ~GTF_ICON_HDL_MASK;
    }

    public bool IsEmbMaskOp()
    {
        return Configuration.FEATURE_HW_INTRINSICS && Oper.IsHWIntrinsic && ((Flags & GTF_HW_EM_OP) != 0);
    }

    public void MakeEmbMaskOp()
    {
        if (!Configuration.FEATURE_HW_INTRINSICS)
        {
            FatalJitException.ThrowFatalJitException("Embedded hardware-intrinsic masking requires hardware-intrinsic support.");
            return;
        }

        assert(Oper.IsHWIntrinsic);
        assert(!IsEmbMaskOp());
        Flags |= GTF_HW_EM_OP;
    }

    // A false result does not promise to preserve ZF. Callers consuming the
    // result's zero flag must mark the producer with GTF_SET_FLAGS.
    public bool SupportsSettingZeroFlag()
    {
        if (SupportsSettingFlagsAsCompareToZero())
        {
            return true;
        }

        if (Configuration.TARGET_XARCH)
        {
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

            if (Configuration.FEATURE_HW_INTRINSICS && (Oper is GT_HWINTRINSIC) &&
                Emitter.DoesWriteZeroFlagForResult(HWIntrinsicInfo.lookupIns(AsHWIntrinsic(), null)))
            {
                return true;
            }
        }
        else if (Configuration.TARGET_ARM64)
        {
            // Fused multiply-add/subtract operations cannot set the zero flag.
            if ((Oper is GT_NEG) && ((AsUnOp().Op1.Oper is not GT_MUL) || !AsUnOp().Op1.IsContained))
            {
                return true;
            }

            if ((Oper is GT_ADD or GT_SUB) && ((AsOp().Op2.Oper is not GT_MUL) || !AsOp().Op2.IsContained))
            {
                return true;
            }
        }

        return false;
    }

    public bool SupportsSettingFlagsAsCompareToZero()
    {
        return Configuration.TARGET_ARM64 && (Oper is GT_AND or GT_AND_NOT);
    }
}
