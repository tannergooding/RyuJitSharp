// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.GCInfo.WriteBarrierForm;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenXarchResidualClosureTests
{
    [TestCase(WBF_BarrierUnknown)]
    [TestCase(WBF_BarrierChecked)]
    [TestCase(WBF_BarrierUnchecked)]
    public static void OptimizedX86BarrierFormsDoNotReplaceAmd64Stores(GCInfo.WriteBarrierForm form)
    {
        Assume.That(Configuration.TARGET_AMD64);
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var address = Register(compiler, TYP_BYREF, REG_X64_RAX);
            var data = Register(compiler, TYP_REF, REG_X64_RCX);
            var before = codeGen.Emitter.emitCurIG;

            Assert.That(codeGen.genEmitOptimizedGCWriteBarrier(form, address, data), Is.False);
            Assert.That(codeGen.Emitter.emitCurIG, Is.SameAs(before));
        });
    }

    [TestCase(WBF_BarrierUnchecked, REG_X86_EAX, CORINFO_HELP_ASSIGN_REF_EAX)]
    [TestCase(WBF_BarrierUnchecked, REG_X86_ECX, CORINFO_HELP_ASSIGN_REF_ECX)]
    [TestCase(WBF_BarrierUnchecked, REG_X86_EBX, CORINFO_HELP_ASSIGN_REF_EBX)]
    [TestCase(WBF_BarrierUnchecked, REG_X86_EBP, CORINFO_HELP_ASSIGN_REF_EBP)]
    [TestCase(WBF_BarrierUnchecked, REG_X86_ESI, CORINFO_HELP_ASSIGN_REF_ESI)]
    [TestCase(WBF_BarrierUnchecked, REG_X86_EDI, CORINFO_HELP_ASSIGN_REF_EDI)]
    [TestCase(WBF_BarrierChecked, REG_X86_EAX, CORINFO_HELP_CHECKED_ASSIGN_REF_EAX)]
    [TestCase(WBF_BarrierChecked, REG_X86_ECX, CORINFO_HELP_CHECKED_ASSIGN_REF_ECX)]
    [TestCase(WBF_BarrierChecked, REG_X86_EBX, CORINFO_HELP_CHECKED_ASSIGN_REF_EBX)]
    [TestCase(WBF_BarrierChecked, REG_X86_EBP, CORINFO_HELP_CHECKED_ASSIGN_REF_EBP)]
    [TestCase(WBF_BarrierChecked, REG_X86_ESI, CORINFO_HELP_CHECKED_ASSIGN_REF_ESI)]
    [TestCase(WBF_BarrierChecked, REG_X86_EDI, CORINFO_HELP_CHECKED_ASSIGN_REF_EDI)]
    public static void X86OptimizedBarriersSelectTheValueRegisterHelper(
        GCInfo.WriteBarrierForm form, regNumber sourceReg, CorInfoHelpFunc expected)
    {
        Assume.That(Configuration.TARGET_X86 && Configuration.NOGC_WRITE_BARRIERS);
        var helpers = OptimizedBarrierHelpers(null);
        var row = form is WBF_BarrierUnchecked ? 0 : 1;

        Assert.That(helpers[row][(int)sourceReg], Is.EqualTo(expected));
        Assert.That(helpers[row][(int)REG_X86_EDX], Is.EqualTo((CorInfoHelpFunc)(-1)));
        Assert.That(helpers[row][(int)REG_X86_ESP], Is.EqualTo((CorInfoHelpFunc)(-1)));
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "s_optimizedWriteBarrierHelpers")]
    private static extern ref CorInfoHelpFunc[][] OptimizedBarrierHelpers(CodeGen? codeGen);

    [TestCase(false, TYP_INT, true)]
    [TestCase(true, TYP_INT, true)]
    [TestCase(false, TYP_UINT, true)]
    [TestCase(true, TYP_UINT, true)]
    [TestCase(false, TYP_INT, false)]
    [TestCase(true, TYP_UINT, false)]
    public static void X86LongToIntCastsCheckTheNativeHighBitsBeforeMovingTheLowWord(
        bool sourceUnsigned, var_types destination, bool overflow)
    {
        Assume.That(Configuration.TARGET_X86);
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var low = Register(compiler, TYP_INT, REG_X86_EAX);
            var high = Register(compiler, TYP_INT, REG_X86_ECX);
            var pair = new GenTreeOp(GT_LONG, var_types.TYP_LONG, low, high);
            var cast = new GenTreeCast(TYP_INT, pair, sourceUnsigned, destination) { RegNum = REG_X64_EDX };
            if (overflow)
            {
                cast.Flags |= GTF_OVERFLOW;
                _ = CodeGenBinaryTests.PrepareThrowTarget(compiler);
            }

            var firstGroup = codeGen.Emitter.emitCurIG;
            codeGen.genLongToIntCast(cast);

            var descriptors = CodeGenLocalHeapTests.AllDescriptors(firstGroup, codeGen);
            instruction[] expected = !overflow
                ? [INS_mov]
                : (sourceUnsigned, destination) switch
                {
                    (false, TYP_INT) => [INS_test, INS_js, INS_test, INS_jne, INS_jmp,
                        INS_cmp, INS_jne, INS_mov],
                    (true, TYP_INT) => [INS_test, INS_js, INS_test, INS_jne, INS_mov],
                    _ => [INS_test, INS_jne, INS_mov],
                };
            Assert.That(descriptors.Select(static descriptor => descriptor.idIns()), Is.EqualTo(expected));
            Assert.That(descriptors[^1].idReg1(), Is.EqualTo(REG_X64_EDX));
            Assert.That(descriptors[^1].idReg2(), Is.EqualTo(REG_X86_EAX));
            Assert.That(descriptors[^1].idOpSize(), Is.EqualTo(EA_4BYTE));
            if (!sourceUnsigned && (destination is TYP_INT) && overflow)
            {
                Assert.That(InstructionConstant(codeGen.Emitter, descriptors[5]), Is.EqualTo((nint)(-1)));
            }
        });
    }
}
