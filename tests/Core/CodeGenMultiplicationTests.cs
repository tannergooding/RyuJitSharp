// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenMultiplicationTests
{
    [TestCase(3, false, false)]
    [TestCase(5, true, false)]
    [TestCase(9, false, false)]
    [TestCase(3, false, true)]
    [TestCase(5, true, true)]
    [TestCase(7, false, false)]
    [TestCase(-1, true, false)]
    [TestCase(128, false, false)]
    public static void ImmediateProductsUseLeaOnlyWithoutOverflow(int value, bool immediateFirst, bool overflow)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var reg = CodeGenShiftTests.Register(compiler, TYP_INT, REG_RAX);
            var immediate = compiler.gtNewIconNode(TYP_INT, value);
            immediate.IsContained = true;
            var tree = new GenTreeOp(GT_MUL, TYP_INT,
                immediateFirst ? immediate : reg, immediateFirst ? reg : immediate)
            {
                RegNum = REG_R8,
            };
            if (overflow)
            {
                tree.Flags |= GTF_OVERFLOW;
                _ = CodeGenBinaryTests.PrepareThrowTarget(compiler);
            }

            codeGen.genCodeForMul(tree);

            var descriptors = CodeGenShiftTests.Descriptors(codeGen);
            var lea = !overflow && (value is 3 or 5 or 9);
            Assert.That(descriptors, Has.Count.EqualTo(overflow ? 2 : 1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(lea ? INS_lea : INS_imul_08));
            if (lea)
            {
                var address = descriptors[0].idAddr().iiaAddrMode;
                Assert.That(address.amBaseReg, Is.EqualTo(REG_RAX));
                Assert.That(address.amIndxReg, Is.EqualTo(REG_RAX));
                Assert.That(address.amScale, Is.EqualTo(value == 3 ? 1 : value == 5 ? 2 : 3));
            }
            else if (overflow)
            {
                Assert.That(descriptors[1].idIns(), Is.EqualTo(INS_jo));
            }
        });
    }

    [TestCase(3)]
    [TestCase(128)]
    public static void ContainedLocalAndImmediateUseTheThreeOperandForm(int value)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var local = compiler.gtNewLclvNode(TYP_INT, 0);
            local.IsContained = true;
            var immediate = compiler.gtNewIconNode(TYP_INT, value);
            immediate.IsContained = true;
            var tree = new GenTreeOp(GT_MUL, TYP_INT, local, immediate) { RegNum = REG_RCX };

            codeGen.genCodeForMul(tree);

            var descriptors = CodeGenShiftTests.Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_imul_CX));
            Assert.That(descriptors[0].idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MultiplicationSelectsTheSourceAlreadyInTheDestination(bool memoryFirst)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            GenTree first = memoryFirst ? compiler.gtNewLclvNode(TYP_INT, 0)
                : CodeGenShiftTests.Register(compiler, TYP_INT, REG_RCX);
            first.IsContained = memoryFirst;
            var tree = new GenTreeOp(GT_MUL, TYP_INT, first,
                CodeGenShiftTests.Register(compiler, TYP_INT, REG_RAX))
            {
                RegNum = REG_RAX,
            };

            codeGen.genCodeForMul(tree);

            var descriptors = CodeGenShiftTests.Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_imul));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_RAX));
        });
    }

    [TestCase(TYP_INT)]
    [TestCase(TYP_LONG)]
    public static void UnsignedOverflowUsesRaxAndCopiesBeforeTheBranch(var_types type)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            _ = CodeGenBinaryTests.PrepareThrowTarget(compiler);
            var tree = new GenTreeOp(GT_MUL, type,
                CodeGenShiftTests.Register(compiler, type, REG_RCX),
                CodeGenShiftTests.Register(compiler, type, REG_RAX))
            {
                RegNum = REG_R8,
                Flags = GTF_UNSIGNED | GTF_OVERFLOW,
            };

            codeGen.genCodeForMul(tree);

            var descriptors = CodeGenShiftTests.Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(3));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_mulEAX));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_RCX));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(INS_mov));
            Assert.That(descriptors[1].idReg1(), Is.EqualTo(REG_R8));
            Assert.That(descriptors[1].idReg2(), Is.EqualTo(REG_RAX));
            Assert.That(descriptors[2].idIns(), Is.EqualTo(INS_jb));
        });
    }

    [TestCase(false, false, false)]
    [TestCase(true, false, false)]
    [TestCase(true, true, false)]
    [TestCase(false, true, true)]
    [TestCase(true, true, true)]
    public static void HighProductsPreserveImplicitRegistersAndBmi2(
        bool unsigned, bool bmi2, bool memoryFirst)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            if (bmi2)
            {
                CodeGenShiftTests.EnableAvx2(compiler);
            }
            var useMulx = unsigned && bmi2;
            var implicitReg = useMulx ? REG_RDX : REG_RAX;
            GenTree first = memoryFirst ? compiler.gtNewLclvNode(TYP_INT, 0)
                : CodeGenShiftTests.Register(compiler, TYP_INT, REG_RCX);
            first.IsContained = memoryFirst;
            var second = CodeGenShiftTests.Register(compiler, TYP_INT, implicitReg);
            var tree = new GenTreeOp(GT_MULHI, TYP_INT, first, second)
            {
                RegNum = REG_R8,
                Flags = unsigned ? GTF_UNSIGNED : GTF_EMPTY,
            };

            codeGen.genCodeForMulHi(tree);

            var descriptors = CodeGenShiftTests.Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(useMulx ? 1 : 2));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(useMulx ? INS_mulx : unsigned ? INS_mulEAX : INS_imulEAX));
            if (useMulx)
            {
                Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R8));
                Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_R8));
            }
            else
            {
                Assert.That(descriptors[1].idReg1(), Is.EqualTo(REG_R8));
                Assert.That(descriptors[1].idReg2(), Is.EqualTo(REG_RDX));
            }
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void MemoryProductsConsumeIndirectAndSpilledOperands(bool high, bool spill)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            GenTree memory;
            if (spill)
            {
                codeGen.RegSet.tmpInit();
                codeGen.RegSet.tmpPreAllocateTemps(TYP_INT, 1);
                var temp = codeGen.RegSet.tmpGetTemp(TYP_INT);
                temp.tdTempOffs = -32;
                codeGen.RegSet.tmpRlsTemp(temp);
                memory = CodeGenShiftTests.Register(compiler, TYP_INT, REG_RCX);
                memory.Flags |= GTF_SPILL;
                codeGen.RegSet.rsSpillTree(REG_RCX, memory);
                memory.Flags |= GTF_NOREG_AT_USE;
                memory.IsRegOptional = true;
            }
            else
            {
                memory = new GenTreeIndir(GT_IND, TYP_INT,
                    CodeGenShiftTests.Register(compiler, TYP_BYREF, REG_RBX))
                {
                    IsContained = true,
                };
            }
            if (high)
            {
                CodeGenShiftTests.EnableAvx2(compiler);
            }
            var tree = new GenTreeOp(high ? GT_MULHI : GT_MUL, TYP_INT,
                CodeGenShiftTests.Register(compiler, TYP_INT, high ? REG_RDX : REG_RAX), memory)
            {
                RegNum = REG_RAX,
                Flags = high ? GTF_UNSIGNED : GTF_EMPTY,
            };

            if (high)
            {
                codeGen.genCodeForMulHi(tree);
            }
            else
            {
                codeGen.genCodeForMul(tree);
            }

            var descriptors = CodeGenShiftTests.Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(spill ? 2 : 1));
            Assert.That(descriptors[^1].idIns(), Is.EqualTo(high ? INS_mulx : INS_imul));
            Assert.That(tree.Op2, Is.SameAs(memory));
        });
    }

    [TestCase(false, TYP_INT)]
    [TestCase(false, TYP_LONG)]
    [TestCase(true, TYP_INT)]
    [TestCase(true, TYP_LONG)]
    public static void HighProductsCopyTheMultiplicandToTheImplicitRegister(bool bmi2, var_types type)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            if (bmi2)
            {
                CodeGenShiftTests.EnableAvx2(compiler);
            }
            var tree = new GenTreeOp(GT_MULHI, type,
                CodeGenShiftTests.Register(compiler, type, REG_R8),
                CodeGenShiftTests.Register(compiler, type, REG_RCX))
            {
                RegNum = REG_RDX,
                Flags = GTF_UNSIGNED,
            };

            codeGen.genCodeForMulHi(tree);

            var descriptors = CodeGenShiftTests.Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_mov));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(bmi2 ? REG_RDX : REG_RAX));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_R8));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(bmi2 ? INS_mulx : INS_mulEAX));
            Assert.That(descriptors[1].idOpSize(), Is.EqualTo(type.EmitSize));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void OverflowBranchesPrecedeProductSpilling(bool unsigned)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var target = CodeGenBinaryTests.PrepareThrowTarget(compiler);
            codeGen.RegSet.tmpInit();
            codeGen.RegSet.tmpPreAllocateTemps(TYP_LONG, 1);
            var temp = codeGen.RegSet.tmpGetTemp(TYP_LONG);
            temp.tdTempOffs = -32;
            codeGen.RegSet.tmpRlsTemp(temp);
            var tree = new GenTreeOp(GT_MUL, TYP_LONG,
                CodeGenShiftTests.Register(compiler, TYP_LONG, REG_RAX),
                CodeGenShiftTests.Register(compiler, TYP_LONG, REG_RCX))
            {
                RegNum = REG_RAX,
                Flags = GTF_OVERFLOW | GTF_SPILL | (unsigned ? GTF_UNSIGNED : GTF_EMPTY),
            };

            codeGen.genCodeForMul(tree);

            var descriptors = CodeGenShiftTests.Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(3));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(unsigned ? INS_mulEAX : INS_imul));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(unsigned ? INS_jb : INS_jo));
            Assert.That(EmitterJumpInstructionTests.JumpView.Target(descriptors[1]), Is.SameAs(target));
            Assert.That(descriptors[2].idIns(), Is.EqualTo(INS_mov));
            Assert.That((tree.Flags & GTF_SPILLED) != 0, Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    [NonParallelizable]
    public static void RegisterProductsUseApxOnlyWhenEnabled(bool ndd)
    {
        var saved = ApxNdd(ref JitConfig);
        ApxNdd(ref JitConfig) = ndd ? 1 : 0;
        try
        {
            CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
            {
                codeGen.Emitter.UsePromotedEvexEncodings = ndd;
                var tree = new GenTreeOp(GT_MUL, TYP_LONG,
                    CodeGenShiftTests.Register(compiler, TYP_LONG, REG_RAX),
                    CodeGenShiftTests.Register(compiler, TYP_LONG, REG_RCX))
                {
                    RegNum = REG_R8,
                };

                codeGen.genCodeForMul(tree);

                var descriptors = CodeGenShiftTests.Descriptors(codeGen);
                Assert.That(descriptors, Has.Count.EqualTo(ndd ? 1 : 2));
                Assert.That(descriptors[^1].idIns(), Is.EqualTo(INS_imul));
                Assert.That(descriptors[^1].idIsEvexNdContextSet(), Is.EqualTo(ndd));
            });
        }
        finally
        {
            ApxNdd(ref JitConfig) = saved;
        }
    }

    [Test]
    public static void ImulOpcodesPreserveEveryIntegerRegisterNumber()
    {
        for (var reg = REG_RAX; reg <= REG_R31; reg++)
        {
            var ins = Emitter.inst3opImulForReg(reg);
            Assert.That((int)ins - (int)INS_imul_AX, Is.EqualTo((int)reg));
            Assert.That(Emitter.instrIs3opImul(ins), Is.True);
        }
    }

    [Test]
    public static void CheckedMultiplyBranchesAroundTheInlineThrow()
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            var tree = new GenTreeOp(GT_MUL, TYP_INT,
                CodeGenShiftTests.Register(compiler, TYP_INT, REG_RAX),
                CodeGenShiftTests.Register(compiler, TYP_INT, REG_RCX))
            {
                RegNum = REG_RAX,
                Flags = GTF_OVERFLOW,
            };
            compiler.opts.compDbgCode = true;
            var firstGroup = codeGen.Emitter.emitCurIG;

            codeGen.genCodeForMul(tree);
            _ = CodeGenThrowHelperTests.AssertInlineThrow(firstGroup, INS_jno, 3);
        });
    }

#if DEBUG
    [TestCase(false)]
    [TestCase(true)]
    public static void DisassemblyRejectsBeforeMultiplyConsumption(bool high)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var tree = new GenTreeOp(high ? GT_MULHI : GT_MUL, TYP_INT,
                CodeGenShiftTests.Register(compiler, TYP_INT, REG_RAX),
                CodeGenShiftTests.Register(compiler, TYP_INT, REG_RCX))
            {
                RegNum = high ? REG_RDX : REG_RAX,
            };
            compiler.opts.dspCode = true;
            _ = Assert.Throws<FatalJitException>(() =>
            {
                if (high)
                {
                    codeGen.genCodeForMulHi(tree);
                }
                else
                {
                    codeGen.genCodeForMul(tree);
                }
            });
            compiler.opts.dspCode = false;
            if (high)
            {
                codeGen.genCodeForMulHi(tree);
            }
            else
            {
                codeGen.genCodeForMul(tree);
            }
            Assert.That(CodeGenShiftTests.Descriptors(codeGen), Has.Count.EqualTo(1));
        });
    }
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_enableApxNDD")]
    private static extern ref int ApxNdd(ref JitConfigValues config);
}
