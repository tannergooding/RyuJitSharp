// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.CorInfoGCType;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

#if TARGET_RISCV64
using PlatformClassifier = RyuJitSharp.RiscV64Classifier;
#elif TARGET_LOONGARCH64
using PlatformClassifier = RyuJitSharp.LoongArch64Classifier;
#endif

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class AbiHeaderTests
{
#if DEBUG
    private static JitTls? _tls;
#else
    private static Compiler? _previousCompiler;
#endif

    [SetUp]
    public static void SetUp()
    {
#if DEBUG
        _tls = new JitTls(null);
#else
        _previousCompiler = JitTls.Compiler;
#endif
    }

    [TearDown]
    public static void TearDown()
    {
#if DEBUG
        assert(_tls is not null);
        _tls.Dispose();
        _tls = null;
#else
        JitTls.Compiler = _previousCompiler;
        _previousCompiler = null;
#endif
    }

    [Test]
    public static void DefaultInformationHasNoSegments()
    {
        AbiPassingInformation info = default;

        Assert.That(info.NumSegments, Is.Zero);
        Assert.That(info.Segments.Length, Is.Zero);
        Assert.That(info.HasAnyRegisterSegment, Is.False);
        Assert.That(info.HasAnyFloatingRegisterSegment, Is.False);
        Assert.That(info.HasAnyStackSegment, Is.False);
        Assert.That(info.HasExactlyOneRegisterSegment, Is.False);
        Assert.That(info.HasExactlyOneStackSegment, Is.False);
        Assert.That(info.IsSplitAcrossRegistersAndStack, Is.False);
        Assert.That(info.CountRegsAndStackSlots(), Is.Zero);
        Assert.That(info.StackBytesConsumed(), Is.Zero);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(4)]
    public static void ExplicitInformationHasTheRequestedNumberOfSegments(int count)
    {
        var info = new AbiPassingInformation(count);

        Assert.That(info.NumSegments, Is.EqualTo(count));
        Assert.That(info.Segments.Length, Is.EqualTo(count));

        for (var i = 0; i < count; i++)
        {
            info.Segments[i] = AbiPassingSegment.OnStack(i * TARGET_POINTER_SIZE, i, 1);
        }

        Assert.That(info.CountRegsAndStackSlots(), Is.EqualTo(count));
        Assert.That(info.StackBytesConsumed(), Is.EqualTo(count * TARGET_POINTER_SIZE));
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public static void FloatingSegmentQueryChecksTheRegisterBank(bool floating, bool includeStack)
    {
        var segment = AbiPassingSegment.InRegister(
            floating ? FloatRegister(0) : IntRegister(0), 0, 4);
        var info = includeStack
            ? AbiPassingInformation.FromSegments(NewCompiler(), segment, AbiPassingSegment.OnStack(0, 4, 3))
            : AbiPassingInformation.FromSegmentByValue(NewCompiler(), segment);

        Assert.That(info.HasAnyFloatingRegisterSegment, Is.EqualTo(floating));
        Assert.That(info.HasAnyStackSegment, Is.EqualTo(includeStack));
        Assert.That(info.HasAnyRegisterSegment, Is.True);
    }

    [Test]
    public static void StackConsumptionSumsOnlyStackSegmentsIncludingPackedTails()
    {
        var info = new AbiPassingInformation(4);
        info.Segments[0] = AbiPassingSegment.InRegister(IntRegister(0), 0, TARGET_POINTER_SIZE);
        info.Segments[1] = AbiPassingSegment.OnStack(0, TARGET_POINTER_SIZE, TARGET_POINTER_SIZE + 1);
        info.Segments[2] = AbiPassingSegment.InRegister(FloatRegister(0), TARGET_POINTER_SIZE + 8, 4);
        info.Segments[3] = AbiPassingSegment.OnStackWithoutConsumingFullSlot(64, TARGET_POINTER_SIZE + 12, 3);

        Assert.That(info.StackBytesConsumed(), Is.EqualTo((2 * TARGET_POINTER_SIZE) + 3));
        Assert.That(info.CountRegsAndStackSlots(), Is.EqualTo(5));
        Assert.That(info.IsSplitAcrossRegistersAndStack, Is.True);
    }

    [Test]
    public static void RegisterQueuePreservesOrderAndClearExhaustsIt()
    {
        var queue = new RegisterQueue([IntRegister(0), IntRegister(1)]);

        Assert.That(queue.Count, Is.EqualTo(2));
        Assert.That(queue.Peek(), Is.EqualTo(IntRegister(0)));
        Assert.That(queue.Count, Is.EqualTo(2));
        Assert.That(queue.Dequeue(), Is.EqualTo(IntRegister(0)));
        Assert.That(queue.Peek(), Is.EqualTo(IntRegister(1)));
        Assert.That(queue.Count, Is.EqualTo(1));

        queue.Clear();
        Assert.That(queue.Count, Is.Zero);
        queue.Clear();
        Assert.That(queue.Count, Is.Zero);
    }

#if TARGET_X86
    [TestCase(CorInfoCallConvExtension.Managed, false, false, false, 2)]
    [TestCase(CorInfoCallConvExtension.Managed, true, false, false, 0)]
    [TestCase(CorInfoCallConvExtension.Managed, true, true, false, 1)]
    [TestCase(CorInfoCallConvExtension.Managed, true, false, true, 1)]
    [TestCase(CorInfoCallConvExtension.Managed, true, true, true, 1)]
    [TestCase(CorInfoCallConvExtension.Thiscall, false, true, false, 1)]
    [TestCase(CorInfoCallConvExtension.C, false, false, false, 0)]
    [TestCase(CorInfoCallConvExtension.Stdcall, false, false, false, 0)]
    [TestCase(CorInfoCallConvExtension.CMemberFunction, false, true, false, 0)]
    [TestCase(CorInfoCallConvExtension.StdcallMemberFunction, false, true, false, 0)]
    public static void X86ConventionsSelectTheNativeRegisterCount(
        CorInfoCallConvExtension callConv, bool varArgs, bool hasThis, bool hasRetBuff, int registerCount)
    {
        var compiler = NewCompiler();
        var info = new ClassifierInfo {
            CallConv = callConv,
            IsVarArgs = varArgs,
            HasThis = hasThis,
            HasRetBuff = hasRetBuff,
        };
        var classifier = new X86Classifier(info);

        for (var i = 0; i < 3; i++)
        {
            var result = classifier.Classify(compiler, TYP_INT, null, WellKnownArg.None);
            Assert.That(result.HasExactlyOneRegisterSegment, Is.EqualTo(i < registerCount));

            if (i >= registerCount)
            {
                var slot = i - registerCount;
                var offset = callConv is CorInfoCallConvExtension.Managed ? (slot + 1) * 4 : slot * 4;
                Assert.That(result.Segments[0].StackOffset, Is.EqualTo(offset));
            }
        }

        Assert.That(classifier.StackSize, Is.EqualTo((3 - registerCount) * 4));
    }

    [Test]
    public static void X86TailCallAndFloatingArgumentsDoNotConsumeIntegerRegisters()
    {
        var compiler = NewCompiler();
        var classifier = new X86Classifier(new ClassifierInfo());
        var tailCallArg = classifier.Classify(compiler, TYP_INT, null, WellKnownArg.X86TailCallSpecialArg);
        var floatingArg = classifier.Classify(compiler, TYP_DOUBLE, null, WellKnownArg.None);
        var ordinaryArg = classifier.Classify(compiler, TYP_INT, null, WellKnownArg.None);

        Assert.That(tailCallArg.Segments[0].StackOffset, Is.EqualTo(4));
        Assert.That(floatingArg.Segments[0].StackOffset, Is.EqualTo(12));
        Assert.That(ordinaryArg.Segments[0].Register, Is.EqualTo(REG_ECX));
        Assert.That(classifier.StackSize, Is.EqualTo(12));
    }

    [Test]
    public static void X86StructDependencyTerminatesInsteadOfGuessingClassification()
    {
        var compiler = NewCompiler();
        var layout = new ClassLayout((CORINFO_CLASS_STRUCT_*)1, true, 4, TYP_STRUCT, "Struct", "Struct");
        FatalJitException? error = null;
        var classifier = new X86Classifier(new ClassifierInfo());

        try
        {
            _ = classifier.Classify(compiler, TYP_STRUCT, layout, WellKnownArg.None);
        }
        catch (FatalJitException exception)
        {
            error = exception;
        }

        Assert.That(error, Is.Not.Null);
    }
#endif

#if TARGET_ARM
#if CONFIGURABLE_ARM_ABI || !ARM_SOFTFP
    [TestCase(CorInfoHFAElemType.CORINFO_HFA_ELEM_FLOAT, 4, 3, 13, false)]
    [TestCase(CorInfoHFAElemType.CORINFO_HFA_ELEM_FLOAT, 4, 3, 14, true)]
    [TestCase(CorInfoHFAElemType.CORINFO_HFA_ELEM_DOUBLE, 8, 2, 12, false)]
    [TestCase(CorInfoHFAElemType.CORINFO_HFA_ELEM_DOUBLE, 8, 2, 13, true)]
    public static void Arm32HfasRequireConsecutiveRegistersAndSpillAsAWhole(
        CorInfoHFAElemType kind, int elementSize, int count, int preceding, bool spill)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getHFAType = &GetArmHfaType;
        ArmHfaState state = new() {
            Runtime = new ICorJitInfo { lpVtbl = &vtable },
            Kind = kind,
        };
        var compiler = NewCompiler();
        compiler.info.compCompHnd = &state.Runtime;
#if CONFIGURABLE_ARM_ABI
        var oldFeatureHfa = GlobalJitOptions.compFeatureHfa;
        GlobalJitOptions.compFeatureHfa = true;
        compiler.opts.compUseSoftFP = false;
#endif
        try
        {
            var classifier = new Arm32Classifier(new ClassifierInfo());

            for (var i = 0; i < preceding; i++)
            {
                _ = classifier.Classify(compiler, TYP_FLOAT, null, WellKnownArg.None);
            }

            var layout = new ClassLayout(
                (CORINFO_CLASS_STRUCT_*)1, true, (uint)(elementSize * count), TYP_STRUCT, "Hfa", "Hfa");
            var result = classifier.Classify(compiler, TYP_STRUCT, layout, WellKnownArg.None);

            Assert.That(result.NumSegments, Is.EqualTo(spill ? 1 : count));
            Assert.That(result.HasExactlyOneStackSegment, Is.EqualTo(spill));
            Assert.That(result.StackBytesConsumed(), Is.EqualTo(spill ? elementSize * count : 0));

            if (spill)
            {
                var next = classifier.Classify(compiler, TYP_FLOAT, null, WellKnownArg.None);
                Assert.That(next.Segments[0].StackOffset, Is.EqualTo(elementSize * count));
            }
            else
            {
                for (var i = 0; i < count; i++)
                {
                    Assert.That(result.Segments[i].Offset, Is.EqualTo(i * elementSize));
                    Assert.That(result.Segments[i].Size, Is.EqualTo(elementSize));
                }
            }
        }
        finally
        {
#if CONFIGURABLE_ARM_ABI
            GlobalJitOptions.compFeatureHfa = oldFeatureHfa;
#endif
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoHFAElemType GetArmHfaType(ICorJitInfo* runtime, CORINFO_CLASS_STRUCT_* handle)
    {
        return ((ArmHfaState*)runtime)->Kind;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ArmHfaState
    {
        public ICorJitInfo Runtime;
        public CorInfoHFAElemType Kind;
    }

    [Test]
    public static void Arm32FloatingAllocationBackfillsTheHoleSkippedByADouble()
    {
        var compiler = NewCompiler();
#if CONFIGURABLE_ARM_ABI
        compiler.opts.compUseSoftFP = false;
#endif
        var classifier = new Arm32Classifier(new ClassifierInfo());
        var first = classifier.Classify(compiler, TYP_FLOAT, null, WellKnownArg.None);
        var second = classifier.Classify(compiler, TYP_DOUBLE, null, WellKnownArg.None);
        var third = classifier.Classify(compiler, TYP_FLOAT, null, WellKnownArg.None);

        Assert.That(first.Segments[0].Register, Is.EqualTo(REG_F0));
        Assert.That(second.Segments[0].Register, Is.EqualTo(REG_F2));
        Assert.That(second.Segments[0].RegisterMask,
            Is.EqualTo((regMask)((1u << 2) | (1u << 3))));
        Assert.That(third.Segments[0].Register, Is.EqualTo(REG_F1));
        Assert.That(classifier.StackSize, Is.Zero);
    }
#endif

#if CONFIGURABLE_ARM_ABI || ARM_SOFTFP
    [Test]
    public static void Arm32DoubleAlignmentSkipsTheOddIntegerRegister()
    {
        var compiler = NewCompiler();
#if CONFIGURABLE_ARM_ABI
        compiler.opts.compUseSoftFP = true;
#endif
        var classifier = new Arm32Classifier(new ClassifierInfo());
        _ = classifier.Classify(compiler, TYP_INT, null, WellKnownArg.None);
        var aligned = classifier.Classify(compiler, TYP_DOUBLE, null, WellKnownArg.None);
        var spilled = classifier.Classify(compiler, TYP_INT, null, WellKnownArg.None);

        Assert.That(aligned.NumSegments, Is.EqualTo(2));
        Assert.That(aligned.Segments[0].Register, Is.EqualTo(REG_R2));
        Assert.That(aligned.Segments[1].Register, Is.EqualTo(REG_R3));
        Assert.That(spilled.Segments[0].StackOffset, Is.Zero);
        Assert.That(classifier.StackSize, Is.EqualTo(4));
    }

    [TestCase(0, 12, 3, 0)]
    [TestCase(3, 12, 1, 8)]
    [TestCase(4, 12, 0, 12)]
    public static void Arm32SoftFpStructsSplitInOrder(
        int preceding, int size, int registerCount, int stackSize)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getClassAlignmentRequirement = &FourByteAlignment;
        ICorJitInfo runtime = new() { lpVtbl = &vtable };
        var compiler = NewCompiler();
        compiler.info.compCompHnd = &runtime;
#if CONFIGURABLE_ARM_ABI
        compiler.opts.compUseSoftFP = true;
#endif
        var classifier = new Arm32Classifier(new ClassifierInfo());

        for (var i = 0; i < preceding; i++)
        {
            _ = classifier.Classify(compiler, TYP_INT, null, WellKnownArg.None);
        }

        var layout = new ClassLayout((CORINFO_CLASS_STRUCT_*)1, true, (uint)size, TYP_STRUCT, "Struct", "Struct");
        var result = classifier.Classify(compiler, TYP_STRUCT, layout, WellKnownArg.None);

        Assert.That(result.NumSegments, Is.EqualTo(registerCount + (stackSize > 0 ? 1 : 0)));
        Assert.That(result.IsSplitAcrossRegistersAndStack, Is.EqualTo((registerCount > 0) && (stackSize > 0)));
        Assert.That(classifier.StackSize, Is.EqualTo(stackSize));

        for (var i = 0; i < registerCount; i++)
        {
            Assert.That(result.Segments[i].Register, Is.EqualTo((regNumber)((int)REG_R0 + preceding + i)));
            Assert.That(result.Segments[i].Offset, Is.EqualTo(i * 4));
        }

        if (stackSize > 0)
        {
            Assert.That(result.Segments[registerCount].Offset, Is.EqualTo(registerCount * 4));
            Assert.That(result.Segments[registerCount].Size, Is.EqualTo(stackSize));
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static uint FourByteAlignment(ICorJitInfo* runtime, CORINFO_CLASS_HANDLE handle, bool hint) => 4;
#endif
#endif

#if TARGET_RISCV64 || TARGET_LOONGARCH64
    [Test]
    public static void HardwareFloatingAbiFallsBackToIntegerRegistersThenStack()
    {
        var compiler = NewCompiler();
        var classifier = new PlatformClassifier(new ClassifierInfo());

        for (var i = 0; i < FltArgRegs.Length; i++)
        {
            var floating = classifier.Classify(compiler, TYP_FLOAT, null, WellKnownArg.None);
            Assert.That(floating.Segments[0].Register, Is.EqualTo(FltArgRegs[i]));
        }

        for (var i = 0; i < IntArgRegs.Length; i++)
        {
            var integer = classifier.Classify(compiler, TYP_FLOAT, null, WellKnownArg.None);
            Assert.That(integer.Segments[0].Register, Is.EqualTo(IntArgRegs[i]));
            Assert.That(integer.Segments[0].Size, Is.EqualTo(4));
        }

        var spilled = classifier.Classify(compiler, TYP_FLOAT, null, WellKnownArg.None);
        Assert.That(spilled.Segments[0].StackOffset, Is.Zero);
        Assert.That(spilled.StackBytesConsumed(), Is.EqualTo(8));
        Assert.That(classifier.StackSize, Is.EqualTo(8));
    }

    [Test]
    public static void OversizedStructsConsumeOneImplicitReferenceSlot()
    {
        var compiler = NewCompiler();
        var classifier = new PlatformClassifier(new ClassifierInfo());
        var result = classifier.Classify(compiler, TYP_STRUCT, new ClassLayout(24), WellKnownArg.None);

        Assert.That(result.IsPassedByReference, Is.True);
        Assert.That(result.Segments[0].Register, Is.EqualTo(IntArgRegs[0]));
        Assert.That(result.Segments[0].Size, Is.EqualTo(8));
    }

    [Test]
    public static void FloatingStructLoweringPreservesClassifierAndReturnMetadata()
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getClassSize = &GetFpStructClassSize;
        vtable.Base.Base.getClassGClayout = &GetFpStructGcLayout;
        vtable.Base.Base.getFpStructLowering = &GetFpStructLowering;
        FpStructLoweringState state = new() {
            Runtime = new ICorJitInfo { lpVtbl = &vtable },
            StructSize = 16,
            GcType1 = TYPE_GC_REF,
        };
        var compiler = NewCompiler();
        compiler.info.compCompHnd = &state.Runtime;
        var handle = (CORINFO_CLASS_STRUCT_*)1;
        var layout = new ClassLayout(handle, false, 16, TYP_STRUCT, "Struct", "Struct");
        var classifier = new PlatformClassifier(new ClassifierInfo());
        var first = classifier.Classify(compiler, TYP_STRUCT, layout, WellKnownArg.None);
        var second = classifier.Classify(compiler, TYP_STRUCT, layout, WellKnownArg.None);

        Assert.That(first.NumSegments, Is.EqualTo(2));
        Assert.That(first.Segments[0].Register, Is.EqualTo(FltArgRegs[0]));
        Assert.That(first.Segments[0].Offset, Is.Zero);
        Assert.That(first.Segments[0].Size, Is.EqualTo(4));
        Assert.That(first.Segments[1].Register, Is.EqualTo(IntArgRegs[0]));
        Assert.That(first.Segments[1].Offset, Is.EqualTo(8));
        Assert.That(first.Segments[1].Size, Is.EqualTo(8));

        var descriptor = new ReturnTypeDesc();
        descriptor.InitializeStructReturnType(compiler, handle, CorInfoCallConvExtension.Managed);
        Assert.That(descriptor.ReturnRegCount, Is.EqualTo(2));
        Assert.That(descriptor.GetReturnRegType(0), Is.EqualTo(TYP_FLOAT));
        Assert.That(descriptor.GetReturnFieldOffset(0), Is.Zero);
        Assert.That(descriptor.GetReturnRegType(1), Is.EqualTo(TYP_REF));
        Assert.That(descriptor.GetReturnFieldOffset(1), Is.EqualTo(8));
        Assert.That(compiler.compFloatingPointUsed, Is.True);
        Assert.That(second.NumSegments, Is.EqualTo(2));
        Assert.That(state.CallCount, Is.EqualTo(1));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetFpStructClassSize(ICorJitInfo* runtime, CORINFO_CLASS_STRUCT_* handle)
        => ((FpStructLoweringState*)runtime)->StructSize;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetFpStructGcLayout(ICorJitInfo* runtime, CORINFO_CLASS_STRUCT_* handle, CorInfoGCType* layout)
    {
        var state = (FpStructLoweringState*)runtime;
        layout[0] = state->GcType0;
        layout[1] = state->GcType1;
        return (state->GcType0 is TYPE_GC_NONE ? 0 : 1) + (state->GcType1 is TYPE_GC_NONE ? 0 : 1);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetFpStructLowering(ICorJitInfo* runtime, CORINFO_CLASS_STRUCT_* handle, CORINFO_FPSTRUCT_LOWERING* lowering)
    {
        var state = (FpStructLoweringState*)runtime;
        state->CallCount++;
        *lowering = default;
        lowering->numLoweredElements = 2;
        lowering->loweredElements[0] = CorInfoType.CORINFO_TYPE_FLOAT;
        lowering->loweredElements[1] = CorInfoType.CORINFO_TYPE_LONG;
        lowering->offsets[1] = 8;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FpStructLoweringState
    {
        public ICorJitInfo Runtime;
        public int CallCount;
        public int StructSize;
        public CorInfoGCType GcType0;
        public CorInfoGCType GcType1;
    }
#endif

#if TARGET_RISCV64
    [TestCase(6, false)]
    [TestCase(7, true)]
    [TestCase(8, false)]
    public static void RiscV64BlockStructsUseTheRemainingRegistersAndSplitTheirTail(int preceding, bool split)
    {
        var compiler = NewCompiler();
        var classifier = new RiscV64Classifier(new ClassifierInfo());

        for (var i = 0; i < preceding; i++)
        {
            _ = classifier.Classify(compiler, TYP_INT, null, WellKnownArg.None);
        }

        var result = classifier.Classify(compiler, TYP_STRUCT, new ClassLayout(12), WellKnownArg.None);
        Assert.That(result.IsSplitAcrossRegistersAndStack, Is.EqualTo(split));
        Assert.That(result.StackBytesConsumed(), Is.EqualTo(preceding == 6 ? 0 : preceding == 7 ? 8 : 16));

        if (split)
        {
            Assert.That(result.Segments[0].Register, Is.EqualTo(IntArgRegs[7]));
            Assert.That(result.Segments[1].Offset, Is.EqualTo(8));
            Assert.That(result.Segments[1].Size, Is.EqualTo(4));
        }
    }
#endif

#if TARGET_WASM
    [TestCase(TYP_LONG, 4, TYP_INT)]
    [TestCase(TYP_DOUBLE, 4, TYP_FLOAT)]
    [TestCase(TYP_SIMD16, 12, TYP_SIMD16)]
    public static void WasmSegmentTypesFollowNativeSizeAndRegisterBank(
        var_types registerType, int segmentSize, var_types expectedType)
    {
        var register = regNumberExtensions.MakeWasmReg(0, registerType);
        var segment = AbiPassingSegment.InRegister(register, 0, segmentSize);

        Assert.That(segment.GetRegisterType(), Is.EqualTo(expectedType));
    }
#endif

#if SWIFT_SUPPORT
    [Test]
    public static void SwiftParameterDispatcherClassifiesSpecialAndOrdinaryArguments()
    {
        var compiler = NewCompiler();
        compiler.info.compCallConv = CorInfoCallConvExtension.Swift;
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
        compiler.info.compThisArg = BAD_VAR_NUM;
        compiler.info.compArgsCount = 4;
        compiler.lvaSecretStubArg = BAD_VAR_NUM;
        compiler.lvaSwiftSelfArg = 0;
        compiler.lvaSwiftIndirectResultArg = BAD_VAR_NUM;
        compiler.lvaSwiftErrorArg = 1;
        compiler.lvaCount = 4;
        compiler.lvaTable = [
            new LclVarDsc { Type = TYP_I_IMPL, lvIsParam = true },
            new LclVarDsc { Type = TYP_I_IMPL, lvIsParam = true },
            new LclVarDsc { Type = TYP_INT, lvIsParam = true },
            new LclVarDsc { Type = TYP_FLOAT, lvIsParam = true },
        ];

        compiler.lvaClassifyParameterAbi();

        ref readonly var self = ref compiler.lvaGetParameterAbiInfo(0);
        ref readonly var error = ref compiler.lvaGetParameterAbiInfo(1);
        ref readonly var ordinary = ref compiler.lvaGetParameterAbiInfo(2);
        ref readonly var floating = ref compiler.lvaGetParameterAbiInfo(3);
        Assert.That(self.Segments[0].Register, Is.EqualTo(REG_SWIFT_SELF));
        Assert.That(error.Segments[0].Register, Is.EqualTo(REG_SWIFT_ERROR));
        Assert.That(ordinary.Segments[0].Register, Is.EqualTo(IntArgRegs[0]));
        Assert.That(floating.Segments[0].Register, Is.EqualTo(FltArgRegs[0]));

        for (var i = 0; i < 4; i++)
        {
            Assert.That(compiler.lvaGetDesc(i).lvIsRegArg, Is.True);
            Assert.That(compiler.lvaGetDesc(i).lvIsMultiRegArg, Is.False);
        }
    }

    [TestCase(WellKnownArg.RetBuffer)]
    [TestCase(WellKnownArg.SwiftSelf)]
    [TestCase(WellKnownArg.SwiftError)]
    public static void SwiftSpecialArgumentsDoNotConsumeTheOrdinaryQueues(WellKnownArg argument)
    {
        var compiler = NewCompiler();
        var info = new ClassifierInfo { CallConv = CorInfoCallConvExtension.Swift };
        var classifier = new SwiftABIClassifier(info);
        var special = classifier.Classify(compiler, TYP_I_IMPL, null, argument);
        var ordinary = classifier.Classify(compiler, TYP_INT, null, WellKnownArg.None);
        var expected = argument switch {
            WellKnownArg.RetBuffer => theFixedRetBuffReg(CorInfoCallConvExtension.Swift),
            WellKnownArg.SwiftSelf => REG_SWIFT_SELF,
            _ => REG_SWIFT_ERROR,
        };

        Assert.That(special.Segments[0].Register, Is.EqualTo(expected));
        Assert.That(ordinary.Segments[0].Register, Is.EqualTo(IntArgRegs[0]));
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    public static void SwiftStructLoweringPreservesOffsetsTailSizesAndReferencePassing(
        bool byReference, bool spill)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getSwiftLowering = &GetSwiftLowering;
        LoweringState state = new() {
            Runtime = new ICorJitInfo { lpVtbl = &vtable },
            ByReference = byReference,
        };
        var compiler = NewCompiler();
        compiler.info.compCompHnd = &state.Runtime;
        var info = new ClassifierInfo { CallConv = CorInfoCallConvExtension.Swift };
        var classifier = new SwiftABIClassifier(info);

        if (spill)
        {
            for (var i = 0; i < IntArgRegs.Length - 1; i++)
            {
                _ = classifier.Classify(compiler, TYP_INT, null, WellKnownArg.None);
            }
        }

        var layout = new ClassLayout((CORINFO_CLASS_STRUCT_*)1, true, 11, TYP_STRUCT, "SwiftStruct", "SwiftStruct");
        var result = classifier.Classify(compiler, TYP_STRUCT, layout, WellKnownArg.None);
        Assert.That(result.IsPassedByReference, Is.EqualTo(byReference));

        if (byReference)
        {
            Assert.That(result.NumSegments, Is.EqualTo(1));
            Assert.That(result.Segments[0].Size, Is.EqualTo(TARGET_POINTER_SIZE));
        }
        else
        {
            Assert.That(result.NumSegments, Is.EqualTo(2));
            Assert.That(result.Segments[0].Offset, Is.Zero);
            Assert.That(result.Segments[0].Size, Is.EqualTo(4));
            Assert.That(result.Segments[1].Offset, Is.EqualTo(8));
            Assert.That(result.Segments[1].Size, Is.EqualTo(3));
            Assert.That(result.Segments[1].IsPassedOnStack, Is.EqualTo(spill));
        }
    }

    [Test]
    public static void EmptySwiftLoweringProducesNoSegmentsWithoutConsumingRegisters()
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getSwiftLowering = &GetEmptySwiftLowering;
        ICorJitInfo runtime = new() { lpVtbl = &vtable };
        var compiler = NewCompiler();
        compiler.info.compCompHnd = &runtime;
        var classifier = new SwiftABIClassifier(new ClassifierInfo { CallConv = CorInfoCallConvExtension.Swift });
        var layout = new ClassLayout((CORINFO_CLASS_STRUCT_*)1, true, 1, TYP_STRUCT, "Empty", "Empty");
        var empty = classifier.Classify(compiler, TYP_STRUCT, layout, WellKnownArg.None);
        var next = classifier.Classify(compiler, TYP_INT, null, WellKnownArg.None);

        Assert.That(empty.NumSegments, Is.Zero);
        Assert.That(empty.StackBytesConsumed(), Is.Zero);
        Assert.That(next.Segments[0].Register, Is.EqualTo(IntArgRegs[0]));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetSwiftLowering(ICorJitInfo* runtime, CORINFO_CLASS_STRUCT_* handle, CORINFO_SWIFT_LOWERING* lowering)
    {
        *lowering = default;
        lowering->byReference = ((LoweringState*)runtime)->ByReference;
        lowering->numLoweredElements = 2;
        lowering->loweredElements[0] = CorInfoType.CORINFO_TYPE_INT;
        lowering->loweredElements[1] = CorInfoType.CORINFO_TYPE_LONG;
        lowering->offsets[0] = 0;
        lowering->offsets[1] = 8;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetEmptySwiftLowering(ICorJitInfo* runtime, CORINFO_CLASS_STRUCT_* handle, CORINFO_SWIFT_LOWERING* lowering)
    {
        *lowering = default;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LoweringState
    {
        public ICorJitInfo Runtime;
        public bool ByReference;
    }
#endif

    private static Compiler NewCompiler()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitTls.Compiler = compiler;

        return compiler;
    }

    private static regNumber IntRegister(int index)
    {
#if TARGET_WASM
        return regNumberExtensions.MakeWasmReg((uint)index, TYP_INT);
#else
        return IntArgRegs[index];
#endif
    }

    private static regNumber FloatRegister(int index)
    {
#if TARGET_WASM
        return regNumberExtensions.MakeWasmReg((uint)index, TYP_FLOAT);
#elif TARGET_X86
        return (regNumber)((int)REG_XMM0 + index);
#else
        return FltArgRegs[index];
#endif
    }
}
