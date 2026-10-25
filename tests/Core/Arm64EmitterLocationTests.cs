// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;

namespace RyuJitSharp.UnitTests;

internal static unsafe class Arm64EmitterLocationTests
{
    [SetUp]
    public static void RequireArm64Target()
    {
        Assume.That(Configuration.TARGET_ARM64, "This fixture requires ARM64 target semantics.");
    }

    [TestCase(false, 108u)]
    [TestCase(true, 112u)]
    public static void FinalOffsetsRespectPredictedAndChangedInstructionSizes(bool changed, uint expected)
    {
        var (_, emitter) = CreateEmitter();
        var first = LocationEmitter.Basic(IF_ARM64_LARGEJMP);
        var second = LocationEmitter.Basic(IF_ARM64_SN_0A);
        var third = LocationEmitter.Basic(IF_ARM64_SN_0A);
        var group = Group(first, second, third);
        group.igOffs = 100;
        group.igSize = changed ? (ushort)16 : (ushort)12;
        if (changed)
        {
            group.igFlags |= InsGroupFlags.UpdatedInstructionSize;
        }
        else
        {
            first.idInsFmt(IF_ARM64_SN_0A);
        }

        Assert.That(new emitLocation(group, Emitter.emitSpecifiedOffset(2, 8)).CodeOffset(emitter), Is.EqualTo(expected));
        Assert.That(new emitLocation(group, Emitter.emitSpecifiedOffset(3, 99)).CodeOffset(emitter),
            Is.EqualTo(changed ? 116u : 112u));
    }

    [Test]
    public static void ReplacedInstructionsCanMoveIntoTheNextGroup()
    {
        var (_, emitter) = CreateEmitter();
        var group = Group(LocationEmitter.Basic(IF_ARM64_SN_0A));
        var replacement = Group(LocationEmitter.Basic(IF_ARM64_LARGEJMP));
        group.igFlags |= InsGroupFlags.HasRemovedInstruction;
        group.igNext = replacement;
        replacement.igOffs = uint.MaxValue - 3;

        Assert.That(new emitLocation(group, Emitter.emitSpecifiedOffset(2, 99)).CodeOffset(emitter), Is.EqualTo(4u));
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void WalkingSkipsEmptyGroupsAndHonorsCapturedEndLocations(bool afterFirst)
    {
        var (compiler, emitter) = CreateEmitter();
        var currentDescriptor = emitter.Allocate();
        var savedDescriptor = LocationEmitter.Basic(IF_ARM64_SN_0A);
        var first = Group(savedDescriptor);
        var empty = Group();
        first.igNext = empty;
        empty.igNext = emitter.emitCurIG;
        List<Emitter.instrDesc> visited = [];
        var location = new emitLocation(first, Emitter.emitSpecifiedOffset(afterFirst ? 1u : 0u, 0));

        var visitor = new CodeGenTestSupport.EmitterDescriptorRecorder(visited, compiler);
        emitter.emitWalkIDs(location, ref visitor, compiler);

        Emitter.instrDesc[] expected = afterFirst ? [currentDescriptor] : [savedDescriptor, currentDescriptor];
        Assert.That(visited, Is.EqualTo(expected));
    }

    [Test]
    public static void WalkingWithinAGroupPreservesDescriptorOrder()
    {
        var (compiler, emitter) = CreateEmitter();
        var first = emitter.Allocate();
        var second = emitter.Allocate();
        List<Emitter.instrDesc> visited = [];
        var visitor = new CodeGenTestSupport.EmitterDescriptorRecorder(visited);
        emitter.emitWalkIDs(new emitLocation(emitter.emitCurIG), ref visitor, compiler);

        Emitter.instrDesc[] expected = [first, second];
        Assert.That(visited, Is.EqualTo(expected));
    }

    [Test]
    public static void NonemptyPaddingRecordsTheArm64UnwindNop()
    {
        var (compiler, emitter) = CreateEmitter();
        var unwindInfo = new UnwindInfo();
        compiler.compFuncInfos = [new FuncInfoDsc { funKind = FuncKind.FUNC_ROOT, uwi = unwindInfo }];
        compiler.compFuncInfoCount = 1;
        compiler.fgFuncletsCreated = true;
        unwindInfo.InitUnwindInfo(compiler, new emitLocation(emitter), null);
        unwindInfo.CaptureLocation();

        var initialLocation = unwindInfo.GetCurrentEmitterLocation()
            ?? throw new AssertionException("Missing initial unwind location.");
        UnwindPadding(emitter, initialLocation, compiler);
        Assert.That(unwindInfo.GetCurrentEmitterLocation()?.CodeOffset(emitter), Is.Zero);

        _ = emitter.Allocate();
        var instructionGroup = emitter.emitCurIG
            ?? throw new AssertionException("Missing current instruction group.");
        instructionGroup.igInsCnt = 1;
        instructionGroup.igSize = 4;
        UnwindPadding(emitter, new emitLocation(instructionGroup), compiler);

        Assert.That(unwindInfo.GetCurrentEmitterLocation()?.CodeOffset(emitter), Is.EqualTo(4u));
    }

    [Test]
    public static void CurrentPrologOffsetsUseTheSharedUnsignedAccumulator()
    {
        var (_, emitter) = CreateEmitter();
        var first = FirstGroup(emitter) ?? throw new AssertionException("Missing prolog group.");
        var current = emitter.emitCurIG ?? throw new AssertionException("Missing current group.");
        current.igFlags |= InsGroupFlags.Prolog;
        first.igSize = 8;
        CurrentSize(emitter) = -4;

        Assert.That(emitter.emitGetCurrentCodeOffsetFrom(null), Is.EqualTo(4u));
    }

    [TestCase(false)]
    [TestCase(true)]
    [NonParallelizable]
    public static void CorruptLocationsRespectNowayRecoveryPolicy(bool minOpts)
    {
        using var tls = TestConfiguration.CreateJitTls();
        var previousCompiler = JitTls.Compiler;
        var previousConfig = Globals.JitConfig;
        var (compiler, emitter) = CreateEmitter();
        compiler.opts.compMinOpts = minOpts;
        compiler.opts.compMinOptsIsSet = TestConfiguration.IsDebug;
        JitTls.Compiler = compiler;
        Globals.JitConfig = new JitConfigValues();

        try
        {
            var location = new emitLocation(Group());
            var visitor = new CodeGenTestSupport.EmitterDescriptorRecorder(failIfVisited: true);
            if (minOpts)
            {
                Assert.DoesNotThrow(() => emitter.emitWalkIDs(location, ref visitor, compiler));
            }
            else
            {
                var error = Assert.Throws<FatalJitException>(() =>
                    emitter.emitWalkIDs(location, ref visitor, compiler));
                Assert.That(error, Has.Property(nameof(FatalJitException.Result))
                    .EqualTo(CorJitResult.CORJIT_RECOVERABLEERROR));
            }
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
            Globals.JitConfig = previousConfig;
        }
    }

    private static insGroup Group(params Emitter.instrDesc[] descriptors)
    {
        var group = new insGroup { igData = descriptors, igInsCnt = (byte)descriptors.Length };
        if (TestConfiguration.IsDebug)
        {
            group.igSelf = group;
        }

        return group;
    }

    private static (Compiler Compiler, LocationEmitter Emitter) CreateEmitter()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        var emitter = new LocationEmitter(codeGen);
        CodeGenEmitter(codeGen) = emitter;
        emitter.emitBegCG(compiler, default);
        emitter.Init();
        emitter.emitBegFN(false, TestConfiguration.IsDebug);
        return (compiler, emitter);
    }

    private sealed class LocationEmitter(CodeGen codeGen) : Emitter(codeGen)
    {
        public static instrDesc Basic(insFormat format)
        {
            var descriptor = new instrDescBasic();
            descriptor.idIns(INS_nop);
            descriptor.idInsFmt(format);
            return descriptor;
        }

        public instrDesc Allocate()
        {
            var descriptor = AllocateSmall(this, EA_8BYTE);
            descriptor.idIns(INS_nop);
            descriptor.idInsFmt(IF_ARM64_SN_0A);
            CurrentSize(this) += 4;
            return descriptor;
        }

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNewInstrSmall")]
        private static extern instrDescBasic AllocateSmall(Emitter emitter, emitAttr attr);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_cgEmitter")]
    private static extern ref Emitter CodeGenEmitter(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitUnwindNopPadding")]
    private static extern void UnwindPadding(Emitter emitter, emitLocation location, Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int CurrentSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitIGlist")]
    private static extern ref insGroup? FirstGroup(Emitter emitter);
}
