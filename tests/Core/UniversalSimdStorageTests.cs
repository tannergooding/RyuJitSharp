// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class UniversalSimdStorageTests
{
    [TestCase(true, false, 64)]
    [TestCase(false, true, 32)]
    [TestCase(false, false, 16)]
    public static void LogicalCapacityUsesConfigurationRatherThanPhysicalSize(bool xarch, bool arm64, int expectedSize)
    {
        var previousXarch = Configuration.TARGET_XARCH;
        var previousArm64 = Configuration.TARGET_ARM64;
        AppContext.SetSwitch("RyuJitSharp.TARGET_XARCH", xarch);
        AppContext.SetSwitch("RyuJitSharp.TARGET_ARM64", arm64);

        try
        {
            Assert.That(SimdStorageSize, Is.EqualTo(expectedSize));
            Assert.That(Unsafe.SizeOf<simd64_t>(), Is.EqualTo(64));
        }
        finally
        {
            AppContext.SetSwitch("RyuJitSharp.TARGET_XARCH", previousXarch);
            AppContext.SetSwitch("RyuJitSharp.TARGET_ARM64", previousArm64);
        }
    }

    [Test]
    public static void PhysicalStorageRetainsAllLaneViewsAndFullWidthEquality()
    {
        simd64_t value = default;
        value.v256[1].v128[1].u64[1] = 0xFEDC_BA98_7654_3210;
        Assert.That(value.u64[7], Is.EqualTo(0xFEDC_BA98_7654_3210));
        Assert.That(value.v128[3].u64[1], Is.EqualTo(value.u64[7]));
        Assert.That(value.v64[7].u64[0], Is.EqualTo(value.u64[7]));

        var copy = value;
        Assert.That(copy.Equals(value), Is.True);
        Assert.That(copy.GetHashCode(), Is.EqualTo(value.GetHashCode()));
        copy.u8[63] ^= 1;
        Assert.That(copy == value, Is.False);

        var allBitsSet = simd64_t.AllBitsSet;
        Assert.That(allBitsSet.AsSpan<byte>().ToArray(), Is.All.EqualTo(byte.MaxValue));
    }

    [TestCase(TYP_SIMD8)]
    [TestCase(TYP_SIMD12)]
    [TestCase(TYP_SIMD16)]
    [TestCase(TYP_SIMD32)]
    [TestCase(TYP_SIMD64)]
    public static void VectorIdentityAndValueNumbersUseOnlyTheTypedPayload(var_types type)
    {
        RequireSimd(type);
        WithCompiler(compiler =>
        {
            var left = new GenTreeVecCon(type);
            var right = new GenTreeVecCon(type);
            var storage = left.SimdVal;
            Assert.That(Unsafe.SizeOf<simd64_t>(), Is.EqualTo(storage.AsSpan<byte>().Length));

            left.SimdVal.AsSpan<byte>().Fill(0xA5);
            left.SimdVal.AsSpan<byte>()[..type.Size].Clear();
            left.SimdVal.u32[0] = 0x8000_0000;
            left.SimdVal.u32[1] = 0x7F80_0001;
            right.SimdVal.AsSpan<byte>().Fill(0x5A);
            left.SimdVal.AsSpan<byte>()[..type.Size].CopyTo(right.SimdVal.AsSpan<byte>());

            Assert.That(GenTreeVecCon.Equals(left, right), Is.True);
#if DEBUG
            Assert.That(compiler.gtHashValue(left), Is.EqualTo(compiler.gtHashValue(right)));
#endif
            var store = new ValueNumStore(compiler);
            var reference = new ValueNumStore(compiler);
            var expected = left.SimdVal.AsSpan<byte>()[..type.Size].ToArray();
            var first = store.VNForGenericCon(type, left.SimdVal.AsSpan<byte>());
            var repeated = store.VNForGenericCon(type, right.SimdVal.AsSpan<byte>());
            Assert.That(repeated, Is.EqualTo(first));
            Assert.That(first, Is.EqualTo(reference.VNForGenericCon(type, expected)));
            Assert.That(store.VNForIntCon(93), Is.EqualTo(reference.VNForIntCon(93)));

            var extracted = store.GetConstantSimd(first);
            Assert.That(extracted.AsSpan<byte>()[..type.Size].ToArray(), Is.EqualTo(expected));
            Assert.That(extracted.AsSpan<byte>()[type.Size..].ToArray(), Is.All.EqualTo((byte)0));

            right.SimdVal.u32[0] = 0;
            Assert.That(GenTreeVecCon.Equals(left, right), Is.False);
            Assert.That(store.VNForGenericCon(type, right.SimdVal.AsSpan<byte>()), Is.Not.EqualTo(first));
            right.SimdVal.u32[0] = 0x8000_0000;
            right.SimdVal.u32[1]++;
            Assert.That(store.VNForGenericCon(type, right.SimdVal.AsSpan<byte>()), Is.Not.EqualTo(first));
        });
    }

    [TestCase(TYP_SIMD8)]
    [TestCase(TYP_SIMD12)]
    [TestCase(TYP_SIMD16)]
    [TestCase(TYP_SIMD32)]
    [TestCase(TYP_SIMD64)]
    public static void PredicatesAndUnaryFoldingIgnoreAndPreserveInactiveBytes(var_types type)
    {
        RequireSimd(type);
        WithCompiler(compiler =>
        {
            var vector = new GenTreeVecCon(type);
            vector.SimdVal.AsSpan<byte>().Fill(0xA5);
            vector.SimdVal.AsSpan<byte>()[..type.Size].Clear();
            Assert.That(vector.IsZero, Is.True);
            Assert.That(vector.IsAllBitsSet, Is.False);

            Assert.That(vector.TryEvaluateUnaryInPlace(GT_NOT, false, TYP_INT), Is.True);
            Assert.That(vector.IsZero, Is.False);
            Assert.That(vector.IsAllBitsSet, Is.True);
            Assert.That(vector.SimdVal.AsSpan<byte>()[type.Size..].ToArray(), Is.All.EqualTo((byte)0xA5));
        });
    }

#if FEATURE_SIMD
    [Test]
    public static void PatternConstructionUsesTheTargetCapacity()
    {
        RequireSimd(TYP_SIMD16);
        WithCompiler(compiler =>
        {
            var vector = compiler.gtNewConWithPattern(TYP_SIMD16, 0xA5).AsVecCon();
            Assert.That(vector.SimdVal.AsSpan<byte>()[..SimdStorageSize].ToArray(), Is.All.EqualTo((byte)0xA5));
            Assert.That(vector.SimdVal.AsSpan<byte>()[SimdStorageSize..].ToArray(), Is.All.EqualTo((byte)0));
        });
    }
#endif

    [TestCase(TYP_SIMD32)]
    [TestCase(TYP_SIMD64)]
    public static void UniversalStorageDoesNotEnableForeignVectorTypes(var_types type)
    {
        WithCompiler(compiler =>
        {
            var store = new ValueNumStore(compiler);
            if (Configuration.FEATURE_SIMD && Configuration.TARGET_XARCH)
            {
                var vector = new GenTreeVecCon(type);
                Assert.That(vector.IsZero, Is.True);
                Assert.That(store.TypeOfVN(store.VNZeroForType(type)), Is.EqualTo(type));
            }
            else
            {
                _ = Assert.Throws<FatalJitException>(
                    () => Assert.That(new GenTreeVecCon(type).IsZero, Is.True));
                _ = Assert.Throws<PlatformNotSupportedException>(() => store.VNZeroForType(type));
            }
        });
    }

    [TestCase(TYP_SIMD32)]
    [TestCase(TYP_SIMD64)]
    public static void GenericConstantPreservesXarchVectorWidths(var_types type)
    {
        WithCompiler(compiler =>
        {
            var bytes = new byte[type.Size];
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)i;
            }

            if (Configuration.FEATURE_SIMD && Configuration.TARGET_XARCH)
            {
                var vector = compiler.gtNewGenericCon(type, bytes).AsVecCon();

                Assert.That(vector.Type, Is.EqualTo(type));
                Assert.That(vector.SimdVal.AsSpan<byte>()[..type.Size].ToArray(), Is.EqualTo(bytes));
            }
            else
            {
                _ = Assert.Throws<FatalJitException>(() => compiler.gtNewGenericCon(type, bytes));
            }
        });
    }

#if FEATURE_SIMD
    [TestCase(TYP_SIMD8, TYP_DOUBLE)]
    [TestCase(TYP_DOUBLE, TYP_SIMD8)]
    [TestCase(TYP_SIMD16, TYP_SIMD16)]
    public static void BitcastLoweringCopiesOnlyActiveBytes(var_types sourceType, var_types destinationType)
    {
        RequireSimd(TYP_SIMD16);
        WithCompiler(compiler =>
        {
            var bytes = new byte[sourceType.Size];
            BitConverter.GetBytes(0x7FF0_0000_0000_0001UL).CopyTo(bytes, 0);
            var source = compiler.gtNewGenericCon(sourceType, bytes);
            if (sourceType != TYP_DOUBLE)
            {
                source.AsVecCon().SimdVal.AsSpan<byte>()[sourceType.Size..].Fill(0xA5);
            }

            var bitcast = new GenTreeUnOp(GT_BITCAST, destinationType, source);
            var consumer = new GenTreeUnOp(GT_RETURN, TYP_VOID, bitcast);
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            block.InsertAtEnd(source);
            block.InsertAtEnd(bitcast);
            block.InsertAtEnd(consumer);
            compiler.fgNodeThreading = NodeThreading.LIR;
            compiler.codeGen = new CodeGen(compiler);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;

            Assert.That(LowerBitCast(lowering, bitcast), Is.SameAs(consumer));
            var result = consumer.Op1;
            Assert.That(result.Type, Is.EqualTo(destinationType));
            Assert.That(block.FirstNode, Is.SameAs(result));
            Assert.That(result.Next, Is.SameAs(consumer));
            if (destinationType == TYP_DOUBLE)
            {
                Assert.That(BitConverter.DoubleToUInt64Bits(result.AsDblCon().DconVal),
                    Is.EqualTo(0x7FF0_0000_0000_0001UL));
            }
            else
            {
                Assert.That(result.AsVecCon().SimdVal.AsSpan<byte>()[..destinationType.Size].ToArray(),
                    Is.EqualTo(bytes));
                Assert.That(result.AsVecCon().SimdVal.AsSpan<byte>()[destinationType.Size..].ToArray(),
                    Is.All.EqualTo((byte)0));
            }
        });
    }
#endif

#if FEATURE_HW_INTRINSICS && FEATURE_MASKED_HW_INTRINSICS
    [TestCase(TYP_BYTE)]
    [TestCase(TYP_INT)]
    [TestCase(TYP_FLOAT)]
    [TestCase(TYP_DOUBLE)]
    public static void MaskConversionFoldingRetainsTheTargetWidePayload(var_types baseType)
    {
        RequireSimd(TYP_SIMD16);
        WithCompiler(compiler =>
        {
            var mask = compiler.gtNewMskConNode(simdmask_t.AllBitsSet(64));
            var intrinsic = compiler.gtNewSimdCvtMaskToVectorNode(TYP_SIMD16, mask, baseType, 16);
            var vector = compiler.gtFoldExpr(intrinsic).AsVecCon();
            Assert.That(vector.SimdVal.AsSpan<byte>()[..SimdStorageSize].ToArray(), Is.All.EqualTo(byte.MaxValue));
            Assert.That(vector.SimdVal.AsSpan<byte>()[SimdStorageSize..].ToArray(), Is.All.EqualTo((byte)0));

            vector.SimdVal.AsSpan<byte>().Fill(0xA5);
            vector.SimdVal.AsSpan<byte>()[..16].Clear();
            vector.SimdVal.u32[0] = 0x8000_0000;
            vector.SimdVal.u32[1] = 0x7F80_0001;
            simdmask_t extracted = default;
            EvaluateExtractMSB(TYP_FLOAT, ref extracted, vector.SimdVal.AsSpan<byte>()[..16]);
            Assert.That(extracted.RawBits, Is.EqualTo(1L));
        });
    }
#endif

#if FEATURE_HW_INTRINSICS && TARGET_ARM64
    [TestCase(TYP_BYTE)]
    [TestCase(TYP_SHORT)]
    [TestCase(TYP_INT)]
    public static void WideShiftNarrowingUsesOnlyTheNativeSimd16View(var_types baseType)
    {
        WithCompiler(compiler =>
        {
            var value = compiler.gtNewVconNode(TYP_SIMD16);
            value.EvaluateBroadcastInPlace(baseType, 1L);
            var shifts = compiler.gtNewVconNode(TYP_SIMD16);
            shifts.SimdVal.AsSpan<byte>()[16..].Fill(0xA5);
            shifts.SimdVal.u64[0] = 1;
            shifts.SimdVal.u64[1] = 2;
            var intrinsic = new GenTreeHWIntrinsic(TYP_SIMD16, NamedIntrinsic.NI_AdvSimd_ShiftLeftLogical,
                baseType, 16, value, shifts)
            {
                AuxiliaryType = TYP_ULONG,
            };

            Assert.That(compiler.gtFoldExpr(intrinsic), Is.SameAs(value));
            var count = 16 / baseType.Size;
            for (var index = 0; index < count; index++)
            {
                Assert.That(shifts.GetElementIntegral(baseType, index), Is.EqualTo(index < count / 2 ? 1L : 2L));
                Assert.That(value.GetElementIntegral(baseType, index), Is.EqualTo(index < count / 2 ? 2L : 4L));
            }
            Assert.That(shifts.SimdVal.AsSpan<byte>()[16..].ToArray(), Is.All.EqualTo((byte)0xA5));
        });
    }
#endif

#if FEATURE_SIMD && TARGET_XARCH
    [TestCase(4)]
    [TestCase(8)]
    [TestCase(16)]
    [TestCase(32)]
    [TestCase(64)]
    public static void EmittedConstantsUseOnlyTheRequestedPayload(int size)
    {
        WithCompiler(compiler =>
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            var emitter = codeGen.Emitter;
            emitter.emitBegCG(compiler, null);
            simd64_t value = default;
            value.AsSpan<byte>().Fill(0xA5);
            value.u32[0] = 0x7F80_0001;
            value.u32[1] = 0x8000_0000;
            var expected = value.AsSpan<byte>()[..size].ToArray();
            var handle = emitter.emitSimdConst(value, (emitAttr)size);
            var section = emitter.emitConsDsc.dsdList ?? throw new AssertionException("Missing SIMD constant section.");
            Assert.That(section.Data, Is.EqualTo(expected));
            Assert.That(section.dsSize, Is.EqualTo(size));

            value.AsSpan<byte>()[size..].Fill(0x5A);
            var repeated = emitter.emitSimdConst(value, (emitAttr)size);
            Assert.That((nuint)repeated, Is.EqualTo((nuint)handle));
            Assert.That(emitter.emitDataSize(), Is.EqualTo(size));
        });
    }
#endif

    private static void RequireSimd(var_types type)
    {
        if (!Configuration.FEATURE_SIMD || ((type is TYP_SIMD32 or TYP_SIMD64) && !Configuration.TARGET_XARCH))
        {
            Assert.Ignore("The configured target does not support this fixed SIMD type.");
        }
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        JitTls.Compiler = compiler;

        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

#if FEATURE_SIMD
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerBitCast")]
    private static extern GenTree? LowerBitCast(Lowering lowering, GenTreeUnOp bitcast);
#endif
}
