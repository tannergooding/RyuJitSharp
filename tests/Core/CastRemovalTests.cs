// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CastRemovalTests
{
    [TestCase(var_types.TYP_INT, var_types.TYP_BYTE, 0x1234, false, 0x34)]
    [TestCase(var_types.TYP_INT, var_types.TYP_UBYTE, -1, false, 255)]
    [TestCase(var_types.TYP_INT, var_types.TYP_LONG, -1, false, -1)]
    [TestCase(var_types.TYP_INT, var_types.TYP_LONG, -1, true, uint.MaxValue)]
    [TestCase(var_types.TYP_LONG, var_types.TYP_INT, 0x1234567890L, false, 0x34567890)]
    public static void IntegralConstantFoldingKeepsLogicalIdentityAndOwner(var_types sourceType,
        var_types destinationType, long input, bool fromUnsigned, long expected)
    {
        WithCompiler(minOpts: false, compiler => {
            var operand = compiler.gtNewIconNode(sourceType, unchecked((nint)input));
            var cast = new GenTreeCast(destinationType.ActualType, operand, fromUnsigned, destinationType);
            cast.Flags |= GenTreeFlags.GTF_DONT_CSE | GenTreeFlags.GTF_ORDER_SIDEEFF;
            cast._vnPair.SetBoth(173);
            var owner = new GenTreeUnOp(genTreeOps.GT_RETURN, var_types.TYP_VOID, cast);
            var block = NewBlock(operand, cast, owner);
            var lowering = NewLowering(compiler, block);
            var successor = cast.Next ?? throw new InvalidOperationException();
#if DEBUG
            var id = cast.TreeId;
#endif

            Assert.That(TryRemoveCast(lowering, cast), Is.True);
            Assert.That(owner.Op1, Is.SameAs(operand.Next));
            Assert.That(owner.Op1, Is.SameAs(successor.Prev));
            Assert.That(owner.Op1.Oper, Is.EqualTo(genTreeOps.GT_CNS_INT));
            Assert.That(owner.Op1.AsIntCon().IconValue, Is.EqualTo(unchecked((nint)expected)));
            Assert.That(owner.Op1.Type, Is.EqualTo(destinationType.ActualType));
            Assert.That(owner.Op1._vnPair.Conservative, Is.EqualTo(173));
            Assert.That(owner.Op1.Flags & GenTreeFlags.GTF_ORDER_SIDEEFF, Is.EqualTo(GenTreeFlags.GTF_EMPTY));
            Assert.That(owner.Op1.Flags & GenTreeFlags.GTF_DONT_CSE, Is.EqualTo(GenTreeFlags.GTF_EMPTY));
            Assert.That(owner.Op1.Flags & GenTreeFlags.GTF_UNSIGNED, Is.EqualTo(GenTreeFlags.GTF_EMPTY));
            Assert.That(operand.IsUnusedValue, Is.True);
            Assert.That(cast.Next, Is.Null);
            Assert.That(cast.Prev, Is.Null);
            Assert.That(successor, Is.SameAs(owner));
#if DEBUG
            Assert.That(owner.Op1.TreeId, Is.EqualTo(id));
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [TestCase(var_types.TYP_DOUBLE, var_types.TYP_INT, 42.75, 42)]
    [TestCase(var_types.TYP_FLOAT, var_types.TYP_LONG, 17.5, 17)]
    public static void FloatingConstantToIntegralUsesExistingFolding(var_types sourceType,
        var_types destinationType, double input, long expected)
    {
        WithCompiler(minOpts: false, compiler => {
            var operand = new GenTreeDblCon(sourceType, input);
            var cast = new GenTreeCast(destinationType, operand, false, destinationType);
            var owner = new GenTreeUnOp(genTreeOps.GT_RETURN, var_types.TYP_VOID, cast);
            var block = NewBlock(operand, cast, owner);

            Assert.That(TryRemoveCast(NewLowering(compiler, block), cast), Is.True);
            Assert.That(owner.Op1.Oper, Is.EqualTo(genTreeOps.GT_CNS_INT));
            Assert.That(owner.Op1.AsIntCon().IconValue, Is.EqualTo((nint)expected));
            Assert.That(operand.IsUnusedValue, Is.True);
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [Test]
    public static void FloatingConstantKeepsNegativeZero()
    {
        WithCompiler(minOpts: false, compiler => {
            var operand = new GenTreeDblCon(var_types.TYP_FLOAT, -0.0);
            var cast = new GenTreeCast(var_types.TYP_DOUBLE, operand, false, var_types.TYP_DOUBLE);
            var owner = new GenTreeUnOp(genTreeOps.GT_RETURN, var_types.TYP_VOID, cast);
            var block = NewBlock(operand, cast, owner);

            Assert.That(TryRemoveCast(NewLowering(compiler, block), cast), Is.True);
            Assert.That(owner.Op1.Oper, Is.EqualTo(genTreeOps.GT_CNS_DBL));
            Assert.That(owner.Op1.AsDblCon().IsNegativeZero, Is.True);
            Assert.That(operand.IsUnusedValue, Is.True);
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [TestCase(true, false, false)]
    [TestCase(false, true, false)]
    [TestCase(false, false, true)]
    public static void MinOptsOverflowAndNonconstantOperandsRemainCasts(bool minOpts,
        bool overflow, bool nonconstant)
    {
        WithCompiler(minOpts, compiler => {
            compiler.lvaTable[0].Type = var_types.TYP_INT;
            GenTree operand = nonconstant
                ? compiler.gtNewLclvNode(var_types.TYP_INT, 0)
                : compiler.gtNewIconNode(var_types.TYP_INT, 127);
            var cast = new GenTreeCast(var_types.TYP_INT, operand, false, var_types.TYP_BYTE);
            if (overflow)
            {
                cast.Flags |= GenTreeFlags.GTF_OVERFLOW | GenTreeFlags.GTF_EXCEPT;
            }
            var owner = new GenTreeUnOp(genTreeOps.GT_RETURN, var_types.TYP_VOID, cast);
            var block = NewBlock(operand, cast, owner);

            Assert.That(TryRemoveCast(NewLowering(compiler, block), cast), Is.False);
            Assert.That(owner.Op1, Is.SameAs(cast));
            Assert.That(operand.IsUnusedValue, Is.False);
            Assert.That(operand.Next, Is.SameAs(cast));
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [Test]
    public static void FloatingNaNToIntRemainsForCodegen()
    {
        WithCompiler(minOpts: false, compiler => {
            var operand = new GenTreeDblCon(var_types.TYP_DOUBLE, double.NaN);
            var cast = new GenTreeCast(var_types.TYP_INT, operand, false, var_types.TYP_INT);
            var owner = new GenTreeUnOp(genTreeOps.GT_RETURN, var_types.TYP_VOID, cast);
            var block = NewBlock(operand, cast, owner);

            Assert.That(TryRemoveCast(NewLowering(compiler, block), cast), Is.False);
            Assert.That(owner.Op1, Is.SameAs(cast));
            Assert.That(operand.IsUnusedValue, Is.False);
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [TestCase(genTreeOps.GT_LSH, 3, 2, 12)]
    [TestCase(genTreeOps.GT_RSZ, -1, 4, 268435455)]
    public static void BinaryLirFoldingTransfersOwnerAndLeavesOperandCleanupToCaller(genTreeOps oper,
        int left, int right, int expected)
    {
        WithCompiler(minOpts: false, compiler => {
            var first = compiler.gtNewIconNode(var_types.TYP_INT, left);
            var second = compiler.gtNewIconNode(var_types.TYP_INT, right);
            var binary = new GenTreeOp(oper, var_types.TYP_INT, first, second);
            binary._vnPair.SetBoth(314);
            var owner = new GenTreeUnOp(genTreeOps.GT_RETURN, var_types.TYP_VOID, binary);
            var block = NewBlock(first, second, binary, owner);
            var lowering = NewLowering(compiler, block);
#if DEBUG
            var id = binary.TreeId;
#endif

            var folded = TryFoldLirConst(lowering, binary);
            Assert.That(folded, Is.SameAs(owner.Op1));
            Assert.That(folded?.AsIntCon().IconValue, Is.EqualTo((nint)expected));
            Assert.That(folded?._vnPair.Conservative, Is.EqualTo(314));
            Assert.That(binary.Next, Is.Null);
            Assert.That(block.FirstNode, Is.SameAs(first));
            block.Remove(first);
            block.Remove(second);
#if DEBUG
            Assert.That(folded?.TreeId, Is.EqualTo(id));
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [Test]
    public static void BinaryFlagSetterNeverFolds()
    {
        WithCompiler(minOpts: false, compiler => {
            var first = compiler.gtNewIconNode(var_types.TYP_INT, 3);
            var second = compiler.gtNewIconNode(var_types.TYP_INT, 2);
            var binary = new GenTreeOp(genTreeOps.GT_LSH, var_types.TYP_INT, first, second) {
                Flags = GenTreeFlags.GTF_SET_FLAGS,
            };
            var owner = new GenTreeUnOp(genTreeOps.GT_RETURN, var_types.TYP_VOID, binary);
            var block = NewBlock(first, second, binary, owner);

            Assert.That(TryFoldLirConst(NewLowering(compiler, block), binary), Is.Null);
            Assert.That(owner.Op1, Is.SameAs(binary));
            Assert.That(second.Next, Is.SameAs(binary));
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "TryRemoveCast")]
    private static extern bool TryRemoveCast(Lowering lowering, GenTreeCast cast);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "TryFoldLirConst")]
    private static extern GenTree? TryFoldLirConst(Lowering lowering, GenTree node);

    private static void WithCompiler(bool minOpts, Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minOpts);
        compiler.lvaTable = new LclVarDsc[4];
        compiler.lvaCount = 1;
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
