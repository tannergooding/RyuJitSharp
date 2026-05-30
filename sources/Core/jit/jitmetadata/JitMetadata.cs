// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static class JitMetadata
{
    public const string MethodFullName = nameof(MethodFullName);
    public const string TieringName = nameof(TieringName);
    public const string ActualCodeBytes = nameof(ActualCodeBytes);
    public const string AllocatedHotCodeBytes = nameof(AllocatedHotCodeBytes);
    public const string AllocatedColdCodeBytes = nameof(AllocatedColdCodeBytes);
    public const string ReadOnlyDataBytes = nameof(ReadOnlyDataBytes);
    public const string GCInfoBytes = nameof(GCInfoBytes);
    public const string EHClauseCount = nameof(EHClauseCount);
    public const string PhysicallyPromotedFields = nameof(PhysicallyPromotedFields);
    public const string LoopsFoundDuringOpts = nameof(LoopsFoundDuringOpts);
    public const string LoopsInverted = nameof(LoopsInverted);
    public const string LoopsCloned = nameof(LoopsCloned);
    public const string LoopsUnrolled = nameof(LoopsUnrolled);
    public const string LoopAlignmentCandidates = nameof(LoopAlignmentCandidates);
    public const string LoopsAligned = nameof(LoopsAligned);
    public const string LoopsIVWidened = nameof(LoopsIVWidened);
    public const string WidenedIVs = nameof(WidenedIVs);
    public const string UnusedIVsRemoved = nameof(UnusedIVsRemoved);
    public const string LoopsMadeDownwardsCounted = nameof(LoopsMadeDownwardsCounted);
    public const string LoopsStrengthReduced = nameof(LoopsStrengthReduced);
    public const string VarsInSsa = nameof(VarsInSsa);
    public const string HoistedExpressions = nameof(HoistedExpressions);
    public const string RedundantBranchesEliminated = nameof(RedundantBranchesEliminated);
    public const string JumpThreadingsPerformed = nameof(JumpThreadingsPerformed);
    public const string CseCount = nameof(CseCount);
    public const string BasicBlocksAtCodegen = nameof(BasicBlocksAtCodegen);
    public const string PerfScore = nameof(PerfScore);
    public const string BytesAllocated = nameof(BytesAllocated);
    public const string ImporterBranchFold = nameof(ImporterBranchFold);
    public const string ImporterSwitchFold = nameof(ImporterSwitchFold);
    public const string DevirtualizedCall = nameof(DevirtualizedCall);
    public const string DevirtualizedCallUnboxedEntry = nameof(DevirtualizedCallUnboxedEntry);
    public const string GDV = nameof(GDV);
    public const string ClassGDV = nameof(ClassGDV);
    public const string MethodGDV = nameof(MethodGDV);
    public const string MultiGuessGDV = nameof(MultiGuessGDV);
    public const string ChainedGDV = nameof(ChainedGDV);
    public const string EnumeratorGDV = nameof(EnumeratorGDV);
    public const string InlinerBranchFold = nameof(InlinerBranchFold);
    public const string InlineAttempt = nameof(InlineAttempt);
    public const string InlineCount = nameof(InlineCount);
    public const string ProfileConsistentBeforeInline = nameof(ProfileConsistentBeforeInline);
    public const string ProfileConsistentAfterInline = nameof(ProfileConsistentAfterInline);
    public const string ProfileConsistentBeforeMorph = nameof(ProfileConsistentBeforeMorph);
    public const string ProfileConsistentAfterMorph = nameof(ProfileConsistentAfterMorph);
    public const string ProfileSynthesizedBlendedOrRepaired = nameof(ProfileSynthesizedBlendedOrRepaired);
    public const string ProfileInconsistentInitially = nameof(ProfileInconsistentInitially);
    public const string ProfileInconsistentResetLeave = nameof(ProfileInconsistentResetLeave);
    public const string ProfileInconsistentImporterBranchFold = nameof(ProfileInconsistentImporterBranchFold);
    public const string ProfileInconsistentImporterSwitchFold = nameof(ProfileInconsistentImporterSwitchFold);
    public const string ProfileInconsistentChainedGDV = nameof(ProfileInconsistentChainedGDV);
    public const string ProfileInconsistentScratchBB = nameof(ProfileInconsistentScratchBB);
    public const string ProfileInconsistentInlinerBranchFold = nameof(ProfileInconsistentInlinerBranchFold);
    public const string ProfileInconsistentInlineeScale = nameof(ProfileInconsistentInlineeScale);
    public const string ProfileInconsistentInlinee = nameof(ProfileInconsistentInlinee);
    public const string ProfileInconsistentNoReturnInlinee = nameof(ProfileInconsistentNoReturnInlinee);
    public const string ProfileInconsistentMayThrowInlinee = nameof(ProfileInconsistentMayThrowInlinee);
    public const string NewRefClassHelperCalls = nameof(NewRefClassHelperCalls);
    public const string StackAllocatedRefClasses = nameof(StackAllocatedRefClasses);
    public const string NewBoxedValueClassHelperCalls = nameof(NewBoxedValueClassHelperCalls);
    public const string StackAllocatedBoxedValueClasses = nameof(StackAllocatedBoxedValueClasses);
    public const string NewArrayHelperCalls = nameof(NewArrayHelperCalls);
    public const string StackAllocatedArrays = nameof(StackAllocatedArrays);
    public const string LocalAssertionCount = nameof(LocalAssertionCount);
    public const string LocalAssertionOverflow = nameof(LocalAssertionOverflow);
    public const string MorphTrackedLocals = nameof(MorphTrackedLocals);
    public const string MorphLocals = nameof(MorphLocals);
    public const string EnumeratorGDVProvisionalNoEscape = nameof(EnumeratorGDVProvisionalNoEscape);
    public const string EnumeratorGDVCanCloneToEnsureNoEscape = nameof(EnumeratorGDVCanCloneToEnsureNoEscape);
    public const string SuspensionPointsMerged = nameof(SuspensionPointsMerged);

    public static unsafe void report(Compiler compiler, string name, double data)
        => report(compiler, name, &data, sizeof(double));

    public static unsafe void report(Compiler compiler, string name, int data)
        => report(compiler, name, &data, sizeof(int));

    public static unsafe void report(Compiler compiler, string name, long data)
        => report(compiler, name, &data, sizeof(long));

    public static unsafe void report(Compiler compiler, string name, string data)
    {
        using var utf8Data = new MarshaledUtf8String(data);

        fixed (byte* pUtf8Data = utf8Data)
        {
            report(compiler, name, pUtf8Data, utf8Data.Length);
        }
    }

    /// <summary>Report metadata back to the EE.</summary>
    /// <param name="compiler">Compiler instance</param>
    /// <param name="name">Key name of metadata</param>
    /// <param name="data">Pointer to the value to report back</param>
    /// <param name="length"></param>
    public static unsafe void report(Compiler compiler, string name, void* data, int length)
    {
        using var utf8Name = new MarshaledUtf8String(name);

        fixed (byte* pUtf8Name = utf8Name)
        {
            compiler.info.compCompHnd->reportMetadata(pUtf8Name, data, length);
        }
    }
}
