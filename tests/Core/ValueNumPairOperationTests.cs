// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;
using static RyuJitSharp.VNFunc;
#if TARGET_64BIT
using target_ssize_t = System.Int64;
#else
using target_ssize_t = System.Int32;
#endif

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ValueNumPairOperationTests
{
    [TestCase(-1)]
    [TestCase(17)]
    public static void PointerSizedConstantsUseTargetWidth(int value)
    {
        WithStore(store =>
        {
            var constant = store.VNForPtrSizeIntCon((target_ssize_t)value);
#if TARGET_64BIT
            Assert.That(constant, Is.EqualTo(store.VNForLongCon(value)));
            Assert.That(store.TypeOfVN(constant), Is.EqualTo(TYP_LONG));
#else
            Assert.That(constant, Is.EqualTo(store.VNForIntCon(value)));
            Assert.That(store.TypeOfVN(constant), Is.EqualTo(TYP_INT));
#endif
        });
    }

#if TARGET_64BIT
    [Test]
    public static void TargetPointerSizedConstantRetainsHighBits()
    {
        WithStore(store =>
        {
            const target_ssize_t value = 0x1_0000_0000;
            var constant = store.VNForPtrSizeIntCon(value);
            Assert.That(constant, Is.EqualTo(store.VNForLongCon(value)));
            Assert.That(store.GetConstantInt64(constant), Is.EqualTo(value));
        });
    }
#endif

    [Test]
    public static void PairFunctionsPreserveLaneOrderAndShareEqualResults()
    {
        WithStore(store =>
        {
            var first = store.VNForExpr(null, TYP_INT);
            var second = store.VNForExpr(null, TYP_INT);
            var equal = new ValueNumPair(first, first);
            var split = new ValueNumPair(first, second);
            var zero = store.VNPairForFunc(TYP_BYREF, VNF_LoopCloneChoiceAddr);
            Assert.That(zero.BothEqual(), Is.True);
            Assert.That(zero.Liberal, Is.EqualTo(store.VNForFunc(TYP_BYREF, VNF_LoopCloneChoiceAddr)));

            var binary = store.VNPairForFunc(TYP_INT, VNF_SUB, equal, split);
            Assert.That(binary.Liberal, Is.EqualTo(store.VNForFunc(TYP_INT, VNF_SUB, first, first)));
            Assert.That(binary.Conservative, Is.EqualTo(store.VNForFunc(TYP_INT, VNF_SUB, first, second)));
            Assert.That(binary.BothEqual(), Is.False);

            var noFolding = store.VNPairForFuncNoFolding(TYP_INT, VNF_ADD,
                new(store.VNForIntCon(1), store.VNForIntCon(1)),
                new(store.VNForIntCon(2), store.VNForIntCon(2)));
            Assert.That(noFolding.BothEqual(), Is.True);
            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(noFolding.Liberal, ref app), Is.True);
            Assert.That(app.Func, Is.EqualTo(VNF_ADD));

            var ternary = store.VNPairForFunc(TYP_REF, VNF_JitNewArr, equal, split, equal);
            Assert.That(ternary.Liberal, Is.EqualTo(store.VNForFunc(TYP_REF, VNF_JitNewArr, first, first, first)));
            Assert.That(ternary.Conservative, Is.EqualTo(store.VNForFunc(TYP_REF, VNF_JitNewArr, first, second, first)));

            var quaternary = store.VNPairForFunc(TYP_HEAP, VNF_MapStore, equal, equal, equal, split);
            Assert.That(quaternary.Liberal, Is.EqualTo(store.VNForFunc(TYP_HEAP, VNF_MapStore, first, first, first, first)));
            Assert.That(quaternary.Conservative, Is.EqualTo(store.VNForFunc(TYP_HEAP, VNF_MapStore, first, first, first, second)));
            Assert.That(store.VNPairForFunc(TYP_INT, VNF_SUB, equal, equal).BothEqual(), Is.True);
        });
    }

    [Test]
    public static void UniquePairAllocatesOneNormalValueAndSharesBothLanes()
    {
        WithStore(store =>
        {
            var first = store.VNPairForExpr(null, TYP_INT);
            var second = store.VNPairForExpr(null, TYP_INT);
            Assert.That(first.BothEqual(), Is.True);
            Assert.That(second.BothEqual(), Is.True);
            Assert.That(second.Liberal, Is.EqualTo(first.Liberal + 1));
            Assert.That(store.TypeOfVN(first.Liberal), Is.EqualTo(TYP_INT));
            Assert.That(store.TypeOfVN(second.Liberal), Is.EqualTo(TYP_INT));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void NumericCastPreservesLaneSelectionAndCheckedExceptions(bool checkedCast)
    {
        WithStore(store =>
        {
            var first = store.VNForExpr(null, TYP_LONG);
            var second = store.VNForExpr(null, TYP_LONG);
            var source = new ValueNumPair(first, second);
            var cast = store.VNPairForCast(source, TYP_INT, TYP_LONG, hasOverflowCheck: checkedCast);
            Assert.That(store.VNNormalValue(cast.Liberal),
                Is.EqualTo(store.VNNormalValue(store.VNForCast(first, TYP_INT, TYP_LONG,
                    hasOverflowCheck: checkedCast))));
            Assert.That(store.VNNormalValue(cast.Conservative),
                Is.EqualTo(store.VNNormalValue(store.VNForCast(second, TYP_INT, TYP_LONG,
                    hasOverflowCheck: checkedCast))));
            Assert.That(store.VNExceptionSet(cast.Liberal) != ValueNumStore.VNForEmptyExcSet(),
                Is.EqualTo(checkedCast));
            Assert.That(store.VNExceptionSet(cast.Conservative) != ValueNumStore.VNForEmptyExcSet(),
                Is.EqualTo(checkedCast));

            var shared = store.VNPairForCast(new(first, first), TYP_INT, TYP_LONG);
            Assert.That(shared.BothEqual(), Is.True);
        });
    }

    [Test]
    public static void CastNormalizesUnsignedAndRetainsHandleIdentity()
    {
        WithStore(store =>
        {
            var source = store.VNForIntCon(-1);
            var signed = store.VNForCast(source, TYP_BYTE, TYP_INT);
            var unsigned = store.VNForCast(source, TYP_BYTE, TYP_INT, srcIsUnsigned: true);
            Assert.That(signed, Is.EqualTo(unsigned));
            Assert.That(store.GetConstantInt32(signed), Is.EqualTo(-1));

            var extendedSigned = store.VNForCast(source, TYP_LONG, TYP_INT);
            var extendedUnsigned = store.VNForCast(source, TYP_LONG, TYP_INT, srcIsUnsigned: true);
            Assert.That(store.GetConstantInt64(extendedSigned), Is.EqualTo(-1L));
            Assert.That(store.GetConstantInt64(extendedUnsigned), Is.EqualTo(uint.MaxValue));

            var handle = store.VNForHandle(123, GTF_ICON_CLASS_HDL);
            Assert.That(store.VNForCast(handle, TYP_BYREF, TYP_I_IMPL), Is.EqualTo(handle));
        });
    }

    [Test]
    public static void FoldedCheckedCastRetainsOnlyExistingExceptions()
    {
        WithStore(store =>
        {
            var source = store.VNForIntCon(23);
            var oldException = store.VNExcSetSingleton(store.VNForFunc(TYP_REF, VNF_OverflowExc, source));
            var withException = store.VNWithExc(source, oldException);
            var cast = store.VNForCast(withException, TYP_BYTE, TYP_INT, hasOverflowCheck: true);
            Assert.That(store.VNNormalValue(cast), Is.EqualTo(source));
            Assert.That(store.VNExceptionSet(cast), Is.EqualTo(oldException));

            var unique = store.VNUniqueWithExc(TYP_INT, oldException);
            Assert.That(store.VNNormalValue(unique), Is.Not.EqualTo(source));
            Assert.That(store.TypeOfVN(store.VNNormalValue(unique)), Is.EqualTo(TYP_INT));
            Assert.That(store.VNExceptionSet(unique), Is.EqualTo(oldException));
        });
    }

    [Test]
    public static void BitCastCollapsesNestedCastsAndEncodesStructWidth()
    {
        WithStore(store =>
        {
            var source = store.VNForExpr(null, TYP_INT);
            var first = store.VNForBitCast(source, TYP_FLOAT, new ValueSize(4));
            var nested = store.VNForBitCast(first, TYP_LONG, new ValueSize(8));
            Assert.That(store.VNForBitCast(source, TYP_INT, new ValueSize(4)), Is.EqualTo(source));

            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(nested, ref app), Is.True);
            Assert.That(app.Func, Is.EqualTo(VNF_BitCast));
            Assert.That(app.GetArg(0), Is.EqualTo(source));
            Assert.That(store.DecodeBitCastType(app.GetArg(1), out var width), Is.EqualTo(TYP_LONG));
            Assert.That(width, Is.EqualTo(8u));

            var structType = store.EncodeBitCastType(TYP_STRUCT, new ValueSize(3));
            Assert.That(store.DecodeBitCastType(structType, out width), Is.EqualTo(TYP_STRUCT));
            Assert.That(width, Is.EqualTo(3u));
            var equal = store.VNPairForBitCast(new(source, source), TYP_FLOAT, new ValueSize(4));
            Assert.That(equal.BothEqual(), Is.True);
            Assert.That(equal.Liberal, Is.EqualTo(first));

            var different = store.VNForExpr(null, TYP_INT);
            var split = store.VNPairForBitCast(new(source, different), TYP_FLOAT, new ValueSize(4));
            Assert.That(split.Liberal, Is.EqualTo(first));
            Assert.That(split.Conservative, Is.EqualTo(store.VNForBitCast(different, TYP_FLOAT, new ValueSize(4))));

            var zeroObject = store.VNForFunc(TYP_STRUCT, VNF_ZeroObj, store.VNForIntCon(8));
            Assert.That(store.VNForBitCast(zeroObject, TYP_INT, new ValueSize(4)),
                Is.EqualTo(store.VNForIntCon(0)));
        });
    }

    [Test]
    public static void ExceptionPairsKeepLaneSetsAndOneSharedUniqueNormal()
    {
        WithStore(store =>
        {
            var first = store.VNForExpr(null, TYP_INT);
            var second = store.VNForExpr(null, TYP_INT);
            var left = store.VNExcSetSingleton(store.VNForFunc(TYP_REF, VNF_OverflowExc, first));
            var right = store.VNExcSetSingleton(store.VNForFunc(TYP_REF, VNF_OverflowExc, second));
            var exceptions = new ValueNumPair(left, right);

            var unique = store.VNPUniqueWithExc(TYP_INT, exceptions);
            store.VNPUnpackExc(unique, out var normal, out var unpacked);
            Assert.That(normal.BothEqual(), Is.True);
            Assert.That(unpacked, Is.EqualTo(exceptions));
            Assert.That(store.VNPNormalPair(unique), Is.EqualTo(normal));
            Assert.That(store.VNPExceptionSet(unique), Is.EqualTo(exceptions));
            Assert.That(store.VNPUnionExcSet(unique, ValueNumStore.VNPForEmptyExcSet()), Is.EqualTo(exceptions));

            var replaced = store.VNPMakeNormalUniquePair(unique);
            store.VNPUnpackExc(replaced, out var replacedNormal, out var retained);
            Assert.That(replacedNormal.BothEqual(), Is.False);
            Assert.That(replacedNormal.Liberal, Is.Not.EqualTo(normal.Liberal));
            Assert.That(replacedNormal.Conservative, Is.EqualTo(replacedNormal.Liberal + 1));
            Assert.That(retained, Is.EqualTo(exceptions));

            var union = store.VNPExcSetUnion(new(left, left), new(right, right));
            Assert.That(union.BothEqual(), Is.True);
            Assert.That(store.VNPExcSetIntersection(union, new(left, right)),
                Is.EqualTo(new ValueNumPair(left, right)));
            Assert.That(store.VNPExcIsSubset(union, new(left, right)), Is.True);
            Assert.That(store.VNPExcIsSubset(new(left, left), new(right, right)), Is.False);
            Assert.That(store.VNPExcIsSubset(new(left, left), ValueNumStore.VNPForEmptyExcSet()), Is.True);
        });
    }

    private static void WithStore(Action<ValueNumStore> action)
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
            action(new ValueNumStore(compiler));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
