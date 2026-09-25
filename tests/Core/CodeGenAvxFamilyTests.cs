// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.Globals;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenAvxFamilyTests
{
    [TestCase(0, REG_XMM1, 0, REG_XMM1, REG_XMM2, REG_XMM3)]
    [TestCase(0, REG_XMM2, 0, REG_XMM2, REG_XMM1, REG_XMM3)]
    [TestCase(0, REG_XMM3, 1, REG_XMM3, REG_XMM2, REG_XMM1)]
    [TestCase(0, REG_XMM4, 0, REG_XMM1, REG_XMM2, REG_XMM3)]
    [TestCase(1, REG_XMM2, -1, REG_XMM2, REG_XMM3, REG_NA)]
    [TestCase(1, REG_XMM3, 1, REG_XMM3, REG_XMM2, REG_NA)]
    [TestCase(1, REG_XMM4, 1, REG_XMM3, REG_XMM2, REG_NA)]
    [TestCase(2, REG_XMM1, -1, REG_XMM1, REG_XMM3, REG_NA)]
    [TestCase(2, REG_XMM3, 1, REG_XMM3, REG_XMM1, REG_NA)]
    [TestCase(2, REG_XMM4, -1, REG_XMM1, REG_XMM3, REG_NA)]
    [TestCase(3, REG_XMM1, 0, REG_XMM1, REG_XMM2, REG_NA)]
    [TestCase(3, REG_XMM2, 0, REG_XMM2, REG_XMM1, REG_NA)]
    [TestCase(3, REG_XMM4, 0, REG_XMM1, REG_XMM2, REG_NA)]
    public static void FmaSelectsTheNativeFormWithoutRewritingOperands(
        int memoryOperand, regNumber target, int formOffset, regNumber seed, regNumber source, regNumber rm)
    {
        WithHardware((compiler, codeGen) =>
        {
            var operands = new GenTree[] { Vector(REG_XMM1), Vector(REG_XMM2), Vector(REG_XMM3) };
            if (memoryOperand != 0)
            {
                operands[memoryOperand - 1] = Memory(compiler, TYP_SIMD16);
            }
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AVX512_FusedMultiplyAdd, TYP_FLOAT, 16, operands)
            {
                RegNum = target,
            };
            var original = node.Operands.ToArray();
#if DEBUG
            var useNumber = 0;
            foreach (var operand in original)
            {
                codeGen.genNumberOperandUse(operand, ref useNumber);
            }
#endif

            codeGen.genAvxFamilyIntrinsic(node, INS_OPTS_NONE);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(target == seed ? 1 : 2));
            if (target != seed)
            {
                AssertMove(descriptors[0], target, seed, EA_16BYTE);
            }
            var id = descriptors[^1];
            Assert.That(id.idIns(), Is.EqualTo((instruction)((int)INS_vfmadd213ps + formOffset)));
            Assert.That(id.idOpSize(), Is.EqualTo(EA_16BYTE));
            Assert.That(id.idReg1(), Is.EqualTo(target));
            Assert.That(id.idReg2(), Is.EqualTo(source));
            if (memoryOperand == 0)
            {
                Assert.That(id.idReg3(), Is.EqualTo(rm));
            }
            else
            {
                Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_RAX));
            }
            Assert.That(node.Operands.ToArray(), Is.EqualTo(original));
            AssertProduced(node);
        });
    }

    [TestCase(NI_AVX512_FusedMultiplyAddNegated, INS_vfnmadd213pd)]
    [TestCase(NI_AVX512_FusedMultiplySubtract, INS_vfmsub213pd)]
    [TestCase(NI_AVX512_FusedMultiplySubtractNegated, INS_vfnmsub213pd)]
    [TestCase(NI_AVX512_FusedMultiplyAddSubtract, INS_vfmaddsub213pd)]
    [TestCase(NI_AVX512_FusedMultiplySubtractAdd, INS_vfmsubadd213pd)]
    public static void FmaPreservesTheOperationAndVectorWidth(NamedIntrinsic intrinsic, instruction expected)
    {
        WithHardware((compiler, codeGen) =>
        {
            var node = new GenTreeHWIntrinsic(TYP_SIMD32, intrinsic, TYP_DOUBLE, 32,
                Vector(REG_XMM1, TYP_SIMD32), Vector(REG_XMM2, TYP_SIMD32), Vector(REG_XMM3, TYP_SIMD32))
            {
                RegNum = REG_XMM1,
            };
            codeGen.genAvxFamilyIntrinsic(node, INS_OPTS_NONE);
            var id = Descriptors(codeGen)[^1];
            Assert.That(id.idIns(), Is.EqualTo(expected));
            Assert.That(id.idOpSize(), Is.EqualTo(EA_32BYTE));
        });
    }

    [TestCase(2, INS_vfmadd132ss, REG_XMM3)]
    [TestCase(3, INS_vfmadd213ss, REG_XMM2)]
    public static void ScalarFmaCopiesUpperBitsFromTheFirstOperand(
        int memoryOperand, instruction expected, regNumber source)
    {
        WithHardware((compiler, codeGen) =>
        {
            var operands = new GenTree[] { Vector(REG_XMM1), Vector(REG_XMM2), Vector(REG_XMM3) };
            operands[memoryOperand - 1] = Memory(compiler, TYP_FLOAT);
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AVX512_FusedMultiplyAddScalar, TYP_FLOAT, 16, operands)
            {
                RegNum = REG_XMM4,
            };
            codeGen.genAvxFamilyIntrinsic(node, INS_OPTS_NONE);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            AssertMove(descriptors[0], REG_XMM4, REG_XMM1, EA_16BYTE);
            Assert.That(descriptors[1].idIns(), Is.EqualTo(expected));
            Assert.That(descriptors[1].idReg2(), Is.EqualTo(source));
            Assert.That(descriptors[1].idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_RAX));
        });
    }

    [Test]
    public static void RoundingMaskingAndZeroingReachTheSelectedFmaForm()
    {
        WithHardware((compiler, codeGen) =>
        {
            var node = new GenTreeHWIntrinsic(TYP_SIMD64, NI_AVX512_FusedMultiplyAdd, TYP_FLOAT, 64,
                Vector(REG_XMM1, TYP_SIMD64), Vector(REG_XMM2, TYP_SIMD64), Vector(REG_XMM3, TYP_SIMD64))
            {
                RegNum = REG_XMM3,
            };
            codeGen.genAvxFamilyIntrinsic(node, INS_OPTS_EVEX_er_ru | INS_OPTS_EVEX_em_k7 | INS_OPTS_EVEX_em_zero);

            var id = Descriptors(codeGen)[^1];
            Assert.That(id.idIns(), Is.EqualTo(INS_vfmadd231ps));
            Assert.That(id.idOpSize(), Is.EqualTo(EA_64BYTE));
            Assert.That(id.idReg2(), Is.EqualTo(REG_XMM2));
            Assert.That(id.idReg3(), Is.EqualTo(REG_XMM1));
            Assert.That(id.idGetEvexbContext(), Is.EqualTo(2u));
            Assert.That(id.idGetEvexAaaContext(), Is.EqualTo(7u));
            Assert.That(id.idIsEvexZContextSet(), Is.True);
        });
    }

    [Test]
    public static void RoundingDispatchCanReuseFmaWithoutConsumingOrProducingOperands()
    {
        WithHardware((compiler, codeGen) =>
        {
            var node = new GenTreeHWIntrinsic(TYP_SIMD64, NI_AVX512_FusedMultiplyAdd, TYP_FLOAT, 64,
                Vector(REG_XMM1, TYP_SIMD64), Vector(REG_XMM2, TYP_SIMD64), Vector(REG_XMM3, TYP_SIMD64))
            {
                RegNum = REG_XMM2,
            };
            codeGen.genFmaIntrinsic(node, INS_OPTS_EVEX_er_rd);
            codeGen.genFmaIntrinsic(node, INS_OPTS_EVEX_er_ru);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            foreach (var id in descriptors)
            {
                Assert.That(id.idIns(), Is.EqualTo(INS_vfmadd213ps));
                Assert.That(id.idReg1(), Is.EqualTo(REG_XMM2));
                Assert.That(id.idReg2(), Is.EqualTo(REG_XMM1));
                Assert.That(id.idReg3(), Is.EqualTo(REG_XMM3));
            }
            Assert.That(descriptors[0].idGetEvexbContext(), Is.EqualTo(1u));
            Assert.That(descriptors[1].idGetEvexbContext(), Is.EqualTo(2u));
#if DEBUG
            Assert.That(node._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_PRODUCED,
                Is.EqualTo((GenTreeDebugFlags)0));
            foreach (var operand in node.Operands)
            {
                Assert.That(operand._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
                    Is.EqualTo((GenTreeDebugFlags)0));
            }
#endif
        });
    }

    [Test]
    public static void FmaUsesAndReleasesTheSpillHomeWithoutReloading()
    {
        WithHardware((compiler, codeGen) =>
        {
            var spilled = Vector(REG_XMM2);
            codeGen.RegSet.tmpInit();
            codeGen.RegSet.tmpPreAllocateTemps(TYP_SIMD16, 1);
            var temp = codeGen.RegSet.tmpGetTemp(TYP_SIMD16);
            temp.tdTempOffs = -32;
            codeGen.RegSet.tmpRlsTemp(temp);
            spilled.Flags |= GTF_SPILL;
            codeGen.RegSet.rsSpillTree(spilled.RegNum, spilled);
            spilled.Flags |= GTF_NOREG_AT_USE;
            spilled.IsRegOptional = true;
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AVX512_FusedMultiplyAdd, TYP_FLOAT, 16,
                Vector(REG_XMM1), spilled, Vector(REG_XMM3))
            {
                RegNum = REG_XMM1,
            };
            var before = Descriptors(codeGen).Count;

            codeGen.genAvxFamilyIntrinsic(node, INS_OPTS_NONE);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(before + 1));
            Assert.That(descriptors[^1].idIns(), Is.EqualTo(INS_vfmadd132ps));
            Assert.That(descriptors[^1].idReg2(), Is.EqualTo(REG_XMM3));
            Assert.That(descriptors[^1].idAddr().iiaLclVar.lvaVarNum(), Is.EqualTo(temp.tdTempNum));
            Assert.That(codeGen.RegSet.rsGetSpillInfo(spilled, spilled.RegNum, out _), Is.Null);
        });
    }

    [TestCase(REG_XMM1, false, INS_vpermt2d, REG_XMM1, REG_XMM2)]
    [TestCase(REG_XMM2, false, INS_vpermi2d, REG_XMM2, REG_XMM1)]
    [TestCase(REG_XMM4, false, INS_vpermt2d, REG_XMM1, REG_XMM2)]
    [TestCase(REG_XMM1, true, INS_vpermt2d, REG_XMM1, REG_XMM2)]
    [TestCase(REG_XMM2, true, INS_vpermi2d, REG_XMM2, REG_XMM1)]
    public static void PermuteSelectsIndexOrTableDestination(
        regNumber target, bool memory, instruction expected, regNumber seed, regNumber source)
    {
        WithHardware((compiler, codeGen) =>
        {
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AVX512_PermuteVar4x32x2, TYP_INT, 16,
                Vector(REG_XMM1), Vector(REG_XMM2), memory ? Memory(compiler, TYP_SIMD16) : Vector(REG_XMM3))
            {
                RegNum = target,
            };
            codeGen.genAvxFamilyIntrinsic(node, INS_OPTS_NONE);
            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(seed == target ? 1 : 2));
            if (seed != target)
            {
                AssertMove(descriptors[0], target, seed, EA_16BYTE);
            }
            Assert.That(descriptors[^1].idIns(), Is.EqualTo(expected));
            Assert.That(descriptors[^1].idReg2(), Is.EqualTo(source));
            AssertProduced(node);
        });
    }

    [TestCase(false, TYP_INT, TYP_INT, TYP_SIMD16, INS_vpgatherdd, EA_16BYTE)]
    [TestCase(false, TYP_FLOAT, TYP_LONG, TYP_SIMD32, INS_vgatherqps, EA_32BYTE)]
    [TestCase(false, TYP_INT, TYP_LONG, TYP_SIMD32, INS_vpgatherqd, EA_32BYTE)]
    [TestCase(true, TYP_LONG, TYP_LONG, TYP_SIMD16, INS_vpgatherqq, EA_16BYTE)]
    [TestCase(true, TYP_DOUBLE, TYP_LONG, TYP_SIMD16, INS_vgatherqpd, EA_16BYTE)]
    public static void GatherPreservesTheInputMaskAndUsesTheNativeIndexWidth(
        bool masked, var_types baseType, var_types indexType, var_types indexVectorType,
        instruction expected, emitAttr expectedSize)
    {
        WithHardware((compiler, codeGen) =>
        {
            var address = Register(compiler, TYP_I_IMPL, REG_RAX);
            var index = Vector(REG_XMM2, indexVectorType);
            var scale = compiler.gtNewIconNode(TYP_INT, 4);
            scale.IsContained = true;
            var operands = masked
                ? new GenTree[] { Vector(REG_XMM1), address, index, Vector(REG_XMM3), scale }
                : [address, index, scale];
            var node = new GenTreeHWIntrinsic(TYP_SIMD16,
                masked ? NI_AVX2_GatherMaskVector128 : NI_AVX2_GatherVector128, baseType, 16, operands)
            {
                RegNum = REG_XMM4,
                AuxiliaryType = indexType,
            };
            codeGen.InternalRegisters.Add(node, Mask(REG_XMM5));
            codeGen.genAvxFamilyIntrinsic(node, INS_OPTS_NONE);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(masked ? 3 : 2));
            if (masked)
            {
                AssertMove(descriptors[0], REG_XMM5, REG_XMM3, EA_16BYTE);
                AssertMove(descriptors[1], REG_XMM4, REG_XMM1, EA_16BYTE);
            }
            else
            {
                Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_pcmpeqd));
                Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_XMM5));
            }
            var id = descriptors[^1];
            Assert.That(id.idIns(), Is.EqualTo(expected));
            Assert.That(id.idOpSize(), Is.EqualTo(expectedSize));
            Assert.That(id.idReg1(), Is.EqualTo(REG_XMM4));
            Assert.That(id.idReg2(), Is.EqualTo(REG_XMM5));
            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_RAX));
            Assert.That(id.idAddr().iiaAddrMode.amIndxReg, Is.EqualTo(REG_XMM2));
            Assert.That(codeGen.InternalRegisters.GetAll(node), Is.EqualTo(default(regMaskTP)));
        });
    }

    [TestCase(16, TYP_LONG, INS_knotb, true)]
    [TestCase(16, TYP_INT, INS_knotb, true)]
    [TestCase(16, TYP_SHORT, INS_knotb, false)]
    [TestCase(16, TYP_BYTE, INS_knotw, false)]
    [TestCase(32, TYP_BYTE, INS_knotd, false)]
    [TestCase(64, TYP_BYTE, INS_knotq, false)]
    public static void MaskNotUsesTheLaneCountAndClearsOnlyUnusedByteBits(
        int size, var_types baseType, instruction expected, bool clearsHighBits)
    {
        WithHardware((compiler, codeGen) =>
        {
            var node = new GenTreeHWIntrinsic(TYP_MASK, NI_AVX512_NotMask, baseType, (byte)size,
                new GenTreePhysReg(REG_K1, TYP_MASK) { RegNum = REG_K1 })
            {
                RegNum = REG_K2,
            };
            codeGen.genAvxFamilyIntrinsic(node, INS_OPTS_NONE);
            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(clearsHighBits ? 3 : 1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(expected));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_8BYTE));
            if (clearsHighBits)
            {
                Assert.That(descriptors[1].idIns(), Is.EqualTo(INS_kshiftlb));
                Assert.That(descriptors[2].idIns(), Is.EqualTo(INS_kshiftrb));
                Assert.That(InstructionConstant(codeGen.Emitter, descriptors[1]), Is.EqualTo((nint)(8 - (size / baseType.Size))));
                Assert.That(InstructionConstant(codeGen.Emitter, descriptors[2]), Is.EqualTo((nint)(8 - (size / baseType.Size))));
            }
        });
    }

    [TestCase(NI_AVX512_AddMask, INS_kaddd)]
    [TestCase(NI_AVX512_AndMask, INS_kandd)]
    [TestCase(NI_AVX512_AndNotMask, INS_kandnd)]
    [TestCase(NI_AVX512_OrMask, INS_kord)]
    [TestCase(NI_AVX512_XorMask, INS_kxord)]
    [TestCase(NI_AVX512_XnorMask, INS_kxnord)]
    public static void BinaryMasksUseVexLengthAndDistinctInputs(NamedIntrinsic intrinsic, instruction expected)
    {
        WithHardware((compiler, codeGen) =>
        {
            var node = new GenTreeHWIntrinsic(TYP_MASK, intrinsic, TYP_BYTE, 32,
                new GenTreePhysReg(REG_K1, TYP_MASK) { RegNum = REG_K1 },
                new GenTreePhysReg(REG_K2, TYP_MASK) { RegNum = REG_K2 })
            {
                RegNum = REG_K3,
            };
            codeGen.genAvxFamilyIntrinsic(node, INS_OPTS_NONE);
            var id = Descriptors(codeGen)[^1];
            Assert.That(id.idIns(), Is.EqualTo(expected));
            Assert.That(id.idOpSize(), Is.EqualTo(EA_32BYTE));
            Assert.That(id.idReg1(), Is.EqualTo(REG_K3));
            Assert.That(id.idReg2(), Is.EqualTo(REG_K1));
            Assert.That(id.idReg3(), Is.EqualTo(REG_K2));
        });
    }

    [TestCase(NI_AVX512_KORTEST, INS_kortestw)]
    [TestCase(NI_AVX512_KTEST, INS_ktestw)]
    public static void MaskTestsPreserveThePinnedOracleSelfTestOperands(NamedIntrinsic intrinsic, instruction expected)
    {
        WithHardware((compiler, codeGen) =>
        {
            var node = new GenTreeHWIntrinsic(TYP_VOID, intrinsic, TYP_BYTE, 16,
                new GenTreePhysReg(REG_K1, TYP_MASK) { RegNum = REG_K1 },
                new GenTreePhysReg(REG_K2, TYP_MASK) { RegNum = REG_K2 });
            codeGen.genAvxFamilyIntrinsic(node, INS_OPTS_NONE);
            var id = Descriptors(codeGen)[^1];
            Assert.That(id.idIns(), Is.EqualTo(expected));
            Assert.That(id.idReg1(), Is.EqualTo(REG_K1));
            Assert.That(id.idReg2(), Is.EqualTo(REG_K1));
        });
    }

    [TestCase(NI_AVX512_ShiftLeftMask, INS_kshiftlq)]
    [TestCase(NI_AVX512_ShiftRightMask, INS_kshiftrq)]
    public static void MaskShiftsKeepTheNativeSignedImmediateByte(NamedIntrinsic intrinsic, instruction expected)
    {
        WithHardware((compiler, codeGen) =>
        {
            var count = compiler.gtNewIconNode(TYP_INT, 255);
            count.IsContained = true;
            var node = new GenTreeHWIntrinsic(TYP_MASK, intrinsic, TYP_BYTE, 64,
                new GenTreePhysReg(REG_K1, TYP_MASK) { RegNum = REG_K1 }, count)
            {
                RegNum = REG_K2,
            };
            codeGen.genAvxFamilyIntrinsic(node, INS_OPTS_NONE);
            var id = Descriptors(codeGen)[^1];
            Assert.That(id.idIns(), Is.EqualTo(expected));
            Assert.That(InstructionConstant(codeGen.Emitter, id), Is.EqualTo((nint)(-1)));
        });
    }

    [TestCase(16, TYP_LONG, INS_kmovb_gpr, EA_4BYTE)]
    [TestCase(16, TYP_BYTE, INS_kmovw_gpr, EA_4BYTE)]
    [TestCase(32, TYP_BYTE, INS_kmovd_gpr, EA_4BYTE)]
    [TestCase(64, TYP_BYTE, INS_kmovq_gpr, EA_8BYTE)]
    public static void MoveMaskUsesOnlyTheRequiredIntegerWidth(int size, var_types baseType,
        instruction expected, emitAttr expectedSize)
    {
        WithHardware((compiler, codeGen) =>
        {
            var node = new GenTreeHWIntrinsic(size == 64 ? TYP_LONG : TYP_INT, NI_AVX512_MoveMask,
                baseType, (byte)size, new GenTreePhysReg(REG_K1, TYP_MASK) { RegNum = REG_K1 })
            {
                RegNum = REG_RAX,
            };
            codeGen.genAvxFamilyIntrinsic(node, INS_OPTS_NONE);
            var id = Descriptors(codeGen)[^1];
            Assert.That(id.idIns(), Is.EqualTo(expected));
            Assert.That(id.idOpSize(), Is.EqualTo(expectedSize));
            Assert.That(id.idReg1(), Is.EqualTo(REG_RAX));
            Assert.That(id.idReg2(), Is.EqualTo(REG_K1));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void WideningSupportsVectorAndPointerOverloads(bool pointer)
    {
        WithHardware((compiler, codeGen) =>
        {
            var source = pointer ? Register(compiler, TYP_I_IMPL, REG_RAX) : (GenTree)Vector(REG_XMM1);
            var node = new GenTreeHWIntrinsic(TYP_SIMD32, NI_AVX2_ConvertToVector256Int32, TYP_SHORT, 32, source)
            {
                RegNum = REG_XMM2,
                AuxiliaryType = pointer ? TYP_U_IMPL : TYP_UNKNOWN,
            };
            codeGen.genAvxFamilyIntrinsic(node, INS_OPTS_NONE);
            var id = Descriptors(codeGen)[^1];
            Assert.That(id.idIns(), Is.EqualTo(INS_pmovsxwd));
            Assert.That(id.idOpSize(), Is.EqualTo(EA_32BYTE));
            Assert.That(id.idReg1(), Is.EqualTo(REG_XMM2));
            if (pointer)
            {
                Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_RAX));
            }
            else
            {
                Assert.That(id.idReg2(), Is.EqualTo(REG_XMM1));
            }
        });
    }

    [TestCase(false, REG_RAX)]
    [TestCase(false, REG_RDX)]
    [TestCase(true, REG_RAX)]
    [TestCase(true, REG_RDX)]
    public static void MultiplyNoFlagsUsesRdxAndTheOptionalLowResultHome(bool storeLow, regNumber first)
    {
        WithHardware((compiler, codeGen) =>
        {
            var op1 = Register(compiler, TYP_LONG, first);
            var op2 = Register(compiler, TYP_LONG, REG_RCX);
            var operands = storeLow
                ? new GenTree[] { op1, op2, Register(compiler, TYP_I_IMPL, REG_R9) }
                : [op1, op2];
            var node = new GenTreeHWIntrinsic(TYP_LONG, NI_AVX2_X64_MultiplyNoFlags, TYP_UNKNOWN, 0, operands)
            {
                RegNum = REG_R8,
            };
            if (storeLow)
            {
                codeGen.InternalRegisters.Add(node, Mask(REG_R10));
            }
            codeGen.genAvxFamilyIntrinsic(node, INS_OPTS_NONE);
            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1 + (first == REG_RDX ? 0 : 1) + (storeLow ? 1 : 0)));
            var mul = descriptors[storeLow ? ^2 : ^1];
            Assert.That(mul.idIns(), Is.EqualTo(INS_mulx));
            Assert.That(mul.idInsFmt(), Is.EqualTo(IF_RWR_RWR_RRD));
            Assert.That(mul.idReg1(), Is.EqualTo(REG_R8));
            Assert.That(mul.idReg2(), Is.EqualTo(storeLow ? REG_R10 : REG_R8));
            Assert.That(mul.idReg3(), Is.EqualTo(REG_RCX));
            if (storeLow)
            {
                Assert.That(descriptors[^1].idIns(), Is.EqualTo(INS_mov));
                Assert.That(descriptors[^1].idReg1(), Is.EqualTo(REG_R10));
                Assert.That(descriptors[^1].idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_R9));
            }
        });
    }

    [TestCase(false, REG_RCX, true)]
    [TestCase(false, REG_RAX, false)]
    [TestCase(true, REG_RCX, true)]
    [TestCase(true, REG_RAX, false)]
    public static void CountZeroBreaksOnlyFalseDependencies(bool memory, regNumber target, bool zeroes)
    {
        WithHardware((compiler, codeGen) =>
        {
            GenTree operand = memory ? Memory(compiler, TYP_LONG) : Register(compiler, TYP_LONG, REG_RAX);
            var node = new GenTreeHWIntrinsic(TYP_LONG, NI_AVX2_X64_LeadingZeroCount, TYP_UNKNOWN, 0, operand)
            {
                RegNum = target,
            };
            codeGen.genAvxFamilyIntrinsic(node, INS_OPTS_NONE);
            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(zeroes ? 2 : 1));
            if (zeroes)
            {
                Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_xor));
                Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_4BYTE));
                Assert.That(descriptors[0].idReg1(), Is.EqualTo(target));
                Assert.That(descriptors[0].idReg2(), Is.EqualTo(target));
            }
            Assert.That(descriptors[^1].idIns(), Is.EqualTo(INS_lzcnt));
            Assert.That(descriptors[^1].idOpSize(), Is.EqualTo(EA_8BYTE));
        });
    }

    [TestCase(NI_AVX512_ConvertToVector128Byte, TYP_INT, TYP_SIMD32, TYP_SIMD16, INS_vpmovdb)]
    [TestCase(NI_AVX512_ConvertToVector128ByteWithSaturation, TYP_UINT, TYP_SIMD32, TYP_SIMD16, INS_vpmovusdb)]
    [TestCase(NI_AVX512_ConvertToVector256Int32, TYP_LONG, TYP_SIMD64, TYP_SIMD32, INS_vpmovqd)]
    public static void NarrowingConversionsPutTheDestinationInTheRmField(
        NamedIntrinsic intrinsic, var_types baseType, var_types sourceType, var_types targetType, instruction expected)
    {
        WithHardware((compiler, codeGen) =>
        {
            var node = new GenTreeHWIntrinsic(targetType, intrinsic, baseType, (byte)sourceType.Size, Vector(REG_XMM1, sourceType))
            {
                RegNum = REG_XMM2,
            };
            codeGen.genAvxFamilyIntrinsic(node, INS_OPTS_NONE);
            var id = Descriptors(codeGen)[^1];
            Assert.That(id.idIns(), Is.EqualTo(expected));
            Assert.That(id.idReg1(), Is.EqualTo(REG_XMM1));
            Assert.That(id.idReg2(), Is.EqualTo(REG_XMM2));
            Assert.That(id.idOpSize(), Is.EqualTo(sourceType.EmitSize));
        });
    }

    [TestCase(TYP_UBYTE, TYP_UBYTE, INS_vpdpbuud, INS_vpdpbuuds)]
    [TestCase(TYP_BYTE, TYP_UBYTE, INS_vpdpbsud, INS_vpdpbsuds)]
    [TestCase(TYP_BYTE, TYP_BYTE, INS_vpdpbssd, INS_vpdpbssds)]
    [TestCase(TYP_SHORT, TYP_USHORT, INS_vpdpwsud, INS_vpdpwsuds)]
    [TestCase(TYP_USHORT, TYP_USHORT, INS_vpdpwuud, INS_vpdpwuuds)]
    [TestCase(TYP_USHORT, TYP_SHORT, INS_vpdpwusd, INS_vpdpwusds)]
    public static void VnniUsesBothInputSignednesses(
        var_types baseType, var_types auxiliaryType, instruction normal, instruction saturating)
    {
        foreach (var saturate in new[] { false, true })
        {
            WithHardware((compiler, codeGen) =>
            {
                compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVXVNNIINT_V512);
                compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_AVXVNNIINT_V512);
                compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_AVXVNNIINT_V512);
                compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVXVNNIINT);
                compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_AVXVNNIINT);
                compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_AVXVNNIINT);
                var node = new GenTreeHWIntrinsic(TYP_SIMD16,
                    saturate ? NI_AVXVNNIINT_MultiplyWideningAndAddSaturate : NI_AVXVNNIINT_MultiplyWideningAndAdd,
                    baseType, 16, Vector(REG_XMM1), Vector(REG_XMM2), Vector(REG_XMM3))
                {
                    RegNum = REG_XMM4,
                    AuxiliaryType = auxiliaryType,
                };
                codeGen.genAvxFamilyIntrinsic(node, INS_OPTS_NONE);
                var descriptors = Descriptors(codeGen);
                Assert.That(descriptors, Has.Count.EqualTo(2));
                AssertMove(descriptors[0], REG_XMM4, REG_XMM1, EA_16BYTE);
                Assert.That(descriptors[1].idIns(), Is.EqualTo(saturate ? saturating : normal));
                Assert.That(descriptors[1].idReg2(), Is.EqualTo(REG_XMM2));
                Assert.That(descriptors[1].idReg3(), Is.EqualTo(REG_XMM3));
            });
        }
    }

#if DEBUG
    [Test]
    public static void GatherRejectionLeavesItsScratchMaskAndOperandsReusable()
    {
        WithHardware((compiler, codeGen) =>
        {
            var scale = compiler.gtNewIconNode(TYP_INT, 4);
            scale.IsContained = true;
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AVX2_GatherVector128, TYP_INT, 16,
                Register(compiler, TYP_I_IMPL, REG_RAX), Vector(REG_XMM2), scale)
            {
                RegNum = REG_XMM1,
                AuxiliaryType = TYP_INT,
            };
            var mask = Mask(REG_XMM5);
            codeGen.InternalRegisters.Add(node, mask);
            compiler.opts.dspCode = true;
            _ = Assert.Throws<FatalJitException>(() => codeGen.genAvxFamilyIntrinsic(node, INS_OPTS_NONE));
            compiler.opts.dspCode = false;
            Assert.That(Descriptors(codeGen), Is.Empty);
            Assert.That(codeGen.InternalRegisters.GetAll(node), Is.EqualTo(mask));
            codeGen.genAvxFamilyIntrinsic(node, INS_OPTS_NONE);
            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(2));
            Assert.That(codeGen.InternalRegisters.GetAll(node), Is.EqualTo(default(regMaskTP)));
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public static void DisassemblyRejectionPrecedesConsumptionAndAllowsRetry(int entry)
    {
        WithHardware((compiler, codeGen) =>
        {
            var node = entry == 3
                ? new GenTreeHWIntrinsic(TYP_LONG, NI_AVX2_X64_LeadingZeroCount, TYP_UNKNOWN, 0,
                    Register(compiler, TYP_LONG, REG_RAX))
                : new GenTreeHWIntrinsic(TYP_SIMD16,
                    entry == 2 ? NI_AVX512_PermuteVar4x32x2 : NI_AVX512_FusedMultiplyAdd,
                    entry == 2 ? TYP_INT : TYP_FLOAT, 16, Vector(REG_XMM1), Vector(REG_XMM2), Vector(REG_XMM3));
            node.RegNum = entry == 3 ? REG_RAX : REG_XMM1;
            var before = Descriptors(codeGen).Count;
            compiler.opts.dspCode = true;
            _ = Assert.Throws<FatalJitException>(() => EmitEntry(codeGen, node, entry));
            compiler.opts.dspCode = false;
            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(before));
            foreach (var operand in node.Operands)
            {
                Assert.That(operand._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
                    Is.EqualTo((GenTreeDebugFlags)0));
            }
            EmitEntry(codeGen, node, entry);
            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(before + 1));
        });
    }

    private static void EmitEntry(CodeGen codeGen, GenTreeHWIntrinsic node, int entry)
    {
        switch (entry)
        {
            case 0:
            {
                codeGen.genAvxFamilyIntrinsic(node, INS_OPTS_NONE);
                break;
            }
            case 1:
            {
                codeGen.genFmaIntrinsic(node, INS_OPTS_NONE);
                break;
            }
            case 2:
            {
                codeGen.genPermuteVar2x(node, INS_OPTS_NONE);
                break;
            }
            case 3:
            {
                codeGen.genXCNTIntrinsic(node, INS_lzcnt);
                break;
            }
        }
    }
#endif

    private static GenTreePhysReg Vector(regNumber reg, var_types type = TYP_SIMD16)
        => new(reg, type) { RegNum = reg };

    private static GenTreeIndir Memory(Compiler compiler, var_types type)
        => new(GT_IND, type, Register(compiler, TYP_I_IMPL, REG_RAX)) { IsContained = true };

    private static regMaskTP Mask(regNumber reg) => regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);

    private static void AssertMove(Emitter.instrDesc id, regNumber target, regNumber source, emitAttr size)
    {
        Assert.That(id.idIns(), Is.EqualTo(INS_movaps));
        Assert.That(id.idReg1(), Is.EqualTo(target));
        Assert.That(id.idReg2(), Is.EqualTo(source));
        Assert.That(id.idOpSize(), Is.EqualTo(size));
    }

    private static void AssertProduced(GenTree node)
    {
#if DEBUG
        Assert.That(node._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_PRODUCED,
            Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NODE_CG_PRODUCED));
#endif
    }

    private static void WithHardware(Action<Compiler, CodeGen> action)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            EnableAvx2(compiler);
            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX512);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_AVX512);
            compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_AVX512);
            AllFloat(compiler) = SRBM_ALLFLOAT_INIT;
            AllMask(compiler) = SRBM_ALLMASK_EVEX;
            codeGen.CopyRegisterInfo();
            codeGen.Emitter.UseVexEncodings = true;
            codeGen.Emitter.UseEvexEncodings = true;
            action(compiler, codeGen);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask AllFloat(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask AllMask(Compiler compiler);
}
