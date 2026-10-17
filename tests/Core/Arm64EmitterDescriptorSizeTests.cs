// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.instruction;

namespace RyuJitSharp.UnitTests;

internal static class Arm64EmitterDescriptorSizeTests
{
    [SetUp]
    public static void RequireArm64Target()
    {
        Assume.That(Configuration.TARGET_ARM64, "This fixture requires the ARM64 target.");
    }

    [Test]
    public static void OperandCategoriesMatchEveryPinnedFormat()
    {
        var text = new StringBuilder();
        foreach (var kind in Enum.GetValues<ID_OPS>())
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"{kind}:{(int)kind}\n");
        }

        var operands = OperandCategories(null);
        Assert.That(operands.Length, Is.EqualTo(599));
        for (var index = 0; index < operands.Length; index++)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"{index}:{(Emitter.insFormat)index}:{operands[index]}\n");
        }

        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
        Assert.That(hash, Is.EqualTo("507050ec2548319b8e52bdaa4f89052d7907624a152fe333bd5299f92badf628"));
    }

    [TestCase("instrDescCns", 24)]
    [TestCase("instrDescDsp", 24)]
    [TestCase("instrDescCnsDsp", 32)]
    [TestCase("instrDescLclVarPair", 24)]
    [TestCase("instrDescLclVarPairCns", 32)]
    [TestCase("instrDescJmp", 48)]
    [TestCase("instrDescCGCA", 80)]
    public static void ManagedDescriptorStorageMatchesNativeLayouts(string name, int expected)
    {
        if ((name is "instrDescCGCA") && !Configuration.TARGET_WINDOWS)
        {
            Assert.Ignore("The large-call descriptor uses the Windows ARM64 layout.");
        }

        var type = typeof(Emitter).GetNestedType(name, BindingFlags.NonPublic)
            ?? throw new AssertionException($"Missing descriptor type {name}.");
        if (RuntimeHelpers.GetUninitializedObject(type) is not Emitter.instrDesc descriptor)
        {
            throw new AssertionException($"Invalid descriptor type {name}.");
        }

        Assert.That(descriptor.NativeLogicalSize, Is.EqualTo(expected));
    }

    [Test]
    public static void AlignmentDescriptorMatchesConfiguredNativeLayout()
    {
        var type = typeof(Emitter).GetNestedType("instrDescAlign", BindingFlags.NonPublic)
            ?? throw new AssertionException("Missing descriptor type instrDescAlign.");
        if (RuntimeHelpers.GetUninitializedObject(type) is not Emitter.instrDesc descriptor)
        {
            throw new AssertionException("Invalid descriptor type instrDescAlign.");
        }

        Assert.That(descriptor.NativeLogicalSize, Is.EqualTo(ExpectedAlignDescriptorSize));
    }

    [TestCase(0, 16)]
    [TestCase(1, 24)]
    [TestCase(2, 24)]
    [TestCase(3, 32)]
    [TestCase(4, 24)]
    [TestCase(5, 32)]
    [TestCase(6, 24)]
    [TestCase(7, 32)]
    public static void ExtendedPayloadSizesFollowNativeFlags(int flags, int expected)
    {
        var descriptor = Descriptor(INS_nop, IF_ARM64_SN_0A);
        if ((flags & 1) != 0)
        {
            descriptor.idSetIsLargeCns();
        }
        if ((flags & 2) != 0)
        {
            descriptor.idSetIsLargeDsp();
        }
        if ((flags & 4) != 0)
        {
            SetLocalPair(descriptor);
        }

        Assert.That(Size(CreateEmitter(), descriptor), Is.EqualTo(expected));
    }

    [Test]
    public static void SmallDescriptorPrecedesFormatClassification()
    {
        var descriptor = new FlagDescriptor();
        descriptor.idSetIsSmallDsc();

        Assert.That(Size(CreateEmitter(), descriptor), Is.EqualTo(8));
    }

    [TestCase(false, 16)]
    [TestCase(true, 80)]
    public static void CallsUseTheirNativePayloadSize(bool large, int expected)
    {
        if (large && !Configuration.TARGET_WINDOWS)
        {
            Assert.Ignore("The large-call descriptor uses the Windows ARM64 layout.");
        }

        var descriptor = Descriptor(INS_blr, IF_ARM64_BR_1B);
        if (large)
        {
            descriptor.idSetIsLargeCall();
        }

        Assert.That(Size(CreateEmitter(), descriptor), Is.EqualTo(expected));
    }

    [Test]
    public static void FatCallLayoutRemainsAnExplicitDependency()
    {
        if (Configuration.TARGET_WINDOWS)
        {
            Assert.Ignore("The non-Windows ARM64 target has no native large-call descriptor.");
        }

        var descriptor = Descriptor(INS_blr, IF_ARM64_BR_1B);
        descriptor.idSetIsLargeCall();

        _ = Assert.Throws<PlatformNotSupportedException>(() => Size(CreateEmitter(), descriptor));
    }

    [TestCase(IF_ARM64_LABEL)]
    [TestCase(IF_ARM64_LARGEJMP)]
    [TestCase(IF_ARM64_LARGEADR)]
    [TestCase(IF_ARM64_LARGELDC)]
    public static void LabelFormatsUseJumpStorage(Emitter.insFormat format)
    {
        Assert.That(Size(CreateEmitter(), Descriptor(INS_b, format)), Is.EqualTo(48));
    }

    [Test]
    public static void AlignmentStorageMatchesConfiguredNativeLayout()
    {
        Assert.That(Size(CreateEmitter(), Descriptor(INS_align, IF_ARM64_SN_0A)), Is.EqualTo(ExpectedAlignDescriptorSize));
    }

    private static int ExpectedAlignDescriptorSize =>
        Configuration.DEBUG || Configuration.LATE_DISASM ? 48 : 40;

    private static FlagDescriptor Descriptor(instruction instruction, Emitter.insFormat format)
    {
        var descriptor = new FlagDescriptor();
        descriptor.idIns(instruction);
        descriptor.idInsFmt(format);
        return descriptor;
    }

    private static Emitter CreateEmitter()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        return new CodeGen(compiler).Emitter;
    }

    private sealed class FlagDescriptor : Emitter.instrDesc
    {
        public override int NativeLogicalSize => throw new AssertionException("ARM64 sizing must inspect the native flags.");
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitSizeOfInsDsc")]
    private static extern int Size(Emitter emitter, Emitter.instrDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "idSetIsLclVarPair")]
    private static extern void SetLocalPair(Emitter.instrDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "get_emitFmtToOps")]
    private static extern ReadOnlySpan<byte> OperandCategories(Emitter? emitter);
}
