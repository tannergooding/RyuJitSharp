// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
#if TARGET_X86 || TARGET_ARM || TARGET_LOONGARCH64 || TARGET_RISCV64
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regMask;
#endif
#if !FEATURE_FIXED_OUT_ARGS
using static RyuJitSharp.SpecialCodeKind;
#endif

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

#if FEATURE_FIXED_OUT_ARGS || TARGET_WASM
    [TestCase(0U)]
    [TestCase(12U)]
    [TestCase(uint.MaxValue)]
    public static void FixedArgumentStackAdjustmentPreservesTheNativeEmptyBody(uint stackLevel)
    {
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
#endif

#if !FEATURE_FIXED_OUT_ARGS
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
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var block = (BasicBlock)RuntimeHelpers.GetUninitializedObject(typeof(BasicBlock));
        block.SetFlags(BBF_THROW_HELPER);
#if DEBUG
        block.bbTgtStkDepth = stackLevel;
#endif
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
#endif

#if TARGET_X86 || TARGET_ARM || TARGET_LOONGARCH64 || TARGET_RISCV64
    private const regMask CalleeTrash = SRBM_INT_CALLEE_TRASH | SRBM_FLT_CALLEE_TRASH;

#if TARGET_X86
#if FEATURE_USE_ASM_GC_WRITE_BARRIERS
    [TestCase(CORINFO_HELP_ASSIGN_REF, SRBM_EAX | SRBM_EDX)]
    [TestCase(CORINFO_HELP_CHECKED_ASSIGN_REF, SRBM_EAX | SRBM_EDX)]
#else
    [TestCase(CORINFO_HELP_ASSIGN_REF, CalleeTrash)]
    [TestCase(CORINFO_HELP_CHECKED_ASSIGN_REF, CalleeTrash)]
#endif
    [TestCase(CORINFO_HELP_PROF_FCN_ENTER, SRBM_NONE)]
    [TestCase(CORINFO_HELP_PROF_FCN_LEAVE, SRBM_NONE)]
    [TestCase(CORINFO_HELP_PROF_FCN_TAILCALL, SRBM_EAX | SRBM_FLT_CALLEE_TRASH)]
    [TestCase(CORINFO_HELP_INIT_PINVOKE_FRAME, SRBM_EAX | SRBM_ESI)]
    [TestCase(CORINFO_HELP_VALIDATE_INDIRECT_CALL, SRBM_EAX | SRBM_EDX)]
    [TestCase(CORINFO_HELP_ASSIGN_REF_EAX, SRBM_EDX)]
    [TestCase(CORINFO_HELP_ASSIGN_REF_ECX, SRBM_EDX)]
    [TestCase(CORINFO_HELP_ASSIGN_REF_EBX, SRBM_EDX)]
    [TestCase(CORINFO_HELP_ASSIGN_REF_EBP, SRBM_EDX)]
    [TestCase(CORINFO_HELP_ASSIGN_REF_ESI, SRBM_EDX)]
    [TestCase(CORINFO_HELP_ASSIGN_REF_EDI, SRBM_EDX)]
    [TestCase(CORINFO_HELP_CHECKED_ASSIGN_REF_EAX, SRBM_EDX)]
    [TestCase(CORINFO_HELP_CHECKED_ASSIGN_REF_ECX, SRBM_EDX)]
    [TestCase(CORINFO_HELP_CHECKED_ASSIGN_REF_EBX, SRBM_EDX)]
    [TestCase(CORINFO_HELP_CHECKED_ASSIGN_REF_EBP, SRBM_EDX)]
    [TestCase(CORINFO_HELP_CHECKED_ASSIGN_REF_ESI, SRBM_EDX)]
    [TestCase(CORINFO_HELP_CHECKED_ASSIGN_REF_EDI, SRBM_EDX)]
    [TestCase(CORINFO_HELP_STOP_FOR_GC, CalleeTrash)]
#elif TARGET_ARM
    [TestCase(CORINFO_HELP_ASSIGN_REF, SRBM_R0 | SRBM_R3 | SRBM_LR | SRBM_R12)]
    [TestCase(CORINFO_HELP_CHECKED_ASSIGN_REF, SRBM_R0 | SRBM_R3 | SRBM_LR | SRBM_R12)]
    [TestCase(CORINFO_HELP_PROF_FCN_ENTER, SRBM_NONE)]
    [TestCase(CORINFO_HELP_PROF_FCN_LEAVE, SRBM_R2)]
    [TestCase(CORINFO_HELP_PROF_FCN_TAILCALL, SRBM_NONE)]
    [TestCase(CORINFO_HELP_INIT_PINVOKE_FRAME, CalleeTrash | SRBM_R5 | SRBM_R6)]
    [TestCase(CORINFO_HELP_VALIDATE_INDIRECT_CALL, SRBM_INT_CALLEE_TRASH)]
    [TestCase(CORINFO_HELP_STOP_FOR_GC, SRBM_R2 | SRBM_R3 | SRBM_R12 | SRBM_LR |
        SRBM_F8 | SRBM_F9 | SRBM_F10 | SRBM_F11 | SRBM_F12 | SRBM_F13 | SRBM_F14 | SRBM_F15)]
#elif TARGET_LOONGARCH64
    private const regMask ProfilerTrash =
        SRBM_T0 | SRBM_T1 | SRBM_T2 | SRBM_T3 | SRBM_T4 | SRBM_T5 | SRBM_T6 | SRBM_T7 | SRBM_T8;

    [TestCase(CORINFO_HELP_ASSIGN_REF, SRBM_T0 | SRBM_T1 | SRBM_T3 | SRBM_T4 | SRBM_T7)]
    [TestCase(CORINFO_HELP_CHECKED_ASSIGN_REF, SRBM_T0 | SRBM_T1 | SRBM_T3 | SRBM_T4 | SRBM_T7)]
    [TestCase(CORINFO_HELP_PROF_FCN_ENTER, ProfilerTrash)]
    [TestCase(CORINFO_HELP_PROF_FCN_LEAVE, ProfilerTrash)]
    [TestCase(CORINFO_HELP_PROF_FCN_TAILCALL, ProfilerTrash)]
    [TestCase(CORINFO_HELP_INIT_PINVOKE_FRAME, CalleeTrash)]
    [TestCase(CORINFO_HELP_VALIDATE_INDIRECT_CALL, SRBM_T0 | SRBM_T1 | SRBM_T2 |
        SRBM_T4 | SRBM_T5 | SRBM_T6 | SRBM_T7 | SRBM_T8)]
    [TestCase(CORINFO_HELP_STOP_FOR_GC, CalleeTrash)]
#else
    private const regMask ProfilerTrash =
        SRBM_T0 | SRBM_T1 | SRBM_T2 | SRBM_T3 | SRBM_T4 | SRBM_T5 | SRBM_T6 |
        SRBM_FT0 | SRBM_FT1 | SRBM_FT2 | SRBM_FT3 | SRBM_FT4 | SRBM_FT5 |
        SRBM_FT6 | SRBM_FT7 | SRBM_FT8 | SRBM_FT9 | SRBM_FT10 | SRBM_FT11;

    [TestCase(CORINFO_HELP_ASSIGN_REF, SRBM_T0 | SRBM_T1 | SRBM_T2 | SRBM_T4 | SRBM_T6)]
    [TestCase(CORINFO_HELP_CHECKED_ASSIGN_REF, SRBM_T0 | SRBM_T1 | SRBM_T2 | SRBM_T4 | SRBM_T6)]
    [TestCase(CORINFO_HELP_PROF_FCN_ENTER, ProfilerTrash)]
    [TestCase(CORINFO_HELP_PROF_FCN_LEAVE, ProfilerTrash)]
    [TestCase(CORINFO_HELP_PROF_FCN_TAILCALL, ProfilerTrash)]
    [TestCase(CORINFO_HELP_INIT_PINVOKE_FRAME, CalleeTrash)]
    [TestCase(CORINFO_HELP_VALIDATE_INDIRECT_CALL,
        SRBM_T0 | SRBM_T1 | SRBM_T2 | SRBM_T4 | SRBM_T5 | SRBM_T6)]
    [TestCase(CORINFO_HELP_STOP_FOR_GC, CalleeTrash)]
#endif
    [TestCase(CORINFO_HELP_UNDEF, CalleeTrash)]
    [TestCase(CORINFO_HELP_INTERFACELOOKUP_FOR_SLOT, CalleeTrash)]
    public static void HelperMasksMatchPinnedTargetCallingConventions(CorInfoHelpFunc helper, regMask expected)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));

        Assert.That(compiler.compHelperCallKillSet(helper), Is.EqualTo(new regMaskTP(expected)));
    }
#endif
}
