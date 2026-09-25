// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public readonly partial struct HWIntrinsicInfo
{
#if FEATURE_HW_INTRINSICS
    private static readonly int[] s_isaRangeBounds = CreateIsaRangeBounds();

    private static int[] CreateIsaRangeBounds()
    {
        var instructionSets = s_instructionSets;
        var isaRangeBounds = new int[((int)InstructionSet_NONE + 1) * 2];
        Array.Fill(isaRangeBounds, -1);

        for (var index = 0; index < instructionSets.Length; index++)
        {
            var boundsIndex = (int)instructionSets[index] * 2;

            if (isaRangeBounds[boundsIndex] is -1)
            {
                isaRangeBounds[boundsIndex] = index;
            }
            else if (isaRangeBounds[boundsIndex + 1] != index - 1)
            {
                // Internal codegen-only intrinsics appear after their ISA's sorted lookup range.
                continue;
            }

            isaRangeBounds[boundsIndex + 1] = index;
        }

        // All portable vector widths use the same intrinsic IDs and names.
        var vectorRange = (int)InstructionSet_Vector * 2;
        var vector128Range = (int)InstructionSet_Vector128 * 2;
        isaRangeBounds[vector128Range] = isaRangeBounds[vectorRange];
        isaRangeBounds[vector128Range + 1] = isaRangeBounds[vectorRange + 1];

#if TARGET_XARCH
        var vector256Range = (int)InstructionSet_Vector256 * 2;
        var vector512Range = (int)InstructionSet_Vector512 * 2;
        isaRangeBounds[vector256Range] = isaRangeBounds[vectorRange];
        isaRangeBounds[vector256Range + 1] = isaRangeBounds[vectorRange + 1];
        isaRangeBounds[vector512Range] = isaRangeBounds[vectorRange];
        isaRangeBounds[vector512Range + 1] = isaRangeBounds[vectorRange + 1];
#elif TARGET_ARM64
        var vector64Range = (int)InstructionSet_Vector64 * 2;
        isaRangeBounds[vector64Range] = isaRangeBounds[vectorRange];
        isaRangeBounds[vector64Range + 1] = isaRangeBounds[vectorRange + 1];
#endif

        return isaRangeBounds;
    }

    public static unsafe NamedIntrinsic LookupId(CORINFO_SIG_INFO* sig, CORINFO_InstructionSet isa, ReadOnlySpan<byte> methodName)
    {
        if (sig->hasThis() || isa is InstructionSet_NONE)
        {
            return NI_Illegal;
        }

        if (isa is InstructionSet_Vector)
        {
            return LookupIdForIsa(sig, InstructionSet_Vector128, methodName);
        }

#if TARGET_XARCH
        if (isa is InstructionSet_AVX10v1)
        {
            var id = LookupIdForIsa(sig, InstructionSet_AVX512, methodName);

            if (id is not NI_Illegal)
            {
                return id;
            }

            id = LookupIdForIsa(sig, InstructionSet_AVX512v2, methodName);
            return id is not NI_Illegal ? id : LookupIdForIsa(sig, InstructionSet_AVX512v3, methodName);
        }
        else if (isa is InstructionSet_AVX10v1_X64)
        {
            return LookupIdForIsa(sig, InstructionSet_AVX512_X64, methodName);
        }
#endif

        return LookupIdForIsa(sig, isa, methodName);
    }

    private static unsafe NamedIntrinsic LookupIdForIsa(CORINFO_SIG_INFO* sig, CORINFO_InstructionSet isa, ReadOnlySpan<byte> methodName)
    {
        var boundsIndex = (int)isa * 2;

        if ((uint)boundsIndex >= (uint)s_isaRangeBounds.Length)
        {
            return NI_Illegal;
        }

        var rangeLower = s_isaRangeBounds[boundsIndex];
        var rangeUpper = s_isaRangeBounds[boundsIndex + 1];

        if (rangeLower is -1)
        {
            return NI_Illegal;
        }

        while (rangeLower <= rangeUpper)
        {
            var rangeIndex = (rangeUpper + rangeLower) / 2;
            var sortOrder = CompareUtf8Name(methodName, s_names[rangeIndex]);

            if (sortOrder < 0)
            {
                rangeUpper = rangeIndex - 1;
            }
            else if (sortOrder > 0)
            {
                rangeLower = rangeIndex + 1;
            }
            else
            {
                var expectedArgCount = s_numArgs[rangeIndex];
                assert((expectedArgCount == byte.MaxValue) || (sig->numArgs == expectedArgCount));
                return (NamedIntrinsic)((int)NI_HW_INTRINSIC_START + rangeIndex + 1);
            }
        }

        return NI_Illegal;
    }

    private static int CompareUtf8Name(ReadOnlySpan<byte> utf8Name, string intrinsicName)
    {
        var compareLength = int.Min(utf8Name.Length, intrinsicName.Length);

        for (var index = 0; index < compareLength; index++)
        {
            var character = intrinsicName[index];
            assert(character <= byte.MaxValue);

            var comparison = utf8Name[index].CompareTo((byte)character);

            if (comparison is not 0)
            {
                return comparison;
            }
        }

        return utf8Name.Length.CompareTo(intrinsicName.Length);
    }
#endif

    private static int GetTableIndex(NamedIntrinsic id)
    {
        assert(id is > NI_HW_INTRINSIC_START and < NI_HW_INTRINSIC_END);
        return id - NI_HW_INTRINSIC_START - 1;
    }

    public static byte GetMultiRegCount(NamedIntrinsic id)
    {
        assert(IsMultiReg(id));

        switch (id)
        {
#if TARGET_ARM64
            case NI_AdvSimd_Arm64_LoadPairScalarVector64:
            case NI_AdvSimd_Arm64_LoadPairScalarVector64NonTemporal:
            case NI_AdvSimd_Arm64_LoadPairVector64:
            case NI_AdvSimd_Arm64_LoadPairVector64NonTemporal:
            case NI_AdvSimd_Arm64_LoadPairVector128:
            case NI_AdvSimd_Arm64_LoadPairVector128NonTemporal:
            case NI_AdvSimd_Load2xVector64AndUnzip:
            case NI_AdvSimd_Arm64_Load2xVector128AndUnzip:
            case NI_AdvSimd_Load2xVector64:
            case NI_AdvSimd_Arm64_Load2xVector128:
            case NI_AdvSimd_LoadAndInsertScalarVector64x2:
            case NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x2:
            case NI_AdvSimd_LoadAndReplicateToVector64x2:
            case NI_AdvSimd_Arm64_LoadAndReplicateToVector128x2:
            case NI_Sve_Load2xVectorAndUnzip:
            {
                return 2;
            }

            case NI_AdvSimd_Load3xVector64AndUnzip:
            case NI_AdvSimd_Arm64_Load3xVector128AndUnzip:
            case NI_AdvSimd_Load3xVector64:
            case NI_AdvSimd_Arm64_Load3xVector128:
            case NI_AdvSimd_LoadAndInsertScalarVector64x3:
            case NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x3:
            case NI_AdvSimd_LoadAndReplicateToVector64x3:
            case NI_AdvSimd_Arm64_LoadAndReplicateToVector128x3:
            case NI_Sve_Load3xVectorAndUnzip:
            {
                return 3;
            }

            case NI_AdvSimd_Load4xVector64AndUnzip:
            case NI_AdvSimd_Arm64_Load4xVector128AndUnzip:
            case NI_AdvSimd_Load4xVector64:
            case NI_AdvSimd_Arm64_Load4xVector128:
            case NI_AdvSimd_LoadAndInsertScalarVector64x4:
            case NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x4:
            case NI_AdvSimd_LoadAndReplicateToVector64x4:
            case NI_AdvSimd_Arm64_LoadAndReplicateToVector128x4:
            case NI_Sve_Load4xVectorAndUnzip:
            {
                return 4;
            }
#endif

#if TARGET_XARCH
            case NI_X86Base_DivRem:
            case NI_X86Base_X64_BigMul:
            case NI_X86Base_X64_DivRem:
            {
                return 2;
            }
#endif

            default:
            {
                unreached();
                return 0;
            }
        }
    }

    public static bool HasSpecialSideEffect(NamedIntrinsic id) => (lookupFlags(id) & HW_Flag_SpecialSideEffectMask) != 0;

    public static bool IsCommutative(NamedIntrinsic id) => (lookupFlags(id) & HW_Flag_Commutative) != 0;

    public static bool IsMaybeCommutative(NamedIntrinsic id)
    {
#if TARGET_XARCH
        return (lookupFlags(id) & HW_Flag_MaybeCommutative) != 0;
#else
        _ = lookupFlags(id);
        return false;
#endif
    }

    public static bool CanBenefitFromConstantProp(NamedIntrinsic id) => (lookupFlags(id) & HW_Flag_CanBenefitFromConstantProp) != 0;

    public static bool HasImmediateOperand(NamedIntrinsic id)
    {
#if TARGET_ARM64 || TARGET_WASM
        return (lookupFlags(id) & HW_Flag_HasImmediateOperand) != 0;
#elif TARGET_XARCH
        return lookupCategory(id) == HW_Category_IMM;
#else
        return false;
#endif
    }

    public static bool HasSpecialSideEffect_Barrier(NamedIntrinsic id) => (lookupFlags(id) & HW_Flag_SpecialSideEffect_Barrier) != 0;

    public static bool IsInvalidNodeId(NamedIntrinsic id) => (lookupFlags(id) & HW_Flag_InvalidNodeId) != 0;

#if TARGET_XARCH
    public static bool NeedsNormalizeSmallTypeToInt(NamedIntrinsic id) => (lookupFlags(id) & HW_Flag_NormalizeSmallTypeToInt) != 0;
#endif

    public static int lookupNumArgs(NamedIntrinsic id)
    {
        var count = s_numArgs[GetTableIndex(id)];
        return count == byte.MaxValue ? -1 : count;
    }

    public static bool IsMultiReg(NamedIntrinsic id) => (lookupFlags(id) & HW_Flag_MultiReg) != 0;

    public static HWIntrinsicCategory lookupCategory(NamedIntrinsic id)
    {
        return s_categories[GetTableIndex(id)];
    }

    public static CORINFO_InstructionSet lookupIsa(NamedIntrinsic id)
    {
        return s_instructionSets[GetTableIndex(id)];
    }

    public static HWIntrinsicFlag lookupFlags(NamedIntrinsic id)
    {
        return s_flags[GetTableIndex(id)];
    }

    public static bool ReturnsBoolean(NamedIntrinsic id) => (lookupFlags(id) & HW_Flag_ReturnsBoolean) != 0;

    public static bool ReturnsPerElementMask(NamedIntrinsic id) => (lookupFlags(id) & HW_Flag_ReturnsPerElementMask) != 0;

#if TARGET_XARCH
    public static bool IsVariableShift(NamedIntrinsic id) => id is NI_AVX2_ShiftLeftLogicalVariable or
        NI_AVX2_ShiftRightArithmeticVariable or NI_AVX2_ShiftRightLogicalVariable or
        NI_AVX512_ShiftLeftLogicalVariable or NI_AVX512_ShiftRightArithmeticVariable or NI_AVX512_ShiftRightLogicalVariable;
#else
    public static bool IsVariableShift(NamedIntrinsic id) => false;
#endif

    public static bool ReturnsScalarT(NamedIntrinsic id) => (lookupFlags(id) & HW_Flag_ReturnsScalarT) != 0;

#if TARGET_XARCH
    public static byte lookupFltCost(NamedIntrinsic id)
    {
        return s_fltCosts[GetTableIndex(id)];
    }

    public static byte lookupIntCost(NamedIntrinsic id)
    {
        return s_intCosts[GetTableIndex(id)];
    }
#endif

#if DEBUG
    public static string lookupName(NamedIntrinsic id)
    {
        return s_names[GetTableIndex(id)];
    }
#endif

#if TARGET_XARCH
    public static bool MaybeMemoryLoad(NamedIntrinsic id)
    {
        var flags = lookupFlags(id);
        return (flags & HW_Flag_MaybeMemoryLoad) != 0;
    }

    public static bool MaybeMemoryStore(NamedIntrinsic id)
    {
        var flags = lookupFlags(id);
        return (flags & HW_Flag_MaybeMemoryStore) != 0;
    }
#endif
}
