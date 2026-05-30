// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct LclVarDsc
{
    private enum Flags : long
    {
        None = 0,
        IsParam = 1L << 0,
        IsRegArg = 1L << 1,
        IsParamRegTarget = 1L << 2,
        FramePointerBased = 1L << 3,
        OnFrame = 1L << 4,
        Register = 1L << 5,
        Tracked = 1L << 6,
        Pinned = 1L << 7,
        MustInit = 1L << 8,
        AddrExposed = 1L << 9,
        LiveInOutOfHandler = 1L << 10,
        DoNotEnregister = 1L << 11,
        FieldAccessed = 1L << 12,
        InSsa = 1L << 13,
        IsCse = 1L << 14,
        HasLdAddrOp = 1L << 15,
        HasIlStoreOp = 1L << 16,
        HasMultipleIlStoreOp = 1L << 17,
        IsTemp = 1L << 18,
        SingleDef = 1L << 19,
        SingleDefRegCandidate = 1L << 20,
        DisqualifySingleDefRegCandidate = 1L << 21,
        SpillAtSingleDef = 1L << 22,
        HasExceptionalUsesHint = 1L << 23,
        IsPtr = 1L << 24,
        IsUnsafeBuffer = 1L << 25,
        Promoted = 1L << 26,
        IsStructField = 1L << 27,
        ContainsHoles = 1L << 28,
        IsMultiRegArg = 1L << 29,
        IsMultiRegRet = 1L << 30,
        IsMultiRegDest = 1L << 31,
        LraCandidate = 1L << 32,
        RegStruct = 1L << 33,
        ClassIsExact = 1L << 34,
        ImplicitlyReferenced = 1L << 35,
        SuppressedZeroInit = 1L << 36,
        HasExplicitInit = 1L << 37,
        IsOsrLocal = 1L << 38,
        IsOsrExposedLocal = 1L << 39,
        RedefinedInEmbeddedStatement = 1L << 40,
        IsEnumerator = 1L << 41,
        IsNeverNegative = 1L << 42,
        IsSpan = 1L << 43,
        AllDefsAreNoGc = 1L << 44,
        StackAllocatedObject = 1L << 45,

#if TARGET_64BIT
        QuirkToLong = 1L << 46,
#else
	    StructDoubleAlign = 1L << 46,
#endif

#if FEATURE_SIMD
        UsedInSimdIntrinsic = 1L << 47,
#endif

#if FEATURE_IMPLICIT_BYREFS
        IsImplicitByRef = 1L << 48,
        IsLastUseCopyOmissionCandidate = 1L << 49,
#endif
    }
}
