// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Linq;
using NUnit.Framework;
using static RyuJitSharp.emitAttr;
#if DEBUG && TARGET_AMD64
using static RyuJitSharp.genTreeOps;
#endif
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
#if DEBUG && TARGET_AMD64
using static RyuJitSharp.var_types;
#endif
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenOperandAdapterTests
{
#if TARGET_AMD64
    [TestCase(INS_addps, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2)]
    [TestCase(INS_addsd, EA_8BYTE, REG_XMM2, REG_XMM1, REG_XMM0)]
    [TestCase(INS_subps, EA_32BYTE, REG_XMM1, REG_XMM2, REG_XMM0)]
    public static void ThreeRegisterAdapterPreservesInstructionSizeAndRegisterOrder(
        instruction ins, emitAttr size, regNumber dst, regNumber src1, regNumber src2)
    {
        CodeGenBinaryTests.WithCodeGen((_, codeGen) =>
        {
            codeGen.inst_RV_RV_RV(ins, dst, src1, src2, size);

            var id = Descriptors(codeGen).Single();
            Assert.That(id.idIns(), Is.EqualTo(ins));
            Assert.That(id.idOpSize(), Is.EqualTo(size));
            Assert.That(id.idReg1(), Is.EqualTo(dst));
            Assert.That(id.idReg2(), Is.EqualTo(src1));
            Assert.That(id.idReg3(), Is.EqualTo(src2));
        });
    }

#if DEBUG
    [TestCase(GT_LCL_VAR, 0x24)]
    [TestCase(GT_STORE_LCL_VAR, 0x45)]
    public static void StackReferenceAdapterPreservesLocalIlOffset(genTreeOps oper, int ilOffset)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var tree = oper switch
            {
                GT_LCL_VAR => new GenTreeLclVar(TYP_INT, 0),
                GT_STORE_LCL_VAR => new GenTreeLclVar(TYP_INT, 0, compiler.gtNewIconNode(TYP_INT, 1)),
                _ => throw new AssertionException("Expected a local-variable load or store."),
            };
            tree.LclIlOffs = ilOffset;

            codeGen.inst_set_SV_var(tree);
            codeGen.Emitter.emitIns_R_S(INS_mov, EA_4BYTE, REG_RAX, 0, 0);

            var id = Descriptors(codeGen).Single();
            var debugInfo = id.idDebugOnlyInfo() ??
                throw new AssertionException("Missing stack-reference debug information.");
            Assert.That(debugInfo.idVarRefOffs, Is.EqualTo(unchecked((uint)ilOffset)));
        });
    }

    [Test]
    public static void LocalAddressCannotPublishAStackReferenceOffset()
    {
        CodeGenBinaryTests.WithCodeGen((_, codeGen) =>
        {
            var address = new GenTreeLclFld(GT_LCL_ADDR, TYP_BYREF, 0, 0);
            codeGen.Emitter.emitVarRefOffs = 0x24;

            var failure = Assert.Throws<FatalJitException>(() => codeGen.inst_set_SV_var(address));

            Assert.That(failure?.Result, Is.EqualTo(CorJitResult.CORJIT_INTERNALERROR));
            Assert.That(codeGen.Emitter.emitVarRefOffs, Is.EqualTo(0x24));
            Assert.That(Descriptors(codeGen), Is.Empty);
        });
    }
#endif
#endif

#if TARGET_X86
    [TestCase(-128)]
    [TestCase(0)]
    [TestCase(127)]
    public static void HandleImmediateKeepsPointerWidthRelocationAndFullEncoding(int value)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.opts.compReloc = true;

            codeGen.inst_IV_handle(INS_push, value);

            var id = Descriptors(codeGen).Single();
            Assert.That(id.idIns(), Is.EqualTo(INS_push));
            Assert.That(id.idOpSize(), Is.EqualTo(EA_PTRSIZE));
            Assert.That(id.idIsCnsReloc(), Is.True);
            Assert.That(id.idCodeSize(), Is.EqualTo(5u));
            Assert.That(InstructionConstant(codeGen.Emitter, id), Is.EqualTo((nint)value));
        });
    }
#endif
}
