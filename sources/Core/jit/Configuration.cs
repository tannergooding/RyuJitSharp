using System;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

internal static class Configuration
{
#if DEBUG
    internal const bool DEBUG = true;
#else
    internal const bool DEBUG = false;
#endif

    // Host configuration substitutions
    internal static bool HOST_AMD64 => RuntimeInformation.ProcessArchitecture == Architecture.X64;

    internal static bool HOST_X86 => RuntimeInformation.ProcessArchitecture == Architecture.X86;

    internal static bool HOST_64BIT => IntPtr.Size == 8;

    internal static bool HOST_WINDOWS => OperatingSystem.IsWindows();

    // Target architecture and OS substitutions
    internal static bool TARGET_ARM => GetFeatureSwitch(nameof(TARGET_ARM));

    internal static bool TARGET_ARMARCH => GetFeatureSwitch(nameof(TARGET_ARMARCH));

    internal static bool TARGET_XARCH => GetFeatureSwitch(nameof(TARGET_XARCH));

    internal static bool TARGET_ARMV6 => GetFeatureSwitch(nameof(TARGET_ARMV6));

    internal static bool TARGET_ARM64 => GetFeatureSwitch(nameof(TARGET_ARM64));

    internal static bool TARGET_LOONGARCH64 => GetFeatureSwitch(nameof(TARGET_LOONGARCH64));

    internal static bool TARGET_RISCV64 => GetFeatureSwitch(nameof(TARGET_RISCV64));

    internal static bool TARGET_WASM => GetFeatureSwitch(nameof(TARGET_WASM));

    internal static bool TARGET_WASM32 => TARGET_WASM;

    internal static bool TARGET_AMD64 => TARGET_XARCH && TARGET_64BIT;

    internal static bool TARGET_X86 => TARGET_XARCH && !TARGET_64BIT;

    internal static bool TARGET_32BIT => !TARGET_64BIT;

    internal static bool TARGET_64BIT => GetFeatureSwitch(nameof(TARGET_64BIT));

    internal static bool TARGET_APPLE => GetFeatureSwitch(nameof(TARGET_APPLE));

    internal static bool TARGET_UNIX => GetFeatureSwitch(nameof(TARGET_UNIX));

    internal static bool TARGET_UNIX_POSSIBLY_SUPPORTED => TARGET_UNIX || TARGET_OS_RUNTIMEDETERMINED;

    internal static bool FEATURE_CFI_SUPPORT => TARGET_UNIX || TARGET_OS_RUNTIMEDETERMINED;

    internal static bool TARGET_OS_RUNTIMEDETERMINED => GetFeatureSwitch(nameof(TARGET_OS_RUNTIMEDETERMINED));

    internal static bool TARGET_WINDOWS_POSSIBLY_SUPPORTED =>
        TARGET_WINDOWS || (TARGET_OS_RUNTIMEDETERMINED && !TARGET_UNIX_OS_RUNTIMEDETERMINED);

    internal static bool TARGET_UNIX_ANYOS => TARGET_UNIX_OS_RUNTIMEDETERMINED;

    internal static bool TARGET_UNIX_OS_RUNTIMEDETERMINED => GetFeatureSwitch(nameof(TARGET_UNIX_OS_RUNTIMEDETERMINED));

    internal static bool TARGET_WINDOWS => GetFeatureSwitch(nameof(TARGET_WINDOWS));

    // Fixed target and ABI substitutions
    internal static bool UNIX_ARM_ABI => TARGET_ARM && !TARGET_ARMV6 && TARGET_UNIX;

    internal static bool OSX_ARM64_ABI => TARGET_ARM64 && TARGET_APPLE;

    internal static bool UNIX_AMD64_ABI => TARGET_AMD64 && TARGET_UNIX;

    internal static bool WINDOWS_AMD64_ABI => GetFeatureSwitch(nameof(WINDOWS_AMD64_ABI));

    internal static bool UNIX_X86_ABI => GetFeatureSwitch(nameof(UNIX_X86_ABI));

    internal static bool ALIGN_SIMD_TYPES => TARGET_XARCH || TARGET_ARM64;

    internal static bool CPU_HAS_BYTE_REGS => TARGET_X86;

    internal static bool CPU_LOAD_STORE_ARCH =>
        TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64 || TARGET_WASM;

    // Native double-aligned frames are an x86-only policy.
    internal static bool DOUBLE_ALIGN => TARGET_X86;

    internal static bool EMIT_TRACK_STACK_DEPTH => !TARGET_WASM;

    internal static bool ETW_EBP_FRAMED =>
        TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64 || (TARGET_AMD64 && TARGET_UNIX) || TARGET_X86;

    // Arm64 Windows uses a split ABI distinct from the usual Arm64 ABI.
    // ARM32, LoongArch64, and RISC-V64 also use split arguments.
    internal static bool FEATURE_ARG_SPLIT => TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64;

    internal static bool FEATURE_FASTTAILCALL =>
        TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64 || TARGET_WASM || TARGET_AMD64;

    internal static bool FEATURE_FIXED_OUT_ARGS =>
        TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64 || TARGET_AMD64;

    internal static bool FEATURE_HAS_ZERO_REG => GetFeatureSwitch(nameof(FEATURE_HAS_ZERO_REG));

    internal static bool FEATURE_HFA =>
        TARGET_ARM64 || (TARGET_ARM && (!ARM_SOFTFP || CONFIGURABLE_ARM_ABI));

    internal static bool FEATURE_HW_INTRINSICS => GetFeatureSwitch(nameof(FEATURE_HW_INTRINSICS));

    internal static bool FEATURE_IMPLICIT_BYREFS =>
        TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64 || TARGET_WASM || TARGET_AMD64;

    internal static bool FEATURE_LOOP_ALIGN => GetFeatureSwitch(nameof(FEATURE_LOOP_ALIGN));

    internal static bool FEATURE_MASKED_HW_INTRINSICS => GetFeatureSwitch(nameof(FEATURE_MASKED_HW_INTRINSICS));

    internal static bool FEATURE_MULTIREG_ARGS =>
        FEATURE_ARG_SPLIT || (TARGET_AMD64 && TARGET_UNIX);

    internal static bool FEATURE_MULTIREG_RET => GetFeatureSwitch(nameof(FEATURE_MULTIREG_RET));

    internal static bool FEATURE_MULTIREG_STRUCT_PROMOTE =>
        TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64 || TARGET_WASM || (TARGET_AMD64 && TARGET_UNIX);

    internal static bool FEATURE_MULTITHREADING => !TARGET_WASM;

    internal static bool FEATURE_PAL_ANSI => TARGET_UNIX;

    internal static bool FEATURE_PARTIAL_SIMD_CALLEE_SAVE =>
        TARGET_ARM64 || (TARGET_AMD64 && !TARGET_UNIX);

    internal static bool FEATURE_REMAP_FUNCTION =>
        TARGET_ARM64 || TARGET_AMD64 || (TARGET_X86 && TARGET_WINDOWS);

    internal static bool FEATURE_SIMD => GetFeatureSwitch(nameof(FEATURE_SIMD));

    internal static bool FEATURE_TAILCALL_OPT =>
        TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64 || TARGET_AMD64;

    // Shared returns allow implicit tail calls through a call/jump/return-block pattern.
    internal static bool FEATURE_TAILCALL_OPT_SHARED_RETURN => FEATURE_TAILCALL_OPT;

    internal static bool HAS_FIXED_REGISTER_SET => !TARGET_WASM;

    internal static bool HAS_MORE_THAN_64_REGISTERS => TARGET_AMD64 || TARGET_ARM64;

    internal static bool JIT32_GCENCODER => TARGET_X86;

    internal static bool LOWER_DECOMPOSE_LONGS => (TARGET_ARM && !TARGET_ARMV6) || TARGET_X86;

    internal static bool USE_HELPERS_FOR_INT_DIV => TARGET_ARM && !TARGET_ARMV6;

    internal static bool MULTIREG_HAS_SECOND_GC_RET =>
        TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64 || (TARGET_AMD64 && TARGET_UNIX);

    internal static bool NOGC_WRITE_BARRIERS =>
        TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64 || TARGET_X86;

    internal static bool PROFILING_SUPPORTED => GetFeatureSwitch(nameof(PROFILING_SUPPORTED));

    internal static bool SWIFT_SUPPORT => TARGET_ARM64 || (TARGET_AMD64 && TARGET_UNIX);

    internal static bool REGMASK_BITS_32 => TARGET_X86 || TARGET_WASM;

    internal static bool REGMASK_BITS_64 => !REGMASK_BITS_32;

    internal static bool USER_ARGS_COME_LAST => !TARGET_X86;

    internal static bool USE_PER_FRAME_PINVOKE_INIT => TARGET_64BIT;

    // Debug substitutions
    internal static bool DUMP_FLOWGRAPHS => GetOptionalFeatureSwitch(nameof(DUMP_FLOWGRAPHS));

    internal static bool DUMP_GC_TABLES => GetOptionalFeatureSwitch(nameof(DUMP_GC_TABLES));

    internal static bool LATE_DISASM => GetFeatureSwitch(nameof(LATE_DISASM));

    internal static bool TRACK_LSRA_STATS => GetOptionalFeatureSwitch(nameof(TRACK_LSRA_STATS));

    // Always-enabled substitutions

    // JIT method timing is enabled by default and can be changed by a runtime switch.
    internal static bool FEATURE_JIT_METHOD_PERF =>
        GetOptionalFeatureSwitch(nameof(FEATURE_JIT_METHOD_PERF), defaultValue: true);

    internal static bool FEATURE_ON_STACK_REPLACEMENT => GetFeatureSwitch(nameof(FEATURE_ON_STACK_REPLACEMENT));

    internal static bool FEATURE_READYTORUN => GetFeatureSwitch(nameof(FEATURE_READYTORUN));

    internal static bool FUNC_INFO_LOGGING => GetOptionalFeatureSwitch(nameof(FUNC_INFO_LOGGING));

    // Optional substitutions
    internal static bool FEATURE_VN_DUMP_FUNC_ARGS => GetFeatureSwitch(nameof(FEATURE_VN_DUMP_FUNC_ARGS));

    internal static bool VERIFY_GC_TABLES => GetOptionalFeatureSwitch(nameof(VERIFY_GC_TABLES));

    internal static bool CALL_ARG_STATS => GetOptionalFeatureSwitch(nameof(CALL_ARG_STATS));

    internal static bool COUNT_AST_OPERS => GetOptionalFeatureSwitch(nameof(COUNT_AST_OPERS));

    internal static bool COUNT_BASIC_BLOCKS => GetOptionalFeatureSwitch(nameof(COUNT_BASIC_BLOCKS));

    internal static bool DISPLAY_SIZES => GetOptionalFeatureSwitch(nameof(DISPLAY_SIZES));

    internal static bool EMITTER_STATS => GetOptionalFeatureSwitch(nameof(EMITTER_STATS));

    internal static bool MEASURE_BLOCK_SIZE => GetOptionalFeatureSwitch(nameof(MEASURE_BLOCK_SIZE));

    internal static bool MEASURE_FATAL => GetOptionalFeatureSwitch(nameof(MEASURE_FATAL));

    internal static bool MEASURE_MEM_ALLOC => GetOptionalFeatureSwitch(nameof(MEASURE_MEM_ALLOC));

    internal static bool MEASURE_NODE_SIZE => GetOptionalFeatureSwitch(nameof(MEASURE_NODE_SIZE));

    internal static bool MEASURE_NOWAY => GetOptionalFeatureSwitch(nameof(MEASURE_NOWAY));

    internal static bool MEASURE_PTRTAB_SIZE => GetOptionalFeatureSwitch(nameof(MEASURE_PTRTAB_SIZE));

    internal static bool NODEBASH_STATS => GetOptionalFeatureSwitch(nameof(NODEBASH_STATS));

    internal static bool DETAILED_FATAL_ERROR_MESSAGES =>
        DEBUG || GetOptionalFeatureSwitch(nameof(DETAILED_FATAL_ERROR_MESSAGES));

    // Fixed-value substitutions
    internal static bool ARM_SOFTFP => GetFeatureSwitch(nameof(ARM_SOFTFP));

    internal static bool DISASM_DEBUG => GetFeatureSwitch(nameof(DISASM_DEBUG));

    // JIT-only optional switches
    internal static bool DEBUG_EMIT => GetOptionalFeatureSwitch(nameof(DEBUG_EMIT));

    internal static bool MEASURE_CLRAPI_CALLS => GetOptionalFeatureSwitch(nameof(MEASURE_CLRAPI_CALLS));

    internal static bool VARSET_COUNTOPS => GetOptionalFeatureSwitch(nameof(VARSET_COUNTOPS));

    internal static bool ALLVARSET_COUNTOPS => GetOptionalFeatureSwitch(nameof(ALLVARSET_COUNTOPS));

    internal static bool EMIT_BACKWARDS_NAVIGATION => GetOptionalFeatureSwitch(nameof(EMIT_BACKWARDS_NAVIGATION));

    internal static bool OPTIONAL_STATISTICS =>
        NODEBASH_STATS ||
        COUNT_BASIC_BLOCKS ||
        COUNT_AST_OPERS ||
        CALL_ARG_STATS ||
        MEASURE_MEM_ALLOC ||
        MEASURE_NODE_SIZE ||
        MEASURE_BLOCK_SIZE ||
        MEASURE_PTRTAB_SIZE ||
        MEASURE_FATAL ||
        EMITTER_STATS;

    // Target-derived features

    // RyuJitSharp does not currently enable these optional ARM instruction subsets.
    internal static bool FEATURE_ITINSTRUCTION => false;

    internal static bool FEATURE_PLI_INSTRUCTION => false;

    // Native CMake enables this only for the runtime-OS-determined universal ARM JIT.
    internal static bool CONFIGURABLE_ARM_ABI => TARGET_ARM && TARGET_OS_RUNTIMEDETERMINED;

    // Target-dependent sizes and limits
    internal static int TARGET_POINTER_SIZE => TARGET_64BIT ? 8 : 4;

    internal static int REGSIZE_BYTES => TARGET_POINTER_SIZE;

    internal static int MAX_PASS_SINGLEREG_BYTES => TargetRuntimeMetadata.Select(
        MAX_PASS_SINGLEREG_BYTES_X86,
        MAX_PASS_SINGLEREG_BYTES_AMD64,
        MAX_PASS_SINGLEREG_BYTES_ARM64,
        MAX_PASS_SINGLEREG_BYTES_ARM,
        MAX_PASS_SINGLEREG_BYTES_LOONGARCH64,
        MAX_PASS_SINGLEREG_BYTES_RISCV64,
        MAX_PASS_SINGLEREG_BYTES_WASM);

    internal static int MAX_PASS_MULTIREG_BYTES =>
        TARGET_AMD64
            ? UNIX_AMD64_ABI ? MAX_PASS_MULTIREG_BYTES_X64_UNIX : MAX_PASS_MULTIREG_BYTES_X64_WINDOWS
            : TargetRuntimeMetadata.Select(
                MAX_PASS_MULTIREG_BYTES_X86,
                0,
                MAX_PASS_MULTIREG_BYTES_ARM64,
                MAX_PASS_MULTIREG_BYTES_ARM,
                MAX_PASS_MULTIREG_BYTES_LOONGARCH64,
                MAX_PASS_MULTIREG_BYTES_RISCV64,
                MAX_PASS_MULTIREG_BYTES_WASM);

    internal static int MAX_RET_MULTIREG_BYTES =>
        TARGET_AMD64
            ? UNIX_AMD64_ABI ? MAX_RET_MULTIREG_BYTES_X64_UNIX : MAX_RET_MULTIREG_BYTES_X64_WINDOWS
            : TargetRuntimeMetadata.Select(
                MAX_RET_MULTIREG_BYTES_X86,
                0,
                MAX_RET_MULTIREG_BYTES_ARM64,
                MAX_RET_MULTIREG_BYTES_ARM,
                MAX_RET_MULTIREG_BYTES_LOONGARCH64,
                MAX_RET_MULTIREG_BYTES_RISCV64,
                MAX_RET_MULTIREG_BYTES_WASM);

    internal static int MAX_ARG_REG_COUNT =>
        TARGET_AMD64
            ? UNIX_AMD64_ABI ? MAX_ARG_REG_COUNT_X64_UNIX : MAX_ARG_REG_COUNT_X64_WINDOWS
            : TargetRuntimeMetadata.Select(
                MAX_ARG_REG_COUNT_X86,
                0,
                MAX_ARG_REG_COUNT_ARM64,
                MAX_ARG_REG_COUNT_ARM,
                MAX_ARG_REG_COUNT_LOONGARCH64,
                MAX_ARG_REG_COUNT_RISCV64,
                MAX_ARG_REG_COUNT_WASM);

    internal static int MAX_RET_REG_COUNT =>
        TARGET_AMD64
            ? UNIX_AMD64_ABI ? MAX_RET_REG_COUNT_X64_UNIX : MAX_RET_REG_COUNT_X64_WINDOWS
            : TargetRuntimeMetadata.Select(
                MAX_RET_REG_COUNT_X86,
                0,
                MAX_RET_REG_COUNT_ARM64,
                MAX_RET_REG_COUNT_ARM,
                MAX_RET_REG_COUNT_LOONGARCH64,
                MAX_RET_REG_COUNT_RISCV64,
                MAX_RET_REG_COUNT_WASM);

    internal static int MAX_MULTIREG_COUNT =>
        TARGET_AMD64
            ? UNIX_AMD64_ABI ? MAX_MULTIREG_COUNT_X64_UNIX : MAX_MULTIREG_COUNT_X64_WINDOWS
            : TargetRuntimeMetadata.Select(
                MAX_MULTIREG_COUNT_X86,
                0,
                MAX_MULTIREG_COUNT_ARM64,
                MAX_MULTIREG_COUNT_ARM,
                MAX_MULTIREG_COUNT_LOONGARCH64,
                MAX_MULTIREG_COUNT_RISCV64,
                MAX_MULTIREG_COUNT_WASM);

    internal static int MAX_REG_ARG =>
        TARGET_AMD64
            ? UNIX_AMD64_ABI ? MAX_REG_ARG_X64_UNIX : MAX_REG_ARG_X64_WINDOWS
            : TargetRuntimeMetadata.Select(
                0,
                0,
                MAX_REG_ARG_ARM64,
                MAX_REG_ARG_ARM,
                MAX_REG_ARG_LOONGARCH64,
                MAX_REG_ARG_RISCV64,
                0);

    internal static int MIN_ARG_AREA_FOR_CALL =>
        TARGET_AMD64
            ? UNIX_AMD64_ABI ? MIN_ARG_AREA_FOR_CALL_X64_UNIX : MIN_ARG_AREA_FOR_CALL_X64_WINDOWS
            : TargetRuntimeMetadata.Select(
                MIN_ARG_AREA_FOR_CALL_X86,
                0,
                MIN_ARG_AREA_FOR_CALL_ARM64,
                MIN_ARG_AREA_FOR_CALL_ARM,
                MIN_ARG_AREA_FOR_CALL_LOONGARCH64,
                MIN_ARG_AREA_FOR_CALL_RISCV64,
                MIN_ARG_AREA_FOR_CALL_WASM);

    internal static int REGNUM_BITS => TargetRuntimeMetadata.Select(
        REGNUM_BITS_X86,
        REGNUM_BITS_AMD64,
        REGNUM_BITS_ARM64,
        REGNUM_BITS_ARM,
        REGNUM_BITS_LOONGARCH64,
        REGNUM_BITS_RISCV64,
        REGNUM_BITS_WASM);

    internal static int XMM_REGSIZE_BYTES => 16;

    internal static int YMM_REGSIZE_BYTES => 32;

    internal static int ZMM_REGSIZE_BYTES => 64;

    internal static int FP_REGSIZE_BYTES => TargetRuntimeMetadata.Select(
        XMM_REGSIZE_BYTES,
        XMM_REGSIZE_BYTES,
        FP_REGSIZE_BYTES_ARM64,
        8,
        FP_REGSIZE_BYTES_LOONGARCH64,
        FP_REGSIZE_BYTES_RISCV64,
        FP_REGSIZE_BYTES_WASM);

    internal static int STACK_ALIGN =>
        TARGET_ARM ? 8 : TARGET_X86 ? (UNIX_X86_ABI ? 16 : 4) : 16;

    internal static int EMIT_JUMPKIND_COUNT
    {
        get
        {
            if (TARGET_XARCH)
            {
                return (int)emitJumpKind.EJ_XARCH_COUNT;
            }
            else
            {
                if (TARGET_ARMARCH)
                {
                    return (int)emitJumpKind.EJ_ARMARCH_COUNT;
                }
                else
                {
                    if (TARGET_LOONGARCH64)
                    {
                        return (int)emitJumpKind.EJ_LOONGARCH64_COUNT;
                    }
                    else
                    {
                        if (TARGET_RISCV64)
                        {
                            return (int)emitJumpKind.EJ_RISCV64_COUNT;
                        }
                        else
                        {
                            if (TARGET_WASM)
                            {
                                return (int)emitJumpKind.EJ_WASM_COUNT;
                            }
                            else
                            {
                                FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.JumpKindsAreNotSupportedForThisTarget);
                                return default!;
                            }
                        }
                    }
                }
            }
        }
    }

    // Runtime support checks and feature-switch helpers

    internal static void EnsureWasmTarget()
    {
        if (!TARGET_WASM)
        {
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.WebAssemblyOnlyCompilerCodeRequiresTheWebAssemblyTarget);
            return;
        }
    }

    internal static void EnsureSupportedHostArchitecture()
    {
        if (RuntimeInformation.ProcessArchitecture is
            Architecture.X86 or Architecture.X64 or Architecture.Arm or Architecture.Arm64 or Architecture.LoongArch64 or
            Architecture.RiscV64)
        {
            return;
        }

        FatalJitException.ThrowFatalJitException(
        FatalJitErrorCode.UnsupportedHostArchitectureArgument,
        (Configuration.DETAILED_FATAL_ERROR_MESSAGES ? RuntimeInformation.ProcessArchitecture : default)
    );
        return;
    }

    private static bool GetOptionalFeatureSwitch(string featureName, bool defaultValue = false)
    {
        var switchName = $"RyuJitSharp.{featureName}";
        return AppContext.TryGetSwitch(switchName, out var isEnabled) ? isEnabled : defaultValue;
    }

    private static bool GetFeatureSwitch(string featureName)
    {
        var switchName = $"RyuJitSharp.{featureName}";
        if (AppContext.TryGetSwitch(switchName, out var isEnabled))
        {
            return isEnabled;
        }

        JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.RequiredCompilerConfigurationSwitchArgumentIsNotSet, (Configuration.DETAILED_FATAL_ERROR_MESSAGES ? switchName : default));
        return default;
    }
}
