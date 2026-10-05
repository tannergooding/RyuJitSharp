// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.insBarrier;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class Arm64CodeGenLockedInstructionTests
{
    [TestCase(GT_XORR, INS_ldsetal)]
    [TestCase(GT_XAND, INS_ldclral)]
    [TestCase(GT_XCHG, INS_swpal)]
    [TestCase(GT_XADD, INS_ldaddal)]
    public static void AtomicFeatureUsesItsOperationSpecificInstruction(genTreeOps oper, instruction expected)
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            EnableAtomics(compiler);
            var tree = CreateAtomic(compiler, oper, TYP_INT, TYP_INT);
            if (oper is GT_XAND)
            {
                codeGen.InternalRegisters.Add(tree, RegisterMask(REG_R6));
            }

            codeGen.genCodeForTreeNode(tree);

            var descriptors = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Has.Count.EqualTo(oper is GT_XAND ? 2 : 1));
            Assert.That(descriptors[^1].idIns(), Is.EqualTo(expected));
            Assert.That(descriptors[^1].idOpSize(), Is.EqualTo(EA_4BYTE));
            if (oper is GT_XAND)
            {
                Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_mvn));
                Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R6));
                Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_R5));
                Assert.That(descriptors[1].idReg1(), Is.EqualTo(REG_R6));
                Assert.That(descriptors[1].idReg2(), Is.EqualTo(REG_R3));
                Assert.That(descriptors[1].idReg3(), Is.EqualTo(REG_R4));
            }
            else
            {
                Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R5));
                Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_R3));
                Assert.That(descriptors[0].idReg3(), Is.EqualTo(REG_R4));
            }
        });
    }

    [TestCase(TYP_BYTE, INS_swpalb, INS_sxtb)]
    [TestCase(TYP_UBYTE, INS_swpalb, INS_swpalb)]
    [TestCase(TYP_SHORT, INS_swpalh, INS_sxth)]
    [TestCase(TYP_USHORT, INS_swpalh, INS_swpalh)]
    public static void AtomicExchangeUsesTheCorrectWidthAndSignedResultExtension(var_types type,
        instruction expected, instruction expectedLast)
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            EnableAtomics(compiler);
            var tree = CreateAtomic(compiler, GT_XCHG, type, type.ActualType);

            codeGen.genCodeForTreeNode(tree);

            var descriptors = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter);
            Assert.That(descriptors[0].idIns(), Is.EqualTo(expected));
            Assert.That(descriptors[^1].idIns(), Is.EqualTo(expectedLast));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R5));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_R3));
            Assert.That(descriptors[0].idReg3(), Is.EqualTo(REG_R4));
            Assert.That(descriptors.Count, Is.EqualTo(type is TYP_BYTE or TYP_SHORT ? 2 : 1));
        });
    }

    [TestCase(TYP_BYTE, INS_casalb, INS_sxtb, 3, EA_4BYTE)]
    [TestCase(TYP_UBYTE, INS_casalb, INS_casalb, 2, EA_4BYTE)]
    [TestCase(TYP_SHORT, INS_casalh, INS_sxth, 3, EA_4BYTE)]
    [TestCase(TYP_USHORT, INS_casalh, INS_casalh, 2, EA_4BYTE)]
    [TestCase(TYP_INT, INS_casal, INS_casal, 2, EA_4BYTE)]
    [TestCase(TYP_UINT, INS_casal, INS_casal, 2, EA_4BYTE)]
    [TestCase(TYP_LONG, INS_casal, INS_casal, 2, EA_8BYTE)]
    [TestCase(TYP_ULONG, INS_casal, INS_casal, 2, EA_8BYTE)]
    public static void CompareExchangeUsesAtomicCompareAndSwapAndExtendsSignedResults(var_types type,
        instruction expectedAtomic, instruction expectedLast, int expectedCount, emitAttr expectedSize)
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            EnableAtomics(compiler);
            var tree = new GenTreeCmpXchg(type, Register(compiler, TYP_BYREF, REG_R4),
                Register(compiler, type, REG_R5), Register(compiler, type, REG_R6))
            {
                RegNum = REG_R3,
            };

            codeGen.genCodeForTreeNode(tree);

            var descriptors = Arm64CodeGenLocalVariableTests.AllDescriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(expectedCount));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_mov));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(expectedSize));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R3));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_R6));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(expectedAtomic));
            Assert.That(descriptors[1].idOpSize(), Is.EqualTo(expectedSize));
            Assert.That(descriptors[1].idReg1(), Is.EqualTo(REG_R3));
            Assert.That(descriptors[1].idReg2(), Is.EqualTo(REG_R5));
            Assert.That(descriptors[1].idReg3(), Is.EqualTo(REG_R4));
            Assert.That(descriptors[^1].idIns(), Is.EqualTo(expectedLast));
            if (type is TYP_BYTE or TYP_SHORT)
            {
                Assert.That(descriptors[^1].idOpSize(), Is.EqualTo(EA_4BYTE));
                Assert.That(descriptors[^1].idReg1(), Is.EqualTo(REG_R3));
                Assert.That(descriptors[^1].idReg2(), Is.EqualTo(REG_R3));
            }
        });
    }

    [TestCase(true, 0, INS_cbnz, 5, TYP_INT, EA_4BYTE, INS_ldaxr, INS_stlxr, INS_dmb, false)]
    [TestCase(true, 1, INS_cmp, 6, TYP_INT, EA_4BYTE, INS_ldaxr, INS_stlxr, INS_dmb, false)]
    [TestCase(false, 0, INS_cmp, 6, TYP_INT, EA_4BYTE, INS_ldaxr, INS_stlxr, INS_dmb, false)]
    [TestCase(true, 0, INS_cbnz, 6, TYP_BYTE, EA_4BYTE, INS_ldaxrb, INS_stlxrb, INS_sxtb, true)]
    [TestCase(false, 0, INS_cmp, 7, TYP_BYTE, EA_4BYTE, INS_ldaxrb, INS_stlxrb, INS_sxtb, true)]
    [TestCase(true, 0, INS_cbnz, 6, TYP_SHORT, EA_4BYTE, INS_ldaxrh, INS_stlxrh, INS_sxth, true)]
    [TestCase(true, 0, INS_cbnz, 5, TYP_UBYTE, EA_4BYTE, INS_ldaxrb, INS_stlxrb, INS_dmb, false)]
    [TestCase(true, 0, INS_cbnz, 5, TYP_USHORT, EA_4BYTE, INS_ldaxrh, INS_stlxrh, INS_dmb, false)]
    [TestCase(false, 0, INS_cmp, 6, TYP_LONG, EA_8BYTE, INS_ldaxr, INS_stlxr, INS_dmb, false)]
    public static void CompareExchangeExclusivePathChecksComparandAndRetriesStore(bool contained,
        int comparandValue, instruction expectedCompare, int expectedCount, var_types type, emitAttr expectedSize,
        instruction expectedLoad, instruction expectedStore, instruction expectedLast, bool expectedExtension)
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compCurBB = new BasicBlock(null, null);
            var address = Register(compiler, TYP_BYREF, REG_R4);
            var data = Register(compiler, type, REG_R5);
            var comparand = compiler.gtNewIconNode(type, comparandValue);
            if (contained)
            {
                comparand.IsContained = true;
                comparand.RegNum = REG_NA;
            }
            else
            {
                comparand.RegNum = REG_R6;
            }

            var tree = new GenTreeCmpXchg(type, address, data, comparand)
            {
                RegNum = REG_R3,
            };
            codeGen.InternalRegisters.Add(tree, RegisterMask(REG_R7));

            codeGen.genCodeForTreeNode(tree);
            Arm64CodeGenLocalVariableTests.SaveCurrentGroup(codeGen.Emitter);

            var descriptors = Arm64CodeGenLocalVariableTests.AllDescriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(expectedCount));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(expectedLoad));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(expectedSize));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R3));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_R4));

            var compareIndex = contained && comparandValue == 0 ? 1 : 2;
            if (expectedCompare == INS_cmp)
            {
                Assert.That(descriptors[1].idIns(), Is.EqualTo(INS_cmp));
                Assert.That(descriptors[1].idOpSize(), Is.EqualTo(expectedSize));
                Assert.That(descriptors[1].idReg1(), Is.EqualTo(REG_R3));
                if (contained)
                {
                    Assert.That(descriptors[1].idSmallCns(), Is.EqualTo(comparandValue));
                }
                else
                {
                    Assert.That(descriptors[1].idReg2(), Is.EqualTo(REG_R6));
                }
            }

            var barrierIndex = expectedCount - (expectedExtension ? 2 : 1);
            var compareFailBranch = (Emitter.instrDescJmp)descriptors[compareIndex];
            var storeIndex = barrierIndex - 2;
            var retryBranch = (Emitter.instrDescJmp)descriptors[barrierIndex - 1];
            Assert.That(descriptors[storeIndex].idIns(), Is.EqualTo(expectedStore));
            Assert.That(descriptors[storeIndex].idOpSize(), Is.EqualTo(expectedSize));
            Assert.That(descriptors[storeIndex].idReg1(), Is.EqualTo(REG_R7));
            Assert.That(descriptors[storeIndex].idReg2(), Is.EqualTo(REG_R5));
            Assert.That(descriptors[storeIndex].idReg3(), Is.EqualTo(REG_R4));
            Assert.That(compareFailBranch.idIns(), Is.EqualTo(expectedCompare is INS_cbnz ? INS_cbnz : INS_bne));
            Assert.That(retryBranch.idIns(), Is.EqualTo(INS_cbnz));
            Assert.That(retryBranch.idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(retryBranch.idReg1(), Is.EqualTo(REG_R7));
            Assert.That(compareFailBranch.idjTarget, Is.Not.SameAs(retryBranch.idjTarget));
            var retryTarget = retryBranch.idjTarget ?? throw new AssertionException("Missing retry target.");
            var compareFailTarget = compareFailBranch.idjTarget ?? throw new AssertionException("Missing compare-fail target.");
            Assert.That(retryTarget.HasFlag(BBF_HAS_LABEL), Is.True);
            Assert.That(compareFailTarget.HasFlag(BBF_HAS_LABEL), Is.True);
            Assert.That(new regMaskTP((regMask)retryTarget.bbEmitCookie!.igByrefRegs()),
                Is.EqualTo(RegisterMask(REG_R4)));
            Assert.That(new regMaskTP((regMask)compareFailTarget.bbEmitCookie!.igByrefRegs()),
                Is.EqualTo(RegisterMask(REG_R4)));
            Assert.That(descriptors[barrierIndex].idIns(), Is.EqualTo(INS_dmb));
            Assert.That(descriptors[barrierIndex].idSmallCns(), Is.EqualTo((int)INS_BARRIER_ISH));
            Assert.That(descriptors[^1].idIns(), Is.EqualTo(expectedLast));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(default(regMaskTP)));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ExclusiveAddRecordsItsConditionalRetryBranch(bool useImmediate)
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compCurBB = new BasicBlock(null, null);
            var address = Register(compiler, TYP_BYREF, REG_R4);
            var data = compiler.gtNewIconNode(TYP_INT, 7);
            data.IsContained = useImmediate;
            data.RegNum = useImmediate ? REG_NA : REG_R5;
            var tree = new GenTreeIndir(GT_XADD, TYP_INT, address, data)
            {
                RegNum = REG_R3,
            };
            codeGen.InternalRegisters.Add(tree, RegisterMask(REG_R6) | RegisterMask(REG_R7));

            codeGen.genCodeForTreeNode(tree);

            var descriptors = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Has.Count.EqualTo(5));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_ldaxr));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R3));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_R4));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(INS_add));
            Assert.That(descriptors[1].idReg1(), Is.EqualTo(REG_R7));
            Assert.That(descriptors[1].idReg2(), Is.EqualTo(REG_R3));
            if (useImmediate)
            {
                Assert.That(descriptors[1].idSmallCns(), Is.EqualTo(7));
            }
            else
            {
                Assert.That(descriptors[1].idReg3(), Is.EqualTo(REG_R5));
            }
            Assert.That(descriptors[2].idIns(), Is.EqualTo(INS_stlxr));
            Assert.That(descriptors[2].idReg1(), Is.EqualTo(REG_R6));
            Assert.That(descriptors[2].idReg2(), Is.EqualTo(REG_R7));
            Assert.That(descriptors[2].idReg3(), Is.EqualTo(REG_R4));
            var branch = (Emitter.instrDescJmp)descriptors[3];
            var retryTarget = branch.idjTarget ?? throw new AssertionException("Missing retry target.");
            Assert.That(descriptors[3].idIns(), Is.EqualTo(INS_cbnz));
            Assert.That(descriptors[3].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptors[3].idReg1(), Is.EqualTo(REG_R6));
            Assert.That(retryTarget.HasFlag(BBF_HAS_LABEL), Is.True);
            Assert.That(descriptors[4].idIns(), Is.EqualTo(INS_dmb));
            Assert.That(descriptors[4].idSmallCns(), Is.EqualTo((int)INS_BARRIER_ISH));
        });
    }

    private static GenTreeIndir CreateAtomic(Compiler compiler, genTreeOps oper, var_types type, var_types dataType)
    {
        return new GenTreeIndir(oper, type, Register(compiler, TYP_BYREF, REG_R4),
            Register(compiler, dataType, REG_R5))
        {
            RegNum = REG_R3,
        };
    }

    private static GenTreeIntCon Register(Compiler compiler, var_types type, regNumber reg)
    {
        var tree = compiler.gtNewIconNode(type, 7);
        tree.RegNum = reg;

        return tree;
    }

    private static regMaskTP RegisterMask(regNumber reg)
    {
        return regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
    }

    private static void EnableAtomics(Compiler compiler)
    {
        compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_Atomics);
        compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_Atomics);
        compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_Atomics);
    }
}
#endif
