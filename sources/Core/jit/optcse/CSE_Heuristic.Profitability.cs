// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed partial class CSE_Heuristic
{
    public override bool PromotionCheck(CSE_Candidate candidate)
    {
#if DEBUG
        if (m_compiler.optConfigDisableCSE2())
        {
            return false;
        }
#endif

        uint cseDefCost;
        uint cseUseCost;
        uint extraYesCost = 0;
        uint extraNoCost = 0;

        // A CSE definition references the promoted local twice; a use references it once.
        var cseRefCnt = (candidate.DefCount() * 2) + candidate.UseCount();
        var expr = candidate.Expr();
        var canEnregister = true;
        uint slotCount = 1;
        var enregCount = 0;
        var cntAggressiveEnreg = 0;

        if (expr.Type is TYP_STRUCT)
        {
            canEnregister = false;
            var size = expr.GetLayout(m_compiler).Size;
            slotCount = (size + TARGET_POINTER_SIZE - 1) / TARGET_POINTER_SIZE;
        }
        else if (varTypeUsesIntReg(expr.Type))
        {
            enregCount = enregCountInt;
            cntAggressiveEnreg = CntAggressiveEnreg;
        }
        else if (varTypeUsesMaskReg(expr.Type))
        {
            enregCount = enregCountMsk;
            cntAggressiveEnreg = CntAggressiveEnregMsk;
        }
        else
        {
            assert(varTypeUsesFloatReg(expr.Type));
            enregCount = enregCountFlt;
            cntAggressiveEnreg = CntAggressiveEnregFlt;
        }

        if (codeOptKind is Compiler.SMALL_CODE)
        {
            if (cseRefCnt >= aggressiveRefCnt)
            {
                candidate.SetAggressive();
                cseDefCost = 1;
                cseUseCost = 1;

                if (candidate.LiveAcrossCall() || !canEnregister)
                {
                    if (largeFrame)
                    {
                        cseDefCost++;
                        cseUseCost++;
                    }

                    if (hugeFrame)
                    {
                        cseDefCost++;
                        cseUseCost++;
                    }
                }
            }
            else
            {
                candidate.SetConservative();
                if (largeFrame)
                {
                    cseDefCost = 6;
                    cseUseCost = 5;
                }
                else
                {
                    cseDefCost = 3;
                    cseUseCost = 2;
                }
            }

            if (varTypeIsFloating(expr.Type))
            {
                cseDefCost += 2;
                cseUseCost += 1;
            }
        }
        else
        {
            if ((cseRefCnt >= aggressiveRefCnt) && canEnregister)
            {
                candidate.SetAggressive();
                cseDefCost = 1;
                cseUseCost = 1;
            }
            else if (cseRefCnt >= moderateRefCnt)
            {
                candidate.SetModerate();
                cseDefCost = 2;
                if (!candidate.LiveAcrossCall() && canEnregister)
                {
                    cseUseCost = 1;
                }
                else if (canEnregister)
                {
                    cseUseCost = enregCount < cntAggressiveEnreg ? 1u : 2u;
                }
                else
                {
                    cseUseCost = 3;
                }
            }
            else
            {
                candidate.SetConservative();
                cseDefCost = 2;
                cseUseCost = !candidate.LiveAcrossCall() && canEnregister ? 2u : 3u;

                if (m_compiler.lvaTrackedCount == JitConfig.JitMaxLocalsToTrack)
                {
                    cseDefCost++;
                    cseUseCost++;
                }
            }
        }

        if (slotCount > 1)
        {
            cseDefCost = unchecked(cseDefCost * slotCount);
            cseUseCost = unchecked(cseUseCost * slotCount);
        }

        if (candidate.LiveAcrossCall())
        {
            if (!candidate.IsConservative())
            {
                var hasRequiredSpill = false;
                if (!varTypeUsesIntReg(expr.Type))
                {
                    if (varTypeUsesMaskReg(expr.Type))
                    {
                        // Windows AMD64 has no callee-saved mask registers.
                        hasRequiredSpill = true;
                    }
                    else
                    {
                        assert(varTypeUsesFloatReg(expr.Type));
#if FEATURE_SIMD
                        if (expr.Type is TYP_SIMD32 or TYP_SIMD64)
                        {
                            hasRequiredSpill = true;
                        }
#endif
                    }
                }

                if (hasRequiredSpill)
                {
                    cseDefCost++;
                    cseUseCost++;
                }
            }

            if (enregCount < cntAggressiveEnreg)
            {
                extraYesCost = BB_UNITY_WEIGHT_UNSIGNED;
                if (cseRefCnt < moderateRefCnt)
                {
                    extraYesCost *= 2;
                }
            }
        }

        if (candidate.Size() > cseUseCost)
        {
            extraNoCost = unchecked((candidate.Size() - cseUseCost) *
                candidate.CseDsc().csdUseCount * 2u);
        }

        var noCseCost = (candidate.UseCount() * candidate.Cost()) + extraNoCost;
        var yesCseCost = (candidate.DefCount() * cseDefCost) +
            (candidate.UseCount() * cseUseCost) + extraYesCost;

#if DEBUG
        if (m_compiler.verbose)
        {
            JITDUMP($"cseRefCnt={cseRefCnt}, aggressiveRefCnt={aggressiveRefCnt}, " +
                $"moderateRefCnt={moderateRefCnt}\n");
            JITDUMP($"defCnt={candidate.DefCount()}, useCnt={candidate.UseCount()}, " +
                $"cost={candidate.Cost()}, size={candidate.Size()}\n");
            JITDUMP($"def_cost={cseDefCost}, use_cost={cseUseCost}, " +
                $"extra_no_cost={extraNoCost}, extra_yes_cost={extraYesCost}\n");
            JITDUMP($"CSE cost savings check ({noCseCost} >= {yesCseCost}) " +
                $"{(noCseCost >= yesCseCost ? "passes" : "fails")}\n");
        }
#endif

        if (yesCseCost <= noCseCost)
        {
            return true;
        }

        if (noCseCost > 0)
        {
            var percentage = (int)((noCseCost * 100) / yesCseCost);
            if (m_compiler.compStressCompile(Compiler.STRESS_MAKE_CSE, percentage))
            {
                return true;
            }
        }

        return false;
    }

    public override void AdjustHeuristic(CSE_Candidate candidate)
    {
        var cseRefCnt = (candidate.DefCount() * 2) + candidate.UseCount();
        if (candidate.LiveAcrossCall())
        {
            if (cseRefCnt > aggressiveRefCnt)
            {
                aggressiveRefCnt += BB_UNITY_WEIGHT;
            }

            if (cseRefCnt > moderateRefCnt)
            {
                moderateRefCnt += BB_UNITY_WEIGHT / 2;
            }
        }
    }
}
