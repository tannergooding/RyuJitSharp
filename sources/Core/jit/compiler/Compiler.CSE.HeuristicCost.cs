// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public static int CompareCSECandidatesByExecutionCost(CSEdsc first, CSEdsc second)
    {
        var firstCost = first.csdTreeList.tslTree.CostEx;
        var secondCost = second.csdTreeList.tslTree.CostEx;
        if (firstCost != secondCost)
        {
            return secondCost.CompareTo(firstCost);
        }

        if (first.csdUseWtCnt != second.csdUseWtCnt)
        {
            return second.csdUseWtCnt.CompareTo(first.csdUseWtCnt);
        }

        if (first.csdDefWtCnt != second.csdDefWtCnt)
        {
            return first.csdDefWtCnt.CompareTo(second.csdDefWtCnt);
        }

        return first.csdIndex.CompareTo(second.csdIndex);
    }

    public static int CompareCSECandidatesBySize(CSEdsc first, CSEdsc second)
    {
        var firstCost = first.csdTreeList.tslTree.CostSz;
        var secondCost = second.csdTreeList.tslTree.CostSz;
        if (firstCost != secondCost)
        {
            return secondCost.CompareTo(firstCost);
        }

        if (first.csdUseCount != second.csdUseCount)
        {
            return second.csdUseCount.CompareTo(first.csdUseCount);
        }

        if (first.csdDefCount != second.csdDefCount)
        {
            return first.csdDefCount.CompareTo(second.csdDefCount);
        }

        return first.csdIndex.CompareTo(second.csdIndex);
    }
}
