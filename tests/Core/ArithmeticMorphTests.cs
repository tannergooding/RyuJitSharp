// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ArithmeticMorphTests
{
    [TestCase(8L, false, 3, GT_LCL_VAR)]
    [TestCase(-8L, false, 3, GT_NEG)]
    [TestCase(long.MinValue, false, 63, GT_LCL_VAR)]
    [TestCase(12L, false, 2, GT_MUL)]
    [TestCase(12L, true, 2, GT_MUL)]
    [TestCase(7L, false, -1, GT_LCL_VAR)]
    public static void MultiplyStrengthReductionPreservesWidthsAndNativeCodePreference(long factor, bool smallCode, int shift, genTreeOps operand)
    {
        WithCompiler(compiler => {
            compiler.opts.compCodeOpt = smallCode ? Compiler.SMALL_CODE : Compiler.BLENDED_CODE;
            Assert.That(compiler.compCodeOpt, Is.EqualTo(Compiler.BLENDED_CODE));
            var local = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            var multiply = compiler.gtNewBinaryNode(GT_MUL, TYP_I_IMPL, local, compiler.gtNewIconNode(TYP_I_IMPL, (nint)factor));
            multiply._vnPair.SetBoth(42);
            var result = compiler.fgOptimizeMultiply(multiply);
            if (shift < 0)
            {
                Assert.That(result, Is.Null);
                Assert.That(multiply.Oper, Is.EqualTo(GT_MUL));
                Assert.That(multiply.Op2.AsIntCon().IconValue, Is.EqualTo((nint)factor));
            }
            else
            {
                Assert.That(result, Is.SameAs(multiply));
                Assert.That(multiply.Oper, Is.EqualTo(GT_LSH));
                Assert.That(multiply.Op1.Oper, Is.EqualTo(operand));
                Assert.That(multiply.Op2.AsIntCon().IconValue, Is.EqualTo((nint)shift));
                Assert.That(multiply._vnPair.Liberal, Is.EqualTo(42));
                if (operand is GT_MUL)
                {
                    Assert.That(multiply.Op1.AsOp().Op2.AsIntCon().IconValue, Is.EqualTo((nint)3));
                }
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ZeroMultiplicationPreservesSideEffects(bool sideEffects)
    {
        WithCompiler(compiler => {
            GenTree value = sideEffects
                ? compiler.gtNewCallNode(TYP_INT, gtCallTypes.CT_USER_FUNC, null)
                : compiler.gtNewLclvNode(TYP_INT, 1);
            var zero = compiler.gtNewIconNode(TYP_INT, 0);
            var multiply = compiler.gtNewBinaryNode(GT_MUL, TYP_INT, value, zero);
            var result = compiler.fgOptimizeMultiply(multiply);
            Assert.That(result, Is.SameAs(sideEffects ? multiply : zero));
            if (sideEffects)
            {
                Assert.That(multiply.Oper, Is.EqualTo(GT_COMMA));
                Assert.That(multiply.Op1, Is.SameAs(value));
                Assert.That(multiply.Flags & GTF_CALL, Is.EqualTo(GTF_CALL));
            }
        });
    }

    [TestCase(1.0, GT_LCL_VAR)]
    [TestCase(-1.0, GT_NEG)]
    [TestCase(2.0, GT_ADD)]
    public static void FloatingMultiplicationPreservesOperandOwnershipAndReplacementIdentity(double factor, genTreeOps operation)
    {
        WithCompiler(compiler => {
            var local = compiler.gtNewLclvNode(TYP_DOUBLE, 2);
            var multiply = compiler.gtNewBinaryNode(GT_MUL, TYP_DOUBLE, local, compiler.gtNewDconNode(TYP_DOUBLE, factor));
            multiply._vnPair.SetBoth(42);
            var result = compiler.fgOptimizeMultiply(multiply);
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Oper, Is.EqualTo(operation));
            Assert.That(multiply.Oper, Is.EqualTo(GT_MUL));
            if (operation is GT_NEG)
            {
                Assert.That(result, Is.TypeOf<GenTreeUnOp>());
                Assert.That(result.AsUnOp().Op1, Is.SameAs(local));
                Assert.That(result._vnPair.Liberal, Is.EqualTo(42));
#if DEBUG
                Assert.That(result.TreeId, Is.EqualTo(multiply.TreeId));
#endif
            }
            else if (operation is GT_ADD)
            {
                Assert.That(result.AsOp().Op1, Is.SameAs(local));
                Assert.That(result.AsOp().Op2, Is.Not.SameAs(local));
                Assert.That(result.AsOp().Op2.AsLclVar().LclNum, Is.EqualTo(local.LclNum));
            }
        });
    }

    [Test]
    public static void NegationMotionTruncatesIntConstantsAndDropsFieldAnnotations()
    {
        WithCompiler(compiler => {
            var local = compiler.gtNewLclvNode(TYP_INT, 1);
            var negation = compiler.gtNewUnaryNode(GT_NEG, TYP_INT, local);
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.isFieldStatic = &IsFieldStatic;
            ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;
            var fields = new FieldSeq((CORINFO_FIELD_STRUCT_*)0x1000, 0, FieldSeq.FieldKind.SharedStatic);
            var constant = new GenTreeIntCon(TYP_INT, int.MinValue, fields);
            var multiply = compiler.gtNewBinaryNode(GT_MUL, TYP_INT, negation, constant);
            _ = compiler.fgOptimizeMultiply(multiply);
            Assert.That(constant.FieldSeq, Is.Null);
            Assert.That(multiply.Oper, Is.EqualTo(GT_LSH));
            Assert.That(multiply.Op1.Oper, Is.EqualTo(GT_NEG));
            Assert.That(multiply.Op1.AsUnOp().Op1, Is.SameAs(local));
            Assert.That(constant.IconValue, Is.EqualTo((nint)31));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void FloatingXorRecognizesOnlyNegativeZero(bool negative)
    {
        WithCompiler(compiler => {
            var local = compiler.gtNewLclvNode(TYP_DOUBLE, 2);
            var xor = compiler.gtNewBinaryNode(GT_XOR, TYP_DOUBLE, local,
                compiler.gtNewDconNode(TYP_DOUBLE, negative ? -0.0 : 0.0));
            xor._vnPair.SetBoth(42);
            var result = compiler.fgOptimizeBitwiseXor(xor);
            if (negative)
            {
                Assert.That(result, Is.TypeOf<GenTreeUnOp>());
                Assert.That(result!.Oper, Is.EqualTo(GT_NEG));
                Assert.That(result.AsUnOp().Op1, Is.SameAs(local));
                Assert.That(result._vnPair.Liberal, Is.EqualTo(42));
                Assert.That(xor.Oper, Is.EqualTo(GT_XOR));
            }
            else
            {
                Assert.That(result, Is.Null);
            }
        });
    }

    [Test]
    public static void BitwiseBooleanAndComplementTransformsKeepTheirNativeShapes()
    {
        WithCompiler(compiler => {
            var value = compiler.gtNewLclvNode(TYP_INT, 1);
            var complement = compiler.gtNewBinaryNode(GT_XOR, TYP_INT, value, compiler.gtNewIconNode(TYP_INT, -1));
            complement._vnPair.SetBoth(42);
            var result = compiler.fgOptimizeBitwiseXor(complement);
            Assert.That(result, Is.TypeOf<GenTreeUnOp>());
            Assert.That(result!.Oper, Is.EqualTo(GT_NOT));
            Assert.That(result.AsUnOp().Op1, Is.SameAs(value));
            Assert.That(result._vnPair.Liberal, Is.EqualTo(42));

            var comparison = compiler.gtNewBinaryNode(GT_EQ, TYP_INT, value, compiler.gtNewIconNode(TYP_INT, 0));
            var and = compiler.gtNewBinaryNode(GT_AND, TYP_INT, comparison, compiler.gtNewIconNode(TYP_INT, 1));
            Assert.That(Compiler.fgOptimizeBitwiseAnd(and), Is.SameAs(comparison));
            var xor = compiler.gtNewBinaryNode(GT_XOR, TYP_INT, comparison, compiler.gtNewIconNode(TYP_INT, 1));
            Assert.That(compiler.fgOptimizeBitwiseXor(xor), Is.SameAs(comparison));
            Assert.That(comparison.Oper, Is.EqualTo(GT_NE));
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    public static void CommaThrowPropagationPreservesPrecedingEffectsAndConditionalReachability(bool effects, bool conditional)
    {
        WithCompiler(compiler => {
            var throwing = compiler.gtNewHelperCallNode(TYP_VOID, CorInfoHelpFunc.CORINFO_HELP_OVERFLOW);
            var zero = compiler.gtNewIconNode(TYP_INT, 0);
            var comma = compiler.gtNewCommaNode(TYP_INT, throwing, zero);
            if (conditional)
            {
                comma.Flags |= GTF_COLON_COND;
            }

            var parent = compiler.gtNewCastNode(TYP_DOUBLE, comma, false, TYP_DOUBLE);
            var result = compiler.fgPropagateCommaThrow(parent, comma, effects ? GTF_CALL : GTF_EMPTY);
            Assert.That(compiler.fgRemoveRestOfBlock, Is.EqualTo(!conditional));
            if (effects)
            {
                Assert.That(result, Is.Null);
                Assert.That(comma.Op2, Is.SameAs(zero));
            }
            else
            {
                Assert.That(result, Is.SameAs(comma));
                Assert.That(comma.Type, Is.EqualTo(TYP_DOUBLE));
                Assert.That(comma.Op1, Is.SameAs(throwing));
                Assert.That(comma.Op2, Is.TypeOf<GenTreeDblCon>());
                Assert.That(comma.Op2.AsDblCon().DconVal, Is.Zero);
                Assert.That(zero.Type, Is.EqualTo(TYP_INT));
            }
        });
    }

    [TestCase(4, false)]
    [TestCase(8, true)]
    public static void ReturnedStructFieldsReplaceOnlyWholeLocals(int size, bool replace)
    {
        WithCompiler(compiler => {
            var field = new GenTreeLclFld(GT_LCL_FLD, TYP_STRUCT, 0, 0, new ClassLayout(size));
            field._vnPair.SetBoth(42);
            var ret = compiler.gtNewUnaryNode(GT_RETURN, TYP_STRUCT, field);
            ret.Op1 = compiler.fgMorphRetInd(ret);
            Assert.That(ret.Op1.Oper, Is.EqualTo(replace ? GT_LCL_VAR : GT_LCL_FLD));
            Assert.That(compiler.lvaTable[0].lvDoNotEnregister, Is.EqualTo(!replace));
            Assert.That(field.Oper, Is.EqualTo(GT_LCL_FLD));
            if (replace)
            {
                Assert.That(ret.Op1.Type, Is.EqualTo(TYP_LONG));
                Assert.That(ret.Op1._vnPair, Is.EqualTo(new ValueNumPair()));
#if DEBUG
                Assert.That(ret.Op1.TreeId, Is.EqualTo(field.TreeId));
#endif
            }
        });
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsFieldStatic(ICorJitInfo* info, CORINFO_FIELD_STRUCT_* handle) => 1;

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
        compiler.opts.compFlags |= CLFLG_TREETRANS;
        compiler.fgGlobalMorph = true;
        compiler.lvaTable = [
            new LclVarDsc { Type = TYP_LONG },
            new LclVarDsc { Type = TYP_INT },
            new LclVarDsc { Type = TYP_DOUBLE },
        ];
        compiler.lvaCount = 3;
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
