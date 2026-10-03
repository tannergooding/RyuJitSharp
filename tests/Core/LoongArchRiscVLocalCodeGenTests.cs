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
using static RyuJitSharp.genTreeOps;
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
