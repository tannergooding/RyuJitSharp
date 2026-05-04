// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Globals
{
    public static readonly string[] PhaseNames = [
        // TODO: Port PhaseNames
    ];

    public static readonly string[] PhaseEnums = [
        // TODO: Port PhaseEnums
    ];

    public const CorInfoFlag FLG_CCTOR = CORINFO_FLG_CONSTRUCTOR | CORINFO_FLG_STATIC;

#if DEBUG
    // for LclVarDsc::lvStkOffs
    public const int BAD_STK_OFFS = unchecked((int)(0xBAADF00D));
#endif

    public const uint ROOT_FUNC_IDX = 0;

    /// <summary>Limit frames size to 1GB.</summary>
    /// <remarks>The maximum is 2GB in theory - make it intentionally smaller to avoid bugs from borderline cases.</remarks>
    public const int MAX_FrameSize = 0x3FFF_FFFF;

    /// <summary>Maximum number of fields in promotable struct.</summary>
    public const int MAX_NumOfFieldsInPromotableStruct = 4;

    /// <summary>Number of elements in impSmallStack.</summary>
    public const int SMALL_STACK_SIZE = 16;

    public const string FMT_CSE = "CSE #{0:D2}";

    /// <summary>Method contains 'new' of an SD array.</summary>
    public const int OMF_HAS_NEWARRAY = 0x00000001;

    /// <summary>Method contains 'new' of an object type.</summary>
    public const int OMF_HAS_NEWOBJ = 0x00000002;

    /// <summary>Method contains array element loads or stores.</summary>
    public const int OMF_HAS_ARRAYREF = 0x00000004;

    /// <summary>Method contains null check.</summary>
    public const int OMF_HAS_NULLCHECK = 0x00000008;

    /// <summary>Method contains call, that needs fat pointer transformation.</summary>
    public const int OMF_HAS_FATPOINTER = 0x00000010;

    /// <summary>Method contains an object allocated on the stack.</summary>
    public const int OMF_HAS_OBJSTACKALLOC = 0x00000020;

    /// <summary>Method contains guarded devirtualization candidate.</summary>
    public const int OMF_HAS_GUARDEDDEVIRT = 0x00000040;

    /// <summary>Method contains a runtime lookup to an expandable dictionary.</summary>
    public const int OMF_HAS_EXPRUNTIMELOOKUP = 0x00000080;

    /// <summary>Method contains patchpoints.</summary>
    public const int OMF_HAS_PATCHPOINT = 0x00000100;

    /// <summary>Method needs GC polls.</summary>
    public const int OMF_NEEDS_GCPOLLS = 0x00000200;

    /// <summary>Method has potential tail call in a non <see cref="BBJ_RETURN" /> block.</summary>
    public const int OMF_HAS_TAILCALL_SUCCESSOR = 0x00001000;

    /// <summary>Method contains 'new' of an MD array.</summary>
    public const int OMF_HAS_MDNEWARRAY = 0x00002000;

    /// <summary>Method contains multi-dimensional intrinsic array element loads or stores.</summary>
    public const int OMF_HAS_MDARRAYREF = 0x00004000;

    /// <summary>Method has static initializations we might want to partially inline.</summary>
    public const int OMF_HAS_STATIC_INIT = 0x00008000;

    /// <summary>Method contains TLS field access.</summary>
    public const int OMF_HAS_TLS_FIELD = 0x00010000;

    /// <summary>Method contains special intrinsics expanded in late phases.</summary>
    public const int OMF_HAS_SPECIAL_INTRINSICS = 0x00020000;

    /// <summary>Method contains recursive tail call.</summary>
    public const int OMF_HAS_RECURSIVE_TAILCALL = 0x00040000;

    /// <summary>Method contains casts eligible for late expansion.</summary>
    public const int OMF_HAS_EXPANDABLE_CAST = 0x00080000;

    /// <summary>Method contains stack allocated arrays</summary>
    public const int OMF_HAS_STACK_ARRAY = 0x00100000;

    /// <summary>Method contains bounds checks</summary>
    public const int OMF_HAS_BOUNDS_CHECKS = 0x00200000;

    /// <summary>Method contains early expandable QMARKs</summary>
    public const int OMF_HAS_EARLY_QMARKS = 0x00400000;

    //
    // optimize maximally and/or favor speed over size?
    //

    public const int DEFAULT_MIN_OPTS_CODE_SIZE = 60000;

    public const int DEFAULT_MIN_OPTS_INSTR_COUNT = 20000;

    public const int DEFAULT_MIN_OPTS_BB_COUNT = 2000;

    public const int DEFAULT_MIN_OPTS_LV_NUM_COUNT = 2000;

    public const int DEFAULT_MIN_OPTS_LV_REF_COUNT = 8000;

    /// <summary>Maximum number of locals before turning off the inlining.</summary>
    public const int MAX_LV_NUM_COUNT_FOR_INLINING = 512;

    //
    // Default numbers used to perform loop alignment. All the numbers are chosen
    // based on experimenting with various benchmarks.
    //

    /// <summary>Default minimum loop block weight required to enable loop alignment.</summary>
    public const int DEFAULT_ALIGN_LOOP_MIN_BLOCK_WEIGHT = 3;

    /// <summary>By default a loop will be aligned at 32B address boundary to get better performance as per architecture manuals.</summary>
    public const int DEFAULT_ALIGN_LOOP_BOUNDARY = 0x20;

    // For non-adaptive loop alignment, by default, only align a loop whose size is
    // at most 3 times the alignment block size. If the loop is bigger than that, it is most
    // likely complicated enough that loop alignment will not impact performance.
    public const int DEFAULT_MAX_LOOPSIZE_FOR_ALIGN = DEFAULT_ALIGN_LOOP_BOUNDARY * 3;

    /// <summary>By default only loops with a constant iteration count less than or equal to this will be unrolled.</summary>
    public const int DEFAULT_UNROLL_LOOP_MAX_ITERATION_COUNT = 4;

    public const int MAX_STRESS_WEIGHT = 100;

    // Quirk for VS debug-launch scenario to work:
    // Bytes of padding between save-reg area and locals.
    // TODO: public const int VSQUIRK_STACK_PAD = 2 * REGSIZE_BYTES;

    /// <summary>Methods with > DEFAULT_MAX_INLINE_SIZE IL bytes will never be inlined.</summary>
    /// <remarks>This can be overwritten by setting DOTNET_JITInlineSize env variable.</remarks>
    public const int DEFAULT_MAX_INLINE_SIZE = 100;

    /// <summary>Methods at more than this level deep will not be inlined.</summary>
    public const int DEFAULT_MAX_INLINE_DEPTH = 20;

    /// <summary>Maximum estimated compile time increase via inlining</summary>
    public const int DEFAULT_INLINE_BUDGET = 22;

    /// <summary>Methods at more than this level deep will not be force inlined.</summary>
    public const int DEFAULT_MAX_FORCE_INLINE_DEPTH = 1;

    /// <summary>Fixed locallocs of this size or smaller will convert to local buffers.</summary>
    public const int DEFAULT_MAX_LOCALLOC_TO_LOCAL_SIZE = 32;

    public static var_types HfaTypeFromElemKind(CorInfoHFAElemType kind) => kind switch {
        CORINFO_HFA_ELEM_NONE => TYP_UNDEF,
        CORINFO_HFA_ELEM_FLOAT => TYP_FLOAT,
        CORINFO_HFA_ELEM_DOUBLE => TYP_DOUBLE,
        CORINFO_HFA_ELEM_VECTOR64 => TYP_SIMD8,
        CORINFO_HFA_ELEM_VECTOR128 => TYP_SIMD16,
        _ => TYP_UNKNOWN,
    };

    // Compile a single method
    public static unsafe CorJitResult jitNativeCode(CORINFO_METHOD_HANDLE methodHandle, CORINFO_MODULE_HANDLE classHandle, COMP_HANDLE jitInfo, CORINFO_METHOD_INFO* methodInfo, out void* methodCodePtr, out uint methodCodeSize, JitFlags* jitFlags, InlineInfo? inlineInfo)
    {
        // A non-null inlineInfo means we are compiling the inlinee method.
        var result = JitNativeCodeCore(methodHandle, classHandle, jitInfo, methodInfo, out methodCodePtr, out methodCodeSize, jitFlags, inlineInfo, jitFallbackCompile: false);

        if ((inlineInfo is null) && (result is CORJIT_INTERNALERROR or CORJIT_RECOVERABLEERROR or CORJIT_IMPLLIMITATION or CORJIT_R2R_UNSUPPORTED))
        {
            // Update the flags for 'safer' code generation.
            jitFlags->Set(JitFlag.JIT_FLAG_MIN_OPT);
            jitFlags->Clear(JitFlag.JIT_FLAG_SIZE_OPT);
            jitFlags->Clear(JitFlag.JIT_FLAG_SPEED_OPT);
            jitFlags->Clear(JitFlag.JIT_FLAG_BBOPT);

            // Reattempt with debuggable code.
            result = JitNativeCodeCore(methodHandle, classHandle, jitInfo, methodInfo, out methodCodePtr, out methodCodeSize, jitFlags, inlineInfo, jitFallbackCompile: true);
        }

        return result;

        static CorJitResult JitNativeCodeCore(CORINFO_METHOD_HANDLE methodHandle, CORINFO_MODULE_HANDLE classHandle, COMP_HANDLE jitInfo, CORINFO_METHOD_INFO* methodInfo, out void* methodCodePtr, out uint methodCodeSize, JitFlags* jitFlags, InlineInfo? inlineInfo, bool jitFallbackCompile)
        {
            var result = CORJIT_INTERNALERROR;

            try
            {
                var compiler = null as Compiler;
                var previousCompiler = null as Compiler;

                try
                {
                    if (inlineInfo is not null)
                    {
                        var inlinerCompiler = inlineInfo.InlinerCompiler;
                        compiler = inlinerCompiler.InlineeCompiler;

                        if (compiler is null)
                        {
                            // Lazily create the inlinee compiler object
                            compiler = new Compiler(methodHandle, jitInfo, methodInfo, inlineInfo);
                            inlinerCompiler.InlineeCompiler = compiler;
                        }
                    }
                    else
                    {
                        compiler = new Compiler(methodHandle, jitInfo, methodInfo, inlineInfo);
                    }

#if MEASURE_CLRAPI_CALLS
                    var wrapCLR = WrapICorJitInfo::makeOne(pParam->pAlloc, pComp, compHnd);
#endif

                    // push this compiler on the stack (TLS)
                    previousCompiler = JitTls.Compiler;
                    JitTls.Compiler = compiler;

#if DEBUG
                    compiler.jitFallbackCompile = jitFallbackCompile;
#endif

                    // Now generate the code
                    result = compiler.compCompileAfterInit(classHandle, out methodCodePtr, out methodCodeSize, jitFlags);
                }
                finally
                {
                    // If OOM is thrown when allocating memory for a pComp, we will end up here.
                    // For this case, pComp and also pCompiler will be a null

                    if (compiler is not null)
                    {
                        compiler.info.compCode = null;

                        // pop the compiler off the TLS stack only if it was linked above
                        assert(JitTls.Compiler == compiler);

                        JitTls.Compiler = previousCompiler;
                    }
                }
            }
            catch (Exception ex) when (ex.HResult == FATAL_JIT_EXCEPTION)
            {
                if (jitInfo is not null)
                {
                    jitInfo->reportFatalError(CORJIT_INTERNALERROR);
                }

                // If we were looking at an inlinee....
                // Note that we failed to compile the inlinee, and that there's no point trying to inline it again anywhere else.
                inlineInfo?.inlineResult.NoteFatal(InlineObservation.CALLEE_COMPILATION_ERROR);
                result = CORJIT_INTERNALERROR;

                methodCodePtr = null;
                methodCodeSize = 0;
            }

            return result;
        }
    }

    /// <summary>ConfigInteger does not offer an option for decimal flags, any numbers are interpreted as hex.</summary>
    /// <param name="value"></param>
    /// <returns>I could add the decimal option to ConfigInteger or I could write a function to reinterpret this value as the user intended.</returns>
    public static uint ReinterpretHexAsDecimal(uint value)
    {
        // ex: in: 0x100 returns: 100
        var result = 0u;
        var index = 1u;

        // default value
        if (value == int.MaxValue)
        {
            return value;
        }

        while (value != 0)
        {
            var digit = value % 16;
            value >>= 4;

            assert(digit < 10);

            result += digit * index;
            index *= 10;
        }
        return result;
    }

#if PROFILING_SUPPORTED
    // A Dummy routine to receive Enter/Leave/Tailcall profiler callbacks.
    // These are used when DOTNET_JitEltHookEnabled=1
#if TARGET_AMD64
    internal static void DummyProfilerELTStub(nuint ProfilerHandle, nuint callerSP) { }
#else
    internal static void DummyProfilerELTStub(nuint ProfilerHandle) { }
#endif
#endif
}
