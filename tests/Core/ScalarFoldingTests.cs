// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ScalarFoldingTests
{
    [TestCase(GT_ADD, 0, false, true)]
    [TestCase(GT_ADD, 0, true, true)]
    [TestCase(GT_SUB, 0, false, true)]
    [TestCase(GT_SUB, 0, true, false)]
    [TestCase(GT_MUL, 1, false, true)]
    [TestCase(GT_MUL, 1, true, true)]
    [TestCase(GT_DIV, 1, false, true)]
    [TestCase(GT_DIV, 1, true, false)]
    [TestCase(GT_UDIV, 1, false, true)]
    [TestCase(GT_OR, 0, true, true)]
    [TestCase(GT_LSH, 0, false, true)]
    [TestCase(GT_RSH, 0, false, true)]
    [TestCase(GT_RSZ, 0, false, true)]
    [TestCase(GT_ROL, 0, false, true)]
    [TestCase(GT_ROR, 0, false, true)]
    public static void IntegerIdentitiesPreserveOperandIdentity(genTreeOps oper, int value, bool constantFirst, bool folds)
    {
        WithCompiler(compiler => {
            var operand = compiler.gtNewLclvNode(TYP_INT, 0);
            var constant = compiler.gtNewIconNode(TYP_INT, value);
            var tree = compiler.gtNewBinaryNode(oper, TYP_INT,
                constantFirst ? constant : operand, constantFirst ? operand : constant);
            Assert.That(compiler.gtFoldExpr(tree), Is.SameAs(folds ? operand : tree));
        });
    }

    [TestCase(GT_MUL)]
    [TestCase(GT_AND)]
    [TestCase(GT_LSH)]
    [TestCase(GT_RSH)]
    [TestCase(GT_RSZ)]
    [TestCase(GT_ROL)]
    [TestCase(GT_ROR)]
    public static void ZeroResultsPreserveEffectsAndConstantIdentity(genTreeOps oper)
    {
        WithCompiler(compiler => {
            var call = compiler.gtNewCallNode(TYP_INT, gtCallTypes.CT_USER_FUNC, null);
            var zero = compiler.gtNewIconNode(TYP_INT, 0);
            var tree = compiler.gtNewBinaryNode(oper, TYP_INT, zero, call);
            var result = compiler.gtFoldExpr(tree);
            Assert.That(result.Oper, Is.EqualTo(GT_COMMA));
            Assert.That(result.AsOp().Op1, Is.SameAs(call));
            Assert.That(result.AsOp().Op2, Is.SameAs(zero));
            Assert.That(result.Flags & GTF_CALL, Is.EqualTo(GTF_CALL));
        });
    }

    [TestCase(GT_LE, true, 1)]
    [TestCase(GT_GE, false, 1)]
    [TestCase(GT_LT, false, 0)]
    [TestCase(GT_GT, true, 0)]
    public static void UnsignedZeroComparisonsRetainEffectsAndJumpRoot(genTreeOps oper, bool constantFirst, int expected)
    {
        WithCompiler(compiler => {
            var call = compiler.gtNewCallNode(TYP_INT, gtCallTypes.CT_USER_FUNC, null);
            var zero = compiler.gtNewIconNode(TYP_INT, 0);
            var tree = compiler.gtNewBinaryNode(oper, TYP_INT,
                constantFirst ? zero : call, constantFirst ? call : zero);
            tree.Flags |= GTF_UNSIGNED | GTF_RELOP_JMP_USED;
            Assert.That(compiler.gtFoldExpr(tree), Is.SameAs(tree));
            tree.Flags &= ~GTF_RELOP_JMP_USED;
            var result = compiler.gtFoldExpr(tree);
            Assert.That(result.Oper, Is.EqualTo(GT_COMMA));
            Assert.That(result.AsOp().Op1, Is.SameAs(call));
            Assert.That(result.AsOp().Op2.AsIntCon().IconValue, Is.EqualTo((nint)expected));
        });
    }

    [TestCase(false, 0xFFL, TYP_UBYTE)]
    [TestCase(false, 0xFFFFL, TYP_USHORT)]
    [TestCase(true, 0xFFL, TYP_UBYTE)]
    [TestCase(true, 0xFFFFL, TYP_USHORT)]
    [TestCase(true, 0xFFFFFFFFL, TYP_UINT)]
    public static void IntegerMasksBecomeMorphedZeroExtensions(bool wide, long mask, var_types castType)
    {
        WithCompiler(compiler => {
            compiler.fgGlobalMorph = true;
            var type = wide ? TYP_LONG : TYP_INT;
            var operand = compiler.gtNewLclvNode(type, 0);
            var tree = compiler.gtNewBinaryNode(GT_AND, type, operand, compiler.gtNewIconNode(type, (nint)mask));
            var result = compiler.gtFoldExpr(tree);
            Assert.That(result.Oper, Is.EqualTo(GT_CAST));
            Assert.That(result.Type, Is.EqualTo(type));
#if DEBUG
            Assert.That(result.WasMorphed, Is.True);
#endif
            if (wide)
            {
                Assert.That(result.Flags & GTF_UNSIGNED, Is.EqualTo(GTF_UNSIGNED));
                Assert.That(result.AsCast().CastType, Is.EqualTo(TYP_LONG));
                result = result.AsCast().CastOp;
            }

            Assert.That(result.Type, Is.EqualTo(TYP_INT));
            Assert.That(result.AsCast().CastType, Is.EqualTo(castType));
            Assert.That(result.AsCast().CastOp, Is.SameAs(operand));
        });
    }

    [TestCase(GT_EQ, 1)]
    [TestCase(GT_LE, 1)]
    [TestCase(GT_GE, 1)]
    [TestCase(GT_NE, 0)]
    [TestCase(GT_LT, 0)]
    [TestCase(GT_GT, 0)]
    public static void IdenticalComparisonsKeepLinksAndAllocateNewIdentity(genTreeOps oper, int expected)
    {
        WithCompiler(compiler => {
            var tree = compiler.gtNewBinaryNode(oper, TYP_INT,
                compiler.gtNewLclvNode(TYP_INT, 0), compiler.gtNewLclvNode(TYP_INT, 0));
            tree.Prev = compiler.gtNewIconNode(TYP_INT, 42);
            tree.Next = compiler.gtNewIconNode(TYP_INT, 43);
#if DEBUG
            var nextId = compiler.compGenTreeID;
#endif
            var result = compiler.gtFoldExpr(tree);
            Assert.That(result.AsIntCon().IconValue, Is.EqualTo((nint)expected));
            Assert.That(result.Prev, Is.SameAs(tree.Prev));
            Assert.That(result.Next, Is.SameAs(tree.Next));
#if DEBUG
            Assert.That(result.TreeId, Is.EqualTo(nextId));
            Assert.That(compiler.compGenTreeID, Is.EqualTo(nextId + 1));
#endif
        }, minOpts: true);
    }

    [TestCase(TYP_DOUBLE, GTF_EMPTY, GTF_EMPTY, false)]
    [TestCase(TYP_INT, GTF_EXCEPT, GTF_EMPTY, false)]
    [TestCase(TYP_INT, GTF_ORDER_SIDEEFF, GTF_EMPTY, true)]
    [TestCase(TYP_INT, GTF_EMPTY, GTF_ORDER_SIDEEFF, false)]
    [TestCase(TYP_INT, GTF_ORDER_SIDEEFF, GTF_ORDER_SIDEEFF, false)]
    public static void IdenticalComparisonsPreserveNaNsEffectsAndOrder(var_types type, GenTreeFlags firstFlags,
        GenTreeFlags secondFlags, bool folds)
    {
        WithCompiler(compiler => {
            var first = compiler.gtNewLclvNode(type, 0);
            var second = compiler.gtNewLclvNode(type, 0);
            first.Flags |= firstFlags;
            second.Flags |= secondFlags;
            var tree = compiler.gtNewBinaryNode(GT_EQ, TYP_INT, first, second);
            Assert.That(compiler.gtFoldExpr(tree).Oper, Is.EqualTo(folds ? GT_CNS_INT : GT_EQ));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ConstantSelectsFoldSelectedComparisons(bool condition)
    {
        WithCompiler(compiler => {
            var compare = compiler.gtNewBinaryNode(GT_EQ, TYP_INT,
                compiler.gtNewLclvNode(TYP_INT, 0), compiler.gtNewLclvNode(TYP_INT, 0));
            var other = compiler.gtNewIconNode(TYP_INT, 42);
            var tree = new GenTreeConditional(GT_SELECT, TYP_INT, compiler.gtNewIconNode(TYP_INT, condition ? 1 : 0),
                condition ? compare : other, condition ? other : compare) {
                Next = other,
            };
            var result = compiler.gtFoldExpr(tree);
            Assert.That(result.AsIntCon().IconValue, Is.EqualTo((nint)1));
            Assert.That(result.Next, Is.SameAs(other));
        });
    }

    [TestCase(GTF_EMPTY, GTF_EMPTY, true)]
    [TestCase(GTF_EXCEPT, GTF_EMPTY, false)]
    [TestCase(GTF_ORDER_SIDEEFF, GTF_EMPTY, true)]
    [TestCase(GTF_EMPTY, GTF_ORDER_SIDEEFF, false)]
    [TestCase(GTF_ORDER_SIDEEFF, GTF_ORDER_SIDEEFF, false)]
    public static void IdenticalSelectArmsRespectEffects(GenTreeFlags firstFlags, GenTreeFlags secondFlags, bool folds)
    {
        WithCompiler(compiler => {
            var first = compiler.gtNewLclvNode(TYP_INT, 1);
            var second = compiler.gtNewLclvNode(TYP_INT, 1);
            first.Flags |= firstFlags;
            second.Flags |= secondFlags;
            var condition = compiler.gtNewBinaryNode(GT_EQ, TYP_INT,
                compiler.gtNewLclvNode(TYP_INT, 0), compiler.gtNewIconNode(TYP_INT, 0));
            var tree = new GenTreeConditional(GT_SELECT, TYP_INT, condition, first, second);
            tree.Flags |= firstFlags | secondFlags;
            Assert.That(compiler.gtFoldExpr(tree), Is.SameAs(folds ? first : tree));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void QmarkFoldingPreservesNestedConditionalExecution(bool conditionallyExecuted)
    {
        WithCompiler(compiler => {
            var condition = compiler.gtNewLclvNode(TYP_INT, 0);
            var thenNode = compiler.gtNewLclvNode(TYP_INT, 1);
            var elseNode = compiler.gtNewLclvNode(TYP_INT, 2);
            var colon = new GenTreeColon(TYP_INT, thenNode, elseNode);
            var selected = new GenTreeQmark(TYP_INT, condition, colon);
            selected.Flags |= GTF_COLON_COND;
            condition.Flags |= GTF_COLON_COND;
            colon.Flags |= GTF_COLON_COND;
            thenNode.Flags |= GTF_COLON_COND;
            elseNode.Flags |= GTF_COLON_COND;
            var tree = new GenTreeQmark(TYP_INT, compiler.gtNewIconNode(TYP_INT, 1),
                new GenTreeColon(TYP_INT, selected, compiler.gtNewIconNode(TYP_INT, 0)));
            tree.Flags |= conditionallyExecuted ? GTF_COLON_COND : GTF_EMPTY;
            Assert.That(compiler.gtFoldExpr(tree), Is.SameAs(selected));
            var expected = conditionallyExecuted ? GTF_COLON_COND : GTF_EMPTY;
            Assert.That(selected.Flags & GTF_COLON_COND, Is.EqualTo(expected));
            Assert.That(condition.Flags & GTF_COLON_COND, Is.EqualTo(expected));
            Assert.That(colon.Flags & GTF_COLON_COND, Is.EqualTo(GTF_COLON_COND));
            Assert.That(thenNode.Flags & GTF_COLON_COND, Is.EqualTo(GTF_COLON_COND));
            Assert.That(elseNode.Flags & GTF_COLON_COND, Is.EqualTo(GTF_COLON_COND));
        });
    }

    [TestCase(GT_EQ, false, false)]
    [TestCase(GT_NE, true, false)]
    [TestCase(GT_GT, false, true)]
    [TestCase(GT_GT, false, false)]
    public static void NullableBoxComparisonReadsHasValue(genTreeOps oper, bool constantFirst, bool unsigned)
    {
        WithCompiler(compiler => {
            Assert.That(OFFSETOF__CORINFO_NullableOfT__hasValue, Is.Zero);
            compiler.lvaTable = [new LclVarDsc { Type = TYP_BYREF }];
            compiler.lvaCount = 1;
            var call = compiler.gtNewCallNode(TYP_REF, gtCallTypes.CT_HELPER,
                Compiler.eeFindHelper(CorInfoHelpFunc.CORINFO_HELP_BOX_NULLABLE));
            var address = compiler.gtNewLclvNode(TYP_BYREF, 0);
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_I_IMPL, 42)));
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(address));
            var zero = compiler.gtNewNull();
            var tree = compiler.gtNewBinaryNode(oper, TYP_INT,
                constantFirst ? zero : call, constantFirst ? call : zero);
            tree.Flags |= unsigned ? GTF_UNSIGNED : GTF_EMPTY;
            Assert.That(compiler.gtFoldExpr(tree), Is.SameAs(tree));
            var operand = constantFirst ? tree.Op2 : tree.Op1;
            if ((oper == GT_GT) && !unsigned)
            {
                Assert.That(operand, Is.SameAs(call));
                return;
            }

            Assert.That(operand.Oper, Is.EqualTo(GT_IND));
            Assert.That(operand.Type, Is.EqualTo(TYP_UBYTE));
            Assert.That(operand.AsIndir().Addr, Is.SameAs(address));
            Assert.That(zero.Type, Is.EqualTo(TYP_INT));
        });
    }

    [Test]
    public static void NullableBoxComparisonDoesNotRemoveCompletedArguments()
    {
        WithCompiler(compiler => {
            var call = compiler.gtNewCallNode(TYP_REF, gtCallTypes.CT_HELPER,
                Compiler.eeFindHelper(CorInfoHelpFunc.CORINFO_HELP_BOX_NULLABLE));
            var flags = typeof(CallArgs).GetField("_flags", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Missing call argument flags.");
            var flagsType = typeof(CallArgs).GetNestedType("Flags", BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Missing call argument flag type.");
            object args = call.Args;
            flags.SetValue(args, Enum.Parse(flagsType, "ArgsComplete"));
            call.Args = (CallArgs)args;
            var tree = compiler.gtNewBinaryNode(GT_EQ, TYP_INT, call, compiler.gtNewNull());
            Assert.That(compiler.gtFoldExpr(tree), Is.SameAs(tree));
            Assert.That(tree.Op1, Is.SameAs(call));
        });
    }

    [Test]
    public static void OneConstantFoldingRespectsTypeAndOptimizationGates()
    {
        WithCompiler(compiler => {
            var pointer = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var tree = compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF, pointer, compiler.gtNewIconNode(TYP_I_IMPL, 0));
            Assert.That(compiler.gtFoldExpr(tree), Is.SameAs(tree));
        });
        WithCompiler(compiler => {
            var local = compiler.gtNewLclvNode(TYP_INT, 0);
            var tree = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, local, compiler.gtNewIconNode(TYP_INT, 0));
            Assert.That(compiler.gtFoldExpr(tree), Is.SameAs(tree));
        }, minOpts: true);
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ConstantReplacementRefreshesBothValueNumbers(bool wide)
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var type = wide ? TYP_LONG : TYP_INT;
            var tree = compiler.gtNewBinaryNode(GT_ADD, type,
                compiler.gtNewIconNode(type, 2), compiler.gtNewIconNode(type, 3));
            tree._vnPair = new(store.VNForExpr(null, type), store.VNForExpr(null, type));
            var result = compiler.gtFoldExpr(tree);
            var expected = wide ? store.VNForLongCon(5) : store.VNForIntCon(5);
            Assert.That(result._vnPair.Liberal, Is.EqualTo(expected));
            Assert.That(result._vnPair.Conservative, Is.EqualTo(expected));
            Assert.That(tree._vnPair.Liberal, Is.Not.EqualTo(expected));
        });
    }

    [Test]
    public static void TreeConstantNumbersPreserveReferenceZerosAndFloatingBits()
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var byrefZero = compiler.gtNewIconNode(TYP_BYREF, 0);
            compiler.fgValueNumberTreeConst(byrefZero);
            Assert.That(byrefZero._vnPair.Liberal, Is.EqualTo(ValueNumStore.VNForNull()));
            Assert.That(byrefZero._vnPair.Liberal, Is.Not.EqualTo(store.VNZeroForType(TYP_BYREF)));
            var byref = compiler.gtNewIconNode(TYP_BYREF, -1);
            compiler.fgValueNumberTreeConst(byref);
            Assert.That(store.ConstantValue<nuint>(byref._vnPair.Liberal), Is.EqualTo(nuint.MaxValue));
            var obj = compiler.gtNewIconNode(TYP_REF, 42);
            obj.Flags |= GTF_ICON_OBJ_HDL;
            compiler.fgValueNumberTreeConst(obj);
            Assert.That(store.IsVNObjHandle(obj._vnPair.Liberal), Is.True);
            var nullObj = compiler.gtNewNull();
            compiler.fgValueNumberTreeConst(nullObj);
            Assert.That(nullObj._vnPair.Liberal, Is.EqualTo(ValueNumStore.VNForNull()));
            var negativeZero = compiler.gtNewDconNode(TYP_DOUBLE, -0.0);
            compiler.fgValueNumberTreeConst(negativeZero);
            Assert.That(BitConverter.DoubleToInt64Bits(store.GetConstantDouble(negativeZero._vnPair.Liberal)), Is.EqualTo(long.MinValue));
            var rounded = compiler.gtNewDconNode(TYP_FLOAT, 16777217.0);
            compiler.fgValueNumberTreeConst(rounded);
            Assert.That(store.GetConstantSingle(rounded._vnPair.Liberal), Is.EqualTo(16777216.0f));
        });
    }

    [Test]
    public static void UnknownCompileTimeHandlesDoNotPoisonEmbeddedHandleMappings()
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var embedded = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            embedded.Flags |= GTF_ICON_CLASS_HDL;
            embedded.CompileTimeHandle = 0x2000;
            compiler.fgValueNumberTreeConst(embedded);
            var reflagged = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            reflagged.Flags |= GTF_ICON_CLASS_HDL;
            compiler.fgValueNumberTreeConst(reflagged);
            nint handle = 42;
            Assert.That(store.EmbeddedHandleMapLookup(0x1000, ref handle), Is.True);
            Assert.That(handle, Is.EqualTo((nint)0x2000));
            Assert.That(store.EmbeddedHandleMapLookup(0x3000, ref handle), Is.False);
            Assert.That(handle, Is.EqualTo((nint)0x2000));
            Assert.That(reflagged._vnPair, Is.EqualTo(embedded._vnPair));
        });
    }

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

    [TestCase(false)]
    [TestCase(true)]
    public static void GlobalMorphOverflowCreatesAnOrderedThrowAndZero(bool valueNumbered)
    {
        WithCompiler(compiler => {
            compiler.fgGlobalMorph = true;
            if (valueNumbered)
            {
                compiler.vnStore = new ValueNumStore(compiler);
            }

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
            if (compiler.vnStore is ValueNumStore store)
            {
                var helper = result.AsOp().Op1;
                Assert.That(helper._vnPair.BothEqual(), Is.True);
                store.VNUnpackExc(helper._vnPair.Liberal, out var normal, out var exceptions);
                Assert.That(normal, Is.EqualTo(ValueNumStore.VNForVoid()));
                var overflow = store.VNForFunc(TYP_REF, VNFunc.VNF_OverflowExc, ValueNumStore.VNForVoid());
                Assert.That(exceptions, Is.EqualTo(store.VNExcSetSingleton(overflow)));
                Assert.That(result.AsOp().Op2._vnPair.Liberal, Is.EqualTo(store.VNForLongCon(0)));
                Assert.That(result.AsOp().Op2._vnPair.BothEqual(), Is.True);
            }
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

    [TestCase(TYP_DOUBLE, GT_ADD, -0.0, false, true)]
    [TestCase(TYP_DOUBLE, GT_ADD, -0.0, true, true)]
    [TestCase(TYP_DOUBLE, GT_ADD, 0.0, false, false)]
    [TestCase(TYP_DOUBLE, GT_ADD, 0.0, true, false)]
    [TestCase(TYP_FLOAT, GT_ADD, -0.0, false, true)]
    [TestCase(TYP_FLOAT, GT_ADD, 0.0, false, false)]
    [TestCase(TYP_DOUBLE, GT_SUB, 0.0, false, true)]
    [TestCase(TYP_DOUBLE, GT_SUB, -0.0, false, false)]
    [TestCase(TYP_DOUBLE, GT_SUB, 0.0, true, false)]
    [TestCase(TYP_DOUBLE, GT_MUL, 1.0, false, true)]
    [TestCase(TYP_DOUBLE, GT_MUL, 1.0, true, true)]
    [TestCase(TYP_FLOAT, GT_MUL, 1.0, false, true)]
    [TestCase(TYP_DOUBLE, GT_MUL, 0.0, false, false)]
    [TestCase(TYP_DOUBLE, GT_MUL, -0.0, false, false)]
    [TestCase(TYP_DOUBLE, GT_DIV, 1.0, false, true)]
    [TestCase(TYP_DOUBLE, GT_DIV, 1.0, true, false)]
    public static void FloatingIdentitiesRetainSignedZero(var_types type, genTreeOps oper,
        double value, bool constantFirst, bool folds)
    {
        WithCompiler(compiler => {
            var operand = compiler.gtNewLclvNode(type, 0);
            var constant = compiler.gtNewDconNode(type, value);
            var tree = compiler.gtNewBinaryNode(oper, type,
                constantFirst ? constant : operand, constantFirst ? operand : constant);
            Assert.That(compiler.gtFoldExpr(tree), Is.SameAs(folds ? operand : tree));
        });
    }

    [TestCase(GT_ADD, false)]
    [TestCase(GT_ADD, true)]
    [TestCase(GT_SUB, false)]
    [TestCase(GT_SUB, true)]
    [TestCase(GT_MUL, false)]
    [TestCase(GT_MUL, true)]
    [TestCase(GT_DIV, false)]
    [TestCase(GT_DIV, true)]
    public static void FloatingNaNFoldsPreserveSideEffects(genTreeOps oper, bool constantFirst)
    {
        WithCompiler(compiler => {
            var call = compiler.gtNewCallNode(TYP_DOUBLE, gtCallTypes.CT_USER_FUNC, null);
            var nan = compiler.gtNewDconNode(TYP_DOUBLE, BitConverter.Int64BitsToDouble(unchecked((long)0xFFF8000000000123)));
            var tree = compiler.gtNewBinaryNode(oper, TYP_DOUBLE, constantFirst ? nan : call, constantFirst ? call : nan);
            var result = compiler.gtFoldExpr(tree);
            Assert.That(result.Oper, Is.EqualTo(GT_COMMA));
            Assert.That(result.AsOp().Op1, Is.SameAs(call));
            Assert.That(result.AsOp().Op2, Is.SameAs(nan));
            Assert.That(result.Flags & GTF_CALL, Is.EqualTo(GTF_CALL));
        });
    }

    [TestCase(GT_EQ, false)]
    [TestCase(GT_EQ, true)]
    [TestCase(GT_NE, false)]
    [TestCase(GT_NE, true)]
    [TestCase(GT_LT, false)]
    [TestCase(GT_LT, true)]
    [TestCase(GT_LE, false)]
    [TestCase(GT_LE, true)]
    [TestCase(GT_GT, false)]
    [TestCase(GT_GT, true)]
    [TestCase(GT_GE, false)]
    [TestCase(GT_GE, true)]
    public static void FloatingNaNComparisonsRespectUnorderedFlags(genTreeOps oper, bool unordered)
    {
        WithCompiler(compiler => {
            var operand = compiler.gtNewLclvNode(TYP_FLOAT, 0);
            var nan = compiler.gtNewDconNode(TYP_FLOAT, float.NaN);
            var tree = compiler.gtNewBinaryNode(oper, TYP_INT, operand, nan);
            tree.Flags |= unordered ? GTF_RELOP_NAN_UN : GTF_EMPTY;
            Assert.That(compiler.gtFoldExpr(tree).AsIntCon().IconValue, Is.EqualTo(unordered ? (nint)1 : 0));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void FloatingNaNComparisonsPreserveEffectsAndJumpRoots(bool jumpUsed)
    {
        WithCompiler(compiler => {
            var call = compiler.gtNewCallNode(TYP_DOUBLE, gtCallTypes.CT_USER_FUNC, null);
            var tree = compiler.gtNewBinaryNode(GT_NE, TYP_INT, call, compiler.gtNewDconNode(TYP_DOUBLE, double.NaN));
            tree.Flags |= GTF_RELOP_NAN_UN;
            tree.Flags |= jumpUsed ? GTF_RELOP_JMP_USED : GTF_EMPTY;
            var result = compiler.gtFoldExpr(tree);

            if (jumpUsed)
            {
                Assert.That(result, Is.SameAs(tree));
            }
            else
            {
                Assert.That(result.Oper, Is.EqualTo(GT_COMMA));
                Assert.That(result.AsOp().Op1, Is.SameAs(call));
                Assert.That(result.AsOp().Op2.AsIntCon().IconValue, Is.EqualTo((nint)1));
                Assert.That(result.Flags & GTF_CALL, Is.EqualTo(GTF_CALL));
            }
        });
    }

    [Test]
    public static void FloatingConstantOperandFoldingRespectsTier0()
    {
        WithCompiler(compiler => {
            Assert.That(compiler.opts.Tier0OptimizationEnabled, Is.True);
            var operand = compiler.gtNewLclvNode(TYP_DOUBLE, 0);
            var tree = compiler.gtNewBinaryNode(GT_MUL, TYP_DOUBLE, operand, compiler.gtNewDconNode(TYP_DOUBLE, 1));
            Assert.That(compiler.gtFoldExpr(tree), Is.SameAs(tree));
        }, minOpts: true);
    }

    [Test]
    public static void FloatingCommaDoesNotTreatItsValueAsArithmetic()
    {
        WithCompiler(compiler => {
            var tree = compiler.gtNewBinaryNode(GT_COMMA, TYP_INT,
                compiler.gtNewDconNode(TYP_DOUBLE, double.NaN), compiler.gtNewLclvNode(TYP_INT, 0));
            Assert.That(compiler.gtFoldExpr(tree), Is.SameAs(tree));
        });
    }

#if DEBUG
    [TestCase(long.MinValue, " -0x8000000000000000")]
    [TestCase(long.MinValue + 1, " -0x7fffffffffffffff")]
    [TestCase(-1000L, " -0x3e8")]
    [TestCase(long.MaxValue, " 0x7fffffffffffffff")]
    public static void IntegerDumpPreservesUnsignedHexMagnitude(long value, string expected)
    {
        WithCompiler(compiler => {
            using var stream = new MemoryStream();
            using var writer = new JitTextWriter(stream, leaveOpen: true);
            var previous = s_jitstdout;

            try
            {
                s_jitstdout = writer;
                compiler.gtDispConst(compiler.gtNewLconNode(value));
                writer.Flush();
            }
            finally
            {
                s_jitstdout = previous;
            }

            Assert.That(Encoding.UTF8.GetString(stream.ToArray()), Is.EqualTo(expected));
        });
    }
#endif

    private static void WithCompiler(Action<Compiler> action, bool minOpts = false)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minOpts);
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
