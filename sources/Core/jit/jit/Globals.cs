// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics;

namespace RyuJitSharp;

public partial class Globals
{
#if HOST_X86
#if HOST_ARM
#error Cannot define both HOST_X86 and HOST_ARM
#endif
#if HOST_AMD64
#error Cannot define both HOST_X86 and HOST_AMD64
#endif
#if HOST_ARM64
#error Cannot define both HOST_X86 and HOST_ARM64
#endif
#if HOST_LOONGARCH64
#error Cannot define both HOST_X86 and HOST_LOONGARCH64
#endif
#if HOST_RISCV64
#error Cannot define both HOST_X86 and HOST_RISCV64
#endif
#elif HOST_AMD64
#if HOST_X86
#error Cannot define both HOST_AMD64 and HOST_X86
#endif
#if HOST_ARM
#error Cannot define both HOST_AMD64 and HOST_ARM
#endif
#if HOST_ARM64
#error Cannot define both HOST_AMD64 and HOST_ARM64
#endif
#if HOST_LOONGARCH64
#error Cannot define both HOST_AMD64 and HOST_LOONGARCH64
#endif
#if HOST_RISCV64
#error Cannot define both HOST_AMD64 and HOST_RISCV64
#endif
#elif HOST_ARM
#if HOST_X86
#error Cannot define both HOST_ARM and HOST_X86
#endif
#if HOST_AMD64
#error Cannot define both HOST_ARM and HOST_AMD64
#endif
#if HOST_ARM64
#error Cannot define both HOST_ARM and HOST_ARM64
#endif
#if HOST_LOONGARCH64
#error Cannot define both HOST_ARM and HOST_LOONGARCH64
#endif
#if HOST_RISCV64
#error Cannot define both HOST_ARM and HOST_RISCV64
#endif
#elif HOST_ARM64
#if HOST_X86
#error Cannot define both HOST_ARM64 and HOST_X86
#endif
#if HOST_AMD64
#error Cannot define both HOST_ARM64 and HOST_AMD64
#endif
#if HOST_ARM
#error Cannot define both HOST_ARM64 and HOST_ARM
#endif
#if HOST_LOONGARCH64
#error Cannot define both HOST_ARM64 and HOST_LOONGARCH64
#endif
#if HOST_RISCV64
#error Cannot define both HOST_ARM64 and HOST_RISCV64
#endif
#elif HOST_LOONGARCH64
#if HOST_X86
#error Cannot define both HOST_LOONGARCH64 and HOST_X86
#endif
#if HOST_AMD64
#error Cannot define both HOST_LOONGARCH64 and HOST_AMD64
#endif
#if HOST_ARM
#error Cannot define both HOST_LOONGARCH64 and HOST_ARM
#endif
#if HOST_ARM64
#error Cannot define both HOST_LOONGARCH64 and HOST_ARM64
#endif
#if HOST_RISCV64
#error Cannot define both HOST_LOONGARCH64 and HOST_RISCV64
#endif
#elif HOST_RISCV64
#if HOST_X86
#error Cannot define both HOST_RISCV64 and HOST_X86
#endif
#if HOST_AMD64
#error Cannot define both HOST_RISCV64 and HOST_AMD64
#endif
#if HOST_ARM
#error Cannot define both HOST_RISCV64 and HOST_ARM
#endif
#if HOST_ARM64
#error Cannot define both HOST_RISCV64 and HOST_ARM64
#endif
#if HOST_LOONGARCH64
#error Cannot define both HOST_RISCV64 and HOST_LOONGARCH64
#endif
#else
#error Unsupported or unset host architecture
#endif

#if TARGET_X86
#if TARGET_ARM
#error Cannot define both TARGET_X86 and TARGET_ARM
#endif
#if TARGET_AMD64
#error Cannot define both TARGET_X86 and TARGET_AMD64
#endif
#if TARGET_ARM64
#error Cannot define both TARGET_X86 and TARGET_ARM64
#endif
#if TARGET_LOONGARCH64
#error Cannot define both TARGET_X86 and TARGET_LOONGARCH64
#endif
#if TARGET_RISCV64
#error Cannot define both TARGET_X86 and TARGET_RISCV64
#endif
#if TARGET_WASM32
#error Cannot define both TARGET_X86 and TARGET_WASM32
#endif
#elif TARGET_AMD64
#if TARGET_X86
#error Cannot define both TARGET_AMD64 and TARGET_X86
#endif
#if TARGET_ARM
#error Cannot define both TARGET_AMD64 and TARGET_ARM
#endif
#if TARGET_ARM64
#error Cannot define both TARGET_AMD64 and TARGET_ARM64
#endif
#if TARGET_LOONGARCH64
#error Cannot define both TARGET_AMD64 and TARGET_LOONGARCH64
#endif
#if TARGET_RISCV64
#error Cannot define both TARGET_AMD64 and TARGET_RISCV64
#endif
#if TARGET_WASM32
#error Cannot define both TARGET_AMD64 and TARGET_WASM32
#endif
#elif TARGET_ARM
#if TARGET_X86
#error Cannot define both TARGET_ARM and TARGET_X86
#endif
#if TARGET_AMD64
#error Cannot define both TARGET_ARM and TARGET_AMD64
#endif
#if TARGET_ARM64
#error Cannot define both TARGET_ARM and TARGET_ARM64
#endif
#if TARGET_LOONGARCH64
#error Cannot define both TARGET_ARM and TARGET_LOONGARCH64
#endif
#if TARGET_RISCV64
#error Cannot define both TARGET_ARM and TARGET_RISCV64
#endif
#if TARGET_WASM32
#error Cannot define both TARGET_ARM and TARGET_WASM32
#endif
#elif TARGET_ARM64
#if TARGET_X86
#error Cannot define both TARGET_ARM64 and TARGET_X86
#endif
#if TARGET_AMD64
#error Cannot define both TARGET_ARM64 and TARGET_AMD64
#endif
#if TARGET_ARM
#error Cannot define both TARGET_ARM64 and TARGET_ARM
#endif
#if TARGET_LOONGARCH64
#error Cannot define both TARGET_ARM64 and TARGET_LOONGARCH64
#endif
#if TARGET_RISCV64
#error Cannot define both TARGET_ARM64 and TARGET_RISCV64
#endif
#if TARGET_WASM32
#error Cannot define both TARGET_ARM64 and TARGET_WASM32
#endif
#elif TARGET_LOONGARCH64
#if TARGET_X86
#error Cannot define both TARGET_LOONGARCH64 and TARGET_X86
#endif
#if TARGET_AMD64
#error Cannot define both TARGET_LOONGARCH64 and TARGET_AMD64
#endif
#if TARGET_ARM
#error Cannot define both TARGET_LOONGARCH64 and TARGET_ARM
#endif
#if TARGET_ARM64
#error Cannot define both TARGET_LOONGARCH64 and TARGET_ARM64
#endif
#if TARGET_RISCV64
#error Cannot define both TARGET_LOONGARCH64 and TARGET_RISCV64
#endif
#if TARGET_WASM32
#error Cannot define both TARGET_LOONGARCH64 and TARGET_WASM32
#endif
#elif TARGET_RISCV64
#if TARGET_X86
#error Cannot define both TARGET_RISCV64 and TARGET_X86
#endif
#if TARGET_AMD64
#error Cannot define both TARGET_RISCV64 and TARGET_AMD64
#endif
#if TARGET_ARM
#error Cannot define both TARGET_RISCV64 and TARGET_ARM
#endif
#if TARGET_ARM64
#error Cannot define both TARGET_RISCV64 and TARGET_ARM64
#endif
#if TARGET_LOONGARCH64
#error Cannot define both TARGET_RISCV64 and TARGET_LOONGARCH64
#endif
#if TARGET_WASM32
#error Cannot define both TARGET_RISCV64 and TARGET_WASM32
#endif

#elif TARGET_WASM32
#if TARGET_X86
#error Cannot define both TARGET_WASM32 and TARGET_X86
#endif
#if TARGET_AMD64
#error Cannot define both TARGET_WASM32 and TARGET_AMD64
#endif
#if TARGET_ARM
#error Cannot define both TARGET_WASM32 and TARGET_ARM
#endif
#if TARGET_ARM64
#error Cannot define both TARGET_WASM32 and TARGET_ARM64
#endif
#if TARGET_LOONGARCH64
#error Cannot define both TARGET_WASM32 and TARGET_LOONGARCH64
#endif
#if TARGET_RISCV64
#error Cannot define both TARGET_WASM32 and TARGET_RISCV64
#endif
#else
#error Unsupported or unset target architecture
#endif

#if TARGET_64BIT
#if TARGET_X86
#error Cannot define both TARGET_X86 and TARGET_64BIT
#endif
#if TARGET_ARM
#error Cannot define both TARGET_ARM and TARGET_64BIT
#endif
#if TARGET_WASM32
#error Cannot define both TARGET_WASM32 and TARGET_64BIT
#endif
#endif

#if UNIX_AMD64_ABI && !TARGET_AMD64
#error When UNIX_AMD64_ABI is defined, you must define TARGET_AMD64 as well.
#endif

#if UNIX_X86_ABI && !TARGET_X86
#error When UNIX_X86_ABI is defined, you must define TARGET_X86 as well.
#endif

#if USE_COREDISTOOLS && !LATE_DISASM
#error When USE_COREDISTOOLS is defined, you must define LATE_DISASM as well.
#endif

    public const int REGEN_SHORTCUTS = 0;

    public const int REGEN_CALLPAT = 0;

    /// <summary>Did Jit or Inline succeeded?</summary>
    public const int INFO6 = LL_INFO10000;

    /// <summary>NYI stuff.</summary>
    public const int INFO7 = LL_INFO100000;

    /// <summary>Weird failures.</summary>
    public const int INFO8 = LL_INFO1000000;

    public static unsafe CORINFO_OBJECT_HANDLE NO_OBJECT_HANDLE => null;

    public static unsafe CORINFO_CLASS_HANDLE NO_CLASS_HANDLE => null;

    public static unsafe CORINFO_FIELD_HANDLE NO_FIELD_HANDLE => null;

    public static unsafe CORINFO_METHOD_HANDLE NO_METHOD_HANDLE => null;

    public const IL_OFFSET BAD_IL_OFFSET = 0xFFFF_FFFF;

    public const uint BAD_VAR_NUM = uint.MaxValue;

    public const ushort BAD_LCL_OFFSET = ushort.MaxValue;

    // For the following specially handled FIELD_HANDLES we need
    //   values that are negative and have the low two bits zero
    // See eeFindJitDataOffs and eeGetJitDataOffs in Compiler.hpp
    public static unsafe CORINFO_FIELD_HANDLE FLD_GLOBAL_DS => (CORINFO_FIELD_HANDLE)(-4);

    public static unsafe CORINFO_FIELD_HANDLE FLD_GLOBAL_FS => (CORINFO_FIELD_HANDLE)(-8);

    public static unsafe CORINFO_FIELD_HANDLE FLD_GLOBAL_GS => (CORINFO_FIELD_HANDLE)(-12);

    // offset of vtable pointer from obj ptr
    public const int VPTR_OFFS = 0;

#if MEASURE_CLRAPI_CALLS
#if FEATURE_JIT_METHOD_PERF
#error Can't time these calls without METHOD_PERF.
#endif
#if DEBUG
#error No point in measuring DEBUG code.
#endif
#if !HOST_X86 && !HOST_AMD64
#error Cycle counters only hooked up on x86/x64.
#endif
#endif

#if FEATURE_TAILCALL_OPT_SHARED_RETURN && !FEATURE_TAILCALL_OPT
#error When FEATURE_TAILCALL_OPT_SHARED_RETURN is defined, you must define FEATURE_TAILCALL_OPT as well.
#endif

    public const int CLFLG_REGVAR = 0x00008;

    public const int CLFLG_TREETRANS = 0x00100;

    public const int CLFLG_INLINING = 0x00200;

#if FEATURE_STRUCTPROMOTE
    public const int CLFLG_STRUCTPROMOTE = 0x00400;
#else
    public const int CLFLG_STRUCTPROMOTE = 0x00000;
#endif

    public const int CLFLG_MAXOPT = CLFLG_REGVAR | CLFLG_TREETRANS | CLFLG_INLINING | CLFLG_STRUCTPROMOTE;

    public const int CLFLG_MINOPT = CLFLG_TREETRANS;

    [Conditional("DEBUG")]
    public static void JITDUMP(string format, params ReadOnlySpan<object> args)
    {
        var compiler = JitTls.GetCompiler();
        assert(compiler is not null);

        if (compiler.verbose)
        {
            logf(format, args);
        }
    }
}
