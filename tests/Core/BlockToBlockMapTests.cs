// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class BlockToBlockMapTests
{
    [Test]
    public static void KeysFollowNativeBucketAndChainOrder()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler =>
        {
            var blocks = CreateBlocks(compiler);
            var inserted = blocks.Reverse().ToArray();
            var map = CreateMap(inserted);

            Assert.That(map.Keys, Is.EqualTo(GetNativeOrder(inserted)));
        });
    }

#if DEBUG
    [Test]
    public static void StressHashChangesNativeBlockMapOrder()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler =>
        {
            var originalConfig = JitConfig;
            try
            {
                var blocks = CreateBlocks(compiler);
                var inserted = blocks.Reverse().ToArray();

                var defaultConfig = originalConfig;
                SsaStress(ref defaultConfig) = 0;
                JitConfig = defaultConfig;
                var defaultHashes = inserted.Select(block => block.GetHashCode()).ToArray();
                var defaultOrder = GetNativeOrder(inserted);
                Assert.That(CreateMap(inserted).Keys, Is.EqualTo(defaultOrder));

                var stressConfig = originalConfig;
                SsaStress(ref stressConfig) = 2;
                JitConfig = stressConfig;
                var stressHashes = inserted.Select(block => block.GetHashCode()).ToArray();
                var stressOrder = GetNativeOrder(inserted);
                Assert.That(CreateMap(inserted).Keys, Is.EqualTo(stressOrder));
                Assert.That(stressHashes, Is.Not.EqualTo(defaultHashes));
            }
            finally
            {
                JitConfig = originalConfig;
            }
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitSsaStress")]
    private static extern ref int SsaStress(ref JitConfigValues config);
#endif

    private static BasicBlock[] CreateBlocks(Compiler compiler)
    {
        var blocks = new BasicBlock[6];
        for (var index = 0; index < blocks.Length; index++)
        {
            blocks[index] = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
        }

        return blocks;
    }

    private static BlockToBlockMapDictionary CreateMap(IEnumerable<BasicBlock> inserted)
    {
        var map = new BlockToBlockMapDictionary();
        foreach (var block in inserted)
        {
            map.Add(block, block);
        }

        return map;
    }

    internal static BasicBlock[] GetNativeOrder(BasicBlock[] inserted)
    {
        // Six entries fit in the default nine-bucket table without triggering growth.
        var expected = new List<BasicBlock>(inserted.Length);
        for (uint bucket = 0; bucket < 9; bucket++)
        {
            for (var index = inserted.Length - 1; index >= 0; index--)
            {
                var block = inserted[index];
                var hash = unchecked((uint)block.GetHashCode());
                if (jitPrimeInfo[0].magicNumberRem(hash) == bucket)
                {
                    expected.Add(block);
                }
            }
        }

        return [.. expected];
    }
}
