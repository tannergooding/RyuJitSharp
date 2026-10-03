// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.insCond;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public static insCond JumpKindToInsCond(emitJumpKind condition)
    {
        switch (condition)
        {
            case EJ_eq:
            {
                return INS_COND_EQ;
            }
            case EJ_ne:
            {
                return INS_COND_NE;
            }
            case EJ_hs:
            {
                return INS_COND_HS;
            }
            case EJ_lo:
            {
                return INS_COND_LO;
            }
            case EJ_mi:
            {
                return INS_COND_MI;
            }
            case EJ_pl:
            {
                return INS_COND_PL;
            }
            case EJ_vs:
            {
                return INS_COND_VS;
            }
            case EJ_vc:
            {
                return INS_COND_VC;
            }
            case EJ_hi:
            {
                return INS_COND_HI;
            }
            case EJ_ls:
            {
                return INS_COND_LS;
            }
            case EJ_ge:
            {
                return INS_COND_GE;
            }
            case EJ_lt:
            {
                return INS_COND_LT;
            }
            case EJ_gt:
            {
                return INS_COND_GT;
            }
            case EJ_le:
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
#endif
