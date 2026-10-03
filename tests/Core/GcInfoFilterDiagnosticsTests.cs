// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if DEBUG && TARGET_AMD64 && WINDOWS_AMD64_ABI
using System;
using System.Collections.Generic;
using NUnit.Framework;
using static RyuJitSharp.EHHandlerType;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class GcInfoFilterDiagnosticsTests
{
    [TestCase(2u, 12u, false)]
    [TestCase(2u, 6u, false)]
    [TestCase(6u, 12u, true)]
    [TestCase(5u, 7u, false)]
    [TestCase(4u, 4u, false)]
    [TestCase(8u, 12u, false)]
    public static void FilterSplitsAndPinningDumpNativeOldAndNewDescriptors(
        uint begin, uint end, bool byref)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.verbose = true;
            compiler.opts.dspDiffable = true;
            var filterGroup = new insGroup { igOffs = 4 };
            var handlerGroup = new insGroup { igOffs = 8 };
            filterGroup.igSelf = filterGroup;
            handlerGroup.igSelf = handlerGroup;
            compiler.compHndBBtab =
            [
                new EHblkDsc
                {
                    ebdHandlerType = EH_HANDLER_FILTER,
                    ebdFilter = new BasicBlock(null, null) { bbEmitCookie = filterGroup },
                    ebdHndBeg = new BasicBlock(null, null) { bbEmitCookie = handlerGroup },
                },
            ];
            compiler.compHndBBtabCount = 1;
            codeGen.GCInfo.gcVarPtrList = new GCInfo.varPtrDsc
            {
                vpdVarNum = unchecked((uint)-16) | (byref ? 1u : 0u),
                vpdBegOfs = begin,
                vpdEndOfs = end,
            };

            var actual = CodeGenLifeTransitionTests.Capture(() => codeGen.GCInfo.gcMarkFilterVarsPinned());
            var old = Descriptor(begin, end, byref, false);
            var expected = (begin, end) switch
            {
                (2, 12) => $"Splitting lifetime for filter: [0004, 0008).{Environment.NewLine}" +
                    $"Old: {old}" +
                    $"New (1 of 3): {Descriptor(2, 4, byref, false)}" +
                    $"New (2 of 3): {Descriptor(4, 8, byref, true)}" +
                    $"New (3 of 3): {Descriptor(8, 12, byref, false)}",
                (2, 6) => $"Splitting lifetime for filter.{Environment.NewLine}" +
                    $"Old: {old}" +
                    $"New (1 of 2): {Descriptor(2, 4, byref, false)}" +
                    $"New (2 of 2): {Descriptor(4, 6, byref, true)}",
                (6, 12) => $"Splitting lifetime for filter.{Environment.NewLine}" +
                    $"Old: {old}" +
                    $"New (1 of 2): {Descriptor(6, 8, byref, true)}" +
                    $"New (2 of 2): {Descriptor(8, 12, byref, false)}",
                (5, 7) => $"Pinning lifetime for filter.{Environment.NewLine}" +
                    $"Old: {old}" +
                    $"New : {Descriptor(5, 7, byref, true)}",
                _ => "",
            };
            Assert.That(actual, Is.EqualTo(expected));
        });
    }

    [Test]
    public static void FilterSplittingPreservesDescriptorRangesAndListOrder()
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, REG_RAX, (compiler, codeGen, _) =>
        {
            var filterGroup = new insGroup { igOffs = 4 };
            var handlerGroup = new insGroup { igOffs = 8 };
            filterGroup.igSelf = filterGroup;
            handlerGroup.igSelf = handlerGroup;
            compiler.compHndBBtab =
            [
                new EHblkDsc
                {
                    ebdHandlerType = EH_HANDLER_FILTER,
                    ebdFilter = new BasicBlock(null, null) { bbEmitCookie = filterGroup },
                    ebdHndBeg = new BasicBlock(null, null) { bbEmitCookie = handlerGroup },
                },
            ];
            compiler.compHndBBtabCount = 1;
            var original = new GCInfo.varPtrDsc
            {
                vpdVarNum = unchecked((uint)-16) | 1u,
                vpdBegOfs = 2,
                vpdEndOfs = 12,
            };
            codeGen.GCInfo.gcVarPtrList = original;

            codeGen.GCInfo.gcMarkFilterVarsPinned();

            var actual = new List<(uint Begin, uint End, bool Pinned, bool Byref)>();
            for (var descriptor = codeGen.GCInfo.gcVarPtrList; descriptor is not null; descriptor = descriptor.vpdNext)
            {
                actual.Add((
                    descriptor.vpdBegOfs,
                    descriptor.vpdEndOfs,
                    (descriptor.vpdVarNum & 2u) != 0,
                    (descriptor.vpdVarNum & 1u) != 0));
            }

            Assert.That(actual, Is.EqualTo(new[]
            {
                (8u, 12u, false, true),
                (4u, 8u, true, true),
                (2u, 4u, false, true),
            }));
            var tail = codeGen.GCInfo.gcVarPtrList;
            while ((tail is not null) && (tail.vpdNext is not null))
            {
                tail = tail.vpdNext;
            }
            Assert.That(tail, Is.SameAs(original));
        });
    }

    private static string Descriptor(uint begin, uint end, bool byref, bool pinned)
        => $"[00000000D1FFAB1E] {(byref ? "byr" : "gcr")}{(pinned ? "pinned-ptr" : "")} " +
            $"var at [rbp-0x10] live from {begin:X4} to {end:X4}{Environment.NewLine}";
}
#endif
