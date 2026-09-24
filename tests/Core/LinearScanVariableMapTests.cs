// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regMask;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinearScanVariableMapTests
{
    [TestCase(1, 4)]
    [TestCase(4, 4)]
    [TestCase(5, 8)]
    public static void InitializationIncludesBlockZeroGapsAndRoundedTrackedSlots(int trackedCount, int mapCount)
    {
        WithCompiler(compiler => {
            compiler.lvaTrackedCount = trackedCount;
            compiler.lvaTrackedFixed = true;
            compiler.fgBBNumMax = 5;
            var allocator = new LinearScan(compiler);
            MaxBlockBeforeResolution(allocator) = 5;

            allocator.initVarRegMaps();

            Assert.That(RegMapCount(allocator), Is.EqualTo((uint)mapCount));
            var inMaps = InMaps(allocator);
            var outMaps = OutMaps(allocator);
            Assert.That(inMaps, Is.Not.Null);
            Assert.That(outMaps, Is.Not.Null);
            assert(inMaps is not null);
            assert(outMaps is not null);
            Assert.That(inMaps, Has.Length.EqualTo(6));
            Assert.That(outMaps, Has.Length.EqualTo(6));
            var identities = new HashSet<regNumber[]>();
            for (var blockNumber = 0; blockNumber <= 5; blockNumber++)
            {
                var inMap = inMaps[blockNumber];
                var outMap = outMaps[blockNumber];
                Assert.That(inMap, Has.Length.EqualTo(mapCount));
                Assert.That(outMap, Has.Length.EqualTo(mapCount));
                Assert.That(inMap, Is.All.EqualTo(regNumber.REG_STK));
                Assert.That(outMap, Is.All.EqualTo(regNumber.REG_STK));
                assert(inMap is not null);
                assert(outMap is not null);
                Assert.That(identities.Add(inMap), Is.True);
                Assert.That(identities.Add(outMap), Is.True);
                Assert.That(allocator.getInVarToRegMap((uint)blockNumber), Is.SameAs(inMap));
                if (blockNumber != 0)
                {
                    Assert.That(allocator.getOutVarToRegMap((uint)blockNumber), Is.SameAs(outMap));
                }
            }

            var scratch = ScratchMap(allocator);
            Assert.That(scratch, Has.Length.EqualTo(mapCount));
            assert(scratch is not null);
            Assert.That(identities.Add(scratch), Is.True);
            Assert.That(allocator.getOutVarToRegMap(0), Is.Null);
        });
    }

    [Test]
    public static void NoTrackedLocalsProducesNullEntriesRatherThanEmptyMaps()
    {
        WithCompiler(compiler => {
            compiler.lvaTrackedFixed = true;
            compiler.fgBBNumMax = 3;
            var allocator = new LinearScan(compiler);
            MaxBlockBeforeResolution(allocator) = 3;

            allocator.initVarRegMaps();

            Assert.That(RegMapCount(allocator), Is.Zero);
            Assert.That(InMaps(allocator), Has.Length.EqualTo(4).And.All.Null);
            Assert.That(OutMaps(allocator), Has.Length.EqualTo(4).And.All.Null);
            Assert.That(ScratchMap(allocator), Is.Null);
            Assert.That(allocator.getInVarToRegMap(2), Is.Null);
            Assert.That(allocator.getOutVarToRegMap(2), Is.Null);
        });
    }

    [Test]
    public static void DisabledLocalEnregistrationDoesNotRequireFixedTrackedLocals()
    {
        WithCompiler(compiler => {
            compiler.opts.compFlags &= ~CLFLG_REGVAR;
            compiler.lvaTrackedCount = 5;
            compiler.fgBBNumMax = 3;
            var allocator = new LinearScan(compiler);

            allocator.initVarRegMaps();

            Assert.That(compiler.lvaTrackedFixed, Is.False);
            Assert.That(InMaps(allocator), Is.Null);
            Assert.That(OutMaps(allocator), Is.Null);
            Assert.That(ScratchMap(allocator), Is.Null);
        });
    }

    [Test]
    public static void LocalNumberSettersTranslateToTrackedIndicesWhileMapAccessorsDoNot()
    {
        WithCompiler(compiler => {
            compiler.lvaTable = new LclVarDsc[4];
            compiler.lvaCount = 4;
            compiler.lvaTrackedCount = 3;
            compiler.lvaTrackedFixed = true;
            compiler.lvaTable[0].lvTracked = true;
            compiler.lvaTable[0]._varIndex = 2;
            compiler.lvaTable[2].lvTracked = true;
            compiler.lvaTable[2]._varIndex = 0;
            compiler.lvaTable[3].lvTracked = true;
            compiler.lvaTable[3]._varIndex = 1;
            compiler.fgBBNumMax = 2;
            var allocator = new LinearScan(compiler);
            MaxBlockBeforeResolution(allocator) = 2;
            allocator.initVarRegMaps();

            allocator.setInVarRegForBB(1, 0, regNumber.REG_RAX);
            allocator.setInVarRegForBB(1, 3, regNumber.REG_XMM2);
            allocator.setOutVarRegForBB(2, 2, regNumber.REG_K7);
            var inMap = allocator.getInVarToRegMap(1);
            var outMap = allocator.getOutVarToRegMap(2);
            assert(inMap is not null);
            assert(outMap is not null);

            regNumber[] expectedInMap = [
                regNumber.REG_STK, regNumber.REG_XMM2, regNumber.REG_RAX, regNumber.REG_STK,
            ];
            regNumber[] expectedOutMap = [
                regNumber.REG_K7, regNumber.REG_STK, regNumber.REG_STK, regNumber.REG_STK,
            ];
            Assert.That(inMap, Is.EqualTo(expectedInMap));
            Assert.That(outMap, Is.EqualTo(expectedOutMap));
            Assert.That(allocator.getVarReg(inMap, 2), Is.EqualTo(regNumber.REG_RAX));
            allocator.setVarReg(inMap, 0, regNumber.REG_NA);
            Assert.That(allocator.getVarReg(inMap, 0), Is.EqualTo(regNumber.REG_NA));
            Assert.That(allocator.getVarReg(outMap, 0), Is.EqualTo(regNumber.REG_K7));
        });
    }

    [TestCase(1u, 2u)]
    [TestCase(0u, 2u)]
    [TestCase(1u, 0u)]
    public static void SplitBlocksAliasTheOriginalEndpointMaps(uint fromBlock, uint toBlock)
    {
        WithCompiler(compiler => {
            compiler.lvaTrackedCount = 1;
            compiler.lvaTrackedFixed = true;
            compiler.fgBBNumMax = 2;
            var allocator = new LinearScan(compiler);
            MaxBlockBeforeResolution(allocator) = 2;
            allocator.initVarRegMaps();
            var fromMap = allocator.getOutVarToRegMap(1);
            var toMap = allocator.getInVarToRegMap(2);
            assert(fromMap is not null);
            assert(toMap is not null);
            allocator.setVarReg(fromMap, 0, regNumber.REG_RAX);
            allocator.setVarReg(toMap, 0, regNumber.REG_RBX);

            compiler.fgBBNumMax = 3;
            var splitMap = allocator.getSplitBBNumToTargetBBNumMap();
            splitMap.Add(3, new LinearScan.SplitEdgeInfo { fromBBNum = fromBlock, toBBNum = toBlock });
            Assert.That(allocator.getSplitBBNumToTargetBBNumMap(), Is.SameAs(splitMap));

            var inMap = allocator.getInVarToRegMap(3);
            var outMap = allocator.getOutVarToRegMap(3);
            Assert.That(inMap, Is.SameAs(fromBlock == 0 ? toMap : fromMap));
            Assert.That(outMap, Is.SameAs(toBlock == 0 ? fromMap : toMap));
            assert(inMap is not null);
            assert(outMap is not null);
            allocator.setVarReg(inMap, 0, regNumber.REG_RDX);
            Assert.That(allocator.getVarReg(fromBlock == 0 ? toMap : fromMap, 0),
                Is.EqualTo(regNumber.REG_RDX));
            allocator.setVarReg(outMap, 0, regNumber.REG_RSI);
            Assert.That(allocator.getVarReg(toBlock == 0 ? fromMap : toMap, 0),
                Is.EqualTo(regNumber.REG_RSI));
            Assert.That(InMaps(allocator), Has.Length.EqualTo(3));
            Assert.That(OutMaps(allocator), Has.Length.EqualTo(3));
        });
    }

    [TestCase(1, 4)]
    [TestCase(5, 8)]
    public static void MapCopyPreservesDestinationAliasesAndCopiesOnlyAllocatedEntries(int trackedCount, int mapCount)
    {
        WithCompiler(compiler => {
            compiler.lvaTrackedCount = trackedCount;
            compiler.lvaTrackedFixed = true;
            compiler.fgBBNumMax = 2;
            var allocator = new LinearScan(compiler);
            MaxBlockBeforeResolution(allocator) = 2;
            allocator.initVarRegMaps();
            var destination = allocator.getInVarToRegMap(2);
            assert(destination is not null);
            compiler.fgBBNumMax = 3;
            allocator.getSplitBBNumToTargetBBNumMap().Add(3,
                new LinearScan.SplitEdgeInfo { fromBBNum = 0, toBBNum = 2 });
            var alias = allocator.getInVarToRegMap(3);
            Assert.That(alias, Is.SameAs(destination));

            var source = new regNumber[mapCount + 1];
            Array.Fill(source, regNumber.REG_RAX);
            source[mapCount - 1] = regNumber.REG_K7;
            source[mapCount] = regNumber.REG_NA;
            var result = allocator.setInVarToRegMap(2, source);

            Assert.That(result, Is.SameAs(destination));
            Assert.That(allocator.getInVarToRegMap(3), Is.SameAs(alias));
            Assert.That(destination, Has.Length.EqualTo(mapCount));
            Assert.That(destination, Is.EqualTo(source.AsSpan(0, mapCount).ToArray()));
            source[0] = regNumber.REG_RDX;
            Assert.That(destination[0], Is.EqualTo(regNumber.REG_RAX));
            Assert.That(source[mapCount], Is.EqualTo(regNumber.REG_NA));

            result = allocator.setInVarToRegMap(2, alias);
            Assert.That(result, Is.SameAs(destination));
            Assert.That(destination[0], Is.EqualTo(regNumber.REG_RAX));
            Assert.That(destination[mapCount - 1], Is.EqualTo(regNumber.REG_K7));
        });
    }

    [Test]
    public static void EmptyMapCopyReturnsTheNullDestinationWithoutDereferencingEitherMap()
    {
        WithCompiler(compiler => {
            compiler.lvaTrackedFixed = true;
            compiler.fgBBNumMax = 1;
            var allocator = new LinearScan(compiler);
            MaxBlockBeforeResolution(allocator) = 1;
            allocator.initVarRegMaps();

            Assert.That(allocator.setInVarToRegMap(1, null), Is.Null);
            Assert.That(allocator.getInVarToRegMap(1), Is.Null);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regMapCount")]
    private static extern ref uint RegMapCount(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_bbNumMaxBeforeResolution")]
    private static extern ref uint MaxBlockBeforeResolution(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_inVarToRegMaps")]
    private static extern ref regNumber[]?[]? InMaps(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_outVarToRegMaps")]
    private static extern ref regNumber[]?[]? OutMaps(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_sharedCriticalVarToRegMap")]
    private static extern ref regNumber[]? ScratchMap(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.opts.compFlags = CLFLG_REGVAR;
        CompilerAllIntRegs(compiler) = SRBM_ALLINT_INIT;
        CompilerAllFloatRegs(compiler) = SRBM_ALLFLOAT_INIT;
        CompilerAllMaskRegs(compiler) = SRBM_ALLMASK_INIT;
        JitTls.Compiler = compiler;
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        codeGen.CopyRegisterInfo();
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
