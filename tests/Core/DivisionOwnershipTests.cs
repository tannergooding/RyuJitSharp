// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class DivisionOwnershipTests
{
    [TestCase(GT_DIV, GT_EQ)]
    [TestCase(GT_UDIV, GT_RSZ)]
    [TestCase(GT_UMOD, GT_AND)]
    [TestCase(GT_MOD, GT_SUB)]
    public static void NativeChangeOperClearsSpecificFlagsAndValueNumbers(genTreeOps original, genTreeOps replacement)
    {
        WithCompiler(compiler => {
            var dividend = compiler.gtNewLclvNode(TYP_INT, 0);
            var divisor = compiler.gtNewIconNode(TYP_INT, 2);
            var division = new GenTreeOp(original, TYP_INT, dividend, divisor);
            division.Flags |= GTF_IND_UNALIGNED | GTF_DONT_CSE | GTF_EXCEPT;
            division._vnPair.SetBoth(123);
            var common = division.Flags & GTF_COMMON_MASK;

            ChangeOper(new Lowering(compiler, new LinearScan(compiler)), division, replacement);

            Assert.That(division.Oper, Is.EqualTo(replacement));
            Assert.That(division.Flags, Is.EqualTo(common));
            Assert.That(division._vnPair.Liberal, Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(division._vnPair.Conservative, Is.EqualTo(ValueNumStore.NoVN));
        });
    }

    [Test]
    public static void LocalDividendUseNeedsNoTemporary()
    {
        WithCompiler(compiler => {
            var dividend = compiler.gtNewLclvNode(TYP_INT, 0);
            var divisor = compiler.gtNewIconNode(TYP_INT, 3);
            var divMod = new GenTreeOp(GT_UMOD, TYP_INT, dividend, divisor);
            var block = NewBlock(dividend, divisor, divMod);
            var lowering = NewLowering(compiler, block);
            var originalLocalCount = compiler.lvaCount;
            var use = new LIR.Use(block, ref divMod.Op1Ref, divMod);

            var result = ReplaceWithLclVar(lowering, use);

            Assert.That(result, Is.SameAs(dividend));
            Assert.That(divMod.Op1, Is.SameAs(dividend));
            Assert.That(compiler.lvaCount, Is.EqualTo(originalLocalCount));
            Assert.That(dividend.Next, Is.SameAs(divisor));
        });
    }

    [Test]
    public static void NonLocalDividendGetsSingleLoweredStoreInMinOpts()
    {
        WithCompiler(compiler => {
            var value = compiler.gtNewLclvNode(TYP_INT, 0);
            var amount = compiler.gtNewIconNode(TYP_INT, 4);
            var dividend = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, value, amount);
            var divisor = compiler.gtNewIconNode(TYP_INT, 3);
            var divMod = new GenTreeOp(GT_UMOD, TYP_INT, dividend, divisor);
            var block = NewBlock(value, amount, dividend, divisor, divMod);
            var lowering = NewLowering(compiler, block);
            var originalLocalCount = compiler.lvaCount;
            var use = new LIR.Use(block, ref divMod.Op1Ref, divMod);

            var read = ReplaceWithLclVar(lowering, use);

            Assert.That(compiler.lvaCount, Is.EqualTo(originalLocalCount + 1));
            Assert.That(divMod.Op1, Is.SameAs(read));
            Assert.That(dividend.Next!.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            Assert.That(dividend.Next!.AsLclVarCommon().Op1, Is.SameAs(dividend));
            Assert.That(dividend.Next.Next, Is.SameAs(read));
            Assert.That(read.Next, Is.SameAs(divisor));
            Assert.That(dividend.IsContained, Is.False);
        });
    }

    [Test]
    public static void UnsignedIntQuotientCastPreservesLogicalIdentityAndFlags()
    {
        WithCompiler(compiler => {
            var dividend = compiler.gtNewLclvNode(TYP_INT, 0);
            var divisor = compiler.gtNewIconNode(TYP_INT, 3);
            var division = new GenTreeOp(GT_UDIV, TYP_INT, dividend, divisor);
            division.Flags |= GTF_IND_UNALIGNED | GTF_EXCEPT;
            division._vnPair.SetBoth(123);
            var originalFlags = division.Flags;
#if DEBUG
            var originalId = division.TreeId;
#endif
            var user = new GenTreeUnOp(GT_RETURN, TYP_VOID, division);
            var block = NewBlock(dividend, divisor, division, user);

            Assert.That(TryUnsigned(NewLowering(compiler, block), division, out var next), Is.True);
            var cast = user.Op1.AsCast();
            Assert.That(cast.CastType, Is.EqualTo(TYP_INT));
            Assert.That(cast.Type, Is.EqualTo(TYP_INT));
            Assert.That(cast.IsUnsigned, Is.EqualTo((originalFlags & GTF_UNSIGNED) != 0));
            Assert.That(cast.Flags, Is.EqualTo(originalFlags & GTF_COMMON_MASK));
            Assert.That(cast._vnPair.Liberal, Is.EqualTo(ValueNumStore.NoVN));
#if DEBUG
            Assert.That(cast.TreeId, Is.EqualTo(originalId));
#endif
            Assert.That(next, Is.SameAs(user));
        }, minOpts: false);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ChangeDivisionOper")]
    private static extern void ChangeOper(Lowering lowering, GenTreeOp node, genTreeOps oper);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ReplaceCallTargetUseWithLclVar")]
    private static extern GenTreeLclVar ReplaceWithLclVar(Lowering lowering, LIR.Use use, int tempNum = BAD_VAR_NUM);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "TryLowerConstIntUDivOrUMod")]
    private static extern bool TryUnsigned(Lowering lowering, GenTreeOp node, out GenTree? next);

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
