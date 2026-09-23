// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Compiler.optOp2Kind;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.SymbolicIntegerValue;
using static RyuJitSharp.var_types;
using AssertionDsc = RyuJitSharp.Compiler.AssertionDsc;
using BitOps = RyuJitSharp.BitSetOps<RyuJitSharp.BitVecTraits, RyuJitSharp.BitVecTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class AssertionTests
{
    [Test]
    public static void PhiQueriesOwnArgumentsAndTraverseConservativeValuesInNativeOrder()
    {
        WithCompiler(compiler => {
            compiler.lvaTable = new LclVarDsc[2];
            ref var definitions = ref compiler.lvaTable[0].lvPerSsaData;
            var first = definitions.AllocSsaNum();
            var second = definitions.AllocSsaNum();
            var third = definitions.AllocSsaNum();
            var fourth = definitions.AllocSsaNum();
            var store = new ValueNumStore(compiler);
            var seven = store.VNForIntCon(7);
            var eleven = store.VNForIntCon(11);
            var negative = store.VNForIntCon(-1);
            int[] arguments = [first, second, first];
            var phi = store.VNForPhiDef(TYP_INT, 0, fourth, arguments);
            var nested = store.VNForPhiDef(TYP_INT, 0, second, [third, fourth]);
            arguments[0] = fourth;
            definitions.GetSsaDef(first)._vnPair = new(negative, seven);
            definitions.GetSsaDef(second)._vnPair.SetBoth(nested);
            definitions.GetSsaDef(third)._vnPair.SetBoth(eleven);
            definitions.GetSsaDef(fourth)._vnPair.SetBoth(phi);
            var visited = new System.Collections.Generic.List<int>();
            Assert.That(store.VNVisitReachingVNs(phi, vn => {
                visited.Add(vn);
                return ValueNumStore.VNVisit.Continue;
            }), Is.EqualTo(ValueNumStore.VNVisit.Continue));
            int[] expected = [eleven, seven];
            Assert.That(visited, Is.EqualTo(expected));
            Assert.That(store.IsVNNeverNegative(phi), Is.True);
            definitions.GetSsaDef(third)._vnPair.Conservative = negative;
            Assert.That(store.IsVNNeverNegative(phi), Is.False);
            VNPhiDef view = default;
            Assert.That(store.GetPhiDef(phi, ref view), Is.True);
            Assert.That(store.GetPhiDef(seven, ref view), Is.False);
            Assert.That(view.SsaArgs.Span[0], Is.EqualTo(first));
            definitions.Reset();
            Assert.That(definitions.AllocSsaNum(), Is.EqualTo(first));
            Assert.That(definitions.Count, Is.EqualTo(1));
        });
    }

    [Test]
    public static void NonnegativeVNsSupplySignedComparisonAndCheckedBoundFacts()
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var unknown = store.VNForExpr(null, TYP_INT);
            var zero = store.VNForIntCon(0);
            var length = store.VNForFuncNoFolding(TYP_INT, VNFunc.VNF_MDArrLength,
                store.VNForExpr(null, TYP_REF), zero);
            var comparison = store.VNForFuncNoFolding(TYP_INT, VNFunc.VNF_LT, unknown, zero);
            Assert.That(store.IsVNNeverNegative(ValueNumStore.NoVN), Is.False);
            Assert.That(store.IsVNNeverNegative(store.VNForLongCon(long.MinValue)), Is.False);
            Assert.That(store.IsVNNeverNegative(store.VNForLongCon(long.MaxValue)), Is.True);
            Assert.That(store.IsVNNeverNegative(store.VNForFloatCon(1)), Is.False);
            Assert.That(store.IsVNNeverNegative(unknown), Is.False);
            Assert.That(store.IsVNNeverNegative(length), Is.True);
            Assert.That(store.IsVNNeverNegative(comparison), Is.True);
            var bounds = AssertionDsc.CreateCompareCheckedBound(compiler, VNFunc.VNF_LT, unknown, length, -5);
            Assert.That(bounds.Op2.IsVNNeverNegative, Is.True);
            Assert.That(bounds.Op2.Cns, Is.EqualTo(-5));
            Assert.That(AssertionDsc.CreateRelopVN(compiler, VNFunc.VNF_LT, unknown, comparison).Op2.IsVNNeverNegative, Is.True);
            Assert.That(AssertionDsc.CreateRelopVN(compiler, VNFunc.VNF_LT, length, unknown).Op2.IsVNNeverNegative, Is.False);
        }, local: false);
    }

    [TestCase(16, TYP_UBYTE, true)]
    [TestCase(32, TYP_UBYTE, false)]
    [TestCase(64, TYP_INT, true)]
    [TestCase(64, TYP_SHORT, false)]
    public static void IntrinsicNonnegativityRetainsNativeElementCountThreshold(int size, var_types baseType, bool expected)
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            var simdType = store.VNForFuncNoFolding(TYP_REF, VNFunc.VNF_SimdType,
                store.VNForIntCon(size), store.VNForIntCon((int)baseType));
            var vector = store.VNForExpr(null, TYP_SIMD16);
            var mask = store.VNForFuncNoFolding(TYP_INT, VNFunc.VNF_HWI_Vector_ExtractMostSignificantBits, vector, simdType);
            Assert.That(store.IsVNNeverNegative(mask), Is.EqualTo(expected));
        });
    }

    [TestCase(31, VNFunc.VNF_HWI_AVX2_LeadingZeroCount, TYP_INT)]
    [TestCase(63, VNFunc.VNF_HWI_AVX2_X64_LeadingZeroCount, TYP_LONG)]
    public static void Log2ValueNumbersRecognizeNativePattern(int xorBy, VNFunc lzcnt, var_types type)
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            var variable = store.VNForExpr(null, type);
            var one = type == TYP_INT ? store.VNForIntCon(1) : store.VNForLongCon(1);
            var operand = store.VNForFuncNoFolding(type, VNFunc.VNF_OR, variable, one);
            var simdType = store.VNForFuncNoFolding(TYP_REF, VNFunc.VNF_SimdType,
                store.VNForIntCon(0), store.VNForIntCon((int)type));
            var count = store.VNForFuncNoFolding(type, lzcnt, operand, simdType);
            var constant = type == TYP_INT ? store.VNForIntCon(xorBy) : store.VNForLongCon(xorBy);
            var log2 = store.VNForFuncNoFolding(type, VNFunc.VNF_XOR, count, constant);
            var upperBound = -1;
            Assert.That(store.IsVNLog2(log2, ref upperBound), Is.True);
            Assert.That(upperBound, Is.EqualTo(xorBy));
            Assert.That(store.IsVNNeverNegative(log2), Is.True);
            Assert.That(store.IsVNLog2(variable, ref upperBound), Is.False);
            Assert.That(upperBound, Is.EqualTo(xorBy));
        });
    }

    [Test]
    public static void ValueNumberChunksRetainReservedIdsAndAllocationOrder()
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            Assert.That(store.VNIsValid(ValueNumStore.VNForNull()), Is.True);
            Assert.That(store.VNIsValid(ValueNumStore.VNForVoid()), Is.True);
            Assert.That(store.VNIsValid(ValueNumStore.VNForEmptyExcSet()), Is.True);
            Assert.That(store.VNIsValid(3), Is.False);
            Assert.That(store.VNIsValid(ValueNumStore.NoVN), Is.False);
            Assert.That(store.IsVNConstant(ValueNumStore.VNForVoid()), Is.False);
            for (var index = 0; index < 65; index++)
            {
                var vn = store.VNForIntCon(1000 + index);
                Assert.That(vn, Is.EqualTo(64 + index));
                Assert.That(store.ConstantValue<int>(vn), Is.EqualTo(1000 + index));
            }

            Assert.That(store.VNForIntCon(1000), Is.EqualTo(64));
            Assert.That(store.VNForLongCon(1000), Is.EqualTo(192));
            Assert.That(store.VNForIntCon(0), Is.EqualTo(store.VNForIntCon(0)));
            Assert.That(store.IsVNIntegralConstant(store.VNForLongCon(-1), out uint value), Is.False);
            Assert.That(value, Is.Zero);
            var handle = store.VNForHandle(123, GTF_ICON_CLASS_HDL);
            Assert.That(store.IsVNTypeHandle(handle), Is.True);
            Assert.That(store.ConstantValue<nint>(handle), Is.EqualTo((nint)123));
            Assert.That(store.VNForHandle(123, GTF_ICON_CLASS_HDL), Is.EqualTo(handle));
            Assert.That(store.VNForHandle(123, GTF_ICON_OBJ_HDL), Is.Not.EqualTo(handle));
            Assert.That(sizeof(simd12_t), Is.EqualTo(12));
        });
    }

    [Test]
    public static void ValueNumberFloatingConstantsPreserveBitIdentity()
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            Assert.That(store.VNForDoubleCon(0.0), Is.Not.EqualTo(store.VNForDoubleCon(-0.0)));
            Assert.That(store.VNForFloatCon(0.0f), Is.Not.EqualTo(store.VNForFloatCon(-0.0f)));
            var nan = BitConverter.Int64BitsToDouble(0x7ff8000000000001);
            var vn = store.VNForDoubleCon(nan);
            Assert.That(store.VNForDoubleCon(nan), Is.EqualTo(vn));
            Assert.That(store.VNForDoubleCon(BitConverter.Int64BitsToDouble(0x7ff8000000000002)), Is.Not.EqualTo(vn));
            Assert.That(BitConverter.DoubleToInt64Bits(store.ConstantValue<double>(vn)), Is.EqualTo(0x7ff8000000000001));
            Assert.That(BitConverter.DoubleToInt64Bits(store.ConstantValue<double>(store.VNForDoubleCon(-0.0))), Is.EqualTo(long.MinValue));
        });
    }

    [Test]
    public static void GlobalInsertionRegistersBothBoundsAndUnderlyingAdditionOperand()
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var variable = store.VNForExpr(null, TYP_INT);
            var bound = store.VNForExpr(null, TYP_INT);
            var constant = store.VNForIntCon(5);
            var sum = store.VNForFuncNoFolding(TYP_INT, VNFunc.VNF_ADD, variable, constant);
            Assert.That(store.VNForFuncNoFolding(TYP_INT, VNFunc.VNF_ADD, variable, constant), Is.EqualTo(sum));
            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(sum, ref app), Is.True);
            for (var index = 0; index < 512; index++)
            {
                _ = store.VNForExpr(null, TYP_INT);
            }

            Assert.That(app.GetArg(0), Is.EqualTo(variable));
            Assert.That(app.GetArg(1), Is.EqualTo(constant));
            var reversed = store.VNForFuncNoFolding(TYP_INT, VNFunc.VNF_ADD, constant, variable);
            Assert.That(reversed, Is.Not.EqualTo(sum));
            var operand = ValueNumStore.NoVN;
            var addend = 0;
            Assert.That(store.IsVNBinFuncWithConst(reversed, VNFunc.VNF_ADD, ref operand, ref addend), Is.True);
            Assert.That(operand, Is.EqualTo(variable));
            Assert.That(addend, Is.EqualTo(5));
            Assert.That(store.IsVNBinFuncWithConst(reversed, VNFunc.VNF_SUB, ref operand, ref addend), Is.False);
            Assert.That(operand, Is.EqualTo(variable));
            Assert.That(addend, Is.EqualTo(5));
            Assert.That(store.GetVNFunc(constant, ref app), Is.False);
            Assert.That(app.GetArg(0), Is.EqualTo(variable));
            var bounds = AssertionDsc.CreateNoThrowArrBnd(compiler, sum, bound);
            var assertion = compiler.optAddAssertion(bounds);
            Assert.That(compiler.optAddAssertion(bounds), Is.EqualTo(assertion));
            Assert.That(compiler.optAssertionHasAssertionsForVN(variable, false), Is.True);
            Assert.That(compiler.optAssertionHasAssertionsForVN(sum, false), Is.True);
            Assert.That(compiler.optAssertionHasAssertionsForVN(bound, false), Is.True);
            var nonNull = compiler.optAddAssertion(AssertionDsc.CreateVNNonNullAssertion(compiler, store.VNForExpr(null, TYP_REF)));
            compiler.optCreateComplementaryAssertion(nonNull);
            Assert.That(compiler.optFindComplementary(nonNull), Is.Not.Zero);
        }, local: false);
    }

    [Test]
    public static void LocalInsertionDeduplicatesAtCapacityAndResetsCopyDependencies()
    {
        WithCompiler(compiler => {
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var copy = AssertionDsc.CreateLclvarCopy(compiler, 0, 1, true);
            Assert.That(compiler.optAddAssertion(copy), Is.EqualTo(1));
            Assert.That(BitOps.IsMember(traits, compiler.GetAssertionDep(1), 0), Is.True);
            for (var index = 1; index < 64; index++)
            {
                Assert.That(compiler.optAddAssertion(IntAssertion(compiler, index)), Is.EqualTo(index + 1));
            }

            Assert.That(compiler.optAddAssertion(IntAssertion(compiler, 63)), Is.EqualTo(64));
            Assert.That(compiler.optAddAssertion(IntAssertion(compiler, 64)), Is.Zero);
            compiler.optAssertionReset();
            Assert.That(compiler.AssertionCount, Is.Zero);
            Assert.That(BitOps.IsEmpty(traits, compiler.GetAssertionDep(0)), Is.True);
            Assert.That(BitOps.IsEmpty(traits, compiler.GetAssertionDep(1)), Is.True);
        }, crossBlock: false);
    }

    [TestCase(0, false, 16)]
    [TestCase(1, false, 20)]
    [TestCase(3, false, 15)]
    [TestCase(1, true, 20)]
    public static void StoresInvalidateParentAndFieldDependenciesInBothOrders(int local, bool emptyPreorder, long remaining)
    {
        WithCompiler(compiler => {
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            compiler.lvaCount = 4;
            compiler.lvaTable = [
                new LclVarDsc { Type = TYP_STRUCT, lvPromoted = true, lvFieldLclStart = 1, lvFieldCnt = 2 },
                new LclVarDsc { Type = TYP_INT, lvIsStructField = true, lvParentLcl = 0 },
                new LclVarDsc { Type = TYP_INT, lvIsStructField = true, lvParentLcl = 0 },
                new LclVarDsc { Type = TYP_INT },
            ];
            var active = InstallAssertions(compiler, [
                AssertionDsc.CreateConstLclVarAssertion(compiler, 0, ValueNumStore.NoVN, O2K_ZEROOBJ, ValueNumStore.NoVN, true),
                AssertionDsc.CreateConstLclVarAssertion(compiler, 1, ValueNumStore.NoVN, (nint)3, ValueNumStore.NoVN, true),
                AssertionDsc.CreateConstLclVarAssertion(compiler, 2, ValueNumStore.NoVN, (nint)4, ValueNumStore.NoVN, true),
                AssertionDsc.CreateLclvarCopy(compiler, 1, 2, true),
                AssertionDsc.CreateConstLclVarAssertion(compiler, 3, ValueNumStore.NoVN, (nint)5, ValueNumStore.NoVN, true),
            ]);
            compiler.apLocal = emptyPreorder ? BitOps.MakeEmpty(traits) : BitOps.MakeCopy(traits, active);
            compiler.apLocalPostorder = BitOps.MakeCopy(traits, active);
            compiler.fgKillDependentAssertions(local, compiler.gtNewLclvNode(TYP_INT, 3));
            Assert.That((long)compiler.apLocal[0], Is.EqualTo(emptyPreorder ? 0 : remaining));
            Assert.That((long)compiler.apLocalPostorder[0], Is.EqualTo(remaining));
            Assert.That((long)active[0], Is.EqualTo(31));
        });
    }

    [Test]
    public static void LocalQueriesRespectLiveSetsOrderingAndAccessWidth()
    {
        WithCompiler(compiler => {
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            compiler.lvaTable = [new LclVarDsc { Type = TYP_INT }, new LclVarDsc { Type = TYP_INT }];
            var active = InstallAssertions(compiler, [
                IntAssertion(compiler, 2).Reverse(),
                IntAssertion(compiler, 4),
                AssertionDsc.CreateSubrange(compiler, 1, new(Zero, One)),
                AssertionDsc.CreateSubrange(compiler, 0, new(Zero, One)),
            ]);
            var tree = compiler.gtNewLclvNode(TYP_INT, 0);
            Assert.That(compiler.optLocalAssertionIsEqualOrNotEqual(Compiler.optOp1Kind.O1K_LCLVAR, 0, O2K_CONST_INT, 2, active), Is.EqualTo(1));
            Assert.That(compiler.optLocalAssertionIsEqualOrNotEqual(Compiler.optOp1Kind.O1K_LCLVAR, 0, O2K_CONST_INT, 3, active), Is.EqualTo(2));
            Assert.That(compiler.optAssertionIsSubrange(tree, new(Zero, IntMax), active), Is.EqualTo(4));
            BitOps.RemoveElemD(traits, active, 1);
            BitOps.RemoveElemD(traits, active, 3);
            Assert.That(compiler.optLocalAssertionIsEqualOrNotEqual(Compiler.optOp1Kind.O1K_LCLVAR, 0, O2K_CONST_INT, 3, active), Is.Zero);
            Assert.That(compiler.optAssertionIsSubrange(tree, new(Zero, IntMax), active), Is.Zero);

            var field = new LclVarDsc { Type = TYP_SHORT, lvIsStructField = true };
            Assert.That(Compiler.optAssertionProp_LclVarTypeCheck(tree, compiler.lvaTable[0], field), Is.False);
            field.lvIsStructField = false;
            Assert.That(Compiler.optAssertionProp_LclVarTypeCheck(tree, compiler.lvaTable[0], field), Is.True);
        });
    }

    private static nint[] InstallAssertions(Compiler compiler, Compiler.AssertionDsc[] assertions)
    {
        var table = typeof(Compiler).GetField("optAssertionTabPrivate", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException();
        var count = typeof(Compiler).GetField("optAssertionCount", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException();
        table.SetValue(compiler, assertions);
        count.SetValue(compiler, (ushort)assertions.Length);
        var traits = compiler.apTraits ?? throw new InvalidOperationException();
        var active = BitOps.MakeEmpty(traits);
        for (var index = 0; index < assertions.Length; index++)
        {
            BitOps.AddElemD(traits, active, index);
            BitOps.AddElemD(traits, compiler.GetAssertionDep(assertions[index].Op1.LclNum), index);
            if (assertions[index].Op2.KindIs(O2K_LCLVAR_COPY))
            {
                BitOps.AddElemD(traits, compiler.GetAssertionDep(assertions[index].Op2.LclNum), index);
            }
        }

        return active;
    }

    [Test]
    public static void DependenciesExpandWithoutDroppingExistingBits()
    {
        WithCompiler(compiler => {
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var firstDep = compiler.GetAssertionDep(0);
            BitOps.AddElemD(traits, firstDep, 63);

            compiler.lvaCount = 201;
            Assert.That(BitOps.IsEmpty(traits, compiler.GetAssertionDep(200)), Is.True);
            Assert.That(compiler.GetAssertionDep(0), Is.SameAs(firstDep));
            Assert.That(BitOps.IsMember(traits, compiler.GetAssertionDep(0), 63), Is.True);
        }, crossBlock: false);
    }

    [TestCase(GTF_EMPTY)]
    [TestCase(GTF_ICON_CLASS_HDL)]
    [TestCase(GTF_ICON_HDL_MASK)]
    public static void IntegerFlagsRoundTripIncludingHighBit(GenTreeFlags flags)
    {
        WithCompiler(compiler => {
            var assertion = AssertionDsc.CreateConstLclVarAssertion(compiler, 0, ValueNumStore.NoVN,
                (nint)42, ValueNumStore.NoVN, true, flags);
            Assert.That(assertion.Op2.IconFlag, Is.EqualTo(flags));
            Assert.That(assertion.Op2.HasIconFlag, Is.EqualTo(flags != GTF_EMPTY));
            Assert.That(assertion.Equals(IntAssertion(compiler, 42), false), Is.EqualTo(flags == GTF_EMPTY));
        });
    }

    [Test]
    public static void NullHintDistinguishesReferenceNullFromIntegralZero()
    {
        WithCompiler(compiler => {
            var nonNull = AssertionDsc.CreateLclNonNullAssertion(compiler, 0);
            Assert.That(nonNull.CanPropNonNull, Is.True);
            Assert.That(nonNull.Reverse().CanPropNonNull, Is.False);
            Assert.That(IntAssertion(compiler, 0).Reverse().CanPropNonNull, Is.False);
        });
    }

    [TestCase(0L, long.MinValue, false)]
    [TestCase(0L, 0L, true)]
    [TestCase(0x7ff8000000000001L, 0x7ff8000000000001L, true)]
    [TestCase(0x7ff8000000000001L, 0x7ff8000000000002L, false)]
    public static void DoubleEqualityIsBitExact(long left, long right, bool equal)
    {
        WithCompiler(compiler => {
            var first = AssertionDsc.CreateConstLclVarAssertion(compiler, 0, ValueNumStore.NoVN,
                BitConverter.Int64BitsToDouble(left), ValueNumStore.NoVN, true);
            var second = AssertionDsc.CreateConstLclVarAssertion(compiler, 0, ValueNumStore.NoVN,
                BitConverter.Int64BitsToDouble(right), ValueNumStore.NoVN, true);
            Assert.That(first.Equals(second, false), Is.EqualTo(equal));
        });
    }

    [TestCase(TYP_SIMD8)]
    [TestCase(TYP_SIMD12)]
    [TestCase(TYP_SIMD16)]
    [TestCase(TYP_SIMD32)]
    [TestCase(TYP_SIMD64)]
    public static void VectorPayloadIsOwnedAndComparesOnlyActiveBytes(var_types type)
    {
        WithCompiler(compiler => {
            var firstNode = new GenTreeVecCon(type);
            var secondNode = new GenTreeVecCon(type);
            var size = type.Size;
            firstNode.SimdVal.AsSpan<byte>()[size - 1] = 0x80;
            secondNode.SimdVal.AsSpan<byte>()[size - 1] = 0x80;
            secondNode.SimdVal.AsSpan<byte>()[size..].Fill(0xFF);
            var first = AssertionDsc.CreateConstLclVarAssertion(compiler, 0, ValueNumStore.NoVN, firstNode, ValueNumStore.NoVN, true);
            var second = AssertionDsc.CreateConstLclVarAssertion(compiler, 0, ValueNumStore.NoVN, secondNode, ValueNumStore.NoVN, true);
            Assert.That(first.Equals(second, false), Is.True);
            firstNode.SimdVal.AsSpan<byte>()[size - 1] = 0;
            Assert.That(first.Op2.SimdConstant[size - 1], Is.EqualTo(0x80));
            var changed = AssertionDsc.CreateConstLclVarAssertion(compiler, 0, ValueNumStore.NoVN, firstNode, ValueNumStore.NoVN, true);
            Assert.That(first.Equals(changed, false), Is.False);
            Assert.That(first.Reverse().Op2.SimdSize, Is.EqualTo(size));
        });
    }

    [Test]
    public static void GlobalDescriptorsRetainValueNumbersAndBoundFacts()
    {
        WithCompiler(compiler => {
            var nonNull = AssertionDsc.CreateVNNonNullAssertion(compiler, 0x100);
            Assert.That(nonNull.Op1.VN, Is.EqualTo(0x100));
            Assert.That(compiler.optAssertionHasAssertionsForVN(0x100, true), Is.False);
            Assert.That(compiler.optAssertionHasAssertionsForVN(0x100, false), Is.True);
            Assert.That(compiler.optAssertionHasAssertionsForVN(ValueNumStore.NoVN, false), Is.False);
            Assert.That(nonNull.Equals(AssertionDsc.CreateVNNonNullAssertion(compiler, 0x100), true), Is.True);
            var bounds = AssertionDsc.CreateNoThrowArrBnd(compiler, 0x200, 0x300);
            Assert.That(bounds.IsBoundsCheckNoThrow, Is.True);
            Assert.That(bounds.Reverse().IsBoundsCheckNoThrow, Is.False);
        }, local: false);
    }

#if DEBUG
    [Test]
    public static void AssertionDumpsMatchNativeFormattingAndIndexOrder()
    {
        WithCompiler(compiler => {
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            using var stream = new MemoryStream();
            using var writer = new JitTextWriter(stream, leaveOpen: true);
            var previous = s_jitstdout;
            try
            {
                s_jitstdout = writer;
                compiler.optPrintAssertion(AssertionDsc.CreateLclNonNullAssertion(compiler, 0), 1);
                compiler.optPrintAssertion(AssertionDsc.CreateLclvarCopy(compiler, 0, 1, true));
                compiler.optPrintAssertion(AssertionDsc.CreateSubrange(compiler, 0, new(Zero, One)));
                compiler.optPrintAssertion(AssertionDsc.CreateConstLclVarAssertion(compiler, 0, ValueNumStore.NoVN, -0.0, ValueNumStore.NoVN, true));
                compiler.optPrintAssertion(AssertionDsc.CreateConstLclVarAssertion(compiler, 0, ValueNumStore.NoVN, 1.0, ValueNumStore.NoVN, true));
                compiler.optPrintAssertion(AssertionDsc.CreateConstLclVarAssertion(compiler, 0, ValueNumStore.NoVN, O2K_ZEROOBJ, ValueNumStore.NoVN, true));
                compiler.optPrintAssertionIndices(BitOps.MakeEmpty(traits));
                jitprintf("\n");
                var indices = BitOps.MakeEmpty(traits);
                BitOps.AddElemD(traits, indices, 63);
                BitOps.AddElemD(traits, indices, 0);
                compiler.optPrintAssertionIndices(indices);
                writer.Flush();
            }
            finally
            {
                s_jitstdout = previous;
            }

            var expected =
                "#01 lclvar V00 != null\nlclvar V00 == lclvar V01\nlclvar V00 in [0..1]\n"
                + "lclvar V00 == -0.0\nlclvar V00 == 1.00000\nlclvar V00 == ZeroObj\n#NA\n#01 #64";
            Assert.That(Encoding.UTF8.GetString(stream.ToArray()), Is.EqualTo(expected.Replace("\n", Environment.NewLine, StringComparison.Ordinal)));
        });
    }
#endif

    private static AssertionDsc IntAssertion(Compiler compiler, nint value)
        => AssertionDsc.CreateConstLclVarAssertion(compiler, 0, ValueNumStore.NoVN, value, ValueNumStore.NoVN, true);

    private static void SetField(object target, string name, object value)
    {
        var field = typeof(JitConfigValues).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(name);
        field.SetValue(target, value);
    }

    private static void WithCompiler(Action<Compiler> action, bool local = true, int tracked = 0, bool crossBlock = true)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previousCompiler = JitTls.Compiler;
        var previousConfig = JitConfig;
        object config = default(JitConfigValues);
        SetField(config, "_jitMaxLocalsToTrack", 1024);
        SetField(config, "_jitEnableCrossBlockLocalAssertionProp", crossBlock ? 1 : 0);
        JitConfig = (JitConfigValues)config;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.lvaCount = Math.Max(2, tracked);
        compiler.lvaTrackedCount = tracked;
#if DEBUG
        compiler.info.compFullName = "AssertionTests";
#endif
        JitTls.Compiler = compiler;
        try
        {
            compiler.optAssertionInit(local);
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
            JitConfig = previousConfig;
        }
    }
}
