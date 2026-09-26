// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public sealed partial class CSE_Heuristic
{
    public override void SortCandidates()
    {
        var count = m_compiler.CseCandidateCount;
        sortTab = new CSEdsc?[count];
        sortSiz = unchecked((nuint)count * (nuint)IntPtr.Size);
        Array.Copy(m_compiler.CseCandidateTable, sortTab, count);

        if (codeOptKind is Compiler.SMALL_CODE)
        {
            Array.Sort(sortTab, CompareBySize);
        }
        else
        {
            Array.Sort(sortTab, CompareByExecutionCost);
        }

#if DEBUG
        if (m_compiler.verbose)
        {
            JITDUMP("\nSorted CSE candidates:\n");
            foreach (var descriptor in sortTab)
            {
                noway_assert(descriptor is not null);
                var expression = descriptor.csdTreeList.tslTree;
                var definitions = codeOptKind is Compiler.SMALL_CODE
                    ? descriptor.csdDefCount
                    : descriptor.csdDefWtCnt;
                var uses = codeOptKind is Compiler.SMALL_CODE
                    ? descriptor.csdUseCount
                    : descriptor.csdUseWtCnt;
                var cost = codeOptKind is Compiler.SMALL_CODE
                    ? expression.CostSz
                    : expression.CostEx;
                var key = descriptor.csdIsSharedConst
                    ? $"K_{Compiler.DecodeSharedCseConstant(descriptor.csdHashKey):x}"
                    : $"${descriptor.csdHashKey,-3:x}, ${descriptor.defExcSetPromise,-3:x}";
                JITDUMP($"{FMT_CSE(descriptor.csdIndex)}, {{{key}}} useCnt={descriptor.csdUseCount}: " +
                    $"[def={definitions:F6}, use={uses:F6}, cost={cost,3}" +
                    $"{(descriptor.csdLiveAcrossCall ? ", call" : "      ")}]\n        :: ");
                m_compiler.gtDispTree(expression, topOnly: true);
            }
            JITDUMP("\n");
        }
#endif
    }

    private static int CompareBySize(CSEdsc? first, CSEdsc? second)
    {
        noway_assert(first is not null && second is not null);
        return Compiler.CompareCSECandidatesBySize(first, second);
    }

    private static int CompareByExecutionCost(CSEdsc? first, CSEdsc? second)
    {
        noway_assert(first is not null && second is not null);
        return Compiler.CompareCSECandidatesByExecutionCost(first, second);
    }
}
