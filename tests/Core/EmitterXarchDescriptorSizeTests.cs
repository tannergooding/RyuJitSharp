// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.instruction;

namespace RyuJitSharp.UnitTests;

internal static class EmitterXarchDescriptorSizeTests
{
    [TestCase(IF_NONE, false, false, false)]
    [TestCase(IF_NONE, true, false, false)]
    [TestCase(IF_XARCH_LABEL, false, false, false)]
    [TestCase(IF_XARCH_SWR_LABEL, false, false, false)]
    [TestCase(IF_XARCH_METHOD, false, false, false)]
    [TestCase(IF_XARCH_MRD, false, false, false)]
    [TestCase(IF_XARCH_MRD, false, false, true)]
    [TestCase(IF_XARCH_MRD, false, true, false)]
    [TestCase(IF_XARCH_MRD, false, true, true)]
    [TestCase(IF_XARCH_CNS, true, false, false)]
    [TestCase(IF_XARCH_CNS, false, false, false)]
    [TestCase(IF_XARCH_CNS, false, true, false)]
    [TestCase(IF_XARCH_SRD_CNS, false, true, false)]
    [TestCase(IF_XARCH_MWR, false, false, true)]
    [TestCase(IF_XARCH_MRD_CNS, false, true, true)]
    [TestCase(IF_XARCH_AWR, false, false, true)]
    [TestCase(IF_XARCH_ARD_CNS, false, true, false)]
    [TestCase(IF_XARCH_ARD_CNS, false, true, true)]
    public static void OperandCategoryAndPayloadFlagsDetermineNativeSize(
        Emitter.insFormat format, bool small, bool largeConstant, bool largeDisplacement)
    {
        Assume.That(Configuration.TARGET_XARCH);
        var descriptor = new FlagDescriptor();
        descriptor.idIns(format == IF_XARCH_METHOD ? INS_call : INS_mov);
        descriptor.idInsFmt(format);
        if (small)
        {
            descriptor.idSetIsSmallDsc();
        }
        if (largeConstant)
        {
            descriptor.idSetIsLargeCns();
        }
        if (largeDisplacement)
        {
            descriptor.idSetIsLargeDsp();
        }

        Assert.That(Size(CreateEmitter(), descriptor),
            Is.EqualTo(ExpectedNativeSize(format, small, largeConstant, largeDisplacement)));
    }

    [Test]
    public static void AlignmentFormatUsesItsNativeDescriptorLayout()
    {
        Assume.That(Configuration.TARGET_XARCH);
        var descriptor = new FlagDescriptor();
        descriptor.idIns(INS_align);
        descriptor.idInsFmt(IF_NONE);

        var expected = Configuration.TARGET_X86
            ? TestConfiguration.IsDebug ? 28 : 24
            : TestConfiguration.IsDebug ? 48 : 40;
        Assert.That(Size(CreateEmitter(), descriptor), Is.EqualTo(expected));
    }

    [Test]
    public static void FatCallDescriptorTakesPrecedenceOverConstantAndDisplacementTags()
    {
        Assume.That(Configuration.TARGET_AMD64);
        var type = typeof(Emitter).GetNestedType("instrDescCGCA", BindingFlags.NonPublic)
            ?? throw new AssertionException("Missing call descriptor type.");
        var descriptor = (Emitter.instrDesc)RuntimeHelpers.GetUninitializedObject(type);
        descriptor.idIns(INS_call);
        descriptor.idInsFmt(IF_XARCH_METHOD);
        descriptor.idSetIsCall();
        descriptor.idSetIsLargeCns();
        descriptor.idSetIsLargeDsp();

        Assert.That(Size(CreateEmitter(), descriptor), Is.EqualTo(72));
    }

    [TestCase(-32768, true)]
    [TestCase(-32767, false)]
    [TestCase(32767, false)]
    [TestCase(32768, true)]
    public static unsafe void X86PackedAddressDisplacementReservesItsMostNegativeValue(int displacement, bool large)
    {
        Assume.That(Configuration.TARGET_X86);
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compCurBB = new BasicBlock(null, null);
        var emitter = new CodeGen(compiler).Emitter;
        emitter.emitBegCG(compiler, default);
        emitter.Init();
        emitter.emitBegFN(false);

        var descriptor = NewAddress(emitter, emitAttr.EA_4BYTE, displacement);
        Assert.That(descriptor.idIsLargeDsp(), Is.EqualTo(large));
        var expectedSize = Configuration.TARGET_X86 ? (large ? 16 : 12) : (large ? 24 : 16);
        Assert.That(descriptor.NativeLogicalSize, Is.EqualTo(expectedSize));
        Assert.That(descriptor.StorageSize,
            Is.EqualTo(unchecked((nuint)(expectedSize + (TestConfiguration.IsDebug ? 4 : 0)))));
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNewInstrAmd")]
    private static extern Emitter.instrDesc NewAddress(Emitter emitter, emitAttr size, nint displacement);

    private static Emitter CreateEmitter()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        return new CodeGen(compiler).Emitter;
    }

    private static int ExpectedNativeSize(
        Emitter.insFormat format, bool small, bool largeConstant, bool largeDisplacement)
    {
        if (small)
        {
            return 8;
        }

        var full = Configuration.TARGET_X86 ? 12 : 16;
        var extension = Configuration.TARGET_X86 ? 4 : 8;
        return format switch
        {
            IF_XARCH_LABEL => Configuration.TARGET_X86 ? 28 : 48,
            IF_XARCH_SWR_LABEL => Configuration.TARGET_X86 ? 32 : 56,
            IF_XARCH_MRD or IF_XARCH_CNS or IF_XARCH_SRD_CNS or IF_XARCH_MWR or
                IF_XARCH_MRD_CNS or IF_XARCH_AWR or IF_XARCH_ARD_CNS =>
                full + (extension * ((largeConstant ? 1 : 0) + (largeDisplacement ? 1 : 0))),
            _ => full,
        };
    }

    private sealed class FlagDescriptor : Emitter.instrDesc
    {
        public override int NativeLogicalSize => throw new AssertionException("Sizing must inspect native descriptor flags.");
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitSizeOfInsDsc")]
    private static extern int Size(Emitter emitter, Emitter.instrDesc descriptor);
}
