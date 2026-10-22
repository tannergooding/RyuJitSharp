// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1065:Do not raise exceptions in unexpected locations",
    Justification = "Target-qualified globals fail fast when the selected runtime target or ABI does not provide them.")]
public partial class Globals
{
    public const CorInfoArch CORINFO_ARCH_TARGET_AMD64 = CORINFO_ARCH_X64;

    /// <summary>For CpObj code generation, this is the threshold of the number of contiguous non-gc slots that trigger generating rep movsq instead of sequences of movsq instructions</summary>
    public const int CPOBJ_NONGC_SLOTS_LIMIT = 4;

    /// <summary>Maximum size of a struct passed in a single register (double).</summary>
    public const int MAX_PASS_SINGLEREG_BYTES_AMD64 = 8;

    /// <summary>Maximum size of a struct that could be passed in more than one register (Max is two simd16s)</summary>
    public const int MAX_PASS_MULTIREG_BYTES_X64_UNIX = 32;

    /// <summary>Maximum size of a struct that could be returned in more than one register  (Max is two simd16s)</summary>
    public const int MAX_RET_MULTIREG_BYTES_X64_UNIX = 32;

    /// <summary>Maximum registers used to pass a single argument in multiple registers.</summary>
    public const int MAX_ARG_REG_COUNT_X64_UNIX = 2;

    /// <summary>Maximum registers used to return a value.</summary>
    public const int MAX_RET_REG_COUNT_X64_UNIX = 4;

    /// <summary>Maximum number of registers defined by a single instruction (including calls).</summary>
    /// <remarks>This is also the maximum number of registers for a MultiReg node.</remarks>
    public const int MAX_MULTIREG_COUNT_X64_UNIX = 4;

    /// <summary>No multireg arguments</summary>
    public const int MAX_PASS_MULTIREG_BYTES_X64_WINDOWS = 0;

    /// <summary>No multireg return values</summary>
    public const int MAX_RET_MULTIREG_BYTES_X64_WINDOWS = 0;

    /// <summary>Maximum registers used to pass a single argument (no arguments are passed using multiple registers)</summary>
    public const int MAX_ARG_REG_COUNT_X64_WINDOWS = 1;

    /// <summary>Maximum registers used to return a value.</summary>
    public const int MAX_RET_REG_COUNT_X64_WINDOWS = 1;

    /// <summary>Maximum number of registers defined by a single instruction (including calls).</summary>
    /// <remarks>
    ///   <para>This is also the maximum number of registers for a MultiReg node.</para>
    ///   <para>Note that this must be greater than 1 so that GenTreeLclVar can have an array of MAX_MULTIREG_COUNT_X64_WINDOWS - 1.</para>
    /// </remarks>
    public const int MAX_MULTIREG_COUNT_X64_WINDOWS = 2;

    public const int MAX_REG_ARG_X64_UNIX = 6;

    public const int MAX_REG_ARG_X64_WINDOWS = 4;

    public const int MIN_ARG_AREA_FOR_CALL_X64_UNIX = 0;

    public const int MIN_ARG_AREA_FOR_CALL_X64_WINDOWS = 4 * REGSIZE_BYTES_AMD64;

    /// <summary>equal to sizeof(void*) and the managed pointer size in bytes for this target</summary>
    public const int TARGET_POINTER_SIZE_AMD64 = 8;

    public const int TARGET_MASKS_SHIFTS_AMD64 = 1;

    public const int TARGET_HAS_MULHI_AMD64 = 1;

    public const int CNT_HIGHFLOAT_AMD64 = 16;

    public const regMask SRBM_LOWFLOAT = SRBM_X64_XMM0 | SRBM_X64_XMM1 | SRBM_X64_XMM2 | SRBM_X64_XMM3 | SRBM_X64_XMM4 | SRBM_X64_XMM5 | SRBM_X64_XMM6 | SRBM_X64_XMM7 | SRBM_X64_XMM8 | SRBM_X64_XMM9 | SRBM_X64_XMM10 | SRBM_X64_XMM11 | SRBM_X64_XMM12 | SRBM_X64_XMM13 | SRBM_X64_XMM14 | SRBM_X64_XMM15;

    public const regMask SRBM_HIGHFLOAT = SRBM_X64_XMM16 | SRBM_X64_XMM17 | SRBM_X64_XMM18 | SRBM_X64_XMM19 | SRBM_X64_XMM20 | SRBM_X64_XMM21 | SRBM_X64_XMM22 | SRBM_X64_XMM23 | SRBM_X64_XMM24 | SRBM_X64_XMM25 | SRBM_X64_XMM26 | SRBM_X64_XMM27 | SRBM_X64_XMM28 | SRBM_X64_XMM29 | SRBM_X64_XMM30 | SRBM_X64_XMM31;

    public const regMask SRBM_ALLFLOAT_INIT = SRBM_LOWFLOAT;

    public const regNumber REG_FP_FIRST_AMD64 = REG_X64_XMM0;

    public const regNumber REG_FP_LAST_AMD64 = REG_X64_XMM31;

    public const regNumber FIRST_FP_ARGREG_AMD64 = REG_X64_XMM0;

    public const regNumber REG_MASK_FIRST_AMD64 = REG_X64_K0;

    public const regNumber REG_MASK_LAST_AMD64 = REG_X64_K7;

    public const regMask SRBM_ALLMASK_INIT_AMD64 = SRBM_NONE;

    public const regMask SRBM_ALLMASK_EVEX_AMD64 = SRBM_X64_K1 | SRBM_X64_K2 | SRBM_X64_K3 | SRBM_X64_K4 | SRBM_X64_K5 | SRBM_X64_K6 | SRBM_X64_K7;

    public const int CNT_MASK_REGS_AMD64 = 8;

    public static regNumber LAST_FP_ARGREG_AMD64 =>
        Configuration.UNIX_AMD64_ABI ? REG_X64_XMM7 : REG_X64_XMM3;

    /// <summary>number of bits in a REG_*</summary>
    public const int REGNUM_BITS_AMD64 = 7;

    /// <summary>number of bytes in one register</summary>
    public const int REGSIZE_BYTES_AMD64 = 8;

    /// <summary>XMM register size in bytes</summary>
    public const int XMM_REGSIZE_BYTES_AMD64 = 16;

    /// <summary>YMM register size in bytes</summary>
    public const int YMM_REGSIZE_BYTES_AMD64 = 32;

    /// <summary>ZMM register size in bytes</summary>
    public const int ZMM_REGSIZE_BYTES_AMD64 = 64;

    /// <summary>code alignment requirement</summary>
    public const int CODE_ALIGN_AMD64 = 1;

    /// <summary>stack alignment requirement</summary>
    public const int STACK_ALIGN_AMD64 = 16;

    /// <summary>Shift-right amount to convert size in bytes to size in STACK_ALIGN_AMD64 units == log2(STACK_ALIGN_AMD64)</summary>
    public const int STACK_ALIGN_SHIFT_AMD64 = 4;

    public static regMask SRBM_ETW_FRAMED_EBP =>
        Configuration.ETW_EBP_FRAMED ? SRBM_NONE : SRBM_X64_EBP;

    public static regMask SRBM_ETW_FRAMED_EBP_LIST => SRBM_ETW_FRAMED_EBP;

    public static ReadOnlySpan<regNumber> REG_ETW_FRAMED_EBP_LIST =>
        Configuration.ETW_EBP_FRAMED ? [] : [REG_X64_EBP];

    public static int REG_ETW_FRAMED_EBP_COUNT => Configuration.ETW_EBP_FRAMED ? 0 : 1;

    /// <summary>Minimum required outgoing argument space for a call.</summary>
    public static int MIN_ARG_AREA_FOR_CALL_AMD64 =>
        Configuration.UNIX_AMD64_ABI ? 0 : 4 * REGSIZE_BYTES_AMD64;

    public static regMask SRBM_INT_CALLEE_SAVED_AMD64 => Configuration.UNIX_AMD64_ABI
        ? SRBM_X64_EBX | SRBM_ETW_FRAMED_EBP | SRBM_X64_R12 | SRBM_X64_R13 | SRBM_X64_R14 | SRBM_X64_R15
        : SRBM_X64_EBX | SRBM_X64_ESI | SRBM_X64_EDI | SRBM_ETW_FRAMED_EBP
            | SRBM_X64_R12 | SRBM_X64_R13 | SRBM_X64_R14 | SRBM_X64_R15;

    public static regMask SRBM_INT_CALLEE_TRASH_INIT => Configuration.UNIX_AMD64_ABI
        ? SRBM_X64_EAX | SRBM_X64_RDI | SRBM_X64_RSI | SRBM_X64_EDX | SRBM_X64_ECX
            | SRBM_X64_R8 | SRBM_X64_R9 | SRBM_X64_R10 | SRBM_X64_R11
        : SRBM_X64_EAX | SRBM_X64_ECX | SRBM_X64_EDX | SRBM_X64_R8
            | SRBM_X64_R9 | SRBM_X64_R10 | SRBM_X64_R11;

    public static regMask SRBM_FLT_CALLEE_SAVED_AMD64 => Configuration.UNIX_AMD64_ABI
        ? SRBM_NONE
        : SRBM_X64_XMM6 | SRBM_X64_XMM7 | SRBM_X64_XMM8 | SRBM_X64_XMM9 | SRBM_X64_XMM10
            | SRBM_X64_XMM11 | SRBM_X64_XMM12 | SRBM_X64_XMM13 | SRBM_X64_XMM14 | SRBM_X64_XMM15;

    public static regMask SRBM_FLT_CALLEE_TRASH_INIT => Configuration.UNIX_AMD64_ABI
        ? SRBM_X64_XMM0 | SRBM_X64_XMM1 | SRBM_X64_XMM2 | SRBM_X64_XMM3
            | SRBM_X64_XMM4 | SRBM_X64_XMM5 | SRBM_X64_XMM6 | SRBM_X64_XMM7
            | SRBM_X64_XMM8 | SRBM_X64_XMM9 | SRBM_X64_XMM10 | SRBM_X64_XMM11
            | SRBM_X64_XMM12 | SRBM_X64_XMM13 | SRBM_X64_XMM14 | SRBM_X64_XMM15
        : SRBM_X64_XMM0 | SRBM_X64_XMM1 | SRBM_X64_XMM2 | SRBM_X64_XMM3
            | SRBM_X64_XMM4 | SRBM_X64_XMM5;

    public static regNumber REG_PROFILER_ENTER_ARG_0
    {
        get
        {
            if (Configuration.UNIX_AMD64_ABI)
            {
                return REG_X64_R14;
            }
            else
            {
                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.ProfilerEntryRegisterIsUnavailableForThisABI);
                return default!;
            }
        }
    }

    public static regMask SRBM_PROFILER_ENTER_ARG_0
    {
        get
        {
            if (Configuration.UNIX_AMD64_ABI)
            {
                return SRBM_X64_R14;
            }
            else
            {
                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.ProfilerEntryRegisterIsUnavailableForThisABI);
                return default!;
            }
        }
    }

    public static regNumber REG_PROFILER_ENTER_ARG_1
    {
        get
        {
            if (Configuration.UNIX_AMD64_ABI)
            {
                return REG_X64_R15;
            }
            else
            {
                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.ProfilerEntryRegisterIsUnavailableForThisABI);
                return default!;
            }
        }
    }

    public static regMask SRBM_PROFILER_ENTER_ARG_1
    {
        get
        {
            if (Configuration.UNIX_AMD64_ABI)
            {
                return SRBM_X64_R15;
            }
            else
            {
                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.ProfilerEntryRegisterIsUnavailableForThisABI);
                return default!;
            }
        }
    }

    public static regNumber REG_DEFAULT_PROFILER_CALL_TARGET
    {
        get
        {
            if (Configuration.UNIX_AMD64_ABI)
            {
                return REG_X64_R11;
            }
            else
            {
                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.ProfilerCallTargetIsUnavailableForThisABI);
                return default!;
            }
        }
    }

    public const regMask SRBM_MSK_CALLEE_TRASH_INIT_AMD64 = SRBM_NONE;

    public const regMask SRBM_MSK_CALLEE_TRASH_EVEX_AMD64 = SRBM_ALLMASK_EVEX_AMD64;

    public const regMask SRBM_MSK_CALLEE_SAVED_AMD64 = SRBM_NONE;

    public static regMask SRBM_OSR_INT_CALLEE_SAVED => SRBM_INT_CALLEE_SAVED_AMD64 | SRBM_X64_EBP;

    public const regNumber REG_FLT_CALLEE_SAVED_FIRST_AMD64 = REG_X64_XMM6;

    public const regNumber REG_FLT_CALLEE_SAVED_LAST_AMD64 = REG_X64_XMM15;

    public static regMask SRBM_LOWINT => SRBM_ALLINT_INIT;

    public const regMask SRBM_HIGHINT = SRBM_X64_R16 | SRBM_X64_R17 | SRBM_X64_R18 | SRBM_X64_R19 | SRBM_X64_R20 | SRBM_X64_R21 | SRBM_X64_R22 | SRBM_X64_R23 | SRBM_X64_R24 | SRBM_X64_R25 | SRBM_X64_R26 | SRBM_X64_R27 | SRBM_X64_R28 | SRBM_X64_R29 | SRBM_X64_R30 | SRBM_X64_R31;

    public static regMask SRBM_ALLINT_INIT => SRBM_INT_CALLEE_SAVED_AMD64 | SRBM_INT_CALLEE_TRASH_INIT;

    public static regMask SRBM_INT_CALLEE_TRASH_ALL => SRBM_INT_CALLEE_TRASH_INIT | SRBM_HIGHINT;

    public static regMask SRBM_ALLINT_ALL => SRBM_INT_CALLEE_SAVED_AMD64 | SRBM_INT_CALLEE_TRASH_ALL;

    // AMD64 write barrier ABI (see vm\amd64\JitHelpers_FastWriteBarriers.{asm,S},
    // vm\amd64\patchedcode.{asm,S}, vm\amd64\JitHelpers_Slow.asm,
    // runtime\amd64\WriteBarriers.{asm,S}):
    //
    // CORINFO_HELP_ASSIGN_REF (JIT_WriteBarrier), CORINFO_HELP_CHECKED_ASSIGN_REF (JIT_CheckedWriteBarrier):
    //     The usual amd64 calling convention is observed: dst in REG_ARG_0_AMD64, src in REG_ARG_1_AMD64.
    //     On exit:
    //       Dst register (RCX on Windows, RDI on SysV): clobbered (the helper shifts it in
    //           place to index the card table). Cannot be assumed to retain its value.
    //       Src register (RDX on Windows, RSI on SysV): clobbered in the Region variants of
    //           the patched slot, preserved in the others. Since the patched slot may change
    //           at runtime, callers must assume it is clobbered.
    //       All integer callee-trash registers: must be considered clobbered. RAX, R8 and R9
    //           are unconditionally used by some variant. R10/R11 are touched by the _DEBUG
    //           variant (JIT_WriteBarrier_Debug) and by the RhpAssignRef path that runs when
    //           DOTNET_UseGCWriteBarrierCopy=0.
    //       Flags: clobbered.
    //       XMM/YMM/ZMM/mask registers: PRESERVED. The write barrier helpers never execute
    //           any SSE/AVX/AVX-512/EVEX-mask instruction, so no FP/SIMD/mask register is
    //           touched. This is identical on both Windows and SysV ABIs.
    //
    // Because of the FP/SIMD/mask preservation, SRBM_CALLEE_TRASH_WRITEBARRIER_AMD64 is reduced to
    // SRBM_INT_CALLEE_TRASH_INIT (the standard int callee-trash set, excluding APX high regs
    // R16-R31 which are also never touched by the helpers).

    public static regNumber REG_WRITE_BARRIER_DST_AMD64 => REG_ARG_0_AMD64;

    public static regMask SRBM_WRITE_BARRIER_DST_AMD64 => SRBM_ARG_0_AMD64;

    public static regNumber REG_WRITE_BARRIER_SRC_AMD64 => REG_ARG_1_AMD64;

    public static regMask SRBM_WRITE_BARRIER_SRC_AMD64 => SRBM_ARG_1_AMD64;

    // We have two register classifications
    // * callee trash: aka     volatile or caller saved
    // * callee saved: aka non-volatile
    //
    // Callee trash are used for passing arguments, returning results, and are freely
    // mutable by the method. Because of this, the caller is responsible for saving
    // them if they are in use prior to making a call. This saving doesn't need to
    // happen for leaf methods (that is methods which don't themselves make any calls)
    // and can be done by spilling to the stack or to a callee saved register. This
    // means they are cheaper to use but can have higher overall cost if there are
    // many calls to be made with values in callee trash registers needing to live
    // across the call boundary.
    //
    // Callee saved don't have any special uses but have to be spilled prior to usage
    // and restored prior to returning back to the caller, so they have an inherently
    // higher baseline cost. This cost can be offset by re-using the register across
    // call boundaries to reduce the overall amount of spilling required.
    //
    // Given this, we order the registers here to prefer callee trash first and then
    // callee save. This allows us to use the registers we've already been assumed
    // to overwrite first and then to use those with a higher consumption cost. It
    // is up to the register allocator to preference using any callee saved registers
    // for values that are frequently live across call boundaries.
    //
    // Within those two groups registers are generally preferenced in numerical order
    // based on the encoding. This helps avoid using larger encodings unneccesarily since
    // higher numbered registers typically take more bytes to encode.
    //
    // For integer registers, the numerical order is eax, ecx, edx, ebx, esp, ebp,
    // esi, edi. You then also have r8-r15 which take an additional byte to encode. We
    // deviate from the numerical order slightly because esp, ebp, r12, and r13 have
    // special encoding requirements. In particular, esp is used by the stack and isn't
    // generally usable, instead it can only be used to access locals occupying stack
    // space. Both esp and r12 take an additional byte to encode the addressing form of
    // the instruction. ebp and r13 likewise can take additional bytes to encode certain
    // addressing modes, in particular those with displacements. Because of this ebp is
    // always ordered last of the base 8 registers. r13 and then r12 are likewise last
    // of the upper 8 registers. This helps reduce the total number of emitted bytes
    // quite significantly across typical usages.
    //
    // There are some other minor deviations based on special uses for particular registers
    // on a given platform which give additional size savings for the typical case.
    //
    // For simd registers, the numerical order is xmm0-xmm7. You then have xmm8-xmm15
    // which take an additional byte to encode and can also have xmm16-xmm31 for EVEX
    // when the hardware supports it. There are no additional hidden costs for these.

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_CALLEE_TRASH => Configuration.UNIX_AMD64_ABI
        ? s_regVarOrderSysVIntTrash
        : s_regVarOrderWindowsIntTrash;

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_CALLEE_SAVED => Configuration.UNIX_AMD64_ABI
        ? (Configuration.ETW_EBP_FRAMED ? s_regVarOrderSysVIntSavedEtw : s_regVarOrderSysVIntSaved)
        : (Configuration.ETW_EBP_FRAMED ? s_regVarOrderWindowsIntSavedEtw : s_regVarOrderWindowsIntSaved);

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_FLT_CALLEE_TRASH => Configuration.UNIX_AMD64_ABI
        ? s_regVarOrderSysVFloatTrash
        : s_regVarOrderWindowsFloatTrash;

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_FLT_CALLEE_SAVED => Configuration.UNIX_AMD64_ABI
        ? []
        : s_regVarOrderWindowsFloatSaved;

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_FLT_EVEX_CALLEE_TRASH => Configuration.UNIX_AMD64_ABI
        ? s_regVarOrderSysVFloatEvexTrash
        : s_regVarOrderWindowsFloatEvexTrash;

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_FLT_EVEX_CALLEE_SAVED =>
        REG_VAR_ORDER_FLT_CALLEE_SAVED;

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_AMD64 => Configuration.UNIX_AMD64_ABI
        ? (Configuration.ETW_EBP_FRAMED ? s_regVarOrderSysVEtw : s_regVarOrderSysV)
        : (Configuration.ETW_EBP_FRAMED ? s_regVarOrderWindowsEtw : s_regVarOrderWindows);

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_FLT_AMD64 => s_regVarOrderFloat;

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_FLT_EVEX => Configuration.UNIX_AMD64_ABI
        ? s_regVarOrderFloatEvexSysV
        : s_regVarOrderFloatEvexWindows;

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_MSK_AMD64 =>
        [REG_X64_K1, REG_X64_K2, REG_X64_K3, REG_X64_K4, REG_X64_K5, REG_X64_K6, REG_X64_K7];

    private static ReadOnlySpan<regNumber> s_regVarOrderSysVIntTrash => [
        REG_X64_EAX, REG_X64_ECX, REG_X64_EDX, REG_X64_EDI, REG_X64_ESI, REG_X64_R8, REG_X64_R9,
        REG_X64_R10, REG_X64_R11, REG_X64_R16, REG_X64_R17, REG_X64_R18, REG_X64_R19, REG_X64_R20,
        REG_X64_R21, REG_X64_R22, REG_X64_R23, REG_X64_R24, REG_X64_R25, REG_X64_R26, REG_X64_R27,
        REG_X64_R28, REG_X64_R29, REG_X64_R30, REG_X64_R31,
    ];

    private static ReadOnlySpan<regNumber> s_regVarOrderSysVIntSavedEtw => [
        REG_X64_EBX, REG_X64_R15, REG_X64_R14, REG_X64_R13, REG_X64_R12,
    ];

    private static ReadOnlySpan<regNumber> s_regVarOrderSysVIntSaved => [
        REG_X64_EBX, REG_X64_EBP, REG_X64_R15, REG_X64_R14, REG_X64_R13, REG_X64_R12,
    ];

    private static ReadOnlySpan<regNumber> s_regVarOrderWindowsIntTrash => [
        REG_X64_EAX, REG_X64_ECX, REG_X64_EDX, REG_X64_R8, REG_X64_R10, REG_X64_R9, REG_X64_R11,
        REG_X64_R16, REG_X64_R17, REG_X64_R18, REG_X64_R19, REG_X64_R20, REG_X64_R21, REG_X64_R22,
        REG_X64_R23, REG_X64_R24, REG_X64_R25, REG_X64_R26, REG_X64_R27, REG_X64_R28, REG_X64_R29,
        REG_X64_R30, REG_X64_R31,
    ];

    private static ReadOnlySpan<regNumber> s_regVarOrderWindowsIntSavedEtw => [
        REG_X64_EBX, REG_X64_ESI, REG_X64_EDI, REG_X64_R14, REG_X64_R15, REG_X64_R13, REG_X64_R12,
    ];

    private static ReadOnlySpan<regNumber> s_regVarOrderWindowsIntSaved => [
        REG_X64_EBX, REG_X64_ESI, REG_X64_EDI, REG_X64_EBP, REG_X64_R14, REG_X64_R15, REG_X64_R13, REG_X64_R12,
    ];

    private static ReadOnlySpan<regNumber> s_regVarOrderSysVFloatTrash => [
        REG_X64_XMM0, REG_X64_XMM1, REG_X64_XMM2, REG_X64_XMM3, REG_X64_XMM4, REG_X64_XMM5,
        REG_X64_XMM6, REG_X64_XMM7, REG_X64_XMM8, REG_X64_XMM9, REG_X64_XMM10, REG_X64_XMM11,
        REG_X64_XMM12, REG_X64_XMM13, REG_X64_XMM14, REG_X64_XMM15,
    ];

    private static ReadOnlySpan<regNumber> s_regVarOrderWindowsFloatTrash => [
        REG_X64_XMM0, REG_X64_XMM1, REG_X64_XMM2, REG_X64_XMM3, REG_X64_XMM4, REG_X64_XMM5,
    ];

    private static ReadOnlySpan<regNumber> s_regVarOrderWindowsFloatSaved => [
        REG_X64_XMM6, REG_X64_XMM7, REG_X64_XMM8, REG_X64_XMM9, REG_X64_XMM10,
        REG_X64_XMM11, REG_X64_XMM12, REG_X64_XMM13, REG_X64_XMM14, REG_X64_XMM15,
    ];

    private static ReadOnlySpan<regNumber> s_regVarOrderSysVFloatEvexTrash => [
        REG_X64_XMM0, REG_X64_XMM1, REG_X64_XMM2, REG_X64_XMM3, REG_X64_XMM4, REG_X64_XMM5,
        REG_X64_XMM6, REG_X64_XMM7, REG_X64_XMM8, REG_X64_XMM9, REG_X64_XMM10, REG_X64_XMM11,
        REG_X64_XMM12, REG_X64_XMM13, REG_X64_XMM14, REG_X64_XMM15, REG_X64_XMM16, REG_X64_XMM17,
        REG_X64_XMM18, REG_X64_XMM19, REG_X64_XMM20, REG_X64_XMM21, REG_X64_XMM22, REG_X64_XMM23,
        REG_X64_XMM24, REG_X64_XMM25, REG_X64_XMM26, REG_X64_XMM27, REG_X64_XMM28, REG_X64_XMM29,
        REG_X64_XMM30, REG_X64_XMM31,
    ];

    private static ReadOnlySpan<regNumber> s_regVarOrderWindowsFloatEvexTrash => [
        REG_X64_XMM0, REG_X64_XMM1, REG_X64_XMM2, REG_X64_XMM3, REG_X64_XMM4, REG_X64_XMM5,
        REG_X64_XMM16, REG_X64_XMM17, REG_X64_XMM18, REG_X64_XMM19, REG_X64_XMM20, REG_X64_XMM21,
        REG_X64_XMM22, REG_X64_XMM23, REG_X64_XMM24, REG_X64_XMM25, REG_X64_XMM26, REG_X64_XMM27,
        REG_X64_XMM28, REG_X64_XMM29, REG_X64_XMM30, REG_X64_XMM31,
    ];

    private static ReadOnlySpan<regNumber> s_regVarOrderSysV => [
        REG_X64_EAX, REG_X64_ECX, REG_X64_EDX, REG_X64_EDI, REG_X64_ESI, REG_X64_R8, REG_X64_R9,
        REG_X64_R10, REG_X64_R11, REG_X64_R16, REG_X64_R17, REG_X64_R18, REG_X64_R19, REG_X64_R20,
        REG_X64_R21, REG_X64_R22, REG_X64_R23, REG_X64_R24, REG_X64_R25, REG_X64_R26, REG_X64_R27,
        REG_X64_R28, REG_X64_R29, REG_X64_R30, REG_X64_R31, REG_X64_EBX, REG_X64_EBP,
        REG_X64_R15, REG_X64_R14, REG_X64_R13, REG_X64_R12,
    ];

    private static ReadOnlySpan<regNumber> s_regVarOrderSysVEtw => [
        REG_X64_EAX, REG_X64_ECX, REG_X64_EDX, REG_X64_EDI, REG_X64_ESI, REG_X64_R8, REG_X64_R9,
        REG_X64_R10, REG_X64_R11, REG_X64_R16, REG_X64_R17, REG_X64_R18, REG_X64_R19, REG_X64_R20,
        REG_X64_R21, REG_X64_R22, REG_X64_R23, REG_X64_R24, REG_X64_R25, REG_X64_R26, REG_X64_R27,
        REG_X64_R28, REG_X64_R29, REG_X64_R30, REG_X64_R31, REG_X64_EBX, REG_X64_R15,
        REG_X64_R14, REG_X64_R13, REG_X64_R12,
    ];

    private static ReadOnlySpan<regNumber> s_regVarOrderWindows => [
        REG_X64_EAX, REG_X64_ECX, REG_X64_EDX, REG_X64_R8, REG_X64_R10, REG_X64_R9, REG_X64_R11,
        REG_X64_R16, REG_X64_R17, REG_X64_R18, REG_X64_R19, REG_X64_R20, REG_X64_R21, REG_X64_R22,
        REG_X64_R23, REG_X64_R24, REG_X64_R25, REG_X64_R26, REG_X64_R27, REG_X64_R28, REG_X64_R29,
        REG_X64_R30, REG_X64_R31, REG_X64_EBX, REG_X64_ESI, REG_X64_EDI, REG_X64_EBP,
        REG_X64_R14, REG_X64_R15, REG_X64_R13, REG_X64_R12,
    ];

    private static ReadOnlySpan<regNumber> s_regVarOrderWindowsEtw => [
        REG_X64_EAX, REG_X64_ECX, REG_X64_EDX, REG_X64_R8, REG_X64_R10, REG_X64_R9, REG_X64_R11,
        REG_X64_R16, REG_X64_R17, REG_X64_R18, REG_X64_R19, REG_X64_R20, REG_X64_R21, REG_X64_R22,
        REG_X64_R23, REG_X64_R24, REG_X64_R25, REG_X64_R26, REG_X64_R27, REG_X64_R28, REG_X64_R29,
        REG_X64_R30, REG_X64_R31, REG_X64_EBX, REG_X64_ESI, REG_X64_EDI, REG_X64_R14,
        REG_X64_R15, REG_X64_R13, REG_X64_R12,
    ];

    private static ReadOnlySpan<regNumber> s_regVarOrderFloat => [
        REG_X64_XMM0, REG_X64_XMM1, REG_X64_XMM2, REG_X64_XMM3, REG_X64_XMM4, REG_X64_XMM5,
        REG_X64_XMM6, REG_X64_XMM7, REG_X64_XMM8, REG_X64_XMM9, REG_X64_XMM10, REG_X64_XMM11,
        REG_X64_XMM12, REG_X64_XMM13, REG_X64_XMM14, REG_X64_XMM15,
    ];

    private static ReadOnlySpan<regNumber> s_regVarOrderFloatEvexSysV =>
        s_regVarOrderSysVFloatEvexTrash;

    private static ReadOnlySpan<regNumber> s_regVarOrderFloatEvexWindows => [
        REG_X64_XMM0, REG_X64_XMM1, REG_X64_XMM2, REG_X64_XMM3, REG_X64_XMM4, REG_X64_XMM5,
        REG_X64_XMM16, REG_X64_XMM17, REG_X64_XMM18, REG_X64_XMM19, REG_X64_XMM20, REG_X64_XMM21,
        REG_X64_XMM22, REG_X64_XMM23, REG_X64_XMM24, REG_X64_XMM25, REG_X64_XMM26, REG_X64_XMM27,
        REG_X64_XMM28, REG_X64_XMM29, REG_X64_XMM30, REG_X64_XMM31, REG_X64_XMM6, REG_X64_XMM7,
        REG_X64_XMM8, REG_X64_XMM9, REG_X64_XMM10, REG_X64_XMM11, REG_X64_XMM12, REG_X64_XMM13,
        REG_X64_XMM14, REG_X64_XMM15,
    ];

    public static int CNT_CALLEE_SAVED_AMD64 =>
        (Configuration.UNIX_AMD64_ABI ? 5 : 7) + REG_ETW_FRAMED_EBP_COUNT;

    public static int CNT_CALLEE_ENREG_AMD64 => CNT_CALLEE_SAVED_AMD64;

    public static int CNT_CALLEE_TRASH_INT_INIT => Configuration.UNIX_AMD64_ABI ? 9 : 7;

    public const int CNT_CALLEE_TRASH_HIGHINT = 16;

    public static int CNT_CALLEE_SAVED_FLOAT_AMD64 => Configuration.UNIX_AMD64_ABI ? 0 : 10;

    public static int CNT_CALLEE_ENREG_FLOAT_AMD64 => CNT_CALLEE_SAVED_FLOAT_AMD64;

    public static int CNT_CALLEE_TRASH_FLOAT_INIT => Configuration.UNIX_AMD64_ABI ? 16 : 6;

    public const int CNT_CALLEE_TRASH_HIGHFLOAT = 16;

    public static regMask SRBM_ENC_CALLEE_SAVED_AMD64 => Configuration.UNIX_AMD64_ABI
        ? SRBM_NONE
        : SRBM_X64_RSI | SRBM_X64_RDI;

    public const int CNT_CALLEE_SAVED_MASK_AMD64 = 0;

    public const int CNT_CALLEE_ENREG_MASK_AMD64 = CNT_CALLEE_SAVED_MASK_AMD64;

    public const int CNT_CALLEE_TRASH_MASK_INIT_AMD64 = 0;

    public const int CNT_CALLEE_TRASH_MASK_EVEX_AMD64 = 7;

    public static int CALLEE_SAVED_REG_MAXSZ_AMD64 => CNT_CALLEE_SAVED_AMD64 * REGSIZE_BYTES_AMD64;

    public static int CALLEE_SAVED_FLOAT_MAXSZ_AMD64 => CNT_CALLEE_SAVED_FLOAT_AMD64 * 16;

    /// <summary>register to hold shift amount</summary>
    public const regNumber REG_SHIFT_AMD64 = REG_X64_ECX;

    public const regMask SRBM_SHIFT_AMD64 = SRBM_X64_ECX;

    /// <summary>This is a general scratch register that does not conflict with the argument registers</summary>
    public const regNumber REG_SCRATCH_AMD64 = REG_X64_EAX;

    // Where is the exception object on entry to the handler block?
    public static regNumber REG_EXCEPTION_OBJECT_AMD64 =>
        Configuration.UNIX_AMD64_ABI ? REG_X64_EDI : REG_X64_ECX;

    public static regMask SRBM_EXCEPTION_OBJECT_AMD64 =>
        Configuration.UNIX_AMD64_ABI ? SRBM_X64_EDI : SRBM_X64_ECX;

    public const regNumber REG_JUMP_THUNK_PARAM_AMD64 = REG_X64_EAX;

    public const regMask SRBM_JUMP_THUNK_PARAM_AMD64 = SRBM_X64_EAX;

    // Register to be used for emitting helper calls whose call target is an indir of an
    // absolute memory address in case of Rel32 overflow i.e. a data address could not be
    // encoded as PC-relative 32-bit offset.
    //
    // Notes:
    // 1) that RAX is callee trash register that is not used for passing parameter and
    //    also results in smaller instruction encoding.
    // 2) Profiler Leave callback requires the return value to be preserved
    //    in some form.  We can use custom calling convention for Leave callback.
    //    For e.g return value could be preserved in rcx so that it is available for
    //    profiler.
    public const regNumber REG_DEFAULT_HELPER_CALL_TARGET_AMD64 = REG_X64_RAX;

    public const regMask SRBM_DEFAULT_HELPER_CALL_TARGET_AMD64 = SRBM_X64_RAX;

    /// <summary>Indirection cell for R2R fast tailcall</summary>
    /// <remarks>See ImportThunk.Kind.DelayLoadHelperWithExistingIndirectionCell in crossgen2.</remarks>
    public const regNumber REG_R2R_INDIRECT_PARAM_AMD64 = REG_X64_RAX;

    public const regMask SRBM_R2R_INDIRECT_PARAM_AMD64 = SRBM_X64_RAX;

    /// <summary>IL stub's secret MethodDesc parameter (JitFlags.JIT_FLAG_PUBLISH_SECRET_PARAM)</summary>
    public const regNumber REG_SECRET_STUB_PARAM_AMD64 = REG_X64_R10;

    public const regMask SRBM_SECRET_STUB_PARAM_AMD64 = SRBM_X64_R10;

    // The following defines are useful for iterating a regNumber

    public const regNumber REG_FIRST_AMD64 = REG_X64_EAX;

    public const regNumber REG_INT_FIRST_AMD64 = REG_X64_EAX;

    public const regNumber REG_INT_LAST_AMD64 = REG_X64_R31;
    /// <summary>Which register are int and long values returned in ?</summary>
    public const regNumber REG_INTRET_AMD64 = REG_X64_EAX;

    public const regMask SRBM_INTRET_AMD64 = SRBM_X64_EAX;

    public const regMask SRBM_LNGRET_AMD64 = SRBM_X64_EAX;

    public const regNumber REG_INTRET_1_AMD64 = REG_X64_RDX;

    public const regMask SRBM_INTRET_1_AMD64 = SRBM_X64_RDX;

    public const regNumber REG_LNGRET_1 = REG_X64_RDX;

    public const regMask SRBM_LNGRET_1 = SRBM_X64_RDX;

    public const regMask SRBM_FLOATRET_AMD64 = SRBM_X64_XMM0;

    public const regNumber REG_DOUBLERET = REG_X64_XMM0;

    public const regMask SRBM_DOUBLERET_AMD64 = SRBM_X64_XMM0;

    public const regNumber REG_FLOATRET_1_AMD64 = REG_X64_XMM1;

    public const regMask SRBM_FLOATRET_1_AMD64 = SRBM_X64_XMM1;

    public const regNumber REG_DOUBLERET_1 = REG_X64_XMM1;

    public const regMask SRBM_DOUBLERET_1_AMD64 = SRBM_X64_XMM1;

    public const regNumber REG_FPBASE_AMD64 = REG_X64_EBP;

    public const regMask SRBM_FPBASE_AMD64 = SRBM_X64_EBP;

    public const string STR_FPBASE_AMD64 = "rbp";

    public const regNumber REG_SPBASE_AMD64 = REG_X64_ESP;

    public const regMask SRBM_SPBASE_AMD64 = SRBM_X64_ESP;

    public const string STR_SPBASE_AMD64 = "rsp";

    /// <summary>return address</summary>
    public const int FIRST_ARG_STACK_OFFS_AMD64 = REGSIZE_BYTES_AMD64;

    public static int MAX_REG_ARG_AMD64 => Configuration.UNIX_AMD64_ABI ? 6 : 4;

    public static int MAX_FLOAT_REG_ARG_AMD64 => Configuration.UNIX_AMD64_ABI ? 8 : 4;

    public static regNumber REG_ARG_FIRST_AMD64 =>
        Configuration.UNIX_AMD64_ABI ? REG_X64_EDI : REG_X64_ECX;

    public const regNumber REG_ARG_LAST_AMD64 = REG_X64_R9;

    /// <summary>Number of outgoing reserved stack slots.</summary>
    public static int INIT_ARG_STACK_SLOT_AMD64 => Configuration.UNIX_AMD64_ABI ? 0 : 4;

    public static regNumber REG_ARG_0_AMD64 =>
        Configuration.UNIX_AMD64_ABI ? REG_X64_EDI : REG_X64_ECX;

    public static regNumber REG_ARG_1_AMD64 =>
        Configuration.UNIX_AMD64_ABI ? REG_X64_ESI : REG_X64_EDX;

    public static regNumber REG_ARG_2_AMD64 =>
        Configuration.UNIX_AMD64_ABI ? REG_X64_EDX : REG_X64_R8;

    public static regNumber REG_ARG_3_AMD64 =>
        Configuration.UNIX_AMD64_ABI ? REG_X64_ECX : REG_X64_R9;

    public static regNumber REG_ARG_4_AMD64
    {
        get
        {
            if (Configuration.UNIX_AMD64_ABI)
            {
                return REG_X64_R8;
            }
            else
            {
                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.AMD64ABIHasNoFifthIntegerArgumentRegister);
                return default!;
            }
        }
    }

    public static regNumber REG_ARG_5_AMD64
    {
        get
        {
            if (Configuration.UNIX_AMD64_ABI)
            {
                return REG_X64_R9;
            }
            else
            {
                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.AMD64ABIHasNoSixthIntegerArgumentRegister);
                return default!;
            }
        }
    }

    public static regMask SRBM_ARG_0_AMD64 =>
        Configuration.UNIX_AMD64_ABI ? SRBM_X64_RDI : SRBM_X64_ECX;

    public static regMask SRBM_ARG_1_AMD64 =>
        Configuration.UNIX_AMD64_ABI ? SRBM_X64_RSI : SRBM_X64_EDX;

    public static regMask SRBM_ARG_2_AMD64 =>
        Configuration.UNIX_AMD64_ABI ? SRBM_X64_EDX : SRBM_X64_R8;

    public static regMask SRBM_ARG_3_AMD64 =>
        Configuration.UNIX_AMD64_ABI ? SRBM_X64_ECX : SRBM_X64_R9;

    public static regMask SRBM_ARG_4_AMD64
    {
        get
        {
            if (Configuration.UNIX_AMD64_ABI)
            {
                return SRBM_X64_R8;
            }
            else
            {
                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.AMD64ABIHasNoFifthIntegerArgumentRegister);
                return default!;
            }
        }
    }

    public static regMask SRBM_ARG_5_AMD64
    {
        get
        {
            if (Configuration.UNIX_AMD64_ABI)
            {
                return SRBM_X64_R9;
            }
            else
            {
                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.AMD64ABIHasNoSixthIntegerArgumentRegister);
                return default!;
            }
        }
    }

    public const regNumber REG_FLTARG_0_AMD64 = REG_X64_XMM0;

    public const regNumber REG_FLTARG_1_AMD64 = REG_X64_XMM1;

    public const regNumber REG_FLTARG_2_AMD64 = REG_X64_XMM2;

    public const regNumber REG_FLTARG_3_AMD64 = REG_X64_XMM3;

    public const regMask SRBM_FLTARG_0_AMD64 = SRBM_X64_XMM0;

    public const regMask SRBM_FLTARG_1_AMD64 = SRBM_X64_XMM1;

    public const regMask SRBM_FLTARG_2_AMD64 = SRBM_X64_XMM2;

    public const regMask SRBM_FLTARG_3_AMD64 = SRBM_X64_XMM3;

    public static regNumber REG_FLTARG_4_AMD64
    {
        get
        {
            if (Configuration.UNIX_AMD64_ABI)
            {
                return REG_X64_XMM4;
            }
            else
            {
                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.AMD64ABIHasNoFifthFloatingPointArgumentRegister);
                return default!;
            }
        }
    }

    public static regNumber REG_FLTARG_5_AMD64
    {
        get
        {
            if (Configuration.UNIX_AMD64_ABI)
            {
                return REG_X64_XMM5;
            }
            else
            {
                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.AMD64ABIHasNoSixthFloatingPointArgumentRegister);
                return default!;
            }
        }
    }

    public static regNumber REG_FLTARG_6_AMD64
    {
        get
        {
            if (Configuration.UNIX_AMD64_ABI)
            {
                return REG_X64_XMM6;
            }
            else
            {
                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.AMD64ABIHasNoSeventhFloatingPointArgumentRegister);
                return default!;
            }
        }
    }

    public static regNumber REG_FLTARG_7_AMD64
    {
        get
        {
            if (Configuration.UNIX_AMD64_ABI)
            {
                return REG_X64_XMM7;
            }
            else
            {
                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.AMD64ABIHasNoEighthFloatingPointArgumentRegister);
                return default!;
            }
        }
    }

    public static regMask SRBM_FLTARG_4_AMD64
    {
        get
        {
            if (Configuration.UNIX_AMD64_ABI)
            {
                return SRBM_X64_XMM4;
            }
            else
            {
                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.AMD64ABIHasNoFifthFloatingPointArgumentRegister);
                return default!;
            }
        }
    }

    public static regMask SRBM_FLTARG_5_AMD64
    {
        get
        {
            if (Configuration.UNIX_AMD64_ABI)
            {
                return SRBM_X64_XMM5;
            }
            else
            {
                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.AMD64ABIHasNoSixthFloatingPointArgumentRegister);
                return default!;
            }
        }
    }

    public static regMask SRBM_FLTARG_6_AMD64
    {
        get
        {
            if (Configuration.UNIX_AMD64_ABI)
            {
                return SRBM_X64_XMM6;
            }
            else
            {
                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.AMD64ABIHasNoSeventhFloatingPointArgumentRegister);
                return default!;
            }
        }
    }

    public static regMask SRBM_FLTARG_7_AMD64
    {
        get
        {
            if (Configuration.UNIX_AMD64_ABI)
            {
                return SRBM_X64_XMM7;
            }
            else
            {
                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.AMD64ABIHasNoEighthFloatingPointArgumentRegister);
                return default!;
            }
        }
    }

    public static regMask SRBM_ARG_REGS_AMD64 => Configuration.UNIX_AMD64_ABI
        ? SRBM_ARG_0_AMD64 | SRBM_ARG_1_AMD64 | SRBM_ARG_2_AMD64 | SRBM_ARG_3_AMD64 | SRBM_ARG_4_AMD64 | SRBM_ARG_5_AMD64
        : SRBM_ARG_0_AMD64 | SRBM_ARG_1_AMD64 | SRBM_ARG_2_AMD64 | SRBM_ARG_3_AMD64;

    public static regMask SRBM_VALIDATE_INDIRECT_CALL_TRASH_ALL =>
        SRBM_INT_CALLEE_TRASH_ALL & ~(SRBM_X64_R10 | SRBM_X64_RCX);

    public const regNumber REG_VALIDATE_INDIRECT_CALL_ADDR_AMD64 = REG_X64_RCX;

    public const regMask SRBM_VALIDATE_INDIRECT_CALL_ADDR = SRBM_X64_RCX;

    public const regNumber REG_DISPATCH_INDIRECT_CALL_ADDR_AMD64 = REG_X64_RAX;
    public const regNumber REG_PINVOKE_FRAME_AMD64 = REG_X64_NA;
    public const regNumber REG_LNGARG_LO_AMD64 = REG_X64_NA;
    public const regNumber REG_LNGARG_HI_AMD64 = REG_X64_NA;

    public const regNumber REG_ASYNC_CONTINUATION_RET_AMD64 = REG_X64_RCX;

    // TODO: Port once instruction enum exists
    // // Pointer-sized string move instructions
    // public const instruction INS_movsp = INS_movsq;
    //
    // public const instruction INS_r_movsp = INS_r_movsq;
    //
    // public const instruction INS_stosp = INS_stosq;
    //
    // public const instruction INS_r_stosp = INS_r_stosq;

    // AMD64 uses FEATURE_FIXED_OUT_ARGS so this can be zero.
    public const int STACK_PROBE_BOUNDARY_THRESHOLD_BYTES_AMD64 = 0;

    public const regNumber REG_STACK_PROBE_HELPER_ARG_AMD64 = REG_X64_R11;

    public const regMask SRBM_STACK_PROBE_HELPER_ARG_AMD64 = SRBM_X64_R11;

    public static regMask SRBM_STACK_PROBE_HELPER_TRASH_AMD64 =>
        Configuration.UNIX_AMD64_ABI ? SRBM_NONE : SRBM_X64_RAX;

    public static regNumber REG_SWIFT_ERROR_AMD64 =>
        Configuration.UNIX_AMD64_ABI ? REG_X64_R12 : REG_X64_NA;

    public static regMask SRBM_SWIFT_ERROR_AMD64
    {
        get
        {
            if (Configuration.UNIX_AMD64_ABI)
            {
                return SRBM_X64_R12;
            }
            else
            {
                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.SwiftABIErrorRegisterIsUnavailableForThisABI);
                return default!;
            }
        }
    }

    public static regNumber REG_SWIFT_SELF_AMD64 =>
        Configuration.UNIX_AMD64_ABI ? REG_X64_R13 : REG_X64_NA;

    public static regMask SRBM_SWIFT_SELF_AMD64
    {
        get
        {
            if (Configuration.UNIX_AMD64_ABI)
            {
                return SRBM_X64_R13;
            }
            else
            {
                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.SwiftABISelfRegisterIsUnavailableForThisABI);
                return default!;
            }
        }
    }

    public static ReadOnlySpan<regNumber> REG_SWIFT_INTRET_ORDER_AMD64
    {
        get
        {
            if (Configuration.UNIX_AMD64_ABI)
            {
                return [REG_X64_RAX, REG_X64_RDX, REG_X64_RCX, REG_X64_R8];
            }
            else
            {
                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.SwiftABIReturnRegistersAreUnavailableForThisABI);
                return default!;
            }
        }
    }

    public static ReadOnlySpan<regNumber> REG_SWIFT_FLOATRET_ORDER_AMD64
    {
        get
        {
            if (Configuration.UNIX_AMD64_ABI)
            {
                return [REG_X64_XMM0, REG_X64_XMM1, REG_X64_XMM2, REG_X64_XMM3];
            }
            else
            {
                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.SwiftABIReturnRegistersAreUnavailableForThisABI);
                return default!;
            }
        }
    }

    public static regNumber REG_SWIFT_ARG_RET_BUFF
    {
        get
        {
            if (Configuration.UNIX_AMD64_ABI)
            {
                return REG_X64_RAX;
            }
            else
            {
                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.SwiftABIReturnBufferIsUnavailableForThisABI);
                return default!;
            }
        }
    }

    public static regMask SRBM_SWIFT_ARG_RET_BUFF
    {
        get
        {
            if (Configuration.UNIX_AMD64_ABI)
            {
                return SRBM_X64_RAX;
            }
            else
            {
                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.SwiftABIReturnBufferIsUnavailableForThisABI);
                return default!;
            }
        }
    }

    public static int SWIFT_RET_BUFF_ARGNUM
    {
        get
        {
            if (Configuration.UNIX_AMD64_ABI)
            {
                return MAX_REG_ARG_AMD64;
            }
            else
            {
                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.SwiftABIReturnBufferIsUnavailableForThisABI);
                return default!;
            }
        }
    }
}
