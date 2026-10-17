// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenXarchScalarClosureTests
{
    [TestCase(0, false)]
    [TestCase(0, true)]
    [TestCase(1, true)]
    public static void UnixDispatcherPreservesReusedConstantGcBoundary(int value, bool nonempty)
    {
        Assume.That(Configuration.UNIX_AMD64_ABI);
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var tree = compiler.gtNewIconNode(TYP_INT, value);
            tree.RegNum = REG_RAX;
            tree.IsReuseRegVal = true;
            if (nonempty)
            {
                codeGen.instGen(INS_nop);
            }

            var before = codeGen.Emitter.emitCurIG;
            codeGen.genCodeForTreeNode(tree);

            Assert.That(ReferenceEquals(before, codeGen.Emitter.emitCurIG), Is.EqualTo(value != 0 || !nonempty));
        });
    }

    [TestCase(0, false)]
    [TestCase(0, true)]
    [TestCase(1, true)]
    public static void X86ReusedConstantOnlyLabelsExistingInstructionsForZero(int value, bool nonempty)
    {
        Assume.That(Configuration.TARGET_X86);
        X86EmitterStaticOutputTests.WithEmitter((compiler, emitter) =>
        {
            var codeGen = compiler.codeGen as CodeGen
                ?? throw new AssertionException("Missing x86 code generator.");
            compiler.compCurBB = new BasicBlock(null, null) { bbNum = 1 };
            var tree = new GenTreeIntCon(TYP_INT, value) { RegNum = REG_EAX, IsReuseRegVal = true };
            if (nonempty)
            {
                codeGen.instGen(INS_nop);
            }

            var before = emitter.emitCurIG;
            codeGen.genCodeForReuseVal(tree);

            Assert.That(ReferenceEquals(before, emitter.emitCurIG), Is.EqualTo(value != 0 || !nonempty));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void X86CatchArgumentTransfersTheExceptionObjectRoot(bool same)
    {
        Assume.That(Configuration.TARGET_X86);
        X86EmitterStaticOutputTests.WithEmitter((compiler, emitter) =>
        {
            var codeGen = compiler.codeGen as CodeGen
                ?? throw new AssertionException("Missing x86 code generator.");
            compiler.compCurBB = new BasicBlock(null, null) { CatchType = bbCatchType.BBCT_FILTER };
            var exceptionMask = RBM_X86_EAX;
            codeGen.GCInfo.gcMarkRegSetGCref(exceptionMask);
            var tree = new GenTree(GT_CATCH_ARG, TYP_REF) { RegNum = same ? REG_X86_EAX : REG_X86_ECX };

            var before = emitter.emitCurIG;
            codeGen.genCodeForCatchArg(tree);

            Assert.That(emitter.emitCurIG, Is.SameAs(before));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(same ? exceptionMask : RBM_X86_ECX));
        });
    }
}
