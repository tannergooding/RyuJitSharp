// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Linq;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenScalarNodeTests
{
    [TestCase(TYP_INT, false)]
    [TestCase(TYP_INT, true)]
    [TestCase(TYP_LONG, false)]
    [TestCase(TYP_LONG, true)]
    public static void SaturatingIncrementRetainsCarryCorrection(var_types type, bool same)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var tree = new GenTreeUnOp(GT_INC_SATURATE, type, Register(compiler, type, REG_RAX))
            {
                RegNum = same ? REG_RAX : REG_R8,
            };
            codeGen.genCodeForIncSaturate(tree);
            var ids = Descriptors(codeGen);

            Assert.That(ids.Select(id => id.idIns()),
                Is.EqualTo(same ? (instruction[])[INS_add, INS_sbb] : [INS_mov, INS_add, INS_sbb]));
            Assert.That(ids[^2].idOpSize(), Is.EqualTo(type == TYP_INT ? EA_4BYTE : EA_8BYTE));
            Assert.That(ids[^1].idOpSize(), Is.EqualTo(ids[^2].idOpSize()));
            Assert.That(InstructionConstant(codeGen.Emitter, ids[^2]), Is.EqualTo((nint)1));
            Assert.That(InstructionConstant(codeGen.Emitter, ids[^1]), Is.EqualTo((nint)0));
        });
    }

    [TestCase(GT_BIT_SET, TYP_INT, false, INS_bts)]
    [TestCase(GT_BIT_CLEAR, TYP_INT, false, INS_btr)]
    [TestCase(GT_BIT_INVERT, TYP_LONG, false, INS_btc)]
    [TestCase(GT_BIT_SET, TYP_LONG, true, INS_bts)]
    [TestCase(GT_BIT_CLEAR, TYP_LONG, true, INS_btr)]
    [TestCase(GT_BIT_INVERT, TYP_INT, true, INS_btc)]
    public static void BitModificationCopiesTheValueBeforeReadingTheIndex(
        genTreeOps oper, var_types type, bool sharedValue, instruction expected)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var source = Register(compiler, type, REG_RAX);
            var index = Register(compiler, TYP_INT, sharedValue ? REG_RAX : REG_RCX);
            var tree = new GenTreeOp(oper, type, source, index) { RegNum = sharedValue ? REG_RAX : REG_R8 };
            codeGen.genCodeForBitOp(tree);
            var ids = Descriptors(codeGen);

            Assert.That(ids.Select(id => id.idIns()),
                Is.EqualTo(sharedValue ? (instruction[])[expected] : [INS_mov, expected]));
            Assert.That(ids[^1].idReg1(), Is.EqualTo(tree.RegNum));
            Assert.That(ids[^1].idReg2(), Is.EqualTo(index.RegNum));
            Assert.That(ids[^1].idOpSize(), Is.EqualTo(type == TYP_INT ? EA_4BYTE : EA_8BYTE));
        });
    }

    [TestCase(TYP_REF, false)]
    [TestCase(TYP_BYREF, false)]
    [TestCase(TYP_I_IMPL, false)]
    [TestCase(TYP_REF, true)]
    public static void PhysicalRegisterReadsPreserveSourceAndDestinationGcState(var_types type, bool same)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var tree = new GenTreePhysReg(REG_RAX, type) { RegNum = same ? REG_RAX : REG_R8 };
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RAX, type);
            codeGen.genCodeForPhysReg(tree);
            var expected = same ? RBM_RAX : RBM_RAX | RBM_R8;

            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(same ? 0 : 1));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(type == TYP_REF ? expected : RBM_NONE));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(type == TYP_BYREF ? expected : RBM_NONE));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CatchArgumentsTransferTheExceptionRoot(bool same)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var block = compiler.compCurBB ?? throw new AssertionException("Missing current block.");
            block.CatchType = bbCatchType.BBCT_FILTER;
            var exceptionMask = new regMaskTP(SRBM_EXCEPTION_OBJECT);
            codeGen.GCInfo.gcMarkRegSetGCref(exceptionMask);
            var tree = new GenTree(GT_CATCH_ARG, TYP_REF) { RegNum = same ? REG_EXCEPTION_OBJECT : REG_R8 };
            codeGen.genCodeForCatchArg(tree);

            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(same ? 0 : 1));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(same ? exceptionMask : RBM_R8));
        });
    }

    [TestCase(0, false)]
    [TestCase(0, true)]
    [TestCase(1, true)]
    public static void ReusedZeroDefinesAGcBoundaryOnlyAfterExistingInstructions(int value, bool nonempty)
    {
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
            codeGen.genCodeForReuseVal(tree);

            Assert.That(ReferenceEquals(before, codeGen.Emitter.emitCurIG), Is.EqualTo(value != 0 || !nonempty));
            Assert.That(CodeGenLocalHeapTests.AllDescriptors(before, codeGen), Has.Count.EqualTo(nonempty ? 1 : 0));
        });
    }
}
