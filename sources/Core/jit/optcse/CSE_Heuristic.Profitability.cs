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
#if DEBUG
                if (m_compiler.verbose)
                {
                    jitprintf($"Aggressive CSE Promotion ({formatFloat(cseRefCnt, "F6")} >= " +
                        $"{formatFloat(aggressiveRefCnt, "F6")})\n");
                }
#endif
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
#if DEBUG
                    if (m_compiler.verbose)
                    {
                        jitprintf($"Codesize CSE Promotion ({(hugeFrame ? "huge" : "large")} frame)\n");
                    }
#endif
#if TARGET_XARCH
                    cseDefCost = 6;
                    cseUseCost = 5;
#else
                    cseDefCost = hugeFrame ? 12u : 8u;
                    cseUseCost = cseDefCost;
#endif
                }
                else
                {
#if DEBUG
                    if (m_compiler.verbose)
                    {
                        jitprintf("Codesize CSE Promotion (small frame)\n");
                    }
#endif
#if TARGET_XARCH
                    cseDefCost = 3;
                    cseUseCost = 2;
#else
                    cseDefCost = 2;
                    cseUseCost = 2;
#endif
                }
            }

#if TARGET_XARCH
            if (varTypeIsFloating(expr.Type))
            {
                cseDefCost += 2;
                cseUseCost += 1;
            }
#endif
        }
        else
        {
            if ((cseRefCnt >= aggressiveRefCnt) && canEnregister)
            {
                candidate.SetAggressive();
#if DEBUG
                if (m_compiler.verbose)
                {
                    jitprintf($"Aggressive CSE Promotion ({formatFloat(cseRefCnt, "F6")} >= " +
                        $"{formatFloat(aggressiveRefCnt, "F6")})\n");
                }
#endif
                cseDefCost = 1;
                cseUseCost = 1;
            }
            else if (cseRefCnt >= moderateRefCnt)
            {
                candidate.SetModerate();
#if DEBUG
                if (m_compiler.verbose)
                {
                    if (!candidate.LiveAcrossCall() && canEnregister)
                    {
                        jitprintf($"Moderate CSE Promotion (CSE never live at call) " +
                            $"({formatFloat(cseRefCnt, "F6")} >= {formatFloat(moderateRefCnt, "F6")})\n");
                    }
                    else
                    {
                        jitprintf($"Moderate CSE Promotion " +
                            $"({(candidate.LiveAcrossCall() ? "CSE is live across a call" : "not enregisterable")}) " +
                            $"({formatFloat(cseRefCnt, "F6")} >= {formatFloat(moderateRefCnt, "F6")})\n");
                    }
                }
#endif

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
#if DEBUG
                if (m_compiler.verbose)
                {
                    if (!candidate.LiveAcrossCall() && canEnregister)
                    {
                        jitprintf($"Conservative CSE Promotion (not enregisterable) " +
                            $"({formatFloat(cseRefCnt, "F6")} < {formatFloat(moderateRefCnt, "F6")})\n");
                    }
                    else
                    {
                        jitprintf($"Conservative CSE Promotion " +
                            $"({formatFloat(cseRefCnt, "F6")} < {formatFloat(moderateRefCnt, "F6")})\n");
                    }
                }
#endif

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
#pragma warning disable CA1508 // Callee-save register counts differ between targets.
                if (varTypeUsesIntReg(expr.Type))
                {
                    assert(CNT_CALLEE_SAVED != 0);
                }
                else if (varTypeUsesMaskReg(expr.Type))
                {
                    if (CNT_CALLEE_SAVED_MASK == 0)
                    {
                        hasRequiredSpill = true;
                    }
                }
                else
                {
                    assert(varTypeUsesFloatReg(expr.Type));
                    if (CNT_CALLEE_SAVED_FLOAT == 0)
                    {
                        hasRequiredSpill = true;
                    }
#if FEATURE_SIMD && (TARGET_XARCH || TARGET_ARM64)
#if TARGET_XARCH
                    else if (expr.Type is TYP_SIMD32 or TYP_SIMD64)
#elif TARGET_ARM64
                    else if (expr.Type is TYP_SIMD16)
#endif
                    {
                        hasRequiredSpill = true;
                    }
#endif
                }
#pragma warning restore CA1508

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
            JITDUMP($"cseRefCnt={formatFloat(cseRefCnt, "F6")}, " +
                $"aggressiveRefCnt={formatFloat(aggressiveRefCnt, "F6")}, " +
                $"moderateRefCnt={formatFloat(moderateRefCnt, "F6")}\n");
            JITDUMP($"defCnt={formatFloat(candidate.DefCount(), "F6")}, " +
                $"useCnt={formatFloat(candidate.UseCount(), "F6")}, " +
                $"cost={candidate.Cost()}, size={candidate.Size()}" +
                $"{(candidate.LiveAcrossCall() ? ", LiveAcrossCall" : "")}\n");
            JITDUMP($"def_cost={cseDefCost}, use_cost={cseUseCost}, " +
                $"extra_no_cost={extraNoCost}, extra_yes_cost={extraYesCost}\n");
            JITDUMP($"CSE cost savings check ({formatFloat(noCseCost, "F6")} >= " +
                $"{formatFloat(yesCseCost, "F6")}) " +
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
