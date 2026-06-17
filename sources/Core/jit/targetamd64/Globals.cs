// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_AMD64
using System;

namespace RyuJitSharp;

public partial class Globals
{
    public const CorInfoArch CORINFO_ARCH_TARGET = CORINFO_ARCH_X64;

    /// <summary>For CpObj code generation, this is the threshold of the number of contiguous non-gc slots that trigger generating rep movsq instead of sequences of movsq instructions</summary>
    public const int CPOBJ_NONGC_SLOTS_LIMIT = 4;

    /// <summary>Maximum size of a struct passed in a single register (double).</summary>
    public const int MAX_PASS_SINGLEREG_BYTES = 8;

#if UNIX_AMD64_ABI
    /// <summary>Maximum size of a struct that could be passed in more than one register (Max is two simd16s)</summary>
    public const int MAX_PASS_MULTIREG_BYTES = 32;

    /// <summary>Maximum size of a struct that could be returned in more than one register  (Max is two simd16s)</summary>
    public const int MAX_RET_MULTIREG_BYTES = 32;

    /// <summary>Maximum registers used to pass a single argument in multiple registers.</summary>
    public const int MAX_ARG_REG_COUNT = 2;

    /// <summary>Maximum registers used to return a value.</summary>
    public const int MAX_RET_REG_COUNT = 4;

    /// <summary>Maximum number of registers defined by a single instruction (including calls).</summary>
    /// <remarks>This is also the maximum number of registers for a MultiReg node.</remarks>
    public const int MAX_MULTIREG_COUNT = 4;
#else
    /// <summary>No multireg arguments</summary>
    public const int MAX_PASS_MULTIREG_BYTES = 0;

    /// <summary>No multireg return values</summary>
    public const int MAX_RET_MULTIREG_BYTES = 0;

    /// <summary>Maximum registers used to pass a single argument (no arguments are passed using multiple registers)</summary>
    public const int MAX_ARG_REG_COUNT = 1;

    /// <summary>Maximum registers used to return a value.</summary>
    public const int MAX_RET_REG_COUNT = 1;

    /// <summary>Maximum number of registers defined by a single instruction (including calls).</summary>
    /// <remarks>
    ///   <para>This is also the maximum number of registers for a MultiReg node.</para>
    ///   <para>Note that this must be greater than 1 so that GenTreeLclVar can have an array of MAX_MULTIREG_COUNT - 1.</para>
    /// </remarks>
    public const int MAX_MULTIREG_COUNT = 2;
#endif

    /// <summary>equal to sizeof(void*) and the managed pointer size in bytes for this target</summary>
    public const int TARGET_POINTER_SIZE = 8;

    public const int CNT_HIGHFLOAT = 16;

    public const regMask SRBM_LOWFLOAT = SRBM_XMM0 | SRBM_XMM1 | SRBM_XMM2 | SRBM_XMM3 | SRBM_XMM4 | SRBM_XMM5 | SRBM_XMM6 | SRBM_XMM7 | SRBM_XMM8 | SRBM_XMM9 | SRBM_XMM10 | SRBM_XMM11 | SRBM_XMM12 | SRBM_XMM13 | SRBM_XMM14 | SRBM_XMM15;

    public const regMask SRBM_HIGHFLOAT = SRBM_XMM16 | SRBM_XMM17 | SRBM_XMM18 | SRBM_XMM19 | SRBM_XMM20 | SRBM_XMM21 | SRBM_XMM22 | SRBM_XMM23 | SRBM_XMM24 | SRBM_XMM25 | SRBM_XMM26 | SRBM_XMM27 | SRBM_XMM28 | SRBM_XMM29 | SRBM_XMM30 | SRBM_XMM31;

    public const regMask SRBM_ALLFLOAT_INIT = SRBM_LOWFLOAT;

    public const regNumber REG_FP_FIRST = REG_XMM0;

    public const regNumber REG_FP_LAST = REG_XMM31;

    public const regNumber FIRST_FP_ARGREG = REG_XMM0;

    public const regNumber REG_MASK_FIRST = REG_K0;

    public const regNumber REG_MASK_LAST = REG_K7;

    public const regMask SRBM_ALLMASK_INIT = SRBM_NONE;

    public const regMask SRBM_ALLMASK_EVEX = SRBM_K1 | SRBM_K2 | SRBM_K3 | SRBM_K4 | SRBM_K5 | SRBM_K6 | SRBM_K7;

    public const int CNT_MASK_REGS = 8;

#if UNIX_AMD64_ABI
    public const regNumber LAST_FP_ARGREG = REG_XMM7;
#else
    public const regNumber LAST_FP_ARGREG = REG_XMM3;
#endif

    /// <summary>number of bits in a REG_*</summary>
    public const int REGNUM_BITS = 7;

    /// <summary>number of bytes in one register</summary>
    public const int REGSIZE_BYTES = 8;

    /// <summary>XMM register size in bytes</summary>
    public const int XMM_REGSIZE_BYTES = 16;

    /// <summary>YMM register size in bytes</summary>
    public const int YMM_REGSIZE_BYTES = 32;

    /// <summary>ZMM register size in bytes</summary>
    public const int ZMM_REGSIZE_BYTES = 64;

    /// <summary>code alignment requirement</summary>
    public const int CODE_ALIGN = 1;       

    /// <summary>stack alignment requirement</summary>
    public const int STACK_ALIGN = 16;

    /// <summary>Shift-right amount to convert size in bytes to size in STACK_ALIGN units == log2(STACK_ALIGN)</summary>
    public const int STACK_ALIGN_SHIFT = 4;

#if ETW_EBP_FRAMED
    public const regMask SRBM_ETW_FRAMED_EBP = SRBM_NONE;

    public const regMask SRBM_ETW_FRAMED_EBP_LIST = SRBM_NONE;

    public static ReadOnlySpan<regNumber> REG_ETW_FRAMED_EBP_LIST => [];

    public const int REG_ETW_FRAMED_EBP_COUNT = 0;
#else // !ETW_EBP_FRAMED
    public const regMask SRBM_ETW_FRAMED_EBP = SRBM_EBP;

    public const regMask SRBM_ETW_FRAMED_EBP_LIST = SRBM_EBP;

    public static ReadOnlySpan<regNumber> REG_ETW_FRAMED_EBP_LIST => [REG_EBP];

    public const int REG_ETW_FRAMED_EBP_COUNT = 1;
#endif

#if UNIX_AMD64_ABI
    /// <summary>Minimum required outgoing argument space for a call.</summary>
    public const int MIN_ARG_AREA_FOR_CALL = 0;

    public const regMask SRBM_INT_CALLEE_SAVED = SRBM_EBX | SRBM_ETW_FRAMED_EBP | SRBM_R12 | SRBM_R13 | SRBM_R14 | SRBM_R15;

    public const regMask SRBM_INT_CALLEE_TRASH_INIT = SRBM_EAX | SRBM_RDI | SRBM_RSI | SRBM_EDX | SRBM_ECX | SRBM_R8 | SRBM_R9 | SRBM_R10 | SRBM_R11;

    public const regMask SRBM_FLT_CALLEE_SAVED = SRBM_NONE;

    public const regMask SRBM_FLT_CALLEE_TRASH_INIT = SRBM_XMM0 | SRBM_XMM1 | SRBM_XMM2 | SRBM_XMM3 | SRBM_XMM4 | SRBM_XMM5 | SRBM_XMM6 | SRBM_XMM7
                                                    | SRBM_XMM8 | SRBM_XMM9 | SRBM_XMM10 | SRBM_XMM11 | SRBM_XMM12 | SRBM_XMM13 | SRBM_XMM14 | SRBM_XMM15;

    public const regNumber REG_PROFILER_ENTER_ARG_0 = REG_R14;

    public const regMask SRBM_PROFILER_ENTER_ARG_0 = SRBM_R14;

    public const regNumber REG_PROFILER_ENTER_ARG_1 = REG_R15;

    public const regMask SRBM_PROFILER_ENTER_ARG_1 = SRBM_R15;

    public const regNumber REG_DEFAULT_PROFILER_CALL_TARGET = REG_R11;
#else
    /// <summary>Minimum required outgoing argument space for a call.</summary>
    public const int MIN_ARG_AREA_FOR_CALL = 4 * REGSIZE_BYTES;

    public const regMask SRBM_INT_CALLEE_SAVED = SRBM_EBX | SRBM_ESI | SRBM_EDI | SRBM_ETW_FRAMED_EBP | SRBM_R12 | SRBM_R13 | SRBM_R14 | SRBM_R15;

    public const regMask SRBM_INT_CALLEE_TRASH_INIT = SRBM_EAX | SRBM_ECX | SRBM_EDX | SRBM_R8 | SRBM_R9 | SRBM_R10 | SRBM_R11;

    public const regMask SRBM_FLT_CALLEE_SAVED = SRBM_XMM6 | SRBM_XMM7 | SRBM_XMM8 | SRBM_XMM9 | SRBM_XMM10 | SRBM_XMM11 | SRBM_XMM12 | SRBM_XMM13 | SRBM_XMM14 | SRBM_XMM15;

    public const regMask SRBM_FLT_CALLEE_TRASH_INIT = SRBM_XMM0 | SRBM_XMM1 | SRBM_XMM2 | SRBM_XMM3 | SRBM_XMM4 | SRBM_XMM5;
#endif

    public const regMask SRBM_MSK_CALLEE_TRASH_INIT = SRBM_NONE;

    public const regMask SRBM_MSK_CALLEE_TRASH_EVEX = SRBM_ALLMASK_EVEX;

    public const regMask SRBM_MSK_CALLEE_SAVED = SRBM_NONE;

    public const regMask SRBM_OSR_INT_CALLEE_SAVED = SRBM_INT_CALLEE_SAVED | SRBM_EBP;

    public const regNumber REG_FLT_CALLEE_SAVED_FIRST   = REG_XMM6;

    public const regNumber REG_FLT_CALLEE_SAVED_LAST = REG_XMM15;

    public const regMask SRBM_LOWINT = SRBM_ALLINT_INIT;

    public const regMask SRBM_HIGHINT = SRBM_R16 | SRBM_R17 | SRBM_R18 | SRBM_R19 | SRBM_R20 | SRBM_R21 | SRBM_R22 | SRBM_R23 | SRBM_R24 | SRBM_R25 | SRBM_R26 | SRBM_R27 | SRBM_R28 | SRBM_R29 | SRBM_R30 | SRBM_R31;

    public const regMask SRBM_ALLINT_INIT = SRBM_INT_CALLEE_SAVED | SRBM_INT_CALLEE_TRASH_INIT;

    public const regMask SRBM_INT_CALLEE_TRASH_ALL = SRBM_INT_CALLEE_TRASH_INIT | SRBM_HIGHINT;

    public const regMask SRBM_ALLINT_ALL = SRBM_INT_CALLEE_SAVED | SRBM_INT_CALLEE_TRASH_ALL;

    // AMD64 write barrier ABI (see vm\amd64\JitHelpers_FastWriteBarriers.{asm,S},
    // vm\amd64\patchedcode.{asm,S}, vm\amd64\JitHelpers_Slow.asm,
    // runtime\amd64\WriteBarriers.{asm,S}):
    //
    // CORINFO_HELP_ASSIGN_REF (JIT_WriteBarrier), CORINFO_HELP_CHECKED_ASSIGN_REF (JIT_CheckedWriteBarrier):
    //     The usual amd64 calling convention is observed: dst in REG_ARG_0, src in REG_ARG_1.
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
    // Because of the FP/SIMD/mask preservation, SRBM_CALLEE_TRASH_WRITEBARRIER is reduced to
    // SRBM_INT_CALLEE_TRASH_INIT (the standard int callee-trash set, excluding APX high regs
    // R16-R31 which are also never touched by the helpers).

    public const regNumber REG_WRITE_BARRIER_DST = REG_ARG_0;

    public const regMask SRBM_WRITE_BARRIER_DST = SRBM_ARG_0;

    public const regNumber REG_WRITE_BARRIER_SRC = REG_ARG_1;

    public const regMask SRBM_WRITE_BARRIER_SRC = SRBM_ARG_1;

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

#if UNIX_AMD64_ABI
    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_CALLEE_TRASH => [REG_EAX, REG_ECX, REG_EDX, REG_EDI, REG_ESI, REG_R8, REG_R9, REG_R10, REG_R11, REG_R16, REG_R17, REG_R18, REG_R19, REG_R20, REG_R21, REG_R22, REG_R23, REG_R24, REG_R25, REG_R26, REG_R27, REG_R28, REG_R29, REG_R30, REG_R31];

#if ETW_EBP_FRAMED
    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_CALLEE_SAVED => [REG_EBX, REG_R15, REG_R14, REG_R13, REG_R12];
#else
    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_CALLEE_SAVED => [REG_EBX, REG_EBP, REG_R15, REG_R14, REG_R13, REG_R12];
#endif

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_FLT_CALLEE_TRASH => [REG_XMM0, REG_XMM1, REG_XMM2, REG_XMM3, REG_XMM4, REG_XMM5, REG_XMM6, REG_XMM7, REG_XMM8, REG_XMM9, REG_XMM10, REG_XMM11, REG_XMM12, REG_XMM13, REG_XMM14, REG_XMM15];

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_FLT_CALLEE_SAVED => [];

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_FLT_EVEX_CALLEE_TRASH => [REG_XMM0, REG_XMM1, REG_XMM2, REG_XMM3, REG_XMM4, REG_XMM5, REG_XMM6, REG_XMM7, REG_XMM8, REG_XMM9, REG_XMM10, REG_XMM11, REG_XMM12, REG_XMM13, REG_XMM14, REG_XMM15, REG_XMM16, REG_XMM17, REG_XMM18, REG_XMM19, REG_XMM20, REG_XMM21, REG_XMM22, REG_XMM23, REG_XMM24, REG_XMM25, REG_XMM26, REG_XMM27, REG_XMM28, REG_XMM29, REG_XMM30, REG_XMM31];

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_FLT_EVEX_CALLEE_SAVED => REG_VAR_ORDER_FLT_CALLEE_SAVED;

#if ETW_EBP_FRAMED
    public static ReadOnlySpan<regNumber> REG_VAR_ORDER => [REG_EAX, REG_ECX, REG_EDX, REG_EDI, REG_ESI, REG_R8, REG_R9, REG_R10, REG_R11, REG_R16, REG_R17, REG_R18, REG_R19, REG_R20, REG_R21, REG_R22, REG_R23, REG_R24, REG_R25, REG_R26, REG_R27, REG_R28, REG_R29, REG_R30, REG_R31, REG_EBX, REG_R15, REG_R14, REG_R13, REG_R12];
#else
    public static ReadOnlySpan<regNumber> REG_VAR_ORDER => [REG_EAX, REG_ECX, REG_EDX, REG_EDI, REG_ESI, REG_R8, REG_R9, REG_R10, REG_R11, REG_R16, REG_R17, REG_R18, REG_R19, REG_R20, REG_R21, REG_R22, REG_R23, REG_R24, REG_R25, REG_R26, REG_R27, REG_R28, REG_R29, REG_R30, REG_R31, REG_EBX, REG_EBP, REG_R15, REG_R14, REG_R13, REG_R12];
#endif

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_FLT => REG_VAR_ORDER_FLT_CALLEE_TRASH;

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_FLT_EVEX => REG_VAR_ORDER_FLT_EVEX_CALLEE_TRASH;

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_MSK => [REG_K1, REG_K2, REG_K3, REG_K4, REG_K5, REG_K6, REG_K7];
#else
    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_CALLEE_TRASH => [REG_EAX, REG_ECX, REG_EDX, REG_R8, REG_R10, REG_R9, REG_R11, REG_R16, REG_R17, REG_R18, REG_R19, REG_R20, REG_R21, REG_R22, REG_R23, REG_R24, REG_R25, REG_R26, REG_R27, REG_R28, REG_R29, REG_R30, REG_R31];

#if ETW_EBP_FRAMED
    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_CALLEE_SAVED => [REG_EBX, REG_ESI, REG_EDI, REG_R14, REG_R15, REG_R13, REG_R12];
#else
    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_CALLEE_SAVED => [REG_EBX, REG_ESI, REG_EDI, REG_EBP, REG_R14, REG_R15, REG_R13, REG_R12];
#endif

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_FLT_CALLEE_TRASH => [REG_XMM0, REG_XMM1, REG_XMM2, REG_XMM3, REG_XMM4, REG_XMM5];

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_FLT_CALLEE_SAVED => [REG_XMM6, REG_XMM7, REG_XMM8, REG_XMM9, REG_XMM10, REG_XMM11, REG_XMM12, REG_XMM13, REG_XMM14, REG_XMM15];

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_FLT_EVEX_CALLEE_TRASH => [REG_XMM0, REG_XMM1, REG_XMM2, REG_XMM3, REG_XMM4, REG_XMM5, REG_XMM16, REG_XMM17, REG_XMM18, REG_XMM19, REG_XMM20, REG_XMM21, REG_XMM22, REG_XMM23, REG_XMM24, REG_XMM25, REG_XMM26, REG_XMM27, REG_XMM28, REG_XMM29, REG_XMM30, REG_XMM31];

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_FLT_EVEX_CALLEE_SAVED => REG_VAR_ORDER_FLT_CALLEE_SAVED;

#if ETW_EBP_FRAMED
    public static ReadOnlySpan<regNumber> REG_VAR_ORDER => [REG_EAX, REG_ECX, REG_EDX, REG_R8, REG_R10, REG_R9, REG_R11, REG_R16, REG_R17, REG_R18, REG_R19, REG_R20, REG_R21, REG_R22, REG_R23, REG_R24, REG_R25, REG_R26, REG_R27, REG_R28, REG_R29, REG_R30, REG_R31, REG_EBX, REG_ESI, REG_EDI, REG_R14, REG_R15, REG_R13, REG_R12];
#else
    public static ReadOnlySpan<regNumber> REG_VAR_ORDER => [REG_EAX, REG_ECX, REG_EDX, REG_R8, REG_R10, REG_R9, REG_R11, REG_R16, REG_R17, REG_R18, REG_R19, REG_R20, REG_R21, REG_R22, REG_R23, REG_R24, REG_R25, REG_R26, REG_R27, REG_R28, REG_R29, REG_R30, REG_R31, REG_EBX, REG_ESI, REG_EDI, REG_EBP, REG_R14, REG_R15, REG_R13, REG_R12];
#endif

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_FLT => [REG_XMM0, REG_XMM1, REG_XMM2, REG_XMM3, REG_XMM4, REG_XMM5, REG_XMM6, REG_XMM7, REG_XMM8, REG_XMM9, REG_XMM10, REG_XMM11, REG_XMM12, REG_XMM13, REG_XMM14, REG_XMM15];

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_FLT_EVEX => [REG_XMM0, REG_XMM1, REG_XMM2, REG_XMM3, REG_XMM4, REG_XMM5, REG_XMM16, REG_XMM17, REG_XMM18, REG_XMM19, REG_XMM20, REG_XMM21, REG_XMM22, REG_XMM23, REG_XMM24, REG_XMM25, REG_XMM26, REG_XMM27, REG_XMM28, REG_XMM29, REG_XMM30, REG_XMM31, REG_XMM6, REG_XMM7, REG_XMM8, REG_XMM9, REG_XMM10, REG_XMM11, REG_XMM12, REG_XMM13, REG_XMM14, REG_XMM15];

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_MSK => [REG_K1, REG_K2, REG_K3, REG_K4, REG_K5, REG_K6, REG_K7];
#endif

#if UNIX_AMD64_ABI
    public const int CNT_CALLEE_SAVED = 5 + REG_ETW_FRAMED_EBP_COUNT;

    public const int CNT_CALLEE_ENREG = CNT_CALLEE_SAVED;

    public const int CNT_CALLEE_TRASH_INT_INIT = 9;

    public const int CNT_CALLEE_TRASH_HIGHINT = 16;

    public const int CNT_CALLEE_SAVED_FLOAT = 0;

    public const int CNT_CALLEE_ENREG_FLOAT = CNT_CALLEE_SAVED_FLOAT;

    public const int CNT_CALLEE_TRASH_FLOAT_INIT = 16;

    public const int CNT_CALLEE_TRASH_HIGHFLOAT = 16;

    /// <summary>For SysV we have more volatile registers so we do not save any callee saves for EnC.</summary>
    public const int SRBM_ENC_CALLEE_SAVED = 0;
#else
    public const int CNT_CALLEE_SAVED = 7 + REG_ETW_FRAMED_EBP_COUNT;

    public const int CNT_CALLEE_ENREG = CNT_CALLEE_SAVED;

    public const int CNT_CALLEE_TRASH_INT_INIT = 7;

    public const int CNT_CALLEE_TRASH_HIGHINT = 16;

    public const int CNT_CALLEE_SAVED_FLOAT = 10;

    public const int CNT_CALLEE_ENREG_FLOAT = CNT_CALLEE_SAVED_FLOAT;

    public const int CNT_CALLEE_TRASH_FLOAT_INIT = 6;

    public const int CNT_CALLEE_TRASH_HIGHFLOAT = 16;

    /// <summary>Callee-preserved registers we always save and allow use of for EnC code, since there are quite few volatile registers.</summary>
    public const regMask SRBM_ENC_CALLEE_SAVED = SRBM_RSI | SRBM_RDI;
#endif

    public const int CNT_CALLEE_SAVED_MASK = 0;

    public const int CNT_CALLEE_ENREG_MASK = CNT_CALLEE_SAVED_MASK;

    public const int CNT_CALLEE_TRASH_MASK_INIT = 0;

    public const int CNT_CALLEE_TRASH_MASK_EVEX = 7;

    public const int CALLEE_SAVED_REG_MAXSZ = CNT_CALLEE_SAVED * REGSIZE_BYTES;

    public const int CALLEE_SAVED_FLOAT_MAXSZ = CNT_CALLEE_SAVED_FLOAT * 16;

    /// <summary>register to hold shift amount</summary>
    public const regNumber REG_SHIFT = REG_ECX;

    public const regMask SRBM_SHIFT = SRBM_ECX;

    /// <summary>This is a general scratch register that does not conflict with the argument registers</summary>
    public const regNumber REG_SCRATCH = REG_EAX;

    // Where is the exception object on entry to the handler block?
#if UNIX_AMD64_ABI
    public const regNumber REG_EXCEPTION_OBJECT = REG_EDI;

    public const regMask SRBM_EXCEPTION_OBJECT = SRBM_EDI;
#else
    public const regNumber REG_EXCEPTION_OBJECT = REG_ECX;

    public const regMask SRBM_EXCEPTION_OBJECT = SRBM_ECX;
#endif

    public const regNumber REG_JUMP_THUNK_PARAM = REG_EAX;

    public const regMask SRBM_JUMP_THUNK_PARAM = SRBM_EAX;

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
    public const regNumber REG_DEFAULT_HELPER_CALL_TARGET = REG_RAX;

    public const regMask SRBM_DEFAULT_HELPER_CALL_TARGET = SRBM_RAX;

    /// <summary>Indirection cell for R2R fast tailcall</summary>
    /// <remarks>See ImportThunk.Kind.DelayLoadHelperWithExistingIndirectionCell in crossgen2.</remarks>
    public const regNumber REG_R2R_INDIRECT_PARAM = REG_RAX;

    public const regMask SRBM_R2R_INDIRECT_PARAM = SRBM_RAX;

    /// <summary>GenericPInvokeCalliHelper VASigCookie Parameter</summary>
    public const regNumber REG_PINVOKE_COOKIE_PARAM = REG_R11;

    public const regMask SRBM_PINVOKE_COOKIE_PARAM = SRBM_R11;

    /// <summary>GenericPInvokeCalliHelper unmanaged target Parameter</summary>
    public const regNumber REG_PINVOKE_TARGET_PARAM = REG_R10;

    public const regMask SRBM_PINVOKE_TARGET_PARAM = SRBM_R10;

    /// <summary>IL stub's secret MethodDesc parameter (JitFlags.JIT_FLAG_PUBLISH_SECRET_PARAM)</summary>
    public const regNumber REG_SECRET_STUB_PARAM = REG_R10;

    public const regMask SRBM_SECRET_STUB_PARAM = SRBM_R10;

    // The following defines are useful for iterating a regNumber

    public const regNumber REG_FIRST = REG_EAX;

    public const regNumber REG_INT_FIRST = REG_EAX;

    public const regNumber REG_INT_LAST = REG_R31;

    public static regNumber REG_NEXT(regNumber reg) => reg + 1;

    public static regNumber REG_PREV(regNumber reg) => reg - 1;

    /// <summary>Which register are int and long values returned in ?</summary>
    public const regNumber REG_INTRET = REG_EAX;

    public const regMask SRBM_INTRET = SRBM_EAX;

    public const regMask SRBM_LNGRET = SRBM_EAX;

#if UNIX_AMD64_ABI
    public const regNumber REG_INTRET_1 = REG_RDX;

    public const regMask SRBM_INTRET_1 = SRBM_RDX;

    public const regNumber REG_LNGRET_1 = REG_RDX;

    public const regMask SRBM_LNGRET_1 = SRBM_RDX;
#endif

    public const regNumber REG_FLOATRET = REG_XMM0;

    public const regMask SRBM_FLOATRET = SRBM_XMM0;

    public const regNumber REG_DOUBLERET = REG_XMM0;

    public const regMask SRBM_DOUBLERET = SRBM_XMM0;

#if UNIX_AMD64_ABI
    public const regNumber REG_FLOATRET_1 = REG_XMM1;

    public const regMask SRBM_FLOATRET_1 = SRBM_XMM1;
    
    public const regNumber REG_DOUBLERET_1 = REG_XMM1;

    public const regMask SRBM_DOUBLERET_1 = SRBM_XMM1;
#endif

    public const regNumber REG_FPBASE = REG_EBP;

    public const regMask SRBM_FPBASE = SRBM_EBP;

    public const string STR_FPBASE = "rbp";

    public const regNumber REG_SPBASE = REG_ESP;

    public const regMask SRBM_SPBASE = SRBM_ESP;

    public const string STR_SPBASE = "rsp";

    /// <summary>return address</summary>
    public const int FIRST_ARG_STACK_OFFS = REGSIZE_BYTES;

#if UNIX_AMD64_ABI
    public const int MAX_REG_ARG = 6;

    public const int MAX_FLOAT_REG_ARG = 8;

    public const regNumber REG_ARG_FIRST = REG_EDI;

    public const regNumber REG_ARG_LAST = REG_R9;

    /// <summary>No outgoing reserved stack slots</summary>
    public const int INIT_ARG_STACK_SLOT = 0;

    public const regNumber REG_ARG_0 = REG_EDI;

    public const regNumber REG_ARG_1 = REG_ESI;

    public const regNumber REG_ARG_2 = REG_EDX;

    public const regNumber REG_ARG_3 = REG_ECX;

    public const regNumber REG_ARG_4 = REG_R8;

    public const regNumber REG_ARG_5 = REG_R9;

    // extern const regNumber intArgRegs[MAX_REG_ARG];
    // extern const regMaskTP intArgMasks[MAX_REG_ARG];
    // extern const regNumber fltArgRegs[MAX_FLOAT_REG_ARG];
    // extern const regMaskTP fltArgMasks[MAX_FLOAT_REG_ARG];

    public const regMask SRBM_ARG_0 = SRBM_RDI;

    public const regMask SRBM_ARG_1 = SRBM_RSI;

    public const regMask SRBM_ARG_2 = SRBM_EDX;

    public const regMask SRBM_ARG_3 = SRBM_ECX;

    public const regMask SRBM_ARG_4 = SRBM_R8;

    public const regMask SRBM_ARG_5 = SRBM_R9;
#else
    public const int MAX_REG_ARG = 4;

    public const int MAX_FLOAT_REG_ARG = 4;

    public const regNumber REG_ARG_FIRST = REG_ECX;

    public const regNumber REG_ARG_LAST = REG_R9;

    /// <summary>4 outgoing reserved stack slots</summary>
    public const int INIT_ARG_STACK_SLOT = 4;

    public const regNumber REG_ARG_0 = REG_ECX;

    public const regNumber REG_ARG_1 = REG_EDX;

    public const regNumber REG_ARG_2 = REG_R8;

    public const regNumber REG_ARG_3 = REG_R9;

    // extern const regNumber intArgRegs[MAX_REG_ARG];
    // extern const regMaskTP intArgMasks[MAX_REG_ARG];
    // extern const regNumber fltArgRegs[MAX_FLOAT_REG_ARG];
    // extern const regMaskTP fltArgMasks[MAX_FLOAT_REG_ARG];

    public const regMask SRBM_ARG_0 = SRBM_ECX;

    public const regMask SRBM_ARG_1 = SRBM_EDX;

    public const regMask SRBM_ARG_2 = SRBM_R8;

    public const regMask SRBM_ARG_3 = SRBM_R9;
#endif

    public const regNumber REG_FLTARG_0 = REG_XMM0;

    public const regNumber REG_FLTARG_1 = REG_XMM1;

    public const regNumber REG_FLTARG_2 = REG_XMM2;

    public const regNumber REG_FLTARG_3 = REG_XMM3;

    public const regMask SRBM_FLTARG_0 = SRBM_XMM0;

    public const regMask SRBM_FLTARG_1 = SRBM_XMM1;

    public const regMask SRBM_FLTARG_2 = SRBM_XMM2;

    public const regMask SRBM_FLTARG_3 = SRBM_XMM3;

#if UNIX_AMD64_ABI
    public const regNumber REG_FLTARG_4 = REG_XMM4;

    public const regNumber REG_FLTARG_5 = REG_XMM5;

    public const regNumber REG_FLTARG_6 = REG_XMM6;

    public const regNumber REG_FLTARG_7 = REG_XMM7;

    public const regMask SRBM_FLTARG_4 = SRBM_XMM4;

    public const regMask SRBM_FLTARG_5 = SRBM_XMM5;

    public const regMask SRBM_FLTARG_6 = SRBM_XMM6;

    public const regMask SRBM_FLTARG_7 = SRBM_XMM7;

    public const regMask SRBM_ARG_REGS = SRBM_ARG_0 | SRBM_ARG_1 | SRBM_ARG_2 | SRBM_ARG_3 | SRBM_ARG_4 | SRBM_ARG_5;

    public const regMask SRBM_FLTARG_REGS = SRBM_FLTARG_0 | SRBM_FLTARG_1 | SRBM_FLTARG_2 | SRBM_FLTARG_3 | SRBM_FLTARG_4 | SRBM_FLTARG_5 | SRBM_FLTARG_6 | SRBM_FLTARG_7;
#else
    public const regMask SRBM_ARG_REGS = SRBM_ARG_0 | SRBM_ARG_1 | SRBM_ARG_2 | SRBM_ARG_3;

    public const regMask SRBM_FLTARG_REGS = SRBM_FLTARG_0 | SRBM_FLTARG_1 | SRBM_FLTARG_2 | SRBM_FLTARG_3;
#endif

    public const regMask SRBM_VALIDATE_INDIRECT_CALL_TRASH_ALL = SRBM_INT_CALLEE_TRASH_ALL & ~(SRBM_R10 | SRBM_RCX);

    public const regNumber REG_VALIDATE_INDIRECT_CALL_ADDR = REG_RCX;

    public const regMask SRBM_VALIDATE_INDIRECT_CALL_ADDR = SRBM_RCX;

    public const regNumber REG_DISPATCH_INDIRECT_CALL_ADDR = REG_RAX;

    public const regNumber REG_ASYNC_CONTINUATION_RET = REG_RCX;

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
    public const int STACK_PROBE_BOUNDARY_THRESHOLD_BYTES = 0;

    public const regNumber REG_STACK_PROBE_HELPER_ARG = REG_R11;

    public const regMask SRBM_STACK_PROBE_HELPER_ARG = SRBM_R11;

#if UNIX_AMD64_ABI
    public const regMask SRBM_STACK_PROBE_HELPER_TRASH = SRBM_NONE;
#else
    public const regMask SRBM_STACK_PROBE_HELPER_TRASH = SRBM_RAX;
#endif

#if UNIX_AMD64_ABI
    public const regNumber REG_SWIFT_ERROR = REG_R12;

    public const regMask SRBM_SWIFT_ERROR = SRBM_R12;

    public const regNumber REG_SWIFT_SELF  = REG_R13;

    public const regMask SRBM_SWIFT_SELF = SRBM_R13;

    public static ReadOnlySpan<regNumber> REG_SWIFT_INTRET_ORDER => [REG_RAX, REG_RDX, REG_RCX, REG_R8];

    public static ReadOnlySpan<regNumber> REG_SWIFT_FLOATRET_ORDER => [REG_XMM0, REG_XMM1, REG_XMM2, REG_XMM3];

    public const regNumber REG_SWIFT_ARG_RET_BUFF = REG_RAX;

    public const regMask SRBM_SWIFT_ARG_RET_BUFF = SRBM_RAX;

    public const int SWIFT_RET_BUFF_ARGNUM = MAX_REG_ARG;
#endif

#if UNIX_AMD64_ABI
    public static ReadOnlySpan<regNumber> IntArgRegs => [
        REG_EDI,
        REG_ESI,
        REG_EDX,
        REG_ECX,
        REG_R8,
        REG_R9,
    ];

    public static ReadOnlySpan<regNumber> FltArgRegs => [
        REG_XMM0,
        REG_XMM1,
        REG_XMM2,
        REG_XMM3,
        REG_XMM4,
        REG_XMM5,
        REG_XMM6,
        REG_XMM7,
    ];
#else
    public static ReadOnlySpan<regNumber> IntArgRegs => [
        REG_ECX,
        REG_EDX,
        REG_R8,
        REG_R9,
    ];

    public static ReadOnlySpan<regNumber> FltArgRegs => [
        REG_XMM0,
        REG_XMM1,
        REG_XMM2,
        REG_XMM3,
    ];
#endif
}
#endif
