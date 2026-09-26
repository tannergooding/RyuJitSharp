// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Linq;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Globals;
using static RyuJitSharp.SpecialCodeKind;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenAddressCheckTests
{
    [TestCase(true, false)]
    [TestCase(true, true)]
    [TestCase(false, true)]
    public static void ExplicitAddressesPreserveOperandsAndGcResult(bool hasBase, bool hasIndex)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var tree = new GenTreeAddrMode(TYP_BYREF,
                hasBase ? Register(compiler, TYP_BYREF, REG_RAX) : null,
                hasIndex ? Register(compiler, TYP_I_IMPL, REG_RCX) : null,
                hasIndex ? (byte)4 : (byte)0, -16) { RegNum = REG_RDX };
            if (hasBase)
            {
                codeGen.GCInfo.gcMarkRegPtrVal(REG_RAX, TYP_BYREF);
            }

            codeGen.genLeaInstruction(tree);

            var id = Descriptors(codeGen).Single();
            Assert.That(id.idIns(), Is.EqualTo(INS_lea));
            Assert.That(id.idReg1(), Is.EqualTo(REG_RDX));
            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(hasBase ? REG_RAX : REG_NA));
            Assert.That(id.idAddr().iiaAddrMode.amIndxReg, Is.EqualTo(hasIndex ? REG_RCX : REG_NA));
            Assert.That(id.idAddr().iiaAddrMode.amScale, Is.EqualTo(hasIndex ? 2u : 0u));
            Assert.That(id.idAddr().iiaAddrMode.amDisp, Is.EqualTo(-16));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(RBM_RDX));
        });
    }

    [TestCase(TYP_INT, EA_4BYTE)]
    [TestCase(TYP_LONG, EA_8BYTE)]
    public static void NullChecksUseTheConsumedRegisterAsAddressAndCompareOperand(var_types type, emitAttr size)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var tree = new GenTreeIndir(GT_NULLCHECK, type, Register(compiler, TYP_REF, REG_R8));
            codeGen.GCInfo.gcMarkRegPtrVal(REG_R8, TYP_REF);

            codeGen.genCodeForNullCheck(tree);

            var id = Descriptors(codeGen).Single();
            Assert.That(id.idIns(), Is.EqualTo(INS_cmp));
            Assert.That(id.idInsFmt(), Is.EqualTo(IF_ARD_RRD));
            Assert.That(id.idOpSize(), Is.EqualTo(size));
            Assert.That(id.idReg1(), Is.EqualTo(REG_R8));
            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_R8));
            Assert.That(id.idAddr().iiaAddrMode.amDisp, Is.Zero);
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(RBM_NONE));
        });
    }

    [Test]
    public static void BoundsChecksPreserveZeroShortcutAndComparisonDirection(
        [Values(TYP_INT, TYP_LONG)] var_types type, [Values(0, 1, 2, 3)] int form)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].Type = type;
            GenTree index = form < 2
                ? compiler.gtNewIconNode(type, form == 0 ? 0 : -1)
                : Register(compiler, type, REG_RAX);
            index.IsContained = form < 2;
            GenTree length = form == 3
                ? new GenTreeLclVar(type, 0) { IsContained = true }
                : Register(compiler, type, REG_RCX);
            var tree = new GenTreeBoundsChk(index, length, SCK_RNGCHK_FAIL);
            var target = CodeGenBinaryTests.PrepareThrowTarget(compiler, SCK_RNGCHK_FAIL);

            codeGen.genRangeCheck(tree);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(form == 0 ? INS_test : INS_cmp));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(type.EmitSize));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(form < 2 ? REG_RCX : REG_RAX));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(form == 0 ? INS_je : form == 1 ? INS_jbe : INS_jae));
            Assert.That(EmitterJumpInstructionTests.JumpView.Target(descriptors[1]), Is.SameAs(target));
            if (form == 1)
            {
                Assert.That(InstructionConstant(codeGen.Emitter, descriptors[0]), Is.EqualTo((nint)(-1)));
            }
        });
    }

    [TestCase(INS_xchg, REG_RCX, IF_ARW_RRW)]
    [TestCase(INS_inc, REG_NA, IF_ARW)]
    public static void AddressDestinationsRetainReadWriteFormats(
        instruction ins, regNumber reg, Emitter.insFormat format)
    {
        CodeGenBinaryTests.WithCodeGen((_, codeGen) =>
        {
            codeGen.Emitter.emitIns_ARX_R(ins, EA_8BYTE, reg, REG_RAX, REG_RDX, 8, 32);

            var id = Descriptors(codeGen).Single();
            Assert.That(id.idInsFmt(), Is.EqualTo(format));
            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_RAX));
            Assert.That(id.idAddr().iiaAddrMode.amIndxReg, Is.EqualTo(REG_RDX));
            Assert.That(id.idAddr().iiaAddrMode.amScale, Is.EqualTo(3u));
            Assert.That(id.idAddr().iiaAddrMode.amDisp, Is.EqualTo(32));
            Assert.That(id.idCodeSize(), Is.GreaterThan(0u));
        });
    }

    [Test]
    public static void BoundsChecksBranchAroundTheInlineThrow()
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            var tree = new GenTreeBoundsChk(Register(compiler, TYP_INT, REG_RAX),
                Register(compiler, TYP_INT, REG_RCX), SCK_RNGCHK_FAIL);
            compiler.opts.compDbgCode = true;
            var firstGroup = codeGen.Emitter.emitCurIG;

            codeGen.genRangeCheck(tree);
            _ = CodeGenThrowHelperTests.AssertInlineThrow(firstGroup, INS_jb, 3);
        });
    }

#if DEBUG
    [TestCase(GT_LEA)]
    [TestCase(GT_NULLCHECK)]
    [TestCase(GT_BOUNDS_CHECK)]
    public static void DisassemblyRecordsAddressChecks(genTreeOps oper)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var source = Register(compiler, TYP_I_IMPL, REG_RAX);
            Action generate = oper switch
            {
                GT_LEA => () => codeGen.genLeaInstruction(
                    new GenTreeAddrMode(TYP_I_IMPL, source, null, 0, 8) { RegNum = REG_RDX }),
                GT_NULLCHECK => () => codeGen.genCodeForNullCheck(new GenTreeIndir(GT_NULLCHECK, TYP_INT, source)),
                _ => () => codeGen.genRangeCheck(new GenTreeBoundsChk(source,
                    Register(compiler, TYP_LONG, REG_RCX), SCK_RNGCHK_FAIL)),
            };
            compiler.opts.dspCode = true;
            _ = CodeGenBinaryTests.PrepareThrowTarget(compiler, SCK_RNGCHK_FAIL);

            var diagnostic = InstructionRecordingTestSupport.Capture(generate);
            Assert.That(Descriptors(codeGen), Is.Not.Empty);
            Assert.That(diagnostic, Is.Not.Empty);
        });
    }
#endif
}
