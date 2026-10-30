// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Text;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public readonly partial struct HWIntrinsicInfo
{
    private static ReadOnlySpan<byte> GetName(int tableIndex)
    {
        var rangeIndex = tableIndex * 2;
        var byteIndex = GetRvaSpanEntry(s_nameRanges, rangeIndex);
        var byteLength = GetRvaSpanEntry(s_nameRanges, rangeIndex + 1);
        var name = GetRvaSpanSlice(s_nameData, byteIndex, byteLength);
        assert(GetRvaSpanEntry(s_nameData, byteIndex + byteLength) is 0);
        return name;
    }

    private static void EnsureHardwareIntrinsicSupport()
    {
        if (!Configuration.FEATURE_HW_INTRINSICS)
        {
            Globals.NYI("Hardware-intrinsic metadata without hardware-intrinsic support");
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.HardwareIntrinsicMetadataIsUnsupportedOnThisTarget);
            return;
        }
    }

    private static ReadOnlySpan<ushort> GetIsaRangeBounds()
    {
        EnsureHardwareIntrinsicSupport();

        if (Configuration.TARGET_XARCH)
        {
            return s_xarchIsaRangeBounds;
        }

        if (Configuration.TARGET_ARM64)
        {
            return s_arm64IsaRangeBounds;
        }

        if (Configuration.TARGET_WASM)
        {
            return s_wasmIsaRangeBounds;
        }

        JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.HardwareIntrinsicLookupRangesAreUnavailableForThisTarget);
        return default;
    }

    public static unsafe NamedIntrinsic LookupId(CORINFO_SIG_INFO* sig, ManagedInstructionSet isa, ReadOnlySpan<byte> methodName)
    {
        if (sig->hasThis() || isa is InstructionSet_NONE)
        {
            return NI_Illegal;
        }

        if (isa is InstructionSet_Vector)
        {
            return LookupIdForIsa(sig, InstructionSet_Vector128, methodName);
        }

        if (Configuration.TARGET_XARCH && isa is InstructionSet_AVX10v1)
        {
            var id = LookupIdForIsa(sig, InstructionSet_AVX512, methodName);

            if (id is not NI_Illegal)
            {
                return id;
            }

            id = LookupIdForIsa(sig, InstructionSet_AVX512v2, methodName);
            return id is not NI_Illegal ? id : LookupIdForIsa(sig, InstructionSet_AVX512v3, methodName);
        }
        else if (Configuration.TARGET_XARCH && Configuration.TARGET_64BIT && isa is InstructionSet_AVX10v1_X64)
        {
            return LookupIdForIsa(sig, InstructionSet_AVX512_X64, methodName);
        }

        return LookupIdForIsa(sig, isa, methodName);
    }

    private static unsafe NamedIntrinsic LookupIdForIsa(CORINFO_SIG_INFO* sig, ManagedInstructionSet isa, ReadOnlySpan<byte> methodName)
    {
        var isaOrdinal = (int)isa;
        var isaRangeBounds = GetIsaRangeBounds();

        if (isaOrdinal < 0 || isaOrdinal >= (isaRangeBounds.Length / 2))
        {
            return NI_Illegal;
        }

        var boundsIndex = isaOrdinal * 2;
        var rangeLower = (int)GetRvaSpanEntry(isaRangeBounds, boundsIndex);
        var rangeUpper = (int)GetRvaSpanEntry(isaRangeBounds, boundsIndex + 1);

        if (rangeLower is ushort.MaxValue)
        {
            return NI_Illegal;
        }

        while (rangeLower <= rangeUpper)
        {
            var rangeIndex = (rangeUpper + rangeLower) / 2;
            var sortOrder = methodName.SequenceCompareTo(GetName(rangeIndex));

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
                var expectedArgCount = GetRvaSpanEntry(s_numArgs, rangeIndex);
                assert((expectedArgCount == byte.MaxValue) || (sig->numArgs == expectedArgCount));
                return GetIntrinsicIdForNativeTableIndex(rangeIndex);
            }
        }

        return NI_Illegal;
    }

    private static int GetTableIndex(NamedIntrinsic id)
    {
        EnsureHardwareIntrinsicSupport();

        var unionIndex = (int)id - (int)NI_HW_INTRINSIC_START - 1;
        var tableIndices = GetTableIndexByUnionIndex();

        if ((uint)unionIndex >= (uint)tableIndices.Length)
        {
            JitExceptionHelpers.ThrowArgumentOutOfRangeException(FatalJitErrorCode.ValueIsNotAHardwareIntrinsicIdentity, FatalJitArgument.Id);
        }

        var tableIndex = GetRvaSpanEntry(tableIndices, unionIndex);

        if (tableIndex is ushort.MaxValue)
        {
            if (Configuration.DETAILED_FATAL_ERROR_MESSAGES)
            {
                JitExceptionHelpers.ThrowPlatformNotSupportedException(
                    FatalJitErrorCode.HardwareIntrinsicArgumentIsNotPresentInTheConfiguredTargetSNativeMetadataTable,
                    id);
            }
            else
            {
                JitExceptionHelpers.ThrowPlatformNotSupportedException(
                    FatalJitErrorCode.HardwareIntrinsicArgumentIsNotPresentInTheConfiguredTargetSNativeMetadataTable);
            }
        }

        return tableIndex;
    }

    internal readonly struct ResolvedMetadata
    {
        private readonly int _tableIndex;

        internal ResolvedMetadata(NamedIntrinsic id)
        {
            _tableIndex = GetTableIndex(id);
        }

        internal HWIntrinsicCategory Category => GetRvaSpanEntry(s_categories, _tableIndex);

        internal ManagedInstructionSet InstructionSet => GetRvaSpanEntry(s_instructionSets, _tableIndex);

        internal HWIntrinsicFlag Flags => GetRvaSpanEntry(s_flags, _tableIndex);

        internal int NumArgs
        {
            get
            {
                var count = GetRvaSpanEntry(s_numArgs, _tableIndex);
                return count == byte.MaxValue ? -1 : count;
            }
        }

        internal bool TryGetSimdSize(out uint simdSize)
        {
            var size = GetRvaSpanEntry(s_simdSizes, _tableIndex);

            if (size != byte.MaxValue)
            {
                simdSize = size;
                return true;
            }

            simdSize = 0;
            return false;
        }
    }

    internal static ResolvedMetadata ResolveMetadata(NamedIntrinsic id) =>
        new(id);

    internal static int GetNativeTableEntryCount()
    {
        if (!Configuration.FEATURE_HW_INTRINSICS)
        {
            return 0;
        }

        var count = GetUnionIndexByNativeTableIndex().Length;
        assert(s_categories.Length == count);
        assert(s_flags.Length == count);
        assert(s_fltCosts.Length == count);
        assert(s_instructions.Length == count * 10);
        assert(s_instructionSets.Length == count);
        assert(s_intCosts.Length == count);
        assert(s_nameRanges.Length == count * 2);
        assert(s_numArgs.Length == count);
        assert(s_simdSizes.Length == count);
        return count;
    }

    internal static int GetNativeTableIndex(NamedIntrinsic id)
    {
        return GetTableIndex(id);
    }

    internal static NamedIntrinsic GetIntrinsicIdForNativeTableIndex(int tableIndex)
    {
        var unionIndexes = GetUnionIndexByNativeTableIndex();

        if ((uint)tableIndex >= (uint)unionIndexes.Length)
        {
            JitExceptionHelpers.ThrowArgumentOutOfRangeException(FatalJitArgument.TableIndex);
        }

        var unionIndex = GetRvaSpanEntry(unionIndexes, tableIndex);
        return (NamedIntrinsic)((int)NI_HW_INTRINSIC_START + unionIndex + 1);
    }

    private static ReadOnlySpan<ushort> GetTableIndexByUnionIndex()
    {
        if (Configuration.TARGET_XARCH)
        {
            return s_XarchTableIndexByUnionIndex;
        }

        if (Configuration.TARGET_ARM64)
        {
            return s_Arm64TableIndexByUnionIndex;
        }

        if (Configuration.TARGET_WASM)
        {
            return s_WasmTableIndexByUnionIndex;
        }

        JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.HardwareIntrinsicTablesAreUnavailableForThisTarget);
        return default;
    }

    private static ReadOnlySpan<ushort> GetUnionIndexByNativeTableIndex()
    {
        if (Configuration.TARGET_XARCH)
        {
            return s_XarchUnionIndexByTableIndex;
        }

        if (Configuration.TARGET_ARM64)
        {
            return s_Arm64UnionIndexByTableIndex;
        }

        if (Configuration.TARGET_WASM)
        {
            return s_WasmUnionIndexByTableIndex;
        }

        JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.HardwareIntrinsicTablesAreUnavailableForThisTarget);
        return default;
    }

    public static bool IsCrossPlatformVectorIntrinsic(NamedIntrinsic id) =>
        lookupIsa(id) is InstructionSet_Vector;

    public static byte GetMultiRegCount(NamedIntrinsic id)
    {
        assert(IsMultiReg(id));

        switch (id)
        {
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

            case NI_X86Base_DivRem:
            case NI_X86Base_X64_BigMul:
            case NI_X86Base_X64_DivRem:
            {
                return 2;
            }

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
        var flags = lookupFlags(id);
        return Configuration.TARGET_XARCH && ((flags & HW_Flag_MaybeCommutative) != 0);
    }

    public static bool CanBenefitFromConstantProp(NamedIntrinsic id) => (lookupFlags(id) & HW_Flag_CanBenefitFromConstantProp) != 0;

    public static bool HasImmediateOperand(NamedIntrinsic id)
    {
        if (Configuration.TARGET_ARM64 || Configuration.TARGET_WASM)
        {
            return (lookupFlags(id) & HW_Flag_HasImmediateOperand) != 0;
        }

        if (Configuration.TARGET_XARCH)
        {
            return lookupCategory(id) == HW_Category_IMM;
        }

        return false;
    }

    public static bool HasSpecialSideEffect_Barrier(NamedIntrinsic id) => (lookupFlags(id) & HW_Flag_SpecialSideEffect_Barrier) != 0;

    public static bool IsInvalidNodeId(NamedIntrinsic id) => (lookupFlags(id) & HW_Flag_InvalidNodeId) != 0;

    public static bool NeedsNormalizeSmallTypeToInt(NamedIntrinsic id) =>
        Configuration.TARGET_XARCH && ((lookupFlags(id) & HW_Flag_NormalizeSmallTypeToInt) != 0);

    public static int lookupNumArgs(NamedIntrinsic id)
    {
        var count = GetRvaSpanEntry(s_numArgs, GetTableIndex(id));
        return count == byte.MaxValue ? -1 : count;
    }

    public static bool IsMultiReg(NamedIntrinsic id) => (lookupFlags(id) & HW_Flag_MultiReg) != 0;

    public static HWIntrinsicCategory lookupCategory(NamedIntrinsic id)
    {
        return GetRvaSpanEntry(s_categories, GetTableIndex(id));
    }

    public static ManagedInstructionSet lookupIsa(NamedIntrinsic id)
    {
        return GetRvaSpanEntry(s_instructionSets, GetTableIndex(id));
    }

    public static HWIntrinsicFlag lookupFlags(NamedIntrinsic id)
    {
        return GetRvaSpanEntry(s_flags, GetTableIndex(id));
    }

    internal static bool tryLookupSimdSize(NamedIntrinsic id, out uint simdSize)
    {
        var size = GetRvaSpanEntry(s_simdSizes, GetTableIndex(id));

        if (size != byte.MaxValue)
        {
            simdSize = size;
            return true;
        }

        simdSize = 0;
        return false;
    }

    public static unsafe uint lookupSimdSize(Compiler compiler, NamedIntrinsic id, in CORINFO_SIG_INFO sig)
    {
        if (tryLookupSimdSize(id, out var simdSize))
        {
            return simdSize;
        }

        CORINFO_CLASS_HANDLE typeHnd;

        fixed (CORINFO_SIG_INFO* sigPtr = &sig)
        {
            if (BaseTypeFromFirstArg(id))
            {
                typeHnd = compiler.info.compCompHnd->getArgClass(sigPtr, sig.args);
            }
            else if (BaseTypeFromSecondArg(id))
            {
                var secondArg = compiler.info.compCompHnd->getArgNext(sig.args);
                typeHnd = compiler.info.compCompHnd->getArgClass(sigPtr, secondArg);
            }
            else
            {
                assert(sig.retType.VarType == TYP_STRUCT);
                typeHnd = sig.retTypeSigClass;
            }
        }

        if (!Configuration.FEATURE_SIMD)
        {
            Globals.NYI("Hardware-intrinsic SIMD size lookup without SIMD support");
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.HardwareIntrinsicSIMDSizeLookupIsUnsupportedOnThisTarget);
            return default!;
        }

        var simdBaseType = compiler.getBaseTypeAndSizeOfSimdType(typeHnd, out var sizeBytes);
        assert((sizeBytes > 0) && (simdBaseType != TYP_UNDEF));
        return unchecked((uint)sizeBytes);
    }

    public static bool BaseTypeFromFirstArg(NamedIntrinsic id) =>
        (lookupFlags(id) & HW_Flag_BaseTypeFromFirstArg) != 0;

    public static bool BaseTypeFromSecondArg(NamedIntrinsic id) =>
        (lookupFlags(id) & HW_Flag_BaseTypeFromSecondArg) != 0;

    public static bool ReturnsBoolean(NamedIntrinsic id) => (lookupFlags(id) & HW_Flag_ReturnsBoolean) != 0;

    public static bool ReturnsPerElementMask(NamedIntrinsic id) =>
        Configuration.FEATURE_MASKED_HW_INTRINSICS && ((lookupFlags(id) & HW_Flag_ReturnsPerElementMask) != 0);

    public static bool IsFloatingPointUsed(NamedIntrinsic id) => (lookupFlags(id) & HW_Flag_NoFloatingPointUsed) == 0;

    public static bool AvxOnlyCompatible(NamedIntrinsic id) =>
        Configuration.TARGET_XARCH && ((lookupFlags(id) & HW_Flag_AvxOnlyCompatible) != 0);

    public static bool IsVariableShift(NamedIntrinsic id) => Configuration.TARGET_XARCH && (id is NI_AVX2_ShiftLeftLogicalVariable or
        NI_AVX2_ShiftRightArithmeticVariable or NI_AVX2_ShiftRightLogicalVariable or
        NI_AVX512_ShiftLeftLogicalVariable or NI_AVX512_ShiftRightArithmeticVariable or NI_AVX512_ShiftRightLogicalVariable);

    public static bool ReturnsScalarT(NamedIntrinsic id) => (lookupFlags(id) & HW_Flag_ReturnsScalarT) != 0;

    public static byte lookupFltCost(NamedIntrinsic id)
    {
        if (!Configuration.TARGET_XARCH)
        {
            JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.FloatingPointIntrinsicCostsAreOnlyDefinedForXarch);
        }

        return GetRvaSpanEntry(s_fltCosts, GetTableIndex(id));
    }

    public static byte lookupIntCost(NamedIntrinsic id)
    {
        if (!Configuration.TARGET_XARCH)
        {
            JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.IntegerIntrinsicCostsAreOnlyDefinedForXarch);
        }

        return GetRvaSpanEntry(s_intCosts, GetTableIndex(id));
    }

    public static string lookupName(NamedIntrinsic id)
    {
        return Encoding.UTF8.GetString(GetName(GetTableIndex(id)));
    }

    public static bool MaybeMemoryLoad(NamedIntrinsic id)
    {
        if (!Configuration.TARGET_XARCH)
        {
            return false;
        }

        var flags = lookupFlags(id);
        return (flags & HW_Flag_MaybeMemoryLoad) != 0;
    }

    public static bool MaybeMemoryStore(NamedIntrinsic id)
    {
        if (!Configuration.TARGET_XARCH)
        {
            return false;
        }

        var flags = lookupFlags(id);
        return (flags & HW_Flag_MaybeMemoryStore) != 0;
    }
}
