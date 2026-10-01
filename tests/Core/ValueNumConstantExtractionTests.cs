// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
#if DEBUG
using System.Runtime.InteropServices;
#endif
using NUnit.Framework;
using static RyuJitSharp.Globals;
#if DEBUG
using static RyuJitSharp.var_types;
#endif

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ValueNumConstantExtractionTests
{
    private static int s_assertions;
    private static string? s_lastAssertion;

    [TestCase(-1L)]
    [TestCase(long.MinValue)]
    [TestCase(long.MaxValue)]
    [TestCase(0x100000001L)]
    public static void IntegralCoercionPreservesNativeTruncationAndSignExtension(long value)
    {
        WithStore(store =>
        {
            var wide = store.VNForLongCon(value);
            var narrowValue = unchecked((int)value);
            var narrow = store.VNForIntCon(narrowValue);

            Assert.That(store.CoercedConstantValue<int>(wide), Is.EqualTo(narrowValue));
            Assert.That(store.CoercedConstantValue<uint>(wide), Is.EqualTo(unchecked((uint)value)));
            Assert.That(store.CoercedConstantValue<long>(narrow), Is.EqualTo((long)narrowValue));
            Assert.That(store.CoercedConstantValue<ulong>(narrow), Is.EqualTo(unchecked((ulong)narrowValue)));
            Assert.That(store.ConstantValue<uint>(narrow), Is.EqualTo(unchecked((uint)narrowValue)));
            Assert.That(s_assertions, Is.Zero, s_lastAssertion);
        });
    }

    [TestCase(GTF_ICON_CLASS_HDL)]
    [TestCase(GTF_ICON_OBJ_HDL)]
    public static void HandleExtractionPreservesBothPointerSignednesses(GenTreeFlags flags)
    {
        WithStore(store =>
        {
            var value = unchecked((nint)0x81234567ABCDEF12UL);
            var handle = store.VNForHandle(value, flags);

            Assert.That(store.ConstantValue<nint>(handle), Is.EqualTo(value));
            Assert.That(store.ConstantValue<nuint>(handle), Is.EqualTo(unchecked((nuint)value)));
            Assert.That(store.CoercedConstantValue<nint>(handle), Is.EqualTo(value));
            Assert.That(store.CoercedConstantValue<nuint>(handle), Is.EqualTo(unchecked((nuint)value)));
            Assert.That(store.GetHandleFlags(handle), Is.EqualTo(flags));
            if (nuint.Size == 8)
            {
                Assert.That(store.ConstantValue<long>(handle), Is.EqualTo((long)value));
                Assert.That(store.ConstantValue<ulong>(handle), Is.EqualTo(unchecked((ulong)value)));
                Assert.That(store.CoercedConstantValue<long>(handle), Is.EqualTo((long)value));
                Assert.That(store.CoercedConstantValue<ulong>(handle), Is.EqualTo(unchecked((ulong)value)));
            }
            else
            {
                Assert.That(store.ConstantValue<int>(handle), Is.EqualTo((int)value));
                Assert.That(store.ConstantValue<uint>(handle), Is.EqualTo(unchecked((uint)value)));
                Assert.That(store.CoercedConstantValue<int>(handle), Is.EqualTo((int)value));
                Assert.That(store.CoercedConstantValue<uint>(handle), Is.EqualTo(unchecked((uint)value)));
            }

            Assert.That(s_assertions, Is.Zero, s_lastAssertion);

            Assert.That(store.ConstantValue<int>(handle), Is.EqualTo(unchecked((int)value)));
#if DEBUG && HOST_WINDOWS
            Assert.That(s_assertions,
                Is.EqualTo((flags == GTF_ICON_OBJ_HDL) && (nuint.Size != sizeof(int)) ? 1 : 0),
                s_lastAssertion);
#else
            Assert.That(s_assertions, Is.Zero, s_lastAssertion);
#endif
        });
    }

    [Test]
    public static void ObjectHandlesBeyondReservedReferenceOffsetsRemainExtractable()
    {
        WithStore(store =>
        {
            for (var index = 0; index < 4; index++)
            {
                var value = (nint)(0x1234 + index);
                var handle = store.VNForHandle(value, GTF_ICON_OBJ_HDL);
                Assert.That(store.ConstantValue<nint>(handle), Is.EqualTo(value));
            }

            Assert.That(s_assertions, Is.Zero, s_lastAssertion);
        });
    }

    [Test]
    public static void NonHandlePointersRetainNullAndHighAddressBits()
    {
        WithStore(store =>
        {
            Assert.That(store.ConstantValue<nuint>(ValueNumStore.VNForNull()), Is.EqualTo((nuint)0));
            Assert.That(store.CoercedConstantValue<nuint>(ValueNumStore.VNForNull()), Is.EqualTo((nuint)0));

            var value = unchecked((nuint)0x81234567ABCDEF12UL);
            var byref = store.VNForByrefCon(value);
            Assert.That(store.ConstantValue<nuint>(byref), Is.EqualTo(value));
            Assert.That(store.ConstantValue<nint>(byref), Is.EqualTo(unchecked((nint)value)));
            Assert.That(store.CoercedConstantValue<nint>(byref), Is.EqualTo(unchecked((nint)value)));
            Assert.That(s_assertions, Is.Zero, s_lastAssertion);
        });
    }

    [Test]
    public static void PointerAliasesUseHostStorageWidthRatherThanTheSelectedTarget()
    {
        WithStore(store =>
        {
            var value = unchecked((nuint)0x81234567ABCDEF12UL);
            var byref = store.VNForByrefCon(value);
            if (nuint.Size == 8)
            {
                Assert.That(store.ConstantValue<ulong>(ValueNumStore.VNForNull()), Is.Zero);
                Assert.That(store.CoercedConstantValue<ulong>(ValueNumStore.VNForNull()), Is.Zero);
                Assert.That(store.ConstantValue<ulong>(byref), Is.EqualTo((ulong)value));
                Assert.That(store.ConstantValue<long>(byref), Is.EqualTo(unchecked((long)value)));
                Assert.That(store.CoercedConstantValue<ulong>(byref), Is.EqualTo((ulong)value));
                Assert.That(store.CoercedConstantValue<long>(byref), Is.EqualTo(unchecked((long)value)));
            }
            else
            {
                Assert.That(store.ConstantValue<uint>(ValueNumStore.VNForNull()), Is.Zero);
                Assert.That(store.CoercedConstantValue<uint>(ValueNumStore.VNForNull()), Is.Zero);
                Assert.That(store.ConstantValue<uint>(byref), Is.EqualTo((uint)value));
                Assert.That(store.ConstantValue<int>(byref), Is.EqualTo(unchecked((int)value)));
                Assert.That(store.CoercedConstantValue<uint>(byref), Is.EqualTo((uint)value));
                Assert.That(store.CoercedConstantValue<int>(byref), Is.EqualTo(unchecked((int)value)));
            }

            Assert.That(s_assertions, Is.Zero, s_lastAssertion);
        });
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public static void NonHandleReferenceRejectsSignedPointerExtraction(bool coerce, bool fixedWidth)
    {
        var previous = EnableNoWayAssert(ref JitConfig);
        try
        {
            EnableNoWayAssert(ref JitConfig) = 0;
            WithStore(store =>
            {
                var error = Assert.Throws<FatalJitException>(() =>
                {
                    if (fixedWidth)
                    {
                        if (nuint.Size == 8)
                        {
                            _ = coerce
                                ? store.CoercedConstantValue<long>(ValueNumStore.VNForNull())
                                : store.ConstantValue<long>(ValueNumStore.VNForNull());
                        }
                        else
                        {
                            _ = coerce
                                ? store.CoercedConstantValue<int>(ValueNumStore.VNForNull())
                                : store.ConstantValue<int>(ValueNumStore.VNForNull());
                        }
                    }
                    else if (coerce)
                    {
                        _ = store.CoercedConstantValue<nint>(ValueNumStore.VNForNull());
                    }
                    else
                    {
                        _ = store.ConstantValue<nint>(ValueNumStore.VNForNull());
                    }
                });

                Assert.That(error, Has.Property(nameof(FatalJitException.Result)).EqualTo(CorJitResult.CORJIT_RECOVERABLEERROR));
                Assert.That(s_assertions, Is.Zero, s_lastAssertion);
            });
        }
        finally
        {
            EnableNoWayAssert(ref JitConfig) = previous;
        }
    }

    [TestCase(true, 0L)]
    [TestCase(true, 0x80000000L)]
    [TestCase(true, 0x7FC01234L)]
    [TestCase(true, 0xFFC05678L)]
    [TestCase(true, 0x7F801234L)]
    [TestCase(false, 0L)]
    [TestCase(false, long.MinValue)]
    [TestCase(false, 0x7FF8000000001234L)]
    [TestCase(false, unchecked((long)0xFFF8000000005678UL))]
    [TestCase(false, 0x7FF0000000001234L)]
    public static void StrictFloatingExtractionComparesStorageBitsWithoutChangingThem(bool single, long bits)
    {
        WithStore(store =>
        {
            if (single)
            {
                var value = BitConverter.UInt32BitsToSingle(unchecked((uint)bits));
                var vn = store.VNForFloatCon(value);
                Assert.That(BitConverter.SingleToUInt32Bits(store.ConstantValue<float>(vn)),
                    Is.EqualTo(unchecked((uint)bits)));
                Assert.That(BitConverter.SingleToUInt32Bits(store.CoercedConstantValue<float>(vn)),
                    Is.EqualTo(unchecked((uint)bits)));
            }
            else
            {
                var value = BitConverter.Int64BitsToDouble(bits);
                var vn = store.VNForDoubleCon(value);
                Assert.That(BitConverter.DoubleToInt64Bits(store.ConstantValue<double>(vn)), Is.EqualTo(bits));
                Assert.That(BitConverter.DoubleToInt64Bits(store.CoercedConstantValue<double>(vn)), Is.EqualTo(bits));
            }

            Assert.That(s_assertions, Is.Zero, s_lastAssertion);
        });
    }

    [TestCase(false, 1.75, 1)]
    [TestCase(false, -1.75, -1)]
    [TestCase(true, 1.75, 1)]
    [TestCase(true, -1.75, -1)]
    public static void RepresentableFloatingCoercionTruncatesTowardZero(bool single, double value, int expected)
    {
        WithStore(store =>
        {
            var vn = single ? store.VNForFloatCon((float)value) : store.VNForDoubleCon(value);

            Assert.That(store.CoercedConstantValue<int>(vn), Is.EqualTo(expected));
            Assert.That(store.CoercedConstantValue<long>(vn), Is.EqualTo((long)expected));
            Assert.That(s_assertions, Is.Zero, s_lastAssertion);
        });
    }

#if DEBUG
#if HOST_WINDOWS
    [Test]
    public static void PointerDestinationDiagnosticRejectsSameSizedFloatingTypes()
    {
        WithStore(store =>
        {
            var byref = store.VNForByrefCon(1);
            if (nuint.Size == 8)
            {
                Assert.That(store.CoercedConstantValue<double>(byref), Is.EqualTo(1.0));
            }
            else
            {
                Assert.That(store.CoercedConstantValue<float>(byref), Is.EqualTo(1.0f));
            }

            Assert.That(s_assertions, Is.EqualTo(1), s_lastAssertion);
            Assert.That(s_lastAssertion, Does.Contain("typeof(T) == typeof(nuint)"));
        });
    }
#endif

    [Test]
    public static void StrictStorageWidthAssertionNeverReadsBeyondAnElementWhenTheHandlerContinues()
    {
        WithStore(store =>
        {
            var vn = store.VNForFloatCon(1.75f);
            Assert.That(store.CoercedConstantValue<double>(vn), Is.EqualTo(1.75));
            Assert.That(s_assertions, Is.Zero);
            Assert.That(store.ConstantValue<double>(vn), Is.EqualTo(1.75));
            Assert.That(s_assertions, Is.EqualTo(1), s_lastAssertion);
            Assert.That(s_lastAssertion, Does.Contain("Unsafe.SizeOf<T>() == storage.Length"));
        });
    }

    [TestCase(TYP_INT, 0L, 0)]
    [TestCase(TYP_INT, 1L, 1)]
    [TestCase(TYP_LONG, 0L, 0)]
    [TestCase(TYP_LONG, 1L, 1)]
    [TestCase(TYP_FLOAT, 0L, 0)]
    [TestCase(TYP_FLOAT, 0x80000000L, 1)]
    [TestCase(TYP_FLOAT, 0x3F800000L, 1)]
    [TestCase(TYP_DOUBLE, 0L, 0)]
    [TestCase(TYP_DOUBLE, long.MinValue, 1)]
    [TestCase(TYP_DOUBLE, 0x3FF0000000000000L, 1)]
    public static void StrictDiagnosticsCompareReinterpretationWithCoercion(
        var_types type, long bits, int expectedAssertions)
    {
        WithStore(store =>
        {
            switch (type)
            {
                case TYP_INT:
                {
                    var vn = store.VNForIntCon(unchecked((int)bits));
                    var coerced = store.CoercedConstantValue<float>(vn);
                    Assert.That(s_assertions, Is.Zero);
                    Assert.That(store.ConstantValue<float>(vn), Is.EqualTo(coerced));
                    break;
                }
                case TYP_LONG:
                {
                    var vn = store.VNForLongCon(bits);
                    var coerced = store.CoercedConstantValue<double>(vn);
                    Assert.That(s_assertions, Is.Zero);
                    Assert.That(store.ConstantValue<double>(vn), Is.EqualTo(coerced));
                    break;
                }
                case TYP_FLOAT:
                {
                    var vn = store.VNForFloatCon(BitConverter.UInt32BitsToSingle(unchecked((uint)bits)));
                    var coerced = store.CoercedConstantValue<int>(vn);
                    Assert.That(s_assertions, Is.Zero);
                    Assert.That(store.ConstantValue<int>(vn), Is.EqualTo(coerced));
                    break;
                }
                case TYP_DOUBLE:
                {
                    var vn = store.VNForDoubleCon(BitConverter.Int64BitsToDouble(bits));
                    var coerced = store.CoercedConstantValue<long>(vn);
                    Assert.That(s_assertions, Is.Zero);
                    Assert.That(store.ConstantValue<long>(vn), Is.EqualTo(coerced));
                    break;
                }
                default:
                {
                    throw new ArgumentOutOfRangeException(nameof(type));
                }
            }

            Assert.That(s_assertions, Is.EqualTo(expectedAssertions), s_lastAssertion);
            if (expectedAssertions != 0)
            {
                Assert.That(s_lastAssertion, Does.Contain("Use CoercedConstantValue instead"));
            }
        });
    }
#endif

    private static void WithStore(Action<ValueNumStore> action)
    {
#if DEBUG
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        using var tls = new JitTls(&ee);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
#if DEBUG
        compiler.info.compFullName = nameof(ValueNumConstantExtractionTests);
#endif
        JitTls.Compiler = compiler;
        s_assertions = 0;
        s_lastAssertion = null;
        try
        {
            action(new ValueNumStore(compiler));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

#if DEBUG
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertions++;
        s_lastAssertion = Marshal.PtrToStringUTF8((nint)expression);
        return 0;
    }
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitEnableNoWayAssert")]
    private static extern ref int EnableNoWayAssert(ref JitConfigValues config);
}
