// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenLocalLoadTests
{
    [TestCase(TYP_BYREF)]
    [TestCase(TYP_I_IMPL)]
    public static void LocalAddressesPreserveOffsetsAndGcKind(var_types type)
    {
        CodeGenBinaryTests.WithCodeGen((_, codeGen) =>
        {
            var tree = new GenTreeLclFld(GT_LCL_ADDR, type, 0, 24) { RegNum = REG_RCX };

            codeGen.genCodeForLclAddr(tree);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_lea));
            Assert.That(descriptors[0].idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(24u));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur,
                Is.EqualTo(type == TYP_BYREF ? RBM_RCX : RBM_NONE));
        });
    }

    [TestCase(TYP_BYTE, false, INS_movsx)]
    [TestCase(TYP_UBYTE, false, INS_movzx)]
    [TestCase(TYP_SHORT, false, INS_movsx)]
    [TestCase(TYP_USHORT, false, INS_movzx)]
    [TestCase(TYP_BYTE, true, INS_mov)]
    [TestCase(TYP_USHORT, true, INS_mov)]
    public static void FieldLoadsHonorSignednessAndNoExtension(var_types type, bool noExtension, instruction ins)
    {
        CodeGenBinaryTests.WithCodeGen((_, codeGen) =>
        {
            var tree = new GenTreeLclFld(GT_LCL_FLD, type, 0, 2)
            {
                RegNum = REG_RCX,
                Flags = noExtension ? GTF_DONT_EXTEND : GTF_EMPTY,
            };

            codeGen.genCodeForLclFld(tree);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(ins));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(type.EmitSize));
            Assert.That(descriptors[0].idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(2u));
        });
    }

    [TestCase(TYP_BYTE, TYP_INT, REG_RCX, INS_mov, EA_4BYTE)]
    [TestCase(TYP_DOUBLE, TYP_DOUBLE, REG_XMM1, INS_movsd_simd, EA_8BYTE)]
    [TestCase(TYP_SIMD12, TYP_SIMD12, REG_XMM1, INS_movaps, EA_16BYTE)]
    [TestCase(TYP_SIMD16, TYP_SIMD16, REG_XMM1, INS_movaps, EA_16BYTE)]
    [TestCase(TYP_REF, TYP_REF, REG_RCX, INS_mov, EA_8BYTE)]
    public static void LocalLoadsUseRegisterTypesAndAlignedStackHomes(
        var_types localType, var_types nodeType, regNumber reg, instruction ins, emitAttr size)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].Type = localType;
            var tree = compiler.gtNewLclvNode(nodeType, 0);
            tree.RegNum = reg;

            codeGen.genCodeForLclVar(tree);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(ins));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(size));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(reg));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur,
                Is.EqualTo(nodeType == TYP_REF ? RBM_RCX : RBM_NONE));
        });
    }

    [TestCase(true, GTF_EMPTY)]
    [TestCase(false, GTF_SPILLED)]
    [TestCase(false, GTF_VAR_MULTIREG)]
    public static void RegisterCandidatesAndDeferredReloadsAreNotLoadedAgain(bool candidate, GenTreeFlags flags)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].lvLRACandidate = candidate;
            var tree = compiler.gtNewLclvNode(TYP_INT, 0);
            tree.RegNum = REG_RAX;
            tree.Flags |= flags;

            codeGen.genCodeForLclVar(tree);

            Assert.That(Descriptors(codeGen), Is.Empty);
#if DEBUG
            Assert.That(tree._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_PRODUCED,
                Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NONE));
#endif
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void Simd12FieldsReadExactlyTwelveBytesAndZeroTheFourthLane(bool vex)
    {
        CodeGenBinaryTests.WithCodeGen((_, codeGen) =>
        {
            codeGen.Emitter.UseVexEncodings = vex;
            codeGen.Emitter.UseEvexEncodings = vex;
            var tree = new GenTreeLclFld(GT_LCL_FLD, TYP_SIMD12, 0, 4) { RegNum = REG_XMM1 };

            codeGen.genCodeForLclFld(tree);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_movsd_simd));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_8BYTE));
            Assert.That(descriptors[0].idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(4u));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(INS_insertps));
            Assert.That(descriptors[1].idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(12u));
            Assert.That(InstructionConstant(codeGen.Emitter, descriptors[1]), Is.EqualTo((nint)0x28));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SimdStackImmediateWrapperCopiesOnlyForLegacyEncoding(bool vex)
    {
        CodeGenBinaryTests.WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            emitter.UseVexEncodings = vex;
            emitter.UseEvexEncodings = vex;

            emitter.emitIns_SIMD_R_R_S_I(INS_insertps, EA_16BYTE, REG_XMM1, REG_XMM0, 0, 8, 0x28,
                INS_OPTS_NONE);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(vex ? 1 : 2));
            Assert.That(descriptors[^1].idIns(), Is.EqualTo(INS_insertps));
            if (!vex)
            {
                Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_movaps));
                Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_XMM1));
                Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_XMM0));
            }
        });
    }

#if DEBUG
    [Test]
    public static void DisassemblyRejectsBeforeLocalLoadOrGcChanges()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var tree = new GenTreeLclFld(GT_LCL_FLD, TYP_REF, 0, 0) { RegNum = REG_RCX };
            compiler.opts.dspCode = true;
            _ = Assert.Throws<FatalJitException>(() => codeGen.genCodeForLclFld(tree));
            _ = Assert.Throws<FatalJitException>(() => codeGen.Emitter.emitIns_SIMD_R_R_S_I(
                INS_insertps, EA_16BYTE, REG_XMM1, REG_XMM0, 0, 8, 0x28, INS_OPTS_NONE));
            Assert.That(Descriptors(codeGen), Is.Empty);
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(RBM_NONE));
            compiler.opts.dspCode = false;

            codeGen.genCodeForLclFld(tree);
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(RBM_RCX));
        });
    }
#endif
}
