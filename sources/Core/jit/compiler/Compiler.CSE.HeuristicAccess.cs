// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    internal int CseCandidateCount => optCSECandidateCount;
    internal CSEdsc?[] CseCandidateTable => optCSEtab;
    internal int NextCseAttempt() => optCSEattempt++;
    internal void SetCseWeight(weight_t weight) => optCSEweight = weight;
    internal GenTree? ExtractCseSideEffects(GenTree tree) => optExtractSideEffectsForCSE(tree);

    internal void RecordCsePromotion()
    {
        optCSEcount++;
        Metrics.CseCount++;
    }

#if DEBUG
    internal static nuint DecodeSharedCseConstant(nuint key) => Decode_Shared_Const_CSE_Value(key);
#endif
}
