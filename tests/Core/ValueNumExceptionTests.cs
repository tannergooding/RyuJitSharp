// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;
using static RyuJitSharp.VNFunc;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ValueNumExceptionTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "compMaxUncheckedOffsetForNullObject")]
    private static extern ref int MaxUncheckedOffset(Compiler compiler);

    internal enum PairedExceptionOperation
    {
        Singleton,
        Attach,
        Union,
        Intersection,
        Add,
        MakeUnique,
    }

    [TestCase(PairedExceptionOperation.Singleton, false)]
    [TestCase(PairedExceptionOperation.Singleton, true)]
    [TestCase(PairedExceptionOperation.Attach, false)]
    [TestCase(PairedExceptionOperation.Attach, true)]
    [TestCase(PairedExceptionOperation.Union, false)]
    [TestCase(PairedExceptionOperation.Union, true)]
    [TestCase(PairedExceptionOperation.Intersection, false)]
    [TestCase(PairedExceptionOperation.Intersection, true)]
    [TestCase(PairedExceptionOperation.Add, false)]
    [TestCase(PairedExceptionOperation.Add, true)]
    [TestCase(PairedExceptionOperation.MakeUnique, false)]
    [TestCase(PairedExceptionOperation.MakeUnique, true)]
    public static void PairedExceptionsPreserveNativeAllocationOrderAndSemanticRoles(
        PairedExceptionOperation operation, bool different)
    {
        WithStore((compiler, store) =>
        {
            var reference = new ValueNumStore(compiler);
            var actual = EvaluatePairOperation(store, operation, different, paired: true);
            var expected = EvaluatePairOperation(reference, operation, different, paired: false);

            Assert.That(actual, Is.EqualTo(expected));
        });
    }

    private static (ValueNumPair Numbers, string LiberalMeaning, string ConservativeMeaning) EvaluatePairOperation(
        ValueNumStore store, PairedExceptionOperation operation, bool different, bool paired)
    {
        var tokens = new int[8];

        for (var index = 0; index < tokens.Length; index++)
        {
            tokens[index] = store.VNForFunc(TYP_REF, VNF_NullPtrExc, store.VNForExpr(null, TYP_BYREF));
        }

        int Set(int first, int middle, int last)
        {
            var tail = store.VNExcSetSingleton(tokens[last]);
            tail = store.VNForFuncNoFolding(TYP_REF, VNF_ExcSetCons, tokens[middle], tail);

            return store.VNForFuncNoFolding(TYP_REF, VNF_ExcSetCons, tokens[first], tail);
        }

        var leftLiberal = Set(0, 1, 2);
        var rightLiberal = Set(0, 2, 3);
        var left = new ValueNumPair(leftLiberal, different ? Set(4, 5, 6) : leftLiberal);
        var right = new ValueNumPair(rightLiberal, different ? Set(4, 6, 7) : rightLiberal);

        if (operation is PairedExceptionOperation.Singleton)
        {
            left = new(tokens[0], different ? tokens[4] : tokens[0]);
        }
        else if (operation is PairedExceptionOperation.Attach or PairedExceptionOperation.Add or
            PairedExceptionOperation.MakeUnique)
        {
            var liberal = store.VNForIntCon(11);
            var conservative = different ? store.VNForIntCon(22) : liberal;

            if (operation is PairedExceptionOperation.Attach)
            {
                right = left;
                left = new(liberal, conservative);
            }
            else
            {
                left = new(store.VNWithExc(liberal, left.Liberal),
                    store.VNWithExc(conservative, left.Conservative));
            }
        }

        int Scalar(int value, int exceptions)
        {
            return operation switch
            {
                PairedExceptionOperation.Singleton => store.VNExcSetSingleton(value),
                PairedExceptionOperation.Attach => store.VNWithExc(value, exceptions),
                PairedExceptionOperation.Union => store.VNExcSetUnion(value, exceptions),
                PairedExceptionOperation.Intersection => store.VNExcSetIntersection(value, exceptions),
                PairedExceptionOperation.Add => store.VNUnionExcSet(value, exceptions),
                PairedExceptionOperation.MakeUnique => store.VNMakeNormalUnique(value),
                _ => throw new ArgumentOutOfRangeException(nameof(operation)),
            };
        }

        ValueNumPair result;

        if (paired)
        {
            result = operation switch
            {
                PairedExceptionOperation.Singleton => store.VNPExcSetSingleton(left),
                PairedExceptionOperation.Attach => store.VNPWithExc(left, right),
                PairedExceptionOperation.Union => store.VNPExcSetUnion(left, right),
                PairedExceptionOperation.Intersection => store.VNPExcSetIntersection(left, right),
                PairedExceptionOperation.Add => store.VNPUnionExcSet(left, right),
                PairedExceptionOperation.MakeUnique => store.VNPMakeNormalUniquePair(left),
                _ => throw new ArgumentOutOfRangeException(nameof(operation)),
            };
        }
        else
        {
            var conservative = Scalar(left.Conservative, right.Conservative);
            var liberal = Scalar(left.Liberal, right.Liberal);
            result = new(liberal, conservative);
        }

        string Meaning(int value)
        {
            var isValue = operation is PairedExceptionOperation.Attach or PairedExceptionOperation.MakeUnique;
            var normal = isValue ? store.VNNormalValue(value) : ValueNumStore.NoVN;
            var exceptions = isValue ? store.VNExceptionSet(value) : value;
            var text = $"{normal}:";

            while (exceptions != ValueNumStore.VNForEmptyExcSet())
            {
                var app = new VNFuncApp();
                Assert.That(store.GetVNFunc(exceptions, ref app), Is.True);
                Assert.That(app.Func, Is.EqualTo(VNF_ExcSetCons));
                text += $"{app.GetArg(0)},";
                exceptions = app.GetArg(1);
            }

            return text;
        }

        return (result, Meaning(result.Liberal), Meaning(result.Conservative));
    }

    [Test]
    public static void EmptyPairedExceptionsReuseNormalValuesAndSets()
    {
        WithStore((_, store) =>
        {
            var liberal = store.VNForExpr(null, TYP_INT);
            var conservative = store.VNForExpr(null, TYP_INT);
            var values = new ValueNumPair(liberal, conservative);
            var empty = ValueNumStore.VNPForEmptyExcSet();

            Assert.That(store.VNPWithExc(values, empty), Is.EqualTo(values));
            Assert.That(store.VNPUnionExcSet(values, empty), Is.EqualTo(empty));
            Assert.That(store.VNPExcSetUnion(empty, empty), Is.EqualTo(empty));
            Assert.That(store.VNPExcSetIntersection(empty, empty), Is.EqualTo(empty));
            Assert.That(store.VNForExpr(null, TYP_INT), Is.EqualTo(conservative + 1));
        });
    }

#if DEBUG
    [Test]
    public static void DebugExceptionSetCheckUsesSortedSubsetOrder()
    {
        WithStore((_, store) =>
        {
            var first = store.VNExcSetSingleton(store.VNForFunc(TYP_REF, VNF_NullPtrExc,
                store.VNForExpr(null, TYP_BYREF)));
            var second = store.VNExcSetSingleton(store.VNForFunc(TYP_REF, VNF_OverflowExc,
                store.VNForExpr(null, TYP_INT)));
            var full = store.VNExcSetUnion(first, second);
            Assert.That(store.VNExcIsSubset(full, first), Is.True);
            Assert.That(store.VNExcIsSubset(full, second), Is.True);
            Assert.That(store.VNExcIsSubset(first, second), Is.False);
            Assert.That(store.VNExcIsSubset(ValueNumStore.VNForEmptyExcSet(),
                ValueNumStore.VNForEmptyExcSet()), Is.True);
        });
    }
#endif

    [TestCase(GT_DIV, TYP_INT, -1, int.MinValue, false, true)]
    [TestCase(GT_MOD, TYP_INT, 2, int.MinValue, false, false)]
    [TestCase(GT_DIV, TYP_INT, 0, int.MinValue, true, false)]
    [TestCase(GT_UDIV, TYP_INT, 0, int.MinValue, true, false)]
    [TestCase(GT_UMOD, TYP_INT, -1, int.MinValue, false, false)]
    [TestCase(GT_DIV, TYP_LONG, -1, long.MinValue, false, true)]
    [TestCase(GT_MOD, TYP_LONG, 2, long.MinValue, false, false)]
    [TestCase(GT_UDIV, TYP_LONG, 0, long.MinValue, true, false)]
    public static void DivisionConstantPreconditions(genTreeOps oper, var_types type,
        long divisor, long dividend, bool divideByZero, bool arithmetic)
    {
        WithStore((compiler, store) =>
        {
            var left = Node(type, type is TYP_INT
                ? store.VNForIntCon(unchecked((int)dividend)) : store.VNForLongCon(dividend));
            var right = Node(type, type is TYP_INT
                ? store.VNForIntCon(unchecked((int)divisor)) : store.VNForLongCon(divisor));
            var result = compiler.fgValueNumberDivisionExceptions(oper, left, right);
            Assert.That(HasException(store, result.Liberal, VNF_DivideByZeroExc), Is.EqualTo(divideByZero));
            Assert.That(HasException(store, result.Conservative, VNF_DivideByZeroExc), Is.EqualTo(divideByZero));
            Assert.That(HasException(store, result.Liberal, VNF_ArithmeticExc), Is.EqualTo(arithmetic));
            Assert.That(HasException(store, result.Conservative, VNF_ArithmeticExc), Is.EqualTo(arithmetic));
        });
    }

    [TestCase(TYP_INT, GT_DIV)]
    [TestCase(TYP_LONG, GT_MOD)]
    [TestCase(TYP_INT, GT_UDIV)]
    public static void DivisionKeepsLiberalAndConservativeExceptionsSeparate(var_types type, genTreeOps oper)
    {
        WithStore((compiler, store) =>
        {
            var dividendLib = store.VNForExpr(null, type);
            var dividendCon = type is TYP_INT ? store.VNForIntCon(5) : store.VNForLongCon(5);
            var divisorLib = type is TYP_INT ? store.VNForIntCon(-1) : store.VNForLongCon(-1);
            var divisorCon = store.VNForExpr(null, type);
            var dividend = Node(type, dividendLib, dividendCon);
            var divisor = Node(type, divisorLib, divisorCon);
            var exceptions = compiler.fgValueNumberDivisionExceptions(oper, dividend, divisor);

            Assert.That(HasException(store, exceptions.Liberal, VNF_DivideByZeroExc), Is.False);
            Assert.That(HasException(store, exceptions.Conservative, VNF_DivideByZeroExc), Is.True);
            Assert.That(HasException(store, exceptions.Liberal, VNF_ArithmeticExc), Is.EqualTo(oper is GT_DIV or GT_MOD));
            Assert.That(HasException(store, exceptions.Conservative, VNF_ArithmeticExc), Is.False);
            if (oper is GT_DIV or GT_MOD)
            {
                var app = FindException(store, exceptions.Liberal, VNF_ArithmeticExc);
                Assert.That(app.GetArg(0), Is.EqualTo(dividendLib));
                Assert.That(app.GetArg(1), Is.EqualTo(divisorLib));
            }
        });
    }

    [Test]
    public static void ConservativeArithmeticUsesLiberalDividend()
    {
        WithStore((compiler, store) =>
        {
            var liberalDividend = store.VNForExpr(null, TYP_INT);
            var conservativeDividend = store.VNForExpr(null, TYP_INT);
            var dividend = Node(TYP_INT, liberalDividend, conservativeDividend);
            var divisor = Node(TYP_INT, store.VNForIntCon(2), store.VNForIntCon(-1));
            var exceptions = compiler.fgValueNumberDivisionExceptions(GT_DIV, dividend, divisor);

            Assert.That(HasException(store, exceptions.Liberal, VNF_ArithmeticExc), Is.False);
            var arithmetic = FindException(store, exceptions.Conservative, VNF_ArithmeticExc);
            Assert.That(arithmetic.GetArg(0), Is.EqualTo(liberalDividend));
            Assert.That(arithmetic.GetArg(1), Is.EqualTo(store.VNForIntCon(-1)));
        });
    }

    [Test]
    public static void NullCheckPeelsSmallOffsetButRetainsLargeOffsetAndExistingExceptions()
    {
        WithStore((compiler, store) =>
        {
            MaxUncheckedOffset(compiler) = 4096;
            var baseVN = store.VNForExpr(null, TYP_BYREF);
            var small = store.VNForFunc(TYP_BYREF, VNF_ADD, baseVN, store.VNForIntCon(16));
            var large = store.VNForFunc(TYP_BYREF, VNF_ADD, baseVN, store.VNForIntCon(8192));
            var addr = Node(TYP_BYREF, small, large);
            var exceptions = compiler.fgValueNumberIndirNullCheckExceptions(addr);
            Assert.That(FindException(store, exceptions.Liberal, VNF_NullPtrExc).GetArg(0), Is.EqualTo(baseVN));
            Assert.That(FindException(store, exceptions.Conservative, VNF_NullPtrExc).GetArg(0), Is.EqualTo(large));

            var oldException = store.VNExcSetSingleton(store.VNForFunc(TYP_REF, VNF_OverflowExc,
                store.VNForExpr(null, TYP_INT)));
            var indir = new GenTreeIndir(GT_IND, TYP_INT, addr)
            {
                _vnPair = store.VNPWithExc(new(store.VNForExpr(null, TYP_INT), store.VNForExpr(null, TYP_INT)),
                    new(oldException, oldException)),
            };
            compiler.fgValueNumberAddExceptionSetForIndirection(indir, addr);
            Assert.That(HasException(store, store.VNExceptionSet(indir._vnPair.Liberal), VNF_OverflowExc), Is.True);
            Assert.That(HasException(store, store.VNExceptionSet(indir._vnPair.Liberal), VNF_NullPtrExc), Is.True);
        });
    }

    [Test]
    public static void ConstantIndirectionSkipsNullCheck()
    {
        WithStore((compiler, store) =>
        {
            var addr = Node(TYP_BYREF, store.VNForExpr(null, TYP_BYREF));
            var indir = new GenTreeIndir(GT_IND, TYP_INT, addr)
            {
                _vnPair = new(store.VNForIntCon(42), store.VNForIntCon(42)),
            };
            compiler.fgValueNumberAddExceptionSetForIndirection(indir, addr);
            Assert.That(indir._vnPair.Liberal, Is.EqualTo(store.VNForIntCon(42)));
        });
    }

    [TestCase(GT_ADD, false, VNF_ADD_OVF)]
    [TestCase(GT_SUB, true, VNF_SUB_UN_OVF)]
    [TestCase(GT_MUL, false, VNF_MUL_OVF)]
    public static void OverflowSkipsIdentityAndAddsExceptionToSymbolicResult(
        genTreeOps oper, bool unsigned, VNFunc func)
    {
        WithStore((compiler, store) =>
        {
            var source = store.VNForExpr(null, TYP_INT);
            var first = Node(TYP_INT, source);
            var second = Node(TYP_INT, store.VNForIntCon(1));
            var tree = new GenTreeOp(oper, TYP_INT, first, second)
            {
                Flags = GTF_OVERFLOW | GTF_EXCEPT,
                IsUnsigned = unsigned,
                _vnPair = new(source, source),
            };
            compiler.fgValueNumberAddExceptionSetForOverflow(tree);
            Assert.That(store.VNExceptionSet(tree._vnPair.Liberal), Is.EqualTo(ValueNumStore.VNForEmptyExcSet()));

            tree._vnPair.SetBoth(store.VNForIntCon(4));
            compiler.fgValueNumberAddExceptionSetForOverflow(tree);
            Assert.That(store.VNExceptionSet(tree._vnPair.Liberal), Is.EqualTo(ValueNumStore.VNForEmptyExcSet()));

            var normal = store.VNForFuncNoFolding(TYP_INT, func, source, store.VNForIntCon(1));
            tree._vnPair.SetBoth(normal);
            compiler.fgValueNumberAddExceptionSetForOverflow(tree);
            var overflow = FindException(store, store.VNExceptionSet(tree._vnPair.Liberal), VNF_OverflowExc);
            Assert.That(overflow.GetArg(0), Is.EqualTo(normal));
        });
    }

    [Test]
    public static void BoundsAndFiniteChecksUnionPriorExceptions()
    {
        WithStore((compiler, store) =>
        {
            var index = Node(TYP_INT, store.VNForIntCon(2), store.VNForIntCon(3));
            var length = Node(TYP_INT, store.VNForIntCon(4), store.VNForIntCon(5));
            var prior = store.VNExcSetSingleton(store.VNForFunc(TYP_REF, VNF_NullPtrExc,
                store.VNForExpr(null, TYP_REF)));
            var bounds = new GenTreeBoundsChk(index, length, SpecialCodeKind.SCK_RNGCHK_FAIL)
            {
                _vnPair = store.VNPWithExc(new(ValueNumStore.VNForVoid(), ValueNumStore.VNForVoid()),
                    new(prior, prior)),
            };
            compiler.fgValueNumberAddExceptionSetForBoundsCheck(bounds);
            var exceptions = store.VNPExceptionSet(bounds._vnPair);
            Assert.That(HasException(store, exceptions.Liberal, VNF_NullPtrExc), Is.True);
            var check = FindException(store, exceptions.Conservative, VNF_IndexOutOfRangeExc);
            Assert.That(check.GetArg(0), Is.EqualTo(index._vnPair.Conservative));
            Assert.That(check.GetArg(1), Is.EqualTo(length._vnPair.Conservative));

            var finiteNormal = store.VNForExpr(null, TYP_DOUBLE);
            var finite = new GenTreeUnOp(GT_CKFINITE, TYP_DOUBLE, Node(TYP_DOUBLE, finiteNormal))
            {
                _vnPair = store.VNPWithExc(new(finiteNormal, finiteNormal), new(prior, prior)),
            };
            compiler.fgValueNumberAddExceptionSetForCkFinite(finite);
            exceptions = store.VNPExceptionSet(finite._vnPair);
            Assert.That(HasException(store, exceptions.Liberal, VNF_NullPtrExc), Is.True);
            Assert.That(FindException(store, exceptions.Liberal, VNF_ArithmeticExc).GetArg(0),
                Is.EqualTo(finiteNormal));
        });
    }

    [Test]
    public static void DispatcherRoutesDivisionBoundsAndIndirection()
    {
        WithStore((compiler, store) =>
        {
            var dividend = Node(TYP_INT, store.VNForExpr(null, TYP_INT));
            var divisor = Node(TYP_INT, store.VNForIntCon(0));
            var normal = store.VNForExpr(null, TYP_INT);
            var division = new GenTreeOp(GT_DIV, TYP_INT, dividend, divisor)
            {
                _vnPair = new(normal, normal),
            };
            compiler.fgValueNumberAddExceptionSet(division);
            Assert.That(HasException(store, store.VNExceptionSet(division._vnPair.Liberal),
                VNF_DivideByZeroExc), Is.True);

            var bounds = new GenTreeBoundsChk(dividend, divisor, SpecialCodeKind.SCK_RNGCHK_FAIL)
            {
                _vnPair = ValueNumStore.VNPForVoid(),
            };
            compiler.fgValueNumberAddExceptionSet(bounds);
            Assert.That(HasException(store, store.VNExceptionSet(bounds._vnPair.Liberal),
                VNF_IndexOutOfRangeExc), Is.True);

            var address = Node(TYP_BYREF, store.VNForExpr(null, TYP_BYREF));
            var indirNormal = store.VNForExpr(null, TYP_INT);
            var indir = new GenTreeIndir(GT_IND, TYP_INT, address)
            {
                _vnPair = new(indirNormal, indirNormal),
            };
            compiler.fgValueNumberAddExceptionSet(indir);
            Assert.That(HasException(store, store.VNExceptionSet(indir._vnPair.Liberal),
                VNF_NullPtrExc), Is.True);

            var finiteValue = store.VNForExpr(null, TYP_DOUBLE);
            var finite = new GenTreeUnOp(GT_CKFINITE, TYP_DOUBLE, Node(TYP_DOUBLE, finiteValue))
            {
                _vnPair = new(finiteValue, finiteValue),
            };
            compiler.fgValueNumberAddExceptionSet(finite);
            Assert.That(HasException(store, store.VNExceptionSet(finite._vnPair.Liberal),
                VNF_ArithmeticExc), Is.True);

            var checkedNormal = store.VNForFuncNoFolding(TYP_INT, VNF_ADD_OVF,
                dividend._vnPair.Liberal, store.VNForIntCon(1));
            var checkedAdd = new GenTreeOp(GT_ADD, TYP_INT, dividend, Node(TYP_INT, store.VNForIntCon(1)))
            {
                Flags = GTF_OVERFLOW | GTF_EXCEPT,
                _vnPair = new(checkedNormal, checkedNormal),
            };
            compiler.fgValueNumberAddExceptionSet(checkedAdd);
            Assert.That(HasException(store, store.VNExceptionSet(checkedAdd._vnPair.Liberal),
                VNF_OverflowExc), Is.True);
        });
    }

    private static GenTree Node(var_types type, int liberal, int? conservative = null)
    {
        return new GenTree(GT_NOP, type)
        {
            _vnPair = new(liberal, conservative ?? liberal),
        };
    }

    private static bool HasException(ValueNumStore store, int set, VNFunc func)
    {
        while (set != ValueNumStore.VNForEmptyExcSet())
        {
            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(set, ref app) && app.FuncIs(VNF_ExcSetCons), Is.True);
            var exception = new VNFuncApp();
            Assert.That(store.GetVNFunc(app.GetArg(0), ref exception), Is.True);
            if (exception.FuncIs(func))
            {
                return true;
            }
            set = app.GetArg(1);
        }
        return false;
    }

    private static VNFuncApp FindException(ValueNumStore store, int set, VNFunc func)
    {
        while (set != ValueNumStore.VNForEmptyExcSet())
        {
            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(set, ref app) && app.FuncIs(VNF_ExcSetCons), Is.True);
            var exception = new VNFuncApp();
            Assert.That(store.GetVNFunc(app.GetArg(0), ref exception), Is.True);
            if (exception.FuncIs(func))
            {
                return exception;
            }
            set = app.GetArg(1);
        }
        Assert.Fail($"Expected {func} in exception set.");
        return default;
    }

    private static void WithStore(Action<Compiler, ValueNumStore> action)
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
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            action(compiler, store);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
