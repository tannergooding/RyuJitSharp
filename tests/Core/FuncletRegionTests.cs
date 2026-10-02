// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
#if DEBUG
using System.Collections.Generic;
using System.Runtime.InteropServices;
#endif
using NUnit.Framework;
using static RyuJitSharp.EHHandlerType;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class FuncletRegionTests
{
    [TestCase(0, 0U)]
    [TestCase(1, 0U)]
    [TestCase(2, 1U)]
    [TestCase(3, 1U)]
    [TestCase(4, 2U)]
    [TestCase(5, 2U)]
    [TestCase(6, 3U)]
    [TestCase(7, 3U)]
    [TestCase(8, 65535U)]
    [TestCase(9, 65535U)]
    public static void RegionIncludesMainAndFuncletInteriors(int blockIndex, uint expected)
    {
        WithGraph((compiler, blocks) =>
            Assert.That(compiler.bbFuncletRegionOf(blocks[blockIndex]), Is.EqualTo(expected)));
    }

    [TestCase(0, 1, true)]
    [TestCase(2, 3, true)]
    [TestCase(4, 5, true)]
    [TestCase(6, 7, true)]
    [TestCase(8, 9, true)]
    [TestCase(4, 6, false)]
    [TestCase(5, 7, false)]
    [TestCase(0, 2, false)]
    [TestCase(3, 8, false)]
    [TestCase(7, 9, false)]
    [TestCase(6, 5, false)]
    [TestCase(9, 9, true)]
    public static void EqualityDistinguishesFiltersFromTheirHandlers(int first, int second, bool expected)
    {
        WithGraph((compiler, blocks) => {
            Assert.That(compiler.bbIsInSameFunclet(blocks[first], blocks[second]), Is.EqualTo(expected));
            Assert.That(compiler.bbIsInSameFunclet(blocks[second], blocks[first]), Is.EqualTo(expected));
        });
    }

#if DEBUG
    private static readonly List<string?> s_assertions = [];

    [TestCase(false)]
    [TestCase(true)]
    public static void QueriesRequireCreatedFuncletsEvenForMainMethodBlocks(bool compare)
    {
        WithGraph((compiler, blocks) => {
            compiler.fgFuncletsCreated = false;

            if (compare)
            {
                _ = compiler.bbIsInSameFunclet(blocks[0], blocks[1]);
            }
            else
            {
                _ = compiler.bbFuncletRegionOf(blocks[0]);
            }
        }, expectedAssertions: compare ? 2 : 1);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertions.Add(Marshal.PtrToStringUTF8((nint)expression));

        return 0;
    }
#endif

    private static void WithGraph(Action<Compiler, BasicBlock[]> action, int expectedAssertions = 0)
    {
#if DEBUG
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        using var tls = new JitTls(&ee);
        s_assertions.Clear();
#endif
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var blocks = new BasicBlock[10];

        for (var index = 0; index < blocks.Length; index++)
        {
            blocks[index] = new BasicBlock(null, null);

            if (index > 0)
            {
                blocks[index - 1].Next = blocks[index];
            }
        }

        blocks[1].TryIndex = 0;
        blocks[2].HndIndex = 0;
        blocks[3].HndIndex = 0;
        blocks[4].HndIndex = 1;
        blocks[5].HndIndex = 1;
        blocks[6].HndIndex = 1;
        blocks[7].HndIndex = 1;
        blocks[8].HndIndex = 2;
        blocks[9].HndIndex = 2;
        compiler.compHndBBtab = [
            new EHblkDsc { ebdHandlerType = EH_HANDLER_CATCH, ebdFuncIndex = 1 },
            new EHblkDsc {
                ebdHandlerType = EH_HANDLER_FILTER,
                ebdFuncIndex = 3,
                ebdFilter = blocks[4],
                ebdHndBeg = blocks[6],
            },
            new EHblkDsc { ebdHandlerType = EH_HANDLER_CATCH, ebdFuncIndex = ushort.MaxValue },
        ];
        compiler.compHndBBtabCount = 3;
        compiler.fgFuncletsCreated = true;

        action(compiler, blocks);
#if DEBUG
        Assert.That(s_assertions, Has.Count.EqualTo(expectedAssertions));
        Assert.That(s_assertions, Is.All.EqualTo("fgFuncletsCreated"));
#endif
    }
}
