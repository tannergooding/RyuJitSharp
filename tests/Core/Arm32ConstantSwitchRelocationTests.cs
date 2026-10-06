// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm32ConstantSwitchRelocationTests
{
    [Test]
    public static void IntegerConstantsUseTheArm32ImmediateEmitter()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            var tree = compiler.gtNewIconNode(TYP_INT, 7);
            codeGen.genSetRegToConst(REG_R3, TYP_INT, tree);

            var descriptor = LastInstruction(codeGen.Emitter)
                ?? throw new AssertionException("No integer-constant instruction was recorded.");
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_mov));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R3));
            Assert.That(codeGen.Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)7));
        });
    }

    [Test]
    public static void FloatingConstantsMoveFromAnInternalIntegerRegister()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            var tree = compiler.gtNewDconNode(TYP_FLOAT, 1.25);
            codeGen.InternalRegisters.Add(tree, new regMaskTP(SRBM_R4));
            codeGen.genSetRegToConst(REG_F0, TYP_FLOAT, tree);

            var descriptor = LastInstruction(codeGen.Emitter)
                ?? throw new AssertionException("No floating-constant instruction was recorded.");
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_vmov_i2f));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_F0));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_R4));
        });
    }

    [Test]
    public static void DoubleConstantsMoveBothWordsFromInternalIntegerRegisters()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            var tree = compiler.gtNewDconNode(TYP_DOUBLE, 1.0);
            codeGen.InternalRegisters.Add(tree, new regMaskTP(SRBM_R4 | SRBM_R5));
            codeGen.genSetRegToConst(REG_F0, TYP_DOUBLE, tree);

            var descriptor = LastInstruction(codeGen.Emitter)
                ?? throw new AssertionException("No double-constant instruction was recorded.");
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_vmov_i2d));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_F0));
            Assert.That(new[] { REG_R4, REG_R5 }, Does.Contain(descriptor.idReg2()));
            Assert.That(new[] { REG_R4, REG_R5 }, Does.Contain(descriptor.idReg3()));
            Assert.That(descriptor.idReg2(), Is.Not.EqualTo(descriptor.idReg3()));
        });
    }

    [Test]
    public static void TableSwitchLoadsTheProgramCounterFromTheScaledIndex()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compCurBB = new BasicBlock(null, null);
            var tree = new GenTreeOp(GT_SWITCH_TABLE, TYP_VOID,
                Register(compiler, TYP_I_IMPL, REG_R0),
                Register(compiler, TYP_I_IMPL, REG_R1));
            codeGen.genTableBasedSwitch(tree);

            var descriptor = LastInstruction(codeGen.Emitter)
                ?? throw new AssertionException("No switch-dispatch instruction was recorded.");
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_ldr));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_PC));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_R1));
            Assert.That(descriptor.idReg3(), Is.EqualTo(REG_R0));
            Assert.That(codeGen.Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)2));
        });
    }

    [Test]
    public static void JumpTableEmitsAbsoluteTargetsAndLoadsItsDataLabel()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            var targets = PrepareSwitch(compiler);
            _ = codeGen.Emitter.emitDataConst(new byte[4], 4, TYP_INT);
            var tree = new GenTree(GT_JMPTABLE, TYP_I_IMPL) { RegNum = REG_R4 };
            codeGen.genJumpTable(tree);

            var section = codeGen.Emitter.emitConsDsc.dsdLast
                ?? throw new AssertionException("No jump-table data section was emitted.");
            Assert.That(section.dsType, Is.EqualTo(Emitter.dataSection.sectionType.blockAbsoluteAddr));
            Assert.That(section.Blocks, Is.EqualTo(targets));

            var descriptor = LastInstruction(codeGen.Emitter)
                ?? throw new AssertionException("No jump-table address instruction was recorded.");
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_movt));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R4));
            Assert.That(codeGen.Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)section.dsOffset));
        });
    }

    [TestCase(false, INS_movt)]
    [TestCase(true, INS_add)]
    public static void BlockDisplacementsIncludeTheOptionalRelativeCodeAdjustment(
        bool relativeCodeRelocs, instruction expectedLastInstruction)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compCurBB = new BasicBlock(null, null);
            var target = new BasicBlock(null, null);
            target.SetFlags(BBF_HAS_LABEL);
            if (relativeCodeRelocs)
            {
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_RELATIVE_CODE_RELOCS);
            }

            codeGen.genMov32RelocatableDisplacement(target, REG_R4);

            var descriptor = LastInstruction(codeGen.Emitter)
                ?? throw new AssertionException("No block-displacement instruction was recorded.");
            Assert.That(descriptor.idIns(), Is.EqualTo(expectedLastInstruction));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R4));
        });
    }

    private static BasicBlock[] PrepareSwitch(Compiler compiler)
    {
        var current = compiler.compCurBB = new BasicBlock(null, null);
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

    private static GenTreeIntCon Register(Compiler compiler, var_types type, regNumber reg)
    {
        var node = compiler.gtNewIconNode(type, 7);
        node.RegNum = reg;

        return node;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);
}
#endif
