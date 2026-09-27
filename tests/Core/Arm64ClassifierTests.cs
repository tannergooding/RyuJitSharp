// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoHFAElemType;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class Arm64ClassifierTests
{
    private static Compiler NewCompiler() => (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));

    [TestCase(CORINFO_HFA_ELEM_FLOAT, 4, 3, false)]
    [TestCase(CORINFO_HFA_ELEM_FLOAT, 4, 3, true)]
    [TestCase(CORINFO_HFA_ELEM_DOUBLE, 8, 4, false)]
    [TestCase(CORINFO_HFA_ELEM_DOUBLE, 8, 4, true)]
    [TestCase(CORINFO_HFA_ELEM_VECTOR64, 8, 2, false)]
    [TestCase(CORINFO_HFA_ELEM_VECTOR64, 8, 2, true)]
    [TestCase(CORINFO_HFA_ELEM_VECTOR128, 16, 4, false)]
    [TestCase(CORINFO_HFA_ELEM_VECTOR128, 16, 4, true)]
    public static void HomogeneousAggregatesUseCompleteFloatingBanksOrSpill(
        CorInfoHFAElemType kind, int elementSize, int count, bool exhaust)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getHFAType = &GetHfaType;
        vtable.Base.Base.getClassSize = &GetClassSize;
        HfaState state = new() {
            Info = new ICorJitInfo { lpVtbl = &vtable },
            Kind = kind,
            Size = elementSize * count,
        };
#if DEBUG
        using var tls = new JitTls(&state.Info);
#endif
        var previous = JitTls.Compiler;
        var compiler = NewCompiler();
        compiler.info.compCompHnd = &state.Info;
        JitTls.Compiler = compiler;

        try
        {
            var classifier = new Arm64Classifier(new ClassifierInfo());
            if (exhaust)
            {
                for (var i = 0; i < MAX_FLOAT_REG_ARG - 1; i++)
                {
                    _ = classifier.Classify(compiler, TYP_DOUBLE, null, WellKnownArg.None);
                }
            }

            var handle = (CORINFO_CLASS_STRUCT_*)1;
            var layout = new ClassLayout(handle, true, (uint)state.Size, TYP_STRUCT, "Hfa", "Hfa");
            var result = classifier.Classify(compiler, TYP_STRUCT, layout, WellKnownArg.None);
            Assert.That(result.IsPassedByReference, Is.False);
            Assert.That(compiler.GetHfaCount(handle), Is.EqualTo((uint)count));
            Assert.That(compiler.compFloatingPointUsed, Is.True);

            if (exhaust)
            {
                var roundedSize = (state.Size + 7) & ~7;
                var packedSize = compAppleArm64Abi() && elementSize == 4 ? state.Size : roundedSize;
                Assert.That(result.NumSegments, Is.EqualTo(1));
                Assert.That(result.Segments[0].StackOffset, Is.Zero);
                Assert.That(result.Segments[0].Size, Is.EqualTo(state.Size));
                Assert.That(result.Segments[0].StackSize, Is.EqualTo(packedSize));
                Assert.That(classifier.StackSize, Is.EqualTo(roundedSize));

                var next = classifier.Classify(compiler, TYP_FLOAT, null, WellKnownArg.None);
                Assert.That(next.Segments[0].IsPassedOnStack, Is.True);
                Assert.That(next.Segments[0].StackOffset, Is.EqualTo(packedSize));
            }
            else
            {
                Assert.That(result.NumSegments, Is.EqualTo(count));
                Assert.That(classifier.StackSize, Is.Zero);

                for (var i = 0; i < count; i++)
                {
                    Assert.That(result.Segments[i].Register, Is.EqualTo(FltArgRegs[i]));
                    Assert.That(result.Segments[i].Offset, Is.EqualTo(i * elementSize));
                    Assert.That(result.Segments[i].Size, Is.EqualTo(elementSize));
                }
            }
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [Test]
    public static void SpecialParametersUseFixedRegistersWithoutConsumingArgumentRegisters()
    {
        var compiler = NewCompiler();
        var classifier = new Arm64Classifier(new ClassifierInfo {
            CallConv = CorInfoCallConvExtension.Managed,
        });

        var secret = classifier.Classify(compiler, TYP_I_IMPL, null, WellKnownArg.SecretStubParam);
        Assert.That(secret.Segments[0].Register, Is.EqualTo(REG_R12));

        var retBuffer = classifier.Classify(compiler, TYP_I_IMPL, null, WellKnownArg.RetBuffer);
        Assert.That(retBuffer.Segments[0].Register,
            Is.EqualTo(hasFixedRetBuffReg(CorInfoCallConvExtension.Managed) ? REG_R8 : REG_R0));

        var firstOrdinary = classifier.Classify(compiler, TYP_INT, null, WellKnownArg.None);
        Assert.That(firstOrdinary.Segments[0].Register,
            Is.EqualTo(hasFixedRetBuffReg(CorInfoCallConvExtension.Managed) ? REG_R0 : REG_R1));
        Assert.That(classifier.StackSize, Is.Zero);
    }

#if TARGET_WINDOWS
    [Test]
    public static void InstanceReturnBufferUsesOrdinaryRegisterOnWindows()
    {
        var compiler = NewCompiler();
        var classifier = new Arm64Classifier(new ClassifierInfo {
            CallConv = CorInfoCallConvExtension.CMemberFunction,
        });

        var retBuffer = classifier.Classify(compiler, TYP_I_IMPL, null, WellKnownArg.RetBuffer);
        Assert.That(retBuffer.Segments[0].Register, Is.EqualTo(REG_R0));
        Assert.That(classifier.Classify(compiler, TYP_INT, null, WellKnownArg.None).Segments[0].Register,
            Is.EqualTo(REG_R1));
    }
#endif

    [Test]
    public static void StructsUseTwoRegistersOrOneImplicitByReferencePointer()
    {
        var compiler = NewCompiler();
        var classifier = new Arm64Classifier(new ClassifierInfo());

        var pair = classifier.Classify(compiler, TYP_STRUCT, new ClassLayout(13), WellKnownArg.None);
        Assert.That(pair.NumSegments, Is.EqualTo(2));
        Assert.That(pair.Segments[0].Register, Is.EqualTo(REG_R0));
        Assert.That(pair.Segments[0].Size, Is.EqualTo(8));
        Assert.That(pair.Segments[1].Register, Is.EqualTo(REG_R1));
        Assert.That(pair.Segments[1].Offset, Is.EqualTo(8));
        Assert.That(pair.Segments[1].Size, Is.EqualTo(5));

        var large = classifier.Classify(compiler, TYP_STRUCT, new ClassLayout(24), WellKnownArg.None);
        Assert.That(large.IsPassedByReference, Is.True);
        Assert.That(large.Segments[0].Register, Is.EqualTo(REG_R2));
        Assert.That(large.Segments[0].Size, Is.EqualTo(8));

        var simd = classifier.Classify(compiler, TYP_SIMD, new ClassLayout(16), WellKnownArg.None);
        Assert.That(simd.IsPassedByReference, Is.True);
        Assert.That(simd.Segments[0].Register, Is.EqualTo(REG_R3));
    }

    [Test]
    public static void ImplicitByReferenceAggregateUsesOneStackSlotAfterRegisterExhaustion()
    {
        var compiler = NewCompiler();
        var classifier = new Arm64Classifier(new ClassifierInfo());

        for (var i = 0; i < MAX_REG_ARG; i++)
        {
            _ = classifier.Classify(compiler, TYP_INT, null, WellKnownArg.None);
        }

        var large = classifier.Classify(compiler, TYP_STRUCT, new ClassLayout(24), WellKnownArg.None);
        Assert.That(large.IsPassedByReference, Is.True);
        Assert.That(large.Segments[0].StackOffset, Is.Zero);
        Assert.That(large.Segments[0].Size, Is.EqualTo(TARGET_POINTER_SIZE));
        Assert.That(classifier.StackSize, Is.EqualTo(TARGET_POINTER_SIZE));
    }

    [Test]
    public static void ExhaustingIntegerRegistersDoesNotExhaustFloatingRegisters()
    {
        var compiler = NewCompiler();
        var classifier = new Arm64Classifier(new ClassifierInfo());

        for (var i = 0; i < MAX_REG_ARG - 1; i++)
        {
            Assert.That(classifier.Classify(compiler, TYP_INT, null, WellKnownArg.None).Segments[0].Register,
                Is.EqualTo(IntArgRegs[i]));
        }

        var pair = classifier.Classify(compiler, TYP_STRUCT, new ClassLayout(16), WellKnownArg.None);
        Assert.That(pair.Segments[0].StackOffset, Is.Zero);
        Assert.That(pair.Segments[0].Size, Is.EqualTo(16));
        Assert.That(classifier.Classify(compiler, TYP_INT, null, WellKnownArg.None).Segments[0].StackOffset,
            Is.EqualTo(16));
        Assert.That(classifier.Classify(compiler, TYP_DOUBLE, null, WellKnownArg.None).Segments[0].Register,
            Is.EqualTo(REG_V0));
        Assert.That(classifier.StackSize, Is.EqualTo(24));
    }

    [Test]
    public static void FloatingPointExhaustionLeavesIntegerRegistersAvailable()
    {
        var compiler = NewCompiler();
        var classifier = new Arm64Classifier(new ClassifierInfo());

        for (var i = 0; i < MAX_FLOAT_REG_ARG; i++)
        {
            Assert.That(classifier.Classify(compiler, TYP_DOUBLE, null, WellKnownArg.None).Segments[0].Register,
                Is.EqualTo(FltArgRegs[i]));
        }

        var overflow = classifier.Classify(compiler, TYP_FLOAT, null, WellKnownArg.None);
        Assert.That(overflow.Segments[0].StackOffset, Is.Zero);
        Assert.That(overflow.Segments[0].Size, Is.EqualTo(4));
        Assert.That(overflow.Segments[0].StackSize, Is.EqualTo(compAppleArm64Abi() ? 4 : 8));
        Assert.That(classifier.Classify(compiler, TYP_INT, null, WellKnownArg.None).Segments[0].Register,
            Is.EqualTo(REG_R0));
        Assert.That(classifier.StackSize, Is.EqualTo(8));
    }

#if TARGET_WINDOWS
    [Test]
    public static void VarArgsPairSplitsAtTheLastIntegerRegister()
    {
        var compiler = NewCompiler();
        var classifier = new Arm64Classifier(new ClassifierInfo { IsVarArgs = true });

        for (var i = 0; i < MAX_REG_ARG - 1; i++)
        {
            _ = classifier.Classify(compiler, TYP_INT, null, WellKnownArg.None);
        }

        var pair = classifier.Classify(compiler, TYP_STRUCT, new ClassLayout(13), WellKnownArg.None);
        Assert.That(pair.IsSplitAcrossRegistersAndStack, Is.True);
        Assert.That(pair.Segments[0].Register, Is.EqualTo(REG_R7));
        Assert.That(pair.Segments[1].StackOffset, Is.Zero);
        Assert.That(pair.Segments[1].Offset, Is.EqualTo(8));
        Assert.That(pair.Segments[1].Size, Is.EqualTo(5));
        Assert.That(classifier.Classify(compiler, TYP_FLOAT, null, WellKnownArg.None).Segments[0].StackOffset,
            Is.EqualTo(8));
        Assert.That(classifier.StackSize, Is.EqualTo(16));
    }

    [Test]
    public static void VarArgsFloatingPointValueUsesIntegerRegister()
    {
        var compiler = NewCompiler();
        var classifier = new Arm64Classifier(new ClassifierInfo { IsVarArgs = true });
        var value = classifier.Classify(compiler, TYP_DOUBLE, null, WellKnownArg.None);

        Assert.That(value.Segments[0].Register, Is.EqualTo(REG_R0));
        Assert.That(classifier.Classify(compiler, TYP_DOUBLE, null, WellKnownArg.None).Segments[0].Register,
            Is.EqualTo(REG_R1));
    }
#endif

#if TARGET_APPLE
    [Test]
    public static void SmallStackPrimitivesPackAtTheirNaturalAlignment()
    {
        var compiler = NewCompiler();
        var classifier = new Arm64Classifier(new ClassifierInfo());

        for (var i = 0; i < MAX_REG_ARG; i++)
        {
            _ = classifier.Classify(compiler, TYP_INT, null, WellKnownArg.None);
        }

        var byteArg = classifier.Classify(compiler, TYP_BYTE, null, WellKnownArg.None);
        var shortArg = classifier.Classify(compiler, TYP_SHORT, null, WellKnownArg.None);
        var intArg = classifier.Classify(compiler, TYP_INT, null, WellKnownArg.None);
        Assert.That(byteArg.Segments[0].StackOffset, Is.Zero);
        Assert.That(byteArg.Segments[0].StackSize, Is.EqualTo(1));
        Assert.That(shortArg.Segments[0].StackOffset, Is.EqualTo(2));
        Assert.That(shortArg.Segments[0].StackSize, Is.EqualTo(2));
        Assert.That(intArg.Segments[0].StackOffset, Is.EqualTo(4));
        Assert.That(intArg.Segments[0].StackSize, Is.EqualTo(4));
        Assert.That(classifier.StackSize, Is.EqualTo(8));
    }
#endif

    private struct HfaState
    {
        public ICorJitInfo Info;
        public CorInfoHFAElemType Kind;
        public int Size;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoHFAElemType GetHfaType(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* cls)
        => ((HfaState*)self)->Kind;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetClassSize(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* cls)
        => ((HfaState*)self)->Size;
}
#endif
