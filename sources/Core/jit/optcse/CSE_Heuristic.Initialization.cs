// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public sealed partial class CSE_Heuristic
{
    private weight_t aggressiveRefCnt;
    private weight_t moderateRefCnt;
    private int enregCountInt;
    private int enregCountFlt;
    private int enregCountMsk;
    private bool largeFrame;
    private bool hugeFrame;

    private int CntAggressiveEnreg => (CNT_CALLEE_ENREG * 3) / 2;
    private int CntModerateEnreg => (CNT_CALLEE_ENREG * 3) + (m_compiler.CNT_CALLEE_TRASH_INT * 2);
    private int CntAggressiveEnregFlt => (CNT_CALLEE_ENREG_FLOAT * 3) / 2;
    private int CntModerateEnregFlt => (CNT_CALLEE_ENREG_FLOAT * 3) + (m_compiler.CNT_CALLEE_TRASH_FLOAT * 2);
    private int CntAggressiveEnregMsk => (CNT_CALLEE_ENREG_MASK * 3) / 2;
    private int CntModerateEnregMsk => (CNT_CALLEE_ENREG_MASK * 3) + (m_compiler.CNT_CALLEE_TRASH_MASK * 2);

    public override void Initialize()
    {
        uint frameSize = 0;
        var regAvailEstimateInt = unchecked((uint)(CntModerateEnreg + 1));
        var regAvailEstimateFlt = unchecked((uint)(CntModerateEnregFlt + 1));
        var regAvailEstimateMsk = unchecked((uint)(CntModerateEnregMsk + 1));

        for (var lclNum = 0; lclNum < m_compiler.lvaCount; lclNum++)
        {
            ref var varDsc = ref m_compiler.lvaGetDesc(lclNum);

            if ((varDsc.lvRefCnt() == 0) || (varDsc.lvIsParam && !varDsc.lvIsRegArg))
            {
                continue;
            }

#if FEATURE_FIXED_OUT_ARGS
            noway_assert(m_compiler.lvaOutgoingArgSpaceVar != BAD_VAR_NUM);
            if (lclNum == m_compiler.lvaOutgoingArgSpaceVar)
            {
                continue;
            }
#endif

            var varType = varDsc.Type;
            var registerClass = varTypeUsesIntReg(varType) ? 0 : varTypeUsesMaskReg(varType) ? 1 : 2;
            assert((registerClass != 2) || varTypeUsesFloatReg(varType));

            var regAvailEstimate = registerClass switch
            {
                0 => regAvailEstimateInt,
                1 => regAvailEstimateMsk,
                _ => regAvailEstimateFlt,
            };

            var onStack = (regAvailEstimate == 0) || varDsc.lvDoNotEnregister;
            if (onStack && !varTypeHasUnknownSize(varType))
            {
                frameSize += unchecked((uint)m_compiler.lvaLclStackHomeSize(lclNum));
            }
            else
            {
                if (varDsc.lvRefCnt() <= 2)
                {
                    regAvailEstimate = unchecked(regAvailEstimate - 1);
                }
                else
                {
                    regAvailEstimate = regAvailEstimate >= 2 ? regAvailEstimate - 2 : 0;
                }

                switch (registerClass)
                {
                    case 0:
                    {
                        regAvailEstimateInt = regAvailEstimate;
                        break;
                    }
                    case 1:
                    {
                        regAvailEstimateMsk = regAvailEstimate;
                        break;
                    }
                    default:
                    {
                        regAvailEstimateFlt = regAvailEstimate;
                        break;
                    }
                }
            }

            if (frameSize > 0x80)
            {
                largeFrame = true;
                break;
            }
        }

        // Tracked locals are ordered by weighted reference count for LSRA.
        for (var trackedIndex = 0; trackedIndex < m_compiler.lvaTrackedCount; trackedIndex++)
        {
            assert(m_compiler.lvaTrackedToVarNum is not null);
            ref var varDsc = ref m_compiler.lvaGetDesc(m_compiler.lvaTrackedToVarNum[trackedIndex]);
            if ((varDsc.lvRefCnt() == 0) || varDsc.lvDoNotEnregister)
            {
                continue;
            }

            var varType = varDsc.Type;
            int enregCount;
            int cntAggressiveEnreg;
            int cntModerateEnreg;

            if (varTypeUsesIntReg(varType))
            {
                enregCount = ++enregCountInt;
                cntAggressiveEnreg = CntAggressiveEnreg;
                cntModerateEnreg = CntModerateEnreg;
            }
            else if (varTypeUsesMaskReg(varType))
            {
                enregCount = ++enregCountMsk;
                cntAggressiveEnreg = CntAggressiveEnregMsk;
                cntModerateEnreg = CntModerateEnregMsk;
            }
            else
            {
                assert(varTypeUsesFloatReg(varType));
                enregCount = ++enregCountFlt;
                cntAggressiveEnreg = CntAggressiveEnregFlt;
                cntModerateEnreg = CntModerateEnregFlt;
            }

            var refCount = codeOptKind is Compiler.SMALL_CODE ? varDsc.lvRefCnt() : varDsc.lvRefCntWtd();
            if ((aggressiveRefCnt == 0) && (enregCount > cntAggressiveEnreg))
            {
                aggressiveRefCnt = refCount + BB_UNITY_WEIGHT;
            }

            if ((moderateRefCnt == 0) && (enregCount > cntModerateEnreg))
            {
                moderateRefCnt = refCount + (BB_UNITY_WEIGHT / 2);
            }
        }

        aggressiveRefCnt = Math.Max(BB_UNITY_WEIGHT / 2, aggressiveRefCnt);
        moderateRefCnt = Math.Max(BB_UNITY_WEIGHT, moderateRefCnt);

#if DEBUG
        if (m_compiler.verbose)
        {
            JITDUMP($"\nAggressive CSE Promotion cutoff is {formatFloat(aggressiveRefCnt, "F6")}\n");
            JITDUMP($"Moderate CSE Promotion cutoff is {formatFloat(moderateRefCnt, "F6")}\n");
            JITDUMP($"enregCountInt is {enregCountInt}\n");
            JITDUMP($"enregCountFlt is {enregCountFlt}\n");
            JITDUMP($"enregCountMsk is {enregCountMsk}\n");
            JITDUMP($"Framesize estimate is 0x{frameSize:X4}\n");
            JITDUMP($"We have a {(hugeFrame ? "huge" : largeFrame ? "large" : "small")} frame\n");
        }
#endif
    }
}
