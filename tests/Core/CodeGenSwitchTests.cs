// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Linq;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;
using JumpView = RyuJitSharp.UnitTests.EmitterJumpInstructionTests.JumpView;
using JumpLists = RyuJitSharp.UnitTests.EmitterGroupBufferTests.DescriptorFactory;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenSwitchTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void TablesRetainCaseOrderDuplicatesAndNativeAlignment(bool relative)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var targets = PrepareSwitch(compiler);
            _ = codeGen.Emitter.emitDataConst(new byte[12], 4, TYP_INT);
            var prefix = codeGen.Emitter.emitConsDsc.dsdLast;
            var table = new GenTree(GT_JMPTABLE, TYP_I_IMPL) { RegNum = REG_RCX };

            var offset = codeGen.genEmitJumpTable(table, relative);

            var section = codeGen.Emitter.emitConsDsc.dsdLast ??
                throw new AssertionException("Missing jump table.");
            Assert.That(offset, Is.EqualTo(relative ? 12u : 16u));
            Assert.That(section.dsOffset, Is.EqualTo(offset));
            Assert.That(section.dsAlignment, Is.EqualTo(relative ? 4u : 8u));
            Assert.That(section.dsSize, Is.EqualTo(relative ? 12u : 24u));
            Assert.That(section.dsType, Is.EqualTo(relative
                ? Emitter.dataSection.sectionType.blockRelative32 : Emitter.dataSection.sectionType.blockAbsoluteAddr));
            Assert.That(section.Blocks, Is.EqualTo(targets));
            Assert.That(section.Blocks[0], Is.SameAs(section.Blocks[2]));
            Assert.That(prefix?.dsNext, Is.SameAs(section));
            Assert.That(codeGen.Emitter.emitConsDsc.dsdOffs, Is.EqualTo(offset + section.dsSize));
            Assert.That(Descriptors(codeGen), Is.Empty);
#if DEBUG
            Assert.That(codeGen.Emitter.emitDataSecCur, Is.Null);
#endif
        });
    }

    [Test]
    public static void TableAddressesUseTheExistingJitDataHandleAndProduceANonGcValue()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var targets = PrepareSwitch(compiler);
            _ = codeGen.Emitter.emitDataConst(new byte[4], 4, TYP_INT);
            var table = new GenTree(GT_JMPTABLE, TYP_I_IMPL) { RegNum = REG_RCX };
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RCX, TYP_BYREF);

            codeGen.genJumpTable(table);

            var section = codeGen.Emitter.emitConsDsc.dsdLast ??
                throw new AssertionException("Missing jump table.");
            Assert.That(section.Blocks, Is.EqualTo(targets));
            Assert.That(section.dsType, Is.EqualTo(Emitter.dataSection.sectionType.blockRelative32));
            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_lea));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_8BYTE));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_RCX));
            unsafe
            {
                Assert.That(descriptors[0].idAddr().iiaFieldHnd == Compiler.eeFindJitDataOffs(4), Is.True);
            }
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(RBM_NONE));
        });
    }

    [Test]
    public static void SwitchDispatchAddsTheFirstBlockAddressToAnUnsigned32BitOffset()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            _ = PrepareSwitch(compiler);
            var first = new BasicBlock(null, null);
            first.SetFlags(BBF_HAS_LABEL);
            compiler.fgFirstBB = first;
            var tree = new GenTreeOp(GT_SWITCH_TABLE, TYP_VOID,
                Register(compiler, TYP_I_IMPL, REG_RAX), Register(compiler, TYP_I_IMPL, REG_RCX));
            codeGen.InternalRegisters.Add(tree, RBM_R11);

            codeGen.genTableBasedSwitch(tree);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors.Select(static d => d.idIns()),
                Is.EqualTo((instruction[])[INS_mov, INS_lea, INS_add, INS_i_jmp]));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_RCX));
            var address = descriptors[0].idAddr().iiaAddrMode;
            Assert.That(address.amBaseReg, Is.EqualTo(REG_RCX));
            Assert.That(address.amIndxReg, Is.EqualTo(REG_RAX));
            Assert.That(address.amDisp, Is.Zero);
            Assert.That(JumpView.Target(descriptors[1]), Is.SameAs(first));
            Assert.That(descriptors[1].idReg1(), Is.EqualTo(REG_R11));
            Assert.That(descriptors[1].idIsDspReloc(), Is.True);
            Assert.That(descriptors[2].idOpSize(), Is.EqualTo(EA_8BYTE));
            Assert.That(descriptors[2].idReg1(), Is.EqualTo(REG_RCX));
            Assert.That(descriptors[2].idReg2(), Is.EqualTo(REG_R11));
            Assert.That(descriptors[3].idReg1(), Is.EqualTo(REG_RCX));
        });
    }

    [Test]
    public static void LabelAddressDescriptorsKeepLongReferencesRelocationsAndOrdering(
        [Values(false, true)] bool reloc, [Values(false, true)] bool catchReturn)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var target = new BasicBlock(null, null);
            target.SetFlags(BBF_HAS_LABEL);
            var current = compiler.compCurBB ?? throw new AssertionException("Missing current block.");
            if (catchReturn)
            {
                current.SetKindAndTargetEdge(BBJ_EHCATCHRET, new FlowEdge(current, target, null));
            }
            codeGen.Emitter.emitIns_J(INS_jmp, target);
            var previous = Descriptors(codeGen)[0];
            var offset = previous.idCodeSize();
            codeGen.Emitter.emitIns_R_L(INS_lea, EA_8BYTE | (reloc ? EA_DSP_RELOC_FLG : EA_UNKNOWN), target, REG_R11);

            var label = Descriptors(codeGen)[1];
            Assert.That(label.idInsFmt(), Is.EqualTo(Emitter.insFormat.IF_RWR_LABEL));
            Assert.That(label.idOpSize(), Is.EqualTo(EA_8BYTE));
            Assert.That(label.idCodeSize(), Is.EqualTo(reloc ? 7 : 8));
            Assert.That(label.idIsDspReloc(), Is.EqualTo(reloc));
            Assert.That(JumpView.Target(label), Is.SameAs(target));
            Assert.That(JumpView.Group(label), Is.SameAs(codeGen.Emitter.emitCurIG));
            Assert.That(JumpView.Offset(label), Is.EqualTo(offset));
            Assert.That(JumpView.IsShort(label), Is.False);
            Assert.That(JumpView.KeepLong(label), Is.True);
            Assert.That(JumpLists.PendingJump(codeGen.Emitter), Is.SameAs(label));
            Assert.That(JumpLists.NextJump(label), Is.SameAs(previous));
#if DEBUG
            var info = label.idDebugOnlyInfo() ?? throw new AssertionException("Missing instruction metadata.");
            Assert.That(info.idCatchRet, Is.EqualTo(catchReturn));
#endif
            label.idSetRelocFlags(EA_4BYTE | EA_CNS_RELOC_FLG);
            Assert.That(label.idIsCnsReloc(), Is.True);
            Assert.That(label.idIsDspReloc(), Is.False);
            label.idSetRelocFlags(EA_4BYTE);
            Assert.That(label.idIsCnsReloc(), Is.False);
        });
    }

#if DEBUG
    [Test]
    public static void DisassemblyRecordsJumpTablesAndSwitchDispatch()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var targets = PrepareSwitch(compiler);
            compiler.fgFirstBB = targets[0];
            var table = new GenTree(GT_JMPTABLE, TYP_I_IMPL) { RegNum = REG_RCX };
            var tree = new GenTreeOp(GT_SWITCH_TABLE, TYP_VOID,
                Register(compiler, TYP_I_IMPL, REG_RAX), Register(compiler, TYP_I_IMPL, REG_RCX));
            codeGen.InternalRegisters.Add(tree, RBM_R11);
            compiler.opts.dspCode = true;

            var diagnostic = InstructionRecordingTestSupport.Capture(() =>
            {
                codeGen.genJumpTable(table);
                codeGen.genTableBasedSwitch(tree);
                codeGen.Emitter.emitIns_R_L(INS_lea, EA_8BYTE, targets[0], REG_R11);
            });
            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(6));
            Assert.That(codeGen.Emitter.emitConsDsc.dsdLast, Is.Not.Null);
            Assert.That(diagnostic, Does.Contain("lea"));
        });
    }
#endif

    private static BasicBlock[] PrepareSwitch(Compiler compiler)
    {
        var current = compiler.compCurBB ?? throw new AssertionException("Missing current block.");
        current.SetKindAndTargetEdge(BBJ_SWITCH, null);
        var first = new BasicBlock(null, null);
        var second = new BasicBlock(null, null);
        first.SetFlags(BBF_HAS_LABEL);
        second.SetFlags(BBF_HAS_LABEL);
        var firstEdge = new FlowEdge(current, first, null);
        var secondEdge = new FlowEdge(current, second, null);
        current.SwitchTargets = new BBswtDesc([firstEdge, secondEdge], [0, 1, 0], hasDefault: false);
        current.SwitchTargets.Cases[0] = firstEdge;
        current.SwitchTargets.Cases[1] = secondEdge;
        current.SwitchTargets.Cases[2] = firstEdge;

        return [first, second, first];
    }
}
