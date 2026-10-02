// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Globals;
using static RyuJitSharp.IS_INFO;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class EmitterDescriptorFormatHeaderTests
{
#if TARGET_XARCH
    [TestCase(IF_NONE, IS_NONE)]
    [TestCase(IF_RRD, IS_R1_RD)]
    [TestCase(IF_RWR, IS_R1_WR)]
    [TestCase(IF_RRW, IS_R1_RW)]
    [TestCase(IF_RRW_RRW, IS_R1_RW | IS_R2_RW)]
    [TestCase(IF_RWR_RWR_RRD, IS_R1_WR | IS_R2_WR | IS_R3_RD)]
    [TestCase(IF_RWR_RRD_RRD_RRD, IS_R1_WR | IS_R2_RD | IS_R3_RD | IS_R4_RD)]
    [TestCase(IF_MRD, IS_GM_RD)]
    [TestCase(IF_MWR, IS_GM_WR)]
    [TestCase(IF_MRW, IS_GM_RW)]
    [TestCase(IF_SRD, IS_SF_RD)]
    [TestCase(IF_SWR, IS_SF_WR)]
    [TestCase(IF_SRW, IS_SF_RW)]
    [TestCase(IF_ARD, IS_AM_RD)]
    [TestCase(IF_AWR, IS_AM_WR)]
    [TestCase(IF_ARW, IS_AM_RW)]
    [TestCase(IF_RWR_RRD_MRD, IS_R1_WR | IS_R2_RD | IS_GM_RD)]
    [TestCase(IF_SWR_RRD_RRD, IS_SF_WR | IS_R1_RD | IS_R2_RD)]
    [TestCase(IF_ARW_RRW, IS_AM_RW | IS_R1_RW)]
    [TestCase(IF_RWR_RRD_ARD_RRD, IS_R1_WR | IS_R2_RD | IS_AM_RD | IS_R3_RD)]
    public static void NativeSchedulingMasksDriveEveryOperandClassification(Emitter.insFormat format, IS_INFO expected)
    {
        var descriptor = new Descriptor();
        descriptor.idInsFmt(format);

        Assert.That(Emitter.emitGetSchedInfo(format), Is.EqualTo(expected));
        AssertAccess(descriptor.idHasReg1(), descriptor.idIsReg1Read(), descriptor.idIsReg1Write(),
            expected, IS_R1_RD, IS_R1_WR, IS_R1_RW);
        AssertAccess(descriptor.idHasReg2(), descriptor.idIsReg2Read(), descriptor.idIsReg2Write(),
            expected, IS_R2_RD, IS_R2_WR, IS_R2_RW);
        AssertAccess(descriptor.idHasReg3(), descriptor.idIsReg3Read(), descriptor.idIsReg3Write(),
            expected, IS_R3_RD, IS_R3_WR, IS_R3_RW);
        AssertAccess(descriptor.idHasReg4(), descriptor.idIsReg4Read(), descriptor.idIsReg4Write(),
            expected, IS_R4_RD, IS_R4_WR, IS_R4_RW);
        AssertAccess(descriptor.idHasMemGen(), descriptor.idHasMemGenRead(), descriptor.idHasMemGenWrite(),
            expected, IS_GM_RD, IS_GM_WR, IS_GM_RW);
        AssertAccess(descriptor.idHasMemStk(), descriptor.idHasMemStkRead(), descriptor.idHasMemStkWrite(),
            expected, IS_SF_RD, IS_SF_WR, IS_SF_RW);
        AssertAccess(descriptor.idHasMemAdr(), descriptor.idHasMemAdrRead(), descriptor.idHasMemAdrWrite(),
            expected, IS_AM_RD, IS_AM_WR, IS_AM_RW);
        AssertAccess(descriptor.idHasMem(), descriptor.idHasMemRead(), descriptor.idHasMemWrite(), expected,
            IS_GM_RD | IS_SF_RD | IS_AM_RD, IS_GM_WR | IS_SF_WR | IS_AM_WR, IS_GM_RW | IS_SF_RW | IS_AM_RW);
    }

    private static void AssertAccess(bool present, bool read, bool write, IS_INFO schedule, IS_INFO readOnly, IS_INFO writeOnly, IS_INFO readWrite)
    {
        Assert.That(present, Is.EqualTo((schedule & (readOnly | writeOnly | readWrite)) != 0));
        Assert.That(read, Is.EqualTo((schedule & (readOnly | readWrite)) != 0));
        Assert.That(write, Is.EqualTo((schedule & (writeOnly | readWrite)) != 0));
    }

    [TestCase(IF_NONE, false)]
    [TestCase(IF_CNS, false)]
    [TestCase(IF_RRD_CNS, false)]
    [TestCase(IF_MRD, false)]
    [TestCase(IF_MRD_CNS, true)]
    [TestCase(IF_SRD_CNS, true)]
    [TestCase(IF_ARD_CNS, true)]
    [TestCase(IF_RWR_RRD_ARD_CNS, true)]
    [TestCase(IF_RWR_RRD_ARD_RRD, true)]
    public static void MemoryAndConstantClassificationUsesTheNativeOperandKind(Emitter.insFormat format, bool expected)
    {
        var descriptor = new Descriptor();
        descriptor.idInsFmt(format);

        Assert.That(descriptor.idHasMemAndCns(), Is.EqualTo(expected));
    }

    [Test]
    public static void InvalidSchedulingIndexRetainsTheNativeNoneFallback()
    {
#if DEBUG
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordFormatAssertion;
        var context = new AssertionContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
        using var tls = new JitTls(&context.JitInfo);
#endif
        Assert.That(Emitter.emitGetSchedInfo(IF_COUNT), Is.EqualTo(IS_NONE));
        Assert.That(Emitter.emitGetSchedInfo((Emitter.insFormat)uint.MaxValue), Is.EqualTo(IS_NONE));
#if DEBUG
        Assert.That(context.Assertions, Is.EqualTo(2));
#endif
    }
#endif

#if TARGET_XARCH || TARGET_ARM64
    [Test]
    public static void HighestValidFormatRetainsAllItsNativeBits()
    {
        var descriptor = new Descriptor();
        var format = (Emitter.insFormat)((uint)IF_COUNT - 1);
        descriptor.idInsFmt(format);

        Assert.That(descriptor.idInsFmt(), Is.EqualTo(format));
    }
#endif

#if !TARGET_LOONGARCH64 && !TARGET_RISCV64
    [TestCase(0x100u)]
    [TestCase(uint.MaxValue)]
    public static void NativeHeaderTruncatesOnlyAfterTheRangeAssertion(uint value)
    {
        var descriptor = new Descriptor();
#if TARGET_XARCH || TARGET_ARM64
        descriptor.idInsFmt((Emitter.insFormat)1);
#endif
#if DEBUG
#if TARGET_XARCH || TARGET_ARM64
        const uint initial = 1;
#else
        const uint initial = 0;
#endif
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordFormatAssertion;
        var context = new AssertionContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
        using var tls = new JitTls(&context.JitInfo);
        var previous = s_descriptor;
        s_descriptor = descriptor;
        try
        {
#endif
            descriptor.idInsFmt((Emitter.insFormat)value);
#if TARGET_XARCH
            const uint mask = 0x7F;
#elif TARGET_ARM64
            const uint mask = 0x3FF;
#else
            const uint mask = 0xFF;
#endif
            Assert.That((uint)descriptor.idInsFmt(), Is.EqualTo(value & mask));
#if DEBUG
            var expectedAssertions = value >= (uint)IF_COUNT ? 1 : 0;
            Assert.That(context.Assertions, Is.EqualTo(expectedAssertions));
            if (expectedAssertions != 0)
            {
                Assert.That(context.ObservedFormat, Is.EqualTo(initial));
            }
        }
        finally
        {
            s_descriptor = previous;
        }
#endif
    }
#endif

#if TARGET_LOONGARCH64
    [TestCase(0u)]
    [TestCase(uint.MaxValue)]
    public static void NativeUnusedFormatSetterLeavesTheGetterAtZero(uint value)
    {
        var descriptor = new Descriptor();
        descriptor.idInsFmt((Emitter.insFormat)value);

        Assert.That((uint)descriptor.idInsFmt(), Is.Zero);
    }
#endif

#if TARGET_RISCV64
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public static void RiscVFormatAccessPreservesTheNativeNyiContinuation(bool setter, bool continueAfterNyi)
    {
        var config = JitConfig;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
#if DEBUG
        ICorJitInfo jitInfo = default;
        using var tls = new JitTls(&jitInfo);
#endif
        var previousCompiler = JitTls.Compiler;
#if FUNC_INFO_LOGGING
        var functionLog = Compiler.compJitFuncInfoFile;
#endif
#if MEASURE_FATAL
        var nyiCount = s_fatalNyiCount;
#endif
        var configuration = new JitConfigValues();
        NyiConfiguration(configuration) = continueAfterNyi ? 2 : 0;
        var descriptor = new Descriptor();
        try
        {
            JitConfig = configuration;
            JitTls.Compiler = compiler;
#if FUNC_INFO_LOGGING
            Compiler.compJitFuncInfoFile = null;
#endif
            if (continueAfterNyi)
            {
                AccessRiscVFormat(descriptor, setter);
                Assert.That((uint)descriptor.idInsFmt(), Is.Zero);
            }
            else
            {
                var exception = Assert.Throws<FatalJitException>(() => AccessRiscVFormat(descriptor, setter));
                Assert.That(exception?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
            }
        }
        finally
        {
            JitConfig = config;
            JitTls.Compiler = previousCompiler;
#if FUNC_INFO_LOGGING
            Compiler.compJitFuncInfoFile = functionLog;
#endif
#if MEASURE_FATAL
            s_fatalNyiCount = nyiCount;
#endif
        }
    }

    private static void AccessRiscVFormat(Descriptor descriptor, bool setter)
    {
        if (setter)
        {
            descriptor.idInsFmt((Emitter.insFormat)uint.MaxValue);
        }
        else
        {
            _ = descriptor.idInsFmt();
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_altJitAssertOnNYI")]
    private static extern ref int NyiConfiguration(JitConfigValues config);
#endif

#if DEBUG && !TARGET_LOONGARCH64 && !TARGET_RISCV64
    private static Descriptor? s_descriptor;

    private struct AssertionContext
    {
        public ICorJitInfo JitInfo;
        public int Assertions;
        public uint ObservedFormat;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordFormatAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        var context = (AssertionContext*)self;
        context->Assertions++;
        if (s_descriptor is Descriptor descriptor)
        {
            context->ObservedFormat = (uint)descriptor.idInsFmt();
        }

        return 0;
    }
#endif

    private sealed class Descriptor : Emitter.instrDesc
    {
        public override int NativeLogicalSize => throw new NotSupportedException();
    }
}
