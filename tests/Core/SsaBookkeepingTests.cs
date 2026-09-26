// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class SsaBookkeepingTests
{
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public static void LocalDefinitionsStartWithUnsetValueNumbers(int constructor)
    {
        SsaLivenessTests.WithCompiler(1, compiler =>
        {
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            var definition = constructor switch
            {
                0 => new LclSsaVarDsc(),
                1 => new LclSsaVarDsc(block),
                _ => new LclSsaVarDsc(block,
                    compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 1))),
            };
            Assert.That(definition._vnPair, Is.EqualTo(new ValueNumPair()));
#if DEBUG
            Assert.That(definition._origVNPair, Is.EqualTo(new ValueNumPair()));
#endif
        });
    }

    [TestCase(0, 0)]
    [TestCase(4, 4)]
    [TestCase(4, 16)]
    [TestCase(32767, 32768)]
    [TestCase(32768, 65536)]
    public static void OutlinedCreationPreservesPrefixAndInitializesFields(int prefixCount, int capacity)
    {
        WithPromotedLocal(compiler => {
            var table = new List<int>(capacity);
            for (var index = 0; index < prefixCount; index++)
            {
                table.Add(index + 1);
            }
            compiler._outlinedCompositeSsaNums = table;

            var number = SsaNumInfo.Composite(default, compiler, 0, 2, 128);

            Assert.That(number.IsComposite, Is.True);
            Assert.That(compiler._outlinedCompositeSsaNums, Is.SameAs(table));
            Assert.That(table.Count, Is.EqualTo(prefixCount + 4));
            AssertFields(compiler, number, [0, 0, 128, 0]);
            for (var index = 0; index < prefixCount; index++)
            {
                Assert.That(table[index], Is.EqualTo(index + 1));
            }
        });
    }

    [TestCase(0)]
    [TestCase(4)]
    public static void OutliningCopiesAllCompactFieldsBeforeOverwriting(int prefixCount)
    {
        WithPromotedLocal(compiler => {
            compiler._outlinedCompositeSsaNums = [.. new int[prefixCount]];
            SsaNumInfo compact = default;
            int[] fields = [1, 2, 127, 3];
            for (var index = 0; index < fields.Length; index++)
            {
                compact = SsaNumInfo.Composite(compact, compiler, 0, index, fields[index]);
            }
            Assert.That(compiler._outlinedCompositeSsaNums.Count, Is.EqualTo(prefixCount));

            var outlined = SsaNumInfo.Composite(compact, compiler, 0, 1, 128);

            AssertFields(compiler, compact, fields);
            AssertFields(compiler, outlined, [1, 128, 127, 3]);
            Assert.That(compiler._outlinedCompositeSsaNums.Count, Is.EqualTo(prefixCount + 4));
        });
    }

    [Test]
    public static void UpdatingOutlinedNumbersPreservesIdentityAndAliases()
    {
        WithPromotedLocal(compiler => {
            var original = SsaNumInfo.Composite(default, compiler, 0, 1, 128);
            var table = compiler._outlinedCompositeSsaNums;
            var updated = SsaNumInfo.Composite(original, compiler, 0, 1, 3);
            updated = SsaNumInfo.Composite(updated, compiler, 0, 3, 255);

            Assert.That(updated, Is.EqualTo(original));
            Assert.That(compiler._outlinedCompositeSsaNums, Is.SameAs(table));
            Assert.That(table, Has.Count.EqualTo(4));
            AssertFields(compiler, original, [0, 3, 0, 255]);
        });
    }

    [Test]
    public static void ReusingResetStorageDoesNotExposeOldFieldNumbers()
    {
        WithPromotedLocal(compiler => {
            var table = new List<int>(16) { 128, 129, 130, 131 };
            compiler._outlinedCompositeSsaNums = table;
            compiler.fgResetForSsa(true);

            var number = SsaNumInfo.Composite(default, compiler, 0, 2, 256);

            Assert.That(compiler._outlinedCompositeSsaNums, Is.SameAs(table));
            Assert.That(table.Capacity, Is.EqualTo(16));
            AssertFields(compiler, number, [0, 0, 256, 0]);
        });
    }

    [Test]
    public static void MemoryPhiPlaceholderIsDistinctFromAbsentAndPopulatedPhis()
    {
        SsaLivenessTests.WithCompiler(0, compiler => {
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            var placeholder = BasicBlock.EmptyMemoryPhiDef;
            var argument = new BasicBlock.MemoryPhiArg(SsaConfig.FIRST_SSA_NUM);
            Assert.That(placeholder, Is.Not.Null);
            Assert.That(argument, Is.Not.SameAs(placeholder));
            foreach (var kind in new AllMemoryKinds())
            {
                Assert.That(block.bbMemorySsaPhiFunc[(int)kind], Is.Null);
                block.bbMemorySsaPhiFunc[(int)kind] = placeholder;
                Assert.That(block.bbMemorySsaPhiFunc[(int)kind], Is.SameAs(placeholder));
                block.bbMemorySsaPhiFunc[(int)kind] = argument;
                Assert.That(block.bbMemorySsaPhiFunc[(int)kind], Is.SameAs(argument));
                block.bbMemorySsaPhiFunc[(int)kind] = placeholder;
            }

            compiler.fgResetForSsa(false);

            foreach (var kind in new AllMemoryKinds())
            {
                Assert.That(block.bbMemorySsaPhiFunc[(int)kind], Is.Null);
            }
            Assert.That(BasicBlock.EmptyMemoryPhiDef, Is.SameAs(placeholder));
        });
    }

#if DEBUG
    [TestCase(0, 0, false)]
    [TestCase(0, 1, false)]
    [TestCase(0, 2, false)]
    [TestCase(1, 2, true)]
    [TestCase(1, 3, false)]
    [TestCase(2, 3, true)]
    public static void StressCompactEncodingUsesUnsignedSubtraction(int index, int ssaNum, bool compact)
    {
        WithPromotedLocal(compiler => {
            compiler.compAllowStress = true;
            compiler.info.compMethodName = nameof(StressCompactEncodingUsesUnsignedSubtraction);
            fixed (byte* names = "STRESS_SSA_INFO\0"u8)
            {
                StressNames(ref JitConfig) = names;
                var number = SsaNumInfo.Composite(default, compiler, 0, index, ssaNum);

                Assert.That(compiler._outlinedCompositeSsaNums is null, Is.EqualTo(compact));
                Assert.That(number.GetNum(compiler, index), Is.EqualTo(ssaNum));
                Assert.That(compiler.compActiveStressModes[(int)Compiler.STRESS_SSA_INFO], Is.EqualTo(1));
            }
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitStressModeNames")]
    private static extern ref byte* StressNames(ref JitConfigValues config);
#endif

    private static void WithPromotedLocal(Action<Compiler> action)
    {
        SsaLivenessTests.WithCompiler(5, compiler => {
            ref var parent = ref compiler.lvaTable[0];
            parent.Type = TYP_STRUCT;
            parent.lvPromoted = true;
            parent.lvFieldCnt = 4;
            parent.lvFieldLclStart = 1;
            for (var index = 1; index < compiler.lvaCount; index++)
            {
                compiler.lvaTable[index].lvIsStructField = true;
                compiler.lvaTable[index].lvParentLcl = 0;
            }
            action(compiler);
        });
    }

    private static void AssertFields(Compiler compiler, SsaNumInfo number, int[] expected)
    {
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.That(number.GetNum(compiler, index), Is.EqualTo(expected[index]));
        }
    }
}
