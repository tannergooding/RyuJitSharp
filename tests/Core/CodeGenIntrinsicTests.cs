// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.SpecialCodeKind;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CodeGenIntrinsicTests
{
    [Test]
    public static void FiniteChecksTestTheExponentWithoutChangingTheValue(
        [Values(TYP_FLOAT, TYP_DOUBLE)] var_types type, [Values(false, true)] bool alias)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var target = CodeGenBinaryTests.PrepareThrowTarget(compiler, SCK_ARITH_EXCPN);
            var operand = compiler.gtNewDconNode(type, -0.0);
            operand.RegNum = REG_XMM0;
            var tree = compiler.gtNewUnaryNode(GT_CKFINITE, type, operand);
            tree.RegNum = alias ? REG_XMM0 : REG_XMM1;
            codeGen.InternalRegisters.Add(tree, RBM_R11);

            codeGen.genCkfinite(tree);

            var descriptors = Descriptors(codeGen);
            var isDouble = type == TYP_DOUBLE;
            var mask = isDouble ? 0x7FF00000 : 0x7F800000;
            var maskIndex = isDouble ? 2 : 1;
            Assert.That(descriptors, Has.Count.EqualTo(maskIndex + 3 + (alias ? 0 : 1)));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(isDouble ? INS_movd64 : INS_movd32));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(isDouble ? EA_8BYTE : EA_4BYTE));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R11));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_XMM0));
            if (isDouble)
            {
                Assert.That(descriptors[1].idIns(), Is.EqualTo(INS_shr_N));
                Assert.That(descriptors[1].idOpSize(), Is.EqualTo(EA_8BYTE));
                Assert.That(InstructionConstant(codeGen.Emitter, descriptors[1]), Is.EqualTo((nint)32));
            }
            Assert.That(descriptors[maskIndex].idIns(), Is.EqualTo(INS_and));
            Assert.That(descriptors[maskIndex].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(InstructionConstant(codeGen.Emitter, descriptors[maskIndex]), Is.EqualTo((nint)mask));
            Assert.That(descriptors[maskIndex + 1].idIns(), Is.EqualTo(INS_cmp));
            Assert.That(InstructionConstant(codeGen.Emitter, descriptors[maskIndex + 1]), Is.EqualTo((nint)mask));
            Assert.That(descriptors[maskIndex + 2].idIns(), Is.EqualTo(INS_je));
            Assert.That(EmitterJumpInstructionTests.JumpView.Target(descriptors[maskIndex + 2]), Is.SameAs(target));
            if (!alias)
            {
                Assert.That(descriptors[^1].idIns(), Is.EqualTo(INS_movaps));
                Assert.That(descriptors[^1].idReg1(), Is.EqualTo(REG_XMM1));
                Assert.That(descriptors[^1].idReg2(), Is.EqualTo(REG_XMM0));
            }
            Assert.That(BitConverter.DoubleToInt64Bits(operand.DconVal), Is.EqualTo(long.MinValue));
        });
    }

    [Test]
    public static void FiniteChecksRejectInlineThrowsBeforeExtractingTemporaries()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var source = compiler.gtNewDconNode(TYP_DOUBLE, double.PositiveInfinity);
            source.RegNum = REG_XMM0;
            var tree = compiler.gtNewUnaryNode(GT_CKFINITE, TYP_DOUBLE, source);
            tree.RegNum = REG_XMM0;
            codeGen.InternalRegisters.Add(tree, RBM_R11);
            compiler.opts.compDbgCode = true;

            _ = Assert.Throws<FatalJitException>(() => codeGen.genCkfinite(tree));
            Assert.That(Descriptors(codeGen), Is.Empty);
            compiler.opts.compDbgCode = false;
            _ = CodeGenBinaryTests.PrepareThrowTarget(compiler, SCK_ARITH_EXCPN);
            codeGen.genCkfinite(tree);
            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(5));
        });
    }

    [Test]
    public static void ScalarMathRetainsRoundingImmediatesAndOperandForms(
        [Values(NI_System_Math_Round, NI_System_Math_Ceiling, NI_System_Math_Floor,
            NI_System_Math_Truncate, NI_System_Math_Sqrt)] NamedIntrinsic intrinsic,
        [Values(TYP_FLOAT, TYP_DOUBLE)] var_types type,
        [Values(false, true)] bool vex, [Values(0, 1, 2, 3, 4)] int sourceKind)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            codeGen.Emitter.UseVexEncodings = vex;
            codeGen.Emitter.UseEvexEncodings = vex;
            if (vex)
            {
                EnableAvx2(compiler);
            }
            compiler.lvaTable[0].Type = type;
            GenTree source;
            if (sourceKind == 1)
            {
                source = compiler.gtNewLclvNode(type, 0);
                source.IsContained = true;
            }
            else if (sourceKind == 2)
            {
                source = new GenTreeIndir(GT_IND, type, Register(compiler, TYP_BYREF, REG_RAX))
                {
                    IsContained = true,
                };
            }
            else
            {
                source = compiler.gtNewDconNode(type, -0.0);
                source.RegNum = sourceKind == 3 ? REG_NA : REG_XMM0;
                if (sourceKind == 3)
                {
                    source.IsContained = true;
                }
                else if (sourceKind == 4)
                {
                    codeGen.RegSet.tmpInit();
                    codeGen.RegSet.tmpPreAllocateTemps(type, 1);
                    var temp = codeGen.RegSet.tmpGetTemp(type);
                    temp.tdTempOffs = -32;
                    codeGen.RegSet.tmpRlsTemp(temp);
                    source.Flags |= GTF_SPILL;
                    codeGen.RegSet.rsSpillTree(REG_XMM0, source);
                    source.Flags |= GTF_NOREG_AT_USE;
                    source.IsRegOptional = true;
                }
            }
            var tree = new GenTreeIntrinsic(type, source, intrinsic, null) { RegNum = REG_XMM1 };
            var before = Descriptors(codeGen).Count;

            codeGen.genIntrinsic(tree);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(before + 1));
            var descriptor = descriptors[^1];
            var ins = intrinsic == NI_System_Math_Sqrt
                ? type == TYP_FLOAT ? INS_sqrtss : INS_sqrtsd
                : type == TYP_FLOAT ? INS_roundss : INS_roundsd;
            Assert.That(descriptor.idIns(), Is.EqualTo(ins));
            Assert.That(descriptor.idOpSize(), Is.EqualTo(type.EmitSize));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_XMM1));
            if (intrinsic != NI_System_Math_Sqrt)
            {
                var immediate = intrinsic switch
                {
                    NI_System_Math_Round => 4,
                    NI_System_Math_Ceiling => 10,
                    NI_System_Math_Floor => 9,
                    _ => 11,
                };
                Assert.That(InstructionConstant(codeGen.Emitter, descriptor), Is.EqualTo((nint)immediate));
            }
            if (sourceKind == 3)
            {
                var data = codeGen.Emitter.emitConsDsc.dsdLast ??
                    throw new AssertionException("Missing scalar constant.");
                Assert.That(data.Data, Is.EqualTo(type == TYP_FLOAT
                    ? BitConverter.GetBytes(-0.0f) : BitConverter.GetBytes(-0.0)));
            }
            else if (sourceKind == 4)
            {
                Assert.That(codeGen.RegSet.rsGetSpillInfo(source, REG_XMM0, out _), Is.Null);
            }
        });
    }

    [TestCase(TYP_FLOAT, 0x7FFFFFFF7FFFFFFFUL)]
    [TestCase(TYP_DOUBLE, 0x7FFFFFFFFFFFFFFFUL)]
    public static void AbsoluteValueDispatchUsesTheNativeSignMask(var_types type, ulong mask)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var source = compiler.gtNewDconNode(type, -0.0);
            source.RegNum = REG_XMM0;
            var tree = new GenTreeIntrinsic(type, source, NI_System_Math_Abs, null) { RegNum = REG_XMM0 };

            codeGen.genIntrinsic(tree);

            Assert.That(Descriptors(codeGen)[^1].idIns(), Is.EqualTo(INS_andps));
            var data = codeGen.Emitter.emitConsDsc.dsdLast ??
                throw new AssertionException("Missing sign-mask constant.");
            Assert.That(BitConverter.ToUInt64(data.Data, 0), Is.EqualTo(mask));
            Assert.That(BitConverter.ToUInt64(data.Data, 8), Is.EqualTo(mask));
        });
    }

    [Test]
    public static void ImmediateRmwInstructionsSwapAliasedCommutativeSources()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            codeGen.Emitter.UseVexEncodings = false;
            codeGen.Emitter.UseEvexEncodings = false;
            var source = compiler.gtNewDconNode(TYP_DOUBLE, 1.0);
            source.RegNum = REG_XMM1;

            codeGen.inst_RV_RV_TT_IV(INS_dppd, EA_16BYTE, REG_XMM1, REG_XMM0,
                source, 0x31, isRMW: true, INS_OPTS_NONE);

            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(1));
            Assert.That(Descriptors(codeGen)[0].idReg1(), Is.EqualTo(REG_XMM1));
            Assert.That(Descriptors(codeGen)[0].idReg2(), Is.EqualTo(REG_XMM0));
            Assert.That(InstructionConstant(codeGen.Emitter, Descriptors(codeGen)[0]), Is.EqualTo((nint)0x31));
        });
    }

    [TestCase(INS_vinsertf64x2, INS_vinsertf32x4)]
    [TestCase(INS_vinserti64x2, INS_vinserti32x4)]
    public static void ImmediateOperandsKeepTheNativeEncodingFallback(instruction selected, instruction expected)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            EnableAvx2(compiler);
            codeGen.Emitter.UseVexEncodings = true;
            codeGen.Emitter.UseEvexEncodings = true;
            var source = new GenTreePhysReg(REG_XMM2, TYP_SIMD16) { RegNum = REG_XMM2 };

            codeGen.inst_RV_RV_TT_IV(selected, EA_32BYTE, REG_XMM0, REG_XMM1,
                source, 1, isRMW: false, INS_OPTS_NONE);

            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(1));
            Assert.That(Descriptors(codeGen)[0].idIns(), Is.EqualTo(expected));
            Assert.That(InstructionConstant(codeGen.Emitter, Descriptors(codeGen)[0]), Is.EqualTo((nint)1));
        });
    }

#if DEBUG
    [Test]
    public static void ImmediateOperandGuardsPrecedeConstantAllocation()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var source = compiler.gtNewDconNode(TYP_DOUBLE, -0.0);
            source.IsContained = true;
            compiler.opts.dspCode = true;
            _ = Assert.Throws<FatalJitException>(() => codeGen.inst_RV_RV_TT_IV(
                INS_roundsd, EA_8BYTE, REG_XMM0, REG_XMM0, source, 4, isRMW: true, INS_OPTS_NONE));
            _ = Assert.Throws<FatalJitException>(() => codeGen.Emitter.emitIns_SIMD_R_R_C_I(
                INS_roundsd, EA_8BYTE, REG_XMM0, REG_XMM0, null, 0, 4, INS_OPTS_NONE));
            Assert.That(Descriptors(codeGen), Is.Empty);
            Assert.That(codeGen.Emitter.emitConsDsc.dsdLast, Is.Null);
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public static void DisassemblyRejectsBeforeFiniteOrIntrinsicConsumption(bool finite)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var source = compiler.gtNewDconNode(TYP_DOUBLE, 1.0);
            source.RegNum = REG_XMM0;
            GenTree tree = finite ? compiler.gtNewUnaryNode(GT_CKFINITE, TYP_DOUBLE, source)
                : new GenTreeIntrinsic(TYP_DOUBLE, source, NI_System_Math_Round, null);
            tree.RegNum = REG_XMM1;
            codeGen.InternalRegisters.Add(tree, RBM_R11);
            void Generate()
            {
                if (finite)
                {
                    codeGen.genCkfinite(tree);
                }
                else
                {
                    codeGen.genIntrinsic(tree.AsIntrinsic());
                }
            }

            compiler.opts.dspCode = true;
            _ = Assert.Throws<FatalJitException>(Generate);
            Assert.That(Descriptors(codeGen), Is.Empty);
            compiler.opts.dspCode = false;
            _ = CodeGenBinaryTests.PrepareThrowTarget(compiler, SCK_ARITH_EXCPN);
            Generate();
            Assert.That(Descriptors(codeGen), Is.Not.Empty);
        });
    }
#endif
}
