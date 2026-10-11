// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.ManagedInstructionSet;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

#if FEATURE_HW_INTRINSICS && FEATURE_SIMD
internal static unsafe class HWIntrinsicLookupRangeTests
{
    [Test]
    public static void LookupUsesGeneratedIsaRangesAndVectorAliases()
    {
        CORINFO_SIG_INFO sig = default;
        sig.numArgs = 2;

        Assert.That(HWIntrinsicInfo.LookupId(&sig, InstructionSet_Vector, "op_Addition"u8), Is.EqualTo(NI_Vector_op_Addition));

#if TARGET_XARCH
        Assert.That(HWIntrinsicInfo.LookupId(&sig, InstructionSet_AVX2, "Add"u8), Is.EqualTo(NI_AVX2_Add));
#elif TARGET_ARM64
        Assert.That(HWIntrinsicInfo.LookupId(&sig, InstructionSet_AdvSimd, "Add"u8), Is.EqualTo(NI_AdvSimd_Add));
#elif TARGET_WASM
        Assert.That(HWIntrinsicInfo.LookupId(&sig, InstructionSet_PackedSimd, "Add"u8), Is.EqualTo(NI_PackedSimd_Add));
#else
#error Unsupported target with hardware-intrinsic and SIMD support.
#endif
    }

    [Test]
    public static void MetadataQueriesUseConfiguredTargetRows()
    {
        if (Configuration.TARGET_XARCH)
        {
            Assert.That(HWIntrinsicInfo.GetMultiRegCount(NI_X86Base_DivRem), Is.EqualTo(2));
            Assert.That(HWIntrinsicInfo.HasImmediateOperand(NI_AVX2_InsertVector128), Is.True);
            Assert.That(HWIntrinsicInfo.IsMaybeCommutative(NI_AVX_Min), Is.True);
            Assert.That(HWIntrinsicInfo.NeedsNormalizeSmallTypeToInt(NI_AVX2_InsertVector128), Is.True);
            Assert.That(HWIntrinsicInfo.AvxOnlyCompatible(NI_Vector_AsInt32), Is.True);
            Assert.That(HWIntrinsicInfo.MaybeMemoryLoad(NI_AVX2_GatherVector256), Is.True);
        }
        else if (Configuration.TARGET_ARM64)
        {
            Assert.That(HWIntrinsicInfo.GetMultiRegCount(NI_AdvSimd_Load3xVector64AndUnzip), Is.EqualTo(3));
            Assert.That(HWIntrinsicInfo.HasImmediateOperand(NI_AdvSimd_ShiftLeftLogical), Is.True);
            Assert.That(HWIntrinsicInfo.IsMaybeCommutative(NI_AdvSimd_Add), Is.False);
            Assert.That(HWIntrinsicInfo.NeedsNormalizeSmallTypeToInt(NI_AdvSimd_Add), Is.False);
            Assert.That(HWIntrinsicInfo.AvxOnlyCompatible(NI_AdvSimd_Add), Is.False);
            Assert.That(HWIntrinsicInfo.MaybeMemoryLoad(NI_AdvSimd_Load3xVector64AndUnzip), Is.False);
        }
        else if (Configuration.TARGET_WASM)
        {
            Assert.That(HWIntrinsicInfo.HasImmediateOperand(NI_PackedSimd_ExtractScalar), Is.True);
            Assert.That(HWIntrinsicInfo.IsMaybeCommutative(NI_PackedSimd_Add), Is.False);
            Assert.That(HWIntrinsicInfo.NeedsNormalizeSmallTypeToInt(NI_PackedSimd_Add), Is.False);
            Assert.That(HWIntrinsicInfo.AvxOnlyCompatible(NI_PackedSimd_Add), Is.False);
            Assert.That(HWIntrinsicInfo.MaybeMemoryStore(NI_PackedSimd_Store), Is.False);
        }
        else
        {
            Assert.Fail("The hardware-intrinsic test is running on an unsupported target.");
        }
    }

    [Test]
    public static void MetadataTableContentsMatchThePinnedTargetSnapshot()
    {
        var count = HWIntrinsicInfo.GetNativeTableEntryCount();
        var hash = AppendHash(14695981039346656037UL, (uint)count);
        int expectedCount;
        ulong expectedHash;

        if (Configuration.TARGET_XARCH)
        {
            expectedCount = 836;
#if DEBUG
            expectedHash = Configuration.TARGET_64BIT ? 0x3E4B226FF246FC63UL : 0x12753D926736CD8EUL;
#else
            expectedHash = Configuration.TARGET_64BIT ? 0x72D2520BCDBCF9B3UL : 0xDCA5B3763809FDE6UL;
#endif
        }
        else if (Configuration.TARGET_ARM64)
        {
            expectedCount = 1209;
#if DEBUG
            expectedHash = 0xFE294BA4C807E11BUL;
#else
            expectedHash = 0x4ADC8167113A6932UL;
#endif
        }
        else if (Configuration.TARGET_WASM)
        {
            expectedCount = 208;
#if DEBUG
            expectedHash = 0x2631CE8A5AACB2F1UL;
#else
            expectedHash = 0xFF4D09D72ECED4A5UL;
#endif
        }
        else
        {
            Assert.Fail("The hardware-intrinsic test is running on an unsupported target.");
            return;
        }

        for (var tableIndex = 0; tableIndex < count; tableIndex++)
        {
            var id = HWIntrinsicInfo.GetIntrinsicIdForNativeTableIndex(tableIndex);
            hash = AppendHash(hash, unchecked((uint)id));
            hash = AppendHash(hash, unchecked((uint)HWIntrinsicInfo.lookupCategory(id)));
            hash = AppendHash(hash, unchecked((uint)HWIntrinsicInfo.lookupFlags(id)));
            hash = AppendHash(hash, unchecked((uint)HWIntrinsicInfo.lookupIsa(id)));
            hash = AppendHash(hash, unchecked((uint)HWIntrinsicInfo.lookupNumArgs(id)));

            var hasSimdSize = HWIntrinsicInfo.tryLookupSimdSize(id, out var simdSize);
            hash = AppendHash(hash, hasSimdSize ? 1u : 0u);
            if (hasSimdSize)
            {
                hash = AppendHash(hash, simdSize);
            }

#if DEBUG
            var name = HWIntrinsicInfo.lookupName(id);
            hash = AppendHash(hash, (uint)name.Length);
            foreach (var character in name)
            {
                hash = AppendHash(hash, character);
            }
#endif

            for (var typeValue = (int)TYP_BYTE; typeValue <= (int)TYP_DOUBLE; typeValue++)
            {
                var instruction = HWIntrinsicInfo.lookupIns(id, (var_types)typeValue, compiler: null);
                hash = AppendHash(hash, unchecked((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(instruction)));
            }

            if (Configuration.TARGET_XARCH)
            {
                hash = AppendHash(hash, HWIntrinsicInfo.lookupIntCost(id));
                hash = AppendHash(hash, HWIntrinsicInfo.lookupFltCost(id));
            }
        }

        Assert.That(count, Is.EqualTo(expectedCount));
        Assert.That(hash, Is.EqualTo(expectedHash), $"HWI table snapshot mismatch: {count} rows, {hash:X16}.");
    }

    private static ulong AppendHash(ulong hash, uint value)
    {
        for (var shift = 0; shift < 32; shift += 8)
        {
            hash = unchecked((hash ^ (byte)(value >> shift)) * 1099511628211UL);
        }

        return hash;
    }
}
#endif
