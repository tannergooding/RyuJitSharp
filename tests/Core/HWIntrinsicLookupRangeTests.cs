// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.ManagedInstructionSet;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.instruction;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class HWIntrinsicLookupRangeTests
{
    [SetUp]
    public static void RequireHardwareIntrinsicsAndSimd()
    {
        Assume.That(Configuration.FEATURE_HW_INTRINSICS && Configuration.FEATURE_SIMD,
            "This fixture requires hardware intrinsics and SIMD support.");
    }

    [Test]
    public static void LookupUsesGeneratedIsaRangesAndVectorAliases()
    {
        CORINFO_SIG_INFO sig = default;
        sig.numArgs = 2;

        Assert.That(HWIntrinsicInfo.LookupId(&sig, InstructionSet_Vector, "op_Addition"u8), Is.EqualTo(NI_Vector_op_Addition));
        Assert.That(HWIntrinsicInfo.lookupName(NI_Vector_op_Addition), Is.EqualTo("op_Addition"));

        if (Configuration.TARGET_XARCH)
        {
            Assert.That(HWIntrinsicInfo.LookupId(&sig, InstructionSet_AVX2, "Add"u8), Is.EqualTo(NI_AVX2_Add));
        }
        else if (Configuration.TARGET_ARM64)
        {
            Assert.That(HWIntrinsicInfo.LookupId(&sig, InstructionSet_AdvSimd, "Add"u8), Is.EqualTo(NI_AdvSimd_Add));
            Assert.That(
                HWIntrinsicInfo.LookupId(&sig, InstructionSet_SvePmull128, "PolynomialMultiplyWideningEven"u8),
                Is.EqualTo(NI_SvePmull128_PolynomialMultiplyWideningEven));
            Assert.That(
                HWIntrinsicInfo.lookupIns(NI_SvePmull128_PolynomialMultiplyWideningEven, TYP_ULONG, compiler: null),
                Is.EqualTo(INS_sve_pmullb));
            Assert.That(
                HWIntrinsicInfo.lookupIsa(NI_SvePmull128_PolynomialMultiplyWideningEven),
                Is.EqualTo(InstructionSet_SvePmull128));
        }
        else if (Configuration.TARGET_WASM)
        {
            Assert.That(HWIntrinsicInfo.LookupId(&sig, InstructionSet_PackedSimd, "Add"u8), Is.EqualTo(NI_PackedSimd_Add));

            CORINFO_SIG_INFO relaxedSig = default;
            relaxedSig.numArgs = 1;
            Assert.That(
                HWIntrinsicInfo.LookupId(&relaxedSig, InstructionSet_RelaxedSimd, "ConvertToInt32Native"u8),
                Is.EqualTo(NI_RelaxedSimd_ConvertToInt32Native));
            Assert.That(HWIntrinsicInfo.LookupId(&sig, InstructionSet_RelaxedSimd, "SwizzleNative"u8),
                Is.EqualTo(NI_RelaxedSimd_SwizzleNative));
            Assert.That(HWIntrinsicInfo.lookupIsa(NI_RelaxedSimd_SwizzleNative), Is.EqualTo(InstructionSet_RelaxedSimd));
            Assert.That(
                HWIntrinsicInfo.lookupIns(NI_RelaxedSimd_SwizzleNative, TYP_BYTE, compiler: null),
                Is.EqualTo(INS_i8x16_relaxed_swizzle));
        }
        else
        {
            Assert.Fail("Unsupported target with hardware-intrinsic and SIMD support.");
        }
    }

    [Test]
    public static void RelaxedSimdClassNameMapsToItsInstructionSet()
    {
        Assume.That(Configuration.TARGET_WASM);

        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        Assert.That(LookupInstructionSet(compiler, "RelaxedSimd"u8), Is.EqualTo(InstructionSet_RelaxedSimd));
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
            Assert.That(HWIntrinsicInfo.IsCommutative(NI_RelaxedSimd_MaxNative), Is.True);
            Assert.That(HWIntrinsicInfo.HasImmediateOperand(NI_RelaxedSimd_SwizzleNative), Is.False);
            Assert.That(HWIntrinsicInfo.lookupIsa(NI_RelaxedSimd_SwizzleNative), Is.EqualTo(InstructionSet_RelaxedSimd));
            Assert.That(HWIntrinsicInfo.lookupIns(NI_RelaxedSimd_SwizzleNative, TYP_BYTE, compiler: null),
                Is.EqualTo(INS_i8x16_relaxed_swizzle));
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
            if (TestConfiguration.IsDebug)
            {
                expectedHash = Configuration.TARGET_64BIT ? 0x0DF10172D7F7CCE3UL : 0x12753D926736CD8EUL;
            }
            else
            {
                expectedHash = Configuration.TARGET_64BIT ? 0x8DBB7042D05347F3UL : 0xDCA5B3763809FDE6UL;
            }
        }
        else if (Configuration.TARGET_ARM64)
        {
            expectedCount = 1215;
            if (TestConfiguration.IsDebug)
            {
                expectedHash = 0x533AAF38E11C3091UL;
            }
            else
            {
                expectedHash = 0xD63A43E8538D0755UL;
            }
        }
        else if (Configuration.TARGET_WASM)
        {
            expectedCount = 219;
            if (TestConfiguration.IsDebug)
            {
                expectedHash = 0xEA0AB86F2863F123UL;
            }
            else
            {
                expectedHash = 0x7F9E1049142177D6UL;
            }
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

            if (TestConfiguration.IsDebug)
            {
                var name = HWIntrinsicInfo.lookupName(id);
                hash = AppendHash(hash, (uint)name.Length);
                foreach (var character in name)
                {
                    hash = AppendHash(hash, character);
                }
            }

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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "lookupInstructionSet")]
    private static extern ManagedInstructionSet LookupInstructionSet(Compiler compiler, ReadOnlySpan<byte> className);

    private static ulong AppendHash(ulong hash, uint value)
    {
        for (var shift = 0; shift < 32; shift += 8)
        {
            hash = unchecked((hash ^ (byte)(value >> shift)) * 1099511628211UL);
        }

        return hash;
    }
}
