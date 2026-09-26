// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class CSE_HeuristicRLHook : CSE_HeuristicCommon
{
    private const int MaxFeatures = 19;
    private const int RlHookTypeOther = 0;
    private const int RlHookTypeInt = 1;
    private const int RlHookTypeLong = 2;
    private const int RlHookTypeFloat = 3;
    private const int RlHookTypeDouble = 4;
    private const int RlHookTypeStruct = 5;
    private const int RlHookTypeSimd = 6;

    private static readonly string[] s_featureNameAndType =
    [
        "type", "viable", "live_across_call", "const",
        "shared_const", "make_cse", "has_call", "containable",
        "cost_ex", "cost_sz", "use_count", "def_count",
        "use_wt_cnt", "def_wt_cnt", "distinct_locals", "local_occurrences",
        "bb_count", "block_spread", "enreg_count",
    ];

    public CSE_HeuristicRLHook(Compiler compiler) : base(compiler)
    {
    }

    public override string Name() => "RL Hook CSE Heuristic";

    public override bool ConsiderTree(GenTree tree, bool isReturn) => CanConsiderTree(tree, isReturn);

    private void GetFeatures(CSEdsc descriptor, int[] features)
    {
        assert(features.Length >= MaxFeatures);
        var candidate = new CSE_Candidate(this, descriptor);
        var enregCount = 0;

        for (var trackedIndex = 0; trackedIndex < m_compiler.lvaTrackedCount; trackedIndex++)
        {
            assert(m_compiler.lvaTrackedToVarNum is not null);
            ref var local = ref m_compiler.lvaGetDesc(m_compiler.lvaTrackedToVarNum[trackedIndex]);
            if ((local.lvRefCnt() == 0) || local.lvDoNotEnregister)
            {
                continue;
            }

            if (!varTypeIsFloating(local.Type))
            {
                enregCount++;
#if !TARGET_64BIT
                if (local.Type is TYP_LONG)
                {
                    enregCount++;
                }
#endif
            }
        }

        var numberOfBlocks = m_compiler.fgBBcount;
        var makeCse = false;
        var minimumPostorder = numberOfBlocks;
        var maximumPostorder = 0;

        for (var occurrence = descriptor.csdTreeList; occurrence is not null; occurrence = occurrence.tslNext)
        {
            var block = occurrence.tslBlock;
            var postorder = block.bbPostorderNum;
            if (postorder < minimumPostorder)
            {
                minimumPostorder = postorder;
            }

            if (postorder > maximumPostorder)
            {
                maximumPostorder = postorder;
            }

            makeCse |= (occurrence.tslTree.Flags & GTF_MAKE_CSE) != 0;
        }

        var expression = candidate.Expr();
        var type = expression.Type switch
        {
            TYP_INT => RlHookTypeInt,
            TYP_LONG => RlHookTypeLong,
            TYP_FLOAT => RlHookTypeFloat,
            TYP_DOUBLE => RlHookTypeDouble,
            TYP_STRUCT => RlHookTypeStruct,
            _ when varTypeIsSimd(expression.Type) => RlHookTypeSimd,
            _ => RlHookTypeOther,
        };

        var index = 0;
        features[index++] = type;
        features[index++] = descriptor.IsViable() ? 1 : 0;
        features[index++] = descriptor.csdLiveAcrossCall ? 1 : 0;
        features[index++] = expression.Oper.IsConst ? 1 : 0;
        features[index++] = descriptor.csdIsSharedConst ? 1 : 0;
        features[index++] = makeCse ? 1 : 0;
        features[index++] = (expression.Flags & GTF_CALL) != 0 ? 1 : 0;
        features[index++] = expression.Oper is GT_ADD or GT_NOT or GT_MUL or GT_LSH ? 1 : 0;
        features[index++] = expression.CostEx;
        features[index++] = expression.CostSz;
        features[index++] = descriptor.csdUseCount;
        features[index++] = descriptor.csdDefCount;
        features[index++] = unchecked((int)descriptor.csdUseWtCnt);
        features[index++] = unchecked((int)descriptor.csdDefWtCnt);
        features[index++] = descriptor.numDistinctLocals;
        features[index++] = descriptor.numLocalOccurrences;
        features[index++] = numberOfBlocks;
        features[index++] = maximumPostorder - minimumPostorder;
        features[index++] = enregCount;

        assert(index <= MaxFeatures);
        for (; index < MaxFeatures; index++)
        {
            features[index] = 0;
        }
    }
}
#endif
