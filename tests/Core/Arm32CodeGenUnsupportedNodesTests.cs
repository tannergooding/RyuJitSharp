// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Emitter.insSize;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.insFlags;
using static RyuJitSharp.instruction;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm32CodeGenUnsupportedNodesTests
{
    [Test]
    public static void NonLocalJumpRetainsTheNativeUnsupportedBoundary()
    {
        WithCodeGen((_, codeGen) =>
        {
            var value = new GenTree(GT_FTN_ENTRY, TYP_I_IMPL);
            var tree = new GenTreeUnOp(GT_NONLOCAL_JMP, TYP_VOID, value);

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genNonLocalJmp(tree));

            Assert.That(failure?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
        });
    }

    [Test]
    public static void FunctionEntryRetainsTheNativeUnsupportedBoundary()
    {
        WithCodeGen((_, codeGen) =>
        {
            var tree = new GenTree(GT_FTN_ENTRY, TYP_I_IMPL);

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genFtnEntry(tree));

            Assert.That(failure?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
        });
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
        compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
        JitTls.Compiler = compiler;
        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            action(compiler, codeGen);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}

[NonParallelizable]
internal static class Arm32CkfiniteCodeGenTests
{
    [TestCase(INS_bfi, 1, 8, 40)]
    [TestCase(INS_bfi, 0, 32, 31)]
    [TestCase(INS_sbfx, 23, 8, 743)]
    [TestCase(INS_ubfx, 20, 11, 650)]
    [TestCase(INS_ssat, 0, 1, 0)]
    [TestCase(INS_ssat, 0, 32, 31)]
    [TestCase(INS_usat, 0, 0, 0)]
    [TestCase(INS_usat, 0, 31, 31)]
    public static void TwoRegisterTwoImmediateRecordingPreservesThumb2Encoding(
        instruction ins, int imm1, int imm2, int encoded)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            var failure = RecordTwoImmediates(codeGen.Emitter, ins, imm1, imm2);
#if DEBUG
            Assert.That(failure?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
            Assert.That(failure?.Message, Is.EqualTo("Instruction sanity checking outside AMD64 is not ported."));
#else
            Assert.That(failure, Is.Null);
#endif

            var descriptor = LastInstruction(codeGen.Emitter)
                ?? throw new AssertionException("No two-immediate instruction was recorded.");
            Assert.That(descriptor.idIns(), Is.EqualTo(ins));
            Assert.That(descriptor.idInsFmt(), Is.EqualTo(IF_T2_D0));
            Assert.That(descriptor.idInsSize(), Is.EqualTo(ISZ_32BIT));
            Assert.That(descriptor.idInsFlags(), Is.EqualTo(INS_FLAGS_NOT_SET));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R2));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_R3));
            Assert.That(codeGen.Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)encoded));
#if !DEBUG
            Assert.That(GroupSize(codeGen.Emitter), Is.EqualTo(4));
#endif
        });
    }

    [TestCase(TYP_FLOAT)]
    [TestCase(TYP_DOUBLE)]
    public static void CkfiniteRetainsTheExistingArmRegisterMoveBoundary(var_types type)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            var operand = compiler.gtNewDconNode(type, 1.25);
            operand.RegNum = REG_F0;
            var tree = compiler.gtNewUnaryNode(GT_CKFINITE, type, operand);
            tree.RegNum = REG_F2;
            codeGen.InternalRegisters.Add(
                tree,
                regMaskTP.CreateFromRegNum(REG_R2, REG_R2.SingleTypeMask));

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCkfinite(tree));
            Assert.That(failure?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
            Assert.That(failure?.Message, Is.EqualTo("Register move recording requires xarch."));
        });
    }

    private static FatalJitException? RecordTwoImmediates(
        Emitter emitter, instruction ins, int imm1, int imm2)
    {
#if DEBUG
        return Assert.Throws<FatalJitException>(() =>
            emitter.emitIns_R_R_I_I(ins, EA_4BYTE, REG_R2, REG_R3, imm1, imm2));
#else
        emitter.emitIns_R_R_I_I(ins, EA_4BYTE, REG_R2, REG_R3, imm1, imm2);
        return null;
#endif
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int GroupSize(Emitter emitter);
}
#endif
