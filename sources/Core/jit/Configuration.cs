using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

internal static class Configuration
{
#if DEBUG
    internal const bool DEBUG = true;
#else
    internal const bool DEBUG = false;
#endif

    internal static bool MEASURE_NODE_SIZE => BuildFeatures.MeasureNodeSize;

    internal static bool MEASURE_BLOCK_SIZE => BuildFeatures.MeasureBlockSize;

    internal static bool MEASURE_PTRTAB_SIZE => BuildFeatures.MeasurePointerTableSize;

    internal static bool DISPLAY_SIZES => BuildFeatures.DisplaySizes;

    internal static bool CALL_ARG_STATS => BuildFeatures.CallArgumentStatistics;

    internal static bool NODEBASH_STATS => BuildFeatures.NodeBashStatistics;

    internal static bool COUNT_AST_OPERS => BuildFeatures.CountAstOperators;

    internal static bool EMITTER_STATS => BuildFeatures.EmitterStatistics;

    internal static bool OPTIONAL_STATISTICS => BuildFeatures.OptionalStatistics;

    private static class BuildFeatures
    {
        private static bool s_measureNodeSize;
        private static bool s_measureBlockSize;
        private static bool s_measurePointerTableSize;
        private static bool s_displaySizes;
        private static bool s_callArgumentStatistics;
        private static bool s_nodeBashStatistics;
        private static bool s_countAstOperators;
        private static bool s_emitterStatistics;
        private static bool s_optionalStatistics;

        static BuildFeatures()
        {
            EnableMeasureNodeSize();
            EnableMeasureBlockSize();
            EnableMeasurePointerTableSize();
            EnableDisplaySizes();
            EnableCallArgumentStatistics();
            EnableNodeBashStatistics();
            EnableCountAstOperators();
            EnableEmitterStatistics();
            EnableOptionalStatistics();
        }

        internal static bool MeasureNodeSize => s_measureNodeSize;

        internal static bool MeasureBlockSize => s_measureBlockSize;

        internal static bool MeasurePointerTableSize => s_measurePointerTableSize;

        internal static bool DisplaySizes => s_displaySizes;

        internal static bool CallArgumentStatistics => s_callArgumentStatistics;

        internal static bool NodeBashStatistics => s_nodeBashStatistics;

        internal static bool CountAstOperators => s_countAstOperators;

        internal static bool EmitterStatistics => s_emitterStatistics;

        internal static bool OptionalStatistics => s_optionalStatistics;

        [Conditional("MEASURE_NODE_SIZE")]
        private static void EnableMeasureNodeSize() => s_measureNodeSize = true;

        [Conditional("MEASURE_BLOCK_SIZE")]
        private static void EnableMeasureBlockSize() => s_measureBlockSize = true;

        [Conditional("MEASURE_PTRTAB_SIZE")]
        private static void EnableMeasurePointerTableSize() => s_measurePointerTableSize = true;

        [Conditional("DISPLAY_SIZES")]
        private static void EnableDisplaySizes() => s_displaySizes = true;

        [Conditional("CALL_ARG_STATS")]
        private static void EnableCallArgumentStatistics() => s_callArgumentStatistics = true;

        [Conditional("NODEBASH_STATS")]
        private static void EnableNodeBashStatistics() => s_nodeBashStatistics = true;

        [Conditional("COUNT_AST_OPERS")]
        private static void EnableCountAstOperators() => s_countAstOperators = true;

        [Conditional("EMITTER_STATS")]
        private static void EnableEmitterStatistics() => s_emitterStatistics = true;

        [Conditional("NODEBASH_STATS")]
        [Conditional("COUNT_AST_OPERS")]
        [Conditional("CALL_ARG_STATS")]
        [Conditional("MEASURE_NODE_SIZE")]
        [Conditional("MEASURE_BLOCK_SIZE")]
        [Conditional("MEASURE_PTRTAB_SIZE")]
        [Conditional("EMITTER_STATS")]
        private static void EnableOptionalStatistics() => s_optionalStatistics = true;
    }

    private static readonly Architecture s_hostArchitecture = RuntimeInformation.ProcessArchitecture;

    private static readonly bool s_hostWindows = OperatingSystem.IsWindows();

    internal static bool FEATURE_HW_INTRINSICS => GetFeatureSwitch(nameof(FEATURE_HW_INTRINSICS));

    // RyuJitSharp currently includes timing support in every build.
    internal static bool FEATURE_JIT_METHOD_PERF => true;

    internal static bool FEATURE_MASKED_HW_INTRINSICS => GetFeatureSwitch(nameof(FEATURE_MASKED_HW_INTRINSICS));

    // RyuJitSharp does not currently enable these optional ARM instruction subsets.
    internal static bool FEATURE_ITINSTRUCTION => false;

    internal static bool FEATURE_PLI_INSTRUCTION => false;

    internal static bool FEATURE_READYTORUN => GetFeatureSwitch(nameof(FEATURE_READYTORUN));

    internal static bool FEATURE_HAS_ZERO_REG => GetFeatureSwitch(nameof(FEATURE_HAS_ZERO_REG));

    internal static bool FEATURE_VN_DUMP_FUNC_ARGS => GetFeatureSwitch(nameof(FEATURE_VN_DUMP_FUNC_ARGS));

    internal static bool DISASM_DEBUG => GetFeatureSwitch(nameof(DISASM_DEBUG));

    internal static bool FEATURE_HFA =>
        TARGET_ARM64 || (TARGET_ARM && (!ARM_SOFTFP || CONFIGURABLE_ARM_ABI));

    // Native CMake enables this only for the runtime-OS-determined universal ARM JIT.
    internal static bool CONFIGURABLE_ARM_ABI => TARGET_ARM && TARGET_OS_RUNTIMEDETERMINED;

    internal static bool CPU_HAS_BYTE_REGS => TARGET_X86;

    internal static bool CPU_LOAD_STORE_ARCH =>
        TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64 || TARGET_WASM;

    internal static bool EMIT_TRACK_STACK_DEPTH => !TARGET_WASM;

    internal static bool ETW_EBP_FRAMED =>
        TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64 || (TARGET_AMD64 && TARGET_UNIX) || TARGET_X86;

    internal static bool FEATURE_REMAP_FUNCTION =>
        TARGET_ARM64 || TARGET_AMD64 || (TARGET_X86 && TARGET_WINDOWS);

    // Arm64 Windows uses a split ABI distinct from the usual Arm64 ABI.
    // ARM32, LoongArch64, and RISC-V64 also use split arguments.
    internal static bool FEATURE_ARG_SPLIT => TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64;

    internal static bool FEATURE_CFI_SUPPORT => TARGET_UNIX || TARGET_OS_RUNTIMEDETERMINED;

    internal static bool FEATURE_MULTIREG_ARGS =>
        FEATURE_ARG_SPLIT || (TARGET_AMD64 && TARGET_UNIX);

    internal static bool FEATURE_FIXED_OUT_ARGS =>
        TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64 || TARGET_AMD64;

    internal static bool FEATURE_IMPLICIT_BYREFS =>
        TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64 || TARGET_WASM || TARGET_AMD64;

    internal static bool FEATURE_MULTITHREADING => !TARGET_WASM;

    internal static bool FEATURE_PAL_ANSI => TARGET_UNIX;

    internal static bool FEATURE_PARTIAL_SIMD_CALLEE_SAVE =>
        TARGET_ARM64 || (TARGET_AMD64 && !TARGET_UNIX);

    internal static bool ALIGN_SIMD_TYPES => TARGET_XARCH || TARGET_ARM64;

    internal static bool JIT32_GCENCODER => TARGET_X86;

    internal static bool LOWER_DECOMPOSE_LONGS => (TARGET_ARM && !TARGET_ARMV6) || TARGET_X86;

    internal static bool USE_HELPERS_FOR_INT_DIV => TARGET_ARM && !TARGET_ARMV6;

    internal static bool FEATURE_MULTIREG_STRUCT_PROMOTE =>
        TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64 || TARGET_WASM || (TARGET_AMD64 && TARGET_UNIX);

    internal static bool MULTIREG_HAS_SECOND_GC_RET =>
        TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64 || (TARGET_AMD64 && TARGET_UNIX);

    internal static bool FEATURE_FASTTAILCALL =>
        TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64 || TARGET_WASM || TARGET_AMD64;

    internal static bool FEATURE_MULTIREG_RET => GetFeatureSwitch(nameof(FEATURE_MULTIREG_RET));

    internal static bool FEATURE_SIMD => GetFeatureSwitch(nameof(FEATURE_SIMD));

    internal static bool FEATURE_LOOP_ALIGN => GetFeatureSwitch(nameof(FEATURE_LOOP_ALIGN));

    internal static bool FEATURE_ON_STACK_REPLACEMENT => GetFeatureSwitch(nameof(FEATURE_ON_STACK_REPLACEMENT));

    internal static bool LATE_DISASM => GetFeatureSwitch(nameof(LATE_DISASM));

    internal static bool PROFILING_SUPPORTED => GetFeatureSwitch(nameof(PROFILING_SUPPORTED));

    internal static bool FEATURE_TAILCALL_OPT =>
        TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64 || TARGET_AMD64;

    // Shared returns allow implicit tail calls through a call/jump/return-block pattern.
    internal static bool FEATURE_TAILCALL_OPT_SHARED_RETURN => FEATURE_TAILCALL_OPT;

    internal static bool HAS_FIXED_REGISTER_SET => !TARGET_WASM;

    internal static bool HAS_MORE_THAN_64_REGISTERS => TARGET_AMD64 || TARGET_ARM64;

    internal static bool REGMASK_BITS_32 => TARGET_X86 || TARGET_WASM;

    internal static bool REGMASK_BITS_64 => !REGMASK_BITS_32;

    internal static bool USER_ARGS_COME_LAST => !TARGET_X86;

    internal static bool USE_PER_FRAME_PINVOKE_INIT => TARGET_64BIT;

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

    internal static bool NOGC_WRITE_BARRIERS =>
        TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64 || TARGET_X86;

    internal static bool TARGET_64BIT => GetFeatureSwitch(nameof(TARGET_64BIT));

    internal static bool HOST_64BIT => Environment.Is64BitProcess;

    internal static bool HOST_AMD64 => s_hostArchitecture == Architecture.X64;

    internal static bool HOST_X86 => s_hostArchitecture == Architecture.X86;

    internal static bool HOST_WINDOWS => s_hostWindows;

    internal static bool TARGET_ARM => GetFeatureSwitch(nameof(TARGET_ARM));

    internal static bool TARGET_ARMV6 => GetFeatureSwitch(nameof(TARGET_ARMV6));

    internal static bool TARGET_ARM64 => GetFeatureSwitch(nameof(TARGET_ARM64));

    internal static bool TARGET_ARMARCH => GetFeatureSwitch(nameof(TARGET_ARMARCH));

    internal static bool ARM_SOFTFP => GetFeatureSwitch(nameof(ARM_SOFTFP));

    internal static bool TARGET_AMD64 => TARGET_XARCH && TARGET_64BIT;

    internal static bool TARGET_32BIT => !TARGET_64BIT;

    internal static bool TARGET_LOONGARCH64 => GetFeatureSwitch(nameof(TARGET_LOONGARCH64));

    internal static bool TARGET_RISCV64 => GetFeatureSwitch(nameof(TARGET_RISCV64));

    internal static bool TARGET_X86 => TARGET_XARCH && !TARGET_64BIT;

    // Native double-aligned frames are an x86-only policy.
    internal static bool DOUBLE_ALIGN => TARGET_X86;

    internal static bool TARGET_XARCH => GetFeatureSwitch(nameof(TARGET_XARCH));

    internal static bool TARGET_WASM => GetFeatureSwitch(nameof(TARGET_WASM));

    internal static bool TARGET_WASM32 => TARGET_WASM;

    internal static int EMIT_JUMPKIND_COUNT =>
        TARGET_XARCH ? (int)emitJumpKind.EJ_XARCH_COUNT :
        TARGET_ARMARCH ? (int)emitJumpKind.EJ_ARMARCH_COUNT :
        TARGET_LOONGARCH64 ? (int)emitJumpKind.EJ_LOONGARCH64_COUNT :
        TARGET_RISCV64 ? (int)emitJumpKind.EJ_RISCV64_COUNT :
        TARGET_WASM ? (int)emitJumpKind.EJ_WASM_COUNT :
        throw new FatalJitException(CORJIT_SKIPPED, "Jump kinds are not supported for this target.");

    internal static void EnsureWasmTarget()
    {
        if (!TARGET_WASM)
        {
            throw new FatalJitException("WebAssembly-only compiler code requires the WebAssembly target.");
        }
    }

    internal static void EnsureSupportedHostArchitecture()
    {
        if (s_hostArchitecture is
            Architecture.X86 or Architecture.X64 or Architecture.Arm or Architecture.Arm64 or Architecture.LoongArch64 or
            Architecture.RiscV64)
        {
            return;
        }

        throw new FatalJitException($"Unsupported host architecture: {s_hostArchitecture}.");
    }

    internal static bool TARGET_WINDOWS => GetFeatureSwitch(nameof(TARGET_WINDOWS));

    internal static bool WINDOWS_AMD64_ABI => GetFeatureSwitch(nameof(WINDOWS_AMD64_ABI));

    internal static bool TARGET_UNIX => GetFeatureSwitch(nameof(TARGET_UNIX));

    internal static bool UNIX_AMD64_ABI => TARGET_AMD64 && TARGET_UNIX;

    internal static bool UNIX_ARM_ABI => TARGET_ARM && !TARGET_ARMV6 && TARGET_UNIX;

    internal static bool TARGET_APPLE => GetFeatureSwitch(nameof(TARGET_APPLE));

    internal static bool OSX_ARM64_ABI => TARGET_ARM64 && TARGET_APPLE;

    internal static bool TARGET_UNIX_ANYOS => TARGET_UNIX_OS_RUNTIMEDETERMINED;

    internal static bool TARGET_UNIX_POSSIBLY_SUPPORTED => TARGET_UNIX || TARGET_OS_RUNTIMEDETERMINED;

    internal static bool TARGET_WINDOWS_POSSIBLY_SUPPORTED =>
        TARGET_WINDOWS || (TARGET_OS_RUNTIMEDETERMINED && !TARGET_UNIX_OS_RUNTIMEDETERMINED);

    internal static bool SWIFT_SUPPORT => TARGET_ARM64 || (TARGET_AMD64 && TARGET_UNIX);

    internal static bool TARGET_OS_RUNTIMEDETERMINED => GetFeatureSwitch(nameof(TARGET_OS_RUNTIMEDETERMINED));

    internal static bool TARGET_UNIX_OS_RUNTIMEDETERMINED => GetFeatureSwitch(nameof(TARGET_UNIX_OS_RUNTIMEDETERMINED));

    internal static bool UNIX_X86_ABI => GetFeatureSwitch(nameof(UNIX_X86_ABI));

    private static bool GetFeatureSwitch(string featureName)
    {
        var switchName = $"RyuJitSharp.{featureName}";
        if (AppContext.TryGetSwitch(switchName, out var isEnabled))
        {
            return isEnabled;
        }

        throw new InvalidOperationException($"Required compiler configuration switch '{switchName}' is not set.");
    }
}
