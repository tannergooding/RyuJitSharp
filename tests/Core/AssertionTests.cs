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
