// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
#if FEATURE_CFI_SUPPORT || DEBUG
using System.Runtime.InteropServices;
#endif
#if TARGET_WASM && DEBUG
using System.IO;
using System.Text;
using static RyuJitSharp.Globals;
#endif
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class FuncInfoDscMetadataTests
{
    [TestCase(FuncKind.FUNC_ROOT, false, true)]
    [TestCase(FuncKind.FUNC_HANDLER, true, false)]
    [TestCase(FuncKind.FUNC_FILTER, true, false)]
    [TestCase(FuncKind.FUNC_COUNT, true, false)]
    public static void KindPredicatesPreserveNativeRootComparison(FuncKind kind, bool isFunclet, bool isMethod)
    {
        var descriptor = new FuncInfoDsc
        {
            funKind = kind,
            funFlags = 0xA5,
            funEHIndex = ushort.MaxValue,
        };

        Assert.That(descriptor.IsFunclet(), Is.EqualTo(isFunclet));
        Assert.That(descriptor.IsMethod(), Is.EqualTo(isMethod));
        Assert.That(descriptor.funKind, Is.EqualTo(kind));
        Assert.That(descriptor.funFlags, Is.EqualTo(0xA5));
        Assert.That(descriptor.funEHIndex, Is.EqualTo(ushort.MaxValue));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(3)]
    public static void IndexUsesActualArrayIdentityRatherThanEqualRecordValues(int index)
    {
        var compiler = NewCompilerWithDescriptors();
        ref var descriptor = ref compiler.compFuncInfos[index];

        Assert.That(descriptor.GetFuncletIdx(compiler), Is.EqualTo((uint)index));
        Assert.That(Unsafe.AreSame(in descriptor, in compiler.compFuncInfos[index]), Is.True);
    }

    [Test]
    public static void ArrayEntryIdentitySurvivesCompactingCollection()
    {
        var compiler = NewCompilerWithDescriptors();
        ref var descriptor = ref compiler.compFuncInfos[2];

        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

        Assert.That(descriptor.GetFuncletIdx(compiler), Is.EqualTo(2u));
        Assert.That(Unsafe.AreSame(in descriptor, in compiler.compFuncInfos[2]), Is.True);
    }

#if DEBUG
    [Test]
    public static unsafe void LogicalUpperBoundIsExclusiveAndRetainsContinuingEeBehavior()
    {
        var compiler = NewCompilerWithDescriptors();
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &ContinueMetadataAssertion;
        var context = new MetadataAssertionContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
        using var tls = new JitTls(&context.JitInfo);

        Assert.That(compiler.compFuncInfos[4].GetFuncletIdx(compiler), Is.EqualTo(4u));
        Assert.That(context.Assertions, Is.EqualTo(1));
    }

    private struct MetadataAssertionContext
    {
        public ICorJitInfo JitInfo;
        public int Assertions;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static unsafe int ContinueMetadataAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        var context = (MetadataAssertionContext*)self;
        context->Assertions++;
        return 0;
    }
#endif

    private static Compiler NewCompilerWithDescriptors()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compFuncInfos = new FuncInfoDsc[6];
        compiler.compFuncInfoCount = 4;
        return compiler;
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CfiPolicyRequiresUnixAndTheNativeAotAbi(bool nativeAot)
    {
        var compiler = NewCompilerWithDescriptors();
        compiler.eeInfoInitialized = true;
        compiler.eeInfo.targetAbi = nativeAot
            ? CORINFO_RUNTIME_ABI.CORINFO_NATIVEAOT_ABI
            : CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI;
#if FEATURE_CFI_SUPPORT
        var expected = TargetOS.IsUnix && nativeAot;
#else
        var expected = false;
#endif

        Assert.That(compiler.generateCFIUnwindCodes(), Is.EqualTo(expected));
    }

#if FEATURE_CFI_SUPPORT
    [StructLayout(LayoutKind.Sequential)]
    private struct CfiAlignmentProbe
    {
        public byte Prefix;
        public CFI_CODE Code;
        public byte Suffix;
    }

    [Test]
    public static void EmbeddedCfiRecordRetainsFourByteAlignment()
    {
        var probe = new CfiAlignmentProbe { Prefix = 1, Code = new CFI_CODE(2, 3, 4, 5), Suffix = 6 };

        Assert.That(Unsafe.ByteOffset(in probe.Prefix, in Unsafe.As<CFI_CODE, byte>(ref probe.Code)), Is.EqualTo((nint)4));
        Assert.That(Unsafe.ByteOffset(in probe.Prefix, in probe.Suffix), Is.EqualTo((nint)12));
        Assert.That(Marshal.OffsetOf<CfiAlignmentProbe>(nameof(CfiAlignmentProbe.Code)), Is.EqualTo((nint)4));
        Assert.That(Unsafe.SizeOf<CfiAlignmentProbe>(), Is.EqualTo(16));
        Assert.That(probe.Code.Offset, Is.EqualTo(5));
    }

    [Test]
    public static void CfiRecordRestoresThePublishedEightByteLayout()
    {
        Assert.That(Unsafe.SizeOf<CFI_CODE>(), Is.EqualTo(8));
        Assert.That(Marshal.SizeOf<CFI_CODE>(), Is.EqualTo(8));
        Assert.That(Marshal.OffsetOf<CFI_CODE>(nameof(CFI_CODE.CodeOffset)), Is.EqualTo((nint)0));
        Assert.That(Marshal.OffsetOf<CFI_CODE>(nameof(CFI_CODE.CfiOpCode)), Is.EqualTo((nint)1));
        Assert.That(Marshal.OffsetOf<CFI_CODE>(nameof(CFI_CODE.DwarfReg)), Is.EqualTo((nint)2));
        Assert.That(Marshal.OffsetOf<CFI_CODE>(nameof(CFI_CODE.Offset)), Is.EqualTo((nint)4));
    }

    [TestCase((byte)0, (byte)0, (short)-1, 0)]
    [TestCase(byte.MaxValue, (byte)1, short.MinValue, int.MinValue)]
    [TestCase((byte)1, (byte)2, short.MaxValue, int.MaxValue)]
    [TestCase((byte)2, (byte)3, (short)0, -1)]
    [TestCase((byte)3, (byte)4, (short)32, 1)]
    [TestCase(byte.MaxValue, byte.MaxValue, short.MinValue, int.MaxValue)]
    public static void CfiConstructorPreservesAllNativeFields(byte codeOffset, byte opcode, short register, int offset)
    {
        var code = new CFI_CODE(codeOffset, opcode, register, offset);

        Assert.That(code.CodeOffset, Is.EqualTo(codeOffset));
        Assert.That(code.CfiOpCode, Is.EqualTo(opcode));
        Assert.That(code.DwarfReg, Is.EqualTo(register));
        Assert.That(code.Offset, Is.EqualTo(offset));
    }

    [Test]
    public static void CfiVectorReferenceRetainsItsAliasAcrossMetadataCopies()
    {
        var descriptor = new FuncInfoDsc
        {
            cfiCodes = [new CFI_CODE(1, 2, -1, 4)],
        };
        var copy = descriptor;

        Assert.That(copy.cfiCodes, Is.SameAs(descriptor.cfiCodes));
    }
#endif

#if TARGET_WASM
    [Test]
    public static void WasmValueTypePreservesTheUnsignedOrdinalsAndPointerAlias()
    {
        Assert.That(Enum.GetUnderlyingType(typeof(WasmValueType)), Is.EqualTo(typeof(uint)));
        WasmValueType[] types =
        [
            WasmValueType.Invalid, WasmValueType.I32, WasmValueType.I64, WasmValueType.F32,
            WasmValueType.F64, WasmValueType.V128, WasmValueType.ExnRef, WasmValueType.Count,
        ];
        for (uint index = 0; index < types.Length; index++)
        {
            Assert.That((uint)types[index], Is.EqualTo(index));
        }

        Assert.That(WasmValueType.First, Is.EqualTo(WasmValueType.I32));
#if TARGET_64BIT
        Assert.That(WasmValueType.I, Is.EqualTo(WasmValueType.I64));
#else
        Assert.That(WasmValueType.I, Is.EqualTo(WasmValueType.I32));
#endif
    }

    [Test]
    public static void WasmExceptionIndexDefaultIsPreservedForEveryClrCreationForm()
    {
        FuncInfoDsc descriptor = default;
        var constructed = new FuncInfoDsc();
        var descriptors = new FuncInfoDsc[1];

        Assert.That(descriptor.funWasmExnRefLocalIndex, Is.EqualTo(uint.MaxValue));
        Assert.That(constructed.funWasmExnRefLocalIndex, Is.EqualTo(uint.MaxValue));
        Assert.That(descriptors[0].funWasmExnRefLocalIndex, Is.EqualTo(uint.MaxValue));
    }

    [TestCase(0u)]
    [TestCase(1u)]
    [TestCase(uint.MaxValue)]
    public static void WasmExceptionIndexPreservesTheFullUnsignedDomain(uint index)
    {
        var descriptor = new FuncInfoDsc { funWasmExnRefLocalIndex = index };
        var copy = descriptor;

        Assert.That(descriptor.funWasmExnRefLocalIndex, Is.EqualTo(index));
        Assert.That(copy.funWasmExnRefLocalIndex, Is.EqualTo(index));
    }

    [Test]
    public static void WasmMetadataPreservesUnsignedFieldsAndDeclarationAliases()
    {
        var descriptor = new FuncInfoDsc
        {
            funWasmFrameSize = uint.MaxValue,
            startVirtualIP = uint.MaxValue - 1,
            endVirtualIP = uint.MaxValue,
            funWasmLocalDecls = [new FuncInfoDsc.WasmLocalsDecl { Type = WasmValueType.ExnRef, Count = uint.MaxValue }],
        };
        var copy = descriptor;

        Assert.That(copy.funWasmFrameSize, Is.EqualTo(uint.MaxValue));
        Assert.That(copy.startVirtualIP, Is.EqualTo(uint.MaxValue - 1));
        Assert.That(copy.endVirtualIP, Is.EqualTo(uint.MaxValue));
        Assert.That(copy.funWasmLocalDecls, Is.SameAs(descriptor.funWasmLocalDecls));
        Assert.That(descriptor.funWasmLocalDecls[0].Type, Is.EqualTo(WasmValueType.ExnRef));
        Assert.That(descriptor.funWasmLocalDecls[0].Count, Is.EqualTo(uint.MaxValue));
    }

    [Test]
    public static unsafe void UnwindableMarkerDoesNotEvaluateTheIndexWhenTraceIsDisabled()
    {
        var compiler = NewCompilerWithDescriptors();
        var descriptor = new FuncInfoDsc();
#if DEBUG
        ICorJitInfo jitInfo = default;
        using var tls = new JitTls(&jitInfo);
        JitTls.Compiler = compiler;
        compiler.verbose = false;
#endif
        descriptor.ensureUnwindableFrame(compiler);
        descriptor.ensureUnwindableFrame(compiler);

        Assert.That(descriptor.needsUnwindableFrame, Is.True);
    }

#if DEBUG
    [Test]
    public static unsafe void UnwindableTraceObservesTheUnchangedMarkerBeforeTheWrite()
    {
        var compiler = NewCompilerWithDescriptors();
        compiler.compFuncInfos[1].funKind = FuncKind.FUNC_HANDLER;
        compiler.verbose = true;
        ICorJitInfo jitInfo = default;
        using var tls = new JitTls(&jitInfo);
        JitTls.Compiler = compiler;
        var stdout = s_jitstdout;
        var logFailed = LogToEeFailed(null);
        using var output = new MemoryStream();
        using var writer = new TraceWriter(output, compiler);
        try
        {
            s_jitstdout = writer;
            LogToEeFailed(null) = true;
            compiler.compFuncInfos[1].ensureUnwindableFrame(compiler);
            compiler.compFuncInfos[1].ensureUnwindableFrame(compiler);
            writer.Flush();

            Assert.That(writer.Writes, Is.EqualTo(1));
            Assert.That(writer.MarkerDuringWrite, Is.False);
            Assert.That(compiler.compFuncInfos[1].needsUnwindableFrame, Is.True);
            Assert.That(Encoding.UTF8.GetString(output.ToArray()), Is.EqualTo("Funclet (index 1) needs to be unwindable\n"));
        }
        finally
        {
            s_jitstdout = stdout;
            LogToEeFailed(null) = logFailed;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "s_logToEEfailed")]
    private static extern ref bool LogToEeFailed(Globals? globals);

    private sealed class TraceWriter : StreamWriter
    {
        private readonly Compiler _compiler;

        public TraceWriter(Stream output, Compiler compiler)
            : base(output, new UTF8Encoding(false), leaveOpen: true)
        {
            _compiler = compiler;
        }

        public int Writes { get; private set; }

        public bool MarkerDuringWrite { get; private set; }

        public override void Write(string? value)
        {
            Writes++;
            MarkerDuringWrite = _compiler.compFuncInfos[1].needsUnwindableFrame;
            base.Write(value);
        }
    }
#endif
#endif

#if TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64
    [Test]
    public static void UnportedUnwindConstructionCannotCreateASuccessfulPayload()
    {
        var exception = Assert.Throws<FatalJitException>(() => new UnwindInfo());
        Assert.That(exception?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
    }

    [Test]
    public static void EmbeddedHotUnwindReadTerminatesInsteadOfInventingState()
    {
        FuncInfoDsc descriptor = default;
        var exception = Assert.Throws<FatalJitException>(() => _ = descriptor.uwi);
        Assert.That(exception?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
        Assert.That(descriptor.uwiCold, Is.Null);
    }

    [Test]
    public static void ColdUnwindReferenceAndRequiredReadsRetainTheirContracts()
    {
        var cold = (UnwindInfo)RuntimeHelpers.GetUninitializedObject(typeof(UnwindInfo));
        var descriptor = new FuncInfoDsc { uwiCold = cold };
        var copy = descriptor;

        Assert.That(copy.uwiCold, Is.SameAs(cold));
        var read = Assert.Throws<FatalJitException>(() => cold.GetCurrentEmitterLocation());
        Assert.That(read?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
        var initialize = Assert.Throws<FatalJitException>(() => cold.InitUnwindInfo(NewCompilerWithDescriptors(), null, null));
        Assert.That(initialize?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
    }
#endif
}
