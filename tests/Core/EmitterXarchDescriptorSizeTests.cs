// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_XARCH
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.instruction;

namespace RyuJitSharp.UnitTests;

internal static class EmitterXarchDescriptorSizeTests
{
#if TARGET_X86
    private const int Full = 12;
    private const int Extended = 16;
    private const int Combined = 20;
    private const int Jump = 28;
    private const int Label = 32;
#else
    private const int Full = 16;
    private const int Extended = 24;
    private const int Combined = 32;
    private const int Jump = 48;
    private const int Label = 56;
#endif

    [TestCase(IF_NONE, false, false, false, Full)]
    [TestCase(IF_NONE, true, false, false, 8)]
    [TestCase(IF_LABEL, false, false, false, Jump)]
    [TestCase(IF_SWR_LABEL, false, false, false, Label)]
    [TestCase(IF_METHOD, false, false, false, Full)]
    [TestCase(IF_MRD, false, false, false, Full)]
    [TestCase(IF_MRD, false, false, true, Extended)]
    [TestCase(IF_MRD, false, true, false, Extended)]
    [TestCase(IF_MRD, false, true, true, Combined)]
    [TestCase(IF_CNS, true, false, false, 8)]
    [TestCase(IF_CNS, false, false, false, Full)]
    [TestCase(IF_CNS, false, true, false, Extended)]
    [TestCase(IF_SRD_CNS, false, true, false, Extended)]
    [TestCase(IF_MWR, false, false, true, Extended)]
    [TestCase(IF_MRD_CNS, false, true, true, Combined)]
    [TestCase(IF_AWR, false, false, true, Extended)]
    [TestCase(IF_ARD_CNS, false, true, false, Extended)]
    [TestCase(IF_ARD_CNS, false, true, true, Combined)]
    public static void OperandCategoryAndPayloadFlagsDetermineNativeSize(
        Emitter.insFormat format, bool small, bool largeConstant, bool largeDisplacement, int expected)
    {
        var descriptor = new FlagDescriptor();
        descriptor.idIns(format == IF_METHOD ? INS_call : INS_mov);
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

        Assert.That(Size(CreateEmitter(), descriptor), Is.EqualTo(expected));
    }

#if FEATURE_LOOP_ALIGN
    [Test]
    public static void AlignmentFormatUsesItsNativeDescriptorLayout()
    {
        var descriptor = new FlagDescriptor();
        descriptor.idIns(INS_align);
        descriptor.idInsFmt(IF_NONE);

        Assert.That(Size(CreateEmitter(), descriptor), Is.EqualTo(
#if TARGET_X86
#if DEBUG
            28
#else
            24
#endif
#elif DEBUG
            48
#else
            40
#endif
            ));
    }
#endif

#if TARGET_AMD64
    [Test]
    public static void FatCallDescriptorTakesPrecedenceOverConstantAndDisplacementTags()
    {
        var type = typeof(Emitter).GetNestedType("instrDescCGCA", BindingFlags.NonPublic)
            ?? throw new AssertionException("Missing call descriptor type.");
        var descriptor = (Emitter.instrDesc)RuntimeHelpers.GetUninitializedObject(type);
        descriptor.idIns(INS_call);
        descriptor.idInsFmt(IF_METHOD);
        descriptor.idSetIsCall();
        descriptor.idSetIsLargeCns();
        descriptor.idSetIsLargeDsp();

        Assert.That(Size(CreateEmitter(), descriptor), Is.EqualTo(72));
    }
#endif

#if TARGET_X86
    [TestCase(-32768, true)]
    [TestCase(-32767, false)]
    [TestCase(32767, false)]
    [TestCase(32768, true)]
    public static void X86PackedAddressDisplacementReservesItsMostNegativeValue(int displacement, bool large)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compCurBB = new BasicBlock(null, null);
        var emitter = new CodeGen(compiler).Emitter;
        emitter.emitBegCG(compiler, default);
        emitter.Init();
        emitter.emitBegFN(false
#if DEBUG
            , false
#endif
            );

        var descriptor = NewAddress(emitter, emitAttr.EA_4BYTE, displacement);
        Assert.That(descriptor.idIsLargeDsp(), Is.EqualTo(large));
        Assert.That(descriptor.NativeLogicalSize, Is.EqualTo(large ? Extended : Full));
        Assert.That(descriptor.StorageSize, Is.EqualTo((nuint)((large ? Extended : Full)
#if DEBUG
            + 4
#endif
            )));
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNewInstrAmd")]
    private static extern Emitter.instrDesc NewAddress(Emitter emitter, emitAttr size, nint displacement);
#endif

    private static Emitter CreateEmitter()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        return new CodeGen(compiler).Emitter;
    }

    private sealed class FlagDescriptor : Emitter.instrDesc
    {
        public override int NativeLogicalSize => throw new AssertionException("Sizing must inspect native descriptor flags.");
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitSizeOfInsDsc")]
    private static extern int Size(Emitter emitter, Emitter.instrDesc descriptor);
}
#endif
