// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CorJitResult;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.GenTreeBlk;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class RiscVCodeGenPortTests
{
    [TestCase(TYP_BYREF)]
    [TestCase(TYP_I_IMPL)]
    public static void LocalAddressPreservesTheStackInstructionRecordingBoundary(var_types type)
    {
        WithCodeGen((_, codeGen) =>
        {
            var localAddress = new GenTreeLclFld(GT_LCL_ADDR, type, 0, 24)
            {
                RegNum = REG_A0,
            };

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForLclAddr(localAddress));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message,
                Does.Contain("Target local-stack instruction recording is not implemented."));
        });
    }

    [TestCase(TYP_INT, TYP_LONG, "Target two-register instruction recording is not implemented.")]
    [TestCase(TYP_LONG, TYP_INT, "Target two-register instruction recording is not implemented.")]
    [TestCase(TYP_LONG, TYP_LONG, "Target conditional-branch recording is not implemented.")]
    public static void RangeCheckPreservesRiscVInstructionRecordingBoundary(
        var_types indexType,
        var_types lengthType,
        string expectedBoundary)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.opts.compDbgCode = true;

            var index = new GenTreePhysReg(REG_A0, indexType) { RegNum = REG_A0 };
            var length = new GenTreePhysReg(REG_A1, lengthType) { RegNum = REG_A1 };
            var boundsCheck = new GenTreeBoundsChk(index, length, SpecialCodeKind.SCK_RNGCHK_FAIL);
            if ((indexType is TYP_INT) || (lengthType is TYP_INT))
            {
                codeGen.InternalRegisters.Add(
                    boundsCheck, regMaskTP.CreateFromRegNum(REG_A2, REG_A2.SingleTypeMask));
            }

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genRangeCheck(boundsCheck));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message, Does.Contain(expectedBoundary));
        });
    }

    [Test]
    public static void NullCheckPreservesRiscVIndirectLoadRecordingBoundary()
    {
        WithCodeGen((_, codeGen) =>
        {
            var address = new GenTreePhysReg(REG_A0, TYP_I_IMPL) { RegNum = REG_A0 };
            var nullCheck = new GenTreeIndir(GT_NULLCHECK, TYP_INT, address);

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForNullCheck(nullCheck));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message,
                Does.Contain("RISC-V64 indirect load/store instruction recording is not ported."));
        });
    }

    [TestCase(GT_LSH, TYP_INT, true, false, "Two-register-immediate instruction recording requires xarch.")]
    [TestCase(GT_RSH, TYP_LONG, false, false, "RISC-V three-register instruction recording is not implemented.")]
    [TestCase(GT_ROR, TYP_INT, false, false, "Two-register-immediate instruction recording requires xarch.")]
    [TestCase(GT_ROR, TYP_LONG, false, true, "RISC-V three-register instruction recording is not implemented.")]
    [TestCase(GT_ROL, TYP_INT, true, true, "Two-register-immediate instruction recording requires xarch.")]
    public static void ShiftPreservesRiscVInstructionRecordingBoundary(
        genTreeOps oper,
        var_types type,
        bool useImmediate,
        bool useZbb,
        string expectedBoundary)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            if (useZbb)
            {
                compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_Zbb);
                compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_Zbb);
                compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_Zbb);
            }

            var value = new GenTreePhysReg(REG_A0, type) { RegNum = REG_A0 };
            GenTree shiftBy;
            if (useImmediate)
            {
                shiftBy = compiler.gtNewIconNode(TYP_INT, 5);
                shiftBy.IsContained = true;
            }
            else
            {
                shiftBy = new GenTreePhysReg(REG_A1, TYP_INT) { RegNum = REG_A1 };
            }

            var shift = new GenTreeOp(oper, type, value, shiftBy) { RegNum = REG_A2 };
            if ((oper is GT_ROR or GT_ROL) && !useZbb)
            {
                codeGen.InternalRegisters.Add(
                    shift, regMaskTP.CreateFromRegNum(REG_A3, REG_A3.SingleTypeMask));
            }

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForShift(shift));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message, Does.Contain(expectedBoundary));
        });
    }

    [TestCase(GT_SH1ADD)]
    [TestCase(GT_SH2ADD)]
    [TestCase(GT_SH3ADD)]
    [TestCase(GT_SH1ADD_UW)]
    [TestCase(GT_SH2ADD_UW)]
    [TestCase(GT_SH3ADD_UW)]
    public static void ShxaddPreservesRiscVInstructionRecordingBoundary(genTreeOps oper)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_Zba);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_Zba);
            compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_Zba);

            var left = new GenTreePhysReg(REG_A0, TYP_LONG) { RegNum = REG_A0 };
            var right = new GenTreePhysReg(REG_A1, TYP_LONG) { RegNum = REG_A1 };
            var tree = new GenTreeOp(oper, TYP_LONG, left, right) { RegNum = REG_A2 };

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForShxadd(tree));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message,
                Does.Contain("RISC-V three-register instruction recording is not implemented."));
        });
    }

    [Test]
    public static void AddUwPreservesRiscVInstructionRecordingBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_Zba);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_Zba);
            compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_Zba);

            var left = new GenTreePhysReg(REG_A0, TYP_LONG) { RegNum = REG_A0 };
            var right = new GenTreePhysReg(REG_A1, TYP_LONG) { RegNum = REG_A1 };
            var tree = new GenTreeOp(GT_ADD_UW, TYP_LONG, left, right) { RegNum = REG_A2 };

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForAddUw(tree));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message,
                Does.Contain("RISC-V three-register instruction recording is not implemented."));
        });
    }

    [Test]
    public static void SlliUwPreservesRiscVInstructionRecordingBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_Zba);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_Zba);
            compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_Zba);

            var value = new GenTreePhysReg(REG_A0, TYP_LONG) { RegNum = REG_A0 };
            var shiftBy = compiler.gtNewIconNode(TYP_INT, 7);
            shiftBy.IsContained = true;

            var tree = new GenTreeOp(GT_SLLI_UW, TYP_LONG, value, shiftBy) { RegNum = REG_A2 };

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForSlliUw(tree));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message,
                Does.Contain("Two-register-immediate instruction recording requires xarch."));
        });
    }

    [TestCase(8)]
    [TestCase(16)]
    public static void CopyBlockUnrollPreservesRiscVInstructionRecordingBoundary(int size)
    {
        WithCodeGen((_, codeGen) =>
        {
            var destination = new GenTreePhysReg(REG_A0, TYP_BYREF) { RegNum = REG_A0 };
            var sourceAddress = new GenTreePhysReg(REG_A1, TYP_BYREF) { RegNum = REG_A1 };
            var source = new GenTreeIndir(GT_IND, TYP_STRUCT, sourceAddress) { IsContained = true };
            var block = new GenTreeBlk(TYP_STRUCT, destination, source, new ClassLayout((uint)size))
            {
                _kind = BlkOpKindUnroll,
            };
            codeGen.InternalRegisters.Add(
                block, regMaskTP.CreateFromRegNum(REG_A2, REG_A2.SingleTypeMask));

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForCpBlkUnroll(block));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message,
                Does.Contain("Two-register-immediate instruction recording requires xarch."));
        });
    }

    [Test]
    public static void InitBlkLoopPreservesRiscVInitialStoreRecordingBoundary()
    {
        WithCodeGen((_, codeGen) =>
        {
            var destination = new GenTreePhysReg(REG_A0, TYP_BYREF) { RegNum = REG_A0 };
            var block = new GenTreeBlk(
                TYP_STRUCT,
                destination,
                new GenTreeIntCon(TYP_INT, 0),
                new ClassLayout((uint)TARGET_POINTER_SIZE))
            {
                _kind = BlkOpKindLoop,
            };

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForInitBlkLoop(block));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message,
                Does.Contain("Two-register-immediate instruction recording requires xarch."));
        });
    }

    [Test]
    public static void CalleeSavedRestorePreservesXarchOnlyEmitterBoundary()
    {
        WithCodeGen((_, codeGen) =>
        {
            var restoreMask = regMaskTP.CreateFromRegNum(REG_S1, REG_S1.SingleTypeMask);
            var failure = Assert.Throws<FatalJitException>(() =>
                RestoreCalleeSavedRegisters(codeGen, restoreMask, REG_FP, 16, reportUnwindData: false));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message,
                Does.Contain("Two-register-immediate instruction recording requires xarch."));
        });
    }

    [Test]
    public static void OSRPrologPreservesXarchOnlyEmitterBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.compFuncInfos = [new FuncInfoDsc { funKind = FuncKind.FUNC_ROOT, uwi = new UnwindInfo() }];
            compiler.compFuncInfoCount = 1;
            compiler.fgFuncletsCreated = true;
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
            codeGen.Emitter.emitCurIG = codeGen.Emitter.emitGetFirstPrologIG();
            compiler.funCurrentFunc().GetUnwindInfo().InitUnwindInfo(compiler, null, null);

            var storage = stackalloc byte[PatchpointInfo.ComputeSize(1)];
            var patchpoint = (PatchpointInfo*)storage;
            patchpoint->Initialize(1, 96);
            patchpoint->CalleeSaveRegisters = (long)(SRBM_S1 | SRBM_FP | SRBM_RA);
            compiler.info.compPatchpointInfo = patchpoint;

            compiler.unwindBegProlog();

            var failure = Assert.Throws<FatalJitException>(
                () => codeGen.genOSRHandleTier0CalleeSavedRegistersAndFrame());

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message,
                Does.Contain("Two-register-immediate instruction recording requires xarch."));
        });
    }

    [TestCase(NI_PRIMITIVE_SaturateToInt8)]
    [TestCase(NI_PRIMITIVE_SaturateToInt16)]
    [TestCase(NI_PRIMITIVE_SaturateToUInt8)]
    [TestCase(NI_PRIMITIVE_SaturateToUInt16)]
    public static void SaturationIntrinsicDispatchPreservesRiscVRegisterRecordingBoundary(
        NamedIntrinsic intrinsic)
    {
        WithCodeGen((compiler, codeGen) => AssertIntrinsicBoundary(
            compiler, codeGen, intrinsic, TYP_INT, hasSecondOperand: false,
            expectedBoundary: "Target two-register instruction recording is not implemented."));
    }

    [TestCase(NI_System_Math_Abs, TYP_FLOAT,
        "RISC-V three-register instruction recording is not implemented.")]
    [TestCase(NI_System_Math_Abs, TYP_DOUBLE,
        "RISC-V three-register instruction recording is not implemented.")]
    [TestCase(NI_System_Math_Sqrt, TYP_FLOAT,
        "Target two-register instruction recording is not implemented.")]
    [TestCase(NI_System_Math_Sqrt, TYP_DOUBLE,
        "Target two-register instruction recording is not implemented.")]
    [TestCase(NI_PRIMITIVE_LeadingZeroCount, TYP_INT,
        "Target two-register instruction recording is not implemented.")]
    [TestCase(NI_PRIMITIVE_LeadingZeroCount, TYP_LONG,
        "Target two-register instruction recording is not implemented.")]
    [TestCase(NI_PRIMITIVE_TrailingZeroCount, TYP_INT,
        "Target two-register instruction recording is not implemented.")]
    [TestCase(NI_PRIMITIVE_TrailingZeroCount, TYP_LONG,
        "Target two-register instruction recording is not implemented.")]
    [TestCase(NI_PRIMITIVE_PopCount, TYP_INT,
        "Target two-register instruction recording is not implemented.")]
    [TestCase(NI_PRIMITIVE_PopCount, TYP_LONG,
        "Target two-register instruction recording is not implemented.")]
    public static void UnaryIntrinsicDispatchPreservesRiscVRegisterRecordingBoundary(
        NamedIntrinsic intrinsic,
        var_types type,
        string expectedBoundary)
    {
        WithCodeGen((compiler, codeGen) => AssertIntrinsicBoundary(
            compiler, codeGen, intrinsic, type, hasSecondOperand: false,
            expectedBoundary: expectedBoundary));
    }

    [TestCase(NI_System_Math_MinNative, TYP_FLOAT)]
    [TestCase(NI_System_Math_MinNative, TYP_DOUBLE)]
    [TestCase(NI_System_Math_MaxNative, TYP_FLOAT)]
    [TestCase(NI_System_Math_MaxNative, TYP_DOUBLE)]
    [TestCase(NI_System_Math_Min, TYP_INT)]
    [TestCase(NI_System_Math_MinUnsigned, TYP_INT)]
    [TestCase(NI_System_Math_Max, TYP_INT)]
    [TestCase(NI_System_Math_MaxUnsigned, TYP_INT)]
    public static void BinaryIntrinsicDispatchPreservesRiscVThreeRegisterRecordingBoundary(
        NamedIntrinsic intrinsic,
        var_types type)
    {
        WithCodeGen((compiler, codeGen) => AssertIntrinsicBoundary(
            compiler, codeGen, intrinsic, type, hasSecondOperand: true,
            expectedBoundary: "RISC-V three-register instruction recording is not implemented."));
    }

    private static void AssertIntrinsicBoundary(
        Compiler compiler,
        CodeGen codeGen,
        NamedIntrinsic intrinsic,
        var_types type,
        bool hasSecondOperand,
        string expectedBoundary)
    {
        var isFloating = type is TYP_FLOAT or TYP_DOUBLE;
        var sourceRegister = isFloating ? REG_FT0 : REG_A0;
        var secondSourceRegister = isFloating ? REG_FT1 : REG_A1;
        var targetRegister = isFloating ? REG_FT2 : REG_A2;
        var source = new GenTreePhysReg(sourceRegister, type) { RegNum = sourceRegister };
        GenTreeIntrinsic tree;
        if (hasSecondOperand)
        {
            var secondSource = new GenTreePhysReg(secondSourceRegister, type) { RegNum = secondSourceRegister };
            tree = new GenTreeIntrinsic(type, source, secondSource, intrinsic, null);
        }
        else
        {
            tree = new GenTreeIntrinsic(type, source, intrinsic, null);
        }

        tree.RegNum = targetRegister;

        if (intrinsic is NI_PRIMITIVE_SaturateToInt8 or NI_PRIMITIVE_SaturateToInt16
            or NI_PRIMITIVE_SaturateToUInt8 or NI_PRIMITIVE_SaturateToUInt16)
        {
            codeGen.InternalRegisters.Add(tree,
                regMaskTP.CreateFromRegNum(REG_A3, REG_A3.SingleTypeMask));
        }

        var failure = Assert.Throws<FatalJitException>(() => codeGen.genIntrinsic(tree));

        Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
        Assert.That(failure?.Message, Does.Contain(expectedBoundary));
    }

    internal static void WithCodeGen(Action<Compiler, CodeGen> action)
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

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "treeLifeUpdater")]
    private static extern ref TreeLifeUpdater? LifeUpdater(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genRestoreCalleeSavedRegistersHelp")]
    private static extern void RestoreCalleeSavedRegisters(CodeGen codeGen, regMaskTP mask, regNumber baseReg,
        int offset, bool reportUnwindData);
}
#endif
