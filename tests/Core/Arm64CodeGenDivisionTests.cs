// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.CorJitResult;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.SpecialCodeKind;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class Arm64CodeGenDivisionTests
{
    [Test]
    public static void UnsignedDivisionNodesDispatchToTheArm64Generator()
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            var dividend = compiler.gtNewIconNode(TYP_INT, 7);
            dividend.RegNum = REG_R1;
            var divisor = compiler.gtNewIconNode(TYP_INT, 3);
            divisor.RegNum = REG_R2;
            var tree = new GenTreeOp(GT_UDIV, TYP_INT, dividend, divisor)
            {
                RegNum = REG_R3,
                Flags = GTF_DIV_MOD_NO_BY_ZERO,
            };

            codeGen.genCodeForTreeNode(tree);

            var descriptors = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_udiv));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R3));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_R1));
            Assert.That(descriptors[0].idReg3(), Is.EqualTo(REG_R2));
        });
    }

    [Test]
    public static void FloatingDivisionUsesTheBinaryGenerator()
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            var dividend = compiler.gtNewDconNode(TYP_DOUBLE, 1.0);
            dividend.RegNum = REG_V0;
            var divisor = compiler.gtNewDconNode(TYP_DOUBLE, 2.0);
            divisor.RegNum = REG_V1;
            var tree = new GenTreeOp(GT_DIV, TYP_DOUBLE, dividend, divisor)
            {
                RegNum = REG_V2,
            };

            codeGen.genCodeForTreeNode(tree);

            var descriptors = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_fdiv));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_8BYTE));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_V2));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_V0));
            Assert.That(descriptors[0].idReg3(), Is.EqualTo(REG_V1));
        });
    }

    [Test]
    public static void SignedDivisionEmitsItsOverflowCheckBeforeTheDivide()
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            PrepareThrowTarget(compiler, SCK_ARITH_EXCPN);
            var dividend = compiler.gtNewIconNode(TYP_INT, int.MinValue);
            dividend.RegNum = REG_R1;
            var divisor = compiler.gtNewLclvNode(TYP_INT, 0);
            divisor.RegNum = REG_R2;
            var tree = new GenTreeOp(GT_DIV, TYP_INT, dividend, divisor)
            {
                RegNum = REG_R3,
                Flags = GTF_DIV_MOD_NO_BY_ZERO,
            };

            Assert.That(tree.Exceptions(compiler) & ExceptionSetFlags.ArithmeticException, Is.Not.Zero);
            codeGen.genCodeForTreeNode(tree);

            var descriptors = AllDescriptors(codeGen.Emitter, compiler);
            Assert.That(descriptors, Has.Count.EqualTo(5));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_cmn));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(INS_bne));
            Assert.That(descriptors[2].idIns(), Is.EqualTo(INS_cmp));
            Assert.That(descriptors[3].idIns(), Is.EqualTo(INS_bvs));
            Assert.That(descriptors[4].idIns(), Is.EqualTo(INS_sdiv));
        });
    }

    [Test]
    public static void VariableDivisorZeroChecksReachTheUnportedRegisterBranchDependency()
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            PrepareThrowTarget(compiler, SCK_DIV_BY_ZERO);
            var dividend = compiler.gtNewIconNode(TYP_INT, 7);
            dividend.RegNum = REG_R1;
            var divisor = compiler.gtNewLclvNode(TYP_INT, 0);
            divisor.RegNum = REG_R2;
            var tree = new GenTreeOp(GT_UDIV, TYP_INT, dividend, divisor)
            {
                RegNum = REG_R3,
            };

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree)) ??
                throw new AssertionException("The unported ARM64 register-branch dependency did not fail.");

            Assert.That(failure.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure.Message, Is.EqualTo("ARM64 emitIns_J_R recording is not ported."));
            Assert.That(Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter), Is.Empty);
        });
    }

    private static List<Emitter.instrDesc> AllDescriptors(Emitter emitter, Compiler compiler)
    {
        var descriptors = new List<Emitter.instrDesc>();
        var firstGroup = FirstGroup(emitter);
        while ((firstGroup is not null) && (firstGroup.igInsCnt == 0))
        {
            firstGroup = firstGroup.igNext;
        }
        var firstInstructionGroup = firstGroup ?? throw new AssertionException("Missing instruction group.");
        Walk(emitter, new emitLocation(firstInstructionGroup), (descriptor, _) => descriptors.Add(descriptor), compiler);
        return descriptors;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitWalkIDs")]
    private static extern void Walk(Emitter emitter, emitLocation location,
        Action<Emitter.instrDesc, Compiler> process, Compiler context);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitIGlist")]
    private static extern ref insGroup? FirstGroup(Emitter emitter);

    private static void PrepareThrowTarget(Compiler compiler, SpecialCodeKind kind)
    {
        compiler.compCurBB = new BasicBlock(null, null);
        var target = new BasicBlock(null, null);
        target.SetFlags(BBF_HAS_LABEL | BBF_THROW_HELPER);
        var descriptor = compiler.fgGetExcptnTarget(kind, compiler.compCurBB);
        descriptor.acdUsed = true;
        descriptor.acdDstBlk = target;
#if !FEATURE_FIXED_OUT_ARGS
        descriptor.acdStkLvlInit = true;
#endif
    }
}
#endif
