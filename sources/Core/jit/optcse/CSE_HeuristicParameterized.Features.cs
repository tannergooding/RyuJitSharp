// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;

namespace RyuJitSharp;

public partial class CSE_HeuristicParameterized
{
    public void GetFeatures(CSEdsc? descriptor, double[] features)
    {
        Array.Clear(features, 0, NumParameters);
        if (descriptor is null)
        {
            GetStoppingFeatures(features);
            return;
        }

        var expression = descriptor.csdTreeList.tslTree;
        var costEx = expression.CostEx;
        const double deMinimis = 1e-3;
        var adjustment = -Math.Log(deMinimis);

        features[0] = costEx;
        features[1] = adjustment + Math.Log(Math.Max(deMinimis, descriptor.csdUseWtCnt));
        features[2] = adjustment + Math.Log(Math.Max(deMinimis, descriptor.csdDefWtCnt));
        features[3] = expression.CostSz;
        features[4] = descriptor.csdUseCount;
        features[5] = descriptor.csdDefCount;

        var liveAcrossCall = descriptor.csdLiveAcrossCall;
        features[6] = BooleanScale * (liveAcrossCall ? 1 : 0);
        features[7] = BooleanScale * (varTypeUsesIntReg(expression.Type) ? 1 : 0);

        var isConstant = expression.Oper.IsConst;
        var isSharedConstant = descriptor.csdIsSharedConst;
        features[8] = BooleanScale * (isConstant && !isSharedConstant ? 1 : 0);
        features[9] = BooleanScale * (isSharedConstant ? 1 : 0);

        var isMinCost = costEx == Compiler.CseMinCost;
        var isLowCost = costEx <= Compiler.CseMinCost + 1;
        features[10] = BooleanScale * (isMinCost ? 1 : 0);
        features[11] = BooleanScale * (isConstant && liveAcrossCall ? 1 : 0);
        features[12] = BooleanScale * (isConstant && isMinCost ? 1 : 0);
        features[13] = BooleanScale * (isMinCost && liveAcrossCall ? 1 : 0);

        var numBlocks = unchecked((uint)m_compiler.fgBBcount);
        var makeCse = false;
        var minPostorderNum = numBlocks;
        uint maxPostorderNum = 0;
        BasicBlock? minPostorderBlock = null;
        BasicBlock? maxPostorderBlock = null;
        for (var occurrence = descriptor.csdTreeList; occurrence is not null; occurrence = occurrence.tslNext)
        {
            var block = occurrence.tslBlock;
            var postorderNum = unchecked((uint)block.bbPostorderNum);
            if (postorderNum < minPostorderNum)
            {
                minPostorderNum = postorderNum;
                minPostorderBlock = block;
            }

            if (postorderNum > maxPostorderNum)
            {
                maxPostorderNum = postorderNum;
                maxPostorderBlock = block;
            }

            makeCse |= (occurrence.tslTree.Flags & GTF_MAKE_CSE) != 0;
        }
        var blockSpread = unchecked(maxPostorderNum - minPostorderNum);

        features[14] = BooleanScale * (makeCse ? 1 : 0);
        features[15] = descriptor.numDistinctLocals;
        features[16] = descriptor.numLocalOccurrences;
        features[17] = BooleanScale * ((expression.Flags & GTF_CALL) != 0 ? 1 : 0);
        features[18] = adjustment + Math.Log(Math.Max(deMinimis,
            descriptor.csdUseCount * descriptor.csdUseWtCnt));
        features[19] = adjustment + Math.Log(Math.Max(deMinimis,
            descriptor.numLocalOccurrences * descriptor.csdUseWtCnt));
        features[20] = BooleanScale * ((double)blockSpread / numBlocks);

        var containable = expression.Oper is GT_ADD or GT_NOT or GT_MUL or GT_LSH;
        features[21] = BooleanScale * (containable ? 1 : 0);
        features[22] = BooleanScale * (containable && isLowCost ? 1 : 0);

        var liveAcrossCallLsra = liveAcrossCall;
        if (!liveAcrossCallLsra)
        {
            uint count = 0;
            for (var block = minPostorderBlock;
                 (block is not null) && (block != maxPostorderBlock) && (count < blockSpread);
                 block = block.Next, count++)
            {
                if (block.HasFlag(BBF_HAS_CALL))
                {
                    liveAcrossCallLsra = true;
                    break;
                }
            }
        }
        features[23] = BooleanScale * (liveAcrossCallLsra ? 1 : 0);
    }

    public void GetStoppingFeatures(double[] features)
    {
        const double deMinimis = 1e-3;
        var spillAtWeight = deMinimis;
        var adjustment = -Math.Log(deMinimis);
        var pressure = m_registerPressure > m_addCSEcount
            ? m_registerPressure - m_addCSEcount : 0;
        var weights = m_localWeights ??
            throw new InvalidOperationException("CSE local weights have not been captured.");
        if (pressure < weights.Count)
        {
            spillAtWeight = weights[checked((int)pressure)];
        }

        JITDUMP($"Pressure count {pressure}, pressure weight {FMT_WT(spillAtWeight)}\n");
        features[24] = adjustment + Math.Log(Math.Max(deMinimis, spillAtWeight));
    }

    public double Preference(CSEdsc? descriptor)
    {
        var features = new double[NumParameters];
        GetFeatures(descriptor, features);
#if DEBUG
        if (JitConfig.JitRLCSECandidateFeatures > 0)
        {
            DumpFeatures(descriptor, features);
        }
#endif
        double preference = 0;
        for (var index = 0; index < NumParameters; index++)
        {
            preference += features[index] * m_parameters[index];
        }

        return preference;
    }

    public double StoppingPreference()
    {
        var features = new double[NumParameters];
        GetFeatures(null, features);
#if DEBUG
        if (JitConfig.JitRLCSECandidateFeatures > 0)
        {
            DumpFeatures(null, features);
        }
#endif
        double preference = 0;
        for (var index = 0; index < NumParameters; index++)
        {
            preference += features[index] * m_parameters[index];
        }

        return preference;
    }
}
