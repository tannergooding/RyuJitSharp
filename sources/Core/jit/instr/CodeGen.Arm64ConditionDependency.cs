// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.insCond;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public static insCond JumpKindToInsCond(emitJumpKind condition)
    {
        if (!Configuration.TARGET_ARM64)
        {
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.ARM64ConditionMappingIsNotAvailableForThisTarget);
            return default!;
        }

        switch (condition)
        {
            case EJ_ARMARCH_eq:
            {
                return INS_COND_EQ;
            }
            case EJ_ARMARCH_ne:
            {
                return INS_COND_NE;
            }
            case EJ_ARMARCH_hs:
            {
                return INS_COND_HS;
            }
            case EJ_ARMARCH_lo:
            {
                return INS_COND_LO;
            }
            case EJ_ARMARCH_mi:
            {
                return INS_COND_MI;
            }
            case EJ_ARMARCH_pl:
            {
                return INS_COND_PL;
            }
            case EJ_ARMARCH_vs:
            {
                return INS_COND_VS;
            }
            case EJ_ARMARCH_vc:
            {
                return INS_COND_VC;
            }
            case EJ_ARMARCH_hi:
            {
                return INS_COND_HI;
            }
            case EJ_ARMARCH_ls:
            {
                return INS_COND_LS;
            }
            case EJ_ARMARCH_ge:
            {
                return INS_COND_GE;
            }
            case EJ_ARMARCH_lt:
            {
                return INS_COND_LT;
            }
            case EJ_ARMARCH_gt:
            {
                return INS_COND_GT;
            }
            case EJ_ARMARCH_le:
            {
                return INS_COND_LE;
            }
            default:
            {
                NO_WAY("unexpected condition type");
                return INS_COND_EQ;
            }
        }
    }
}
