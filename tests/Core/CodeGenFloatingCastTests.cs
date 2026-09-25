// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Linq;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenFloatingCastTests
{
    [TestCase(TYP_INT, TYP_FLOAT, INS_cvtsi2ss32)]
    [TestCase(TYP_INT, TYP_DOUBLE, INS_cvtsi2sd32)]
    [TestCase(TYP_LONG, TYP_FLOAT, INS_cvtsi2ss64)]
    [TestCase(TYP_LONG, TYP_DOUBLE, INS_cvtsi2sd64)]
    [TestCase(TYP_UINT, TYP_FLOAT, INS_vcvtusi2ss32)]
    [TestCase(TYP_UINT, TYP_DOUBLE, INS_vcvtusi2sd32)]
    [TestCase(TYP_ULONG, TYP_FLOAT, INS_vcvtusi2ss64)]
    [TestCase(TYP_ULONG, TYP_DOUBLE, INS_vcvtusi2sd64)]
    [TestCase(TYP_FLOAT, TYP_DOUBLE, INS_cvtss2sd)]
    [TestCase(TYP_DOUBLE, TYP_FLOAT, INS_cvtsd2ss)]
    public static void ConversionSelectionPreservesWidthsAndSignedness(
        var_types from, var_types to, instruction expected)
    {
        CodeGenBinaryTests.WithCodeGen((_, codeGen) =>
            Assert.That(codeGen.ins_FloatConv(to, from), Is.EqualTo(expected)));
    }

    [Test]
    public static void FloatWidthConversionsUseTheDestinationWidthAndNativeMergeRegister(
        [Values(TYP_FLOAT, TYP_DOUBLE)] var_types from, [Values(false, true)] bool vex,
        [Values(false, true)] bool memory)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            ConfigureIsa(compiler, codeGen, vex, evex: false);
            var to = from == TYP_FLOAT ? TYP_DOUBLE : TYP_FLOAT;
            var source = Source(compiler, from, memory);
            var cast = new GenTreeCast(to, source, false, to) { RegNum = REG_XMM0 };

            codeGen.genFloatToFloatCast(cast);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            var id = descriptors[0];
            Assert.That(id.idIns(), Is.EqualTo(from == TYP_FLOAT ? INS_cvtss2sd : INS_cvtsd2ss));
            Assert.That(id.idOpSize(), Is.EqualTo(to.EmitSize));
            Assert.That(id.idReg1(), Is.EqualTo(REG_XMM0));
            if (memory)
            {
                Assert.That(id.idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
            }
            else
            {
                Assert.That(vex ? id.idReg3() : id.idReg2(), Is.EqualTo(REG_XMM1));
            }
            AssertProduced(cast);
        });
    }

    [Test]
    public static void SameTypeFloatingCastsCopyLoadOrElideWithoutConverting(
        [Values(TYP_FLOAT, TYP_DOUBLE)] var_types type, [Values(false, true)] bool memory,
        [Values(false, true)] bool sameRegister)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            ConfigureIsa(compiler, codeGen, vex: true, evex: false);
            var source = Source(compiler, type, memory);
            var target = sameRegister ? REG_XMM1 : REG_XMM0;
            var cast = new GenTreeCast(type, source, false, type) { RegNum = target };

            codeGen.genFloatToFloatCast(cast);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(!memory && sameRegister ? 0 : 1));
            if (descriptors.Count != 0)
            {
                Assert.That(descriptors[0].idIns(), Is.EqualTo(memory
                    ? type == TYP_FLOAT ? INS_movss : INS_movsd_simd
                    : INS_movaps));
                Assert.That(descriptors[0].idOpSize(), Is.EqualTo(memory ? type.EmitSize : EA_16BYTE));
            }
            AssertProduced(cast);
        });
    }

    [Test]
    public static void SignedIntegerConversionsClearTheFalseDependencyBeforeConverting(
        [Values(TYP_INT, TYP_LONG)] var_types from, [Values(TYP_FLOAT, TYP_DOUBLE)] var_types to,
        [Values(false, true)] bool vex, [Values(false, true)] bool memory)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            ConfigureIsa(compiler, codeGen, vex, evex: false);
            var source = Source(compiler, from, memory);
            var cast = new GenTreeCast(to, source, false, to) { RegNum = REG_XMM0 };
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RAX, TYP_BYREF);

            codeGen.genIntToFloatCast(cast);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_xorps));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_16BYTE));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(codeGen.ins_FloatConv(to, from)));
            Assert.That(descriptors[1].idOpSize(), Is.EqualTo(from.EmitSize));
            Assert.That(descriptors[1].idReg1(), Is.EqualTo(REG_XMM0));
            if (!memory)
            {
                Assert.That(vex ? descriptors[1].idReg3() : descriptors[1].idReg2(), Is.EqualTo(REG_RAX));
                Assert.That(codeGen.GCInfo.gcRegByrefSetCur.IsEmpty, Is.True);
            }
            AssertProduced(cast);
        });
    }

    [Test]
    public static void EvexUnsignedConversionsUseNativeUnsignedOpcodesWithoutTemporaries(
        [Values(TYP_INT, TYP_LONG)] var_types from, [Values(TYP_FLOAT, TYP_DOUBLE)] var_types to,
        [Values(false, true)] bool memory)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            ConfigureIsa(compiler, codeGen, vex: true, evex: true);
            var source = Source(compiler, from, memory);
            var cast = new GenTreeCast(to, source, true, to) { RegNum = REG_XMM0 };

            codeGen.genIntToFloatCast(cast);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_xorps));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(codeGen.ins_FloatConv(to, varTypeToUnsigned(from))));
            Assert.That(descriptors[1].idOpSize(), Is.EqualTo(from.EmitSize));
            Assert.That(codeGen.InternalRegisters.GetAll(cast).IsEmpty, Is.True);
            AssertProduced(cast);
        });
    }

    [Test]
    public static void UnsignedLongFallbackRetainsStickyBitFixupWidthsAndTheSignBranch(
        [Values(TYP_FLOAT, TYP_DOUBLE)] var_types to, [Values(false, true)] bool vex,
        [Values(false, true)] bool egpr)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            ConfigureIsa(compiler, codeGen, vex, evex: false);
            var secondTemp = egpr ? REG_R16 : REG_RDX;
            if (egpr)
            {
                compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_APX);
                compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_APX);
                compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_APX);
                codeGen.Emitter.UseRex2Encodings = true;
            }
            var source = Source(compiler, TYP_LONG, memory: false);
            var cast = new GenTreeCast(to, source, true, to) { RegNum = REG_XMM0 };
            codeGen.InternalRegisters.Add(cast, Mask(REG_RCX) | Mask(secondTemp));
            var firstGroup = codeGen.Emitter.emitCurIG ?? throw new AssertionException("Missing instruction group.");
            var currentBlock = compiler.compCurBB ?? throw new AssertionException("Missing current block.");
            currentBlock.SetFlags(BasicBlockFlags.BBF_COLD);

            codeGen.genIntToFloatCast(cast);

            var saved = firstGroup.igData ?? throw new AssertionException("Missing conversion group.");
            Assert.That(saved.Select(id => id.idIns()), Is.EqualTo((instruction[])
            [
                INS_xorps, INS_mov, INS_shr_1, INS_mov, INS_and, INS_or, INS_test, INS_cmovns,
                to == TYP_FLOAT ? INS_cvtsi2ss64 : INS_cvtsi2sd64,
                INS_jns, to == TYP_FLOAT ? INS_addss : INS_addsd,
            ]));
            Assert.That(saved[1].idReg1(), Is.EqualTo(secondTemp));
            Assert.That(saved[1].idOpSize(), Is.EqualTo(EA_8BYTE));
            Assert.That(saved[3].idReg1(), Is.EqualTo(REG_RCX));
            Assert.That(saved[3].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(saved[4].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(InstructionConstant(codeGen.Emitter, saved[4]), Is.EqualTo((nint)1));
            Assert.That(saved[5].idReg2(), Is.EqualTo(secondTemp));
            Assert.That(saved[7].idReg2(), Is.EqualTo(REG_RAX));
            Assert.That(saved[8].idReg2(), Is.EqualTo(REG_RCX));
            Assert.That(saved[8].idOpSize(), Is.EqualTo(EA_8BYTE));
            Assert.That(saved[10].idOpSize(), Is.EqualTo(to.EmitSize));
            var label = EmitterJumpInstructionTests.JumpView.Target(saved[9])
                ?? throw new AssertionException("Missing fixup label.");
            Assert.That(label.HasFlag(BasicBlockFlags.BBF_COLD | BasicBlockFlags.BBF_HAS_LABEL), Is.True);
            Assert.That(label.bbEmitCookie, Is.SameAs(codeGen.Emitter.emitCurIG));
            Assert.That(codeGen.InternalRegisters.GetAll(cast).IsEmpty, Is.True);
            AssertProduced(cast);
        });
    }

    [TestCase(TYP_FLOAT, TYP_DOUBLE)]
    [TestCase(TYP_DOUBLE, TYP_FLOAT)]
    public static void ContainedFloatingConstantsUseThePoolWithoutLosingSignedZero(var_types from, var_types to)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            ConfigureIsa(compiler, codeGen, vex: true, evex: false);
            var source = compiler.gtNewDconNode(from, -0.0);
            source.IsContained = true;
            var cast = new GenTreeCast(to, source, false, to) { RegNum = REG_XMM0 };

            codeGen.genFloatToFloatCast(cast);

            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(1));
            Assert.That(Descriptors(codeGen)[0].idIns(), Is.EqualTo(codeGen.ins_FloatConv(to, from)));
            var data = codeGen.Emitter.emitConsDsc.dsdLast
                ?? throw new AssertionException("Missing floating constant data.");
            Assert.That(data.dsSize, Is.EqualTo(from.Size));
            Assert.That(data.Data, Is.EqualTo(from == TYP_FLOAT
                ? BitConverter.GetBytes(-0.0f) : BitConverter.GetBytes(-0.0)));
            AssertProduced(cast);
        });
    }

    [Test]
    public static void KnownStackAddressesCastAwayByrefTypingBeforeInstructionRecording()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            ConfigureIsa(compiler, codeGen, vex: true, evex: false);
            var source = compiler.gtNewLclAddrNode(TYP_BYREF, 0, 0);
            source.RegNum = REG_RAX;
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RAX, TYP_BYREF);
            var cast = new GenTreeCast(TYP_DOUBLE, source, false, TYP_DOUBLE) { RegNum = REG_XMM0 };

            codeGen.genIntToFloatCast(cast);

            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(2));
            Assert.That(Descriptors(codeGen)[1].idIns(), Is.EqualTo(INS_cvtsi2sd64));
            Assert.That(Descriptors(codeGen)[1].idOpSize(), Is.EqualTo(EA_8BYTE));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur.IsEmpty, Is.True);
            AssertProduced(cast);
        });
    }

    [TestCase(TYP_LONG, TYP_DOUBLE, INS_cvtsi2sd64)]
    [TestCase(TYP_FLOAT, TYP_DOUBLE, INS_cvtss2sd)]
    [TestCase(TYP_DOUBLE, TYP_DOUBLE, INS_movsd_simd)]
    public static void SpilledOperandsAreConsumedFromMemoryAndReturnTheirTemporary(
        var_types from, var_types to, instruction expected)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            ConfigureIsa(compiler, codeGen, vex: true, evex: false);
            var source = Source(compiler, from, memory: false);
            codeGen.RegSet.tmpInit();
            codeGen.RegSet.tmpPreAllocateTemps(from, 1);
            var temp = codeGen.RegSet.tmpGetTemp(from);
            temp.tdTempOffs = -32;
            codeGen.RegSet.tmpRlsTemp(temp);
            source.Flags |= GTF_SPILL;
            codeGen.RegSet.rsSpillTree(source.RegNum, source);
            source.Flags |= GTF_NOREG_AT_USE;
            source.IsRegOptional = true;
            var cast = new GenTreeCast(to, source, false, to) { RegNum = REG_XMM0 };
            var before = Descriptors(codeGen).Count;

            if (varTypeIsFloating(from))
            {
                codeGen.genFloatToFloatCast(cast);
            }
            else
            {
                codeGen.genIntToFloatCast(cast);
            }

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(before + (varTypeIsFloating(from) ? 1 : 2)));
            Assert.That(descriptors[^1].idIns(), Is.EqualTo(expected));
            Assert.That(descriptors[^1].idAddr().iiaLclVar.lvaVarNum(), Is.EqualTo(temp.tdTempNum));
            Assert.That(codeGen.RegSet.rsGetSpillInfo(source, source.RegNum, out _), Is.Null);
            var reused = codeGen.RegSet.tmpGetTemp(from);
            Assert.That(reused, Is.SameAs(temp));
            codeGen.RegSet.tmpRlsTemp(reused);
            AssertProduced(cast);
        });
    }

    [Test]
    public static void ExtractHonorsMasksLowestRegisterOrderAndReleaseOwnership()
    {
        CodeGenBinaryTests.WithCodeGen((_, codeGen) =>
        {
            ref var registers = ref codeGen.InternalRegisters;
            var tree = new GenTreePhysReg(REG_RAX);
            registers.Add(tree, Mask(REG_RDX) | Mask(REG_R8) | Mask(REG_R16) | Mask(REG_K1));

            Assert.That(registers.Extract(tree, Mask(REG_K1)), Is.EqualTo(REG_K1));
            Assert.That(registers.Extract(tree), Is.EqualTo(REG_RDX));
            Assert.That(registers.Extract(tree, Mask(REG_R16) | Mask(REG_R8)), Is.EqualTo(REG_R8));
            Assert.That(registers.Extract(tree), Is.EqualTo(REG_R16));
            Assert.That(registers.GetAll(tree).IsEmpty, Is.True);
        });
    }

    [TestCase(0u, INS_shr_N)]
    [TestCase(1u, INS_shr_1)]
    [TestCase(63u, INS_shr_N)]
    [TestCase(255u, INS_shr_N)]
    public static void ConstantShiftWrapperSelectsTheNativeOneOrImmediateOpcode(uint count, instruction expected)
    {
        CodeGenBinaryTests.WithCodeGen((_, codeGen) =>
        {
            codeGen.inst_RV_SH(INS_shr, EA_8BYTE, REG_RAX, count);
            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(1));
            Assert.That(Descriptors(codeGen)[0].idIns(), Is.EqualTo(expected));
            if (count != 1)
            {
                Assert.That(InstructionConstant(codeGen.Emitter, Descriptors(codeGen)[0]), Is.EqualTo((nint)(count & 127)));
            }
        });
    }

#if DEBUG
    [Test]
    public static void DisassemblyRejectionPrecedesConsumptionTemporaryExtractionAndEmission(
        [Values(false, true)] bool floating)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var source = Source(compiler, floating ? TYP_FLOAT : TYP_LONG, memory: false);
            var cast = new GenTreeCast(TYP_DOUBLE, source, !floating, TYP_DOUBLE) { RegNum = REG_XMM0 };
            var temps = Mask(REG_RCX) | Mask(REG_RDX);
            codeGen.InternalRegisters.Add(cast, temps);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RAX, TYP_BYREF);
            var gc = codeGen.GCInfo.gcRegByrefSetCur;
            compiler.opts.dspCode = true;

            _ = Assert.Throws<FatalJitException>(() =>
            {
                if (floating)
                {
                    codeGen.genFloatToFloatCast(cast);
                }
                else
                {
                    codeGen.genIntToFloatCast(cast);
                }
            });

            Assert.That(Descriptors(codeGen), Is.Empty);
            Assert.That(codeGen.InternalRegisters.GetAll(cast), Is.EqualTo(temps));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(gc));
            Assert.That(source._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED, Is.EqualTo((GenTreeDebugFlags)0));
            Assert.That(cast._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_PRODUCED, Is.EqualTo((GenTreeDebugFlags)0));
        });
    }
#endif

    private static GenTree Source(Compiler compiler, var_types type, bool memory)
    {
        if (memory)
        {
            compiler.lvaTable[0].Type = type;
            compiler.lvaTable[0].RegNum = REG_STK;
            compiler.lvaTable[0].lvTracked = false;
            return new GenTreeLclVar(type, 0) { IsContained = true };
        }

        var reg = varTypeIsFloating(type) ? REG_XMM1 : REG_RAX;

        return new GenTreePhysReg(reg, type) { RegNum = reg };
    }

    private static void ConfigureIsa(Compiler compiler, CodeGen codeGen, bool vex, bool evex)
    {
        codeGen.Emitter.UseVexEncodings = vex;
        codeGen.Emitter.UseEvexEncodings = evex;
        if (vex)
        {
            EnableAvx2(compiler);
        }
        if (evex)
        {
            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX512);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_AVX512);
            compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_AVX512);
        }
    }

    private static regMaskTP Mask(regNumber reg) => regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);

    private static void AssertProduced(GenTree tree)
    {
#if DEBUG
        Assert.That(tree._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_PRODUCED, Is.Not.EqualTo((GenTreeDebugFlags)0));
#else
        Assert.That(tree.RegNum, Is.EqualTo(REG_XMM0).Or.EqualTo(REG_XMM1));
#endif
    }
}
