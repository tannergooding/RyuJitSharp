// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class GenTreeArrayAddressTests
{
    [Test]
    public static void DirectArrayAddressReturnsItself()
    {
        WithCompiler(compiler =>
        {
            var arrayAddress = NewArrayAddress(compiler);

            Assert.That(arrayAddress.IsArrayAddr(out var found), Is.True);
            Assert.That(found, Is.SameAs(arrayAddress));
        });
    }

    [Test]
    public static void ConstantOffsetReturnsUnderlyingArrayAddress()
    {
        WithCompiler(compiler =>
        {
            var arrayAddress = NewArrayAddress(compiler);
            var address = compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF, arrayAddress,
                compiler.gtNewIconNode(TYP_I_IMPL, -8));

            Assert.That(address.IsArrayAddr(out var found), Is.True);
            Assert.That(found, Is.SameAs(arrayAddress));
        });
    }

    [Test]
    public static void NonconstantOffsetDoesNotMatch()
    {
        WithCompiler(compiler =>
        {
            var arrayAddress = NewArrayAddress(compiler);
            var address = compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF, arrayAddress,
                compiler.gtNewLclvNode(TYP_I_IMPL, 0));

            Assert.That(address.IsArrayAddr(out var found), Is.False);
            Assert.That(found, Is.Null);
        });
    }

    [Test]
    public static void ConstantLeftOperandIsNotReordered()
    {
        WithCompiler(compiler =>
        {
            var arrayAddress = NewArrayAddress(compiler);
            var address = compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF,
                compiler.gtNewIconNode(TYP_I_IMPL, 8), arrayAddress);

            Assert.That(address.IsArrayAddr(out var found), Is.False);
            Assert.That(found, Is.Null);
        });
    }

    [Test]
    public static void OffsetAddressWithoutArrayNodeDoesNotMatch()
    {
        WithCompiler(compiler =>
        {
            var address = compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF,
                compiler.gtNewIconNode(TYP_I_IMPL, 0x4000),
                compiler.gtNewIconNode(TYP_I_IMPL, 8));

            Assert.That(address.IsArrayAddr(out var found), Is.False);
            Assert.That(found, Is.Null);
        });
    }

    private static GenTreeArrAddr NewArrayAddress(Compiler compiler)
    {
        return new GenTreeArrAddr(compiler.gtNewIconNode(TYP_I_IMPL, 0x4000), TYP_INT, null, 16);
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
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
