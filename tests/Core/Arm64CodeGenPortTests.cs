// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static RyuJitSharp.ManagedInstructionSet;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.insCond;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class Arm64CodeGenPortTests
{
    [TestCase(NI_System_Math_Abs, INS_fabs)]
    [TestCase(NI_System_Math_Ceiling, INS_frintp)]
    [TestCase(NI_System_Math_Floor, INS_frintm)]
    [TestCase(NI_System_Math_Truncate, INS_frintz)]
    [TestCase(NI_System_Math_Round, INS_frintn)]
    [TestCase(NI_System_Math_Sqrt, INS_fsqrt)]
    public static void ScalarMathIntrinsicsRetainInstructionAndRegisters(
        NamedIntrinsic intrinsic, instruction expected)
    {
        foreach (var type in new[] { TYP_FLOAT, TYP_DOUBLE })
        {
            Arm64CodeGenLocalVariableTests.WithCodeGen((_, codeGen) =>
            {
                var source = new GenTreePhysReg(REG_ARM64_V0, type) { RegNum = REG_ARM64_V0 };
                var tree = new GenTreeIntrinsic(type, source, intrinsic, methodHandle: null)
                {
                    RegNum = REG_ARM64_V1,
                };

                codeGen.genIntrinsic(tree);

                var descriptor = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter).Single();
                Assert.That(descriptor.idIns(), Is.EqualTo(expected));
                Assert.That(descriptor.idOpSize(), Is.EqualTo(type.EmitActualSize));
                Assert.That(descriptor.idReg1(), Is.EqualTo(REG_ARM64_V1));
                Assert.That(descriptor.idReg2(), Is.EqualTo(REG_ARM64_V0));
            });
        }
    }

    [TestCase(TYP_INT, EA_4BYTE, 5)]
    [TestCase(TYP_LONG, EA_8BYTE, 4)]
    public static void PopCountFallbackRetainsVectorReductionSequence(
        var_types sourceType, emitAttr sourceSize, int instructionCount)
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((_, codeGen) =>
        {
            var source = new GenTreePhysReg(REG_ARM64_R1, sourceType) { RegNum = REG_ARM64_R1 };
            var tree = new GenTreeIntrinsic(TYP_INT, source, NI_PRIMITIVE_PopCount, methodHandle: null)
            {
                RegNum = REG_ARM64_R0,
            };
            codeGen.InternalRegisters.Add(tree,
                regMaskTP.CreateFromRegNum(REG_ARM64_V0, REG_ARM64_V0.SingleTypeMask));

            codeGen.genIntrinsic(tree);

            var descriptors = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter);
            Assert.That(descriptors.Select(descriptor => descriptor.idIns()),
                Is.EqualTo(sourceSize == EA_4BYTE
                    ? (instruction[])[INS_movi, INS_ins, INS_cnt, INS_addv, INS_umov]
                    : (instruction[])[INS_ins, INS_cnt, INS_addv, INS_umov]));
            Assert.That(descriptors, Has.Count.EqualTo(instructionCount));
            Assert.That(descriptors[^1].idOpSize(), Is.EqualTo(sourceSize));
            Assert.That(descriptors[^1].idReg1(), Is.EqualTo(REG_ARM64_R0));
            Assert.That(descriptors[^1].idReg2(), Is.EqualTo(REG_ARM64_V0));
        });
    }

    [TestCase(NI_PRIMITIVE_PopCount, INS_cnt)]
    [TestCase(NI_PRIMITIVE_TrailingZeroCount, INS_ctz)]
    public static void CsscScalarIntrinsicsUseGeneralRegisterInstruction(
        NamedIntrinsic intrinsic, instruction expected)
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_Cssc);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_Cssc);
            compiler.opts.setSupportedISAs(compiler.opts.compSupportsISAExactly);
            var source = new GenTreePhysReg(REG_ARM64_R1, TYP_INT) { RegNum = REG_ARM64_R1 };
            var tree = new GenTreeIntrinsic(TYP_INT, source, intrinsic, methodHandle: null)
            {
                RegNum = REG_ARM64_R0,
            };

            codeGen.genIntrinsic(tree);

            var descriptor = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter).Single();
            Assert.That(descriptor.idIns(), Is.EqualTo(expected));
            Assert.That(descriptor.idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_ARM64_R0));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_ARM64_R1));
        });
    }

    [Test]
    public static void TrailingZeroCountFallbackReversesBitsBeforeCountingLeadingZeros()
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((_, codeGen) =>
        {
            var source = new GenTreePhysReg(REG_ARM64_R1, TYP_LONG) { RegNum = REG_ARM64_R1 };
            var tree = new GenTreeIntrinsic(TYP_LONG, source, NI_PRIMITIVE_TrailingZeroCount,
                methodHandle: null)
            {
                RegNum = REG_ARM64_R0,
            };

            codeGen.genIntrinsic(tree);

            var descriptors = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter);
            Assert.That(descriptors.Select(descriptor => descriptor.idIns()),
                Is.EqualTo((instruction[])[INS_rbit, INS_clz]));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_8BYTE));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_ARM64_R0));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_ARM64_R1));
            Assert.That(descriptors[1].idReg1(), Is.EqualTo(REG_ARM64_R0));
            Assert.That(descriptors[1].idReg2(), Is.EqualTo(REG_ARM64_R0));
        });
    }

    [TestCase(GT_SELECTCC, INS_csel)]
    [TestCase(GT_SELECT_INVCC, INS_csinv)]
    [TestCase(GT_SELECT_NEGCC, INS_csneg)]
    [TestCase(GT_SELECT_INCCC, INS_csinc)]
    public static void ConditionalSelectVariantsRetainTheirInstruction(genTreeOps oper, instruction expected)
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            var tree = new GenTreeOpCC(oper, TYP_INT, new GenCondition(GenCondition.CodeKind.NE),
                Register(compiler, REG_ARM64_R0), Register(compiler, REG_ARM64_R1))
            {
                RegNum = REG_ARM64_R2,
            };

            codeGen.genCodeForSelect(tree);

            var descriptor = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter).Single();
            Assert.That(descriptor.idIns(), Is.EqualTo(expected));
            Assert.That((insCond)(uint)descriptor.idSmallCns(), Is.EqualTo(INS_COND_NE));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_ARM64_R2));
        });
    }

    [TestCase(GenCondition.CodeKind.FNE, INS_COND_GT, INS_COND_LO, REG_ARM64_R2, REG_ARM64_R1)]
    [TestCase(GenCondition.CodeKind.FEQU, INS_COND_EQ, INS_COND_VS, REG_ARM64_R0, REG_ARM64_R2)]
    public static void CompoundFloatingConditionsRetainTheSecondSelect(
        GenCondition.CodeKind condition, insCond firstCondition, insCond secondCondition,
        regNumber secondSource1, regNumber secondSource2)
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            var tree = new GenTreeOpCC(GT_SELECTCC, TYP_INT, new GenCondition(condition),
                Register(compiler, REG_ARM64_R0), Register(compiler, REG_ARM64_R1))
            {
                RegNum = REG_ARM64_R2,
            };

            codeGen.genCodeForSelect(tree);

            var descriptors = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter);
            Assert.That(descriptors.Select(descriptor => descriptor.idIns()),
                Is.EqualTo((instruction[])[INS_csel, INS_csel]));
            Assert.That((insCond)(uint)descriptors[0].idSmallCns(), Is.EqualTo(firstCondition));
            Assert.That((insCond)(uint)descriptors[1].idSmallCns(), Is.EqualTo(secondCondition));
            Assert.That(descriptors[1].idReg2(), Is.EqualTo(secondSource1));
            Assert.That(descriptors[1].idReg3(), Is.EqualTo(secondSource2));
        });
    }

    [TestCaseSource(nameof(IndexAddressCases))]
    public static void IndexAddressesPreserveWordWideningScaleAndElementOffset(
        var_types indexType, int elementSize, insOpts scaleOption, int scale)
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            var baseAddress = compiler.gtNewIconNode(TYP_REF, 0);
            baseAddress.RegNum = REG_ARM64_R1;
            var index = compiler.gtNewIconNode(indexType, 0);
            index.RegNum = REG_ARM64_R2;
            var tree = new GenTreeIndexAddr(
                baseAddress, index, TYP_INT, NO_CLASS_HANDLE, elementSize, 8, 16, boundsCheck: false)
            {
                RegNum = REG_ARM64_R0,
            };
            codeGen.InternalRegisters.Add(tree,
                regMaskTP.CreateFromRegNum(REG_ARM64_R3, REG_ARM64_R3.SingleTypeMask));
            codeGen.GCInfo.gcMarkRegPtrVal(REG_ARM64_R1, TYP_REF);

            codeGen.genCodeForTreeNode(tree);

            var descriptors = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_add));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_8BYTE));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_ARM64_R0));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_ARM64_R1));
            Assert.That(descriptors[0].idReg3(), Is.EqualTo(REG_ARM64_R2));
            Assert.That(descriptors[0].idInsOpt(), Is.EqualTo(scaleOption));
            Assert.That(descriptors[0].idSmallCns(), Is.EqualTo(scale));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(INS_add));
            Assert.That(descriptors[1].idSmallCns(), Is.EqualTo(16));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur,
                Is.EqualTo(regMaskTP.CreateFromRegNum(REG_ARM64_R0, REG_ARM64_R0.SingleTypeMask)));
        });
    }

    private static IEnumerable<TestCaseData> IndexAddressCases()
    {
        yield return new TestCaseData(TYP_INT, 4, INS_OPTS_UXTW, 2);
        yield return new TestCaseData(TYP_I_IMPL, 8, INS_OPTS_LSL, 3);
    }

    [Test]
    public static void IndexAddressesWidenWordIndicesBeforeMultiplyAdd()
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            var baseAddress = compiler.gtNewIconNode(TYP_REF, 0);
            baseAddress.RegNum = REG_ARM64_R1;
            var index = compiler.gtNewIconNode(TYP_INT, 0);
            index.RegNum = REG_ARM64_R2;
            var tree = new GenTreeIndexAddr(
                baseAddress, index, TYP_INT, NO_CLASS_HANDLE, 3, 8, 16, boundsCheck: false)
            {
                RegNum = REG_ARM64_R0,
            };
            var internalRegisters =
                regMaskTP.CreateFromRegNum(REG_ARM64_R3, REG_ARM64_R3.SingleTypeMask) |
                regMaskTP.CreateFromRegNum(REG_ARM64_R4, REG_ARM64_R4.SingleTypeMask);
            codeGen.InternalRegisters.Add(tree, internalRegisters);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_ARM64_R1, TYP_REF);

            codeGen.genCodeForTreeNode(tree);

            var descriptors = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter);
            Assert.That(descriptors.ConvertAll(static descriptor => descriptor.idIns()),
                Is.EqualTo((instruction[])[INS_mov, INS_mov, INS_madd, INS_add]));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_ARM64_R4));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_ARM64_R2));
            Assert.That(descriptors[2].idReg1(), Is.EqualTo(REG_ARM64_R0));
            Assert.That(descriptors[2].idReg2(), Is.EqualTo(REG_ARM64_R4));
            Assert.That(descriptors[2].idReg3(), Is.EqualTo(REG_ARM64_R3));
            Assert.That(descriptors[2].idReg4(), Is.EqualTo(REG_ARM64_R1));
        });
    }

    [Test]
    public static void LeaInstructionsUseScaledAddAndOffset()
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            var baseAddress = compiler.gtNewIconNode(TYP_REF, 0);
            baseAddress.RegNum = REG_ARM64_R1;
            var index = compiler.gtNewIconNode(TYP_I_IMPL, 0);
            index.RegNum = REG_ARM64_R2;
            var tree = new GenTreeAddrMode(TYP_BYREF, baseAddress, index, 4, 8)
            {
                RegNum = REG_ARM64_R0,
            };
            codeGen.InternalRegisters.Add(tree,
                regMaskTP.CreateFromRegNum(REG_ARM64_R3, REG_ARM64_R3.SingleTypeMask));

            codeGen.genCodeForTreeNode(tree);

            var descriptors = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter);
            Assert.That(descriptors.ConvertAll(static descriptor => descriptor.idIns()),
                Is.EqualTo((instruction[])[INS_add, INS_add]));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_ARM64_R3));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_ARM64_R1));
            Assert.That(descriptors[0].idReg3(), Is.EqualTo(REG_ARM64_R2));
            Assert.That(descriptors[0].idInsOpt(), Is.EqualTo(INS_OPTS_LSL));
            Assert.That(descriptors[0].idSmallCns(), Is.EqualTo(2));
            Assert.That(descriptors[1].idReg1(), Is.EqualTo(REG_ARM64_R0));
            Assert.That(descriptors[1].idReg2(), Is.EqualTo(REG_ARM64_R3));
            Assert.That(descriptors[1].idSmallCns(), Is.EqualTo(8));
        });
    }

    [TestCase(false, INS_OPTS_SXTW)]
    [TestCase(true, INS_OPTS_UXTW)]
    public static void LeaInstructionExtendsContainedCastIndex(bool isUnsigned, insOpts expectedExtend)
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            var baseAddress = compiler.gtNewIconNode(TYP_REF, 0);
            baseAddress.RegNum = REG_ARM64_R1;
            var source = compiler.gtNewIconNode(TYP_INT, 0);
            source.RegNum = REG_ARM64_R2;
            var index = new GenTreeCast(TYP_I_IMPL, source, isUnsigned, TYP_I_IMPL)
            {
                IsContained = true,
            };
            var tree = new GenTreeAddrMode(TYP_BYREF, baseAddress, index, 4, 0)
            {
                RegNum = REG_ARM64_R0,
            };

            codeGen.genCodeForTreeNode(tree);

            var descriptor = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter).Single();
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_add));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_ARM64_R0));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_ARM64_R1));
            Assert.That(descriptor.idReg3(), Is.EqualTo(REG_ARM64_R2));
            Assert.That(descriptor.idInsOpt(), Is.EqualTo(expectedExtend));
            Assert.That(descriptor.idSmallCns(), Is.EqualTo(2));
        });
    }

    [TestCase(false, INS_OPTS_SXTW)]
    [TestCase(true, INS_OPTS_UXTW)]
    public static void LeaInstructionExtendsContainedBitfieldInsertIndex(bool isUnsigned, insOpts expectedExtend)
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            var baseAddress = compiler.gtNewIconNode(TYP_REF, 0);
            baseAddress.RegNum = REG_ARM64_R1;
            var source = compiler.gtNewIconNode(TYP_INT, 0);
            source.RegNum = REG_ARM64_R2;
            var cast = new GenTreeCast(TYP_I_IMPL, source, isUnsigned, TYP_I_IMPL)
            {
                IsContained = true,
            };
            var shift = compiler.gtNewIconNode(TYP_INT, 2);
            var index = new GenTreeOp(GT_BFIZ, TYP_I_IMPL, cast, shift)
            {
                IsContained = true,
            };
            var tree = new GenTreeAddrMode(TYP_BYREF, baseAddress, index, 1, 0)
            {
                RegNum = REG_ARM64_R0,
            };

            codeGen.genCodeForTreeNode(tree);

            var descriptor = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter).Single();
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_add));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_ARM64_R0));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_ARM64_R1));
            Assert.That(descriptor.idReg3(), Is.EqualTo(REG_ARM64_R2));
            Assert.That(descriptor.idInsOpt(), Is.EqualTo(expectedExtend));
            Assert.That(descriptor.idSmallCns(), Is.EqualTo(2));
        });
    }

    [Test]
    public static void UnknownSizeFrameUsesAddVlAtTheImmediateLimit()
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compLocallocUsed = true;
            compiler.compUsesUnknownSizeFrame = true;
            compiler.unkSizeFrame.nVector = 32;
            compiler.unkSizeFrame.FinalizeLayout();

            codeGen.genUnknownSizeFrame();

            var descriptors = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter);
            Assert.That(descriptors.ConvertAll(static descriptor => descriptor.idIns()),
                Is.EqualTo((instruction[])[INS_mov, INS_sve_addvl]));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_UNKBASE));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_ARM64_ZR));
            Assert.That(descriptors[1].idReg1(), Is.EqualTo(REG_ARM64_ZR));
            Assert.That(descriptors[1].idReg2(), Is.EqualTo(REG_ARM64_ZR));
            Assert.That(Emitter.emitGetInsSC(descriptors[1]), Is.EqualTo((nint)(-32)));
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [NonParallelizable]
    public static void UnknownSizeFrameEmissionRejectsUnsupportedRuntimeConfiguration(bool arm64, bool simd)
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((_, codeGen) =>
        {
            const string targetSwitch = "RyuJitSharp.TARGET_ARM64";
            const string featureSwitch = "RyuJitSharp.FEATURE_SIMD";
            Assert.That(AppContext.TryGetSwitch(targetSwitch, out var previousTarget), Is.True);
            Assert.That(AppContext.TryGetSwitch(featureSwitch, out var previousFeature), Is.True);
            AppContext.SetSwitch(targetSwitch, arm64);
            AppContext.SetSwitch(featureSwitch, simd);

            try
            {
                var frameFailure = Assert.Throws<FatalJitException>(() => codeGen.genUnknownSizeFrame());
                var zeroFailure = Assert.Throws<FatalJitException>(() => codeGen.genZeroInitializeUnknownSizeFrame());

                Assert.That(frameFailure?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
                Assert.That(zeroFailure?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
            }
            finally
            {
                AppContext.SetSwitch(targetSwitch, previousTarget);
                AppContext.SetSwitch(featureSwitch, previousFeature);
            }

            Assert.That(Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter), Is.Empty);
        });
    }

#if !DEBUG
    // The pinned emitter's DEBUG assertions reject SP operands for MSub.
    [Test]
    public static void UnknownSizeFrameUsesRdvlAndMsubAboveTheImmediateLimit()
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compLocallocUsed = true;
            compiler.compUsesUnknownSizeFrame = true;
            compiler.unkSizeFrame.nVector = 33;
            compiler.unkSizeFrame.FinalizeLayout();

            codeGen.genUnknownSizeFrame();

            var descriptors = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter);
            var rdvl = descriptors[^2];
            var msub = descriptors[^1];
            Assert.That(rdvl.idIns(), Is.EqualTo(INS_sve_rdvl));
            Assert.That(rdvl.idReg1(), Is.EqualTo(REG_SCRATCH));
            Assert.That(Emitter.emitGetInsSC(rdvl), Is.EqualTo((nint)1));
            Assert.That(msub.idIns(), Is.EqualTo(INS_msub));
            Assert.That(msub.idReg1(), Is.EqualTo(REG_ARM64_SP));
            Assert.That(msub.idReg2(), Is.EqualTo(codeGen.rsGetRsvdReg()));
            Assert.That(msub.idReg3(), Is.EqualTo(REG_SCRATCH));
            Assert.That(msub.idReg4(), Is.EqualTo(REG_ARM64_SP));
        });
    }

    [TestCase(33u)]
    [TestCase(0x10000u)]
    [TestCase(0xfffffu)]
    [TestCase(0x12345678u)]
    [TestCase(0xffff0000u)]
    [TestCase(uint.MaxValue)]
    public static void UnknownSizeFrameCountMaterializationMatchesTheArm64Emitter(uint count)
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compLocallocUsed = true;
            compiler.compUsesUnknownSizeFrame = true;
            compiler.unkSizeFrame.nVector = count;
            compiler.unkSizeFrame.FinalizeLayout();
            codeGen.genUnknownSizeFrame();

            var frame = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter);
            var reserved = codeGen.rsGetRsvdReg();
            Arm64CodeGenLocalVariableTests.WithCodeGen((_, referenceCodeGen) =>
            {
                referenceCodeGen.instGen_Set_Reg_To_Imm(EA_8BYTE, reserved, (nint)count);
                var reference = Arm64CodeGenLocalVariableTests.Descriptors(referenceCodeGen.Emitter);
                Assert.That(frame, Has.Count.EqualTo(reference.Count + 3));
                for (var index = 0; index < reference.Count; index++)
                {
                    var actual = frame[index + 1];
                    var expected = reference[index];
                    Assert.That(actual.idIns(), Is.EqualTo(expected.idIns()));
                    Assert.That(actual.idInsFmt(), Is.EqualTo(expected.idInsFmt()));
                    Assert.That(actual.idOpSize(), Is.EqualTo(expected.idOpSize()));
                    Assert.That(actual.idInsOpt(), Is.EqualTo(expected.idInsOpt()));
                    Assert.That(actual.idReg1(), Is.EqualTo(expected.idReg1()));
                    Assert.That(actual.idIsSmallDsc(), Is.EqualTo(expected.idIsSmallDsc()));
                    Assert.That(Emitter.emitGetInsSC(actual), Is.EqualTo(Emitter.emitGetInsSC(expected)));
                }
            });
        });
    }
#endif

    [Test]
    public static void UnknownSizeFrameInitializationUnrollsFiveVectorStores()
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compUsesUnknownSizeFrame = true;
            compiler.unkSizeFrame.nVector = 5;
            compiler.unkSizeFrame.FinalizeLayout();

            codeGen.genZeroInitializeUnknownSizeFrame();

            var descriptors = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter);
            Assert.That(descriptors.ConvertAll(static descriptor => descriptor.idIns()),
                Is.EqualTo((instruction[])
                [
                    INS_sve_mov, INS_sve_str, INS_sve_str, INS_sve_str, INS_sve_str, INS_sve_str,
                ]));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_SCRATCH_V));
            Assert.That(descriptors[0].idInsOpt(), Is.EqualTo(INS_OPTS_SCALABLE_B));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_SCALABLE));
            Assert.That(descriptors[0].idHasShift(), Is.False);

            for (var index = 0; index < 5; index++)
            {
                var descriptor = descriptors[index + 1];
                Assert.That(descriptor.idReg1(), Is.EqualTo(REG_SCRATCH_V));
                Assert.That(descriptor.idReg2(), Is.EqualTo(REG_ARM64_ZR));
                Assert.That(descriptor.idOpSize(), Is.EqualTo(EA_SCALABLE));
                Assert.That(descriptor.idHasShift(), Is.False);
                Assert.That(Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)index));
            }
        });
    }

    [Test]
    public static void UnknownSizeFrameInitializationUsesLoopAboveFiveVectors()
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compCurBB = new BasicBlock(null, null);
            compiler.compUsesUnknownSizeFrame = true;
            compiler.unkSizeFrame.nVector = 6;
            compiler.unkSizeFrame.FinalizeLayout();

            codeGen.genZeroInitializeUnknownSizeFrame();

            var descriptors = Arm64CodeGenLocalVariableTests.AllDescriptors(codeGen);
            Assert.That(descriptors.ConvertAll(static descriptor => descriptor.idIns()),
                Is.EqualTo((instruction[])[INS_sve_mov, INS_mov, INS_sve_addvl, INS_sve_str, INS_cmp, INS_bne]));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_SCRATCH_V));
            Assert.That(descriptors[0].idInsOpt(), Is.EqualTo(INS_OPTS_SCALABLE_B));
            Assert.That(descriptors[1].idReg1(), Is.EqualTo(REG_SCRATCH));
            Assert.That(descriptors[1].idReg2(), Is.EqualTo(REG_UNKBASE));
            Assert.That(descriptors[1].idGCref(), Is.EqualTo(GCInfo.GCtype.GCT_BYREF));
            Assert.That(descriptors[2].idReg1(), Is.EqualTo(REG_SCRATCH));
            Assert.That(descriptors[2].idReg2(), Is.EqualTo(REG_SCRATCH));
            Assert.That(Emitter.emitGetInsSC(descriptors[2]), Is.EqualTo((nint)(-1)));
            Assert.That(descriptors[3].idReg1(), Is.EqualTo(REG_SCRATCH_V));
            Assert.That(descriptors[3].idReg2(), Is.EqualTo(REG_SCRATCH));
            Assert.That(descriptors[4].idReg1(), Is.EqualTo(REG_ARM64_ZR));
            Assert.That(descriptors[4].idReg2(), Is.EqualTo(REG_SCRATCH));
            Assert.That(descriptors[4].idInsOpt(), Is.EqualTo(INS_OPTS_UXTX));
            Assert.That(descriptors[4].idInsFmt(), Is.EqualTo(Emitter.insFormat.IF_ARM64_DR_2C));
            var branch = (Emitter.instrDescJmp)descriptors[5];
            Assert.That(branch.idInsFmt(), Is.EqualTo(Emitter.insFormat.IF_ARM64_LARGEJMP));
            Assert.That(branch.idCodeSize(), Is.EqualTo(8));
            Assert.That(branch.idjShort, Is.False);
            Assert.That(branch.idjTarget?.bbEmitCookie, Is.Not.Null);
            Assert.That(branch.idjIG, Is.Not.Null);
        });
    }

    private static GenTreeIntCon Register(Compiler compiler, regNumber reg)
    {
        var node = compiler.gtNewIconNode(TYP_INT, 7);
        node.RegNum = reg;

        return node;
    }
}
#endif
