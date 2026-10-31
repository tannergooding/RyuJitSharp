// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.ManagedInstructionSet;

namespace RyuJitSharp.UnitTests;

internal static unsafe class InstructionSetOrdinalMappingTests
{
    [Test]
    public static void NativeInstructionSetMappingsRoundTripAndPreserveFlagLayout()
    {
        Assert.That(Enum.GetUnderlyingType(typeof(CORINFO_InstructionSet)), Is.EqualTo(typeof(int)));
        Assert.That(sizeof(CORINFO_InstructionSetFlags), Is.EqualTo(16));

        var flags = default(CORINFO_InstructionSetFlags);
        long[] expectedFlags = [0, 0];
        var managedInstructionSets = new HashSet<ManagedInstructionSet>();
        var nativeOrdinals = new HashSet<int>();
        var mappedCount = 0;

        foreach (var instructionSet in Enum.GetValues<ManagedInstructionSet>())
        {
            if (!Globals.TryGetNativeInstructionSetOrdinal(instructionSet, out var nativeOrdinal))
            {
                continue;
            }

            mappedCount++;
            Assert.That(managedInstructionSets.Add(instructionSet), Is.True);
            Assert.That(nativeOrdinals.Add(nativeOrdinal), Is.True, $"Duplicate native ordinal {nativeOrdinal}.");
            Assert.That(Globals.TryGetManagedInstructionSet(nativeOrdinal, out var roundTripped), Is.True);
            Assert.That(roundTripped, Is.EqualTo(instructionSet));

            flags.AddInstructionSet(instructionSet);
            Assert.That(flags.HasInstructionSet(instructionSet), Is.True);
            flags.RemoveInstructionSet(instructionSet);
            Assert.That(flags.HasInstructionSet(instructionSet), Is.False);
            flags.AddInstructionSet(instructionSet);

            expectedFlags[nativeOrdinal / 64] |= 1L << (nativeOrdinal & 63);
        }

        Assert.That(mappedCount, Is.GreaterThanOrEqualTo(3));
        Assert.That(flags.GetInstructionFlagsFieldCount(), Is.EqualTo(2));

        var unknownOrdinal = 1;

        while (nativeOrdinals.Contains(unknownOrdinal))
        {
            unknownOrdinal++;
        }

        Assert.That(unknownOrdinal, Is.LessThan(126));
        var rawFlags = flags.GetFlagsRaw();
        rawFlags[unknownOrdinal / 64] |= 1L << (unknownOrdinal & 63);
        expectedFlags[unknownOrdinal / 64] |= 1L << (unknownOrdinal & 63);
        Assert.That(flags.HasNativeInstructionSetOrdinal(unknownOrdinal), Is.True);
        Assert.That(Globals.TryGetManagedInstructionSet(unknownOrdinal, out _), Is.False);
        Assert.That(flags.GetFlagsRaw()[0], Is.EqualTo(expectedFlags[0]));
        Assert.That(flags.GetFlagsRaw()[1], Is.EqualTo(expectedFlags[1]));

        Assert.That(Globals.TryGetNativeInstructionSetOrdinal((ManagedInstructionSet)int.MaxValue, out _), Is.False);
        Assert.That(Globals.TryGetManagedInstructionSet(-1, out _), Is.False);
        Assert.That(Globals.TryGetManagedInstructionSet(128, out _), Is.False);
        _ = Assert.Throws<PlatformNotSupportedException>(() => Globals.GetNativeInstructionSetOrdinal((ManagedInstructionSet)int.MaxValue));
    }

    [Test]
    public static void KnownNativeInstructionSetOrdinalsRemainTargetStable()
    {
        Assert.That((int)CORINFO_InstructionSet.InstructionSet_XARCH_AVX2, Is.EqualTo(3));
        Assert.That((int)CORINFO_InstructionSet.InstructionSet_XARCH_AVX10v1, Is.EqualTo(7));
        Assert.That((int)CORINFO_InstructionSet.InstructionSet_ARM64_Fp16, Is.EqualTo(7));
        Assert.That((int)CORINFO_InstructionSet.InstructionSet_RISCV64_Zba, Is.EqualTo(2));
        Assert.That((int)CORINFO_InstructionSet.InstructionSet_WASM32_PackedSimd, Is.EqualTo(2));
        Assert.That((int)CORINFO_InstructionSet.InstructionSet_WASM32_RelaxedSimd, Is.EqualTo(4));

        if (Configuration.TARGET_XARCH)
        {
            AssertNativeOrdinal(InstructionSet_AVX2, 3);
            AssertNativeOrdinal(InstructionSet_AVX10v1, 7);

            if (Configuration.TARGET_64BIT)
            {
                AssertNativeOrdinal(InstructionSet_X86Base_X64, 31);
                AssertNativeOrdinal(InstructionSet_AVX2_X64, 33);
            }
            else
            {
                Assert.That(Globals.TryGetNativeInstructionSetOrdinal(InstructionSet_X86Base_X64, out _), Is.False);
            }
        }
        else if (Configuration.TARGET_ARM64)
        {
            AssertNativeOrdinal(InstructionSet_Fp16, 7);
            AssertNativeOrdinal(InstructionSet_SvePmull128, 23);
            AssertNativeOrdinal(InstructionSet_SvePmull128_Arm64, 41);
        }
        else if (Configuration.TARGET_RISCV64)
        {
            AssertNativeOrdinal(InstructionSet_Zba, 2);
        }
        else if (Configuration.TARGET_WASM)
        {
            AssertNativeOrdinal(InstructionSet_PackedSimd, 2);
            AssertNativeOrdinal(InstructionSet_RelaxedSimd, 4);
        }
    }

    [Test]
    public static void WasmRelaxedSimdPreservesItsAbiAndReadyToRunIdentities()
    {
        Assume.That(Configuration.TARGET_WASM, "This test requires the Wasm target.");

        Assert.That((int)ReadyToRunInstructionSet.READYTORUN_INSTRUCTION_RelaxedSimd, Is.EqualTo(96));
        Assert.That(Globals.InstructionSetToString(InstructionSet_RelaxedSimd), Is.EqualTo("RelaxedSimd"));
        Assert.That(
            Globals.InstructionSetFromR2RInstructionSet(ReadyToRunInstructionSet.READYTORUN_INSTRUCTION_RelaxedSimd),
            Is.EqualTo(InstructionSet_RelaxedSimd));

        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        Assert.That(LookupInstructionSet(compiler, "RelaxedSimd"u8), Is.EqualTo(InstructionSet_RelaxedSimd));

        var validFlags = default(CORINFO_InstructionSetFlags);
        validFlags.AddInstructionSet(InstructionSet_WasmBase);
        validFlags.AddInstructionSet(InstructionSet_PackedSimd);
        validFlags.AddInstructionSet(InstructionSet_RelaxedSimd);
        validFlags = Globals.EnsureInstructionSetFlagsAreValid(validFlags);
        Assert.That(validFlags.HasInstructionSet(InstructionSet_WasmBase), Is.True);
        Assert.That(validFlags.HasInstructionSet(InstructionSet_PackedSimd), Is.True);
        Assert.That(validFlags.HasInstructionSet(InstructionSet_RelaxedSimd), Is.True);

        var missingPackedSimd = default(CORINFO_InstructionSetFlags);
        missingPackedSimd.AddInstructionSet(InstructionSet_WasmBase);
        missingPackedSimd.AddInstructionSet(InstructionSet_RelaxedSimd);
        missingPackedSimd = Globals.EnsureInstructionSetFlagsAreValid(missingPackedSimd);
        Assert.That(missingPackedSimd.HasInstructionSet(InstructionSet_RelaxedSimd), Is.False);
    }

    [Test]
    public static void SvePmull128PreservesItsAbiAndReadyToRunIdentities()
    {
        Assume.That(Configuration.TARGET_ARM64, "This test requires the ARM64 target.");

        Assert.That((int)CORINFO_InstructionSet.InstructionSet_ARM64_SvePmull128, Is.EqualTo(23));
        Assert.That((int)CORINFO_InstructionSet.InstructionSet_ARM64_SvePmull128_Arm64, Is.EqualTo(41));
        Assert.That((int)ReadyToRunInstructionSet.READYTORUN_INSTRUCTION_SvePmull128, Is.EqualTo(96));
        Assert.That(Globals.InstructionSetToString(InstructionSet_SvePmull128), Is.EqualTo("SvePmull128"));
        Assert.That(
            Globals.InstructionSetFromR2RInstructionSet(ReadyToRunInstructionSet.READYTORUN_INSTRUCTION_SvePmull128),
            Is.EqualTo(InstructionSet_SvePmull128));

        var validFlags = default(CORINFO_InstructionSetFlags);
        validFlags.AddInstructionSet(InstructionSet_ArmBase);
        validFlags.AddInstructionSet(InstructionSet_ArmBase_Arm64);
        validFlags.AddInstructionSet(InstructionSet_AdvSimd);
        validFlags.AddInstructionSet(InstructionSet_AdvSimd_Arm64);
        validFlags.AddInstructionSet(InstructionSet_Sve);
        validFlags.AddInstructionSet(InstructionSet_Sve_Arm64);
        validFlags.AddInstructionSet(InstructionSet_SvePmull128);
        validFlags.AddInstructionSet(InstructionSet_SvePmull128_Arm64);
        validFlags = Globals.EnsureInstructionSetFlagsAreValid(validFlags);
        Assert.That(validFlags.HasInstructionSet(InstructionSet_SvePmull128), Is.True);
        Assert.That(validFlags.HasInstructionSet(InstructionSet_SvePmull128_Arm64), Is.True);
    }

    [Test]
    public static void NativeAliasesMatchActiveTargetOrdinals()
    {
        var targetPrefix = Configuration.TARGET_XARCH ? "XARCH"
            : Configuration.TARGET_ARM64 ? "ARM64"
            : Configuration.TARGET_RISCV64 ? "RISCV64"
            : Configuration.TARGET_WASM ? "WASM32"
            : throw new PlatformNotSupportedException("Instruction-set aliases are unavailable for this target.");

        foreach (var instructionSet in Enum.GetValues<ManagedInstructionSet>())
        {
            if (!Globals.TryGetNativeInstructionSetOrdinal(instructionSet, out var nativeOrdinal))
            {
                continue;
            }

            if (instructionSet is InstructionSet_ILLEGAL or InstructionSet_Vector or InstructionSet_NONE)
            {
                continue;
            }

            var managedName = instructionSet.ToString();
            var nativeAliasName = $"InstructionSet_{targetPrefix}_{managedName["InstructionSet_".Length..]}";
            Assert.That(Enum.TryParse<CORINFO_InstructionSet>(nativeAliasName, out var nativeAlias), Is.True, nativeAliasName);
            Assert.That((int)nativeAlias, Is.EqualTo(nativeOrdinal), nativeAliasName);
        }
    }

    [Test]
    public static void TargetVariantHelpersPreserveNativeFlags()
    {
        var flags = default(CORINFO_InstructionSetFlags);

        if (Configuration.TARGET_XARCH && Configuration.TARGET_64BIT)
        {
            flags.AddInstructionSet(InstructionSet_AVX2);
            flags.Set64BitInstructionSetVariants();
            Assert.That(flags.HasInstructionSet(InstructionSet_AVX2), Is.True);
            Assert.That(flags.HasInstructionSet(InstructionSet_AVX2_X64), Is.True);

            var incompleteFlags = default(CORINFO_InstructionSetFlags);
            incompleteFlags.AddInstructionSet(InstructionSet_AVX2);
            incompleteFlags = Globals.EnsureInstructionSetFlagsAreValid(incompleteFlags);
            Assert.That(incompleteFlags.HasInstructionSet(InstructionSet_AVX2), Is.False);
        }
        else if (Configuration.TARGET_XARCH)
        {
            flags.AddInstructionSet(InstructionSet_AVX2);
            flags.Set64BitInstructionSetVariants();
            Assert.That(flags.HasInstructionSet(InstructionSet_AVX2), Is.True);
            Assert.That(Globals.TryGetNativeInstructionSetOrdinal(InstructionSet_AVX2_X64, out _), Is.False);
        }
        else if (Configuration.TARGET_ARM64)
        {
            flags.AddInstructionSet(InstructionSet_AdvSimd);
            flags.Set64BitInstructionSetVariants();
            Assert.That(flags.HasInstructionSet(InstructionSet_AdvSimd), Is.True);
            Assert.That(flags.HasInstructionSet(InstructionSet_AdvSimd_Arm64), Is.True);

            var incompleteFlags = default(CORINFO_InstructionSetFlags);
            incompleteFlags.AddInstructionSet(InstructionSet_AdvSimd);
            incompleteFlags = Globals.EnsureInstructionSetFlagsAreValid(incompleteFlags);
            Assert.That(incompleteFlags.HasInstructionSet(InstructionSet_AdvSimd), Is.False);
        }
        else if (Configuration.TARGET_RISCV64)
        {
            flags.AddInstructionSet(InstructionSet_Zba);
            flags.Set64BitInstructionSetVariants();
            Assert.That(flags.HasInstructionSet(InstructionSet_Zba), Is.True);
        }
        else if (Configuration.TARGET_WASM)
        {
            flags.AddInstructionSet(InstructionSet_WasmBase);
            flags.AddInstructionSet(InstructionSet_PackedSimd);
            flags.AddInstructionSet(InstructionSet_RelaxedSimd);
            flags.Set64BitInstructionSetVariants();
            Assert.That(flags.HasInstructionSet(InstructionSet_PackedSimd), Is.True);
            Assert.That(flags.HasInstructionSet(InstructionSet_RelaxedSimd), Is.True);

            var incompleteFlags = default(CORINFO_InstructionSetFlags);
            incompleteFlags.AddInstructionSet(InstructionSet_RelaxedSimd);
            incompleteFlags = Globals.EnsureInstructionSetFlagsAreValid(incompleteFlags);
            Assert.That(incompleteFlags.HasInstructionSet(InstructionSet_RelaxedSimd), Is.False);
        }
    }

    [Test]
    public static void ReadyToRunInstructionSetsMapToTargetUnionIdentity()
    {
        if (Configuration.TARGET_XARCH)
        {
            Assert.That(Globals.InstructionSetFromR2RInstructionSet(ReadyToRunInstructionSet.READYTORUN_INSTRUCTION_Avx2), Is.EqualTo(InstructionSet_AVX2));
        }
        else if (Configuration.TARGET_ARM64)
        {
            Assert.That(Globals.InstructionSetFromR2RInstructionSet(ReadyToRunInstructionSet.READYTORUN_INSTRUCTION_AdvSimd), Is.EqualTo(InstructionSet_AdvSimd));
        }
        else if (Configuration.TARGET_RISCV64)
        {
            Assert.That(Globals.InstructionSetFromR2RInstructionSet(ReadyToRunInstructionSet.READYTORUN_INSTRUCTION_Zba), Is.EqualTo(InstructionSet_Zba));
        }
        else if (Configuration.TARGET_WASM)
        {
            Assert.That(Globals.InstructionSetFromR2RInstructionSet(ReadyToRunInstructionSet.READYTORUN_INSTRUCTION_PackedSimd), Is.EqualTo(InstructionSet_PackedSimd));
            Assert.That(Globals.InstructionSetFromR2RInstructionSet(ReadyToRunInstructionSet.READYTORUN_INSTRUCTION_RelaxedSimd), Is.EqualTo(InstructionSet_RelaxedSimd));
        }
    }

    [Test]
    public static void NotifyInstructionSetUsageForwardsNativeOrdinal()
    {
        var instructionSet = InstructionSet_ILLEGAL;
        var expectedNativeInstructionSet = default(CORINFO_InstructionSet);
        var foundNativeInstructionSet = false;

        foreach (var candidate in Enum.GetValues<ManagedInstructionSet>())
        {
            if (candidate is InstructionSet_ILLEGAL or InstructionSet_Vector or InstructionSet_NONE)
            {
                continue;
            }

            if (Globals.TryGetNativeInstructionSetOrdinal(candidate, out var nativeOrdinal))
            {
                instructionSet = candidate;
                expectedNativeInstructionSet = (CORINFO_InstructionSet)nativeOrdinal;
                foundNativeInstructionSet = true;
                break;
            }
        }

        if (!foundNativeInstructionSet)
        {
            Assert.That(Globals.TryGetNativeInstructionSetOrdinal(instructionSet, out var nativeOrdinal), Is.True);
            expectedNativeInstructionSet = (CORINFO_InstructionSet)nativeOrdinal;
        }

        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.notifyInstructionSetUsage =
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_InstructionSet, bool, bool, byte>)
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_InstructionSet, byte, byte, byte>)&Notify;

        TestJitInfo info = new() { Interface = new() { lpVtbl = &vtable } };
        Assert.That(info.Interface.notifyInstructionSetUsage(instructionSet, true, true), Is.True);
        Assert.That(((ICorDynamicInfo*)&info)->notifyInstructionSetUsage(instructionSet, false, true), Is.False);
        Assert.That(info.Count, Is.EqualTo(2));
        Assert.That(info.InstructionSet, Is.EqualTo(expectedNativeInstructionSet));
        Assert.That(info.Supported, Is.Zero);
        Assert.That(info.Preserve, Is.EqualTo(1));
    }

    private static void AssertNativeOrdinal(ManagedInstructionSet instructionSet, int expectedNativeOrdinal)
    {
        Assert.That(Globals.TryGetNativeInstructionSetOrdinal(instructionSet, out var nativeOrdinal), Is.True);
        Assert.That(nativeOrdinal, Is.EqualTo(expectedNativeOrdinal));
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "lookupInstructionSet")]
    private static extern ManagedInstructionSet LookupInstructionSet(Compiler compiler, ReadOnlySpan<byte> className);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte Notify(ICorJitInfo* self, CORINFO_InstructionSet instructionSet, byte supported, byte preserve)
    {
        var info = (TestJitInfo*)self;
        info->Count++;
        info->InstructionSet = instructionSet;
        info->Supported = supported;
        info->Preserve = preserve;
        return supported;
    }

    private struct TestJitInfo
    {
        public ICorJitInfo Interface;
        public CORINFO_InstructionSet InstructionSet;
        public int Count;
        public byte Supported;
        public byte Preserve;
    }
}
