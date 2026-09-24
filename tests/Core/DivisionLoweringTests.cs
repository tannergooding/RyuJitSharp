// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class DivisionLoweringTests
{
    [TestCase(GT_DIV, 0)]
    [TestCase(GT_MOD, 0)]
    [TestCase(GT_UDIV, 0)]
    [TestCase(GT_UMOD, 0)]
    [TestCase(GT_DIV, -1)]
    [TestCase(GT_MOD, -1)]
    public static void ExceptionalDivisorsRemainInPlace(genTreeOps oper, int divisorValue)
    {
        WithCompiler(compiler => {
            var dividend = compiler.gtNewLclvNode(TYP_INT, 0);
            var divisor = compiler.gtNewIconNode(TYP_INT, divisorValue);
            var divMod = new GenTreeOp(oper, TYP_INT, dividend, divisor);
            var block = NewBlock(dividend, divisor, divMod);

            LowerDivOrMod(NewLowering(compiler, block), divMod);

            Assert.That(divMod.Oper, Is.EqualTo(oper));
            Assert.That(divMod.Op1, Is.SameAs(dividend));
            Assert.That(divMod.Op2, Is.SameAs(divisor));
            Assert.That(divisor.AsIntCon().IconValue, Is.EqualTo((nint)divisorValue));
            Assert.That(dividend.IsContained, Is.False);
            Assert.That(divisor.IsContained, Is.False);
            Assert.That(divisor.IsRegOptional, Is.True);
            Assert.That(dividend.Next, Is.SameAs(divisor));
            Assert.That(divisor.Next, Is.SameAs(divMod));
        });
    }

    [TestCase(GT_DIV)]
    [TestCase(GT_MOD)]
    [TestCase(GT_UDIV)]
    [TestCase(GT_UMOD)]
    public static void RegisterDividendAndMemoryDivisorKeepNativeOperandRoles(genTreeOps oper)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[1].lvDoNotEnregister = true;
            var dividend = compiler.gtNewLclvNode(TYP_INT, 0);
            var divisor = compiler.gtNewLclvNode(TYP_INT, 1);
            var divMod = new GenTreeOp(oper, TYP_INT, dividend, divisor);
            var block = NewBlock(dividend, divisor, divMod);

            ContainCheckDivOrMod(NewLowering(compiler, block), divMod);

            Assert.That(dividend.IsContained, Is.False);
            Assert.That(dividend.IsRegOptional, Is.False);
            Assert.That(divisor.IsContained, Is.True);
            Assert.That(dividend.Next, Is.SameAs(divisor));
            Assert.That(divisor.Next, Is.SameAs(divMod));
        });
    }

    [Test]
    public static void MismatchedDivisorWidthCannotBeContained()
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_LONG;
            compiler.lvaTable[1].Type = TYP_INT;
            compiler.lvaTable[1].lvDoNotEnregister = true;
            var dividend = compiler.gtNewLclvNode(TYP_LONG, 0);
            var divisor = compiler.gtNewLclvNode(TYP_INT, 1);
            var divMod = new GenTreeOp(GT_UDIV, TYP_LONG, dividend, divisor);
            var block = NewBlock(dividend, divisor, divMod);

            ContainCheckDivOrMod(NewLowering(compiler, block), divMod);

            Assert.That(divisor.IsContained, Is.False);
            Assert.That(divisor.IsRegOptional, Is.True);
        });
    }

    [Test]
    public static void FloatingDivisionPreservesNegativeZeroConstant()
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_DOUBLE;
            var dividend = compiler.gtNewLclvNode(TYP_DOUBLE, 0);
            var divisor = compiler.gtNewDconNode(TYP_DOUBLE, -0.0);
            var div = new GenTreeOp(GT_DIV, TYP_DOUBLE, dividend, divisor);
            var block = NewBlock(dividend, divisor, div);

            LowerDivOrMod(NewLowering(compiler, block), div);

            Assert.That(divisor.IsContained, Is.True);
            Assert.That(divisor.IsNegativeZero, Is.True);
            Assert.That(div.Op2, Is.SameAs(divisor));
            Assert.That(dividend.Next, Is.SameAs(divisor));
            Assert.That(divisor.Next, Is.SameAs(div));
        });
    }

    private static BasicBlock NewBlock(params GenTree[] nodes)
    {
        var block = new BasicBlock(null, null);
        block.MakeLir(null, null);
        foreach (var node in nodes)
        {
            block.InsertAtEnd(node);
        }
        return block;
    }

    private static Lowering NewLowering(Compiler compiler, BasicBlock block)
    {
        var lowering = new Lowering(compiler, new LinearScan(compiler));
        LoweringBlock(lowering) = block;
        return lowering;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainCheckDivOrMod")]
    private static extern void ContainCheckDivOrMod(Lowering lowering, GenTreeOp node);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerDivOrMod")]
    private static extern void LowerDivOrMod(Lowering lowering, GenTreeOp node);

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
        compiler.opts.compFlags = CLFLG_REGVAR;
        compiler.fgNodeThreading = NodeThreading.LIR;
        compiler.lvaTable = [new LclVarDsc(), new LclVarDsc()];
        compiler.lvaCount = 2;
        compiler.lvaTable[0].Type = TYP_INT;
        compiler.lvaTable[1].Type = TYP_INT;
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);
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
