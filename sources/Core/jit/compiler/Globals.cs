// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;

namespace RyuJitSharp;

public partial class Globals
{
    public const CorInfoFlag FLG_CCTOR = CORINFO_FLG_CONSTRUCTOR | CORINFO_FLG_STATIC;

#if DEBUG
    // for LclVarDsc.lvStkOffs
    public const int BAD_STK_OFFS = unchecked((int)(0xBAADF00D));
#endif

    public const int ROOT_FUNC_IDX = 0;

    /// <summary>Limit frames size to 1GB.</summary>
    /// <remarks>The maximum == 2GB in theory - make it intentionally smaller to avoid bugs from borderline cases.</remarks>
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

#if DEBUG
    public static bool InlinePInvokeEnabled => JitConfig[ConfigInteger.JitPInvokeEnabled] is not 0;
#else
    public static bool InlinePInvokeEnabled => true;
#endif

    /// <summary>Should we enable JitStress mode?</summary>
    /// <remarks>
    ///   <list type="bullet">
    ///     <item>0:    No stress</item>
    ///     <item>!= 2: Vary stress. Performance will be slightly/moderately degraded</item>
    ///     <item>2:    Check-all stress. Performance will be REALLY horrible</item>
    ///   </list>
    /// </remarks>
    public static int JitStressLevel => JitConfig[ConfigInteger.JitStress];

    /// <summary>Converts input ASCII data to lower case</summary>
    /// <param name="input">Constant data to change casing to lower</param>
    /// <param name="mask">Mask to apply to non-constant data</param>
    /// <returns>false if input contains non-ASCII chars</returns>
    public static bool ConvertToLowerCase(Span<char> input, Span<char> mask)
    {
        for (var i = 0; i < input.Length; i++)
        {
            var ch = input[i];

            if (ch > 127)
            {
                JITDUMP("Constant data contains non-ASCII char(s), give up.\n");
                return false;
            }

            // Inside [0..127] range only [a-z] and [A-Z] sub-ranges are
            // eligible for case changing, we can't apply 0x20 bit for e.g. '-'
            if ((ch is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z')))
            {
                input[i] |= (char)(0x20);
                mask[i] = (char)(0x20);
            }
            else
            {
                mask[i] = '\0';
            }
        }
        return true;
    }

    /// <summary>sets value of tree to garbage to catch extra references</summary>
    /// <param name="tree">This node should not be referenced by anyone now</param>
    [Conditional("DEBUG")]
    public static void DEBUG_DESTROY_NODE(GenTree tree)
    {
#if DEBUG
        // jitprintf($"DEBUG_DESTROY_NODE for [0x{tree.TreeId:X8}]\n", tree);

        // Save gtOper in case we want to find out what this node was
        tree._operSave = tree._oper;

        tree._type = TYP_UNDEF;
        tree.Flags |= ~GTF_NODE_MASK;

        foreach (ref var use in tree.UseEdges)
        {
            use = null;
        }

        // Must do this last, because the "AsOp()" check above will fail otherwise.
        // Don't call SetOper, because GT_COUNT is not a valid value
        tree._oper = GT_COUNT;
#endif
    }

#if DEBUG
    /// <summary>describe the detailed devirtualization reason</summary>
    /// <param name="detail">detail to describe</param>
    /// <returns>descriptive string</returns>
    public static string DevirtualizationDetailToString(CORINFO_DEVIRTUALIZATION_DETAIL detail) => detail switch {
        CORINFO_DEVIRTUALIZATION_UNKNOWN => "unknown",
        CORINFO_DEVIRTUALIZATION_SUCCESS => "success",
        CORINFO_DEVIRTUALIZATION_FAILED_CANON => "object class or method was canonical",
        CORINFO_DEVIRTUALIZATION_FAILED_COM => "object class was com",
        CORINFO_DEVIRTUALIZATION_FAILED_CAST => "object class could not be cast to interface class",
        CORINFO_DEVIRTUALIZATION_FAILED_LOOKUP => "interface method could not be found",
        CORINFO_DEVIRTUALIZATION_FAILED_DIM => "interface method was default interface method",
        CORINFO_DEVIRTUALIZATION_FAILED_SUBCLASS => "object not subclass of base class",
        CORINFO_DEVIRTUALIZATION_FAILED_SLOT => "virtual method installed via explicit override",
        CORINFO_DEVIRTUALIZATION_FAILED_BUBBLE => "devirtualization crossed version bubble",
        CORINFO_DEVIRTUALIZATION_MULTIPLE_IMPL => "object class has multiple implementations of interface",
        CORINFO_DEVIRTUALIZATION_FAILED_BUBBLE_CLASS_DECL => "decl method is defined on class and decl method not in version bubble, and decl method not in type closest to version bubble",
        CORINFO_DEVIRTUALIZATION_FAILED_BUBBLE_INTERFACE_DECL => "decl method is defined on interface and not in version bubble, and implementation type not entirely defined in bubble",
        CORINFO_DEVIRTUALIZATION_FAILED_BUBBLE_IMPL => "object class not defined within version bubble",
        CORINFO_DEVIRTUALIZATION_FAILED_BUBBLE_IMPL_NOT_REFERENCEABLE => "object class cannot be referenced from R2R code due to missing tokens",
        CORINFO_DEVIRTUALIZATION_FAILED_DUPLICATE_INTERFACE => "crossgen2 virtual method algorithm and runtime algorithm differ in the presence of duplicate interface implementations",
        CORINFO_DEVIRTUALIZATION_FAILED_DECL_NOT_REPRESENTABLE => "Decl method cannot be represented in R2R image",
        CORINFO_DEVIRTUALIZATION_FAILED_TYPE_EQUIVALENCE => "Support for type equivalence in devirtualization is not yet implemented in crossgen2",
        _ => "undefined",
    };
#endif

    public static IRegAlloc GetRegisterAllocator(Compiler compiler) => new LinearScan(compiler);

    public static bool handlerGetsXcptnObj(bbCatchType hndType) => hndType is not BBCT_NONE and not BBCT_FAULT and not BBCT_FINALLY;

    public static var_types HfaTypeFromElemKind(CorInfoHFAElemType kind) => kind switch {
        CORINFO_HFA_ELEM_NONE => TYP_UNDEF,
        CORINFO_HFA_ELEM_FLOAT => TYP_FLOAT,
        CORINFO_HFA_ELEM_DOUBLE => TYP_DOUBLE,
        CORINFO_HFA_ELEM_VECTOR64 => TYP_SIMD8,
        CORINFO_HFA_ELEM_VECTOR128 => TYP_SIMD16,
        _ => TYP_UNKNOWN,
    };

    // Compile a single method
    public static unsafe CorJitResult jitNativeCode(CORINFO_METHOD_HANDLE methodHandle, CORINFO_MODULE_HANDLE classHandle, COMP_HANDLE jitInfo, CORINFO_METHOD_INFO* methodInfo, out void* methodCodePtr, out int methodCodeSize, JitFlags* jitFlags, InlineInfo? inlineInfo)
    {
        // A non-null inlineInfo means we are compiling the inlinee method.
        var result = JitNativeCodeCore(methodHandle, classHandle, jitInfo, methodInfo, out methodCodePtr, out methodCodeSize, jitFlags, inlineInfo, jitFallbackCompile: false);

        if ((inlineInfo is null) && (result is CORJIT_INTERNALERROR or CORJIT_RECOVERABLEERROR or CORJIT_IMPLLIMITATION or CORJIT_R2R_UNSUPPORTED))
        {
            // Update the flags for 'safer' code generation.
            jitFlags->Set(JitFlags.JIT_FLAG_MIN_OPT);
            jitFlags->Clear(JitFlags.JIT_FLAG_SIZE_OPT);
            jitFlags->Clear(JitFlags.JIT_FLAG_SPEED_OPT);
            jitFlags->Clear(JitFlags.JIT_FLAG_BBOPT);

            // Reattempt with debuggable code.
            result = JitNativeCodeCore(methodHandle, classHandle, jitInfo, methodInfo, out methodCodePtr, out methodCodeSize, jitFlags, inlineInfo, jitFallbackCompile: true);
        }

        return result;

        static CorJitResult JitNativeCodeCore(CORINFO_METHOD_HANDLE methodHandle, CORINFO_MODULE_HANDLE classHandle, COMP_HANDLE jitInfo, CORINFO_METHOD_INFO* methodInfo, out void* methodCodePtr, out int methodCodeSize, JitFlags* jitFlags, InlineInfo? inlineInfo, bool jitFallbackCompile)
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
                    var wrapCLR = WrapICorJitInfo.makeOne(pParam->pAlloc, pComp, compHnd);
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
    public static int ReinterpretHexAsDecimal(int value)
    {
        // ex: in: 0x100 returns: 100
        var result = 0;
        var index = 1;

        // default value
        if (value == int.MaxValue)
        {
            return value;
        }

        while (value != 0)
        {
            var digit = value & 0x0F;
            value >>>= 4;

            assert(digit < 10);

            result += digit * index;
            index *= 10;
        }
        return result;
    }

#if DEBUG
    /// <summary>Return a string representation of a weighted ref count</summary>
    /// <param name="refCntWtd">weight to format</param>
    /// <param name="padForDecimalPlaces">If true, pad any integral or non-numeric output on the right with three spaces, representing space for ".00".</param>
    /// <returns></returns>
    public static string refCntWtd2str(weight_t refCntWtd, bool padForDecimalPlaces = false)
    {
        if (refCntWtd >= BB_MAX_WEIGHT)
        {
            return padForDecimalPlaces ? "MAX   " : "MAX";
        }
        else
        {
            var scaledWeight = refCntWtd / BB_UNITY_WEIGHT;
            var intPart = weight_t.Floor(scaledWeight);

            var isLarge = intPart > 1e9;
            var isSmall = (intPart < 1e-2) && (intPart != 0);

            // Use g format for high dynamic range counts.
            //
            if (isLarge || isSmall)
            {
                return $"{scaledWeight:G2}";
            }
            else
            {
                if (intPart == scaledWeight)
                {
                    if (padForDecimalPlaces)
                    {
                        return $"{intPart}   ";
                    }
                    else
                    {
                        return $"{intPart}";
                    }
                }
                else
                {
                    return $"{scaledWeight:F2}";
                }
            }
        }
    }
#endif

#if PROFILING_SUPPORTED
    // A Dummy routine to receive Enter/Leave/Tailcall profiler callbacks.
    // These are used when DOTNET_JitEltHookEnabled=1
#if TARGET_AMD64
    internal static void DummyProfilerELTStub(nint ProfilerHandle, nint callerSP) { }
#else
    internal static void DummyProfilerELTStub(nint ProfilerHandle) { }
#endif
#endif
}
