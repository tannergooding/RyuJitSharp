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

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "treeLifeUpdater")]
    private static extern ref TreeLifeUpdater? LifeUpdater(CodeGen codeGen);
}
#endif
