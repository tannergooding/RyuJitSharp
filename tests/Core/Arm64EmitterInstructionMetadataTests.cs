// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;

namespace RyuJitSharp.UnitTests;

internal static class Arm64EmitterInstructionMetadataTests
{
    [Test]
    public static void InstructionClassificationsMatchNativeMetadata()
    {
        for (var index = 0; index < CodeGen.instInfo.Length; index++)
        {
            var ins = (instruction)index;
            var info = CodeGen.instInfo[index];

            Assert.That(IsCompare(null, ins), Is.EqualTo((info & 4) != 0), $"Compare classification for {ins}");
            Assert.That(IsVectorLong(null, ins), Is.EqualTo((info & 32) != 0), $"Long classification for {ins}");
            Assert.That(IsVectorNarrow(null, ins), Is.EqualTo((info & 64) != 0), $"Narrow classification for {ins}");
            Assert.That(IsVectorWide(null, ins), Is.EqualTo((info & 16) != 0), $"Wide classification for {ins}");
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(4096)]
    public static void InstructionsBeyondMetadataHaveNoClassification(int offset)
    {
        var ins = (instruction)(CodeGen.instInfo.Length + offset);

        AssertNoClassification(ins);
    }

    [Test]
    public static void MaximumInstructionValueHasNoClassification()
    {
        AssertNoClassification(unchecked((instruction)uint.MaxValue));
    }

    [TestCase(INS_ldrb, EA_8BYTE, EA_4BYTE, EA_1BYTE)]
    [TestCase(INS_ldrsb, EA_4BYTE, EA_4BYTE, EA_1BYTE)]
    [TestCase(INS_ldrsb, EA_8BYTE, EA_8BYTE, EA_1BYTE)]
    [TestCase(INS_ldrsw, EA_4BYTE, EA_8BYTE, EA_4BYTE)]
    [TestCase(INS_ldr, EA_8BYTE, EA_8BYTE, EA_8BYTE)]
    [TestCase(INS_ldp, EA_16BYTE, EA_16BYTE, EA_16BYTE)]
    public static void LoadAndStoreSizesMatchInstructionSemantics(
        instruction ins, emitAttr opSize, emitAttr expectedTarget, emitAttr expectedAccess)
    {
        var descriptor = new TestDescriptor();
        descriptor.idIns(ins);
        descriptor.idOpSize(opSize);

        Assert.That(TargetRegisterSize(null, descriptor), Is.EqualTo(expectedTarget));
        Assert.That(LoadStoreSize(null, descriptor), Is.EqualTo(expectedAccess));
    }

    [TestCase(INS_OPTS_8B, EA_8BYTE)]
    [TestCase(INS_OPTS_4H, EA_8BYTE)]
    [TestCase(INS_OPTS_2S, EA_8BYTE)]
    [TestCase(INS_OPTS_1D, EA_8BYTE)]
    [TestCase(INS_OPTS_16B, EA_16BYTE)]
    [TestCase(INS_OPTS_8H, EA_16BYTE)]
    [TestCase(INS_OPTS_4S, EA_16BYTE)]
    [TestCase(INS_OPTS_2D, EA_16BYTE)]
    public static void ArrangementDataSizeMatchesNativeVectorWidth(insOpts arrangement, emitAttr expected)
    {
        Assert.That(GetDatasize(null, arrangement), Is.EqualTo(expected));
    }

    [TestCase(EA_1BYTE, EA_2BYTE)]
    [TestCase(EA_2BYTE, EA_4BYTE)]
    [TestCase(EA_4BYTE, EA_8BYTE)]
    public static void WidenedDataSizeDoublesTheElementWidth(emitAttr size, emitAttr expected)
    {
        Assert.That(WidenDatasize(null, size), Is.EqualTo(expected));
    }

    private sealed class TestDescriptor : Emitter.instrDesc
    {
        public override int NativeLogicalSize
            => throw new AssertionException("Size helpers must inspect instruction metadata.");
    }

    private static void AssertNoClassification(instruction ins)
    {
        Assert.That(IsCompare(null, ins), Is.False);
        Assert.That(IsVectorLong(null, ins), Is.False);
        Assert.That(IsVectorNarrow(null, ins), Is.False);
        Assert.That(IsVectorWide(null, ins), Is.False);
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "emitInsIsCompareArm64")]
    private static extern bool IsCompare(Emitter? emitter, instruction ins);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "emitInsIsVectorLongArm64")]
    private static extern bool IsVectorLong(Emitter? emitter, instruction ins);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "emitInsIsVectorNarrowArm64")]
    private static extern bool IsVectorNarrow(Emitter? emitter, instruction ins);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "emitInsIsVectorWideArm64")]
    private static extern bool IsVectorWide(Emitter? emitter, instruction ins);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "emitInsTargetRegSize")]
    private static extern emitAttr TargetRegisterSize(Emitter? emitter, Emitter.instrDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "emitInsLoadStoreSize")]
    private static extern emitAttr LoadStoreSize(Emitter? emitter, Emitter.instrDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "optGetDatasize")]
    private static extern emitAttr GetDatasize(Emitter? emitter, insOpts arrangement);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "widenDatasize")]
    private static extern emitAttr WidenDatasize(Emitter? emitter, emitAttr size);
}
#endif
