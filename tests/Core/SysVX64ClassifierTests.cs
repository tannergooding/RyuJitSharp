// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if UNIX_AMD64_ABI
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoGCType;
using static RyuJitSharp.Globals;
using static RyuJitSharp.SystemVClassificationType;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class SysVX64ClassifierTests
{
    [TestCase(SystemVClassificationTypeInteger, 0, TYP_INT)]
    [TestCase(SystemVClassificationTypeInteger, 1, TYP_INT)]
    [TestCase(SystemVClassificationTypeInteger, 4, TYP_INT)]
    [TestCase(SystemVClassificationTypeInteger, 5, TYP_LONG)]
    [TestCase(SystemVClassificationTypeInteger, 8, TYP_LONG)]
    [TestCase(SystemVClassificationTypeIntegerReference, 8, TYP_REF)]
    [TestCase(SystemVClassificationTypeIntegerByRef, 8, TYP_BYREF)]
    [TestCase(SystemVClassificationTypeSSE, 1, TYP_FLOAT)]
    [TestCase(SystemVClassificationTypeSSE, 4, TYP_FLOAT)]
    [TestCase(SystemVClassificationTypeSSE, 5, TYP_DOUBLE)]
    [TestCase(SystemVClassificationTypeSSE, 8, TYP_DOUBLE)]
    public static void EightbyteTypePreservesNativeSizeAndGcClassification(
        SystemVClassificationType classification, byte size, var_types expected)
    {
        var descriptor = new SYSTEMV_AMD64_CORINFO_STRUCT_REG_PASSING_DESCRIPTOR();
        descriptor.eightByteClassifications[1] = classification;
        descriptor.eightByteSizes[1] = size;

        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        Assert.That(compiler.GetEightByteType(descriptor, 1), Is.EqualTo(expected));
    }

    [Test]
    public static void StructReturnInitializesOnlyTheDescriptorRegisters()
    {
        var descriptor = Descriptor(SystemVClassificationTypeInteger, SystemVClassificationTypeSSE);
        WithCompiler(descriptor, 13, (compiler, layout, metadata) => {
            var call = new GenTreeCall(TYP_STRUCT);
            call.InitializeStructReturnType(compiler, layout.ClassHandle, CorInfoCallConvExtension.Managed);

            Assert.That(call.HasMultiRegRetVal, Is.True);
            Assert.That(call.ReturnTypeDesc.ReturnRegCount, Is.EqualTo(2));
            Assert.That(call.ReturnTypeDesc.GetAbiReturnRegs(CorInfoCallConvExtension.Managed),
                Is.EqualTo(RBM_RAX | RBM_XMM0));
            Assert.That(call.GetMultiRegCount(compiler), Is.EqualTo(2));
            Assert.That(metadata.QueryCount, Is.GreaterThan(0));
        });
    }

    [TestCase(genTreeOps.GT_COPY, true, false)]
    [TestCase(genTreeOps.GT_COPY, false, true)]
    [TestCase(genTreeOps.GT_RELOAD, true, true)]
    public static void MultiRegisterMasksIncludeOnlyTheAssignedPositions(genTreeOps oper, bool first, bool second)
    {
        var descriptor = Descriptor(SystemVClassificationTypeInteger, SystemVClassificationTypeSSE);
        WithCompiler(descriptor, 13, (compiler, layout, metadata) => {
            var call = new GenTreeCall(TYP_STRUCT);
            call.InitializeStructReturnType(compiler, layout.ClassHandle, CorInfoCallConvExtension.Managed);
            call.ClearOtherRegs();
            call.SetRegNumByIdx(regNumber.REG_RAX, 0);
            call.SetRegNumByIdx(regNumber.REG_XMM0, 1);

            Assert.That(call.RegMask, Is.EqualTo(RBM_RAX | RBM_XMM0));
            Assert.That(call.GetOtherRegMask(), Is.EqualTo(RBM_XMM0));

            var copy = new GenTreeCopyOrReload(oper, TYP_STRUCT, call);
            copy.SetRegNumByIdx(first ? regNumber.REG_RCX : regNumber.REG_NA, 0);
            copy.SetRegNumByIdx(second ? regNumber.REG_XMM1 : regNumber.REG_NA, 1);
            var expected = (first ? RBM_RCX : RBM_NONE) | (second ? RBM_XMM1 : RBM_NONE);
            Assert.That(copy.RegMask, Is.EqualTo(expected));

            call.SetRegNumByIdx(regNumber.REG_NA, 1);
            call.SetRegNumByIdx(regNumber.REG_RDX, 2);
            Assert.That(call.GetOtherRegMask(), Is.EqualTo(RBM_NONE));
        });
    }

    [TestCase(TYP_INT, 6, regNumber.REG_EDI, 4)]
    [TestCase(TYP_DOUBLE, 8, regNumber.REG_XMM0, 8)]
    [TestCase(TYP_REF, 6, regNumber.REG_EDI, 8)]
    [TestCase(TYP_BYREF, 6, regNumber.REG_EDI, 8)]
    public static void ScalarRegistersExhaustOnlyTheirOwnBank(
        var_types type, int capacity, regNumber firstRegister, int size)
    {
        WithCompiler(default, 0, (compiler, layout, metadata) => {
            var classifier = new SysVX64Classifier(default);

            for (var i = 0; i < capacity; i++)
            {
                var info = classifier.Classify(compiler, type, null, WellKnownArg.None);
                Assert.That(info.NumSegments, Is.EqualTo(1));
                Assert.That(info.Segments[0].IsPassedInRegister, Is.True);
                Assert.That(info.Segments[0].Offset, Is.Zero);
                Assert.That(info.Segments[0].Size, Is.EqualTo(size));
                if (i == 0)
                {
                    Assert.That(info.Segments[0].Register, Is.EqualTo(firstRegister));
                }
            }

            var spilled = classifier.Classify(compiler, type, null, WellKnownArg.None);
            AssertStack(spilled, 0, size);
            Assert.That(classifier.StackSize, Is.EqualTo(TARGET_POINTER_SIZE));

            var otherType = type is TYP_DOUBLE ? TYP_INT : TYP_DOUBLE;
            var otherRegister = type is TYP_DOUBLE ? regNumber.REG_EDI : regNumber.REG_XMM0;
            var other = classifier.Classify(compiler, otherType, null, WellKnownArg.None);
            Assert.That(other.Segments[0].Register, Is.EqualTo(otherRegister));
            Assert.That(classifier.StackSize, Is.EqualTo(TARGET_POINTER_SIZE));
            Assert.That(metadata.QueryCount, Is.Zero);
        });
    }

    [TestCase(SystemVClassificationTypeInteger, SystemVClassificationTypeSSE, regNumber.REG_EDI, regNumber.REG_XMM0)]
    [TestCase(SystemVClassificationTypeSSE, SystemVClassificationTypeInteger, regNumber.REG_XMM0, regNumber.REG_EDI)]
    [TestCase(SystemVClassificationTypeIntegerReference, SystemVClassificationTypeIntegerByRef, regNumber.REG_EDI, regNumber.REG_ESI)]
    public static void StructEightbytesRetainDescriptorOffsetsSizesAndBankOrder(
        SystemVClassificationType first, SystemVClassificationType second, regNumber firstReg, regNumber secondReg)
    {
        var descriptor = Descriptor(first, second);
        var secondSize = second is SystemVClassificationTypeIntegerByRef ? 8 : 5;
        descriptor.eightByteSizes[1] = (byte)secondSize;
        WithCompiler(descriptor, (uint)(8 + secondSize), (compiler, layout, metadata) => {
            var classifier = new SysVX64Classifier(default);
            var info = classifier.Classify(compiler, TYP_STRUCT, layout, WellKnownArg.None);

            Assert.That(info.NumSegments, Is.EqualTo(2));
            Assert.That(info.IsPassedByReference, Is.False);
            Assert.That(info.Segments[0].Register, Is.EqualTo(firstReg));
            Assert.That(info.Segments[0].Offset, Is.Zero);
            Assert.That(info.Segments[0].Size, Is.EqualTo(8));
            Assert.That(info.Segments[1].Register, Is.EqualTo(secondReg));
            Assert.That(info.Segments[1].Offset, Is.EqualTo(8));
            Assert.That(info.Segments[1].Size, Is.EqualTo(secondSize));
            Assert.That(classifier.StackSize, Is.Zero);
            Assert.That(metadata.QueryCount, Is.EqualTo(1));
        });
    }

    [TestCase(SystemVClassificationTypeInteger, TYP_INT, 5, regNumber.REG_R9)]
    [TestCase(SystemVClassificationTypeSSE, TYP_DOUBLE, 7, regNumber.REG_XMM7)]
    public static void InsufficientBankCapacitySpillsEntireStructWithoutConsumingLastRegister(
        SystemVClassificationType classification, var_types scalar, int consumed, regNumber remaining)
    {
        var descriptor = Descriptor(classification, classification);
        WithCompiler(descriptor, 13, (compiler, layout, metadata) => {
            var classifier = new SysVX64Classifier(default);
            for (var i = 0; i < consumed; i++)
            {
                _ = classifier.Classify(compiler, scalar, null, WellKnownArg.None);
            }

            AssertStack(classifier.Classify(compiler, TYP_STRUCT, layout, WellKnownArg.None), 0, 13);
            Assert.That(classifier.StackSize, Is.EqualTo(16));
            Assert.That(classifier.Classify(compiler, scalar, null, WellKnownArg.None).Segments[0].Register,
                Is.EqualTo(remaining));
            Assert.That(classifier.StackSize, Is.EqualTo(16));
            Assert.That(metadata.QueryCount, Is.EqualTo(1));
        });
    }

    [TestCase(TYP_INT, 6, TYP_DOUBLE, regNumber.REG_XMM0)]
    [TestCase(TYP_DOUBLE, 8, TYP_INT, regNumber.REG_EDI)]
    public static void MixedAggregateNeedsBothBanksWithoutConsumingTheOther(
        var_types exhaustedType, int consumed, var_types otherType, regNumber remaining)
    {
        var descriptor = Descriptor(SystemVClassificationTypeInteger, SystemVClassificationTypeSSE);
        WithCompiler(descriptor, 13, (compiler, layout, metadata) => {
            var classifier = new SysVX64Classifier(default);
            for (var i = 0; i < consumed; i++)
            {
                _ = classifier.Classify(compiler, exhaustedType, null, WellKnownArg.None);
            }

            AssertStack(classifier.Classify(compiler, TYP_STRUCT, layout, WellKnownArg.None), 0, 13);
            Assert.That(classifier.Classify(compiler, otherType, null, WellKnownArg.None).Segments[0].Register,
                Is.EqualTo(remaining));
            Assert.That(classifier.StackSize, Is.EqualTo(16));
            Assert.That(metadata.QueryCount, Is.EqualTo(1));
        });
    }

    [Test]
    public static void FullWidthIntegerSegmentsRetainTheirGcAndByrefTypes()
    {
        var descriptor = Descriptor(SystemVClassificationTypeIntegerReference, SystemVClassificationTypeIntegerByRef);
        descriptor.eightByteSizes[1] = 8;
        WithCompiler(descriptor, 16, (compiler, layout, metadata) => {
            layout.GCPtrCount = 2;
            layout._gcPtrs = [TYPE_GC_REF, TYPE_GC_BYREF];
            var classifier = new SysVX64Classifier(default);
            var info = classifier.Classify(compiler, TYP_STRUCT, layout, WellKnownArg.None);

            Assert.That(info.Segments[0].GetRegisterType(layout), Is.EqualTo(TYP_REF));
            Assert.That(info.Segments[1].GetRegisterType(layout), Is.EqualTo(TYP_BYREF));
            Assert.That(metadata.QueryCount, Is.EqualTo(1));
        });
    }

    [TestCase(1, 8)]
    [TestCase(9, 16)]
    [TestCase(17, 24)]
    public static void MemoryClassifiedStructsUseWholeSizeAndRoundedStackSlots(int size, int stackSize)
    {
        WithCompiler(default, (uint)size, (compiler, layout, metadata) => {
            var classifier = new SysVX64Classifier(default);

            AssertStack(classifier.Classify(compiler, TYP_STRUCT, layout, WellKnownArg.None), 0, size);
            AssertStack(classifier.Classify(compiler, TYP_STRUCT, layout, WellKnownArg.None), stackSize, size);
            Assert.That(classifier.StackSize, Is.EqualTo(stackSize * 2));
            Assert.That(classifier.Classify(compiler, TYP_INT, null, WellKnownArg.None).Segments[0].Register,
                Is.EqualTo(regNumber.REG_EDI));
            Assert.That(metadata.QueryCount, Is.EqualTo(2));
        });
    }

    [Test]
    public static void SecretStubRegisterDoesNotConsumeAnArgumentRegisterOrStackSpace()
    {
        WithCompiler(default, 0, (compiler, layout, metadata) => {
            var classifier = new SysVX64Classifier(default);
            var secret = classifier.Classify(compiler, TYP_I_IMPL, null, WellKnownArg.SecretStubParam);

            Assert.That(secret.Segments[0].Register, Is.EqualTo(REG_SECRET_STUB_PARAM));
            Assert.That(secret.Segments[0].Size, Is.EqualTo(TARGET_POINTER_SIZE));
            Assert.That(classifier.Classify(compiler, TYP_INT, null, WellKnownArg.None).Segments[0].Register,
                Is.EqualTo(regNumber.REG_EDI));
            Assert.That(classifier.StackSize, Is.Zero);
            Assert.That(metadata.QueryCount, Is.Zero);
        });
    }

    [Test]
    public static void StackRoundingUsesNativeUnsignedOverflow()
    {
        WithCompiler(default, uint.MaxValue, (compiler, layout, metadata) => {
            var classifier = new SysVX64Classifier(default);
            AssertStack(classifier.Classify(compiler, TYP_STRUCT, layout, WellKnownArg.None), 0, -1);
            Assert.That(classifier.StackSize, Is.Zero);
            AssertStack(classifier.Classify(compiler, TYP_STRUCT, layout, WellKnownArg.None), 0, -1);
            Assert.That(metadata.QueryCount, Is.EqualTo(2));
        });
    }

    [Test]
    public static void PrologParameterScopeCapturesBothSysVRegisters()
    {
        WithCompiler(default, 0, (compiler, _, _) =>
        {
            CORINFO_METHOD_INFO methodInfo = default;
            JitFlags flags = default;
            compiler.info.compMethodInfo = &methodInfo;
            compiler.info.compRetBuffArg = BAD_VAR_NUM;
            compiler.opts.jitFlags = &flags;
            compiler.opts.SetMinOpts(true);
            compiler.opts.compDbgInfo = true;
            compiler.info.compIsStatic = true;
            compiler.info.compArgsCount = 1;
            compiler.info.compLocalsCount = 2;
            compiler.lvaTable = [
                new() {
                    Type = TYP_STRUCT,
                    Layout = new ClassLayout(16),
                    lvIsParam = true,
                    lvTracked = true,
                    lvOnFrame = true,
                    lvFramePointerBased = true,
                    RegNum = REG_STK,
                },
                new() { Type = TYP_STRUCT, Layout = new ClassLayout(0), lvOnFrame = true, RegNum = REG_STK },
            ];
            compiler.lvaCount = 2;
            compiler.lvaTrackedCount = 1;
            compiler.lvaTrackedCountInSizeTUnits = 1;
            compiler.lvaTrackedToVarNum = [0];
            compiler.lvaOutgoingArgSpaceVar = 1;
            compiler.lvaOutgoingArgSpaceSize.ResetWritePhase();
            compiler.lvaOutgoingArgSpaceSize.Value = 0;
            compiler.lvaRetAddrVar = BAD_VAR_NUM;
            compiler.lvaGSSecurityCookie = BAD_VAR_NUM;
            compiler.lvaSecretStubArg = BAD_VAR_NUM;
            compiler.lvaParameterPassingInfo = [AbiPassingInformation.FromSegments(compiler,
                AbiPassingSegment.InRegister(REG_RDI, 0, 8), AbiPassingSegment.InRegister(REG_XMM0, 8, 8))];
            compiler.info.compVarScopes = [
                new() { vsdVarNum = 0, vsdLVnum = 0, vsdLifeBeg = 0, vsdLifeEnd = 10 },
            ];
            compiler.info.compVarScopesCount = 1;
            compiler.compInitScopeLists();
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            codeGen.initializeVariableLiveKeeper();
            codeGen.Emitter.emitBegCG(compiler, default);
            codeGen.Emitter.Init();
            codeGen.Emitter.emitBegFN(false
#if DEBUG
                , true
#endif
                );
            codeGen.Emitter.emitBegProlog();

            codeGen.psiBegProlog();

            var range = codeGen.getVariableLiveKeeper().getLiveRangesForVarForProlog(0)[0];
            Assert.That(range.m_VarLocation.vlType, Is.EqualTo(ICorDebugInfo.VarLocType.VLT_REG_REG));
            Assert.That(range.m_VarLocation.vlRegReg.vlrrReg1,
                Is.EqualTo(CodeGen.siVarLoc.mapRegNumToDebugRegNum(REG_RDI)));
            Assert.That(range.m_VarLocation.vlRegReg.vlrrReg2,
                Is.EqualTo(CodeGen.siVarLoc.mapRegNumToDebugRegNum(REG_XMM0)));
        });
    }

    private static SYSTEMV_AMD64_CORINFO_STRUCT_REG_PASSING_DESCRIPTOR Descriptor(
        SystemVClassificationType first, SystemVClassificationType second)
    {
        var descriptor = new SYSTEMV_AMD64_CORINFO_STRUCT_REG_PASSING_DESCRIPTOR {
            passedInRegisters = true,
            eightByteCount = 2,
        };
        descriptor.eightByteClassifications[0] = first;
        descriptor.eightByteClassifications[1] = second;
        descriptor.eightByteSizes[0] = 8;
        descriptor.eightByteSizes[1] = 5;
        descriptor.eightByteOffsets[1] = 8;
        return descriptor;
    }

    private static void AssertStack(AbiPassingInformation info, int offset, int size)
    {
        Assert.That(info.NumSegments, Is.EqualTo(1));
        Assert.That(info.IsPassedByReference, Is.False);
        Assert.That(info.Segments[0].IsPassedOnStack, Is.True);
        Assert.That(info.Segments[0].StackOffset, Is.EqualTo(offset));
        Assert.That(info.Segments[0].Offset, Is.Zero);
        Assert.That(info.Segments[0].Size, Is.EqualTo(size));
    }

    private sealed class Metadata
    {
        public SYSTEMV_AMD64_CORINFO_STRUCT_REG_PASSING_DESCRIPTOR Descriptor;
        public uint Size;
        public int QueryCount;
    }

    private delegate void ClassificationAction(Compiler compiler, ClassLayout layout, Metadata metadata);

    private static void WithCompiler(
        SYSTEMV_AMD64_CORINFO_STRUCT_REG_PASSING_DESCRIPTOR descriptor, uint size, ClassificationAction action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getSystemVAmd64PassStructInRegisterDescriptor = &GetDescriptor;
        vtable.Base.Base.getClassSize = &GetClassSize;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compCompHnd = &jitInfo;

#if DEBUG
        using var tls = new JitTls(&jitInfo);
#endif
        var previous = JitTls.Compiler;
        JitTls.Compiler = compiler;
        var metadata = new Metadata { Descriptor = descriptor, Size = size };
        var handle = GCHandle.Alloc(metadata);
        try
        {
            var layout = size == 0 ? new ClassLayout(1) :
                new ClassLayout((CORINFO_CLASS_STRUCT_*)GCHandle.ToIntPtr(handle), true, size,
                    TYP_STRUCT, "Struct", "Struct");
            action(compiler, layout, metadata);
        }
        finally
        {
            handle.Free();
            JitTls.Compiler = previous;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetClassSize(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* handle)
    {
        var metadata = GCHandle.FromIntPtr((nint)handle).Target as Metadata;
        assert(metadata is not null);

        return unchecked((int)metadata.Size);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte GetDescriptor(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* handle,
        SYSTEMV_AMD64_CORINFO_STRUCT_REG_PASSING_DESCRIPTOR* result)
    {
        var metadata = GCHandle.FromIntPtr((nint)handle).Target as Metadata;
        assert(metadata is not null);
        metadata.QueryCount++;
        result->CopyFrom(metadata.Descriptor);
        return 1;
    }
}
#endif
