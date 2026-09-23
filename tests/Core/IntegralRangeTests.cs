// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.SymbolicIntegerValue;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class IntegralRangeTests
{
    [Test]
    public static void SymbolicBoundsPreserveOrderingAndInclusiveContainment()
    {
        var values = Enum.GetValues<SymbolicIntegerValue>();
        Assert.That(values, Has.Length.EqualTo(14));

        for (var i = 0; i < values.Length; i++)
        {
            var lower = IntegralRange.SymbolicToRealValue(values[i]);

            if (i != 0)
            {
                Assert.That(lower, Is.GreaterThan(IntegralRange.SymbolicToRealValue(values[i - 1])));
            }

            for (var j = i; j < values.Length; j++)
            {
                var upper = IntegralRange.SymbolicToRealValue(values[j]);
                var range = new IntegralRange(values[i], values[j]);
                Assert.That(range.Contains(lower), Is.True);
                Assert.That(range.Contains(upper), Is.True);
                Assert.That(range.Contains(new IntegralRange(values[i], values[i])), Is.True);
                Assert.That(range.IsNonNegative, Is.EqualTo(lower >= 0));
                Assert.That(IntegralRange.Union(range, new(values[j], values[j])), Is.EqualTo(range));

                if (lower != long.MinValue)
                {
                    Assert.That(range.Contains(lower - 1), Is.False);
                }

                if (upper != long.MaxValue)
                {
                    Assert.That(range.Contains(upper + 1), Is.False);
                }
            }
        }
    }

    [TestCase(TYP_BYTE, ByteMin, ByteMax)]
    [TestCase(TYP_UBYTE, Zero, UByteMax)]
    [TestCase(TYP_SHORT, ShortMin, ShortMax)]
    [TestCase(TYP_USHORT, Zero, UShortMax)]
    [TestCase(TYP_INT, IntMin, IntMax)]
    [TestCase(TYP_LONG, LongMin, LongMax)]
    public static void TypeBoundsMatchSignedDomain(var_types type, SymbolicIntegerValue lower, SymbolicIntegerValue upper)
    {
        Assert.That(IntegralRange.ForType(type), Is.EqualTo(new IntegralRange(lower, upper)));
    }

    [TestCase(TYP_INT, false)]
    [TestCase(TYP_INT, true)]
    [TestCase(TYP_LONG, false)]
    [TestCase(TYP_LONG, true)]
    public static void CheckedCastInputMatchesMathematicalOverflow(var_types from, bool unsigned)
    {
        WithCompiler(compiler => {
            ReadOnlySpan<var_types> destinations = [TYP_BYTE, TYP_UBYTE, TYP_SHORT, TYP_USHORT, TYP_INT, TYP_UINT, TYP_LONG, TYP_ULONG];
            ReadOnlySpan<long> samples = [
                long.MinValue, long.MinValue + 1, int.MinValue - 1L, int.MinValue,
                short.MinValue - 1L, short.MinValue, -129, -128, -1, 0, 1, 127, 128,
                255, 256, short.MaxValue, short.MaxValue + 1L, ushort.MaxValue, ushort.MaxValue + 1L,
                int.MaxValue, int.MaxValue + 1L, uint.MaxValue, uint.MaxValue + 1L, long.MaxValue,
            ];

            foreach (var to in destinations)
            {
                var cast = compiler.gtNewCastNode(to.ActualType, compiler.gtNewLclvNode(from, 0), unsigned, to);
                cast.Flags |= GTF_OVERFLOW;
                var range = IntegralRange.ForCastInput(cast);
                var bits = to.Size * 8;
                var minimum = varTypeIsUnsigned(to) ? BigInteger.Zero : -(BigInteger.One << (bits - 1));
                var maximum = (BigInteger.One << (bits - (varTypeIsUnsigned(to) ? 0 : 1))) - 1;

                foreach (var sample in samples)
                {
                    var signedInput = from is TYP_INT ? unchecked((int)sample) : sample;
                    BigInteger input = !unsigned ? signedInput
                        : from is TYP_INT ? unchecked((uint)signedInput) : unchecked((ulong)signedInput);
                    Assert.That(range.Contains(signedInput), Is.EqualTo((minimum <= input) && (input <= maximum)),
                        $"{from}, unsigned={unsigned}, to={to}, input={signedInput}");
                }
            }
        });
    }

    [TestCase(TYP_INT, TYP_BYTE, ByteMin, ByteMax)]
    [TestCase(TYP_LONG, TYP_USHORT, Zero, UShortMax)]
    [TestCase(TYP_LONG, TYP_INT, LongMin, LongMax)]
    [TestCase(TYP_INT, TYP_ULONG, IntMin, IntMax)]
    [TestCase(TYP_LONG, TYP_ULONG, LongMin, LongMax)]
    public static void UncheckedInputRetainsNativeRepresentationRules(var_types from, var_types to,
        SymbolicIntegerValue lower, SymbolicIntegerValue upper)
    {
        WithCompiler(compiler => {
            var cast = compiler.gtNewCastNode(to.ActualType, compiler.gtNewLclvNode(from, 0), true, to);
            Assert.That(IntegralRange.ForCastInput(cast), Is.EqualTo(new IntegralRange(lower, upper)));
        });
    }

    [TestCase(TYP_INT, TYP_LONG, false, false, IntMin, IntMax)]
    [TestCase(TYP_INT, TYP_LONG, true, false, Zero, UIntMax)]
    [TestCase(TYP_INT, TYP_LONG, true, true, Zero, UIntMax)]
    [TestCase(TYP_INT, TYP_ULONG, false, true, Zero, IntMax)]
    [TestCase(TYP_LONG, TYP_UINT, false, true, IntMin, IntMax)]
    [TestCase(TYP_LONG, TYP_INT, true, true, Zero, IntMax)]
    [TestCase(TYP_LONG, TYP_INT, false, false, IntMin, IntMax)]
    [TestCase(TYP_LONG, TYP_UBYTE, true, true, Zero, UByteMax)]
    [TestCase(TYP_DOUBLE, TYP_ULONG, false, false, LongMin, LongMax)]
    [TestCase(TYP_FLOAT, TYP_BYTE, false, true, ByteMin, ByteMax)]
    [TestCase(TYP_BYREF, TYP_INT, false, true, IntMin, IntMax)]
    public static void CastOutputsAccountForWidthAndUnsignedInterpretation(var_types from, var_types to,
        bool unsigned, bool overflow, SymbolicIntegerValue lower, SymbolicIntegerValue upper)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = from;
            var cast = compiler.gtNewCastNode(to.ActualType, compiler.gtNewLclvNode(from, 0), unsigned, to);
            cast.Flags |= overflow ? GTF_OVERFLOW : GTF_EMPTY;
            Assert.That(IntegralRange.ForCastOutput(cast, compiler), Is.EqualTo(new IntegralRange(lower, upper)));
        });
    }

    [TestCase(0L, Zero, One)]
    [TestCase(1L, Zero, One)]
    [TestCase(-1L, IntMin, IntMax)]
    [TestCase(2L, Zero, IntMax)]
    [TestCase(2147483648L, Zero, UIntMax)]
    [TestCase(4294967296L, Zero, LongMax)]
    [TestCase(long.MinValue, LongMin, LongMax)]
    public static void ConstantsUseNativeCoarseBounds(long value, SymbolicIntegerValue lower, SymbolicIntegerValue upper)
    {
        WithCompiler(compiler => Assert.That(
            IntegralRange.ForNode(compiler.gtNewLconNode(value), compiler), Is.EqualTo(new IntegralRange(lower, upper))));
    }

    [Test]
    public static void BooleanAndMaskRangesFeedWideningCasts()
    {
        WithCompiler(compiler => {
            var compare = compiler.gtNewBinaryNode(GT_LT, TYP_INT,
                compiler.gtNewLclvNode(TYP_INT, 0), compiler.gtNewIconNode(TYP_INT, 0));
            var mask = compiler.gtNewBinaryNode(GT_AND, TYP_INT, compare, compiler.gtNewIconNode(TYP_INT, 255));
            Assert.That(IntegralRange.ForNode(mask, compiler), Is.EqualTo(new IntegralRange(Zero, One)));
            Assert.That(mask.IsNeverNegative(compiler), Is.True);

            var cast = compiler.gtNewCastNode(TYP_LONG, mask, false, TYP_LONG);
            Assert.That(IntegralRange.ForNode(cast, compiler), Is.EqualTo(new IntegralRange(Zero, UIntMax)));

            mask.Op1 = compiler.gtNewLclvNode(TYP_INT, 0);
            Assert.That(IntegralRange.ForNode(mask, compiler), Is.EqualTo(new IntegralRange(Zero, IntMax)));
            mask.Op2 = compiler.gtNewIconNode(TYP_INT, -1);
            Assert.That(IntegralRange.ForNode(mask, compiler), Is.EqualTo(new IntegralRange(IntMin, IntMax)));
        });
    }

    [Test]
    public static void ConditionalRangesUnionBothArms()
    {
        WithCompiler(compiler => {
            var colon = new GenTreeColon(TYP_LONG, compiler.gtNewLconNode(-1), compiler.gtNewLconNode(uint.MaxValue));
            var qmark = new GenTreeQmark(TYP_LONG, compiler.gtNewIconNode(TYP_INT, 1), colon);
            Assert.That(IntegralRange.ForNode(qmark, compiler), Is.EqualTo(new IntegralRange(IntMin, UIntMax)));
        });
    }

    [Test]
    public static void LocalsRespectStoreNormalizationAndNonnegativeMetadata()
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_UBYTE;
            GenTree local = compiler.gtNewLclvNode(TYP_INT, 0);
            Assert.That(IntegralRange.ForNode(local, compiler), Is.EqualTo(new IntegralRange(Zero, UByteMax)));
            Assert.That(local.IsNeverNegative(compiler), Is.True);
            compiler.lvaTable[0].lvIsParam = true;
            Assert.That(IntegralRange.ForNode(local, compiler), Is.EqualTo(new IntegralRange(IntMin, IntMax)));
            compiler.lvaTable[0].IsNeverNegative = true;
            Assert.That(IntegralRange.ForNode(local, compiler), Is.EqualTo(new IntegralRange(Zero, IntMax)));
        });
    }

    [Test]
    public static void ArrayAndSpanLengthsAreNonnegative()
    {
        WithCompiler(compiler => {
            var array = compiler.gtNewLclvNode(TYP_REF, 0);
            var length = new GenTreeArrLen(TYP_INT, array, 8);
            var mdLength = new GenTreeMDArr(GT_MDARR_LENGTH, array, 0, 2);
            Assert.That(IntegralRange.ForNode(length, compiler), Is.EqualTo(new IntegralRange(Zero, ArrayLenMax)));
            Assert.That(IntegralRange.ForNode(mdLength, compiler), Is.EqualTo(new IntegralRange(Zero, ArrayLenMax)));

            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].IsSpan = true;
            var field = new GenTreeLclFld(GT_LCL_FLD, TYP_INT, 0, (ushort)OFFSETOF__CORINFO_Span__length);
            Assert.That(IntegralRange.ForNode(field, compiler), Is.EqualTo(new IntegralRange(Zero, IntMax)));
            field.LclOffs = 0;
            Assert.That(IntegralRange.ForNode(field, compiler), Is.EqualTo(new IntegralRange(IntMin, IntMax)));

            compiler.lvaTable[0].Type = TYP_BYREF;
            compiler.lvaTable[0].lvIsParam = true;
            compiler.lvaTable[0].IsImplicitByRef = true;
            var address = compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF,
                compiler.gtNewLclvNode(TYP_BYREF, 0), compiler.gtNewIconNode(TYP_I_IMPL, OFFSETOF__CORINFO_Span__length));
            var indir = new GenTreeIndir(GT_IND, TYP_INT, address);
            Assert.That(IntegralRange.ForNode(indir, compiler), Is.EqualTo(new IntegralRange(Zero, IntMax)));
        });
    }

    [Test]
    public static void ManagedCallResultsRespectSmallReturnTypes()
    {
        WithCompiler(compiler => {
            var call = compiler.gtNewCallNode(TYP_INT, gtCallTypes.CT_USER_FUNC, null);
            call._returnType = TYP_UBYTE;
            Assert.That(IntegralRange.ForNode(call, compiler), Is.EqualTo(new IntegralRange(Zero, UByteMax)));
        });
    }

    [TestCase(NI_PRIMITIVE_PopCount, Zero, ByteMax)]
    [TestCase(NI_PRIMITIVE_LeadingZeroCount, Zero, ByteMax)]
    [TestCase(NI_PRIMITIVE_TrailingZeroCount, Zero, ByteMax)]
    [TestCase(NI_PRIMITIVE_SaturateToInt8, ByteMin, ByteMax)]
    [TestCase(NI_PRIMITIVE_SaturateToInt16, ShortMin, ShortMax)]
    [TestCase(NI_PRIMITIVE_SaturateToUInt8, Zero, UByteMax)]
    [TestCase(NI_PRIMITIVE_SaturateToUInt16, Zero, UShortMax)]
    [TestCase(NI_System_Runtime_CompilerServices_RuntimeHelpers_IsKnownConstant, Zero, One)]
    public static void PrimitiveIntrinsicRangesMatchNative(NamedIntrinsic id, SymbolicIntegerValue lower, SymbolicIntegerValue upper)
    {
        WithCompiler(compiler => {
            var node = new GenTreeIntrinsic(TYP_INT, compiler.gtNewLclvNode(TYP_INT, 0), id, null);
            Assert.That(IntegralRange.ForNode(node, compiler), Is.EqualTo(new IntegralRange(lower, upper)));
        });
    }

    [TestCase(TYP_UBYTE, 8, TYP_INT, Zero, UByteMax)]
    [TestCase(TYP_UBYTE, 16, TYP_INT, Zero, UShortMax)]
    [TestCase(TYP_UBYTE, 32, TYP_LONG, Zero, UIntMax)]
    [TestCase(TYP_UBYTE, 32, TYP_INT, IntMin, IntMax)]
    public static void VectorMaskRangesCountBitsNotBytes(var_types baseType, byte size, var_types resultType,
        SymbolicIntegerValue lower, SymbolicIntegerValue upper)
    {
        WithCompiler(compiler => {
            var vectorType = size switch {
                8 => TYP_SIMD8,
                16 => TYP_SIMD16,
                32 => TYP_SIMD32,
                _ => throw new InvalidOperationException(),
            };
            compiler.lvaTable[0].Type = vectorType;
            var node = new GenTreeHWIntrinsic(resultType, NI_Vector_ExtractMostSignificantBits, baseType, size,
                compiler.gtNewLclvNode(vectorType, 0));
            Assert.That(IntegralRange.ForNode(node, compiler), Is.EqualTo(new IntegralRange(lower, upper)));
        });
    }

    [Test]
    public static void HardwareBooleanScalarAndBitCountRangesMatchNative()
    {
        WithCompiler(compiler => {
            var compare = new GenTreeHWIntrinsic(TYP_INT, NI_X86Base_CompareScalarOrderedEqual, TYP_DOUBLE, 16,
                new GenTreeLclVar(TYP_SIMD16, 0), new GenTreeLclVar(TYP_SIMD16, 0));
            var scalar = new GenTreeHWIntrinsic(TYP_INT, NI_Vector_ToScalar, TYP_UBYTE, 16,
                new GenTreeLclVar(TYP_SIMD16, 0));
            var count = new GenTreeHWIntrinsic(TYP_LONG, NI_X86Base_X64_PopCount, TYP_LONG, 0,
                new GenTreeLclVar(TYP_LONG, 0));
            Assert.That(IntegralRange.ForNode(compare, compiler), Is.EqualTo(new IntegralRange(Zero, One)));
            Assert.That(IntegralRange.ForNode(scalar, compiler), Is.EqualTo(new IntegralRange(Zero, UByteMax)));
            Assert.That(IntegralRange.ForNode(count, compiler), Is.EqualTo(new IntegralRange(Zero, ByteMax)));
        });
    }

    [Test]
    public static void ValueNumberAnalysisFailsExplicitlyWhenRequired()
    {
        WithCompiler(compiler => {
            compiler.vnStore = new ValueNumStore(compiler);
            var unknown = compiler.gtNewUnaryNode(GT_NEG, TYP_INT, compiler.gtNewLclvNode(TYP_INT, 0));
            _ = Assert.Throws<NotImplementedException>(() => unknown.IsNeverNegative(compiler));
            Assert.That(compiler.gtNewIconNode(TYP_INT, -1).IsNeverNegative(compiler), Is.False);
            Assert.That(compiler.gtNewIconNode(TYP_INT, 1).IsNeverNegative(compiler), Is.True);
        });
    }

#if DEBUG
    [Test]
    public static void RangeDumpMatchesNativeBounds()
    {
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previous = s_jitstdout;

        try
        {
            s_jitstdout = writer;
            IntegralRange.Print(new(LongMin, UIntMax));
            writer.Flush();
        }
        finally
        {
            s_jitstdout = previous;
        }

        Assert.That(Encoding.UTF8.GetString(stream.ToArray()), Is.EqualTo("[-9223372036854775808..4294967295]"));
    }
#endif

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
        compiler.lvaTable = [new LclVarDsc { Type = TYP_INT }];
        compiler.lvaCount = 1;
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
}
