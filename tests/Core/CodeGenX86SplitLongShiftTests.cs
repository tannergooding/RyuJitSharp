// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CodeGenX86SplitLongShiftTests
{
    [TestCaseSource(nameof(SplitLongShiftCases))]
    public static void SplitLongShiftsMoveTheCorrectHalfBeforeCombining(
        genTreeOps oper, int count, instruction expected, regNumber destination, regNumber otherHalf,
        regNumber copiedHalf, int instructionCount)
    {
        CodeGenTestSupport.WithCodeGen(TYP_INT, REG_EAX, (compiler, codeGen) =>
        {
            var low = compiler.gtNewIconNode(TYP_INT, 1);
            low.RegNum = REG_EAX;
            var high = compiler.gtNewIconNode(TYP_INT, 2);
            high.RegNum = REG_EDX;
            var pair = new GenTreeOp(GT_LONG, TYP_LONG, low, high);
            var amount = compiler.gtNewIconNode(TYP_INT, count);
            amount.IsContained = true;
            var tree = new GenTreeOp(oper, TYP_INT, pair, amount) { RegNum = destination };

            codeGen.genCodeForShiftLongXarch(tree);

            var descriptors = CurrentDescriptors(codeGen.Emitter)
                ?? throw new AssertionException("Missing descriptor buffer.");
            Assert.That(descriptors, Has.Count.EqualTo(instructionCount));
            if (instructionCount == 2)
            {
                Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_mov));
                Assert.That(descriptors[0].idReg1(), Is.EqualTo(destination));
                Assert.That(descriptors[0].idReg2(), Is.EqualTo(copiedHalf));
            }
            Assert.That(descriptors[^1].idIns(), Is.EqualTo(expected));
            Assert.That(descriptors[^1].idReg1(), Is.EqualTo(destination));
            Assert.That(descriptors[^1].idReg2(), Is.EqualTo(otherHalf));
            Assert.That(InstructionConstant(codeGen.Emitter, descriptors[^1]), Is.EqualTo((nint)count));
        });
    }

    private static IEnumerable<TestCaseData> SplitLongShiftCases()
    {
        if (!Configuration.TARGET_X86)
        {
            yield break;
        }

        yield return new TestCaseData(GT_LSH_HI, 1, INS_shld, REG_EDX, REG_EAX, REG_EDX, 1);
        yield return new TestCaseData(GT_LSH_HI, 33, INS_shld, REG_EBX, REG_EAX, REG_EDX, 2);
        yield return new TestCaseData(GT_RSH_LO, 1, INS_shrd, REG_EAX, REG_EDX, REG_EAX, 1);
        yield return new TestCaseData(GT_RSH_LO, 33, INS_shrd, REG_EBX, REG_EDX, REG_EAX, 2);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsCns")]
    private static extern nint InstructionConstant(Emitter emitter, Emitter.instrDesc descriptor);
}
