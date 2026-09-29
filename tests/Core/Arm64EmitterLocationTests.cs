// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
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
    [TestCase(false, 108u)]
    [TestCase(true, 112u)]
    public static void FinalOffsetsRespectPredictedAndChangedInstructionSizes(bool changed, uint expected)
    {
        var (_, emitter) = CreateEmitter();
        var first = LocationEmitter.Basic(IF_LARGEJMP);
        var second = LocationEmitter.Basic(IF_SN_0A);
        var third = LocationEmitter.Basic(IF_SN_0A);
        var group = Group(first, second, third);
        group.igOffs = 100;
        group.igSize = changed ? (ushort)16 : (ushort)12;
        if (changed)
        {
            group.igFlags |= InsGroupFlags.UpdatedInstructionSize;
        }
        else
        {
            first.idInsFmt(IF_SN_0A);
        }

        Assert.That(new emitLocation(group, Emitter.emitSpecifiedOffset(2, 8)).CodeOffset(emitter), Is.EqualTo(expected));
        Assert.That(new emitLocation(group, Emitter.emitSpecifiedOffset(3, 99)).CodeOffset(emitter),
            Is.EqualTo(changed ? 116u : 112u));
    }

    [Test]
    public static void ReplacedInstructionsCanMoveIntoTheNextGroup()
    {
        var (_, emitter) = CreateEmitter();
        var group = Group(LocationEmitter.Basic(IF_SN_0A));
        var replacement = Group(LocationEmitter.Basic(IF_LARGEJMP));
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
        var savedDescriptor = LocationEmitter.Basic(IF_SN_0A);
        var first = Group(savedDescriptor);
        var empty = Group();
        first.igNext = empty;
        empty.igNext = emitter.emitCurIG;
        List<Emitter.instrDesc> visited = [];
        var location = new emitLocation(first, Emitter.emitSpecifiedOffset(afterFirst ? 1u : 0u, 0));

        Walk(emitter, location, (descriptor, context) =>
        {
            Assert.That(context, Is.SameAs(compiler));
            visited.Add(descriptor);
        }, compiler);

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
        Walk(emitter, new emitLocation(emitter.emitCurIG), (descriptor, _) => visited.Add(descriptor), compiler);

        Emitter.instrDesc[] expected = [first, second];
        Assert.That(visited, Is.EqualTo(expected));
    }

    [Test]
    public static void EmptyPaddingDoesNotInvokeTheUnportedEncoder()
    {
        var (compiler, emitter) = CreateEmitter();
        UnwindPadding(emitter, new emitLocation(emitter), compiler);
        _ = emitter.Allocate();

        var error = Assert.Throws<FatalJitException>(() =>
            UnwindPadding(emitter, new emitLocation(emitter.emitCurIG), compiler));
        Assert.That(error, Has.Message.EqualTo("Unwind NOP encoding for this target is not ported."));
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
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previousCompiler = JitTls.Compiler;
        var previousConfig = Globals.JitConfig;
        var (compiler, emitter) = CreateEmitter();
        compiler.opts.compMinOpts = minOpts;
#if DEBUG
        compiler.opts.compMinOptsIsSet = true;
#endif
        JitTls.Compiler = compiler;
        Globals.JitConfig = new JitConfigValues();

        try
        {
            var location = new emitLocation(Group());
            if (minOpts)
            {
                Assert.DoesNotThrow(() => Walk(emitter, location,
                    (_, _) => Assert.Fail("A corrupt end location must not visit a descriptor."), compiler));
            }
            else
            {
                var error = Assert.Throws<FatalJitException>(() => Walk(emitter, location, (_, _) => { }, compiler));
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
#if DEBUG
        group.igSelf = group;
#endif
        return group;
    }

    private static (Compiler Compiler, LocationEmitter Emitter) CreateEmitter()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        var emitter = new LocationEmitter(new CodeGen(compiler));
        emitter.emitBegCG(compiler, default);
        emitter.Init();
        emitter.emitBegFN(false
#if DEBUG
            , true
#endif
            );
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
            descriptor.idInsFmt(IF_SN_0A);
            CurrentSize(this) += 4;
            return descriptor;
        }

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNewInstrSmall")]
        private static extern instrDescBasic AllocateSmall(Emitter emitter, emitAttr attr);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitWalkIDs")]
    private static extern void Walk(Emitter emitter, emitLocation location,
        Action<Emitter.instrDesc, Compiler> process, Compiler context);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitUnwindNopPadding")]
    private static extern void UnwindPadding(Emitter emitter, emitLocation location, Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int CurrentSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitIGlist")]
    private static extern ref insGroup? FirstGroup(Emitter emitter);
}
#endif
