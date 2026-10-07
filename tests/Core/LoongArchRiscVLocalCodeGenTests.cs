// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64 || TARGET_RISCV64
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.CorJitResult;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
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
#if TARGET_RISCV64
    private const string RiscVRecorderDebugBoundary = "Instruction sanity checking outside AMD64 is not ported.";
    private const regNumber REG_S0 = REG_FP;
    private const regNumber REG_F0 = REG_FT0;
    private const regNumber REG_F1 = REG_FT1;
    private const regNumber REG_F2 = REG_FT2;
#endif

#if TARGET_LOONGARCH64
    [TestCase(false, EA_PTRSIZE, "LoongArch64 address constant recording is not ported.")]
    [TestCase(true, EA_PTR_DSP_RELOC, "LoongArch64 relocated-address instruction recording is not ported.")]
    public static void ImmediateMaterializationReachesTheLoongArchEmitterBoundary(
        bool relocatable, emitAttr size, string expectedFailure)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.opts.compReloc = relocatable;

            var failure = Assert.Throws<FatalJitException>(() =>
                codeGen.instGen_Set_Reg_To_Imm(size, REG_S0, 0x1234));

            Assert.That(failure?.Message, Does.Contain(expectedFailure));
        });
    }

    [Test]
    public static void StackArgumentDispatchReachesTheLoongArchStoreBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var source = new GenTreePhysReg(REG_S0, TYP_LONG) { RegNum = REG_S0 };
            var argument = new GenTreePutArgStk(TYP_VOID, source, null, 0, 8, false);

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(argument));

            Assert.That(failure?.Message,
                Does.Contain("Target local-stack store recording is not implemented."));
        });
    }

#if FEATURE_SIMD
    [Test]
    public static void SimdStackArgumentPreservesTheLoongArchNyiBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var source = new GenTreePhysReg(REG_F0, TYP_SIMD16) { RegNum = REG_F0 };
            var argument = new GenTreePutArgStk(TYP_VOID, source, null, 0, 16, false);

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(argument));

            Assert.That(failure?.Message, Does.Contain("unimplemented on LOONGARCH64 yet"));
        });
    }
#endif

    [Test]
    public static void GSCookieCheckReachesTheLoongArchEmissionBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.gsGlobalSecurityCookieVal = 0x1234;

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genEmitGSCookieCheck(false));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
        });
    }

    [TestCase(NamedIntrinsic.NI_PRIMITIVE_SaturateToInt8)]
    [TestCase(NamedIntrinsic.NI_PRIMITIVE_SaturateToInt16)]
    [TestCase(NamedIntrinsic.NI_PRIMITIVE_SaturateToUInt8)]
    [TestCase(NamedIntrinsic.NI_PRIMITIVE_SaturateToUInt16)]
    public static void IntrinsicSaturationDispatchReachesTheLoongArchImmediateBoundary(NamedIntrinsic intrinsic)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var operand = Register(compiler, TYP_INT, REG_S0);
            var tree = new GenTreeIntrinsic(TYP_INT, operand, intrinsic, null)
            {
                RegNum = REG_S1,
            };
            codeGen.InternalRegisters.Add(tree, regMaskTP.CreateFromRegNum(REG_S2, REG_S2.SingleTypeMask));

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree));

            Assert.That(failure?.Message,
                Does.Contain("Two-register-immediate instruction recording requires xarch."));
        });
    }

    [TestCase(NamedIntrinsic.NI_System_Math_MaxNative, TYP_FLOAT)]
    [TestCase(NamedIntrinsic.NI_System_Math_MaxNative, TYP_DOUBLE)]
    [TestCase(NamedIntrinsic.NI_System_Math_MinNative, TYP_FLOAT)]
    [TestCase(NamedIntrinsic.NI_System_Math_MinNative, TYP_DOUBLE)]
    public static void FloatingIntrinsicDispatchReachesTheLoongArchInstructionBoundary(
        NamedIntrinsic intrinsic,
        var_types type)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var op1 = compiler.gtNewDconNode(type, 1.0);
            op1.RegNum = REG_F0;
            var op2 = compiler.gtNewDconNode(type, 2.0);
            op2.RegNum = REG_F1;
            var tree = new GenTreeIntrinsic(type, op1, op2, intrinsic, null)
            {
                RegNum = REG_F2,
            };

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree));

            Assert.That(failure?.Message,
                Does.Contain("LoongArch64 three-register instruction recording is not ported."));
        });
    }

    [Test]
    public static void TreeNodeDispatchReachesTheSharedConstantMaterializationBoundary()
    {
        WithCodeGen((_, codeGen) =>
        {
            var tree = new GenTreeIntCon(TYP_INT, 1) { RegNum = REG_S0 };
            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree));

            Assert.That(failure?.Message, Does.Contain("LoongArch64 address constant recording is not ported."));
        });
    }
#endif

#if TARGET_RISCV64
#if DEBUG
    [TestCase(false, "Instruction sanity checking outside AMD64 is not ported.")]
#else
    [TestCase(false, "Target local-stack instruction recording is not implemented.")]
#endif
    [TestCase(true, "Absolute-address instruction recording requires xarch.")]
    public static void GSCookieCheckReachesTheRiscVEmissionBoundary(bool useCookieAddress, string expectedBoundary)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            if (useCookieAddress)
            {
                compiler.gsGlobalSecurityCookieAddr = (nint*)0x12345678;
            }
            else
            {
                compiler.gsGlobalSecurityCookieVal = 0x1234;
            }

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genEmitGSCookieCheck(false));

            Assert.That(failure?.Message, Does.Contain(expectedBoundary));
        });
    }
#endif

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
#if TARGET_RISCV64
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
#endif
            compiler.lvaTable[0].Type = TYP_STRUCT;
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);
            GenTree destination;
            if (destinationKind is 2)
            {
                var baseAddress = compiler.gtNewLclAddrNode(TYP_BYREF, 0, 0);
                baseAddress.RegNum = REG_S0;

                destination = new GenTreeAddrMode(TYP_BYREF, baseAddress, null, 0, 8)
                {
                    IsContained = true,
                };
            }
            else
            {
                var localAddress = compiler.gtNewLclAddrNode(TYP_BYREF, 0, 0);
                localAddress.IsContained = destinationKind is 0;
                localAddress.RegNum = destinationKind is 0 ? REG_NA : REG_S0;
                destination = localAddress;
            }

            var zero = new GenTreeIntCon(TYP_INT, 0) { IsContained = true };
            var tree = new GenTreeBlk(TYP_STRUCT, destination, zero, new ClassLayout(16)) { RegNum = REG_NA };
            if (isVolatile)
            {
                tree.Flags |= GenTreeFlags.GTF_IND_VOLATILE;
            }

#if TARGET_LOONGARCH64
            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForInitBlkUnroll(tree));
            var expectedBoundary = isVolatile
                ? "LoongArch64 memory barrier emission is not ported."
                : destinationKind is 0
                    ? "Target local-stack store recording is not implemented."
                    : "Two-register-immediate instruction recording requires xarch.";
            Assert.That(failure?.Message, Does.Contain(expectedBoundary));
#else
            var failure = CaptureFatalJitException(() => codeGen.genCodeForInitBlkUnroll(tree));
            if (isVolatile)
            {
                Assert.That(failure?.Message, Does.Contain("RISC-V64 memory barrier emission is not ported."));
            }
            else if (destinationKind is 0)
            {
                Assert.That(failure?.Message,
                    Does.Contain("Target local-stack store recording is not implemented."));
            }
            else
            {
#if DEBUG
                const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
                const string? expectedBoundary = null;
#endif
                AssertRiscVRecorderOutcome(
                    codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
            }
#endif
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
#if TARGET_RISCV64
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
#endif
            if (useZba)
            {
#if TARGET_RISCV64
                compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_Zba);
                compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_Zba);
                compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_Zba);
#endif
            }

            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);
            compiler.fgFirstBB = new BasicBlock(null, null);
            var index = Register(compiler, TYP_I_IMPL, REG_S0);
            var table = Register(compiler, TYP_I_IMPL, REG_S1);
            var tree = new GenTreeOp(GT_SWITCH_TABLE, TYP_VOID, index, table);
            codeGen.InternalRegisters.Add(tree, regMaskTP.CreateFromRegNum(REG_S2, REG_S2.SingleTypeMask));

#if TARGET_RISCV64
            var failure = CaptureFatalJitException(() => codeGen.genCodeForTreeNode(tree));
#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = "RISC-V64 block-relative address recording is not ported.";
#endif
            AssertRiscVRecorderOutcome(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
#else
            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree));
#if TARGET_LOONGARCH64
            Assert.That(failure?.Message,
                Does.Contain("Two-register-immediate instruction recording requires xarch."));
#endif
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
    public static void JumpTableGenerationStopsAtTheTargetEmbeddedDataBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var current = compiler.compCurBB ?? throw new AssertionException("Missing current block.");
            current.SetKindAndTargetEdge(BBJ_SWITCH, null);
            var target = new BasicBlock(null, null);
            target.SetFlags(BBF_HAS_LABEL);
            var edge = new FlowEdge(current, target, null);
            current.SwitchTargets = new BBswtDesc([edge], [0], hasDefault: false);
            current.SwitchTargets.Cases[0] = edge;

            var tree = new GenTree(GT_JMPTABLE, TYP_I_IMPL) { RegNum = REG_S0 };
            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree));

#if TARGET_LOONGARCH64
            Assert.That(failure?.Message, Does.Contain("LoongArch64 embedded-data instruction recording is not ported."));
#else
            Assert.That(failure?.Message, Does.Contain("RISC-V64 embedded-data instruction recording is not ported."));
#endif
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
    [TestCase(1, "LoongArch64 address constant recording is not ported.")]
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
            var value = Register(compiler, TYP_LONG, REG_S0);
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
            var first = Register(compiler, TYP_LONG, REG_S0);
            var second = Register(compiler, TYP_LONG, REG_S1);
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
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);

            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_Zicond);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_Zicond);
            compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_Zicond);
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);

            var condition = Register(compiler, TYP_INT, REG_S0);
            var trueValue = Register(compiler, TYP_INT, REG_S1);
            var falseValue = new GenTreeIntCon(TYP_INT, 0) { IsContained = true };
            var tree = new GenTreeConditional(GT_SELECT, TYP_INT, condition, trueValue, falseValue)
            {
                RegNum = REG_S2,
            };

#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif
            var failure = CaptureFatalJitException(() => codeGen.genCodeForTreeNode(tree));
            AssertRiscVRecorderOutcome(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
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
    public static void NonlocalJumpDispatchPreservesTailCallStateAtTheRecorderBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
#if TARGET_RISCV64
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
#endif
            compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);
            var operand = Register(compiler, TYP_I_IMPL, REG_S0);
            var tree = new GenTreeUnOp(GT_NONLOCAL_JMP, TYP_VOID, operand);

#if TARGET_RISCV64
#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif
            var failure = CaptureFatalJitException(() => codeGen.genCodeForTreeNode(tree));
            AssertRiscVRecorderOutcome(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
#else
            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree));
            Assert.That(failure?.Message,
                Does.Contain("Target two-register-immediate instruction recording is not implemented."));
#endif
            Assert.That(codeGen.HasTailCalls, Is.True);
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
#if TARGET_RISCV64
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
#endif
            compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);
            var address = Register(compiler, TYP_BYREF, REG_S0);
            var data = Register(compiler, TYP_INT, REG_S1);
            var tree = new GenTreeIndir(oper, TYP_INT, address, data) { RegNum = REG_S2 };

#if TARGET_RISCV64
#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif
            var failure = CaptureFatalJitException(() => codeGen.genCodeForTreeNode(tree));
            AssertRiscVRecorderOutcome(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
#else
            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree));
#if TARGET_LOONGARCH64
            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
#endif
#endif
        });
    }

#if TARGET_RISCV64
#if DEBUG
    [TestCase(TYP_LONG, RiscVRecorderDebugBoundary)]
#else
    [TestCase(TYP_LONG, "Target conditional-branch recording is not implemented.")]
#endif
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
#if TARGET_RISCV64
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
#endif
            compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);
            codeGen.GCInfo.gcRegPtrSetInit();
            codeGen.GCInfo.gcVarPtrSetInit();

            var address = Register(compiler, TYP_BYREF, REG_S0);
            var value = Register(compiler, TYP_LONG, REG_S1);
            var comparand = Register(compiler, comparandType, REG_S2);
            var tree = new GenTreeCmpXchg(TYP_LONG, address, value, comparand) { RegNum = REG_S4 };
            var internalRegisters = regMaskTP.CreateFromRegNum(REG_S3, REG_S3.SingleTypeMask);
            if (comparandType is TYP_INT)
            {
                internalRegisters |= regMaskTP.CreateFromRegNum(REG_S5, REG_S5.SingleTypeMask);
            }
            codeGen.InternalRegisters.Add(tree, internalRegisters);

#if TARGET_RISCV64
            var failure = CaptureFatalJitException(() => codeGen.genCodeForTreeNode(tree));
            if (comparandType is TYP_LONG)
            {
                AssertRiscVRecorderOutcome(
                    codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
            }
            else
            {
                Assert.That(failure?.Message, Does.Contain(expectedBoundary));
            }
#else
            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree));
            Assert.That(failure?.Message, Does.Contain(expectedBoundary));
#endif
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

    [Test]
    public static void SimdNarrowPreservesTheRiscVNyiBoundary()
    {
        WithCodeGen((_, codeGen) =>
        {
            var node = new GenTreeVecCon(TYP_SIMD16);
            var failure = Assert.Throws<FatalJitException>(() => codeGen.genSIMDIntrinsicNarrow(node));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
        });
    }

    [Test]
    public static void SimdBinaryIntrinsicPreservesTheRiscVNyiBoundary()
    {
        WithCodeGen((_, codeGen) =>
        {
            var node = new GenTreeVecCon(TYP_SIMD16);
            var failure = Assert.Throws<FatalJitException>(() => codeGen.genSIMDIntrinsicBinOp(node));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
        });
    }

    [Test]
    public static void SimdGetItemPreservesTheRiscVNyiBoundary()
    {
        WithCodeGen((_, codeGen) =>
        {
            var node = new GenTreeVecCon(TYP_SIMD16);
            var failure = Assert.Throws<FatalJitException>(() => codeGen.genSIMDIntrinsicGetItem(node));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
        });
    }

    [Test]
    public static void SimdSetItemPreservesTheRiscVNyiBoundary()
    {
        WithCodeGen((_, codeGen) =>
        {
            var node = new GenTreeVecCon(TYP_SIMD16);
            var failure = Assert.Throws<FatalJitException>(() => codeGen.genSIMDIntrinsicSetItem(node));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
        });
    }

    [Test]
    public static void SimdRelationalIntrinsicPreservesTheRiscVNyiBoundary()
    {
        WithCodeGen((_, codeGen) =>
        {
            var node = new GenTreeVecCon(TYP_SIMD16);
            var failure = Assert.Throws<FatalJitException>(() => codeGen.genSIMDIntrinsicRelOp(node));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
        });
    }

    [Test]
    public static void SimdDotProductIntrinsicPreservesTheRiscVNyiBoundary()
    {
        WithCodeGen((_, codeGen) =>
        {
            var node = new GenTreeVecCon(TYP_SIMD16);
            var failure = Assert.Throws<FatalJitException>(() => codeGen.genSIMDIntrinsicDotProduct(node));

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
            var data = Register(compiler, TYP_I_IMPL, REG_S0);
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
    public static void LocalVariableStoreRecordsImmediateConstant()
    {
        WithCodeGen((compiler, codeGen) =>
        {
#if TARGET_RISCV64
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
#endif
            compiler.lvaTable[0].Type = TYP_INT;
            var constant = new GenTreeIntCon(TYP_INT, 12) { IsContained = true };
#if TARGET_LOONGARCH64
            var tree = new GenTreeLclVar(TYP_INT, 0, constant) { RegNum = REG_NA };
#else
            var tree = new GenTreeLclVar(TYP_INT, 0, constant) { RegNum = REG_S0 };
#endif

#if TARGET_RISCV64
#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = "Register move recording requires xarch.";
#endif
            var failure = CaptureFatalJitException(() => codeGen.genCodeForStoreLclVar(tree));
            AssertRiscVRecorderOutcome(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
#else
            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForStoreLclVar(tree));
            Assert.That(failure?.Message, Does.Contain("LoongArch64 address constant recording is not ported."));
#endif
        });
    }

    [Test]
    public static void LocalHeapDispatchReachesTheTargetEmitterBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
#if TARGET_RISCV64
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
#endif
            compiler.compLocallocUsed = true;
            codeGen.resetFramePointerUsedWritePhase();
            codeGen.IsFramePointerUsed = true;
            var size = new GenTreeIntCon(TYP_I_IMPL, 16) { IsContained = true };
            var tree = compiler.gtNewUnaryNode(GT_LCLHEAP, TYP_I_IMPL, size);
            tree.RegNum = REG_S0;

#if TARGET_RISCV64
            var failure = CaptureFatalJitException(() => codeGen.genLclHeap(tree));
#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = "RISC-V64 register move recording is not ported.";
#endif
            AssertRiscVRecorderOutcome(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
#else
            var failure = Assert.Throws<FatalJitException>(() => codeGen.genLclHeap(tree));
            Assert.That(failure?.Message, Does.Contain(
                "Two-register-immediate instruction recording requires xarch."));
#endif
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
#if TARGET_RISCV64
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
#endif
            compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);

            var isFloating = type is TYP_FLOAT or TYP_DOUBLE;
            GenTree operand = isFloating
                ? compiler.gtNewDconNode(type, 1.0)
                : compiler.gtNewIconNode(type, 1);
            operand.RegNum = isFloating ? REG_F0 : REG_S0;
            var tree = compiler.gtNewUnaryNode(oper, type, operand);
            tree.RegNum = isFloating ? REG_F2 : REG_S1;

#if TARGET_RISCV64
            if (oper is GT_NEG)
            {
#if DEBUG
                const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
                const string? expectedBoundary = null;
#endif
                var failure = CaptureFatalJitException(() => codeGen.genCodeForNegNot(tree.AsUnOp()));
                AssertRiscVRecorderOutcome(
                    codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
            }
            else
            {
                var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForNegNot(tree.AsUnOp()));
                Assert.That(failure?.Message,
                    Does.Contain("Target two-register instruction recording is not implemented."));
            }
#else
            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForNegNot(tree.AsUnOp()));
            Assert.That(failure?.Message, Does.Contain(
                "Target two-register instruction recording is not implemented."));
#endif
        });
    }

#if TARGET_LOONGARCH64
    [TestCase(TYP_FLOAT, TYP_DOUBLE)]
    [TestCase(TYP_DOUBLE, TYP_FLOAT)]
    public static void FloatingWidthCastReachesTheLoongArchTwoRegisterBoundary(var_types sourceType, var_types targetType)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);

            var source = compiler.gtNewDconNode(sourceType, 1.0);
            source.RegNum = REG_F0;
            var cast = new GenTreeCast(targetType, source, false, targetType) { RegNum = REG_F1 };

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genFloatToFloatCast(cast));

            Assert.That(failure?.Message,
                Does.Contain("Target two-register instruction recording is not implemented."));
        });
    }
#endif

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
#if TARGET_RISCV64
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
#endif
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

#if TARGET_RISCV64
#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif
            var failure = CaptureFatalJitException(() => codeGen.genCodeForDivMod(tree));
            AssertRiscVRecorderOutcome(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
#else
            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForDivMod(tree));
            Assert.That(failure?.Message,
                Does.Contain("LoongArch64 three-register instruction recording is not ported."));
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
            var source = Register(compiler, sourceType, REG_S0);
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

            var cast = new GenTreeCast(destinationType, source, fromUnsigned: false, destinationType)
            {
                RegNum = REG_S0,
            };

#if TARGET_RISCV64
            codeGen.InternalRegisters.Add(cast, regMaskTP.CreateFromRegNum(REG_S2, REG_S2.SingleTypeMask));
#endif

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForCast(cast));
#if TARGET_LOONGARCH64
            var expectedMessage = isUnsigned
                ? "Two-register-immediate instruction recording requires xarch."
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
    public static void IntegerCompareDispatchRecordsImmediateComparison()
    {
        WithCodeGen((compiler, codeGen) =>
        {
#if TARGET_RISCV64
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
#endif
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);

            var op1 = Register(compiler, TYP_LONG, REG_S0);
            var op2 = new GenTreeIntCon(TYP_LONG, 5) { IsContained = true };
            var tree = compiler.gtNewBinaryNode(GT_LT, TYP_INT, op1, op2);
            tree.RegNum = REG_S1;

#if TARGET_RISCV64
#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif
            var failure = CaptureFatalJitException(() => codeGen.genCodeForTreeNode(tree));
            AssertRiscVRecorderOutcome(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
#else
            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree));

            Assert.That(failure?.Message,
                Does.Contain("Target two-register-immediate instruction recording is not implemented."));
#endif
        });
    }

    [TestCase(TYP_FLOAT)]
    [TestCase(TYP_DOUBLE)]
    public static void FloatingCompareDispatchRecordsUnorderedCompare(var_types type)
    {
        WithCodeGen((compiler, codeGen) =>
        {
#if TARGET_RISCV64
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
#endif
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);

            var op1 = compiler.gtNewDconNode(type, 1.0);
            op1.RegNum = REG_F0;
            var op2 = compiler.gtNewDconNode(type, 2.0);
            op2.RegNum = REG_F1;
            var tree = compiler.gtNewBinaryNode(GT_LT, TYP_INT, op1, op2);
            tree.RegNum = REG_S1;
            tree.Flags |= GTF_RELOP_NAN_UN;

#if TARGET_RISCV64
#if DEBUG
            const string expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif
            var failure = CaptureFatalJitException(() => codeGen.genCodeForTreeNode(tree));
            AssertRiscVRecorderOutcome(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
#else
            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree));
            Assert.That(failure?.Message,
                Does.Contain("Two-register-immediate instruction recording requires xarch."));
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

#if TARGET_RISCV64
    [Test]
    public static void IndirectLoadReachesTheTargetRecordingBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var address = Register(compiler, TYP_BYREF, REG_S0);
            var load = new GenTreeIndir(GT_IND, TYP_INT, address) { RegNum = REG_S1 };
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForIndir(load));

            Assert.That(failure?.Message,
                Does.Contain("RISC-V64 indirect load/store instruction recording is not ported."));
        });
    }
#endif

#if TARGET_RISCV64
    [Test]
    public static void IndexAddressRecordsRiscVImmediateInstruction()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);

            var baseAddress = Register(compiler, TYP_REF, REG_S0);
            var index = Register(compiler, TYP_INT, REG_S1);
            var tree = new GenTreeIndexAddr(
                baseAddress, index, TYP_INT, NO_CLASS_HANDLE, 1, 8, 16, boundsCheck: false)
            {
                RegNum = REG_A0,
            };
            codeGen.InternalRegisters.Add(
                tree, regMaskTP.CreateFromRegNum(REG_A1, REG_A1.SingleTypeMask));
            codeGen.GCInfo.gcMarkRegPtrVal(REG_S0, TYP_REF);

#if DEBUG
            const string expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif
            var failure = CaptureFatalJitException(() => codeGen.genCodeForIndexAddr(tree));
            AssertRiscVRecorderOutcome(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
        });
    }
#endif

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
                Does.Contain("RISC-V64 indirect load/store instruction recording is not ported."));
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

            Assert.That(failure?.Message, Does.Contain("Register move recording requires xarch."));
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

    private static GenTreeIntCon Register(Compiler compiler, var_types type, regNumber reg)
    {
        var node = compiler.gtNewIconNode(type, 7);
        node.RegNum = reg;
        return node;
    }

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
        compiler.compCurBB = new BasicBlock(null, null);
        JitTls.Compiler = compiler;

        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);
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
    private static FatalJitException? CaptureFatalJitException(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (FatalJitException exception)
        {
            return exception;
        }
    }

    private static void AssertRiscVRecorderOutcome(
        CodeGen codeGen,
        FatalJitException? failure,
        string? expectedBoundary,
        int initialInstructionCount,
        int initialGroupSize)
    {
        var emitter = codeGen.Emitter;
        var instructionBuffer = CurrentInstructionBuffer(emitter)
            ?? throw new AssertionException("Missing current instruction buffer.");
        Assert.That(instructionBuffer.Count, Is.GreaterThan(initialInstructionCount));

        if (expectedBoundary is null)
        {
            Assert.That(failure, Is.Null);
            Assert.That(CurrentInstructionGroupSize(emitter), Is.GreaterThan(initialGroupSize));
        }
        else
        {
            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message, Does.Contain(expectedBoundary));
            if (expectedBoundary != RiscVRecorderDebugBoundary)
            {
                Assert.That(CurrentInstructionGroupSize(emitter), Is.GreaterThan(initialGroupSize));
            }
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentInstructionBuffer(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int CurrentInstructionGroupSize(Emitter emitter);

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
