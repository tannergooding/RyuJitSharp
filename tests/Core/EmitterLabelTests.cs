// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static class EmitterLabelTests
{
    [Test]
    public static void LabelsSaveThePreviousGroupAndOwnTheirGcSets()
    {
        WithEmitter((compiler, codeGen, emitter) =>
        {
            var previous = emitter.emitCurIG;
            assert(previous is not null);
            emitter.emitIns(INS_nop);
            var vars = VarSetOps.MakeSingleton(compiler, 0);

            var label = emitter.emitAddLabel(vars, new(SRBM_RAX), new(SRBM_RCX));

            Assert.That(label, Is.Not.SameAs(previous));
            Assert.That(previous.igNext, Is.SameAs(label));
            Assert.That(previous.igSize, Is.EqualTo(1));
            Assert.That(ThisRefs(emitter), Is.EqualTo(SRBM_RAX));
            Assert.That(ThisByrefs(emitter), Is.EqualTo(SRBM_RCX));
            Assert.That(InitRefs(emitter), Is.EqualTo(SRBM_RAX));
            Assert.That(InitByrefs(emitter), Is.EqualTo(SRBM_RCX));
            Assert.That(ThisVars(emitter), Is.Not.SameAs(vars));
            Assert.That(InitVars(emitter), Is.Not.SameAs(vars).And.Not.SameAs(ThisVars(emitter)));
            VarSetOps.RemoveElemD(compiler, vars, 0);
            Assert.That(VarSetOps.IsMember(compiler, ThisVars(emitter), 0), Is.True);
            Assert.That(VarSetOps.IsMember(compiler, InitVars(emitter), 0), Is.True);

            codeGen.GCInfo.gcRegGCrefSetCur = new(SRBM_RDX);
            var temporary = new BasicBlock(null, null);
            codeGen.genDefineTempLabel(temporary);
            Assert.That(temporary.bbEmitCookie, Is.SameAs(label));
            Assert.That(ThisRefs(emitter), Is.EqualTo(SRBM_RDX));
            emitter.emitSetFirstColdIGCookie(label);
            Assert.That(FirstColdGroup(emitter), Is.SameAs(label));
        });
    }

    [Test]
    public static void InlineLabelsExtendGroupsWithoutResettingEmitterGcState()
    {
        WithEmitter((compiler, codeGen, emitter) =>
        {
            var vars = VarSetOps.MakeSingleton(compiler, 0);
            var previous = emitter.emitAddLabel(vars, new(SRBM_RAX), new(SRBM_RCX));
            emitter.emitIns(INS_nop);
            codeGen.GCInfo.gcRegGCrefSetCur = new(SRBM_RDX);
            codeGen.GCInfo.gcRegByrefSetCur = default;
            var temporary = new BasicBlock(null, null);

            codeGen.genDefineInlineTempLabel(temporary);

            var label = temporary.bbEmitCookie;
            assert(label is not null);
            Assert.That(label, Is.Not.SameAs(previous));
            Assert.That(label.igFlags & InsGroupFlags.Extend, Is.EqualTo(InsGroupFlags.Extend));
            Assert.That(ThisRefs(emitter), Is.EqualTo(SRBM_RAX));
            Assert.That(ThisByrefs(emitter), Is.EqualTo(SRBM_RCX));
            Assert.That(InitRefs(emitter), Is.EqualTo(SRBM_RAX));
            Assert.That(VarSetOps.IsMember(compiler, ThisVars(emitter), 0), Is.True);
            Assert.That(emitter.emitAddInlineLabel(), Is.SameAs(label));
        });
    }

    [TestCase(BBJ_ALWAYS, 0, false, true, 0)]
    [TestCase(BBJ_ALWAYS, 1, false, true, 1)]
    [TestCase(BBJ_ALWAYS, 2, false, true, 1)]
    [TestCase(BBJ_ALWAYS, 3, false, true, 1)]
    [TestCase(BBJ_THROW, 1, false, true, 1)]
    [TestCase(BBJ_ALWAYS, 1, true, true, 0)]
    [TestCase(BBJ_ALWAYS, 1, false, false, 0)]
    public static void GcCapableReturnIpsAreSeparatedOnlyWhenLabeledLivenessChanges(
        BBKinds kind, int change, bool noGc, bool hasLabel, int padding)
    {
        WithEmitter((compiler, codeGen, emitter) =>
        {
            var block = compiler.compCurBB;
            assert(block is not null);
            if (hasLabel)
            {
                block.SetFlags(BBF_HAS_LABEL);
            }
            var previousBlock = new BasicBlock(null, null);
            if (kind == BBJ_ALWAYS)
            {
                previousBlock.SetKindAndTargetEdge(kind, compiler.fgAddRefPred(block, previousBlock));
            }
            else
            {
                previousBlock.Kind = kind;
            }
            var group = emitter.emitCurIG;
            assert(group is not null);
            var call = RecordCallDescriptor(emitter, noGc);
            var vars = change == 3 ? VarSetOps.MakeSingleton(compiler, 0) : VarSetOps.MakeEmpty(compiler);

            _ = emitter.emitAddLabel(vars, change == 1 ? new(SRBM_RAX) : default,
                change == 2 ? new(SRBM_RCX) : default, previousBlock);

            Assert.That(group.igSize, Is.EqualTo(5 + padding));
            Assert.That(group.igInsCnt, Is.EqualTo(1 + padding));
            assert(group.igData is not null);
            Assert.That(group.igData[0], Is.SameAs(call));
            Assert.That(call.idIsNoGC(), Is.EqualTo(noGc));
            if (padding != 0)
            {
                Assert.That(group.igData[^1].idIns(), Is.EqualTo(kind == BBJ_THROW ? INS_int3 : INS_nop));
                Assert.That(group.igData[^1].idCodeSize(), Is.EqualTo(1u));
            }
        });
    }

#if FEATURE_LOOP_ALIGN
    [Test]
    public static void CallPaddingInAnEmptyGroupMovesTheAlignmentPredecessor()
    {
        WithEmitter((compiler, codeGen, emitter) =>
        {
            var block = compiler.compCurBB;
            assert(block is not null);
            block.SetFlags(BBF_HAS_LABEL);
            var previousBlock = new BasicBlock(null, null) { Kind = BBJ_THROW };
            var previous = emitter.emitCurIG;
            assert(previous is not null);
            _ = RecordCallDescriptor(emitter, noGc: false);
            var emptyGroup = emitter.emitAddInlineLabel();
            Assert.That(emptyGroup, Is.Not.SameAs(previous));

            EmitterGroupBufferTests.DescriptorFactory.SetAlignmentLoopHead(emitter, previous);
            _ = emitter.emitAddLabel(VarSetOps.MakeEmpty(compiler), new(SRBM_RAX), default, previousBlock);

            Assert.That(EmitterGroupBufferTests.DescriptorFactory.AlignmentLoopHead(emitter), Is.SameAs(emptyGroup));
            Assert.That(emptyGroup.igSize, Is.EqualTo(1));
            Assert.That(emptyGroup.igLastIns?.idIns(), Is.EqualTo(INS_int3));
        });
    }

#endif

    private static Emitter.instrDesc RecordCallDescriptor(Emitter emitter, bool noGc)
    {
        var call = EmitterInstructionAllocationTests.Allocate(emitter, EA_PTRSIZE);
        call.idIns(INS_call);
        call.idInsFmt(IF_METHOD);
        call.idSetIsCall();
        call.idSetIsNoGC(noGc);
        call.idCodeSize(5);
        CurrentSize(emitter) += 5;

        return call;
    }

    private static void WithEmitter(Action<Compiler, CodeGen, Emitter> action)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_REF, REG_RAX,
            (compiler, codeGen, _) =>
            {
                compiler.fgPredsComputed = true;
#if DEBUG
                compiler.fgSafeFlowEdgeCreation = true;
#endif
                action(compiler, codeGen, codeGen.Emitter);
            });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int CurrentSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisGCrefVars")]
    private static extern ref nint[] ThisVars(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitInitGCrefVars")]
    private static extern ref nint[] InitVars(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisGCrefRegs")]
    private static extern ref regMask ThisRefs(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisByrefRegs")]
    private static extern ref regMask ThisByrefs(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitInitGCrefRegs")]
    private static extern ref regMask InitRefs(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitInitByrefRegs")]
    private static extern ref regMask InitByrefs(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitFirstColdIG")]
    private static extern ref insGroup? FirstColdGroup(Emitter emitter);
}
