// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using NUnit.Framework;
using static RyuJitSharp.CorJitResult;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class Arm64CodeGenSwitchTableTests
{
    [Test]
    public static void TableSwitchDispatchRecordsIndexedLoadThenFailsAtUnportedBlockAddress()
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            var index = compiler.gtNewIconNode(TYP_I_IMPL, 2);
            index.RegNum = REG_R1;
            var table = new GenTree(GT_JMPTABLE, TYP_I_IMPL)
            {
                RegNum = REG_R2,
            };
            var tree = new GenTreeOp(GT_SWITCH_TABLE, TYP_VOID, index, table);
            codeGen.InternalRegisters.Add(tree,
                regMaskTP.CreateFromRegNum(REG_R3, REG_R3.SingleTypeMask));
            compiler.fgFirstBB = new BasicBlock(null, null);

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree)) ??
                throw new AssertionException("The unported ARM64 block-address dependency did not fail.");

            Assert.That(failure.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure.Message, Is.EqualTo("ARM64 block-relative address recording is not ported."));

            var descriptors = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_ldr));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R2));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_R2));
            Assert.That(descriptors[0].idReg3(), Is.EqualTo(REG_R1));
            Assert.That(descriptors[0].idInsOpt(), Is.EqualTo(INS_OPTS_LSL));
            Assert.That(descriptors[0].idReg3Scaled(), Is.True);
        });
    }

    [Test]
    public static void JumpTableDispatchRetainsTheExplicitEmitterNyi()
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((_, codeGen) =>
        {
            var tree = new GenTree(GT_JMPTABLE, TYP_I_IMPL)
            {
                RegNum = REG_R3,
            };

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree)) ??
                throw new AssertionException("The unported jump-table emitter dependency did not fail.");

            Assert.That(failure.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure.Message,
                Is.EqualTo("Instruction recording outside AMD64 is not ported."));
            Assert.That(Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter), Is.Empty);
        });
    }
}
#endif
