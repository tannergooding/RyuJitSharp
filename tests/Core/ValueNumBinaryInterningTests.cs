// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ValueNumBinaryInterningTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "VNEvalFoldTypeCompare")]
    private static extern int FoldTypeCompare(ValueNumStore store, var_types type, VNFunc func, int left, int right);

    [TestCase(VNFunc.VNF_ADD)]
    [TestCase(VNFunc.VNF_MUL)]
    [TestCase(VNFunc.VNF_AND)]
    [TestCase(VNFunc.VNF_OR)]
    public static void CommutativeFunctionsInternConstantsLast(VNFunc func)
    {
        WithStore((store, _) => {
            var constant = store.VNForIntCon(7);
            var symbolic = store.VNForExpr(null, TYP_INT);
            var first = store.VNForFunc(TYP_INT, func, constant, symbolic);
            Assert.That(first, Is.EqualTo(store.VNForFunc(TYP_INT, func, symbolic, constant)));
            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(first, ref app), Is.True);
            Assert.That(app.GetArg(0), Is.EqualTo(symbolic));
            Assert.That(app.GetArg(1), Is.EqualTo(constant));
        });
    }

    [Test]
    public static void EqualityRetainsOperandOrder()
    {
        WithStore((store, _) => {
            var constant = store.VNForIntCon(7);
            var symbolic = store.VNForExpr(null, TYP_INT);
            var left = store.VNForFunc(TYP_INT, VNFunc.VNF_EQ, constant, symbolic);
            var right = store.VNForFunc(TYP_INT, VNFunc.VNF_EQ, symbolic, constant);
            Assert.That(left, Is.Not.EqualTo(right));
        });
    }

    [Test]
    public static void HandlesRemainAheadOfNonHandleConstants()
    {
        WithStore((store, compiler) => {
            compiler.opts.compReloc = true;
            var handle = store.VNForHandle(0x1000, GTF_ICON_CLASS_HDL);
            var constant = store.VNForLongCon(3);
            var result = store.VNForFunc(TYP_LONG, VNFunc.VNF_ADD, constant, handle);
            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(result, ref app), Is.True);
            Assert.That(app.GetArg(0), Is.EqualTo(handle));
            Assert.That(app.GetArg(1), Is.EqualTo(constant));
            Assert.That(store.VNForFunc(TYP_LONG, VNFunc.VNF_ADD, handle, constant), Is.EqualTo(result));
        });
    }

    [TestCase(VNFunc.VNF_DIV, int.MinValue, -1)]
    [TestCase(VNFunc.VNF_MOD, int.MinValue, -1)]
    [TestCase(VNFunc.VNF_DIV, 7, 0)]
    [TestCase(VNFunc.VNF_ADD_OVF, int.MaxValue, 1)]
    public static void UnsafeConstantFoldsRemainInternedApplications(VNFunc func, int left, int right)
    {
        WithStore((store, _) => {
            var lhs = store.VNForIntCon(left);
            var rhs = store.VNForIntCon(right);
            var vn = store.VNForFunc(TYP_INT, func, lhs, rhs);
            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(vn, ref app), Is.True);
            Assert.That(app.Func, Is.EqualTo(func));
            Assert.That(store.VNForFunc(TYP_INT, func, lhs, rhs), Is.EqualTo(vn));
        });
    }

    [TestCase(VNFunc.VNF_ADD, 4, 5, 9)]
    [TestCase(VNFunc.VNF_SUB, 4, 5, -1)]
    [TestCase(VNFunc.VNF_GT_UN, -1, 1, 1)]
    [TestCase(VNFunc.VNF_XOR, 7, 7, 0)]
    public static void SafeScalarConstantsFold(VNFunc func, int left, int right, int expected)
    {
        WithStore((store, _) => {
            var result = store.VNForFunc(TYP_INT, func, store.VNForIntCon(left), store.VNForIntCon(right));
            Assert.That(store.GetConstantInt32(result), Is.EqualTo(expected));
        });
    }

    [Test]
    public static void IdentityPreservesIntegerAndFloatZeroDistinction()
    {
        WithStore((store, _) => {
            var integer = store.VNForExpr(null, TYP_INT);
            var zero = store.VNForIntCon(0);
            Assert.That(store.VNForFunc(TYP_INT, VNFunc.VNF_ADD, zero, integer), Is.EqualTo(integer));
            Assert.That(store.VNForFunc(TYP_INT, VNFunc.VNF_SUB, integer, integer), Is.EqualTo(zero));
            Assert.That(store.VNForFunc(TYP_INT, VNFunc.VNF_OR, integer, zero), Is.EqualTo(integer));

            var floating = store.VNForExpr(null, TYP_DOUBLE);
            var positiveZero = store.VNForDoubleCon(0.0);
            var negativeZero = store.VNForDoubleCon(-0.0);
            var addPositive = store.VNForFunc(TYP_DOUBLE, VNFunc.VNF_ADD, floating, positiveZero);
            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(addPositive, ref app), Is.True);
            Assert.That(store.VNForFunc(TYP_DOUBLE, VNFunc.VNF_ADD, floating, negativeZero),
                Is.EqualTo(floating));
            Assert.That(store.VNForFunc(TYP_DOUBLE, VNFunc.VNF_SUB, floating, positiveZero),
                Is.EqualTo(floating));
            Assert.That(store.VNForFunc(TYP_DOUBLE, VNFunc.VNF_SUB, floating, negativeZero),
                Is.Not.EqualTo(floating));
        });
    }

    [TestCase(VNFunc.VNF_OR)]
    [TestCase(VNFunc.VNF_AND)]
    public static void ZeroIdentityDoesNotAllocateAllBitsBeforeEarlyExit(VNFunc func)
    {
        WithStore((store, _) => {
            var symbolic = store.VNForExpr(null, TYP_INT);
            var zero = store.VNForIntCon(0);
            var result = store.VNForFunc(TYP_INT, func, symbolic, zero);
            Assert.That(result, Is.EqualTo(func == VNFunc.VNF_OR ? symbolic : zero));
            var marker = store.VNForIntCon(2);
            Assert.That(marker, Is.EqualTo(zero + 1));
            Assert.That(store.VNForIntCon(-1), Is.EqualTo(marker + 1));
        });
    }

    [TestCase(VNFunc.VNF_OR)]
    [TestCase(VNFunc.VNF_AND)]
    public static void IdempotentBitwiseIdentityAllocatesAllBitsBeforeEarlyExit(VNFunc func)
    {
        WithStore((store, _) => {
            var symbolic = store.VNForExpr(null, TYP_INT);
            Assert.That(store.VNForFunc(TYP_INT, func, symbolic, symbolic), Is.EqualTo(symbolic));
            var marker = store.VNForIntCon(2);
            Assert.That(store.VNForIntCon(0), Is.LessThan(store.VNForIntCon(-1)));
            Assert.That(store.VNForIntCon(-1), Is.LessThan(marker));
        });
    }

    [TestCase(VNFunc.VNF_ADD)]
    [TestCase(VNFunc.VNF_SUB)]
    [TestCase(VNFunc.VNF_MUL)]
    public static void FloatingIdentitiesInternZeroBeforeAllocatingResult(VNFunc func)
    {
        WithStore((store, _) => {
            var left = store.VNForExpr(null, TYP_DOUBLE);
            var right = store.VNForExpr(null, TYP_DOUBLE);
            var result = store.VNForFunc(TYP_DOUBLE, func, left, right);
            var zero = store.VNForDoubleCon(0.0);
            Assert.That(zero, Is.LessThan(result));
            if (func == VNFunc.VNF_MUL)
            {
                Assert.That(store.VNForDoubleCon(1.0), Is.EqualTo(zero + 1));
            }
        });
    }

    [TestCase(VNFunc.VNF_MUL)]
    [TestCase(VNFunc.VNF_MUL_OVF)]
    [TestCase(VNFunc.VNF_MUL_UN_OVF)]
    public static void MultiplicationInternsZeroBeforeOneAndResult(VNFunc func)
    {
        WithStore((store, _) => {
            var left = store.VNForExpr(null, TYP_INT);
            var right = store.VNForExpr(null, TYP_INT);
            var result = store.VNForFunc(TYP_INT, func, left, right);
            var zero = store.VNForIntCon(0);
            var one = store.VNForIntCon(1);
            Assert.That(one, Is.EqualTo(zero + 1));
            Assert.That(one, Is.LessThan(result));
        });
    }

    [TestCase(VNFunc.VNF_EQ)]
    [TestCase(VNFunc.VNF_GE)]
    [TestCase(VNFunc.VNF_LE)]
    [TestCase(VNFunc.VNF_NE)]
    [TestCase(VNFunc.VNF_GT_UN)]
    [TestCase(VNFunc.VNF_LT_UN)]
    public static void FloatingComparisonsInternIntegerZeroBeforeResult(VNFunc func)
    {
        WithStore((store, _) => {
            var left = store.VNForExpr(null, TYP_DOUBLE);
            var right = store.VNForExpr(null, TYP_DOUBLE);
            var result = store.VNForFunc(TYP_INT, func, left, right);
            Assert.That(store.VNForIntCon(0), Is.LessThan(result));
        });
    }

    [Test]
    public static void IntegerIdentitiesRespectOverflowAndByrefExclusions()
    {
        WithStore((store, _) => {
            var x = store.VNForExpr(null, TYP_INT);
            var zero = store.VNForIntCon(0);
            Assert.That(store.VNForFunc(TYP_INT, VNFunc.VNF_ADD_OVF, x, zero), Is.EqualTo(x));
            Assert.That(store.VNForFunc(TYP_INT, VNFunc.VNF_SUB_OVF, x, x), Is.EqualTo(zero));
            Assert.That(store.VNForFunc(TYP_INT, VNFunc.VNF_MUL_UN_OVF, x, zero), Is.EqualTo(zero));
            Assert.That(store.VNForFunc(TYP_INT, VNFunc.VNF_DIV, x, store.VNForIntCon(1)),
                Is.EqualTo(x));
            Assert.That(store.VNForFunc(TYP_INT, VNFunc.VNF_LSH, x, zero), Is.EqualTo(x));

            var byref = store.VNForExpr(null, TYP_BYREF);
            var byrefResult = store.VNForFunc(TYP_BYREF, VNFunc.VNF_ADD, byref, store.VNForByrefCon(0));
            Assert.That(byrefResult, Is.Not.EqualTo(byref));
            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(byrefResult, ref app), Is.True);
        });
    }

    [Test]
    public static void ComplementIdentitiesPreserveAllBitsAndZero()
    {
        WithStore((store, _) => {
            var x = store.VNForExpr(null, TYP_INT);
            var complement = store.VNForFunc(TYP_INT, VNFunc.VNF_NOT, x);
            Assert.That(store.VNForFunc(TYP_INT, VNFunc.VNF_OR, x, complement),
                Is.EqualTo(store.VNForIntCon(-1)));
            Assert.That(store.VNForFunc(TYP_INT, VNFunc.VNF_AND, x, complement),
                Is.EqualTo(store.VNForIntCon(0)));
        });
    }

    [Test]
    public static void UnsignedNonNegativeComparisonsUseSignedRelations()
    {
        WithStore((store, _) => {
            var array = store.VNForExpr(null, TYP_REF);
            var length = store.VNForFunc(TYP_INT, VNFunc.VNF_ARR_LENGTH, array);
            var other = store.VNForIntCon(2);
            Assert.That(store.VNForFunc(TYP_INT, VNFunc.VNF_GT_UN, length, other),
                Is.EqualTo(store.VNForFunc(TYP_INT, VNFunc.VNF_GT, length, other)));
            Assert.That(store.VNForFunc(TYP_INT, VNFunc.VNF_GE, length, store.VNForIntCon(0)),
                Is.EqualTo(store.VNForIntCon(1)));
        });
    }

    [Test]
    public static void NestedSubtractionIdentitiesKeepNativeOperandOrder()
    {
        WithStore((store, _) => {
            var x = store.VNForExpr(null, TYP_INT);
            var three = store.VNForIntCon(3);
            var five = store.VNForIntCon(5);
            var plusThree = store.VNForFunc(TYP_INT, VNFunc.VNF_ADD, x, three);
            var plusFive = store.VNForFunc(TYP_INT, VNFunc.VNF_ADD, x, five);
            Assert.That(store.VNForFunc(TYP_INT, VNFunc.VNF_SUB, x, plusThree),
                Is.EqualTo(store.VNForIntCon(-3)));
            Assert.That(store.VNForFunc(TYP_INT, VNFunc.VNF_SUB, plusThree, x),
                Is.EqualTo(three));
            Assert.That(store.VNForFunc(TYP_INT, VNFunc.VNF_SUB, plusThree, plusFive),
                Is.EqualTo(store.VNForIntCon(-2)));
        });
    }

    [Test]
    public static void FloatNaNIdentityRetainsPayload()
    {
        WithStore((store, _) => {
            var symbolic = store.VNForExpr(null, TYP_DOUBLE);
            var nan = store.VNForDoubleCon(BitConverter.Int64BitsToDouble(0x7FF8000000001234));
            foreach (var func in new[] { VNFunc.VNF_ADD, VNFunc.VNF_SUB, VNFunc.VNF_MUL, VNFunc.VNF_DIV })
            {
                Assert.That(store.VNForFunc(TYP_DOUBLE, func, symbolic, nan), Is.EqualTo(nan));
            }

            Assert.That(store.VNForFunc(TYP_INT, VNFunc.VNF_NE, symbolic, nan),
                Is.EqualTo(store.VNForIntCon(1)));
            Assert.That(store.VNForFunc(TYP_INT, VNFunc.VNF_LT, symbolic, nan),
                Is.EqualTo(store.VNForIntCon(0)));
            Assert.That(store.VNForFunc(TYP_INT, VNFunc.VNF_LE_UN, symbolic, nan),
                Is.Not.EqualTo(store.VNForIntCon(1)));
        });
    }

    [TestCase(VNFunc.VNF_LE, VNFunc.VNF_NE, VNFunc.VNF_AND, VNFunc.VNF_LT)]
    [TestCase(VNFunc.VNF_LE, VNFunc.VNF_GT, VNFunc.VNF_AND, VNFunc.VNF_COUNT)]
    [TestCase(VNFunc.VNF_LE, VNFunc.VNF_GT, VNFunc.VNF_OR, VNFunc.VNF_COUNT)]
    [TestCase(VNFunc.VNF_LT_UN, VNFunc.VNF_EQ, VNFunc.VNF_OR, VNFunc.VNF_LE_UN)]
    public static void IntegralRelopsCombineUsingNativeTables(
        VNFunc first, VNFunc second, VNFunc connective, VNFunc expected)
    {
        WithStore((store, _) => {
            var x = store.VNForExpr(null, TYP_INT);
            var y = store.VNForExpr(null, TYP_INT);
            var lhs = store.VNForFunc(TYP_INT, first, x, y);
            var rhs = store.VNForFunc(TYP_INT, second, x, y);
            var combined = store.VNForFunc(TYP_INT, connective, lhs, rhs);
            if (expected == VNFunc.VNF_COUNT)
            {
                Assert.That(store.IsVNConstant(combined), Is.True);
                Assert.That(store.GetConstantInt32(combined),
                    Is.EqualTo(connective == VNFunc.VNF_AND ? 0 : 1));
            }
            else
            {
                Assert.That(combined, Is.EqualTo(store.VNForFunc(TYP_INT, expected, x, y)));
            }
        });
    }

    [Test]
    public static void LongComparisonsNarrowOnlyWhenBothOperandsAreSignExtendedInts()
    {
        WithStore((store, _) => {
            var x = store.VNForExpr(null, TYP_INT);
            var y = store.VNForExpr(null, TYP_INT);
            var info = store.VNForCastOper(TYP_LONG, false);
            var longX = store.VNForFuncNoFolding(TYP_LONG, VNFunc.VNF_Cast, x, info);
            var longY = store.VNForFuncNoFolding(TYP_LONG, VNFunc.VNF_Cast, y, info);
            Assert.That(store.VNForFunc(TYP_INT, VNFunc.VNF_LT, longX, longY),
                Is.EqualTo(store.VNForFunc(TYP_INT, VNFunc.VNF_LT, x, y)));
        });
    }

    [Test]
    public static void TestRelopReversalUsesNativeComparisonClassification()
    {
        WithStore((store, _) => {
            var x = store.VNForExpr(null, TYP_INT);
            var y = store.VNForExpr(null, TYP_INT);
            var test = store.VNForFuncNoFolding(TYP_INT, VNFunc.VNF_TEST_EQ, x, y);
            var reversed = store.VNForFunc(TYP_INT, VNFunc.VNF_EQ, test, store.VNForIntCon(0));
            Assert.That(reversed, Is.EqualTo(store.VNForFunc(TYP_INT, VNFunc.VNF_TEST_NE, x, y)));
        });
    }

    [Test]
    public static void IdentityWithMismatchedResultTypeAllocatesFunction()
    {
        WithStore((store, _) => {
            var x = store.VNForExpr(null, TYP_INT);
            var result = store.VNForFunc(TYP_LONG, VNFunc.VNF_ADD, x, store.VNForLongCon(0));
            Assert.That(result, Is.Not.EqualTo(x));
            Assert.That(store.TypeOfVN(result), Is.EqualTo(TYP_LONG));
        });
    }

    [Test]
    public static void TypeCompareWithoutCompileTimeHandlesRemainsSymbolic()
    {
        WithStore((store, _) => {
            var first = store.VNForHandle(0x1000, GTF_ICON_CLASS_HDL);
            var second = store.VNForHandle(0x2000, GTF_ICON_CLASS_HDL);
            store.AddToEmbeddedHandleMap(0x1000, 0);
            store.AddToEmbeddedHandleMap(0x2000, 0);
            var runtimeFirst = store.VNForFunc(TYP_REF, VNFunc.VNF_TypeHandleToRuntimeType, first);
            var runtimeSecond = store.VNForFunc(TYP_REF, VNFunc.VNF_TypeHandleToRuntimeType, second);
            var result = store.VNForFunc(TYP_INT, VNFunc.VNF_EQ, runtimeFirst, runtimeSecond);
            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(result, ref app), Is.True);
            Assert.That(app.Func, Is.EqualTo(VNFunc.VNF_EQ));
        });
    }

    [TestCase(TypeCompareState.Must, VNFunc.VNF_EQ, 1)]
    [TestCase(TypeCompareState.Must, VNFunc.VNF_NE, 0)]
    [TestCase(TypeCompareState.MustNot, VNFunc.VNF_EQ, 0)]
    [TestCase(TypeCompareState.MustNot, VNFunc.VNF_NE, 1)]
    [TestCase(TypeCompareState.May, VNFunc.VNF_EQ, ValueNumStore.NoVN)]
    [TestCase(TypeCompareState.May, VNFunc.VNF_NE, ValueNumStore.NoVN)]
    public static void RuntimeTypeComparisonUsesCompileTimeHandlesAndDefiniteAnswers(
        TypeCompareState answer, VNFunc func, int expected)
    {
        WithStore((store, compiler) => {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.compareTypesForEquality = &CompareTypesForEquality;
            TestEE ee = new() { Info = new() { lpVtbl = &vtable }, Comparison = answer };
            compiler.info.compCompHnd = &ee.Info;

            var firstHandle = store.VNForHandle(0x1000, GTF_ICON_CLASS_HDL);
            var secondHandle = store.VNForHandle(0x2000, GTF_ICON_CLASS_HDL);
            store.AddToEmbeddedHandleMap(0x1000, 0x3000);
            store.AddToEmbeddedHandleMap(0x2000, 0x4000);
            var first = store.VNForFunc(TYP_REF, VNFunc.VNF_TypeHandleToRuntimeType, firstHandle);
            var second = store.VNForFunc(TYP_REF, VNFunc.VNF_TypeHandleToRuntimeType, secondHandle);

            var folded = FoldTypeCompare(store, TYP_INT, func, first, second);
            Assert.That(ee.EqualityCalls, Is.EqualTo(1));
            Assert.That(ee.LastFrom, Is.EqualTo((nint)0x3000));
            Assert.That(ee.LastTo, Is.EqualTo((nint)0x4000));
            if (answer is TypeCompareState.May)
            {
                Assert.That(folded, Is.EqualTo(ValueNumStore.NoVN));
                var symbolic = store.VNForFunc(TYP_INT, func, first, second);
                var app = new VNFuncApp();
                Assert.That(store.GetVNFunc(symbolic, ref app), Is.True);
                Assert.That(app.Func, Is.EqualTo(func));
            }
            else
            {
                Assert.That(store.GetConstantInt32(folded), Is.EqualTo(expected));
                Assert.That(store.VNForFunc(TYP_INT, func, first, second), Is.EqualTo(folded));
            }
            Assert.That(ee.EqualityCalls, Is.EqualTo(2));
        });
    }

    [TestCase(TypeCompareState.Must, VNFunc.VNF_IsInstanceOf, true, false)]
    [TestCase(TypeCompareState.Must, VNFunc.VNF_CastClass, true, false)]
    [TestCase(TypeCompareState.MustNot, VNFunc.VNF_IsInstanceOf, false, false)]
    [TestCase(TypeCompareState.MustNot, VNFunc.VNF_CastClass, false, true)]
    [TestCase(TypeCompareState.May, VNFunc.VNF_IsInstanceOf, false, false)]
    [TestCase(TypeCompareState.May, VNFunc.VNF_CastClass, false, true)]
    public static void CastComparisonUsesExactObjectTypeAndRuntimeAnswer(
        TypeCompareState answer, VNFunc func, bool sameObject, bool hasException)
    {
        WithStore((store, compiler) => {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.getObjectType = &GetObjectType;
            vtable.Base.Base.compareTypesForCast = &CompareTypesForCast;
            TestEE ee = new() { Info = new() { lpVtbl = &vtable }, Comparison = answer };
            compiler.info.compCompHnd = &ee.Info;

            var target = store.VNForHandle(0x1000, GTF_ICON_CLASS_HDL);
            var obj = store.VNForHandle(0x5000, GTF_ICON_OBJ_HDL);
            store.AddToEmbeddedHandleMap(0x1000, 0x2000);
            var result = store.VNForFunc(TYP_REF, func, target, obj);

            Assert.That(ee.ObjectTypeCalls, Is.EqualTo(1));
            Assert.That(ee.CastCalls, Is.EqualTo(1));
            Assert.That(ee.LastFrom, Is.EqualTo((nint)0x6000));
            Assert.That(ee.LastTo, Is.EqualTo((nint)0x2000));
            Assert.That(result == obj, Is.EqualTo(sameObject));
            Assert.That(store.VNHasExc(result), Is.EqualTo(hasException));
            if ((answer is TypeCompareState.MustNot) && (func == VNFunc.VNF_IsInstanceOf))
            {
                Assert.That(result, Is.EqualTo(ValueNumStore.VNForNull()));
            }
            else if ((answer is TypeCompareState.May) && (func == VNFunc.VNF_IsInstanceOf))
            {
                var app = new VNFuncApp();
                Assert.That(store.GetVNFunc(result, ref app), Is.True);
                Assert.That(app.Func, Is.EqualTo(func));
            }
        });
    }

    [Test]
    public static void InexactObjectTypeDoesNotFoldDefiniteFailedIsInstanceOf()
    {
        WithStore((store, compiler) => {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.compareTypesForCast = &CompareTypesForCast;
            TestEE ee = new() { Info = new() { lpVtbl = &vtable }, Comparison = TypeCompareState.MustNot };
            compiler.info.compCompHnd = &ee.Info;

            var target = store.VNForHandle(0x1000, GTF_ICON_CLASS_HDL);
            var sourceClass = store.VNForHandle(0x3000, GTF_ICON_CLASS_HDL);
            store.AddToEmbeddedHandleMap(0x1000, 0x2000);
            store.AddToEmbeddedHandleMap(0x3000, 0x6000);
            var unknown = store.VNForExpr(null, TYP_REF);
            var inexact = store.VNForFuncNoFolding(TYP_REF, VNFunc.VNF_IsInstanceOf, sourceClass, unknown);
            var result = store.VNForFunc(TYP_REF, VNFunc.VNF_IsInstanceOf, target, inexact);

            Assert.That(ee.CastCalls, Is.EqualTo(1));
            Assert.That(ee.LastFrom, Is.EqualTo((nint)0x6000));
            Assert.That(ee.LastTo, Is.EqualTo((nint)0x2000));
            Assert.That(result, Is.Not.EqualTo(ValueNumStore.VNForNull()));
            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(result, ref app), Is.True);
            Assert.That(app.Func, Is.EqualTo(VNFunc.VNF_IsInstanceOf));
        });
    }

    [Test]
    public static void CastOfNullAndRepeatedIsInstanceOfPreserveNativeIdentities()
    {
        WithStore((store, _) => {
            var target = store.VNForHandle(0x1000, GTF_ICON_CLASS_HDL);
            var obj = store.VNForExpr(null, TYP_REF);
            Assert.That(store.VNForFunc(TYP_REF, VNFunc.VNF_IsInstanceOf, target, ValueNumStore.VNForNull()),
                Is.EqualTo(ValueNumStore.VNForNull()));
            Assert.That(store.VNForFunc(TYP_REF, VNFunc.VNF_CastClass, target, ValueNumStore.VNForNull()),
                Is.EqualTo(ValueNumStore.VNForNull()));
            var instance = store.VNForFunc(TYP_REF, VNFunc.VNF_IsInstanceOf, target, obj);
            Assert.That(store.VNForFunc(TYP_REF, VNFunc.VNF_CastClass, target, instance),
                Is.EqualTo(instance));
            var cast = store.VNForFunc(TYP_REF, VNFunc.VNF_CastClass, target, obj);
            Assert.That(store.VNHasExc(cast), Is.True);
            Assert.That(store.VNNormalValue(cast), Is.EqualTo(obj));
            Assert.That(store.VNForFunc(TYP_REF, VNFunc.VNF_CastClass, target, obj),
                Is.EqualTo(cast));
        });
    }

    private static void WithStore(Action<ValueNumStore, Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        JitTls.Compiler = compiler;
        try
        {
            action(new ValueNumStore(compiler), compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private struct TestEE
    {
        public ICorJitInfo Info;
        public TypeCompareState Comparison;
        public nint LastFrom;
        public nint LastTo;
        public int EqualityCalls;
        public int CastCalls;
        public int ObjectTypeCalls;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static TypeCompareState CompareTypesForEquality(
        ICorJitInfo* self, CORINFO_CLASS_STRUCT_* from, CORINFO_CLASS_STRUCT_* to)
    {
        var ee = (TestEE*)self;
        ee->EqualityCalls++;
        ee->LastFrom = (nint)from;
        ee->LastTo = (nint)to;
        return ee->Comparison;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static TypeCompareState CompareTypesForCast(
        ICorJitInfo* self, CORINFO_CLASS_STRUCT_* from, CORINFO_CLASS_STRUCT_* to)
    {
        var ee = (TestEE*)self;
        ee->CastCalls++;
        ee->LastFrom = (nint)from;
        ee->LastTo = (nint)to;
        return ee->Comparison;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetObjectType(ICorJitInfo* self, CORINFO_OBJECT_STRUCT_* obj)
    {
        ((TestEE*)self)->ObjectTypeCalls++;
        return (CORINFO_CLASS_STRUCT_*)0x6000;
    }
}
