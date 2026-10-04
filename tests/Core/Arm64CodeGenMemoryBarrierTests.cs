// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BarrierKind;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.insBarrier;
using static RyuJitSharp.instruction;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64CodeGenMemoryBarrierTests
{
    [TestCase(BARRIER_FULL, INS_BARRIER_ISH)]
    [TestCase(BARRIER_LOAD_ONLY, INS_BARRIER_ISHLD)]
    [TestCase(BARRIER_STORE_ONLY, INS_BARRIER_ISH)]
    public static void MemoryBarrierKindsUseTheExpectedDmbOptions(
        BarrierKind barrierKind, insBarrier expectedBarrier)
    {
        WithCodeGen(optimizationEnabled: false, (compiler, codeGen) =>
        {
            codeGen.instGen_MemoryBarrier(barrierKind);

            var descriptors = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_dmb));
            Assert.That(descriptors[0].idSmallCns(), Is.EqualTo((int)expectedBarrier));
        });
    }

    [Test]
    public static void MemoryBarrierNodesDispatchLoadAndStoreFlags()
    {
        WithCodeGen(optimizationEnabled: false, (compiler, codeGen) =>
        {
            var loadBarrier = compiler.gtNewMemoryBarrierNode(BARRIER_LOAD_ONLY);
            codeGen.genCodeForTreeNode(loadBarrier);

            var storeBarrier = compiler.gtNewMemoryBarrierNode(BARRIER_STORE_ONLY);
            codeGen.genCodeForTreeNode(storeBarrier);

            var bothBarriers = compiler.gtNewMemoryBarrierNode(BARRIER_STORE_ONLY);
            bothBarriers.Flags |= GTF_MEMORYBARRIER_LOAD;
            codeGen.genCodeForTreeNode(bothBarriers);

            var descriptors = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Has.Count.EqualTo(3));
            Assert.That(descriptors[0].idSmallCns(), Is.EqualTo((int)INS_BARRIER_ISHLD));
            Assert.That(descriptors[1].idSmallCns(), Is.EqualTo((int)INS_BARRIER_ISH));
            Assert.That(descriptors[2].idSmallCns(), Is.EqualTo((int)INS_BARRIER_ISHLD));
        });
    }

    [TestCase(BARRIER_LOAD_ONLY, BARRIER_FULL, INS_BARRIER_ISH)]
    [TestCase(BARRIER_FULL, BARRIER_LOAD_ONLY, INS_BARRIER_ISH)]
    [TestCase(BARRIER_LOAD_ONLY, BARRIER_LOAD_ONLY, INS_BARRIER_ISHLD)]
    [TestCase(BARRIER_STORE_ONLY, BARRIER_LOAD_ONLY, INS_BARRIER_ISH)]
    [TestCase(BARRIER_LOAD_ONLY, BARRIER_STORE_ONLY, INS_BARRIER_ISH)]
    public static void OptimizedAdjacentBarriersCoalesceAndUpgradeWhenNeeded(
        BarrierKind first, BarrierKind second, insBarrier expectedBarrier)
    {
        WithCodeGen(optimizationEnabled: true, (_, codeGen) =>
        {
            codeGen.instGen_MemoryBarrier(first);
            codeGen.instGen_MemoryBarrier(second);

            var descriptors = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_dmb));
            Assert.That(descriptors[0].idSmallCns(), Is.EqualTo((int)expectedBarrier));
        });
    }

    [Test]
    public static void UnoptimizedAdjacentBarriersAreNotCoalesced()
    {
        WithCodeGen(optimizationEnabled: false, (_, codeGen) =>
        {
            codeGen.instGen_MemoryBarrier(BARRIER_LOAD_ONLY);
            codeGen.instGen_MemoryBarrier(BARRIER_LOAD_ONLY);

            var descriptors = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            Assert.That(descriptors[0].idSmallCns(), Is.EqualTo((int)INS_BARRIER_ISHLD));
            Assert.That(descriptors[1].idSmallCns(), Is.EqualTo((int)INS_BARRIER_ISHLD));
        });
    }

    private static void WithCodeGen(bool optimizationEnabled, Action<Compiler, CodeGen> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(!optimizationEnabled);
        compiler.eeInfoInitialized = true;
        compiler.lvaCount = 1;
        compiler.lvaTable =
        [
            new LclVarDsc
            {
                Type = TYP_INT,
                lvOnFrame = true,
                lvFramePointerBased = true,
                StackOffset = -16,
            },
        ];
        compiler.lvaDoneFrameLayout = Compiler.REGALLOC_FRAME_LAYOUT;
        compiler.lvaOutgoingArgSpaceVar = BAD_VAR_NUM;
        compiler.lvaOutgoingArgSpaceSize.Value = 0;
        compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
        JitTls.Compiler = compiler;

        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            codeGen.RegSet.rsClearRegsModified();
            codeGen.Emitter.emitBegCG(compiler, default);
            codeGen.Emitter.Init();
            codeGen.Emitter.emitBegFN(false
#if DEBUG
                , true
#endif
                );
            codeGen.Emitter.emitCurIG = codeGen.Emitter.emitGetFirstPrologIG();
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);

            action(compiler, codeGen);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "treeLifeUpdater")]
    private static extern ref TreeLifeUpdater? LifeUpdater(CodeGen codeGen);
}
#endif
