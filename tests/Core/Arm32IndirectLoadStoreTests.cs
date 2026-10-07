// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BarrierKind;
using static RyuJitSharp.Globals;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.insBarrier;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.SpecialCodeKind;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class Arm32IndirectLoadStoreTests
{
    [Test]
    public static void ContainedIndexedLoadUsesScaledBaseAndIndex()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            var baseAddress = compiler.gtNewLclvNode(TYP_BYREF, 0);
            baseAddress.RegNum = REG_R1;
            var index = compiler.gtNewLclvNode(TYP_INT, 0);
            index.RegNum = REG_R2;
            var address = new GenTreeAddrMode(TYP_BYREF, baseAddress, index, 4, 0) { IsContained = true };
            var indir = new GenTreeIndir(GT_IND, TYP_INT, address);

            RecordArm32Instructions(() =>
                EmitLoadStoreOp(codeGen.Emitter, INS_ldr, EA_4BYTE, REG_R0, indir));

            var descriptor = LastInstruction(codeGen.Emitter);
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_ldr));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R0));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_R1));
            Assert.That(descriptor.idReg3(), Is.EqualTo(REG_R2));
            Assert.That(codeGen.Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)2));
        });
    }

    [Test]
    public static void ContainedBaseOffsetStoreUsesLoadStoreImmediate()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            var baseAddress = compiler.gtNewLclvNode(TYP_BYREF, 0);
            baseAddress.RegNum = REG_R1;
            var address = new GenTreeAddrMode(TYP_BYREF, baseAddress, null, 0, 8) { IsContained = true };
            var data = compiler.gtNewLclvNode(TYP_INT, 0);
            data.RegNum = REG_R3;
            var store = new GenTreeStoreInd(TYP_INT, address, data);

            RecordArm32Instructions(() =>
                EmitLoadStoreOp(codeGen.Emitter, INS_str, EA_4BYTE, REG_R3, store));

            var descriptor = LastInstruction(codeGen.Emitter);
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_str));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R3));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_R1));
            Assert.That(codeGen.Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)8));
        });
    }

    [TestCase(TYP_INT, 4, REG_R3, INS_ldr, EA_4BYTE)]
    [TestCase(TYP_FLOAT, 4, REG_F0, INS_vldr, EA_4BYTE)]
    [TestCase(TYP_DOUBLE, 8, REG_F0, INS_vldr, EA_8BYTE)]
    public static void AlignedLocalFieldLoadsUseTheLocalAndFieldOffset(
        var_types type, int offset, regNumber targetReg, instruction loadInstruction, emitAttr size)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            InitializeStackLocal(compiler, codeGen);
            var tree = new GenTreeLclFld(GT_LCL_FLD, type, 0, (ushort)offset)
            {
                RegNum = targetReg,
            };

            RecordArm32Instructions(() => codeGen.genCodeForLclFld(tree));

            var descriptor = LastInstruction(codeGen.Emitter);
            Assert.That(descriptor.idIns(), Is.EqualTo(loadInstruction));
            Assert.That(descriptor.idOpSize(), Is.EqualTo(size));
            Assert.That(descriptor.idReg1(), Is.EqualTo(targetReg));
            Assert.That(descriptor.idIsLclVar(), Is.True);
            Assert.That(descriptor.idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
            Assert.That(descriptor.idAddr().iiaLclVar.lvaOffset(), Is.EqualTo((uint)offset));
        });
    }

    [Test]
    public static void StructLocalFieldLoadsRetainTheUnsupportedBoundary()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            var tree = new GenTreeLclFld(GT_LCL_FLD, TYP_STRUCT, 0, 0, new ClassLayout(8));

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForLclFld(tree));

            Assert.That(failure?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
        });
    }

    [Test]
    public static void LocalVariableLoadsUseTheirAssignedRegisterAndStackHome()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            InitializeStackLocal(compiler, codeGen);
            var tree = compiler.gtNewLclvNode(TYP_INT, 0);
            tree.RegNum = REG_R0;

            codeGen.genCodeForLclVar(tree);

            var descriptor = LastInstruction(codeGen.Emitter);
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_ldr));
            Assert.That(descriptor.idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R0));
            Assert.That(descriptor.idIsLclVar(), Is.True);
            Assert.That(descriptor.idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
            Assert.That(descriptor.idAddr().iiaLclVar.lvaOffset(), Is.Zero);
        });
    }

    [Test]
    public static void LocalFieldStoresUseTheFieldOffsetAndSourceRegister()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            InitializeStackLocal(compiler, codeGen);
            compiler.lvaTable[0].Type = TYP_LONG;
            var value = compiler.gtNewIconNode(TYP_INT, 7);
            value.RegNum = REG_R1;
            var tree = compiler.gtNewStoreLclFldNode(TYP_INT, 0, 4, value);

            codeGen.genCodeForStoreLclFld(tree);

            var descriptor = LastInstruction(codeGen.Emitter);
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_str));
            Assert.That(descriptor.idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R1));
            Assert.That(descriptor.idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
            Assert.That(descriptor.idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(4u));
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
        });
    }

    [Test]
    public static void LocalVariableStoresUseTheStackHome()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            InitializeStackLocal(compiler, codeGen);
            var value = compiler.gtNewIconNode(TYP_INT, 7);
            value.RegNum = REG_R1;
            var tree = compiler.gtNewStoreLclVarNode(0, value);
            tree.RegNum = REG_NA;

            codeGen.genCodeForStoreLclVar(tree);

            var descriptor = LastInstruction(codeGen.Emitter);
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_str));
            Assert.That(descriptor.idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R1));
            Assert.That(descriptor.idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
            Assert.That(descriptor.idAddr().iiaLclVar.lvaOffset(), Is.Zero);
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
        });
    }

    [Test]
    public static void LongLocalStoresWriteBothWordsAtTheirNativeOffsets()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            InitializeStackLocal(compiler, codeGen);
            compiler.lvaTable[0].Type = TYP_LONG;
            var low = compiler.gtNewIconNode(TYP_INT, 7);
            low.RegNum = REG_R1;
            var high = compiler.gtNewIconNode(TYP_INT, 9);
            high.RegNum = REG_R2;
            var value = new GenTreeOp(GT_LONG, TYP_LONG, low, high);
            var tree = compiler.gtNewStoreLclVarNode(0, value);
            tree.RegNum = REG_NA;

            codeGen.genCodeForStoreLclVar(tree);

            var descriptors = CurrentDescriptors(codeGen.Emitter)
                ?? throw new AssertionException("Missing descriptor buffer.");
            Assert.That(descriptors.ConvertAll(static descriptor => descriptor.idIns()),
                Is.EqualTo((instruction[])[INS_str, INS_str]));
            Assert.That(descriptors.ConvertAll(static descriptor => descriptor.idReg1()),
                Is.EqualTo((regNumber[])[REG_R1, REG_R2]));
            Assert.That(descriptors.ConvertAll(static descriptor => descriptor.idAddr().iiaLclVar.lvaOffset()),
                Is.EqualTo((uint[])[0, 4]));
        });
    }

    [Test]
    public static void IntegerDivisionUsesTheArmSignedDivideInstruction()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            var dividend = compiler.gtNewIconNode(TYP_INT, 7);
            dividend.RegNum = REG_R1;
            var divisor = compiler.gtNewIconNode(TYP_INT, 3);
            divisor.RegNum = REG_R2;
            var tree = new GenTreeOp(GT_DIV, TYP_INT, dividend, divisor)
            {
                RegNum = REG_R0,
            };

            codeGen.genCodeForDivMod(tree);

            var descriptor = LastInstruction(codeGen.Emitter);
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_sdiv));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R0));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_R1));
            Assert.That(descriptor.idReg3(), Is.EqualTo(REG_R2));
        });
    }

    [Test]
    public static void IndexAddressUsesScaledAddAndElementOffset()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            var tree = CreateIndexAddress(compiler, codeGen, elementSize: 4, boundsCheck: false);
            var initialCount = CurrentDescriptors(codeGen.Emitter)?.Count ?? 0;

            RecordArm32Instructions(() => codeGen.genCodeForTreeNode(tree));

            var descriptors = CurrentDescriptors(codeGen.Emitter)
                ?? throw new AssertionException("Missing descriptor buffer.");
            var emitted = descriptors.GetRange(initialCount, descriptors.Count - initialCount);
            Assert.That(emitted.ConvertAll(static descriptor => descriptor.idIns()), Is.EqualTo(
                (instruction[])[INS_add, INS_add]));
            Assert.That(codeGen.Emitter.emitGetInsSC(emitted[0]), Is.EqualTo((nint)2));
            Assert.That(codeGen.Emitter.emitGetInsSC(emitted[1]), Is.EqualTo((nint)12));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(new regMaskTP(SRBM_R0)));
        });
    }

    [Test]
    public static void IndexAddressUsesMultiplyAddForNonPowerOfTwoElements()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            var tree = CreateIndexAddress(compiler, codeGen, elementSize: 3, boundsCheck: false);
            var initialCount = CurrentDescriptors(codeGen.Emitter)?.Count ?? 0;

            RecordArm32Instructions(() => codeGen.genCodeForTreeNode(tree));

            var descriptors = CurrentDescriptors(codeGen.Emitter)
                ?? throw new AssertionException("Missing descriptor buffer.");
            var emitted = descriptors.GetRange(initialCount, descriptors.Count - initialCount);
            Assert.That(emitted[^2].idIns(), Is.EqualTo(INS_mla));
            Assert.That(emitted[^2].idReg1(), Is.EqualTo(REG_R0));
            Assert.That(emitted[^2].idReg2(), Is.EqualTo(REG_R2));
            Assert.That(emitted[^2].idReg3(), Is.EqualTo(REG_R3));
            Assert.That(emitted[^1].idIns(), Is.EqualTo(INS_add));
            Assert.That(codeGen.Emitter.emitGetInsSC(emitted[^1]), Is.EqualTo((nint)12));
        });
    }

    [Test]
    public static void IndexAddressChecksBoundsBeforeScaling()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            var tree = CreateIndexAddress(compiler, codeGen, elementSize: 1, boundsCheck: true);
            ConfigureBoundsCheckTarget(compiler, codeGen);
            var initialCount = CurrentDescriptors(codeGen.Emitter)?.Count ?? 0;

            RecordArm32Instructions(() => codeGen.genCodeForTreeNode(tree));

            var descriptors = CurrentDescriptors(codeGen.Emitter)
                ?? throw new AssertionException("Missing descriptor buffer.");
            var emitted = descriptors.GetRange(initialCount, descriptors.Count - initialCount);
            Assert.That(emitted[0].idIns(), Is.EqualTo(INS_ldr));
            Assert.That(emitted[1].idIns(), Is.EqualTo(INS_cmp));
            Assert.That(emitted[2].idIns(), Is.EqualTo(INS_bhs));
            Assert.That(emitted[^1].idIns(), Is.EqualTo(INS_add));
        });
    }

    [Test]
    public static void LeaInstructionUsesScaledAddAndOffset()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            var baseAddress = compiler.gtNewIconNode(TYP_REF, 0);
            baseAddress.RegNum = REG_R1;
            var index = compiler.gtNewIconNode(TYP_INT, 0);
            index.RegNum = REG_R2;
            var tree = new GenTreeAddrMode(TYP_BYREF, baseAddress, index, 4, 8)
            {
                RegNum = REG_R0,
            };
            codeGen.InternalRegisters.Add(tree, new regMaskTP(SRBM_R3));
            var initialCount = CurrentDescriptors(codeGen.Emitter)?.Count ?? 0;

            RecordArm32Instructions(() => codeGen.genCodeForTreeNode(tree));

            var descriptors = CurrentDescriptors(codeGen.Emitter)
                ?? throw new AssertionException("Missing descriptor buffer.");
            var emitted = descriptors.GetRange(initialCount, descriptors.Count - initialCount);
            Assert.That(emitted.ConvertAll(static descriptor => descriptor.idIns()),
                Is.EqualTo((instruction[])[INS_add, INS_add]));
            Assert.That(emitted[0].idReg1(), Is.EqualTo(REG_R3));
            Assert.That(codeGen.Emitter.emitGetInsSC(emitted[0]), Is.EqualTo((nint)2));
            Assert.That(emitted[1].idReg1(), Is.EqualTo(REG_R0));
            Assert.That(emitted[1].idReg2(), Is.EqualTo(REG_R3));
            Assert.That(emitted[1].idSmallCns(), Is.EqualTo(8));
        });
    }

    [Test]
    public static void LeaInstructionUsesScaledAddWithoutOffset()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            var baseAddress = compiler.gtNewIconNode(TYP_REF, 0);
            baseAddress.RegNum = REG_R1;
            var index = compiler.gtNewIconNode(TYP_INT, 0);
            index.RegNum = REG_R2;
            var tree = new GenTreeAddrMode(TYP_BYREF, baseAddress, index, 4, 0)
            {
                RegNum = REG_R0,
            };
            var initialCount = CurrentDescriptors(codeGen.Emitter)?.Count ?? 0;

            RecordArm32Instructions(() => codeGen.genCodeForTreeNode(tree));

            var descriptors = CurrentDescriptors(codeGen.Emitter)
                ?? throw new AssertionException("Missing descriptor buffer.");
            var emitted = descriptors.GetRange(initialCount, descriptors.Count - initialCount);
            Assert.That(emitted, Has.Count.EqualTo(1));
            Assert.That(emitted[0].idIns(), Is.EqualTo(INS_add));
            Assert.That(emitted[0].idReg1(), Is.EqualTo(REG_R0));
            Assert.That(emitted[0].idReg2(), Is.EqualTo(REG_R1));
            Assert.That(emitted[0].idReg3(), Is.EqualTo(REG_R2));
            Assert.That(codeGen.Emitter.emitGetInsSC(emitted[0]), Is.EqualTo((nint)2));
        });
    }

    [Test]
    public static void LeaInstructionMaterializesLargeOffsetInTemporary()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            var baseAddress = compiler.gtNewIconNode(TYP_REF, 0);
            baseAddress.RegNum = REG_R1;
            var index = compiler.gtNewIconNode(TYP_INT, 0);
            index.RegNum = REG_R2;
            var tree = new GenTreeAddrMode(TYP_BYREF, baseAddress, index, 4, 0x12345678)
            {
                RegNum = REG_R0,
            };
            codeGen.InternalRegisters.Add(tree, new regMaskTP(SRBM_R3));
            var initialCount = CurrentDescriptors(codeGen.Emitter)?.Count ?? 0;

            RecordArm32Instructions(() => codeGen.genCodeForTreeNode(tree));

            var descriptors = CurrentDescriptors(codeGen.Emitter)
                ?? throw new AssertionException("Missing descriptor buffer.");
            var emitted = descriptors.GetRange(initialCount, descriptors.Count - initialCount);
            Assert.That(emitted, Is.Not.Empty);
            Assert.That(emitted[^1].idIns(), Is.EqualTo(INS_add));
            Assert.That(emitted[^1].idReg1(), Is.EqualTo(REG_R0));
            Assert.That(emitted[^1].idReg2(), Is.EqualTo(REG_R1));
            Assert.That(emitted[^1].idReg3(), Is.EqualTo(REG_R3));
        });
    }

    [Test]
    public static void LeaInstructionMaterializesLargeBaseOffsetInTemporary()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            var baseAddress = compiler.gtNewIconNode(TYP_REF, 0);
            baseAddress.RegNum = REG_R1;
            var tree = new GenTreeAddrMode(TYP_BYREF, baseAddress, null, 0, 0x12345678)
            {
                RegNum = REG_R0,
            };
            codeGen.InternalRegisters.Add(tree, new regMaskTP(SRBM_R3));
            var initialCount = CurrentDescriptors(codeGen.Emitter)?.Count ?? 0;

            RecordArm32Instructions(() => codeGen.genCodeForTreeNode(tree));

            var descriptors = CurrentDescriptors(codeGen.Emitter)
                ?? throw new AssertionException("Missing descriptor buffer.");
            var emitted = descriptors.GetRange(initialCount, descriptors.Count - initialCount);
            Assert.That(emitted, Is.Not.Empty);
            Assert.That(emitted[^1].idIns(), Is.EqualTo(INS_add));
            Assert.That(emitted[^1].idReg1(), Is.EqualTo(REG_R0));
            Assert.That(emitted[^1].idReg2(), Is.EqualTo(REG_R1));
            Assert.That(emitted[^1].idReg3(), Is.EqualTo(REG_R3));
        });
    }

    [Test]
    public static void LeaInstructionUsesLargeSequenceForInterruptibleByref()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            var baseAddress = compiler.gtNewIconNode(TYP_REF, 0);
            baseAddress.RegNum = REG_R1;
            var index = compiler.gtNewIconNode(TYP_INT, 0);
            index.RegNum = REG_R2;
            var tree = new GenTreeAddrMode(TYP_BYREF, baseAddress, index, 4, 8)
            {
                RegNum = REG_R0,
            };
            codeGen.InternalRegisters.Add(tree, new regMaskTP(SRBM_R3));
            codeGen.Interruptible = true;
            var initialCount = CurrentDescriptors(codeGen.Emitter)?.Count ?? 0;

            RecordArm32Instructions(() => codeGen.genCodeForTreeNode(tree));

            var descriptors = CurrentDescriptors(codeGen.Emitter)
                ?? throw new AssertionException("Missing descriptor buffer.");
            var emitted = descriptors.GetRange(initialCount, descriptors.Count - initialCount);
            Assert.That(emitted, Is.Not.Empty);
            Assert.That(emitted[^1].idIns(), Is.EqualTo(INS_add));
            Assert.That(emitted[^1].idReg1(), Is.EqualTo(REG_R0));
            Assert.That(emitted[^1].idReg2(), Is.EqualTo(REG_R1));
            Assert.That(emitted[^1].idReg3(), Is.EqualTo(REG_R3));
        });
    }

    [Test]
    public static void LeaInstructionCopiesBaseForZeroOffset()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            var baseAddress = compiler.gtNewIconNode(TYP_REF, 0);
            baseAddress.RegNum = REG_R1;
            var tree = new GenTreeAddrMode(TYP_BYREF, baseAddress, null, 0, 0)
            {
                RegNum = REG_R0,
            };
            var initialCount = CurrentDescriptors(codeGen.Emitter)?.Count ?? 0;

            RecordArm32Instructions(() => codeGen.genCodeForTreeNode(tree));

            var descriptors = CurrentDescriptors(codeGen.Emitter)
                ?? throw new AssertionException("Missing descriptor buffer.");
            var emitted = descriptors.GetRange(initialCount, descriptors.Count - initialCount);
            Assert.That(emitted, Has.Count.EqualTo(1));
            Assert.That(emitted[0].idIns(), Is.EqualTo(INS_mov));
            Assert.That(emitted[0].idReg1(), Is.EqualTo(REG_R0));
            Assert.That(emitted[0].idReg2(), Is.EqualTo(REG_R1));
        });
    }

    [TestCase(TYP_FLOAT, 1, REG_F0, INS_vmov_i2f)]
    [TestCase(TYP_DOUBLE, 1, REG_F0, INS_vmov_i2d)]
    public static void MisalignedFloatingLocalFieldsLoadThroughIntegerRegisters(
        var_types type, int offset, regNumber targetReg, instruction conversion)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            InitializeStackLocal(compiler, codeGen);
            var tree = new GenTreeLclFld(GT_LCL_FLD, type, 0, (ushort)offset)
            {
                RegNum = targetReg,
            };
            var tempRegisters = type is TYP_FLOAT
                ? SRBM_R2 | SRBM_R3
                : SRBM_R2 | SRBM_R3 | SRBM_R4;
            codeGen.InternalRegisters.Add(tree, new regMaskTP(tempRegisters));

            var descriptors = CurrentDescriptors(codeGen.Emitter)
                ?? throw new AssertionException("Missing descriptor buffer.");
            var initialCount = descriptors.Count;
            RecordArm32Instructions(() => codeGen.genCodeForLclFld(tree));

            var emitted = descriptors.GetRange(initialCount, descriptors.Count - initialCount);
            Assert.That(emitted, Is.Not.Empty);
            Assert.That(emitted[0].idIns(), Is.EqualTo(INS_subw));
            Assert.That(emitted[0].idReg1(), Is.EqualTo(REG_R2));
            Assert.That(emitted[0].idReg2(), Is.EqualTo(REG_SPBASE));
            if (type is TYP_FLOAT)
            {
                Assert.That(emitted, Has.Count.EqualTo(3));
                Assert.That(emitted[1].idIns(), Is.EqualTo(INS_ldr));
                Assert.That(emitted[1].idReg1(), Is.EqualTo(REG_R3));
                Assert.That(emitted[1].idReg2(), Is.EqualTo(REG_R2));
                Assert.That(emitted[2].idIns(), Is.EqualTo(conversion));
                Assert.That(emitted[2].idReg1(), Is.EqualTo(targetReg));
                Assert.That(emitted[2].idReg2(), Is.EqualTo(REG_R3));
            }
            else
            {
                Assert.That(emitted, Has.Count.EqualTo(4));
                Assert.That(emitted[1].idIns(), Is.EqualTo(INS_ldr));
                Assert.That(emitted[1].idReg1(), Is.EqualTo(REG_R3));
                Assert.That(emitted[2].idIns(), Is.EqualTo(INS_ldr));
                Assert.That(emitted[2].idReg1(), Is.EqualTo(REG_R4));
                Assert.That(emitted[3].idIns(), Is.EqualTo(conversion));
                Assert.That(emitted[3].idReg1(), Is.EqualTo(targetReg));
                Assert.That(emitted[3].idReg2(), Is.EqualTo(REG_R3));
                Assert.That(emitted[3].idReg3(), Is.EqualTo(REG_R4));
            }
        });
    }

    [Test]
    public static void ContainedIndexedLoadWithOffsetFormsPartialAddressInTemporary()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            var baseAddress = compiler.gtNewLclvNode(TYP_BYREF, 0);
            baseAddress.RegNum = REG_R1;
            var index = compiler.gtNewLclvNode(TYP_INT, 0);
            index.RegNum = REG_R2;
            var address = new GenTreeAddrMode(TYP_BYREF, baseAddress, index, 4, 8) { IsContained = true };
            var indir = new GenTreeIndir(GT_IND, TYP_INT, address);
            codeGen.InternalRegisters.Add(indir, new regMaskTP(SRBM_R3));

            var initialCount = CurrentDescriptors(codeGen.Emitter)?.Count ?? 0;
            RecordArm32Instructions(() =>
                EmitLoadStoreOp(codeGen.Emitter, INS_ldr, EA_4BYTE, REG_R0, indir));

            var descriptors = CurrentDescriptors(codeGen.Emitter)
                ?? throw new AssertionException("Missing descriptor buffer.");
            var emittedInstructions = descriptors.GetRange(initialCount, descriptors.Count - initialCount);
            Assert.That(emittedInstructions, Has.Count.EqualTo(2));
            Assert.That(emittedInstructions[0].idIns(), Is.EqualTo(INS_add));
            Assert.That(emittedInstructions[0].idReg1(), Is.EqualTo(REG_R3));
            Assert.That(emittedInstructions[0].idReg2(), Is.EqualTo(REG_R1));
            Assert.That(emittedInstructions[0].idReg3(), Is.EqualTo(REG_R2));
            Assert.That(codeGen.Emitter.emitGetInsSC(emittedInstructions[0]), Is.EqualTo((nint)2));
            Assert.That(emittedInstructions[1].idIns(), Is.EqualTo(INS_ldr));
            Assert.That(emittedInstructions[1].idReg1(), Is.EqualTo(REG_R0));
            Assert.That(emittedInstructions[1].idReg2(), Is.EqualTo(REG_R3));
            Assert.That(codeGen.Emitter.emitGetInsSC(emittedInstructions[1]), Is.EqualTo((nint)8));
        });
    }

    [Test]
    public static void IndirectLoadDispatchesThroughCodeGen()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            InitializeLocalRegisterCandidate(compiler);
            var address = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            address.RegNum = REG_R1;
            var indir = new GenTreeIndir(GT_IND, TYP_INT, address) { RegNum = REG_R0 };

            RecordArm32Instructions(() => codeGen.genCodeForTreeNode(indir));

            var descriptor = LastInstruction(codeGen.Emitter);
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_ldr));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R0));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_R1));
        });
    }

    [TestCase(TYP_BYTE, INS_ldrsb)]
    [TestCase(TYP_UBYTE, INS_ldrb)]
    [TestCase(TYP_SHORT, INS_ldrsh)]
    [TestCase(TYP_USHORT, INS_ldrh)]
    public static void IndirectNarrowLoadsPreserveSignednessAndWidth(
        var_types type, instruction loadInstruction)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0);
            address.RegNum = REG_R1;
            var indir = new GenTreeIndir(GT_IND, type, address) { RegNum = REG_R0 };

            RecordArm32Instructions(() => codeGen.genCodeForTreeNode(indir));

            var descriptor = LastInstruction(codeGen.Emitter);
            Assert.That(descriptor.idIns(), Is.EqualTo(loadInstruction));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R0));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_R1));
        });
    }

    [TestCase(TYP_BYTE, INS_strb)]
    [TestCase(TYP_UBYTE, INS_strb)]
    [TestCase(TYP_SHORT, INS_strh)]
    [TestCase(TYP_USHORT, INS_strh)]
    public static void IndirectNarrowStoresUseTheDestinationWidth(
        var_types type, instruction storeInstruction)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0);
            address.RegNum = REG_R1;
            var data = compiler.gtNewIconNode(type.ActualType, 7);
            data.RegNum = REG_R3;
            var store = new GenTreeStoreInd(type, address, data);

            RecordArm32Instructions(() => codeGen.genCodeForTreeNode(store));

            var descriptor = LastInstruction(codeGen.Emitter);
            Assert.That(descriptor.idIns(), Is.EqualTo(storeInstruction));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R3));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_R1));
        });
    }

    [Test]
    public static void VolatileIndirectLoadUsesTrailingMemoryBarrier()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            InitializeLocalRegisterCandidate(compiler);
            var address = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            address.RegNum = REG_R1;
            var indir = new GenTreeIndir(GT_IND, TYP_INT, address)
            {
                Flags = GTF_IND_VOLATILE,
                RegNum = REG_R0,
            };

            RecordArm32Instructions(() => codeGen.genCodeForTreeNode(indir));

            var descriptors = CurrentDescriptors(codeGen.Emitter)
                ?? throw new AssertionException("Missing descriptor buffer.");
            Assert.That(descriptors, Has.Count.EqualTo(2));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_ldr));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(INS_dmb));
            Assert.That(codeGen.Emitter.emitGetInsSC(descriptors[1]), Is.EqualTo((nint)INS_BARRIER_SY));
        });
    }

    [Test]
    public static void VolatileIndirectStoreUsesLeadingMemoryBarrier()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            InitializeLocalRegisterCandidate(compiler);
            var address = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            address.RegNum = REG_R1;
            var data = compiler.gtNewLclvNode(TYP_INT, 0);
            data.RegNum = REG_R1;
            var store = new GenTreeStoreInd(TYP_INT, address, data) { Flags = GTF_IND_VOLATILE };

            RecordArm32Instructions(() => codeGen.genCodeForTreeNode(store));

            var descriptors = CurrentDescriptors(codeGen.Emitter)
                ?? throw new AssertionException("Missing descriptor buffer.");
            Assert.That(descriptors, Has.Count.EqualTo(2));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_dmb));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(INS_str));
        });
    }

    [TestCase(TYP_FLOAT, false)]
    [TestCase(TYP_DOUBLE, false)]
    [TestCase(TYP_FLOAT, true)]
    [TestCase(TYP_DOUBLE, true)]
    public static void UnalignedFloatingAccessUsesIntegerWordTransfers(var_types type, bool store)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            var address = compiler.gtNewLclvNode(TYP_BYREF, 0);
            address.RegNum = REG_R1;
            var dataReg = REG_F0;
            GenTreeIndir indir;

            if (store)
            {
                var data = compiler.gtNewLclvNode(type, 0);
                data.RegNum = dataReg;
                indir = new GenTreeStoreInd(type, address, data) { Flags = GTF_IND_UNALIGNED };
            }
            else
            {
                indir = new GenTreeIndir(GT_IND, type, address)
                {
                    Flags = GTF_IND_UNALIGNED,
                    RegNum = dataReg,
                };
            }

            var tempRegisters = type is TYP_FLOAT
                ? SRBM_R2
                : SRBM_R2 | SRBM_R3;
            codeGen.InternalRegisters.Add(indir, new regMaskTP(tempRegisters));

            var initialCount = CurrentDescriptors(codeGen.Emitter)?.Count ?? 0;
            var ins = store ? INS_vstr : INS_vldr;
            var attr = type is TYP_FLOAT ? EA_4BYTE : EA_8BYTE;
            RecordArm32Instructions(() => EmitLoadStoreOp(codeGen.Emitter, ins, attr, dataReg, indir));

            var descriptors = CurrentDescriptors(codeGen.Emitter)
                ?? throw new AssertionException("Missing descriptor buffer.");
            var emittedInstructions = descriptors.GetRange(initialCount, descriptors.Count - initialCount);
            instruction[] expected = type is TYP_FLOAT
                ? store ? [INS_vmov_f2i, INS_str] : [INS_ldr, INS_vmov_i2f]
                : store
                    ? [INS_vmov_d2i, INS_str, INS_str]
                    : [INS_ldr, INS_ldr, INS_vmov_i2d];

            Assert.That(emittedInstructions.ConvertAll(static descriptor => descriptor.idIns()),
                Is.EqualTo(expected));
        });
    }

    [TestCase(BARRIER_FULL)]
    [TestCase(BARRIER_LOAD_ONLY)]
    [TestCase(BARRIER_STORE_ONLY)]
    public static void MemoryBarriersEmitFullSystemDmb(BarrierKind barrierKind)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            RecordArm32Instructions(() => codeGen.instGen_MemoryBarrier(barrierKind));

            var descriptor = LastInstruction(codeGen.Emitter);
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_dmb));
            Assert.That(codeGen.Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)INS_BARRIER_SY));
        }, minOpts: false);
    }

#if !DEBUG
    [Test]
    public static void OptimizedAdjacentMemoryBarriersCoalesce()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            codeGen.instGen_MemoryBarrier(BARRIER_FULL);
            codeGen.instGen_MemoryBarrier(BARRIER_LOAD_ONLY);

            var descriptors = CurrentDescriptors(codeGen.Emitter)
                ?? throw new AssertionException("Missing descriptor buffer.");
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_dmb));
            Assert.That(codeGen.Emitter.emitGetInsSC(descriptors[0]), Is.EqualTo((nint)INS_BARRIER_SY));
        }, minOpts: false);
    }
#endif

    private static void RecordArm32Instructions(TestDelegate action)
    {
#if DEBUG
        try
        {
            action();
        }
        catch (FatalJitException failure) when (failure.Result == CorJitResult.CORJIT_SKIPPED)
        {
        }
#else
        action();
#endif
    }

    private static void InitializeLocalRegisterCandidate(Compiler compiler)
    {
        compiler.lvaCount = 1;
        compiler.lvaTable =
        [
            new LclVarDsc
            {
                Type = TYP_I_IMPL,
                lvLRACandidate = true,
                RegNum = REG_R1,
            },
        ];
    }

    private static void InitializeStackLocal(Compiler compiler, CodeGen codeGen)
    {
        compiler.lvaCount = 1;
        compiler.lvaTable =
        [
            new LclVarDsc
            {
                Type = TYP_INT,
                lvOnFrame = true,
                lvFramePointerBased = false,
                StackOffset = -16,
            },
        ];
        compiler.lvaDoneFrameLayout = Compiler.REGALLOC_FRAME_LAYOUT;
        compiler.lvaOutgoingArgSpaceVar = BAD_VAR_NUM;
        codeGen.IsFramePointerUsed = false;
    }

    private static GenTreeIndexAddr CreateIndexAddress(
        Compiler compiler, CodeGen codeGen, int elementSize, bool boundsCheck)
    {
        var baseAddress = compiler.gtNewIconNode(TYP_REF, 0);
        baseAddress.RegNum = REG_R1;
        var index = compiler.gtNewIconNode(TYP_INT, 0);
        index.RegNum = REG_R2;
        var tree = new GenTreeIndexAddr(
            baseAddress, index, TYP_INT, NO_CLASS_HANDLE, elementSize, 8, 12, boundsCheck)
        {
            RegNum = REG_R0,
        };

        codeGen.InternalRegisters.Add(tree, new regMaskTP(SRBM_R3));
        codeGen.GCInfo.gcMarkRegPtrVal(REG_R1, TYP_REF);

        return tree;
    }

    private static void ConfigureBoundsCheckTarget(Compiler compiler, CodeGen codeGen)
    {
        compiler.compCurBB = new BasicBlock(null, null);
        codeGen.GCInfo.gcVarPtrSetCur = VarSetOps.MakeEmpty(compiler);
        var target = new BasicBlock(null, null);
        target.SetFlags(BBF_HAS_LABEL | BBF_THROW_HELPER);
        var descriptor = compiler.fgGetExcptnTarget(SCK_RNGCHK_FAIL, compiler.compCurBB);
        descriptor.acdUsed = true;
        descriptor.acdDstBlk = target;
    }

    private static Emitter.instrDesc LastInstruction(Emitter emitter)
    {
        var descriptors = CurrentDescriptors(emitter)
            ?? throw new AssertionException("Missing descriptor buffer.");
        Assert.That(descriptors, Is.Not.Empty);
        return descriptors[^1];
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitInsLoadStoreOp")]
    private static extern void EmitLoadStoreOp(
        Emitter emitter, instruction ins, emitAttr attr, regNumber dataReg, GenTreeIndir indir);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);
}
#endif
