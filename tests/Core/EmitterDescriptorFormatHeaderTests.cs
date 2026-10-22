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
    [TestCase(IF_NONE, IS_NONE)]
    [TestCase(IF_XARCH_RRD, IS_R1_RD)]
    [TestCase(IF_XARCH_RWR, IS_R1_WR)]
    [TestCase(IF_XARCH_RRW, IS_R1_RW)]
    [TestCase(IF_XARCH_RRW_RRW, IS_R1_RW | IS_R2_RW)]
    [TestCase(IF_XARCH_RWR_RWR_RRD, IS_R1_WR | IS_R2_WR | IS_R3_RD)]
    [TestCase(IF_XARCH_RWR_RRD_RRD_RRD, IS_R1_WR | IS_R2_RD | IS_R3_RD | IS_R4_RD)]
    [TestCase(IF_XARCH_MRD, IS_GM_RD)]
    [TestCase(IF_XARCH_MWR, IS_GM_WR)]
    [TestCase(IF_XARCH_MRW, IS_GM_RW)]
    [TestCase(IF_XARCH_SRD, IS_SF_RD)]
    [TestCase(IF_XARCH_SWR, IS_SF_WR)]
    [TestCase(IF_XARCH_SRW, IS_SF_RW)]
    [TestCase(IF_XARCH_ARD, IS_AM_RD)]
    [TestCase(IF_XARCH_AWR, IS_AM_WR)]
    [TestCase(IF_XARCH_ARW, IS_AM_RW)]
    [TestCase(IF_XARCH_RWR_RRD_MRD, IS_R1_WR | IS_R2_RD | IS_GM_RD)]
    [TestCase(IF_XARCH_SWR_RRD_RRD, IS_SF_WR | IS_R1_RD | IS_R2_RD)]
    [TestCase(IF_XARCH_ARW_RRW, IS_AM_RW | IS_R1_RW)]
    [TestCase(IF_XARCH_RWR_RRD_ARD_RRD, IS_R1_WR | IS_R2_RD | IS_AM_RD | IS_R3_RD)]
    public static void NativeSchedulingMasksDriveEveryOperandClassification(Emitter.insFormat format, IS_INFO expected)
    {
        Assume.That(Configuration.TARGET_XARCH);

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
    [TestCase(IF_XARCH_CNS, false)]
    [TestCase(IF_XARCH_RRD_CNS, false)]
    [TestCase(IF_XARCH_MRD, false)]
    [TestCase(IF_XARCH_MRD_CNS, true)]
    [TestCase(IF_XARCH_SRD_CNS, true)]
    [TestCase(IF_XARCH_ARD_CNS, true)]
    [TestCase(IF_XARCH_RWR_RRD_ARD_CNS, true)]
    [TestCase(IF_XARCH_RWR_RRD_ARD_RRD, true)]
    public static void MemoryAndConstantClassificationUsesTheNativeOperandKind(Emitter.insFormat format, bool expected)
    {
        Assume.That(Configuration.TARGET_XARCH);

        var descriptor = new Descriptor();
        descriptor.idInsFmt(format);

        Assert.That(descriptor.idHasMemAndCns(), Is.EqualTo(expected));
    }

    [Test]
    public static void InvalidSchedulingIndexRetainsTheNativeNoneFallback()
    {
        Assume.That(Configuration.TARGET_XARCH);

        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordFormatAssertion;
        var context = new AssertionContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
        using var tls = TestConfiguration.CreateJitTls(&context.JitInfo);
        var previous = s_descriptor;
        s_descriptor = null;
        try
        {
            Assert.That(Emitter.emitGetSchedInfo(IF_XARCH_COUNT), Is.EqualTo(IS_NONE));
            Assert.That(Emitter.emitGetSchedInfo((Emitter.insFormat)uint.MaxValue), Is.EqualTo(IS_NONE));
            if (TestConfiguration.IsDebug)
            {
                Assert.That(context.Assertions, Is.EqualTo(2));
            }
        }
        finally
        {
            s_descriptor = previous;
        }
    }

    [Test]
    public static void HighestValidFormatRetainsAllItsNativeBits()
    {
        Assume.That(Configuration.TARGET_XARCH || Configuration.TARGET_ARM64);

        var descriptor = new Descriptor();
        var format = (Emitter.insFormat)(Configuration.TARGET_XARCH
            ? IF_XARCH_COUNT - 1
            : IF_ARM64_COUNT - 1);
        descriptor.idInsFmt(format);

        Assert.That(descriptor.idInsFmt(), Is.EqualTo(format));
    }

    [TestCase(0x100u)]
    [TestCase(uint.MaxValue)]
    public static void NativeHeaderTruncatesOnlyAfterTheRangeAssertion(uint value)
    {
        Assume.That(!Configuration.TARGET_LOONGARCH64 && !Configuration.TARGET_RISCV64);

        var descriptor = new Descriptor();
        if (Configuration.TARGET_XARCH || Configuration.TARGET_ARM64)
        {
            descriptor.idInsFmt((Emitter.insFormat)1);
        }

        var initial = Configuration.TARGET_XARCH || Configuration.TARGET_ARM64 ? 1u : 0u;
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordFormatAssertion;
        var context = new AssertionContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
        using var tls = TestConfiguration.CreateJitTls(&context.JitInfo);
        var previous = s_descriptor;
        s_descriptor = descriptor;
        try
        {
            descriptor.idInsFmt((Emitter.insFormat)value);
            var mask = Configuration.TARGET_XARCH ? 0x7Fu :
                Configuration.TARGET_ARM64 ? 0x3FFu : 0xFFu;
            Assert.That((uint)descriptor.idInsFmt(), Is.EqualTo(value & mask));
            if (TestConfiguration.IsDebug)
            {
                var expectedAssertions = (value >= 128 ? 1 : 0) +
                    (value >= (uint)Emitter.emitFmtToOps.Length ? 1 : 0);
                Assert.That(context.Assertions, Is.EqualTo(expectedAssertions));
                if (expectedAssertions != 0)
                {
                    Assert.That(context.ObservedFormat, Is.EqualTo(initial));
                }
            }
        }
        finally
        {
            s_descriptor = previous;
        }
    }

    [TestCase(0u)]
    [TestCase(uint.MaxValue)]
    public static void NativeUnusedFormatSetterLeavesTheGetterAtZero(uint value)
    {
        Assume.That(Configuration.TARGET_LOONGARCH64);

        var descriptor = new Descriptor();
        descriptor.idInsFmt((Emitter.insFormat)value);

        Assert.That((uint)descriptor.idInsFmt(), Is.Zero);
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public static void RiscVFormatAccessPreservesTheNativeNyiContinuation(bool setter, bool continueAfterNyi)
    {
        Assume.That(Configuration.TARGET_RISCV64);

        var config = JitConfig;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        using var tls = TestConfiguration.CreateJitTls();
        var previousCompiler = JitTls.Compiler;
        var functionLog = Compiler.compJitFuncInfoFile;
        var nyiCount = Globals.FatalStatisticsData.s_fatalNyiCount;
        var configuration = new JitConfigValues();
        NyiConfiguration(configuration) = continueAfterNyi ? 2 : 0;
        var descriptor = new Descriptor();
        try
        {
            JitConfig = configuration;
            JitTls.Compiler = compiler;
            Compiler.compJitFuncInfoFile = null;
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
            Compiler.compJitFuncInfoFile = functionLog;
            Globals.FatalStatisticsData.s_fatalNyiCount = nyiCount;
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

    private sealed class Descriptor : Emitter.instrDesc
    {
        public override int NativeLogicalSize => throw new NotSupportedException();
    }
}
