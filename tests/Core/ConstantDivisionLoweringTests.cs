// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class ConstantDivisionLoweringTests
{
    [TestCase(GT_UDIV, 0, GT_RSZ, 0)]
    [TestCase(GT_UDIV, 8, GT_RSZ, 3)]
    [TestCase(GT_UMOD, 8, GT_AND, 7)]
    [TestCase(GT_UDIV, -2147483648, GT_RSZ, 31)]
    public static void UnsignedPowersOfTwoApplyInMinOpts(genTreeOps oper, int constant, genTreeOps expected, int operand)
    {
        WithCompiler(compiler => {
            var dividend = compiler.gtNewLclvNode(TYP_INT, 0);
            var divisor = compiler.gtNewIconNode(TYP_INT, constant);
            var division = new GenTreeOp(oper, TYP_INT, dividend, divisor);
            var block = NewBlock(dividend, divisor, division);
            var lowering = NewLowering(compiler, block);
            var transformed = TryUnsigned(lowering, division, out var next);
            Assert.That(transformed, Is.EqualTo(constant != 0));
            Assert.That(division.Oper, Is.EqualTo(constant != 0 ? expected : oper));
            Assert.That(divisor.IconValue, Is.EqualTo((nint)(constant != 0 ? operand : constant)));
            Assert.That(next, Is.Null);
            Assert.That(dividend.Next, Is.SameAs(divisor));
        });
    }

    [Test]
    public static void UnsignedHighHalfDivisorComparesInMinOpts()
    {
        WithCompiler(compiler => {
            var dividend = compiler.gtNewLclvNode(TYP_INT, 0);
            var divisor = compiler.gtNewIconNode(TYP_INT, -3);
            var division = new GenTreeOp(GT_UDIV, TYP_INT, dividend, divisor);
            var lowering = NewLowering(compiler, NewBlock(dividend, divisor, division));
            Assert.That(TryUnsigned(lowering, division, out _), Is.True);
            Assert.That(division.Oper, Is.EqualTo(GT_GE));
            Assert.That(division.IsUnsigned, Is.True);
        });
    }

    [Test]
    public static void UnsignedNativeWidthHighHalfDivisorComparesInMinOpts()
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_I_IMPL;
            var dividend = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            var divisor = compiler.gtNewIconNode(TYP_I_IMPL, -3);
            var division = new GenTreeOp(GT_UDIV, TYP_I_IMPL, dividend, divisor);
            var lowering = NewLowering(compiler, NewBlock(dividend, divisor, division));
            Assert.That(TryUnsigned(lowering, division, out _), Is.True);
            Assert.That(division.Oper, Is.EqualTo(GT_GE));
            Assert.That(division.IsUnsigned, Is.True);
        });
    }

    [TestCase(GT_DIV, 0)]
    [TestCase(GT_MOD, 0)]
    [TestCase(GT_DIV, -1)]
    [TestCase(GT_MOD, -1)]
    public static void SignedExceptionalCasesRetainDivision(genTreeOps oper, int constant)
    {
        WithCompiler(compiler => {
            var dividend = compiler.gtNewLclvNode(TYP_INT, 0);
            var divisor = compiler.gtNewIconNode(TYP_INT, constant);
            var division = new GenTreeOp(oper, TYP_INT, dividend, divisor);
            var lowering = NewLowering(compiler, NewBlock(dividend, divisor, division));
            Assert.That(TrySigned(lowering, division, out var next), Is.False);
            Assert.That(next, Is.Null);
            Assert.That(division.Oper, Is.EqualTo(oper));
            Assert.That(division.Op1, Is.SameAs(dividend));
        });
    }

    [Test]
    public static void SignedMinimumDivisorComparesInMinOpts()
    {
        WithCompiler(compiler => {
            var dividend = compiler.gtNewLclvNode(TYP_INT, 0);
            var divisor = compiler.gtNewIconNode(TYP_INT, int.MinValue);
            var division = new GenTreeOp(GT_DIV, TYP_INT, dividend, divisor);
            var lowering = NewLowering(compiler, NewBlock(dividend, divisor, division));
            Assert.That(TrySigned(lowering, division, out var next), Is.True);
            Assert.That(division.Oper, Is.EqualTo(GT_EQ));
            Assert.That(next, Is.SameAs(division));
        });
    }

    [Test]
    public static void SignedNativeWidthMinimumDivisorComparesInMinOpts()
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_I_IMPL;
            var dividend = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            var divisor = compiler.gtNewIconNode(TYP_I_IMPL, nint.MinValue);
            var division = new GenTreeOp(GT_DIV, TYP_I_IMPL, dividend, divisor);
            var lowering = NewLowering(compiler, NewBlock(dividend, divisor, division));
            Assert.That(TrySigned(lowering, division, out var next), Is.True);
            Assert.That(division.Oper, Is.EqualTo(GT_EQ));
            Assert.That(next, Is.SameAs(division));
        });
    }

    [TestCase(GT_DIV, 8)]
    [TestCase(GT_DIV, -8)]
    [TestCase(GT_MOD, 8)]
    [TestCase(GT_MOD, -8)]
    public static void SignedPowersOfTwoUseSingleEvaluationInMinOpts(genTreeOps oper, int constant)
    {
        WithCompiler(compiler => {
            var dividend = compiler.gtNewLclvNode(TYP_INT, 0);
            var divisor = compiler.gtNewIconNode(TYP_INT, constant);
            var division = new GenTreeOp(oper, TYP_INT, dividend, divisor);
            var user = new GenTreeUnOp(GT_RETURN, TYP_VOID, division);
            var block = NewBlock(dividend, divisor, division, user);
            var lowering = NewLowering(compiler, block);
            Assert.That(TrySigned(lowering, division, out var next), Is.True);
            Assert.That(user.Op1, Is.Not.SameAs(division));
            Assert.That(user.Op1.Oper, Is.EqualTo(oper is GT_DIV
                ? (constant < 0 ? GT_NEG : GT_RSH) : GT_SUB));
            Assert.That(next, Is.SameAs(user));
            Assert.That(division.Next, Is.Null);
            Assert.That(division.Prev, Is.Null);
        });
    }

    [TestCase(GT_DIV, 3)]
    [TestCase(GT_DIV, 7)]
    [TestCase(GT_MOD, 7)]
    [TestCase(GT_MOD, -3)]
    public static void SignedMagicCreatesQuotientAndRemainderSequences(genTreeOps oper, int constant)
    {
        WithCompiler(compiler => {
            var dividend = compiler.gtNewLclvNode(TYP_INT, 0);
            var divisor = compiler.gtNewIconNode(TYP_INT, constant);
            var division = new GenTreeOp(oper, TYP_INT, dividend, divisor);
            var user = new GenTreeUnOp(GT_RETURN, TYP_VOID, division);
            var block = NewBlock(dividend, divisor, division, user);
            Assert.That(TrySigned(NewLowering(compiler, block), division, out var next), Is.True);
            Assert.That(user.Op1, Is.SameAs(division));
            Assert.That(division.Oper, Is.EqualTo(oper is GT_DIV ? GT_ADD : GT_SUB));
            Assert.That(next?.Oper, Is.EqualTo(GT_MULHI));
            Assert.That(dividend.Next, Is.Not.Null);
        }, minOpts: false);
    }

    [TestCase(GT_UDIV, 3)]
    [TestCase(GT_UDIV, 7)]
    [TestCase(GT_UDIV, 14)]
    [TestCase(GT_UDIV, 28)]
    [TestCase(GT_UMOD, 7)]
    [TestCase(GT_UMOD, 10)]
    [TestCase(GT_UMOD, 14)]
    [TestCase(GT_UMOD, 28)]
    public static void UnsignedMagicCreatesQuotientAndRemainderSequences(genTreeOps oper, int constant)
    {
        WithCompiler(compiler => {
            var dividend = compiler.gtNewLclvNode(TYP_INT, 0);
            var divisor = compiler.gtNewIconNode(TYP_INT, constant);
            var division = new GenTreeOp(oper, TYP_INT, dividend, divisor);
            var user = new GenTreeUnOp(GT_RETURN, TYP_VOID, division);
            var block = NewBlock(dividend, divisor, division, user);
            Assert.That(TryUnsigned(NewLowering(compiler, block), division, out var next), Is.True);
            Assert.That(user.Op1.Oper, Is.EqualTo(oper is GT_UDIV ? GT_CAST : GT_SUB));
            Assert.That(next, Is.SameAs(user));
            Assert.That(dividend.Next, Is.Not.Null);
        }, minOpts: false);
    }

    [TestCase(GT_DIV, 37)]
    [TestCase(GT_MOD, -37)]
    public static void SignedNativeWidthMagicKeepsResultUse(genTreeOps oper, int constant)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_I_IMPL;
            var dividend = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            var divisor = compiler.gtNewIconNode(TYP_I_IMPL, constant);
            var division = new GenTreeOp(oper, TYP_I_IMPL, dividend, divisor);
            var user = new GenTreeUnOp(GT_RETURN, TYP_VOID, division);
            var block = NewBlock(dividend, divisor, division, user);
            Assert.That(TrySigned(NewLowering(compiler, block), division, out var next), Is.True);
            Assert.That(division.Oper, Is.EqualTo(oper is GT_DIV ? GT_ADD : GT_SUB));
            Assert.That(user.Op1, Is.SameAs(division));
            Assert.That(next?.Oper, Is.EqualTo(GT_MULHI));
        }, minOpts: false);
    }

    [TestCase(GT_UDIV, 37)]
    [TestCase(GT_UMOD, 37)]
    public static void UnsignedNativeWidthMagicKeepsResultUse(genTreeOps oper, int constant)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_I_IMPL;
            var dividend = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            var divisor = compiler.gtNewIconNode(TYP_I_IMPL, constant);
            var division = new GenTreeOp(oper, TYP_I_IMPL, dividend, divisor);
            var user = new GenTreeUnOp(GT_RETURN, TYP_VOID, division);
            var block = NewBlock(dividend, divisor, division, user);
            Assert.That(TryUnsigned(NewLowering(compiler, block), division, out var next), Is.True);
            Assert.That(user.Op1, Is.SameAs(division));
            Assert.That(division.Oper, Is.EqualTo(oper is GT_UDIV ? GT_RSZ : GT_SUB));
            Assert.That(next, Is.SameAs(user));
        }, minOpts: false);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "TryLowerConstIntUDivOrUMod")]
    private static extern bool TryUnsigned(Lowering lowering, GenTreeOp node, out GenTree? next);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "TryLowerConstIntDivOrMod")]
    private static extern bool TrySigned(Lowering lowering, GenTreeOp node, out GenTree? next);

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

    private static void WithCompiler(Action<Compiler> action, bool minOpts = true)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minOpts);
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
