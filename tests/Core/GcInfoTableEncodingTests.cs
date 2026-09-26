// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Collections;
using System.Reflection;
using NUnit.Framework;
using static RyuJitSharp.GCInfo.GCtype;
using static RyuJitSharp.GCInfo.rpdArgType_t;
using static RyuJitSharp.GcSlotFlags;
using static RyuJitSharp.GcStackSlotBase;
using static RyuJitSharp.Globals;
using static RyuJitSharp.EHHandlerType;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class GcInfoTableEncodingTests
{
    [Test]
    public static void HeaderPublishesCodeLengthFrameVarargsAndOutgoingArea()
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.lvaOutgoingArgSpaceSize.Value = 32;
            compiler.info.compIsVarArgs = true;
            ICorJitInfo jitInfo = default;
            CORINFO_METHOD_INFO method = default;
            var encoder = new GcInfoEncoder(&jitInfo, &method);

            codeGen.GCInfo.gcInfoBlockHdrSave(encoder, 20, 2);

            Assert.That(Member<uint>(encoder, "m_CodeLength"), Is.EqualTo(20u));
            Assert.That(Member<uint>(encoder, "m_StackBaseRegister"), Is.EqualTo((uint)REG_FPBASE));
            Assert.That(Member<bool>(encoder, "m_IsVarArg"), Is.True);
            Assert.That(Member<uint>(encoder, "m_SizeOfStackOutgoingAndScratchArea"), Is.EqualTo(32u));
        }, minopts: false);
    }

    [TestCase(0u, GC_SLOT_BASE)]
    [TestCase(1u, GC_SLOT_INTERIOR)]
    [TestCase(2u, GC_SLOT_PINNED)]
    [TestCase(3u, GC_SLOT_INTERIOR | GC_SLOT_PINNED)]
    public static void TrackedLifetimesRetainSignedOffsetsFlagsAndEndpoints(uint offsetFlags, GcSlotFlags expected)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, REG_RAX, (_, codeGen, _) =>
        {
            codeGen.GCInfo.gcVarPtrList = new GCInfo.varPtrDsc
            {
                vpdVarNum = unchecked((uint)-16) | offsetFlags,
                vpdBegOfs = 4,
                vpdEndOfs = 12,
            };
            ICorJitInfo jitInfo = default;
            CORINFO_METHOD_INFO method = default;
            var encoder = new GcInfoEncoder(&jitInfo, &method);
            var calls = 0u;

            codeGen.GCInfo.gcMakeRegPtrTable(encoder, 20, 2,
                GCInfo.MakeRegPtrMode.MAKE_REG_PTR_MODE_ASSIGN_SLOTS, ref calls);
            Assert.That(Slots(encoder).Count, Is.EqualTo(1));
            var slot = Slots(encoder)[0] ?? throw new AssertionException("Missing GC slot.");
            Assert.That(Member<int>(slot, "SpOffset"), Is.EqualTo(-16));
            Assert.That(Member<GcSlotFlags>(slot, "Flags"), Is.EqualTo(expected));
            Assert.That(Member<GcStackSlotBase>(slot, "Base"), Is.EqualTo(GC_FRAMEREG_REL));

            encoder.FinalizeSlotIds();
            codeGen.GCInfo.gcMakeRegPtrTable(encoder, 20, 2,
                GCInfo.MakeRegPtrMode.MAKE_REG_PTR_MODE_DO_WORK, ref calls);
            AssertTransition(encoder, 0, 4, 0, true);
            AssertTransition(encoder, 1, 12, 0, false);
        }, minopts: false);
    }

    [Test]
    public static void PinnedUntrackedByrefLocalHasNoLifetimeTransitions()
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_BYREF, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.lvaTable[0].lvTracked = false;
            compiler.lvaTable[0].lvPinned = true;
            ICorJitInfo jitInfo = default;
            CORINFO_METHOD_INFO method = default;
            var encoder = new GcInfoEncoder(&jitInfo, &method);
            var calls = 0u;

            codeGen.GCInfo.gcMakeRegPtrTable(encoder, 20, 2,
                GCInfo.MakeRegPtrMode.MAKE_REG_PTR_MODE_ASSIGN_SLOTS, ref calls);
            var slot = Slots(encoder)[0] ?? throw new AssertionException("Missing GC slot.");
            Assert.That(Member<GcSlotFlags>(slot, "Flags"),
                Is.EqualTo(GC_SLOT_UNTRACKED | GC_SLOT_INTERIOR | GC_SLOT_PINNED));

            encoder.FinalizeSlotIds();
            codeGen.GCInfo.gcMakeRegPtrTable(encoder, 20, 2,
                GCInfo.MakeRegPtrMode.MAKE_REG_PTR_MODE_DO_WORK, ref calls);
            Assert.That(Transitions(encoder).Count, Is.Zero);
        }, minopts: false);
    }

    [Test]
    public static void FilterPinsOnlyItsOverlappingTrackedLifetime()
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, REG_RAX, (compiler, codeGen, _) =>
        {
            var filterGroup = new insGroup { igOffs = 4 };
            var handlerGroup = new insGroup { igOffs = 8 };
#if DEBUG
            filterGroup.igSelf = filterGroup;
            handlerGroup.igSelf = handlerGroup;
#endif
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
                vpdVarNum = unchecked((uint)-16),
                vpdBegOfs = 2,
                vpdEndOfs = 12,
            };
            codeGen.GCInfo.gcVarPtrList = original;
            ICorJitInfo jitInfo = default;
            CORINFO_METHOD_INFO method = default;
            var encoder = new GcInfoEncoder(&jitInfo, &method);
            var calls = 0u;

            codeGen.GCInfo.gcMakeRegPtrTable(encoder, 16, 2,
                GCInfo.MakeRegPtrMode.MAKE_REG_PTR_MODE_ASSIGN_SLOTS, ref calls);
            Assert.That(Slots(encoder).Count, Is.EqualTo(2));
            var after = codeGen.GCInfo.gcVarPtrList
                ?? throw new AssertionException("Missing post-filter lifetime.");
            var pinned = after.vpdNext
                ?? throw new AssertionException("Missing pinned lifetime.");
            Assert.That((after.vpdBegOfs, after.vpdEndOfs), Is.EqualTo((8u, 12u)));
            Assert.That((pinned.vpdBegOfs, pinned.vpdEndOfs), Is.EqualTo((4u, 8u)));
            Assert.That(pinned.vpdVarNum & 2u, Is.EqualTo(2u));
            Assert.That((original.vpdBegOfs, original.vpdEndOfs), Is.EqualTo((2u, 4u)));
            Assert.That(original.vpdVarNum & 2u, Is.EqualTo(0u));

            encoder.FinalizeSlotIds();
            codeGen.GCInfo.gcMakeRegPtrTable(encoder, 16, 2,
                GCInfo.MakeRegPtrMode.MAKE_REG_PTR_MODE_DO_WORK, ref calls);
            Assert.That(Transitions(encoder).Count, Is.EqualTo(6));
            Assert.That(codeGen.GCInfo.gcVarPtrList, Is.SameAs(after));
        }, minopts: false);
    }

    [Test]
    public static void FramePointerCallSitesPublishInstructionStartsSizesAndRegisterLifetimes()
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, REG_RAX, (_, codeGen, _) =>
        {
            codeGen.GCInfo.gcCallDescAppend(13, 5, SRBM_RBX, SRBM_NONE, 0, 0, 0);
            ICorJitInfo jitInfo = default;
            CORINFO_METHOD_INFO method = default;
            var encoder = new GcInfoEncoder(&jitInfo, &method);
            var calls = 0u;

            codeGen.GCInfo.gcMakeRegPtrTable(encoder, 20, 2,
                GCInfo.MakeRegPtrMode.MAKE_REG_PTR_MODE_ASSIGN_SLOTS, ref calls);
            Assert.That(calls, Is.EqualTo(1u));
            encoder.FinalizeSlotIds();
            codeGen.GCInfo.gcMakeRegPtrTable(encoder, 20, 2,
                GCInfo.MakeRegPtrMode.MAKE_REG_PTR_MODE_DO_WORK, ref calls);

            var offsets = Member<uint[]>(encoder, "m_CallSites");
            var sizes = Member<byte[]>(encoder, "m_CallSiteSizes");
            Assert.That(offsets, Has.Length.EqualTo(1));
            Assert.That(sizes, Has.Length.EqualTo(1));
            Assert.That(offsets[0], Is.EqualTo(8u));
            Assert.That(sizes[0], Is.EqualTo((byte)5));
            Assert.That(Member<uint>(Slots(encoder)[0]!, "RegisterNumber"), Is.EqualTo((uint)REG_RBX));
            AssertTransition(encoder, 0, 8, 0, true);
            AssertTransition(encoder, 1, 13, 0, false);
        }, minopts: false);
    }

    [Test]
    public static void FullyInterruptibleOutgoingByrefDiesAtKillAndSkipsProlog()
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, REG_RAX, (_, codeGen, _) =>
        {
            codeGen.Interruptible = true;
            codeGen.IsFullPtrRegMapRequired = true;
            var outgoing = codeGen.GCInfo.gcRegPtrAllocDsc();
            outgoing.rpdArg = true;
            outgoing.rpdArgType = rpdARG_PUSH;
            outgoing.rpdGCtype = GCT_BYREF;
            outgoing.rpdCallData.rpdPtrArg = 8;
            outgoing.rpdOffs = 4;
            var kill = codeGen.GCInfo.gcRegPtrAllocDsc();
            kill.rpdArg = true;
            kill.rpdArgType = rpdARG_KILL;
            kill.rpdOffs = 10;

            ICorJitInfo jitInfo = default;
            CORINFO_METHOD_INFO method = default;
            var encoder = new GcInfoEncoder(&jitInfo, &method);
            var calls = 0u;
            codeGen.GCInfo.gcMakeRegPtrTable(encoder, 20, 2,
                GCInfo.MakeRegPtrMode.MAKE_REG_PTR_MODE_ASSIGN_SLOTS, ref calls);
            encoder.FinalizeSlotIds();
            codeGen.GCInfo.gcMakeRegPtrTable(encoder, 20, 2,
                GCInfo.MakeRegPtrMode.MAKE_REG_PTR_MODE_DO_WORK, ref calls);

            var slot = Slots(encoder)[0] ?? throw new AssertionException("Missing outgoing argument.");
            Assert.That(Member<int>(slot, "SpOffset"), Is.EqualTo(8));
            Assert.That(Member<GcSlotFlags>(slot, "Flags"), Is.EqualTo(GC_SLOT_INTERIOR));
            Assert.That(Member<GcStackSlotBase>(slot, "Base"), Is.EqualTo(GC_SP_REL));
            AssertTransition(encoder, 0, 4, 0, true);
            AssertTransition(encoder, 1, 10, 0, false);
            var range = Member<IList>(encoder, "m_InterruptibleRanges")[0]
                ?? throw new AssertionException("Missing interruptible range.");
            Assert.That(Member<uint>(range, "NormStartOffset"), Is.EqualTo(2u));
            Assert.That(Member<uint>(range, "NormStopOffset"), Is.EqualTo(20u));
        }, minopts: false);
    }

    private static IList Slots(GcInfoEncoder encoder) => Member<IList>(encoder, "m_SlotTable");

    private static IList Transitions(GcInfoEncoder encoder) => Member<IList>(encoder, "m_LifetimeTransitions");

    private static void AssertTransition(GcInfoEncoder encoder, int index, uint offset, uint slot, bool live)
    {
        var transition = Transitions(encoder)[index]
            ?? throw new AssertionException("Missing GC lifetime transition.");
        Assert.That(Member<uint>(transition, "CodeOffset"), Is.EqualTo(offset));
        Assert.That(Member<uint>(transition, "SlotId"), Is.EqualTo(slot));
        Assert.That(Member<bool>(transition, "BecomesLive"), Is.EqualTo(live));
    }

    private static T Member<T>(object instance, string name)
    {
        var type = name switch
        {
            "m_SlotTable" or "m_LifetimeTransitions" or "m_InterruptibleRanges" or
                "m_CallSites" or "m_CallSiteSizes" or "m_CodeLength" or "m_StackBaseRegister" or
                "m_IsVarArg" or "m_SizeOfStackOutgoingAndScratchArea" => typeof(GcInfoEncoder),
            "CodeOffset" or "SlotId" or "BecomesLive" =>
                typeof(GcInfoEncoder).GetNestedType("LifetimeTransition", BindingFlags.NonPublic),
            "NormStartOffset" or "NormStopOffset" =>
                typeof(GcInfoEncoder).GetNestedType("InterruptibleRange", BindingFlags.NonPublic),
            _ => typeof(GcInfoEncoder).GetNestedType("GcSlotDesc", BindingFlags.NonPublic),
        };
        var field = (type ?? throw new AssertionException($"Missing declaring type for {name}."))
            .GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new AssertionException($"Missing {name} field.");
        return (T)(field.GetValue(instance) ?? throw new AssertionException($"Missing {name} value."));
    }
}
