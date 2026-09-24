// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CastLoweringTests
{
    [TestCase(0L, false)]
    [TestCase(long.MinValue, true)]
    [TestCase(0x3FF0000000000000L, true)]
    [TestCase(0x7FF8000000001234L, true)]
    public static void FloatingConstantContainmentDistinguishesPositiveZero(long bits, bool contained)
    {
        WithCompiler(compiler => {
            var value = new GenTreeDblCon(var_types.TYP_DOUBLE, BitConverter.Int64BitsToDouble(bits));
            var cast = new GenTreeCast(var_types.TYP_FLOAT, value, false, var_types.TYP_FLOAT);
            var lowering = CreateLowering(compiler, value, cast);

            Assert.That(value.IsCnsNonZeroFltOrDbl, Is.EqualTo(contained));
            ContainCheckCast(lowering, cast);

            Assert.That(value.IsContained, Is.EqualTo(contained));
            Assert.That(value.IsBitwiseEqual(bits), Is.True);
        });
    }

    [TestCase(var_types.TYP_SHORT, var_types.TYP_BYTE, false, true)]
    [TestCase(var_types.TYP_SHORT, var_types.TYP_UBYTE, false, false)]
    [TestCase(var_types.TYP_USHORT, var_types.TYP_BYTE, false, false)]
    [TestCase(var_types.TYP_USHORT, var_types.TYP_UBYTE, false, true)]
    [TestCase(var_types.TYP_USHORT, var_types.TYP_UBYTE, true, false)]
    public static void IntegralCastContainmentPreservesLoadExtension(
        var_types sourceType, var_types castType, bool overflow, bool contained)
    {
        WithCompiler(compiler => {
            compiler.opts.canUseTier0Opts = true;
            compiler.lvaTable[0].Type = sourceType;
            var value = compiler.gtNewLclvNode(sourceType, 0);
            var cast = new GenTreeCast(var_types.TYP_INT, value, false, castType);
            if (overflow)
            {
                cast.Flags |= GenTreeFlags.GTF_OVERFLOW | GenTreeFlags.GTF_EXCEPT;
            }
            var lowering = CreateLowering(compiler, value, cast);

            ContainCheckCast(lowering, cast);

            Assert.That(value.IsContained, Is.EqualTo(contained));
            Assert.That(value.Type, Is.EqualTo(sourceType));
        });
    }

    private static Lowering CreateLowering(Compiler compiler, GenTree source, GenTreeCast cast)
    {
        var block = new BasicBlock(null, null);
        block.MakeLir(null, null);
        block.InsertAtEnd(source);
        block.InsertAtEnd(cast);
        var lowering = new Lowering(compiler, new LinearScan(compiler));
        LoweringBlock(lowering) = block;
        return lowering;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainCheckCast")]
    private static extern void ContainCheckCast(Lowering lowering, GenTreeCast cast);

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.lvaTable = new LclVarDsc[1];
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
