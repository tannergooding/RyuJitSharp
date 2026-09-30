// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if UNIX_AMD64_ABI || TARGET_X86
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenXarchNodeDispatchRestorationTests
{
#if UNIX_AMD64_ABI
    [TestCase(GT_NOP, false)]
    [TestCase(GT_NO_OP, true)]
    public static void UnixMarkersAndNopsReachTheirNativeDispatchCases(genTreeOps oper, bool emitsInstruction)
    {
        CodeGenBinaryTests.WithCodeGen((_, codeGen) =>
        {
            codeGen.genCodeForTreeNode(new GenTree(oper, TYP_VOID));

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(emitsInstruction ? 1 : 0));
            if (emitsInstruction)
            {
                Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_nop));
            }
        });
    }

    [Test]
    public static void UnixKeepAliveConsumesTheOperandWithoutEmittingAnInstruction()
    {
        CodeGenBinaryTests.WithCodeGen((_, codeGen) =>
        {
            var operand = new GenTreePhysReg(REG_RAX, TYP_REF) { RegNum = REG_RAX };
            codeGen.GCInfo.gcMarkRegSetGCref(RBM_RAX);

            codeGen.genCodeForTreeNode(new GenTreeUnOp(GT_KEEPALIVE, TYP_VOID, operand));

            Assert.That(Descriptors(codeGen), Is.Empty);
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(RBM_NONE));
        });
    }
#endif

#if TARGET_X86
    [TestCase(false)]
    [TestCase(true)]
    public static void X86ContainedAndMarkerNodesDoNotRequireAmd64Recording(bool contained)
    {
        X86EmitterStaticOutputTests.WithEmitter((compiler, emitter) =>
        {
            var codeGen = compiler.codeGen as CodeGen
                ?? throw new AssertionException("Missing x86 code generator.");
            var group = emitter.emitCurIG ?? throw new AssertionException("Missing instruction group.");
            var instructions = group.igData;
            var tree = new GenTree(GT_NOP, TYP_VOID) { IsContained = contained };

            codeGen.genCodeForTreeNode(tree);

            Assert.That(emitter.emitCurIG, Is.SameAs(group));
            Assert.That(group.igData, Is.SameAs(instructions));
        });
    }
#endif
}
#endif
