// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenSwapTests
{
    [Test]
    public static void SwapsPreserveLocalOwnershipAndGcClasses(
        [Values(TYP_INT, TYP_LONG, TYP_REF, TYP_BYREF)] var_types firstType,
        [Values(TYP_INT, TYP_LONG, TYP_REF, TYP_BYREF)] var_types secondType)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var tree = PrepareSwap(compiler, firstType, secondType);
            var second = tree.Op2 ?? throw new AssertionException("Missing second swap operand.");
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RAX, firstType);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RCX, secondType);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RDX, TYP_REF);
            var live = compiler.compCurLife;

            codeGen.genCodeForSwap(tree);

            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_RCX));
            Assert.That(compiler.lvaTable[1].RegNum, Is.EqualTo(REG_RAX));
            Assert.That(tree.Op1.RegNum, Is.EqualTo(REG_RAX));
            Assert.That(second.RegNum, Is.EqualTo(REG_RCX));
            Assert.That(compiler.compCurLife, Is.EqualTo(live));
            var expectedRefs = RBM_RDX | (firstType == TYP_REF ? RBM_RCX : RBM_NONE)
                | (secondType == TYP_REF ? RBM_RAX : RBM_NONE);
            var expectedByrefs = (firstType == TYP_BYREF ? RBM_RCX : RBM_NONE)
                | (secondType == TYP_BYREF ? RBM_RAX : RBM_NONE);
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(expectedRefs));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(expectedByrefs));
            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_xchg));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_8BYTE));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_RAX));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_RCX));
            Assert.That(descriptors[0].idGCref(), Is.EqualTo(varTypeIsGC(firstType) != varTypeIsGC(secondType)
                ? GCInfo.GCtype.GCT_GCREF : GCInfo.GCtype.GCT_NONE));
#if DEBUG
            Assert.That(tree.Op1._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
                Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NONE));
            Assert.That(second._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
                Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NONE));
#endif
        });
    }

#if DEBUG
    [Test]
    public static void DisassemblyRecordsSwapsAndTransfersGcRoots()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var tree = PrepareSwap(compiler, TYP_REF, TYP_LONG);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RAX, TYP_REF);
            compiler.opts.dspCode = true;

            var diagnostic = InstructionRecordingTestSupport.Capture(() => codeGen.genCodeForSwap(tree));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(RBM_RCX));
            Assert.That(Descriptors(codeGen), Is.Not.Empty);
            Assert.That(diagnostic, Does.Contain("xchg"));
        });
    }
#endif

    private static GenTreeOp PrepareSwap(Compiler compiler, var_types firstType, var_types secondType)
    {
        compiler.lvaCount = 2;
        compiler.info.compLocalsCount = 2;
        compiler.lvaTrackedCount = 2;
        compiler.lvaTrackedToVarNum = [0, 1];
        compiler.lvaTable =
        [
            new() { Type = firstType, RegNum = REG_RAX, lvTracked = true, lvLRACandidate = true, _varIndex = 0 },
            new() { Type = secondType, RegNum = REG_RCX, lvTracked = true, lvLRACandidate = true, _varIndex = 1 },
        ];
        var first = new GenTreeLclVar(firstType, 0) { RegNum = REG_RAX };
        var second = new GenTreeLclVar(secondType, 1) { RegNum = REG_RCX };

        return new GenTreeOp(GT_SWAP, TYP_VOID, first, second);
    }
}
