// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, redundantbranchopts.cpp.

namespace RyuJitSharp;

public partial class Compiler
{
    private static System.ReadOnlySpan<ValueNumStore.VN_RELATION_KIND> s_vnRelations => [
        ValueNumStore.VN_RELATION_KIND.VRK_Same,
        ValueNumStore.VN_RELATION_KIND.VRK_Reverse,
        ValueNumStore.VN_RELATION_KIND.VRK_Swap,
        ValueNumStore.VN_RELATION_KIND.VRK_SwapReverse,
    ];

    // Each column preserves the original implication-rule ordering.
    private static System.ReadOnlySpan<VNFunc> s_domRelops => [
        VNF_EQ,
        VNF_EQ,
        VNF_EQ,
        VNF_EQ,
        VNF_EQ,
        VNF_EQ,
        VNF_NE,
        VNF_NE,
        VNF_NE,
        VNF_NE,
        VNF_NE,
        VNF_NE,
        VNF_LE,
        VNF_LE,
        VNF_LE,
        VNF_LE,
        VNF_LE_UN,
        VNF_LE_UN,
        VNF_LE_UN,
        VNF_LE_UN,
        VNF_GT,
        VNF_GT,
        VNF_GT,
        VNF_GT,
        VNF_GT_UN,
        VNF_GT_UN,
        VNF_GT_UN,
        VNF_GT_UN,
        VNF_GE,
        VNF_GE,
        VNF_GE,
        VNF_GE,
        VNF_GE_UN,
        VNF_GE_UN,
        VNF_GE_UN,
        VNF_GE_UN,
        VNF_LT,
        VNF_LT,
        VNF_LT,
        VNF_LT,
        VNF_LT_UN,
        VNF_LT_UN,
        VNF_LT_UN,
        VNF_LT_UN,
    ];

    private static System.ReadOnlySpan<VNFunc> s_treeRelops => [
        VNF_GE,
        VNF_LE,
        VNF_GT,
        VNF_GT_UN,
        VNF_LT,
        VNF_LT_UN,
        VNF_GE,
        VNF_LE,
        VNF_GT,
        VNF_GT_UN,
        VNF_LT,
        VNF_LT_UN,
        VNF_EQ,
        VNF_NE,
        VNF_GE,
        VNF_LT,
        VNF_EQ,
        VNF_NE,
        VNF_GE_UN,
        VNF_LT_UN,
        VNF_EQ,
        VNF_NE,
        VNF_GE,
        VNF_LT,
        VNF_EQ,
        VNF_NE,
        VNF_GE_UN,
        VNF_LT_UN,
        VNF_EQ,
        VNF_NE,
        VNF_LE,
        VNF_GT,
        VNF_EQ,
        VNF_NE,
        VNF_LE_UN,
        VNF_GT_UN,
        VNF_EQ,
        VNF_NE,
        VNF_LE,
        VNF_GT,
        VNF_EQ,
        VNF_NE,
        VNF_LE_UN,
        VNF_GT_UN,
    ];

    [System.Flags]
    private enum RelopInferenceFlags : byte
    {
        None = 0,
        CanInferFromTrue = 1 << 0,
        CanInferFromFalse = 1 << 1,
        ReverseSense = 1 << 2,
    }

    private static System.ReadOnlySpan<RelopInferenceFlags> s_relopInferenceFlags => [
        RelopInferenceFlags.CanInferFromTrue,
        RelopInferenceFlags.CanInferFromTrue,
        RelopInferenceFlags.CanInferFromTrue | RelopInferenceFlags.ReverseSense,
        RelopInferenceFlags.CanInferFromTrue | RelopInferenceFlags.ReverseSense,
        RelopInferenceFlags.CanInferFromTrue | RelopInferenceFlags.ReverseSense,
        RelopInferenceFlags.CanInferFromTrue | RelopInferenceFlags.ReverseSense,
        RelopInferenceFlags.CanInferFromFalse | RelopInferenceFlags.ReverseSense,
        RelopInferenceFlags.CanInferFromFalse | RelopInferenceFlags.ReverseSense,
        RelopInferenceFlags.CanInferFromFalse,
        RelopInferenceFlags.CanInferFromFalse,
        RelopInferenceFlags.CanInferFromFalse,
        RelopInferenceFlags.CanInferFromFalse,
        RelopInferenceFlags.CanInferFromFalse,
        RelopInferenceFlags.CanInferFromFalse | RelopInferenceFlags.ReverseSense,
        RelopInferenceFlags.CanInferFromFalse | RelopInferenceFlags.ReverseSense,
        RelopInferenceFlags.CanInferFromFalse,
        RelopInferenceFlags.CanInferFromFalse,
        RelopInferenceFlags.CanInferFromFalse | RelopInferenceFlags.ReverseSense,
        RelopInferenceFlags.CanInferFromFalse | RelopInferenceFlags.ReverseSense,
        RelopInferenceFlags.CanInferFromFalse,
        RelopInferenceFlags.CanInferFromTrue | RelopInferenceFlags.ReverseSense,
        RelopInferenceFlags.CanInferFromTrue,
        RelopInferenceFlags.CanInferFromTrue,
        RelopInferenceFlags.CanInferFromTrue | RelopInferenceFlags.ReverseSense,
        RelopInferenceFlags.CanInferFromTrue | RelopInferenceFlags.ReverseSense,
        RelopInferenceFlags.CanInferFromTrue,
        RelopInferenceFlags.CanInferFromTrue,
        RelopInferenceFlags.CanInferFromTrue | RelopInferenceFlags.ReverseSense,
        RelopInferenceFlags.CanInferFromFalse,
        RelopInferenceFlags.CanInferFromFalse | RelopInferenceFlags.ReverseSense,
        RelopInferenceFlags.CanInferFromFalse | RelopInferenceFlags.ReverseSense,
        RelopInferenceFlags.CanInferFromFalse,
        RelopInferenceFlags.CanInferFromFalse,
        RelopInferenceFlags.CanInferFromFalse | RelopInferenceFlags.ReverseSense,
        RelopInferenceFlags.CanInferFromFalse | RelopInferenceFlags.ReverseSense,
        RelopInferenceFlags.CanInferFromFalse,
        RelopInferenceFlags.CanInferFromTrue | RelopInferenceFlags.ReverseSense,
        RelopInferenceFlags.CanInferFromTrue,
        RelopInferenceFlags.CanInferFromTrue,
        RelopInferenceFlags.CanInferFromTrue | RelopInferenceFlags.ReverseSense,
        RelopInferenceFlags.CanInferFromTrue | RelopInferenceFlags.ReverseSense,
        RelopInferenceFlags.CanInferFromTrue,
        RelopInferenceFlags.CanInferFromTrue,
        RelopInferenceFlags.CanInferFromTrue | RelopInferenceFlags.ReverseSense,
    ];

    private enum RelopResult
    {
        Unknown,
        AlwaysFalse,
        AlwaysTrue,
    }

    private struct RelopImplicationInfo
    {
        public ValueNum DomCmpNormVN;
        public ValueNum TreeNormVN;
        public ValueNumStore.VN_RELATION_KIND VnRelation;
        public bool CanInfer;
        public bool CanInferFromTrue;
        public bool CanInferFromFalse;
        public bool ReverseSense;

        public RelopImplicationInfo()
        {
            DomCmpNormVN = ValueNumStore.NoVN;
            TreeNormVN = ValueNumStore.NoVN;
            VnRelation = ValueNumStore.VN_RELATION_KIND.VRK_Same;
            CanInfer = false;
            CanInferFromTrue = true;
            CanInferFromFalse = true;
            ReverseSense = false;
        }
    }

    private static RelopResult IsCmp2ImpliedByCmp1(
        genTreeOps oper1, nint bound1, genTreeOps oper2, nint bound2)
    {
        static bool TrySetRange(genTreeOps oper, nint bound, out (nint Start, nint End) range)
        {
            range = (nint.MinValue, nint.MaxValue);
            switch (oper)
            {
                case GT_LT:
                {
                    if (bound == nint.MinValue)
                    {
                        return false;
                    }

                    range.End = bound - 1;
                    return true;
                }

                case GT_LE:
                {
                    range.End = bound;
                    return true;
                }

                case GT_GT:
                {
                    if (bound == nint.MaxValue)
                    {
                        return false;
                    }

                    range.Start = bound + 1;
                    return true;
                }

                case GT_GE:
                {
                    range.Start = bound;
                    return true;
                }

                case GT_EQ:
                case GT_NE:
                {
                    range = (bound, bound);
                    return true;
                }

                default:
                {
                    return false;
                }
            }
        }

        if (TrySetRange(oper1, bound1, out var range1) &&
            TrySetRange(oper2, bound2, out var range2))
        {
            var intersects = (range1.Start <= range2.End) && (range2.Start <= range1.End);

            if ((oper1 is GT_NE) || (oper2 is GT_NE))
            {
                if (oper1 == oper2)
                {
                    return bound1 == bound2 ? RelopResult.AlwaysTrue : RelopResult.Unknown;
                }

                if (oper1 is GT_EQ)
                {
                    return bound1 == bound2 ? RelopResult.AlwaysFalse : RelopResult.AlwaysTrue;
                }

                if ((oper2 is GT_NE) && !intersects)
                {
                    return RelopResult.AlwaysTrue;
                }

                return RelopResult.Unknown;
            }

            if (!intersects)
            {
                return RelopResult.AlwaysFalse;
            }

            if ((range2.Start <= range1.Start) && (range1.End <= range2.End))
            {
                return RelopResult.AlwaysTrue;
            }
        }

        return RelopResult.Unknown;
    }

    private unsafe void optRelopImpliesRelop(ref RelopImplicationInfo info)
    {
        assert(!info.CanInfer);
        assert(vnStore is not null);

        foreach (var relation in s_vnRelations)
        {
            var related = vnStore.GetRelatedRelop(info.DomCmpNormVN, relation);
            if ((related != ValueNumStore.NoVN) && (related == info.TreeNormVN))
            {
                info.CanInfer = true;
                info.VnRelation = relation;
                info.ReverseSense = relation is ValueNumStore.VN_RELATION_KIND.VRK_Reverse
                    or ValueNumStore.VN_RELATION_KIND.VRK_SwapReverse;
                return;
            }
        }

        var domApp = new VNFuncApp();
        if (!vnStore.GetVNFunc(info.DomCmpNormVN, ref domApp) ||
            varTypeIsFloating(vnStore.TypeOfVN(domApp.GetArg(0))))
        {
            return;
        }

        if (Configuration.DEBUG)
        {
            DebugData.MethodRangeData.s_jitEnableRboRange.EnsureInit(JitConfig.JitEnableRboRange);
        }

        var inRange = !Configuration.DEBUG ||
            DebugData.MethodRangeData.s_jitEnableRboRange.Contains(impInlineRoot.info.compMethodHash());

        var domFunc = domApp.Func;
        var treeApp = new VNFuncApp();
        if (inRange && ValueNumStore.VNFuncIsComparison(domFunc) &&
            vnStore.GetVNFunc(info.TreeNormVN, ref treeApp))
        {
            if (((treeApp.GetArg(0) == domApp.GetArg(0)) && (treeApp.GetArg(1) == domApp.GetArg(1))) ||
                ((treeApp.GetArg(0) == domApp.GetArg(1)) && (treeApp.GetArg(1) == domApp.GetArg(0))))
            {
                var swapped = treeApp.GetArg(0) == domApp.GetArg(1);
                var firstFunc = swapped ? ValueNumStore.SwapRelop(domFunc) : domFunc;
                var domRelops = s_domRelops;
                var relationFlags = s_relopInferenceFlags;
                var treeRelops = s_treeRelops;
                for (var ruleIndex = 0; ruleIndex < domRelops.Length; ruleIndex++)
                {
                    if ((domRelops[ruleIndex] == firstFunc) && (treeRelops[ruleIndex] == treeApp.Func))
                    {
                        info.CanInfer = true;
                        info.VnRelation = ValueNumStore.VN_RELATION_KIND.VRK_Inferred;
                        var flags = relationFlags[ruleIndex];
                        info.CanInferFromTrue = (flags & RelopInferenceFlags.CanInferFromTrue) != 0;
                        info.CanInferFromFalse = (flags & RelopInferenceFlags.CanInferFromFalse) != 0;
                        info.ReverseSense = (flags & RelopInferenceFlags.ReverseSense) != 0;
                        JITDUMP($"Can infer {ValueNumStore.VNFuncName(treeApp.Func)} from " +
                            $"[{(info.CanInferFromTrue ? "true" : "false")}] " +
                            $"dominating {ValueNumStore.VNFuncName(domFunc)}\n");
                        return;
                    }
                }
            }

            if (((treeApp.GetArg(0) == domApp.GetArg(0)) ||
                 (treeApp.GetArg(0) == domApp.GetArg(1)) ||
                 (treeApp.GetArg(1) == domApp.GetArg(0)) ||
                 (treeApp.GetArg(1) == domApp.GetArg(1))) &&
                optRelopTryInferWithOneEqualOperand(domApp, treeApp, ref info))
            {
                return;
            }
        }

        // Native genTreeOps is BYTE; extended VNFuncs are narrowed before these operator tests.
        var oper = unchecked((genTreeOps)domFunc);
        if ((oper is not GT_EQ and not GT_NE) ||
            (domApp.GetArg(1) != vnStore.VNZeroForType(TYP_INT)))
        {
            return;
        }

        var predApp = new VNFuncApp();
        if (!vnStore.GetVNFunc(domApp.GetArg(0), ref predApp))
        {
            return;
        }

        var predOper = unchecked((genTreeOps)predApp.Func);
        if (predOper is not GT_AND and not GT_OR)
        {
            return;
        }

        for (var i = 0; (i < predApp.Arity) && !info.CanInfer; i++)
        {
            var predVN = predApp.GetArg(i);
            foreach (var relation in s_vnRelations)
            {
                var related = vnStore.GetRelatedRelop(predVN, relation);
                if ((related == ValueNumStore.NoVN) || (related != info.TreeNormVN))
                {
                    continue;
                }

                info.VnRelation = relation;
                info.CanInfer = true;
                info.ReverseSense = relation is ValueNumStore.VN_RELATION_KIND.VRK_Reverse
                    or ValueNumStore.VN_RELATION_KIND.VRK_SwapReverse;
                if (predOper is GT_AND)
                {
                    info.CanInferFromFalse = oper is GT_EQ;
                    info.CanInferFromTrue = oper is GT_NE;
                }
                else
                {
                    info.CanInferFromFalse = oper is GT_NE;
                    info.CanInferFromTrue = oper is GT_EQ;
                }

                info.ReverseSense ^= oper is GT_EQ;
                JITDUMP($"Inferring predicate value from {predOper.Name}\n");
                return;
            }
        }
    }

    private bool optRelopTryInferWithOneEqualOperand(
        VNFuncApp domApp, VNFuncApp treeApp, ref RelopImplicationInfo info)
    {
        assert(vnStore is not null);

        var domFunc = domApp.Func;
        var domOp1 = domApp.GetArg(0);
        var domOp2 = domApp.GetArg(1);
        var treeFunc = treeApp.Func;
        var treeOp1 = treeApp.GetArg(0);
        var treeOp2 = treeApp.GetArg(1);

        if (vnStore.IsVNConstant(domOp1))
        {
            (domOp1, domOp2) = (domOp2, domOp1);
            domFunc = ValueNumStore.SwapRelop(domFunc);
        }

        if (vnStore.IsVNConstant(treeOp1))
        {
            (treeOp1, treeOp2) = (treeOp2, treeOp1);
            treeFunc = ValueNumStore.SwapRelop(treeFunc);
        }

        if ((treeOp1 != domOp1) || !vnStore.IsVNConstant(treeOp2) ||
            !vnStore.IsVNConstant(domOp2))
        {
            return false;
        }

        var treeType = vnStore.TypeOfVN(treeOp2);
        if (!varTypeIsIntOrI(vnStore.TypeOfVN(treeOp1)) ||
            (vnStore.TypeOfVN(domOp1) != treeType) ||
            (vnStore.TypeOfVN(domOp2) != treeType) ||
            !ValueNumStore.VNFuncIsSignedComparison(domFunc) ||
            !ValueNumStore.VNFuncIsSignedComparison(treeFunc))
        {
            return false;
        }

        var domOper = (genTreeOps)domFunc;
        var domCns = vnStore.CoercedConstantValue<nint>(domOp2);
        var treeOper = (genTreeOps)treeFunc;
        var treeCns = vnStore.CoercedConstantValue<nint>(treeOp2);

        var ifTrue = IsCmp2ImpliedByCmp1(domOper, domCns, treeOper, treeCns);
        var ifFalse = IsCmp2ImpliedByCmp1(domOper.ReverseRelop, domCns, treeOper, treeCns);
        if (((ifTrue is RelopResult.Unknown) && (ifFalse is RelopResult.Unknown)) ||
            ((ifTrue is RelopResult.AlwaysTrue) && (ifFalse is RelopResult.AlwaysTrue)))
        {
            return false;
        }

        info.CanInfer = true;
        info.VnRelation = ValueNumStore.VN_RELATION_KIND.VRK_Inferred;
        info.CanInferFromTrue = ifTrue is not RelopResult.Unknown;
        info.CanInferFromFalse = ifFalse is not RelopResult.Unknown;
        info.ReverseSense = (ifFalse is RelopResult.AlwaysTrue) || (ifTrue is RelopResult.AlwaysFalse);
        return true;
    }
}
