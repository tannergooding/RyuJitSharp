// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.MemoryKind;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class MemorySsaAccessTests
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void MapsUseInlineRootStorageAndCallerMemorySharing(bool inline, bool shared)
    {
        SsaLivenessTests.WithCompiler(0, root =>
        {
            var caller = root;

            if (inline)
            {
                caller = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
                caller.impInlineInfo = new InlineInfo { InlineRoot = root };
            }

            caller.byrefStatesMatchGcHeapStates = shared;
            var heap = caller.GetMemorySsaMap(GcHeap);
            var byref = caller.GetMemorySsaMap(ByrefExposed);
            var node = new GenTreeLclVar(var_types.TYP_INT, 0);
            heap[node] = 7;

            Assert.That(caller.GetMemorySsaMap(GcHeap), Is.SameAs(heap));
            Assert.That(root._memorySsaMap[(int)ByrefExposed], Is.SameAs(byref));
            Assert.That(byref.ContainsKey(node), Is.EqualTo(shared));

            if (shared)
            {
                Assert.That(heap, Is.SameAs(byref));
                Assert.That(root._memorySsaMap[(int)GcHeap], Is.Null);
            }
            else
            {
                Assert.That(heap, Is.Not.SameAs(byref));
                Assert.That(root._memorySsaMap[(int)GcHeap], Is.SameAs(heap));
            }

            if (inline)
            {
                Assert.That(caller._memorySsaMap[(int)ByrefExposed], Is.Null);
                Assert.That(caller._memorySsaMap[(int)GcHeap], Is.Null);
            }
        });
    }

    [Test]
    public static void MemoryNumbersAndDefinitionReferencesUseCompilerOwnedStorage()
    {
        SsaLivenessTests.WithCompiler(0, compiler =>
        {
            var first = compiler.AllocMemorySsaNum();
            var second = compiler.AllocMemorySsaNum();
            Assert.That(first, Is.EqualTo(SsaConfig.FIRST_SSA_NUM));
            Assert.That(second, Is.EqualTo(first + 1));
            Assert.That(Unsafe.AreSame(ref compiler.GetMemoryPerSsaData(first),
                ref compiler.GetMemoryPerSsaData(first)), Is.True);
            Assert.That(Unsafe.AreSame(ref compiler.GetMemoryPerSsaData(first),
                ref compiler.GetMemoryPerSsaData(second)), Is.False);

            compiler.fgResetForSsa(deepClean: true);
            Assert.That(compiler.AllocMemorySsaNum(), Is.EqualTo(first));
        });
    }
}
