// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.GenCondition.CodeKind;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.emitJumpKind;
using static RyuJitSharp.insCFlags;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenConditionalCompareTests
{
    [TestCase(EJ_NONE, INS_none)]
    [TestCase(EJ_jmp, INS_none)]
    [TestCase(EJ_jo, INS_ccmpo)]
    [TestCase(EJ_jno, INS_ccmpno)]
    [TestCase(EJ_jb, INS_ccmpb)]
    [TestCase(EJ_jae, INS_ccmpae)]
    [TestCase(EJ_je, INS_ccmpe)]
    [TestCase(EJ_jne, INS_ccmpne)]
    [TestCase(EJ_jbe, INS_ccmpbe)]
    [TestCase(EJ_ja, INS_ccmpa)]
    [TestCase(EJ_js, INS_ccmps)]
    [TestCase(EJ_jns, INS_ccmpns)]
    [TestCase(EJ_jp, INS_none)]
    [TestCase(EJ_jnp, INS_none)]
    [TestCase(EJ_jl, INS_ccmpl)]
    [TestCase(EJ_jge, INS_ccmpge)]
    [TestCase(EJ_jle, INS_ccmple)]
    [TestCase(EJ_jg, INS_ccmpg)]
    public static void JumpTableRetainsNativeConditionMapping(emitJumpKind jump, instruction expected)
    {
        Assert.That(CodeGen.JumpKindToCcmp(jump), Is.EqualTo(expected));
    }

    [TestCase(EQ, TYP_INT, true, 0, INS_FLAGS_CF, INS_cteste)]
    [TestCase(NE, TYP_LONG, true, 42, INS_FLAGS_ZF, INS_ccmpne)]
    [TestCase(ULT, TYP_INT, false, 0, INS_FLAGS_SF, INS_ccmpb)]
    [TestCase(SGE, TYP_LONG, false, 0, INS_FLAGS_OF, INS_ccmpge)]
    [TestCase(O, TYP_INT, true, -1, INS_FLAGS_NONE, INS_ccmpo)]
    [TestCase(SLT, TYP_LONG, true, 0, INS_FLAGS_CF | INS_FLAGS_ZF | INS_FLAGS_SF | INS_FLAGS_OF, INS_ctestl)]
    public static void ConditionalComparePreservesFlagsAndZeroTestSelection(
        GenCondition.CodeKind condition, var_types type, bool immediate, int value, insCFlags flags, instruction expected)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            codeGen.Emitter.UsePromotedEvexEncodings = true;
            var op2 = compiler.gtNewIconNode(type, value);
            op2.IsContained = immediate;
            op2.RegNum = immediate ? REG_NA : REG_RCX;
            var tree = new GenTreeCCMP(TYP_VOID, new GenCondition(condition),
                Register(compiler, type, REG_RAX), op2, flags);
            codeGen.genCodeForCCMP(tree);
            var ids = Descriptors(codeGen);

            Assert.That(ids, Has.Count.EqualTo(1));
            Assert.That(ids[0].idIns(), Is.EqualTo(expected));
            Assert.That(ids[0].idOpSize(), Is.EqualTo(type == TYP_INT ? EA_4BYTE : EA_8BYTE));
            Assert.That(ids[0].idGetEvexDFV(), Is.EqualTo((uint)flags));
            if (immediate && value == 0)
            {
                Assert.That(ids[0].idReg2(), Is.EqualTo(REG_RAX));
            }
            else if (immediate)
            {
                Assert.That(InstructionConstant(codeGen.Emitter, ids[0]), Is.EqualTo((nint)value));
            }
            else
            {
                Assert.That(ids[0].idReg2(), Is.EqualTo(REG_RCX));
            }
        });
    }
}
