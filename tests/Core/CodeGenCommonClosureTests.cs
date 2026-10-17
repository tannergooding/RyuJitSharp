// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.SpecialCodeKind;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenCommonClosureTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void ThrowHelperClassificationUsesTheCanonicalBlockFlag(bool isThrowHelper)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var block = (BasicBlock)RuntimeHelpers.GetUninitializedObject(typeof(BasicBlock));
        if (isThrowHelper)
        {
            block.SetFlags(BBF_THROW_HELPER);
        }

        Assert.That(compiler.fgIsThrowHlpBlk(block), Is.EqualTo(isThrowHelper));
    }

    [TestCase(0U)]
    [TestCase(12U)]
    [TestCase(uint.MaxValue)]
    public static void FixedArgumentStackAdjustmentPreservesTheNativeEmptyBody(uint stackLevel)
    {
        Assume.That(
            Configuration.FEATURE_FIXED_OUT_ARGS || Configuration.TARGET_WASM,
            "Fixed-argument stack adjustment is only available for fixed-out-args or WebAssembly targets.");

        var codeGen = (CodeGen)RuntimeHelpers.GetUninitializedObject(typeof(CodeGen));
        var block = (BasicBlock)RuntimeHelpers.GetUninitializedObject(typeof(BasicBlock));
        StackLevel(codeGen) = stackLevel;

        AdjustStackLevel(codeGen, block);

        Assert.That(codeGen.getCurrentStackLevel(), Is.EqualTo(stackLevel));
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genAdjustStackLevel")]
    private static extern void AdjustStackLevel(CodeGen codeGen, BasicBlock block);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "genStackLevel")]
    private static extern ref uint StackLevel(CodeGen codeGen);

    [TestCase(SCK_RNGCHK_FAIL, 2)]
    [TestCase(SCK_DIV_BY_ZERO, 2)]
    [TestCase(SCK_OVERFLOW, 2)]
    [TestCase(SCK_ARG_EXCPN, 2)]
    [TestCase(SCK_ARG_RNG_EXCPN, 2)]
    [TestCase(SCK_FAIL_FAST, 2)]
    [TestCase(SCK_OVERFLOW, 0)]
    [TestCase(SCK_OVERFLOW, int.MaxValue)]
    [TestCase(SCK_OVERFLOW, -1)]
    [TestCase(SCK_OVERFLOW, int.MinValue)]
    public static void ThrowHelperStackLevelFindsTheBlockAndPreservesUnsignedBits(
        SpecialCodeKind kind, int stackLevel)
    {
        Assume.That(!Configuration.FEATURE_FIXED_OUT_ARGS, "This test requires variable outgoing arguments.");

        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var block = (BasicBlock)RuntimeHelpers.GetUninitializedObject(typeof(BasicBlock));
        block.SetFlags(BBF_THROW_HELPER);
        if (TestConfiguration.IsDebug)
        {
            block.bbTgtStkDepth = stackLevel;
        }

        var unrelated = new Compiler.AddCodeDsc {
            acdKind = SCK_NULL_CHECK,
            acdDstBlk = (BasicBlock)RuntimeHelpers.GetUninitializedObject(typeof(BasicBlock)),
            acdStkLvl = 7,
        };
        var matched = new Compiler.AddCodeDsc {
            acdKind = kind,
            acdDstBlk = block,
            acdStkLvl = stackLevel,
        };
        var map = compiler.fgGetAddCodeDscMap();
        map[new Compiler.AddCodeDscKey(unrelated)] = unrelated;
        map[new Compiler.AddCodeDscKey(matched)] = matched;

        Assert.That(compiler.fgThrowHlpBlkStkLevel(block), Is.EqualTo(unchecked((uint)stackLevel)));
    }

    private static regMask CalleeTrash => SRBM_INT_CALLEE_TRASH | SRBM_FLT_CALLEE_TRASH;

    private static IEnumerable<TestCaseData> HelperMaskCases
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                yield return new TestCaseData(CORINFO_HELP_ASSIGN_REF, SRBM_EAX | SRBM_EDX);
                yield return new TestCaseData(CORINFO_HELP_CHECKED_ASSIGN_REF, SRBM_EAX | SRBM_EDX);
                yield return new TestCaseData(CORINFO_HELP_PROF_FCN_ENTER, SRBM_NONE);
                yield return new TestCaseData(CORINFO_HELP_PROF_FCN_LEAVE, SRBM_NONE);
                yield return new TestCaseData(CORINFO_HELP_PROF_FCN_TAILCALL, SRBM_EAX | SRBM_FLT_CALLEE_TRASH);
                yield return new TestCaseData(CORINFO_HELP_INIT_PINVOKE_FRAME, SRBM_EAX | SRBM_ESI);
                yield return new TestCaseData(CORINFO_HELP_VALIDATE_INDIRECT_CALL, SRBM_EAX | SRBM_EDX);
                yield return new TestCaseData(CORINFO_HELP_ASSIGN_REF_EAX, SRBM_EDX);
                yield return new TestCaseData(CORINFO_HELP_ASSIGN_REF_ECX, SRBM_EDX);
                yield return new TestCaseData(CORINFO_HELP_ASSIGN_REF_EBX, SRBM_EDX);
                yield return new TestCaseData(CORINFO_HELP_ASSIGN_REF_EBP, SRBM_EDX);
                yield return new TestCaseData(CORINFO_HELP_ASSIGN_REF_ESI, SRBM_EDX);
                yield return new TestCaseData(CORINFO_HELP_ASSIGN_REF_EDI, SRBM_EDX);
                yield return new TestCaseData(CORINFO_HELP_CHECKED_ASSIGN_REF_EAX, SRBM_EDX);
                yield return new TestCaseData(CORINFO_HELP_CHECKED_ASSIGN_REF_ECX, SRBM_EDX);
                yield return new TestCaseData(CORINFO_HELP_CHECKED_ASSIGN_REF_EBX, SRBM_EDX);
                yield return new TestCaseData(CORINFO_HELP_CHECKED_ASSIGN_REF_EBP, SRBM_EDX);
                yield return new TestCaseData(CORINFO_HELP_CHECKED_ASSIGN_REF_ESI, SRBM_EDX);
                yield return new TestCaseData(CORINFO_HELP_CHECKED_ASSIGN_REF_EDI, SRBM_EDX);
                yield return new TestCaseData(CORINFO_HELP_STOP_FOR_GC, CalleeTrash);
            }
            else if (Configuration.TARGET_ARM)
            {
                yield return new TestCaseData(CORINFO_HELP_ASSIGN_REF, SRBM_R0 | SRBM_R3 | SRBM_LR | SRBM_R12);
                yield return new TestCaseData(CORINFO_HELP_CHECKED_ASSIGN_REF, SRBM_R0 | SRBM_R3 | SRBM_LR | SRBM_R12);
                yield return new TestCaseData(CORINFO_HELP_PROF_FCN_ENTER, SRBM_NONE);
                yield return new TestCaseData(CORINFO_HELP_PROF_FCN_LEAVE, SRBM_R2);
                yield return new TestCaseData(CORINFO_HELP_PROF_FCN_TAILCALL, SRBM_NONE);
                yield return new TestCaseData(CORINFO_HELP_INIT_PINVOKE_FRAME, CalleeTrash | SRBM_R5 | SRBM_R6);
                yield return new TestCaseData(CORINFO_HELP_VALIDATE_INDIRECT_CALL, SRBM_INT_CALLEE_TRASH);
                yield return new TestCaseData(CORINFO_HELP_STOP_FOR_GC,
                    SRBM_R2 | SRBM_R3 | SRBM_R12 | SRBM_LR |
                    SRBM_F8 | SRBM_F9 | SRBM_F10 | SRBM_F11 | SRBM_F12 | SRBM_F13 | SRBM_F14 | SRBM_F15);
            }
            else if (Configuration.TARGET_LOONGARCH64)
            {
                var profilerTrash =
                    SRBM_T0 | SRBM_T1 | SRBM_T2 | SRBM_T3 | SRBM_T4 | SRBM_T5 | SRBM_T6 | SRBM_T7 | SRBM_T8;

                yield return new TestCaseData(CORINFO_HELP_ASSIGN_REF, SRBM_T0 | SRBM_T1 | SRBM_T3 | SRBM_T4 | SRBM_T7);
                yield return new TestCaseData(
                    CORINFO_HELP_CHECKED_ASSIGN_REF, SRBM_T0 | SRBM_T1 | SRBM_T3 | SRBM_T4 | SRBM_T7);
                yield return new TestCaseData(CORINFO_HELP_PROF_FCN_ENTER, profilerTrash);
                yield return new TestCaseData(CORINFO_HELP_PROF_FCN_LEAVE, profilerTrash);
                yield return new TestCaseData(CORINFO_HELP_PROF_FCN_TAILCALL, profilerTrash);
                yield return new TestCaseData(CORINFO_HELP_INIT_PINVOKE_FRAME, CalleeTrash);
                yield return new TestCaseData(CORINFO_HELP_VALIDATE_INDIRECT_CALL,
                    SRBM_T0 | SRBM_T1 | SRBM_T2 | SRBM_T4 | SRBM_T5 | SRBM_T6 | SRBM_T7 | SRBM_T8);
                yield return new TestCaseData(CORINFO_HELP_STOP_FOR_GC, CalleeTrash);
            }
            else if (Configuration.TARGET_RISCV64)
            {
                var profilerTrash =
                    SRBM_T0 | SRBM_T1 | SRBM_T2 | SRBM_T3 | SRBM_T4 | SRBM_T5 | SRBM_T6 |
                    SRBM_FT0 | SRBM_FT1 | SRBM_FT2 | SRBM_FT3 | SRBM_FT4 | SRBM_FT5 |
                    SRBM_FT6 | SRBM_FT7 | SRBM_FT8 | SRBM_FT9 | SRBM_FT10 | SRBM_FT11;

                yield return new TestCaseData(
                    CORINFO_HELP_ASSIGN_REF, SRBM_T0 | SRBM_T1 | SRBM_T2 | SRBM_T4 | SRBM_T6);
                yield return new TestCaseData(
                    CORINFO_HELP_CHECKED_ASSIGN_REF, SRBM_T0 | SRBM_T1 | SRBM_T2 | SRBM_T4 | SRBM_T6);
                yield return new TestCaseData(CORINFO_HELP_PROF_FCN_ENTER, profilerTrash);
                yield return new TestCaseData(CORINFO_HELP_PROF_FCN_LEAVE, profilerTrash);
                yield return new TestCaseData(CORINFO_HELP_PROF_FCN_TAILCALL, profilerTrash);
                yield return new TestCaseData(CORINFO_HELP_INIT_PINVOKE_FRAME, CalleeTrash);
                yield return new TestCaseData(CORINFO_HELP_VALIDATE_INDIRECT_CALL,
                    SRBM_T0 | SRBM_T1 | SRBM_T2 | SRBM_T4 | SRBM_T5 | SRBM_T6);
                yield return new TestCaseData(CORINFO_HELP_STOP_FOR_GC, CalleeTrash);
            }
            else
            {
                yield return new TestCaseData(CORINFO_HELP_UNDEF, SRBM_NONE);
                yield break;
            }

            yield return new TestCaseData(CORINFO_HELP_UNDEF, CalleeTrash);
            yield return new TestCaseData(CORINFO_HELP_INTERFACELOOKUP_FOR_SLOT, CalleeTrash);
        }
    }

    [TestCaseSource(nameof(HelperMaskCases))]
    public static void HelperMasksMatchPinnedTargetCallingConventions(CorInfoHelpFunc helper, regMask expected)
    {
        Assume.That(
            Configuration.TARGET_X86 || Configuration.TARGET_ARM ||
                Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64,
            "Helper kill-set cases are only defined for x86, ARM32, LoongArch64, and RISC-V64.");

        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));

        Assert.That(compiler.compHelperCallKillSet(helper), Is.EqualTo(new regMaskTP(expected)));
    }
}
