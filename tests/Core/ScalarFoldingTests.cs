// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ScalarFoldingTests
{
    [TestCase(GT_ADD, int.MaxValue, 1, int.MinValue, false, false)]
    [TestCase(GT_ADD, long.MaxValue, 1, long.MinValue, true, false)]
    [TestCase(GT_SUB, int.MinValue, 1, int.MaxValue, false, false)]
    [TestCase(GT_MUL, int.MaxValue, 2, -2, false, false)]
    [TestCase(GT_MUL, long.MaxValue, 2, -2, true, false)]
    [TestCase(GT_AND, 7, 3, 3, false, false)]
    [TestCase(GT_OR, 4, 3, 7, true, false)]
    [TestCase(GT_XOR, 7, 3, 4, true, false)]
    [TestCase(GT_LSH, 1, 65, 2, false, false)]
    [TestCase(GT_LSH, 1, 65, 2, true, false)]
    [TestCase(GT_RSH, -8, 1, -4, true, false)]
    [TestCase(GT_RSZ, -1, 1, int.MaxValue, false, false)]
    [TestCase(GT_RSZ, -1, 1, long.MaxValue, true, false)]
    [TestCase(GT_ROL, int.MinValue, 1, 1, false, false)]
    [TestCase(GT_ROR, 1, 1, long.MinValue, true, false)]
    [TestCase(GT_DIV, -7, 3, -2, false, false)]
    [TestCase(GT_MOD, -7, 3, -1, true, false)]
    [TestCase(GT_UDIV, -1, 2, int.MaxValue, false, false)]
    [TestCase(GT_UMOD, -1, 2, 1, true, false)]
    [TestCase(GT_EQ, 4, 4, 1, true, false)]
    [TestCase(GT_NE, 4, 4, 0, false, false)]
    [TestCase(GT_LT, -1, 1, 1, true, false)]
    [TestCase(GT_LT, -1, 1, 0, true, true)]
    [TestCase(GT_GE, -1, 1, 1, false, true)]
    public static void IntegerConstantsPreserveWidthAndSignedness(genTreeOps oper, long left, long right,
        long expected, bool wide, bool unsigned)
    {
        WithCompiler(compiler => {
            var type = wide ? TYP_LONG : TYP_INT;
            var a = wide ? compiler.gtNewLconNode(left) : compiler.gtNewIconNode(TYP_INT, (int)left);
            var b = wide && !oper.IsShiftOrRotate
                ? compiler.gtNewLconNode(right)
                : compiler.gtNewIconNode(TYP_INT, (int)right);
            var tree = compiler.gtNewBinaryNode(oper, oper.IsCompare ? TYP_INT : type, a, b);
            tree.Flags |= GTF_COLON_COND | GTF_DONT_CSE | (unsigned ? GTF_UNSIGNED : GTF_EMPTY);
#if DEBUG
            var nextId = compiler.compGenTreeID;
#endif
            var result = compiler.gtFoldExpr(tree);
            Assert.That(result, Is.Not.SameAs(tree));
            Assert.That(result.AsIntConCommon().IntegralValue, Is.EqualTo(expected));
            Assert.That(result.Type, Is.EqualTo(tree.Type));
            Assert.That(result.Flags & GTF_COLON_COND, Is.EqualTo(GTF_COLON_COND));
            Assert.That(result.Flags & GTF_DONT_CSE, Is.EqualTo(GTF_EMPTY));
            Assert.That(result.Flags & GTF_ALL_EFFECT, Is.EqualTo(GTF_EMPTY));
            Assert.That(tree.Oper, Is.EqualTo(oper));
#if DEBUG
            Assert.That(result.TreeId, Is.EqualTo(tree.TreeId));
            Assert.That(compiler.compGenTreeID, Is.EqualTo(nextId));
#endif
        });
    }

    [TestCase(GT_NOT, 3, -4)]
    [TestCase(GT_NEG, int.MinValue, int.MinValue)]
    [TestCase(GT_BSWAP, 0x01020304, 0x04030201)]
    [TestCase(GT_BSWAP16, 0x01020304, 0x0403)]
    public static void UnaryConstantsFold(genTreeOps oper, int value, int expected)
    {
        WithCompiler(compiler => {
            var tree = compiler.gtNewUnaryNode(oper, TYP_INT, compiler.gtNewIconNode(TYP_INT, value));
            Assert.That(compiler.gtFoldExpr(tree).AsIntCon().IconValue, Is.EqualTo((nint)expected));
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void ExceptionalIntegerDivisionIsNotFolded(bool wide, bool unsigned)
    {
        WithCompiler(compiler => {
            var type = wide ? TYP_LONG : TYP_INT;

            foreach (var divisor in new[] { 0, -1 })
            {
                var a = wide ? compiler.gtNewLconNode(long.MinValue) : compiler.gtNewIconNode(TYP_INT, int.MinValue);
                var b = wide ? compiler.gtNewLconNode(divisor) : compiler.gtNewIconNode(TYP_INT, divisor);
                var tree = compiler.gtNewBinaryNode(unsigned ? GT_UDIV : GT_DIV, type, a, b);
                Assert.That(compiler.gtFoldExpr(tree), Is.SameAs(tree));
            }
        });
    }

    [TestCase(GT_ADD, 1.25, 0.5, 1.75)]
    [TestCase(GT_MUL, -0.0, 1.0, -0.0)]
    [TestCase(GT_SUB, 4.0, 0.5, 3.5)]
    [TestCase(GT_DIV, 7.0, 2.0, 3.5)]
    public static void FloatingArithmeticPreservesBits(genTreeOps oper, double a, double b, double expected)
    {
        WithCompiler(compiler => {
            var tree = compiler.gtNewBinaryNode(oper, TYP_DOUBLE,
                compiler.gtNewDconNode(TYP_DOUBLE, a), compiler.gtNewDconNode(TYP_DOUBLE, b));
            var result = compiler.gtFoldExpr(tree);
            Assert.That(BitConverter.DoubleToInt64Bits(result.AsDblCon().DconVal),
                Is.EqualTo(BitConverter.DoubleToInt64Bits(expected)));
        });
    }

    [TestCase(GT_EQ, false)]
    [TestCase(GT_EQ, true)]
    [TestCase(GT_NE, false)]
    [TestCase(GT_NE, true)]
    [TestCase(GT_LT, true)]
    public static void NaNComparisonsHonorTheUnorderedFlag(genTreeOps oper, bool unordered)
    {
        WithCompiler(compiler => {
            var tree = compiler.gtNewBinaryNode(oper, TYP_INT,
                compiler.gtNewDconNode(TYP_DOUBLE, double.NaN), compiler.gtNewDconNode(TYP_DOUBLE, 1));
            tree.Flags |= unordered ? GTF_RELOP_NAN_UN : GTF_EMPTY;
            Assert.That(compiler.gtFoldExpr(tree).AsIntCon().IconValue, Is.EqualTo((nint)(unordered ? 1 : 0)));
        });
    }

    [Test]
    public static void CastsRespectUnsignedValuesTruncationAndOverflow()
    {
        WithCompiler(compiler => {
            var toLong = compiler.gtNewCastNode(TYP_LONG, compiler.gtNewIconNode(TYP_INT, -1), true, TYP_LONG);
            Assert.That(compiler.gtFoldExpr(toLong).AsIntConCommon().LngValue, Is.EqualTo((long)uint.MaxValue));
            var toFloat = compiler.gtNewCastNode(TYP_FLOAT, compiler.gtNewLconNode(-1), true, TYP_FLOAT);
            Assert.That(compiler.gtFoldExpr(toFloat).AsDblCon().DconVal, Is.EqualTo((double)(float)ulong.MaxValue));
            var toByte = compiler.gtNewCastNode(TYP_INT, compiler.gtNewLconNode(-129), false, TYP_BYTE);
            Assert.That(compiler.gtFoldExpr(toByte).AsIntCon().IconValue, Is.EqualTo((nint)127));
            var toUInt = compiler.gtNewCastNode(TYP_INT, compiler.gtNewDconNode(TYP_DOUBLE, -0.5), false, TYP_UINT);
            Assert.That(compiler.gtFoldExpr(toUInt).AsIntCon().IconValue, Is.EqualTo((nint)0));
            var overflow = compiler.gtNewCastNode(TYP_INT, compiler.gtNewDconNode(TYP_DOUBLE, 2147483648.0), false, TYP_INT);
            Assert.That(compiler.gtFoldExpr(overflow), Is.SameAs(overflow));
        });
    }

    [Test]
    public static void FoldedArgumentsReplaceTheirOwningUse()
    {
        WithCompiler(compiler => {
            var expression = compiler.gtNewBinaryNode(GT_ADD, TYP_INT,
                compiler.gtNewIconNode(TYP_INT, 1), compiler.gtNewIconNode(TYP_INT, 2));
            var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
            var arg = call.Args.PushBack(NewCallArg.CreateForPrimitive(expression));
            call.ReplaceOperand(ref arg.EarlyNodeRef, compiler.gtFoldExpr(arg.EarlyNode));
            Assert.That(call.Args.Head, Is.SameAs(arg));
            Assert.That(arg.Node, Is.Not.SameAs(expression));
            Assert.That(arg.Node.AsIntCon().IconValue, Is.EqualTo((nint)3));
        });
    }

    [Test]
    public static void CheckedOverflowIsDeferredOutsideGlobalMorph()
    {
        WithCompiler(compiler => {
            var tree = compiler.gtNewBinaryNode(GT_ADD, TYP_INT,
                compiler.gtNewIconNode(TYP_INT, int.MaxValue), compiler.gtNewIconNode(TYP_INT, 1));
            tree.Flags |= GTF_OVERFLOW | GTF_EXCEPT;
            Assert.That(compiler.gtFoldExpr(tree), Is.SameAs(tree));
        });
    }

    [Test]
    public static void GlobalMorphOverflowCreatesAnOrderedThrowAndZero()
    {
        WithCompiler(compiler => {
            compiler.fgGlobalMorph = true;
            var tree = compiler.gtNewBinaryNode(GT_ADD, TYP_LONG, compiler.gtNewLconNode(long.MaxValue), compiler.gtNewLconNode(1));
            tree.Flags |= GTF_OVERFLOW | GTF_EXCEPT;
            var result = compiler.gtFoldExpr(tree);
            Assert.That(result.Oper, Is.EqualTo(GT_COMMA));
            Assert.That(result.AsOp().Op1.Oper, Is.EqualTo(GT_CALL));
            Assert.That(result.AsOp().Op1.Type, Is.EqualTo(TYP_VOID));
            Assert.That(result.AsOp().Op2.Type, Is.EqualTo(TYP_LONG));
            Assert.That(result.AsOp().Op2.AsIntConCommon().LngValue, Is.EqualTo(0L));
            Assert.That(result.Flags & GTF_EXCEPT, Is.EqualTo(GTF_EXCEPT));
            Assert.That(tree.Oper, Is.EqualTo(GT_ADD));
        });
    }

    [Test]
    public static void FloatingRoundingAndDivisionByZeroFollowNativePolicy()
    {
        WithCompiler(compiler => {
            var tree = compiler.gtNewBinaryNode(GT_ADD, TYP_FLOAT,
                compiler.gtNewDconNode(TYP_FLOAT, 16777216.0), compiler.gtNewDconNode(TYP_FLOAT, 1));
            Assert.That(compiler.gtFoldExpr(tree).AsDblCon().DconVal, Is.EqualTo(16777216.0));
            var division = compiler.gtNewBinaryNode(GT_DIV, TYP_DOUBLE,
                compiler.gtNewDconNode(TYP_DOUBLE, 1), compiler.gtNewDconNode(TYP_DOUBLE, -0.0));
            Assert.That(compiler.gtFoldExpr(division), Is.SameAs(division));
        });
    }

    [Test]
    public static void GcConstantsAndBoundsChecksRetainNativeRules()
    {
        WithCompiler(compiler => {
            var address = compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF,
                compiler.gtNewIconNode(TYP_REF, 0), compiler.gtNewIconNode(TYP_I_IMPL, 8));
            var result = compiler.gtFoldExpr(address);
            Assert.That(result.Type, Is.EqualTo(TYP_BYREF));
            Assert.That(result.AsIntCon().IconValue, Is.EqualTo((nint)0));
            var valid = new GenTreeBoundsChk(compiler.gtNewIconNode(TYP_INT, 1),
                compiler.gtNewIconNode(TYP_INT, 2), SpecialCodeKind.SCK_RNGCHK_FAIL);
            Assert.That(compiler.gtFoldExpr(valid).Oper, Is.EqualTo(GT_NOP));
            var invalid = new GenTreeBoundsChk(compiler.gtNewIconNode(TYP_INT, -1),
                compiler.gtNewIconNode(TYP_INT, 2), SpecialCodeKind.SCK_RNGCHK_FAIL);
            Assert.That(compiler.gtFoldExpr(invalid), Is.SameAs(invalid));
            Assert.That(invalid.Flags & GTF_EXCEPT, Is.EqualTo(GTF_EXCEPT));
        });
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
}
