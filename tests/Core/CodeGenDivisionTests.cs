// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenDivisionTests
{
    [Test]
    public static void DivisionPreservesSignednessWidthAndResultRegister(
        [Values(GT_DIV, GT_UDIV, GT_MOD, GT_UMOD)] genTreeOps oper,
        [Values(TYP_INT, TYP_LONG)] var_types type)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var dividend = compiler.gtNewIconNode(type, -7);
            dividend.RegNum = REG_R8;
            var tree = new GenTreeOp(oper, type, dividend, Register(compiler, type, REG_RCX))
            {
                RegNum = REG_R9,
            };
            var unsigned = oper is GT_UDIV or GT_UMOD;
            if (!unsigned)
            {
                codeGen.GCInfo.gcMarkRegPtrVal(REG_RDX, TYP_BYREF);
            }

            codeGen.genCodeForDivMod(tree);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(4));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_mov));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_RAX));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_R8));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(unsigned ? INS_xor : INS_cdq));
            Assert.That(descriptors[2].idIns(), Is.EqualTo(unsigned ? INS_div : INS_idiv));
            Assert.That(descriptors[2].idOpSize(), Is.EqualTo(type.EmitSize));
            Assert.That(descriptors[2].idReg1(), Is.EqualTo(REG_RCX));
            Assert.That(descriptors[3].idReg1(), Is.EqualTo(REG_R9));
            Assert.That(descriptors[3].idReg2(), Is.EqualTo(oper is GT_DIV or GT_UDIV ? REG_RAX : REG_RDX));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(RBM_NONE));
        });
    }

    [TestCase(-1, INS_cdq)]
    [TestCase(0, INS_cdq)]
    [TestCase(1, INS_xor)]
    public static void PositiveConstantDividendsUseZeroExtension(int value, instruction extension)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var dividend = compiler.gtNewIconNode(TYP_LONG, value);
            dividend.RegNum = REG_RAX;
            var tree = new GenTreeOp(GT_DIV, TYP_LONG, dividend, Register(compiler, TYP_LONG, REG_RCX))
            {
                RegNum = REG_RAX,
            };

            codeGen.genCodeForDivMod(tree);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(extension));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(INS_idiv));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RemainderUsesLocalOrSpilledDivisorsAndSpillsAfterDivision(bool spillDivisor)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            codeGen.RegSet.tmpInit();
            codeGen.RegSet.tmpPreAllocateTemps(TYP_INT, 1);
            var temp = codeGen.RegSet.tmpGetTemp(TYP_INT);
            temp.tdTempOffs = -32;
            codeGen.RegSet.tmpRlsTemp(temp);
            GenTree divisor;
            if (spillDivisor)
            {
                divisor = Register(compiler, TYP_INT, REG_RCX);
                divisor.Flags |= GTF_SPILL;
                codeGen.RegSet.rsSpillTree(REG_RCX, divisor);
                divisor.Flags |= GTF_NOREG_AT_USE;
                divisor.IsRegOptional = true;
            }
            else
            {
                divisor = compiler.gtNewLclvNode(TYP_INT, 0);
                divisor.IsContained = true;
            }
            var tree = new GenTreeOp(GT_UMOD, TYP_INT, Register(compiler, TYP_INT, REG_RAX), divisor)
            {
                RegNum = REG_RDX,
                Flags = GTF_SPILL,
            };

            codeGen.genCodeForDivMod(tree);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(spillDivisor ? 4 : 3));
            Assert.That(descriptors[^3].idIns(), Is.EqualTo(INS_xor));
            Assert.That(descriptors[^2].idIns(), Is.EqualTo(INS_div));
            Assert.That(descriptors[^1].idIns(), Is.EqualTo(INS_mov));
            Assert.That((tree.Flags & GTF_SPILLED) != 0, Is.True);
        });
    }

#if DEBUG
    [Test]
    public static void DisassemblyRecordsDivisionWithoutConsumingOperandsTwice()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var tree = new GenTreeOp(GT_DIV, TYP_INT,
                Register(compiler, TYP_INT, REG_RAX), Register(compiler, TYP_INT, REG_RCX))
            {
                RegNum = REG_RAX,
            };
            compiler.opts.dspCode = true;
            var diagnostic = InstructionRecordingTestSupport.Capture(() => codeGen.genCodeForDivMod(tree));
            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(2));
            Assert.That(diagnostic, Does.Contain("idiv"));
        });
    }
#endif
}
