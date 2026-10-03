// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64 || TARGET_RISCV64
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CorJitResult;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.InfoAccessType;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LoongArchRiscVLocalCodeGenTests
{
    [Test]
    public static void LocalLoadsSkipAllocatorManagedRegisters()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].Type = TYP_INT;
            compiler.lvaTable[0].lvLRACandidate = true;
            var tree = new GenTreeLclVar(TYP_INT, 0) { RegNum = REG_S0 };

            Assert.DoesNotThrow(() => codeGen.genCodeForLclVar(tree));
        });
    }

    [Test]
    public static void StackLocalLoadStopsAtTheTargetEmitterBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].Type = TYP_INT;
            var tree = new GenTreeLclVar(TYP_INT, 0) { RegNum = REG_S0 };

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForLclVar(tree));

            Assert.That(failure?.Message, Does.Contain("Target local-stack instruction recording is not implemented."));
        });
    }

    [Test]
    public static void LocalFieldStoreStopsAtTheTargetEmitterBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].Type = TYP_LONG;
            var zero = new GenTreeIntCon(TYP_INT, 0) { IsContained = true };
            var tree = new GenTreeLclFld(TYP_INT, 0, 4, zero, null) { RegNum = REG_NA };

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForStoreLclFld(tree));

            Assert.That(failure?.Message, Does.Contain("Target local-stack store recording is not implemented."));
        });
    }

    [Test]
    public static void LocalVariableStoreStopsAtTheTargetStackEmitterBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].Type = TYP_INT;
            var zero = new GenTreeIntCon(TYP_INT, 0) { IsContained = true };
            var tree = new GenTreeLclVar(TYP_INT, 0, zero) { RegNum = REG_NA };

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForStoreLclVar(tree));

            Assert.That(failure?.Message, Does.Contain("Target local-stack store recording is not implemented."));
        });
    }

    [TestCase(false, 0)]
    [TestCase(false, 1)]
    [TestCase(false, 2)]
    [TestCase(true, 0)]
    public static void InitBlockUnrollPreservesVolatileAndAddressBoundaries(bool isVolatile, int destinationKind)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].Type = TYP_STRUCT;
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);
            GenTree destination;
            if (destinationKind is 2)
            {
                var baseAddress = compiler.gtNewLclAddrNode(TYP_BYREF, 0, 0) { RegNum = REG_S0 };
                destination = new GenTreeAddrMode(TYP_BYREF, baseAddress, null, 0, 8)
                {
                    IsContained = true,
                };
            }
            else
            {
                destination = compiler.gtNewLclAddrNode(TYP_BYREF, 0, 0)
                {
                    IsContained = destinationKind is 0,
                    RegNum = destinationKind is 0 ? REG_NA : REG_S0,
                };
            }

            var zero = new GenTreeIntCon(TYP_INT, 0) { IsContained = true };
            var tree = new GenTreeBlk(TYP_STRUCT, destination, zero, new ClassLayout(16)) { RegNum = REG_NA };
            if (isVolatile)
            {
                tree.Flags |= GenTreeFlags.GTF_IND_VOLATILE;
            }

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForInitBlkUnroll(tree));
#if TARGET_LOONGARCH64
            var expectedBoundary = isVolatile
                ? "LoongArch64 memory barrier emission is not ported."
                : destinationKind is 0
                    ? "Target local-stack store recording is not implemented."
                    : "Target two-register-immediate instruction recording is not implemented.";
#else
            var expectedBoundary = isVolatile
                ? "RISC-V64 memory barrier emission is not ported."
                : destinationKind is 0
                    ? "Target local-stack store recording is not implemented."
                    : "Target two-register-immediate instruction recording is not implemented.";
#endif
            Assert.That(failure?.Message, Does.Contain(expectedBoundary));
        });
    }

#if TARGET_RISCV64
    [TestCase(false)]
    [TestCase(true)]
#else
    [TestCase(false)]
#endif
    public static void TableBasedSwitchPreservesTargetFeatureBranches(bool useZba)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            if (useZba)
            {
                compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_Zba);
                compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_Zba);
                compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_Zba);
            }

            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);
            compiler.fgFirstBB = new BasicBlock(null, null);
            var index = CodeGenShiftTests.Register(compiler, TYP_I_IMPL, REG_S0);
            var table = CodeGenShiftTests.Register(compiler, TYP_I_IMPL, REG_S1);
            var tree = new GenTreeOp(GT_SWITCH_TABLE, TYP_VOID, index, table);
            codeGen.InternalRegisters.Add(tree, regMaskTP.CreateFromRegNum(REG_S2, REG_S2.SingleTypeMask));

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree));
#if TARGET_LOONGARCH64
            Assert.That(failure?.Message,
                Does.Contain("Target two-register-immediate instruction recording is not implemented."));
#else
            var expectedBoundary = useZba
                ? "RISC-V three-register instruction recording is not implemented."
                : "Target two-register-immediate instruction recording is not implemented.";
            Assert.That(failure?.Message, Does.Contain(expectedBoundary));
#endif
        });
    }

    [Test]
    public static void SwitchTableBlockAddressStopsAtTheTargetEmitterBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var target = new BasicBlock(null, null);
            var failure = Assert.Throws<FatalJitException>(() =>
                codeGen.Emitter.emitIns_R_L(INS_lea, EA_PTRSIZE, target, REG_S2));
#if TARGET_LOONGARCH64
            Assert.That(failure?.Message,
                Does.Contain("LoongArch64 block-relative address recording is not ported."));
#else
            Assert.That(failure?.Message,
                Does.Contain("RISC-V64 block-relative address recording is not ported."));
#endif
        });
    }

    [Test]
    public static void JumpTableGenerationStopsAtTheSharedRecordingBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var tree = new GenTree(GT_JMPTABLE, TYP_I_IMPL) { RegNum = REG_S0 };
            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree));

            Assert.That(failure?.Message, Does.Contain("Instruction recording outside AMD64 is not ported."));
        });
    }

    [Test]
    public static void AsyncResumeInfoDispatchReachesTheSharedRecordingBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var tree = new GenTreeVal(GT_ASYNC_RESUME_INFO, TYP_I_IMPL, 0) { RegNum = REG_S0 };
            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree));

            Assert.That(failure?.Message, Does.Contain("Instruction recording outside AMD64 is not ported."));
        });
    }

#if TARGET_LOONGARCH64
    [TestCase(0, "LoongArch64 conditional-branch instruction recording is not ported.")]
    [TestCase(1, "Target immediate materialization is not implemented.")]
    public static void JumpCompareDispatchPreservesImmediateAndBranchBoundaries(
        long immediate, string expectedBoundary)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var trueTarget = new BasicBlock(null, null);
            var falseTarget = new BasicBlock(null, null);
            var block = new BasicBlock(null, null);
            block.SetCond(new FlowEdge(block, trueTarget, null), new FlowEdge(block, falseTarget, null));
            compiler.compCurBB = block;
            var value = CodeGenShiftTests.Register(compiler, TYP_LONG, REG_S0);
            var constant = new GenTreeIntCon(TYP_LONG, unchecked((nint)immediate)) { IsContained = true };
            var tree = new GenTreeOpCC(GT_JCMP, TYP_VOID, new GenCondition(GenCondition.CodeKind.EQ),
                value, constant);
            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree));

            Assert.That(failure?.Message, Does.Contain(expectedBoundary));
        });
    }
#endif

#if TARGET_RISCV64
    [TestCase(GenCondition.CodeKind.EQ)]
    [TestCase(GenCondition.CodeKind.SGT)]
    [TestCase(GenCondition.CodeKind.ULE)]
    public static void JumpCompareDispatchPreservesRiscVConditionalBranchBoundary(
        GenCondition.CodeKind conditionCode)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);
            var trueTarget = new BasicBlock(null, null);
            var falseTarget = new BasicBlock(null, null);
            var block = new BasicBlock(null, null);
            block.SetCond(new FlowEdge(block, trueTarget, null), new FlowEdge(block, falseTarget, null));
            compiler.compCurBB = block;
            var first = CodeGenShiftTests.Register(compiler, TYP_LONG, REG_S0);
            var second = CodeGenShiftTests.Register(compiler, TYP_LONG, REG_S1);
            var tree = new GenTreeOpCC(GT_JCMP, TYP_VOID, new GenCondition(conditionCode), first, second);

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree));

            Assert.That(failure?.Message,
                Does.Contain("Target conditional-branch recording is not implemented."));
        });
    }
#endif

#if TARGET_LOONGARCH64 && FEATURE_SIMD
    [Test]
    public static void SimdInstructionOptionsPreserveTheLoongArchNyiBoundary()
    {
        WithCodeGen((_, codeGen) =>
        {
            var failure = Assert.Throws<FatalJitException>(() =>
                codeGen.genGetSimdInsOpt(EA_16BYTE, TYP_SIMD16));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
        });
    }

    [Test]
    public static void SimdUpperSavePreservesTheLoongArchNyiBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].Type = TYP_SIMD16;
            var source = compiler.gtNewLclvNode(TYP_SIMD16, 0);
            var node = new GenTreeIntrinsic(TYP_SIMD16, source, NamedIntrinsic.NI_SIMD_UpperSave, null);

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genSimdUpperSave(node));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
        });
    }

    [Test]
    public static void SimdUpperRestorePreservesTheLoongArchNyiBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].Type = TYP_SIMD16;
            var source = compiler.gtNewLclvNode(TYP_SIMD16, 0);
            var node = new GenTreeIntrinsic(TYP_SIMD16, source, NamedIntrinsic.NI_SIMD_UpperRestore, null);

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genSimdUpperRestore(node));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
        });
    }
#endif

#if TARGET_LOONGARCH64 || TARGET_RISCV64
    [Test]
    public static void HelperCallGenerationReachesTheTargetCallRecordingBoundary()
    {
        WithCodeGen((_, codeGen) =>
        {
            var failure = Assert.Throws<FatalJitException>(() =>
                codeGen.genEmitHelperCall(CORINFO_HELP_ASSIGN_REF, 0, EA_PTRSIZE));

            Assert.That(failure?.Message,
                Does.Contain("Target call instruction recording is not implemented."));
        });
    }
#endif

#if TARGET_RISCV64
    [TestCase(true, "RISC-V64 relocated-address load recording is not ported.")]
    [TestCase(false, "RISC-V64 helper-address load recording is not ported.")]
    public static void IndirectHelperLookupPreservesRiscVAddressBoundaries(bool useRelocation, string expectedBoundary)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.getHelperFtn = &GetHelperFtn;
            var context = new HelperContext
            {
                JitInfo = new ICorJitInfo { lpVtbl = &vtable },
                Address = (void*)0x1234,
            };
            compiler.info.compCompHnd = &context.JitInfo;
            compiler.info.compMatchedVM = true;
            compiler.opts.compReloc = useRelocation;

            var failure = Assert.Throws<FatalJitException>(() =>
                codeGen.genEmitHelperCall(CORINFO_HELP_ASSIGN_REF, 0, EA_PTRSIZE));

            Assert.That(failure?.Message, Does.Contain(expectedBoundary));
        });
    }

    [Test]
    public static void ZicondSelectDispatchReachesRiscVInstructionRecordingBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_Zicond);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_Zicond);
            compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_Zicond);
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);

            var condition = CodeGenShiftTests.Register(compiler, TYP_INT, REG_S0);
            var trueValue = CodeGenShiftTests.Register(compiler, TYP_INT, REG_S1);
            var falseValue = new GenTreeIntCon(TYP_INT, 0) { IsContained = true };
            var tree = new GenTreeConditional(GT_SELECT, TYP_INT, condition, trueValue, falseValue)
            {
                RegNum = REG_S2,
            };

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree));

            Assert.That(failure?.Message,
                Does.Contain("RISC-V three-register instruction recording is not implemented."));
        });
    }
#endif

    [Test]
    public static void FunctionEntryDispatchReachesTheInstructionGroupRecordingBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var tree = new GenTree(GT_FTN_ENTRY, TYP_I_IMPL) { RegNum = REG_S0 };
            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree));

            Assert.That(failure?.Message, Does.Contain("Instruction-group address recording requires xarch."));
        });
    }

    [Test]
    public static void NonlocalJumpDispatchPreservesTailCallStateAtTheInstructionBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);
            var operand = CodeGenShiftTests.Register(compiler, TYP_I_IMPL, REG_S0);
            var tree = new GenTreeUnOp(GT_NONLOCAL_JMP, TYP_VOID, operand);

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree));

            Assert.That(codeGen.HasTailCalls, Is.True);
            Assert.That(failure?.Message,
                Does.Contain("Target two-register-immediate instruction recording is not implemented."));
        });
    }

#if TARGET_RISCV64
    [TestCase(GT_XADD)]
    [TestCase(GT_XCHG)]
    [TestCase(GT_XORR)]
    [TestCase(GT_XAND)]
#else
    [TestCase(GT_XADD)]
    [TestCase(GT_XCHG)]
#endif
    public static void LockedInstructionDispatchReachesTheTargetBoundary(genTreeOps oper)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);
            var address = CodeGenShiftTests.Register(compiler, TYP_BYREF, REG_S0);
            var data = CodeGenShiftTests.Register(compiler, TYP_INT, REG_S1);
            var tree = new GenTreeOp(oper, TYP_INT, address, data) { RegNum = REG_S2 };

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree));
#if TARGET_LOONGARCH64
            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
#else
            Assert.That(failure?.Message,
                Does.Contain("RISC-V three-register instruction recording is not implemented."));
#endif
        });
    }

#if TARGET_RISCV64
    [TestCase(TYP_LONG, "RISC-V three-register instruction recording is not implemented.")]
    [TestCase(TYP_INT, "Target two-register instruction recording is not implemented.")]
#else
    [TestCase(TYP_LONG, "unimplemented on LOONGARCH64 yet")]
#endif
    public static void CompareExchangeDispatchPreservesTargetAtomicBoundaries(
        var_types comparandType,
        string expectedBoundary)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);

            var address = CodeGenShiftTests.Register(compiler, TYP_BYREF, REG_S0);
            var value = CodeGenShiftTests.Register(compiler, TYP_LONG, REG_S1);
            var comparand = CodeGenShiftTests.Register(compiler, comparandType, REG_S2);
            var tree = new GenTreeCmpXchg(TYP_LONG, address, value, comparand) { RegNum = REG_S4 };
            var internalRegisters = regMaskTP.CreateFromRegNum(REG_S3, REG_S3.SingleTypeMask);
            if (comparandType is TYP_INT)
            {
                internalRegisters |= regMaskTP.CreateFromRegNum(REG_S5, REG_S5.SingleTypeMask);
            }
            codeGen.InternalRegisters.Add(tree, internalRegisters);

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree));
            Assert.That(failure?.Message, Does.Contain(expectedBoundary));
        });
    }

#if TARGET_RISCV64 && FEATURE_SIMD
    [Test]
    public static void SimdInstructionOptionsPreserveTheRiscVNyiBoundary()
    {
        WithCodeGen((_, codeGen) =>
        {
            var failure = Assert.Throws<FatalJitException>(() =>
                codeGen.genGetSimdInsOpt(EA_16BYTE, TYP_SIMD16));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
        });
    }

    [Test]
    public static void SimdOpcodeLookupPreservesTheRiscVNyiBoundary()
    {
        WithCodeGen((_, codeGen) =>
        {
            var failure = Assert.Throws<FatalJitException>(() =>
                codeGen.getOpForSIMDIntrinsic(0, TYP_INT));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
        });
    }

    [Test]
    public static void SimdIntrinsicInitializationPreservesTheRiscVNyiBoundary()
    {
        WithCodeGen((_, codeGen) =>
        {
            var node = new GenTreeVecCon(TYP_SIMD16);
            var failure = Assert.Throws<FatalJitException>(() => codeGen.genSIMDIntrinsicInit(node));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
        });
    }

    [Test]
    public static void SimdIntrinsicMultiInitializationPreservesTheRiscVNyiBoundary()
    {
        WithCodeGen((_, codeGen) =>
        {
            var node = new GenTreeVecCon(TYP_SIMD16);
            var failure = Assert.Throws<FatalJitException>(() => codeGen.genSIMDIntrinsicInitN(node));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
        });
    }

    [Test]
    public static void SimdUnaryIntrinsicPreservesTheRiscVNyiBoundary()
    {
        WithCodeGen((_, codeGen) =>
        {
            var node = new GenTreeVecCon(TYP_SIMD16);
            var failure = Assert.Throws<FatalJitException>(() => codeGen.genSIMDIntrinsicUnOp(node));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
        });
    }

    [Test]
    public static void SimdWidenPreservesTheRiscVNyiBoundary()
    {
        WithCodeGen((_, codeGen) =>
        {
            var node = new GenTreeVecCon(TYP_SIMD16);
            var failure = Assert.Throws<FatalJitException>(() => codeGen.genSIMDIntrinsicWiden(node));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
        });
    }
#endif

#if TARGET_RISCV64
    [Test]
    public static void CompareExchangeRetryBranchStopsAtTheTargetConditionalBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var target = new BasicBlock(null, null);
            var failure = Assert.Throws<FatalJitException>(() =>
                codeGen.Emitter.emitIns_J_cond_la(INS_bnez, target, REG_S3));

            Assert.That(failure?.Message,
                Does.Contain("RISC-V one-register conditional-branch recording is not implemented."));
        });
    }
#endif

    [Test]
    public static void ReturnTrapDispatchStopsAtTheTargetConditionalBranchBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);
            var data = CodeGenShiftTests.Register(compiler, TYP_I_IMPL, REG_S0);
            var tree = new GenTreeUnOp(GT_RETURNTRAP, TYP_VOID, data);

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree));

            Assert.That(failure?.Message,
                Does.Contain("Target conditional-branch recording is not implemented."));
        });
    }

#if TARGET_LOONGARCH64
    [Test]
    public static void ReturnTrapRelocatedAddressStopsAtTheTargetEmitterBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var failure = Assert.Throws<FatalJitException>(() =>
                codeGen.Emitter.emitIns_R_AI(INS_bl, EA_PTR_DSP_RELOC, REG_S3, 0));

            Assert.That(failure?.Message,
                Does.Contain("LoongArch64 relocated-address instruction recording is not ported."));
        });
    }
#else
    [Test]
    public static void ReturnTrapHelperAddressLoadStopsAtTheTargetEmitterBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var failure = Assert.Throws<FatalJitException>(() =>
                codeGen.Emitter.emitIns_R_R_Addr(INS_ld, EA_PTRSIZE, REG_S3, REG_S3, null));

            Assert.That(failure?.Message,
                Does.Contain("RISC-V64 helper-address load recording is not ported."));
        });
    }
#endif

    [Test]
    public static void JumpTableAddressRecordingStopsAtTheTargetEmitterBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var fieldHandle = Compiler.eeFindJitDataOffs(0);
#if TARGET_LOONGARCH64
            var failure = Assert.Throws<FatalJitException>(() => codeGen.Emitter.emitIns_R_C(
                INS_bl, EA_PTRSIZE, REG_S0, REG_NA, fieldHandle, 0));
            Assert.That(failure?.Message,
                Does.Contain("LoongArch64 embedded-data instruction recording is not ported."));
#else
            var failure = Assert.Throws<FatalJitException>(() => codeGen.Emitter.emitIns_R_C(
                INS_addi, EA_PTRSIZE, REG_S0, REG_NA, fieldHandle));
            Assert.That(failure?.Message,
                Does.Contain("RISC-V64 embedded-data instruction recording is not ported."));
#endif
        });
    }

    [Test]
    public static void LocalVariableStoreStopsAtTheTargetConstantEmitterBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].Type = TYP_INT;
            var constant = new GenTreeIntCon(TYP_INT, 12) { IsContained = true };
#if TARGET_LOONGARCH64
            var tree = new GenTreeLclVar(TYP_INT, 0, constant) { RegNum = REG_NA };
#else
            var tree = new GenTreeLclVar(TYP_INT, 0, constant) { RegNum = REG_S0 };
#endif

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForStoreLclVar(tree));
#if TARGET_LOONGARCH64
            Assert.That(failure?.Message, Does.Contain("LoongArch64 address constant recording is not ported."));
#else
            Assert.That(failure?.Message, Does.Contain("RISC-V64 immediate materialization is not ported."));
#endif
        });
    }

    [Test]
    public static void LocalHeapDispatchReachesTheTargetEmitterBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.compLocallocUsed = true;
            codeGen.resetFramePointerUsedWritePhase();
            codeGen.IsFramePointerUsed = true;
            var size = new GenTreeIntCon(TYP_I_IMPL, 16) { IsContained = true };
            var tree = new GenTreeUnOp(GT_LCLHEAP, TYP_I_IMPL, size) { RegNum = REG_S0 };

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genLclHeap(tree));

            Assert.That(failure?.Message, Does.Contain(
                "Target two-register-immediate instruction recording is not implemented."));
        });
    }

    [TestCase(GT_NEG, TYP_INT)]
    [TestCase(GT_NOT, TYP_INT)]
#if TARGET_LOONGARCH64
    [TestCase(GT_NEG, TYP_FLOAT)]
    [TestCase(GT_NEG, TYP_DOUBLE)]
#endif
    public static void UnaryCodegenReachesTheTargetInstructionBoundary(genTreeOps oper, var_types type)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);

            var isFloating = type is TYP_FLOAT or TYP_DOUBLE;
            GenTree operand = isFloating
                ? compiler.gtNewDconNode(type, 1.0)
                : compiler.gtNewIconNode(type, 1);
            operand.RegNum = isFloating ? REG_F0 : REG_S0;
            var tree = compiler.gtNewUnaryNode(oper, type, operand);
            tree.RegNum = isFloating ? REG_F2 : REG_S1;

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForNegNot(tree.AsUnOp()));
#if TARGET_LOONGARCH64
            var expectedBoundary = oper is GT_NOT
                ? "Target two-register instruction recording is not implemented."
                : "LoongArch64 three-register instruction recording is not ported.";
            Assert.That(failure?.Message, Does.Contain(expectedBoundary));
#else
            Assert.That(failure?.Result, Is.EqualTo(CORJIT_INTERNALERROR));
#endif
        });
    }

    [TestCase(GT_BSWAP, TYP_INT)]
    [TestCase(GT_BSWAP16, TYP_INT)]
    [TestCase(GT_BSWAP, TYP_LONG)]
    public static void ByteSwapCodegenReachesTheTargetInstructionBoundary(genTreeOps oper, var_types type)
    {
        WithCodeGen((compiler, codeGen) =>
        {
#if TARGET_RISCV64
            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_Zbb);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_Zbb);
            compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_Zbb);
#endif
            var operand = compiler.gtNewIconNode(type, 1);
            operand.RegNum = REG_S0;
            var tree = compiler.gtNewUnaryNode(oper, type, operand);
            tree.RegNum = REG_S1;

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForBswap(tree));

            Assert.That(failure?.Message, Does.Contain("Target two-register instruction recording is not implemented."));
        });
    }

    [TestCase(GT_DIV, TYP_DOUBLE)]
    [TestCase(GT_UDIV, TYP_LONG)]
    public static void DivisionCodegenReachesTheTargetInstructionBoundary(genTreeOps oper, var_types type)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);

            var isFloating = type is TYP_FLOAT or TYP_DOUBLE;
            GenTree dividend = isFloating
                ? compiler.gtNewDconNode(type, 1.0)
                : compiler.gtNewLconNode(7);
            GenTree divisor = isFloating
                ? compiler.gtNewDconNode(type, 2.0)
                : compiler.gtNewLconNode(3);

            dividend.RegNum = isFloating ? REG_F0 : REG_S0;
            divisor.RegNum = isFloating ? REG_F1 : REG_S1;

            var tree = compiler.gtNewBinaryNode(oper, type, dividend, divisor);
            tree.RegNum = isFloating ? REG_F2 : REG_S2;
            if (oper is GT_UDIV)
            {
                tree.Flags |= GenTreeFlags.GTF_DIV_MOD_NO_BY_ZERO;
            }

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForDivMod(tree));
#if TARGET_LOONGARCH64
            Assert.That(failure?.Message,
                Does.Contain("LoongArch64 three-register instruction recording is not ported."));
#else
            Assert.That(failure?.Message,
                Does.Contain("RISC-V three-register instruction recording is not implemented."));
#endif
        });
    }

    [TestCase(TYP_INT, TYP_FLOAT, false)]
    [TestCase(TYP_LONG, TYP_DOUBLE, false)]
    [TestCase(TYP_UINT, TYP_FLOAT, true)]
    [TestCase(TYP_ULONG, TYP_DOUBLE, true)]
    public static void IntToFloatCastDispatchReachesTheTargetInstructionBoundary(
        var_types sourceType, var_types destinationType, bool isUnsigned)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);
            var source = CodeGenShiftTests.Register(compiler, sourceType, REG_S0);
            var cast = new GenTreeCast(destinationType, source, isUnsigned, destinationType)
            {
                RegNum = REG_F0,
            };

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForCast(cast));

            Assert.That(failure?.Message, Does.Contain("Target two-register instruction recording is not implemented."));
        });
    }

    [TestCase(TYP_FLOAT, TYP_INT, false)]
    [TestCase(TYP_DOUBLE, TYP_LONG, false)]
    [TestCase(TYP_FLOAT, TYP_UINT, true)]
    [TestCase(TYP_DOUBLE, TYP_ULONG, true)]
    public static void FloatToIntCastDispatchReachesTheTargetInstructionBoundary(
        var_types sourceType, var_types destinationType, bool isUnsigned)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);
            var source = compiler.gtNewDconNode(sourceType, 1.5);
            source.RegNum = REG_F0;

            var cast = new GenTreeCast(destinationType, source, isUnsigned, destinationType)
            {
                RegNum = REG_S0,
            };

#if TARGET_RISCV64
            codeGen.InternalRegisters.Add(cast, regMaskTP.CreateFromRegNum(REG_S2, REG_S2.SingleTypeMask));
#endif

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForCast(cast));
#if TARGET_LOONGARCH64
            var expectedMessage = isUnsigned
                ? "Target two-register-immediate instruction recording is not implemented."
                : "Target two-register instruction recording is not implemented.";
#else
            const string expectedMessage = "Target two-register instruction recording is not implemented.";
#endif
            Assert.That(failure?.Message, Does.Contain(expectedMessage));
        });
    }

    [TestCase(TYP_FLOAT)]
    [TestCase(TYP_DOUBLE)]
    public static void CkfiniteDispatchReachesTheTargetInstructionBoundary(var_types type)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);
            var operand = compiler.gtNewDconNode(type, -0.0);
            operand.RegNum = REG_F0;

            var tree = compiler.gtNewUnaryNode(GT_CKFINITE, type, operand);
            tree.RegNum = REG_F1;
#if TARGET_LOONGARCH64
            codeGen.InternalRegisters.Add(tree, regMaskTP.CreateFromRegNum(REG_R21, REG_R21.SingleTypeMask));
#else
            codeGen.InternalRegisters.Add(tree, regMaskTP.CreateFromRegNum(REG_S2, REG_S2.SingleTypeMask));
#endif

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree));

            Assert.That(failure?.Message, Does.Contain("Target two-register instruction recording is not implemented."));
        });
    }

    [Test]
    public static void IntegerCompareDispatchReachesTheTargetImmediateBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);

            var op1 = CodeGenShiftTests.Register(compiler, TYP_LONG, REG_S0);
            var op2 = new GenTreeIntCon(TYP_LONG, 5) { IsContained = true };
            var tree = compiler.gtNewBinaryNode(GT_LT, TYP_INT, op1, op2) { RegNum = REG_S1 };

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree));

            Assert.That(failure?.Message,
                Does.Contain("Target two-register-immediate instruction recording is not implemented."));
        });
    }

    [TestCase(TYP_FLOAT)]
    [TestCase(TYP_DOUBLE)]
    public static void FloatingCompareDispatchPreservesUnorderedTargetBoundary(var_types type)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);

            var op1 = compiler.gtNewDconNode(type, 1.0);
            op1.RegNum = REG_F0;
            var op2 = compiler.gtNewDconNode(type, 2.0);
            op2.RegNum = REG_F1;
            var tree = compiler.gtNewBinaryNode(GT_LT, TYP_INT, op1, op2) { RegNum = REG_S1 };
            tree.Flags |= GTF_RELOP_NAN_UN;

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree));
#if TARGET_LOONGARCH64
            Assert.That(failure?.Message,
                Does.Contain("Target two-register-immediate instruction recording is not implemented."));
#else
            Assert.That(failure?.Message,
                Does.Contain("RISC-V three-register instruction recording is not implemented."));
#endif
        });
    }

#if FEATURE_SIMD
    [Test]
    public static void Simd12LocalVariableStoreStopsAtTheTargetBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].Type = TYP_SIMD12;
            var zero = new GenTreeIntCon(TYP_INT, 0) { IsContained = true };
            var tree = new GenTreeLclVar(TYP_SIMD12, 0, zero) { RegNum = REG_NA };

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForStoreLclVar(tree));

#if TARGET_LOONGARCH64
            Assert.That(failure?.Message, Does.Contain("LoongArch64 SIMD12 local-store recording is not ported."));
#else
            Assert.That(failure?.Message, Does.Contain("RISC-V SIMD12 local-store recording is not ported."));
#endif
        });
    }
#endif

    [Test]
    public static void IndirectStorePreservesVolatileBarrierBeforeTargetRecording()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var address = Register(compiler, TYP_BYREF, REG_S0);
            var data = Register(compiler, TYP_INT, REG_S1);
            var store = new GenTreeStoreInd(TYP_INT, address, data)
            {
                Flags = GTF_IND_VOLATILE,
            };
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForStoreInd(store));

#if TARGET_LOONGARCH64
            Assert.That(failure?.Message, Does.Contain("LoongArch64 memory barrier emission is not ported."));
#else
            Assert.That(failure?.Message, Does.Contain("RISC-V64 memory barrier emission is not ported."));
#endif
        });
    }

    [Test]
    public static void IndirectStoreReachesTheTargetRecordingBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var address = Register(compiler, TYP_BYREF, REG_S0);
            var data = Register(compiler, TYP_INT, REG_S1);
            var store = new GenTreeStoreInd(TYP_INT, address, data);
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForStoreInd(store));

#if TARGET_LOONGARCH64
            Assert.That(failure?.Message,
                Does.Contain("LoongArch64 indirect-store instruction recording is not ported."));
#else
            Assert.That(failure?.Message,
                Does.Contain("RISC-V64 indirect-store instruction recording is not ported."));
#endif
        });
    }

    [Test]
    public static void GcIndirectStorePreservesWriteBarrierHelperDispatch()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var address = Register(compiler, TYP_BYREF, REG_WRITE_BARRIER_DST);
            var data = Register(compiler, TYP_REF, REG_WRITE_BARRIER_SRC);
            var store = new GenTreeStoreInd(TYP_REF, address, data)
            {
                Flags = GTF_IND_TGT_HEAP,
            };
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForStoreInd(store));

            Assert.That(failure?.Message, Does.Contain("Helper call generation is not implemented for this target."));
        });
    }

#if FEATURE_SIMD
    [Test]
    public static void Simd12IndirectStoreRetainsTheTargetDependencyBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var address = compiler.gtNewLclAddrNode(TYP_BYREF, 0, 0);
            var data = new GenTreeVecCon(TYP_SIMD12);
            var store = new GenTreeStoreInd(TYP_SIMD12, address, data);

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForStoreInd(store));

#if TARGET_LOONGARCH64
            Assert.That(failure?.Message, Does.Contain("LoongArch64 SIMD12 indirect stores are not ported."));
#else
            Assert.That(failure?.Message, Does.Contain("RISC-V64 SIMD12 indirect stores are not ported."));
#endif
        });
    }
#endif

#if TARGET_LOONGARCH64 && FEATURE_SIMD
    [Test]
    public static void Simd12IndirectLoadRetainsTheTargetDependencyBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var address = compiler.gtNewLclAddrNode(TYP_BYREF, 0, 0);
            var load = new GenTreeIndir(GT_IND, TYP_SIMD12, address);

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForIndir(load));

            Assert.That(failure?.Message, Does.Contain("LoongArch64 SIMD12 indirect loads are not ported."));
        });
    }
#endif

    private static void WithCodeGen(Action<Compiler, CodeGen> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.lvaCount = 1;
        compiler.lvaTable = [new LclVarDsc()];
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        compiler.eeInfoInitialized = true;
        compiler.eeInfo.osPageSize = 4096;
        compiler.lvaOutgoingArgSpaceSize.Value = 0;
        compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
        JitTls.Compiler = compiler;

        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            codeGen.RegSet.rsClearRegsModified();
            codeGen.Emitter.emitBegCG(compiler, default);
            codeGen.Emitter.Init();
            codeGen.Emitter.emitBegFN(false
#if DEBUG
                , true
#endif
                );

            action(compiler, codeGen);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

#if TARGET_RISCV64
    private struct HelperContext
    {
        public ICorJitInfo JitInfo;
        public void* Address;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void* GetHelperFtn(ICorJitInfo* self, CorInfoHelpFunc helper,
        CORINFO_CONST_LOOKUP* lookup, CORINFO_METHOD_STRUCT_** method)
    {
        var context = (HelperContext*)self;
        lookup->accessType = IAT_PVALUE;
        lookup->addr = context->Address;

        return context->Address;
    }
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "treeLifeUpdater")]
    private static extern ref TreeLifeUpdater? LifeUpdater(CodeGen codeGen);
}
#endif
