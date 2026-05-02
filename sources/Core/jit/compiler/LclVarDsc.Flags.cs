// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct LclVarDsc
{
    private enum Flags : ulong
    {
        None = 0,
        IsParam = 1UL << 0,
        IsRegArg = 1UL << 1,
        IsParamRegTarget = 1UL << 2,
        FramePointerBased = 1UL << 3,
        OnFrame = 1UL << 4,
        Register = 1UL << 5,
        Tracked = 1UL << 6,
        Pinned = 1UL << 7,
        MustInit = 1UL << 8,
        AddrExposed = 1UL << 9,
        DoNotEnregister = 1UL << 0,
        FieldAccessed = 1UL << 11,
        LiveInOutOfHndlr = 1UL << 12,
        InSsa = 1UL << 13,
        IsCse = 1UL << 14,
        HasLdAddrOp = 1UL << 15,
        HasIlStoreOp = 1UL << 16,
        HasMultipleIlStoreOp = 1UL << 17,
        IsTemp = 1UL << 18,
        SingleDef = 1UL << 19,
        SingleDefRegCandidate = 1UL << 20,
        DisqualifySingleDefRegCandidate = 1UL << 21,
        SpillAtSingleDef = 1UL << 22,
        HasExceptionalUsesHint = 1UL << 23,
        IsPtr = 1UL << 24,
        IsUnsafeBuffer = 1UL << 25,
        Promoted = 1UL << 26,
        IsStructField = 1UL << 27,
        ContainsHoles = 1UL << 28,
        IsMultiRegArg = 1UL << 29,
        IsMultiRegRet = 1UL << 30,
        IsMultiRegDest = 1UL << 31,
        LraCandidate = 1UL << 32,
        RegStruct = 1UL << 33,
        ClassIsExact = 1UL << 34,
        ImplicitlyReferenced = 1UL << 35,
        SuppressedZeroInit = 1UL << 36,
        HasExplicitInit = 1UL << 37,
        IsOsrLocal = 1UL << 38,
        IsOsrExposedLocal = 1UL << 39,
        RedefinedInEmbeddedStatement = 1UL << 40,
        IsEnumerator = 1UL << 41,
        IsNeverNegative = 1UL << 42,
        IsSpan = 1UL << 43,
        AllDefsAreNoGc = 1UL << 44,
        StackAllocatedObject = 1UL << 45,

#if TARGET_64BIT
        QuirkToLong = 1UL << 46,
#else
	    StructDoubleAlign = 1UL << 46,
#endif

#if FEATURE_SIMD
        UsedInSimdIntrinsic = 1UL << 47,
#endif

#if FEATURE_IMPLICIT_BYREFS
        IsImplicitByRef = 1UL << 48,
        IsLastUseCopyOmissionCandidate = 1UL << 49,
#endif
    }
}
