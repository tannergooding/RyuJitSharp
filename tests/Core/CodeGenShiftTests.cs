// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CodeGenShiftTests
{
    [TestCase(1, true, false, INS_add, 1)]
    [TestCase(1, false, false, INS_lea, 1)]
    [TestCase(2, false, false, INS_lea, 1)]
    [TestCase(3, false, false, INS_lea, 1)]
    [TestCase(2, true, false, INS_shl_N, 1)]
    [TestCase(4, false, false, INS_shl_N, 2)]
    [TestCase(1, true, true, INS_shl_1, 1)]
    [TestCase(1, false, true, INS_shl_1, 2)]
    [TestCase(2, false, true, INS_shl_N, 2)]
    public static void LeftShiftShortcutsPreserveFlagsAndDestinationCopies(
        int count, bool sameRegister, bool setFlags, instruction expected, int instructionCount)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var value = Register(compiler, TYP_INT, REG_RAX);
            var amount = compiler.gtNewIconNode(TYP_INT, count);
            amount.IsContained = true;
            var tree = new GenTreeOp(GT_LSH, TYP_INT, value, amount)
            {
                RegNum = sameRegister ? REG_RAX : REG_RDX,
            };
            if (setFlags)
            {
                tree.Flags |= GTF_SET_FLAGS;
            }

            codeGen.genCodeForShift(tree);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(instructionCount));
            Assert.That(descriptors[^1].idIns(), Is.EqualTo(expected));
            Assert.That(descriptors[^1].idReg1(), Is.EqualTo(tree.RegNum));
            if (expected == INS_lea)
            {
                var address = descriptors[^1].idAddr().iiaAddrMode;
                Assert.That(address.amBaseReg, Is.EqualTo(count == 1 ? REG_RAX : REG_NA));
                Assert.That(address.amIndxReg, Is.EqualTo(REG_RAX));
                Assert.That(address.amScale, Is.EqualTo(count == 1 ? 0 : count));
            }
        });
    }

    [TestCase(GT_LSH, INS_shl, INS_shlx)]
    [TestCase(GT_RSH, INS_sar, INS_sarx)]
    [TestCase(GT_RSZ, INS_shr, INS_shrx)]
    public static void VariableShiftsSelectBmi2OnlyWhenFlagsPermit(
        genTreeOps oper, instruction legacy, instruction bmi2)
    {
        foreach (var setFlags in new[] { false, true })
        {
            WithCodeGen((compiler, codeGen) =>
            {
                EnableAvx2(compiler);
                var value = Register(compiler, TYP_LONG, REG_RAX);
                var amount = Register(compiler, TYP_INT, REG_R8);
                var tree = new GenTreeOp(oper, TYP_LONG, value, amount) { RegNum = REG_RDX };
                if (setFlags)
                {
                    tree.Flags |= GTF_SET_FLAGS;
                }

                codeGen.genCodeForShift(tree);

                var descriptors = Descriptors(codeGen);
                Assert.That(descriptors[^1].idIns(), Is.EqualTo(setFlags ? legacy : bmi2));
                Assert.That(descriptors[^1].idOpSize(), Is.EqualTo(EA_8BYTE));
                Assert.That(descriptors, Has.Count.EqualTo(setFlags ? 3 : 1));
                if (setFlags)
                {
                    Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_mov));
                    Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_RCX));
                    Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_R8));
                }
                else
                {
                    Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_R8));
                    Assert.That(descriptors[0].idReg3(), Is.EqualTo(REG_RAX));
                }
            });
        }
    }

    [TestCase(GT_LSH, INS_shl)]
    [TestCase(GT_RSH, INS_sar)]
    [TestCase(GT_RSZ, INS_shr)]
    [TestCase(GT_ROL, INS_rol)]
    [TestCase(GT_ROR, INS_ror)]
    public static void LegacyVariableCountsUseRcx(genTreeOps oper, instruction expected)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var tree = new GenTreeOp(oper, TYP_INT,
                Register(compiler, TYP_INT, REG_RAX), Register(compiler, TYP_INT, REG_R8))
            {
                RegNum = REG_RAX,
            };

            codeGen.genCodeForShift(tree);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_RCX));
            Assert.That(descriptors[^1].idIns(), Is.EqualTo(expected));
        });
    }

    [TestCase(GT_ROR, 7, 7)]
    [TestCase(GT_ROL, 7, 57)]
    [TestCase(GT_ROL, 0, 64)]
    [TestCase(GT_ROL, -1, 1)]
    [TestCase(GT_ROL, 65, 63)]
    public static void LongRotatesUseRorxWithNativeCountConversion(genTreeOps oper, int amount, int encoded)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            EnableAvx2(compiler);
            var count = compiler.gtNewIconNode(TYP_INT, amount);
            count.IsContained = true;
            var tree = new GenTreeOp(oper, TYP_LONG, Register(compiler, TYP_LONG, REG_RAX), count)
            {
                RegNum = REG_RDX,
            };

            codeGen.genCodeForShift(tree);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_rorx));
            Assert.That(InstructionConstant(codeGen.Emitter, descriptors[0]), Is.EqualTo((nint)encoded));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void Bmi2ReadsContainedLocalsForVariableShiftsAndImmediateRotates(bool rotate)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            EnableAvx2(compiler);
            var local = compiler.gtNewLclvNode(TYP_INT, 0);
            local.IsContained = true;
            var amount = Register(compiler, TYP_INT, rotate ? REG_NA : REG_R8);
            amount.IsContained = rotate;
            var tree = new GenTreeOp(rotate ? GT_ROR : GT_RSH, TYP_INT, local, amount)
            {
                RegNum = REG_RDX,
            };

            codeGen.genCodeForShift(tree);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(rotate ? INS_rorx : INS_sarx));
            Assert.That(descriptors[0].idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
        });
    }

    [TestCase(GT_LSH, 1, INS_shl_1)]
    [TestCase(GT_LSH, 4, INS_shl_N)]
    [TestCase(GT_RSH, 1, INS_sar_1)]
    [TestCase(GT_RSZ, 4, INS_shr_N)]
    [TestCase(GT_ROL, 1, INS_rol_1)]
    [TestCase(GT_ROR, 4, INS_ror_N)]
    [TestCase(GT_ROL, -1, INS_rol)]
    public static void MemoryShiftsRetainRmwForms(genTreeOps oper, int amount, instruction expected)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var address = new GenTreeLclFld(GT_LCL_ADDR, TYP_BYREF, 0, 0) { IsContained = true };
            var load = new GenTreeIndir(GT_IND, TYP_INT, address) { IsContained = true };
            var count = compiler.gtNewIconNode(TYP_INT, amount);
            if (amount >= 0)
            {
                count.IsContained = true;
            }
            else
            {
                count.RegNum = REG_R8;
            }
            var data = new GenTreeOp(oper, TYP_INT, load, count) { IsContained = true };
            var store = new GenTreeStoreInd(TYP_INT, address, data);

            codeGen.genCodeForShiftRMW(store);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(amount < 0 ? 2 : 1));
            Assert.That(descriptors[^1].idIns(), Is.EqualTo(expected));
            Assert.That(descriptors[^1].idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
        });
    }

    [TestCase(INS_rcl, INS_rcl_1, INS_rcl_N)]
    [TestCase(INS_rcr, INS_rcr_1, INS_rcr_N)]
    [TestCase(INS_rol, INS_rol_1, INS_rol_N)]
    [TestCase(INS_ror, INS_ror_1, INS_ror_N)]
    [TestCase(INS_shl, INS_shl_1, INS_shl_N)]
    [TestCase(INS_shr, INS_shr_1, INS_shr_N)]
    [TestCase(INS_sar, INS_sar_1, INS_sar_N)]
    public static void OpcodeMappingDistinguishesExactlyOne(instruction ins, instruction one, instruction many)
    {
        Assert.That(CodeGen.genMapShiftInsToShiftByConstantIns(ins, 1), Is.EqualTo(one));
        Assert.That(CodeGen.genMapShiftInsToShiftByConstantIns(ins, 0), Is.EqualTo(many));
        Assert.That(CodeGen.genMapShiftInsToShiftByConstantIns(ins, 33), Is.EqualTo(many));
    }

#if DEBUG
    [Test]
    public static void DisassemblyRecordsShiftOperandsOnce()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var value = Register(compiler, TYP_INT, REG_RAX);
            var count = Register(compiler, TYP_INT, REG_RCX);
            var tree = new GenTreeOp(GT_LSH, TYP_INT, value, count) { RegNum = REG_RDX };
            compiler.opts.dspCode = true;
            var diagnostic = InstructionRecordingTestSupport.Capture(() => codeGen.genCodeForShift(tree));
            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(2));
            Assert.That(diagnostic, Does.Contain("shl"));
        });
    }
#endif

    internal static GenTreeIntCon Register(Compiler compiler, var_types type, regNumber reg)
    {
        var node = compiler.gtNewIconNode(type, 7);
        node.RegNum = reg;
        return node;
    }

    internal static void EnableAvx2(Compiler compiler)
    {
        foreach (var isa in new[] { InstructionSet_AVX, InstructionSet_AVX2 })
        {
            compiler.opts.compSupportsISA.AddInstructionSet(isa);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(isa);
            compiler.opts.compSupportsISAReported.AddInstructionSet(isa);
        }
    }

    internal static void WithCodeGen(Action<Compiler, CodeGen> action, bool minopts = true)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI;
            compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);
            compiler.lvaTable[0].lvLRACandidate = false;
            codeGen.RegSet.ClearMaskVars();
            action(compiler, codeGen);
        }, minopts);
    }

    internal static List<Emitter.instrDesc> Descriptors(CodeGen codeGen)
        => CurrentDescriptors(codeGen.Emitter) ?? throw new AssertionException("Missing descriptor buffer.");

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "treeLifeUpdater")]
    private static extern ref TreeLifeUpdater? LifeUpdater(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsCns")]
    internal static extern nint InstructionConstant(Emitter emitter, Emitter.instrDesc descriptor);
}
