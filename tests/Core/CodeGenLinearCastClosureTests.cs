// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CodeGen.GenIntCastDesc.CheckKind;
using static RyuJitSharp.CodeGen.GenIntCastDesc.ExtendKind;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenLinearCastClosureTests
{
    [Test]
    public static void DescriptorKindsPreserveNativeTargetValues()
    {
        Assert.That((int)CHECK_NONE, Is.Zero);
        Assert.That((int)CHECK_SMALL_INT_RANGE, Is.EqualTo(1));
        Assert.That((int)CHECK_POSITIVE, Is.EqualTo(2));
        Assert.That((int)COPY, Is.Zero);
        Assert.That((int)ZERO_EXTEND_SMALL_INT, Is.EqualTo(1));
        Assert.That((int)SIGN_EXTEND_SMALL_INT, Is.EqualTo(2));

#if TARGET_64BIT || TARGET_WASM
        Assert.That(Enum.GetValues<CodeGen.GenIntCastDesc.CheckKind>(), Has.Length.EqualTo(6));
        Assert.That(Enum.GetValues<CodeGen.GenIntCastDesc.ExtendKind>(), Has.Length.EqualTo(10));
        Assert.That((int)CHECK_UINT_RANGE, Is.EqualTo(3));
        Assert.That((int)CHECK_POSITIVE_INT_RANGE, Is.EqualTo(4));
        Assert.That((int)CHECK_INT_RANGE, Is.EqualTo(5));
        Assert.That((int)ZERO_EXTEND_INT, Is.EqualTo(3));
        Assert.That((int)SIGN_EXTEND_INT, Is.EqualTo(4));
        Assert.That((int)LOAD_ZERO_EXTEND_SMALL_INT, Is.EqualTo(5));
        Assert.That((int)LOAD_SIGN_EXTEND_SMALL_INT, Is.EqualTo(6));
        Assert.That((int)LOAD_ZERO_EXTEND_INT, Is.EqualTo(7));
        Assert.That((int)LOAD_SIGN_EXTEND_INT, Is.EqualTo(8));
        Assert.That((int)LOAD_SOURCE, Is.EqualTo(9));
#else
        Assert.That(Enum.GetValues<CodeGen.GenIntCastDesc.CheckKind>(), Has.Length.EqualTo(3));
        Assert.That(Enum.GetValues<CodeGen.GenIntCastDesc.ExtendKind>(), Has.Length.EqualTo(6));
        Assert.That((int)LOAD_ZERO_EXTEND_SMALL_INT, Is.EqualTo(3));
        Assert.That((int)LOAD_SIGN_EXTEND_SMALL_INT, Is.EqualTo(4));
        Assert.That((int)LOAD_SOURCE, Is.EqualTo(5));
#endif
    }

    [TestCase(TYP_INT, false, TYP_BYTE, -128, 127)]
    [TestCase(TYP_INT, true, TYP_BYTE, 0, 127)]
    [TestCase(TYP_INT, false, TYP_UBYTE, 0, 255)]
    [TestCase(TYP_INT, false, TYP_SHORT, -32768, 32767)]
    [TestCase(TYP_INT, true, TYP_SHORT, 0, 32767)]
    [TestCase(TYP_INT, false, TYP_USHORT, 0, 65535)]
    public static void CheckedSmallMemoryCastsRetainExactRangeAndLoadActualType(
        var_types sourceType, bool sourceUnsigned, var_types castType, int expectedMin, int expectedMax)
    {
        WithCastCompiler(() =>
        {
            var source = new GenTreeLclVar(sourceType, 0) { IsContained = true };
            var cast = new GenTreeCast(castType.ActualType, source, sourceUnsigned, castType);
            cast.Flags |= GTF_OVERFLOW;

            var desc = new CodeGen.GenIntCastDesc(cast);

            Assert.That(desc.Check, Is.EqualTo(CHECK_SMALL_INT_RANGE));
            Assert.That(desc.CheckSrcSize, Is.EqualTo(4));
            Assert.That(desc.CheckSmallIntMin, Is.EqualTo(expectedMin));
            Assert.That(desc.CheckSmallIntMax, Is.EqualTo(expectedMax));
            Assert.That(desc.Extend, Is.EqualTo(LOAD_SOURCE));
            Assert.That(desc.ExtendSrcSize, Is.Zero);
        });
    }

#if TARGET_64BIT
    [TestCase(TYP_INT, false, TYP_ULONG, true, CHECK_POSITIVE, ZERO_EXTEND_INT, 4)]
    [TestCase(TYP_INT, false, TYP_LONG, false, CHECK_NONE, SIGN_EXTEND_INT, 4)]
    [TestCase(TYP_UINT, true, TYP_LONG, false, CHECK_NONE, ZERO_EXTEND_INT, 4)]
    [TestCase(TYP_LONG, false, TYP_ULONG, true, CHECK_POSITIVE, COPY, 8)]
    public static void WideRegisterCastsPreserveSignednessAndOverflow(
        var_types sourceType, bool sourceUnsigned, var_types castType, bool overflow,
        CodeGen.GenIntCastDesc.CheckKind expectedCheck, CodeGen.GenIntCastDesc.ExtendKind expectedExtend,
        int expectedSize)
    {
        WithCastCompiler(() =>
        {
            var source = new GenTreeLclVar(sourceType, 0);
            var cast = new GenTreeCast(castType.ActualType, source, sourceUnsigned, castType);
            if (overflow)
            {
                cast.Flags |= GTF_OVERFLOW;
            }

            var desc = new CodeGen.GenIntCastDesc(cast);
            Assert.That(desc.Check, Is.EqualTo(expectedCheck));
            Assert.That(desc.Extend, Is.EqualTo(expectedExtend));
            Assert.That(desc.ExtendSrcSize, Is.EqualTo(expectedSize));
            if (expectedCheck != CHECK_NONE)
            {
                Assert.That(desc.CheckSrcSize, Is.EqualTo(sourceType.Size));
            }
        });
    }

#if TARGET_LOONGARCH64 || TARGET_RISCV64
    [TestCase(TYP_LONG, false, TYP_INT, true, CHECK_INT_RANGE)]
    [TestCase(TYP_LONG, true, TYP_INT, true, CHECK_POSITIVE_INT_RANGE)]
    [TestCase(TYP_LONG, false, TYP_UINT, true, CHECK_UINT_RANGE)]
    [TestCase(TYP_LONG, false, TYP_UINT, false, CHECK_NONE)]
    public static void NarrowingMaintainsRequiredAbiSignExtension(
        var_types sourceType, bool sourceUnsigned, var_types castType,
        bool overflow, CodeGen.GenIntCastDesc.CheckKind expectedCheck)
    {
        WithCastCompiler(() =>
        {
            var source = new GenTreeLclVar(sourceType, 0);
            var cast = new GenTreeCast(castType.ActualType, source, sourceUnsigned, castType);
            if (overflow)
            {
                cast.Flags |= GTF_OVERFLOW;
            }

            var desc = new CodeGen.GenIntCastDesc(cast);
            Assert.That(desc.Check, Is.EqualTo(expectedCheck));
            Assert.That(desc.Extend, Is.EqualTo(SIGN_EXTEND_INT));
            Assert.That(desc.ExtendSrcSize, Is.EqualTo(4));
            if (expectedCheck != CHECK_NONE)
            {
                Assert.That(desc.CheckSrcSize, Is.EqualTo(8));
            }
        });
    }
#endif
#endif

#if TARGET_WASM
    [TestCase(TYP_INT, false, TYP_LONG, false, CHECK_NONE, SIGN_EXTEND_INT, 4)]
    [TestCase(TYP_UINT, true, TYP_ULONG, false, CHECK_NONE, ZERO_EXTEND_INT, 4)]
    [TestCase(TYP_LONG, false, TYP_INT, true, CHECK_INT_RANGE, COPY, 4)]
    [TestCase(TYP_LONG, false, TYP_UINT, true, CHECK_UINT_RANGE, COPY, 4)]
    public static void Wasm32RetainsWideScalarCastChecks(
        var_types sourceType, bool sourceUnsigned, var_types castType, bool overflow,
        CodeGen.GenIntCastDesc.CheckKind expectedCheck, CodeGen.GenIntCastDesc.ExtendKind expectedExtend,
        int expectedSize)
    {
        WithCastCompiler(() =>
        {
            var source = new GenTreeLclVar(sourceType, 0);
            var cast = new GenTreeCast(castType.ActualType, source, sourceUnsigned, castType);
            if (overflow)
            {
                cast.Flags |= GTF_OVERFLOW;
            }

            var desc = new CodeGen.GenIntCastDesc(cast);
            Assert.That(desc.Check, Is.EqualTo(expectedCheck));
            Assert.That(desc.Extend, Is.EqualTo(expectedExtend));
            Assert.That(desc.ExtendSrcSize, Is.EqualTo(expectedSize));
            if (expectedCheck != CHECK_NONE)
            {
                Assert.That(desc.CheckSrcSize, Is.EqualTo(8));
            }
        });
    }
#endif

    private static unsafe void WithCastCompiler(Action action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitTls.Compiler = compiler;
        try
        {
            action();
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
