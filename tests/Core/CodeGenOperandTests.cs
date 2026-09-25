// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CodeGenOperandTests
{
    [Test]
    public static void RegistersAndExistingIndirectionsRetainIdentity()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var reg = Register(compiler, TYP_I_IMPL, REG_RAX);
            var register = codeGen.genOperandDesc(INS_mov, reg);
            Assert.That(register.GetKind(), Is.EqualTo(CodeGen.OperandKind.Reg));
            Assert.That(register.GetReg(), Is.EqualTo(REG_RAX));
            Assert.That(register.IsContained(), Is.False);

            var indir = new GenTreeIndir(GT_IND, TYP_INT, reg) { IsContained = true };
            var memory = codeGen.genOperandDesc(INS_mov, indir);
            Assert.That(memory.GetKind(), Is.EqualTo(CodeGen.OperandKind.Indir));
            Assert.That(memory.GetIndirForm(), Is.SameAs(indir));
        });
    }

    [TestCase(false, 0)]
    [TestCase(false, 65534)]
    [TestCase(true, 0)]
    [TestCase(true, 65534)]
    public static void LocalFieldsAndFoldedAddressesPreserveUnsignedOffsets(bool indir, int offset)
    {
        WithCodeGen((_, codeGen) =>
        {
            var local = new GenTreeLclFld(indir ? GT_LCL_ADDR : GT_LCL_FLD,
                indir ? TYP_BYREF : TYP_INT, 0, (ushort)offset) { IsContained = true };
            GenTree operand = indir ? new GenTreeIndir(GT_IND, TYP_INT, local) { IsContained = true } : local;
            var descriptor = codeGen.genOperandDesc(INS_mov, operand);

            Assert.That(descriptor.GetKind(), Is.EqualTo(CodeGen.OperandKind.Local));
            Assert.That(descriptor.GetVarNum(), Is.Zero);
            Assert.That(descriptor.GetLclOffset(), Is.EqualTo(offset));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ImmediateDescriptorsPreserveRelocationAttributes(bool relocatable)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.opts.compReloc = relocatable;
            var constant = compiler.gtNewIconNode(TYP_I_IMPL, 0x1234);
            constant.Flags |= GTF_ICON_FIELD_HDL;
            constant.IsContained = true;
            var descriptor = codeGen.genOperandDesc(INS_mov, constant);

            Assert.That(descriptor.GetKind(), Is.EqualTo(CodeGen.OperandKind.Imm));
            Assert.That(descriptor.GetImmediate(), Is.EqualTo((nint)0x1234));
            Assert.That(EA_IS_CNS_RELOC(descriptor.GetEmitAttrForImmediate(EA_8BYTE)), Is.EqualTo(relocatable));
            Assert.That(codeGen.Emitter.emitConsDsc.dsdOffs, Is.Zero);
        });
    }

    [Test]
    public static void FloatingConstantsPreserveNegativeZeroInThePool()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var value = compiler.gtNewDconNode(TYP_DOUBLE, -0.0);
            value.IsContained = true;
            var descriptor = codeGen.genOperandDesc(INS_addsd, value);

            Assert.That(descriptor.GetKind(), Is.EqualTo(CodeGen.OperandKind.ClsVar));
            Assert.That((nuint)descriptor.GetFieldHnd(), Is.Not.Zero);
            Assert.That(Section(codeGen).Data, Is.EqualTo(BitConverter.GetBytes(-0.0)));
        });
    }

    [TestCase(INS_movups, 16)]
    [TestCase(INS_vbroadcastss, 4)]
    [TestCase(INS_vbroadcastsd, 8)]
    public static void VectorConstantsUseOnlyTheInstructionInputWidth(instruction ins, int size)
    {
        WithCodeGen((_, codeGen) =>
        {
            var value = new GenTreeVecCon(TYP_SIMD16) { IsContained = true };
            value.SimdVal.u64[0] = 0xFEDCBA9876543210;
            value.SimdVal.u64[1] = 0x1020304050607080;
            var descriptor = codeGen.genOperandDesc(ins, value);

            Assert.That(descriptor.GetKind(), Is.EqualTo(CodeGen.OperandKind.ClsVar));
            Assert.That(Section(codeGen).dsSize, Is.EqualTo(size));
            Assert.That(Section(codeGen).Data[0], Is.EqualTo(0x10));
        });
    }

    [TestCase(TYP_INT, 4)]
    [TestCase(TYP_UINT, 4)]
    [TestCase(TYP_LONG, 8)]
    [TestCase(TYP_ULONG, 8)]
    public static void IntegralBroadcastsStoreOnlyTheScalarPayload(var_types baseType, int size)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var scalar = compiler.gtNewIconNode(baseType.Size == 8 ? TYP_LONG : TYP_INT, -2);
            scalar.IsContained = true;
            var broadcast = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AVX2_BroadcastScalarToVector128,
                baseType, 16, scalar) { IsContained = true };
            var descriptor = codeGen.genOperandDesc(INS_paddd, broadcast);

            Assert.That(descriptor.GetKind(), Is.EqualTo(CodeGen.OperandKind.ClsVar));
            Assert.That(Section(codeGen).dsAlignment, Is.EqualTo(size));
            Assert.That(Section(codeGen).dsDataType, Is.EqualTo(baseType));
            Assert.That(Section(codeGen).Data, Is.EqualTo(BitConverter.GetBytes(-2L)[..size]));
        });
    }

    [Test]
    public static void BroadcastsOfFloatingConstantsRecurseWithoutWrappingAnAddress()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var scalar = compiler.gtNewDconNode(TYP_DOUBLE, -0.0);
            scalar.IsContained = true;
            var broadcast = new GenTreeHWIntrinsic(TYP_SIMD16, NI_X86Base_MoveAndDuplicate,
                TYP_DOUBLE, 16, scalar) { IsContained = true };
            var descriptor = codeGen.genOperandDesc(INS_addpd, broadcast);

            Assert.That(descriptor.GetKind(), Is.EqualTo(CodeGen.OperandKind.ClsVar));
            Assert.That(Section(codeGen).Data, Is.EqualTo(BitConverter.GetBytes(-0.0)));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MemoryBroadcastsRetainNativeAddressForms(bool localAddress)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            GenTree source = localAddress
                ? new GenTreeLclFld(GT_LCL_ADDR, TYP_BYREF, 0, 4)
                : compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            source.IsContained = true;
            var broadcast = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AVX_BroadcastScalarToVector128,
                TYP_DOUBLE, 16, source) { IsContained = true };
            var descriptor = codeGen.genOperandDesc(INS_addpd, broadcast);

            if (localAddress)
            {
                Assert.That(descriptor.GetKind(), Is.EqualTo(CodeGen.OperandKind.Local));
                Assert.That(descriptor.GetLclOffset(), Is.EqualTo(4));
            }
            else
            {
                var form = descriptor.GetIndirForm();
                Assert.That(form.Type, Is.EqualTo(TYP_DOUBLE));
                Assert.That(form.Op1, Is.SameAs(source));
                Assert.That(form.IsContained, Is.True);
                Assert.That(form.RegNum, Is.EqualTo(REG_NA));
            }
        });
    }

    [Test]
    public static void MaskConstantsUseOwnedData()
    {
        WithCodeGen((_, codeGen) =>
        {
            simdmask_t value = default;
            value.u64[0] = 0x0123456789ABCDEF;
            var node = new GenTreeMskCon(value) { IsContained = true };
            var descriptor = codeGen.genOperandDesc(INS_kmovq_msk, node);

            Assert.That(descriptor.GetKind(), Is.EqualTo(CodeGen.OperandKind.ClsVar));
            Assert.That(Section(codeGen).Data, Is.EqualTo(BitConverter.GetBytes(value.u64[0])));
        });
    }

    [Test]
    public static void SpillDescriptorsTransferOwnershipOnlyOnce()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            codeGen.RegSet.tmpInit();
            codeGen.RegSet.tmpPreAllocateTemps(TYP_INT, 1);
            var temp = codeGen.RegSet.tmpGetTemp(TYP_INT);
            temp.tdTempOffs = -32;
            codeGen.RegSet.tmpRlsTemp(temp);
            var node = Register(compiler, TYP_INT, REG_RAX);
            node.Flags |= GTF_SPILL;
            codeGen.RegSet.rsSpillTree(REG_RAX, node);
            node.Flags |= GTF_NOREG_AT_USE;
            node.IsRegOptional = true;

            var descriptor = codeGen.genOperandDesc(INS_shlx, node);

            Assert.That(descriptor.GetKind(), Is.EqualTo(CodeGen.OperandKind.Local));
            Assert.That(descriptor.GetVarNum(), Is.EqualTo(temp.tdTempNum));
            Assert.That(codeGen.RegSet.rsGetSpillInfo(node, REG_RAX, out _), Is.Null);
            var reused = codeGen.RegSet.tmpGetTemp(TYP_INT);
            Assert.That(reused, Is.SameAs(temp));
            codeGen.RegSet.tmpRlsTemp(reused);
        });
    }

    [TestCase(INS_vpandq, INS_pandd)]
    [TestCase(INS_vpandnq, INS_pandnd)]
    [TestCase(INS_vporq, INS_pord)]
    [TestCase(INS_vpxorq, INS_pxord)]
    public static void OptionalEvexInstructionsFallBackToVex(instruction ins, instruction expected)
    {
        WithCodeGen((_, codeGen) =>
        {
            var operand = new GenTreeVecCon(TYP_SIMD16) { RegNum = REG_XMM2 };
            codeGen.inst_RV_RV_TT(ins, EA_16BYTE, REG_XMM0, REG_XMM1, operand, false, INS_OPTS_NONE);

            Assert.That(Descriptors(codeGen)[^1].idIns(), Is.EqualTo(expected));
        });
    }

    [TestCase(INS_vextractf64x2, INS_vextractf32x4)]
    [TestCase(INS_vextracti64x2, INS_vextracti32x4)]
    public static void OptionalEvexExtractsFallBackToVex(instruction ins, instruction expected)
    {
        WithCodeGen((_, codeGen) =>
        {
            var operand = new GenTreeVecCon(TYP_SIMD32) { RegNum = REG_XMM1 };
            codeGen.inst_RV_TT_IV(ins, EA_32BYTE, REG_XMM0, operand, 1, INS_OPTS_NONE);

            Assert.That(Descriptors(codeGen)[^1].idIns(), Is.EqualTo(expected));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void EmbeddedBroadcastRequiresEvex(bool evex)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            codeGen.Emitter.UseEvexEncodings = evex;
            var scalar = compiler.gtNewIconNode(TYP_INT, 7);
            scalar.IsContained = true;
            var broadcast = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AVX2_BroadcastScalarToVector128,
                TYP_INT, 16, scalar) { IsContained = true };

            Assert.That(codeGen.IsEmbeddedBroadcastEnabled(INS_paddd, broadcast), Is.EqualTo(evex));
            if (evex)
            {
                codeGen.inst_RV_RV_TT(INS_paddd, EA_16BYTE, REG_XMM0, REG_XMM1, broadcast, false, INS_OPTS_NONE);
                Assert.That(Section(codeGen).dsSize, Is.EqualTo(4));
            }
        });
    }

    [Test]
    public static void CommutativeLegacyOperationsSwapAnAliasedSecondOperand()
    {
        WithCodeGen((_, codeGen) =>
        {
            codeGen.Emitter.UseVexEncodings = false;
            codeGen.Emitter.UseEvexEncodings = false;
            var operand = new GenTreeVecCon(TYP_SIMD16) { RegNum = REG_XMM0 };
            codeGen.inst_RV_RV_TT(INS_pandd, EA_16BYTE, REG_XMM0, REG_XMM1, operand, true, INS_OPTS_NONE);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_XMM0));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_XMM1));
        });
    }

#if DEBUG
    [TestCase(false)]
    [TestCase(true)]
    public static void DisassemblyRejectionPrecedesConstantAllocation(bool immediate)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var value = new GenTreeVecCon(TYP_SIMD16) { IsContained = true };
            compiler.opts.dspCode = true;
            _ = Assert.Throws<FatalJitException>(() =>
            {
                if (immediate)
                {
                    codeGen.inst_RV_TT_IV(INS_pshufd, EA_16BYTE, REG_XMM0, value, 1, INS_OPTS_NONE);
                }
                else
                {
                    codeGen.inst_RV_RV_TT(INS_pandd, EA_16BYTE, REG_XMM0, REG_XMM1, value, false, INS_OPTS_NONE);
                }
            });

            Assert.That(codeGen.Emitter.emitConsDsc.dsdOffs, Is.Zero);
        });
    }
#endif

    private static Emitter.dataSection Section(CodeGen codeGen)
        => codeGen.Emitter.emitConsDsc.dsdLast ?? throw new AssertionException("Missing constant data.");
}
