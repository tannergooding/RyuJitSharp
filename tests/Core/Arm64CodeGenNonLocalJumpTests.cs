// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class Arm64CodeGenNonLocalJumpTests
{
    [Test]
    public static void NonLocalJumpDispatchEmitsAnIndirectBranchAndMarksTailCalls()
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            var target = compiler.gtNewIconNode(TYP_I_IMPL, 7);
            target.RegNum = REG_R3;
            var tree = new GenTreeUnOp(GT_NONLOCAL_JMP, TYP_VOID, target);

            codeGen.genCodeForTreeNode(tree);

            var descriptors = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_br));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_PTRSIZE));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R3));
            Assert.That(codeGen.HasTailCalls, Is.True);
        });
    }
}
#endif
