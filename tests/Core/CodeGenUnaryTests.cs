// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CodeGenUnaryTests
{
    [TestCase(GT_ADD, TYP_INT, INS_add)]
    [TestCase(GT_AND, TYP_LONG, INS_and)]
    [TestCase(GT_LSH, TYP_INT, INS_shl)]
    [TestCase(GT_MUL, TYP_INT, INS_imul)]
    [TestCase(GT_NEG, TYP_LONG, INS_neg)]
    [TestCase(GT_NOT, TYP_INT, INS_not)]
    [TestCase(GT_OR, TYP_INT, INS_or)]
    [TestCase(GT_ROL, TYP_LONG, INS_rol)]
    [TestCase(GT_ROR, TYP_INT, INS_ror)]
    [TestCase(GT_RSH, TYP_LONG, INS_sar)]
    [TestCase(GT_RSZ, TYP_INT, INS_shr)]
    [TestCase(GT_SUB, TYP_INT, INS_sub)]
    [TestCase(GT_XOR, TYP_LONG, INS_xor)]
    [TestCase(GT_ADD, TYP_FLOAT, INS_addss)]
    [TestCase(GT_SUB, TYP_FLOAT, INS_subss)]
    [TestCase(GT_MUL, TYP_FLOAT, INS_mulss)]
    [TestCase(GT_DIV, TYP_FLOAT, INS_divss)]
    [TestCase(GT_ADD, TYP_DOUBLE, INS_addsd)]
    [TestCase(GT_SUB, TYP_DOUBLE, INS_subsd)]
    [TestCase(GT_MUL, TYP_DOUBLE, INS_mulsd)]
    [TestCase(GT_DIV, TYP_DOUBLE, INS_divsd)]
    public static void OperationSelectionPreservesNativeIntegerAndFloatingForms(
        genTreeOps oper, var_types type, instruction expected)
    {
        Assert.That(CodeGen.genGetInsForOper(oper, type), Is.EqualTo(expected));
    }

    [TestCase(GT_NEG, TYP_INT, REG_RAX, INS_neg, 1)]
    [TestCase(GT_NEG, TYP_INT, REG_RCX, INS_neg, 2)]
    [TestCase(GT_NEG, TYP_LONG, REG_RAX, INS_neg, 1)]
    [TestCase(GT_NEG, TYP_LONG, REG_RCX, INS_neg, 2)]
    [TestCase(GT_NOT, TYP_INT, REG_RAX, INS_not, 1)]
    [TestCase(GT_NOT, TYP_INT, REG_RCX, INS_not, 2)]
    [TestCase(GT_NOT, TYP_LONG, REG_RAX, INS_not, 1)]
    [TestCase(GT_NOT, TYP_LONG, REG_RCX, INS_not, 2)]
    public static void IntegerUnaryNodesConsumeCopyAndProduceInNativeOrder(
        genTreeOps oper, var_types type, regNumber target, instruction expected, int count)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var operand = compiler.gtNewIconNode(type, 1);
            operand.RegNum = REG_RAX;
            var tree = compiler.gtNewUnaryNode(oper, type, operand);
            tree.RegNum = target;
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RAX, TYP_REF);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RCX, TYP_BYREF);

            codeGen.genCodeForNegNot(tree.AsUnOp());

            var descriptors = Descriptors(codeGen.Emitter);
            Assert.That(descriptors.Count, Is.EqualTo(count));
            Assert.That(descriptors[^1].idIns(), Is.EqualTo(expected));
            Assert.That(descriptors[^1].idOpSize(), Is.EqualTo(type.EmitActualSize));
            Assert.That(descriptors[^1].idReg1(), Is.EqualTo(target));
            if (count == 2)
            {
                Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_mov));
                Assert.That(descriptors[0].idReg1(), Is.EqualTo(target));
                Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_RAX));
            }

            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur.IsEmpty, Is.True);
            var targetMask = regMaskTP.CreateFromRegNum(target, target.SingleTypeMask);
            Assert.That((codeGen.GCInfo.gcRegByrefSetCur & targetMask).IsEmpty, Is.True);
        });
    }

    [TestCase(TYP_FLOAT, false, false, 0x8000000080000000UL, INS_xorps)]
    [TestCase(TYP_FLOAT, false, true, 0x8000000080000000UL, INS_xorps)]
    [TestCase(TYP_DOUBLE, false, false, 0x8000000000000000UL, INS_xorps)]
    [TestCase(TYP_DOUBLE, false, true, 0x8000000000000000UL, INS_xorps)]
    [TestCase(TYP_FLOAT, true, false, 0x7FFFFFFF7FFFFFFFUL, INS_andps)]
    [TestCase(TYP_FLOAT, true, true, 0x7FFFFFFF7FFFFFFFUL, INS_andps)]
    [TestCase(TYP_DOUBLE, true, false, 0x7FFFFFFFFFFFFFFFUL, INS_andps)]
    [TestCase(TYP_DOUBLE, true, true, 0x7FFFFFFFFFFFFFFFUL, INS_andps)]
    public static void FloatingSignOperationsUseExactPackedMasks(
        var_types type, bool absolute, bool vex, ulong mask, instruction expected)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            codeGen.Emitter.UseVexEncodings = vex;
            codeGen.Emitter.UseEvexEncodings = vex;
            var operand = compiler.gtNewDconNode(type, -0.0);
            operand.RegNum = REG_XMM0;
            GenTree tree = absolute
                ? new GenTreeIntrinsic(type, operand, NI_System_Math_Abs, null)
                : compiler.gtNewUnaryNode(GT_NEG, type, operand);
            tree.RegNum = REG_XMM1;

            if (absolute)
            {
                codeGen.genIntrinsicBitwiseOp(tree);
            }
            else
            {
                codeGen.genCodeForNegNot(tree.AsUnOp());
            }

            var section = codeGen.Emitter.emitConsDsc.dsdList ??
                throw new AssertionException("No floating sign mask was recorded.");
            Assert.That(section.dsSize, Is.EqualTo(16));
            Assert.That(section.dsAlignment, Is.EqualTo(16));
            Assert.That(BitConverter.ToUInt64(section.Data, 0), Is.EqualTo(mask));
            Assert.That(BitConverter.ToUInt64(section.Data, 8), Is.EqualTo(mask));

            var descriptors = Descriptors(codeGen.Emitter);
            Assert.That(descriptors.Count, Is.EqualTo(vex ? 1 : 2));
            Assert.That(descriptors[^1].idIns(), Is.EqualTo(expected));
            Assert.That(descriptors[^1].idOpSize(), Is.EqualTo(EA_16BYTE));
            Assert.That(descriptors[^1].idReg1(), Is.EqualTo(REG_XMM1));
            if (!vex)
            {
                Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_movaps));
                Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_XMM0));
            }
        });
    }

    [Test]
    public static void UnaryProductionSpillsOnlyAfterTheOperation()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var operand = compiler.gtNewIconNode(TYP_INT, 1);
            operand.RegNum = REG_RAX;
            var tree = compiler.gtNewUnaryNode(GT_NEG, TYP_INT, operand);
            tree.RegNum = REG_RAX;
            tree.Flags |= GTF_SPILL;
            codeGen.RegSet.tmpInit();
            codeGen.RegSet.tmpPreAllocateTemps(TYP_INT, 1);
            var temp = codeGen.RegSet.tmpGetTemp(TYP_INT);
            temp.tdTempOffs = -32;
            codeGen.RegSet.tmpRlsTemp(temp);

            codeGen.genCodeForNegNot(tree.AsUnOp());

            var descriptors = Descriptors(codeGen.Emitter);
            Assert.That(descriptors.Count, Is.EqualTo(2));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_neg));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(INS_mov));
            Assert.That(descriptors[1].idInsFmt(), Is.EqualTo(Emitter.insFormat.IF_SWR_RRD));
            Assert.That(tree.Flags & (GTF_SPILL | GTF_SPILLED), Is.EqualTo(GTF_SPILLED));
        });
    }

#if DEBUG
    [TestCase(TYP_INT)]
    [TestCase(TYP_DOUBLE)]
    public static void DisassemblyRecordsNegationAndConsumesOperand(var_types type)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            GenTree operand = type == TYP_INT
                ? compiler.gtNewIconNode(type, 1)
                : compiler.gtNewDconNode(type, 1.0);
            operand.RegNum = type == TYP_INT ? REG_RAX : REG_XMM0;
            var tree = compiler.gtNewUnaryNode(GT_NEG, type, operand);
            tree.RegNum = operand.RegNum;
            compiler.opts.dspCode = true;

            var diagnostic = InstructionRecordingTestSupport.Capture(
                () => codeGen.genCodeForNegNot(tree.AsUnOp()));
            Assert.That(Descriptors(codeGen.Emitter), Is.Not.Empty);
            Assert.That(diagnostic, Is.Not.Empty);
        });
    }
#endif

    private static void WithCodeGen(Action<Compiler, CodeGen> action)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);
            codeGen.RegSet.ClearMaskVars();
            action(compiler, codeGen);
        });
    }

    private static List<Emitter.instrDesc> Descriptors(Emitter emitter)
        => CurrentDescriptors(emitter) ?? throw new AssertionException("No descriptor buffer was allocated.");

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "treeLifeUpdater")]
    private static extern ref TreeLifeUpdater? LifeUpdater(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);
}
