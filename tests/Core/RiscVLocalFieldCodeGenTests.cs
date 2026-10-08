// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_RISCV64
using NUnit.Framework;
using static RyuJitSharp.CorJitResult;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class RiscVLocalFieldCodeGenTests
{
    [Test]
    public static void LocalFieldLoadUsesTheRiscVRecorder()
    {
        RiscVCodeGenPortTests.WithCodeGen((compiler, codeGen) =>
        {
            RiscVCodeGenPortTests.InitializeStackRecorderLocal(
                compiler, codeGen, framePointerBased: false, stackOffset: 0, TYP_LONG);
            var descriptors = RiscVCodeGenPortTests.CurrentInstructionBuffer(codeGen.Emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialCount = descriptors.Count;
            var initialSize = RiscVCodeGenPortTests.CurrentInstructionGroupSize(codeGen.Emitter);
            var tree = new GenTreeLclFld(GT_LCL_FLD, TYP_INT, 0, 12) { RegNum = REG_A0 };

            var failure = RiscVCodeGenPortTests.CaptureFatalJitException(
                () => codeGen.genCodeForLclFld(tree));

            Assert.That(failure, Is.Null);
            Assert.That(RiscVCodeGenPortTests.CurrentInstructionGroupSize(codeGen.Emitter),
                Is.EqualTo(initialSize + 4));
            Assert.That(descriptors.Count, Is.EqualTo(initialCount + 1));
            Assert.That(descriptors[^1].idIns(), Is.EqualTo(INS_lw));
            Assert.That(descriptors[^1].idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
            Assert.That(descriptors[^1].idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(12u));
        });
    }
}
#endif
