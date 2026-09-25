// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.instruction;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static class EmitterGCInterruptibilityTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void NestedRequestsSplitOnlyAtTheOuterBoundaryAndDeferTheFollowingGroup(bool nonempty)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            if (nonempty)
            {
                emitter.emitIns(INS_nop);
            }
            var initial = emitter.emitCurIG ?? throw new AssertionException("Missing initial group.");
            var refs = codeGen.GCInfo.gcRegGCrefSetCur;
            var byrefs = codeGen.GCInfo.gcRegByrefSetCur;

            emitter.emitDisableGC();

            var region = emitter.emitCurIG ?? throw new AssertionException("Missing NoGC group.");
            Assert.That(ReferenceEquals(region, initial), Is.EqualTo(!nonempty));
            Assert.That(region.igFlags & InsGroupFlags.NoGCInterrupt, Is.EqualTo(InsGroupFlags.NoGCInterrupt));
            Assert.That(RequestCount(emitter), Is.EqualTo(1));
            Assert.That(NoGcGroup(emitter), Is.True);
            if (nonempty)
            {
                Assert.That(initial.igData?.Select(id => id.idIns()), Is.EqualTo([INS_nop]));
                Assert.That(initial.igInsCnt, Is.EqualTo(1));
                Assert.That(region.igOffs, Is.EqualTo(initial.igOffs + initial.igSize));
                Assert.That(region.igFlags & InsGroupFlags.Extend, Is.EqualTo(InsGroupFlags.Extend));
            }

            emitter.emitDisableGC();
            emitter.emitIns(INS_nop);
            emitter.emitEnableGC();

            Assert.That(emitter.emitCurIG, Is.SameAs(region));
            Assert.That(RequestCount(emitter), Is.EqualTo(1));
            Assert.That(NoGcGroup(emitter), Is.True);
            Assert.That(ForceNewGroup(emitter), Is.False);
            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(1));

            emitter.emitEnableGC();

            Assert.That(RequestCount(emitter), Is.Zero);
            Assert.That(NoGcGroup(emitter), Is.False);
            Assert.That(ForceNewGroup(emitter), Is.True);
            Assert.That(emitter.emitCurIG, Is.SameAs(region));
            Assert.That(region.igNext, Is.Null);
            Assert.That(emitter.emitLastCodeIsNoGC(), Is.True);

            emitter.emitIns(INS_nop);

            var after = emitter.emitCurIG ?? throw new AssertionException("Missing resumed group.");
            Assert.That(after, Is.Not.SameAs(region));
            Assert.That(after.igFlags & InsGroupFlags.NoGCInterrupt, Is.EqualTo(InsGroupFlags.None));
            Assert.That(after.igFlags & InsGroupFlags.Extend, Is.EqualTo(InsGroupFlags.Extend));
            Assert.That(region.igInsCnt, Is.EqualTo(1));
            Assert.That(after.igOffs, Is.EqualTo(region.igOffs + region.igSize));
            Assert.That(emitter.emitLastCodeIsNoGC(), Is.False);
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(refs));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(byrefs));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void EmptyCurrentGroupUsesTheLastSavedNonemptyGroupsInterruptibility(bool noGc)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            if (noGc)
            {
                emitter.emitDisableGC();
            }
            emitter.emitIns(INS_nop);
            if (noGc)
            {
                emitter.emitEnableGC();
            }

            var label = emitter.emitAddLabel(codeGen.GCInfo.gcVarPtrSetCur,
                codeGen.GCInfo.gcRegGCrefSetCur, codeGen.GCInfo.gcRegByrefSetCur);

            Assert.That(Descriptors(codeGen), Is.Empty);
            Assert.That(label.igFlags & InsGroupFlags.NoGCInterrupt, Is.EqualTo(InsGroupFlags.None));
            Assert.That(emitter.emitLastCodeIsNoGC(), Is.EqualTo(noGc));

            emitter.emitIns(INS_nop);

            Assert.That(emitter.emitLastCodeIsNoGC(), Is.False);
            Assert.That(ForceNewGroup(emitter), Is.False);
        });
    }

    [TestCase(true, IPmappingDscKind.Normal, true, true, true)]
    [TestCase(false, IPmappingDscKind.Normal, true, true, false)]
    [TestCase(true, IPmappingDscKind.Normal, false, true, false)]
    [TestCase(true, IPmappingDscKind.Normal, true, false, false)]
    [TestCase(true, IPmappingDscKind.NoMapping, true, true, false)]
    public static void AdjacentDebugRegionsInsertOnlyTheNativeStackEmptyBoundaryNop(
        bool debugCode, IPmappingDscKind kind, bool stackEmpty, bool current, bool expectedNop)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            compiler.genIPmappings = [];
            var emitter = codeGen.Emitter;
            emitter.emitDisableGC();
            var oldRegion = emitter.emitCurIG ?? throw new AssertionException("Missing old region.");
            var staleLocation = new emitLocation(emitter);
            emitter.emitIns(INS_nop);
            emitter.emitEnableGC();
            compiler.opts.compDbgCode = debugCode;
            var mapping = new IPmappingDsc
            {
                ipmdKind = kind,
                ipmdLoc = new ILLocation(0, stackEmpty ? ICorDebugInfo.STACK_EMPTY : 0),
                ipmdNativeLoc = current ? new emitLocation(emitter) : staleLocation,
            };
            _ = compiler.genIPmappings.AddLast(mapping);

            emitter.emitDisableGC();

            var next = oldRegion.igNext ?? throw new AssertionException("Missing following group.");
            if (expectedNop)
            {
                Assert.That(next.igFlags & InsGroupFlags.NoGCInterrupt, Is.EqualTo(InsGroupFlags.None));
                Assert.That(next.igData?.Select(id => id.idIns()), Is.EqualTo([INS_nop]));
                Assert.That(next.igInsCnt, Is.EqualTo(1));
                Assert.That(next.igNext, Is.SameAs(emitter.emitCurIG));
            }
            else
            {
                Assert.That(next, Is.SameAs(emitter.emitCurIG));
            }
            var newRegion = emitter.emitCurIG ?? throw new AssertionException("Missing new region.");
            Assert.That(newRegion.igFlags & InsGroupFlags.NoGCInterrupt, Is.EqualTo(InsGroupFlags.NoGCInterrupt));
            Assert.That(Descriptors(codeGen), Is.Empty);
            Assert.That(RequestCount(emitter), Is.EqualTo(1));
            Assert.That(NoGcGroup(emitter), Is.True);
            Assert.That(compiler.genIPmappings, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public static void DebugNoGcRequestsWithoutMappingsOrWithinANestedRegionDoNotInsertNops()
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            compiler.genIPmappings = [];
            compiler.opts.compDbgCode = true;
            var emitter = codeGen.Emitter;
            emitter.emitDisableGC();
            emitter.emitIns(INS_nop);
            _ = compiler.genIPmappings.AddLast(new IPmappingDsc
            {
                ipmdKind = IPmappingDscKind.Normal,
                ipmdLoc = new ILLocation(0, ICorDebugInfo.STACK_EMPTY),
                ipmdNativeLoc = new emitLocation(emitter),
            });
            var group = emitter.emitCurIG;

            emitter.emitDisableGC();

            Assert.That(emitter.emitCurIG, Is.SameAs(group));
            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(1));
            Assert.That(RequestCount(emitter), Is.EqualTo(2));
        });
    }

#if DEBUG
    [TestCase(false)]
    [TestCase(true)]
    public static void D005RejectsBothTransitionsBeforeChangingRequestsFlagsOrInstructions(bool enable)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            if (enable)
            {
                emitter.emitDisableGC();
            }
            emitter.emitIns(INS_nop);
            var group = emitter.emitCurIG ?? throw new AssertionException("Missing group.");
            var flags = group.igFlags;
            var forceNewGroup = ForceNewGroup(emitter);
            compiler.opts.dspCode = true;

            var exception = Assert.Throws<FatalJitException>(() =>
            {
                if (enable)
                {
                    emitter.emitEnableGC();
                }
                else
                {
                    emitter.emitDisableGC();
                }
            });

            Assert.That(exception, Has.Property(nameof(FatalJitException.Result)).EqualTo(CorJitResult.CORJIT_SKIPPED));
            Assert.That(RequestCount(emitter), Is.EqualTo(enable ? 1 : 0));
            Assert.That(NoGcGroup(emitter), Is.EqualTo(enable));
            Assert.That(ForceNewGroup(emitter), Is.EqualTo(forceNewGroup));
            Assert.That(emitter.emitCurIG, Is.SameAs(group));
            Assert.That(group.igFlags, Is.EqualTo(flags));
            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(1));
        });
    }
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitNoGCRequestCount")]
    private static extern ref int RequestCount(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitNoGCIG")]
    private static extern ref bool NoGcGroup(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitForceNewIG")]
    private static extern ref bool ForceNewGroup(Emitter emitter);
}
