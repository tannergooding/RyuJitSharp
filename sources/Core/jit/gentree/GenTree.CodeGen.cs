// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class GenTree
{
#if TARGET_XARCH
    public void SetDontExtend()
    {
        assert(varTypeIsSmall(Type) && (Oper is GT_IND or GT_LCL_FLD));
        Flags |= GTF_DONT_EXTEND;
    }

    public void ClearDontExtend()
    {
        Flags &= ~GTF_DONT_EXTEND;
    }

    public bool DontExtend
    {
        get
        {
            assert(varTypeIsSmall(Type) || ((Flags & GTF_DONT_EXTEND) == 0));

            return (Flags & GTF_DONT_EXTEND) != 0;
        }
    }
#endif

#if DEBUG
    internal int UseNum
    {
        get
        {
            return _useNum;
        }

        set
        {
            _useNum = value;
        }
    }
#endif

    public regMaskTP ContainedRegMask
    {
        get
        {
            if (!IsContained)
            {
                return IsUsedFromReg ? RegMask : RBM_NONE;
            }

            var mask = RBM_NONE;
            foreach (var operand in Operands)
            {
                mask |= operand.ContainedRegMask;
            }

            return mask;
        }
    }

    public regMaskTP RegMask
    {
        get
        {
#if FEATURE_MULTIREG_RET
            if (IsMultiRegCall)
            {
                return regMaskTP.CreateFromRegNum(RegNum, RegNum.SingleTypeMask) | AsCall().GetOtherRegMask();
            }
            else if (IsCopyOrReloadOfMultiRegCall)
            {
                var copy = AsCopyOrReload();
                var count = copy.Op1.AsCall().ReturnTypeDesc.ReturnRegCount;
                var mask = RBM_NONE;

                // Copies and reloads may have holes for positions that need no move.
                for (byte i = 0; i < count; i++)
                {
                    var reg = copy.GetRegNumByIdx(i);
                    if (reg != REG_NA)
                    {
                        mask |= regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
                    }
                }

                return mask;
            }
#endif
            return regMaskTP.CreateFromRegNum(RegNum, RegNum.SingleTypeMask);
        }
    }
}
