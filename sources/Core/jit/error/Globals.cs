// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Globals
{
    public const int FATAL_JIT_EXCEPTION = 0x02345678;

#if MEASURE_FATAL
    private static int s_fatalBadCodeCount;
    private static int s_fatalNoWayCount;
    private static int s_fatalImplLimitationCount;
    private static int s_fatalNoMemCount;
    private static int s_fatalNoWayAssertBodyCount;
#if DEBUG
    private static int s_fatalNoWayAssertBodyArgsCount;
#endif
    private static int s_fatalNyiCount;
#endif

#if DEBUG
    private static void debugError(ReadOnlySpan<char> message, ReadOnlySpan<char> filePath, int lineNumber)
    {
        var fileName = Path.GetFileName(filePath);
        ref var logEnv = ref JitTls.LogEnv;

        var compiler = logEnv.Compiler;
        assert(compiler is not null);

        JITDUMP($"\nCOMPILATION FAILED: {message} ({fileName}:{lineNumber})\n");
        logf(LL_ERROR, $"COMPILATION FAILED: file: {fileName}:{lineNumber} compiling method {compiler.info.compFullName} reason {message}\n");

        // We now only assert when user explicitly set DOTNET_JitRequired=1
        // If DOTNET_JitRequired == 0 or is not set, we will not assert.
        if ((JitConfig[ConfigInteger.JitRequired] == 1) || (getBreakOnBadCode() != 0))
        {
            assertAbort(message, filePath, lineNumber);
        }

        BreakIfDebuggerPresent();
    }
#endif

    [DoesNotReturn]
    public static void badCode()
    {
#if MEASURE_FATAL
        s_fatalBadCodeCount++;
#endif

        fatal(CORJIT_BADCODE);
    }

#if DEBUG
    [DoesNotReturn]
    public static void badCode3(ReadOnlySpan<char> message, ReadOnlySpan<char> message2, int arg, ReadOnlySpan<char> filePath, int lineNumber)
    {
        debugError(string.Format(CultureInfo.InvariantCulture, $"{message}{message2}", arg), filePath, lineNumber);
        badCode();
    }
#endif

    [DoesNotReturn]
    public static void noWay()
    {
#if MEASURE_FATAL
        s_fatalNoWayCount++;
#endif

        fatal(CORJIT_INTERNALERROR);
    }

    [DoesNotReturn]
    public static void implLimitation()
    {
#if MEASURE_FATAL
        s_fatalImplLimitationCount++;
#endif

        fatal(CORJIT_IMPLLIMITATION);
    }

    [DoesNotReturn]
    public static void implReadyToRunUnsupported() => fatal(CORJIT_R2R_UNSUPPORTED);

    [DoesNotReturn]
    public static void NOMEM()
    {
#if MEASURE_FATAL
        s_fatalNoMemCount++;
#endif

        fatal(CORJIT_OUTOFMEM);
    }

    [DoesNotReturn]
    public static void fatal(CorJitResult jitResult)
    {
#if DEBUG
        // Don't stop on NYI: use DOTNET_AltJitAssertOnNYI for that.
        if (jitResult != CORJIT_SKIPPED)
        {
            if (JitConfig[ConfigInteger.DebugBreakOnVerificationFailure] != 0)
            {
                Debugger.Break();
            }
        }
#endif

        throw new FatalJitException();
    }

    [DoesNotReturn]
    public static void noWayAssertBody()
    {
#if MEASURE_FATAL
        s_fatalNoWayAssertBodyCount++;
#endif

#if !DEBUG
        // Even in retail, if we hit a noway, and we have this variable set, we don't want to fall back
        // to MinOpts, which might hide a regression. Instead, hit a breakpoint (and crash). We don't
        // have the assert code to fall back on here.
        // The debug path goes through this function also, to do the call to 'fatal'.
        // This kind of noway is hit for unreached().
        if (JitConfig[ConfigInteger.JitEnableNoWayAssert] != 0)
        {
            Debugger.Break();
        }
#endif

        fatal(CORJIT_RECOVERABLEERROR);
    }

    [DoesNotReturn]
    public static void noWayAssertBody(ReadOnlySpan<char> message, ReadOnlySpan<char> filePath, int lineNumber)
    {
#if MEASURE_FATAL
        s_fatalNoWayAssertBodyArgsCount++;
#endif

        noWayAssertAbortHelper(message, filePath, lineNumber);
        noWayAssertBody();
    }

    // Conditionally invoke the noway assert body. The conditional predicate is evaluated using a method on the tlsCompiler.
    // If a noway_assert is hit, we ask the Compiler whether to raise an exception (i.e., conditionally raise exception.)
    // To have backward compatibility between v4.5 and v4.0, in min-opts we take a shot at codegen rather than rethrow.
    public static void noWayAssertBodyConditional()
    {
        if (ShouldThrowOnNoway())
        {
            noWayAssertBody();
        }
    }

    public static void noWayAssertBodyConditional(ReadOnlySpan<char> message, ReadOnlySpan<char> filePath, int lineNumber)
    {
        if (ShouldThrowOnNoway())
        {
            noWayAssertBody(message, filePath, lineNumber);
        }
        else
        {
            // In CHK we want the assert UI to show up in min-opts.
            noWayAssertAbortHelper(message, filePath, lineNumber);
        }
    }

#if MEASURE_NOWAY
    public static void RecordNowayAssertGlobal(ReadOnlySpan<char> filePath, int lineNumber, ReadOnlySpan<char> message)
    {
        if ((JitConfig[ConfigInteger.JitMeasureNowayAssert] == 1) && (JitTls.Compiler is Compiler compiler))
        {
            compiler.RecordNowayAssert(filePath, lineNumber, message);
        }
    }

    public static void RECORD_NOWAY_ASSERT(ReadOnlySpan<char> message, [CallerFilePath] ReadOnlySpan<char> filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        RecordNowayAssertGlobal(filePath, lineNumber, message);
    }
#else
    public static void RECORD_NOWAY_ASSERT(ReadOnlySpan<char> message)
    {
    }
#endif

#if DEBUG
    [DoesNotReturn]
    public static void BADCODE(ReadOnlySpan<char> message, [CallerFilePath] ReadOnlySpan<char> filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        debugError(message, filePath, lineNumber);
        badCode();
    }

    [DoesNotReturn]
    public static void BADCODE3(ReadOnlySpan<char> message, ReadOnlySpan<char> message2, int arg, [CallerFilePath] ReadOnlySpan<char> filePath = "", [CallerLineNumber] int lineNumber = 0) => badCode3(message, message2, arg, filePath, lineNumber);

    // Used for an assert that we want to convert into BADCODE to force minopts, or in minopts to force codegen.
    public static void noway_assert([DoesNotReturnIf(false)] bool condition, [CallerArgumentExpression(nameof(condition))] ReadOnlySpan<char> conditionExpression = "", [CallerFilePath] ReadOnlySpan<char> filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        RECORD_NOWAY_ASSERT(conditionExpression);

        if (!condition)                                                                                                   
        {
            noWayAssertBodyConditional(conditionExpression, filePath, lineNumber);
        }
    }

    [DoesNotReturn]
    public static void unreached([CallerFilePath] ReadOnlySpan<char> filePath = "", [CallerLineNumber] int lineNumber = 0) => noWayAssertBody("unreached", filePath, lineNumber);

    [DoesNotReturn]
    public static void NO_WAY(ReadOnlySpan<char> message, [CallerFilePath] ReadOnlySpan<char> filePath = "", [CallerLineNumber] int lineNumber = 0) => noWayAssertBody(message, filePath, lineNumber);

    // Used for fallback stress mode
    [DoesNotReturn]
    public static void NO_WAY_NOASSERT(ReadOnlySpan<char> message) => noWay();

    public static void NOWAY_MSG(ReadOnlySpan<char> message, [CallerFilePath] ReadOnlySpan<char> filePath = "", [CallerLineNumber] int lineNumber = 0) => noWayAssertBodyConditional(message, filePath, lineNumber);

    public static void NOWAY_MSG_FILE_AND_LINE(ReadOnlySpan<char> message, ReadOnlySpan<char> filePath, int lineNumber) => noWayAssertBodyConditional(message, filePath, lineNumber);

    // IMPL_LIMITATION is called when we encounter valid IL that is not
    // supported by our current implementation because of various
    // limitations (that could be removed in the future)
    [DoesNotReturn]
    public static void IMPL_LIMITATION(ReadOnlySpan<char> message, [CallerFilePath] ReadOnlySpan<char> filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        debugError(message, filePath, lineNumber);
        implLimitation();
    }
#else
    [DoesNotReturn]
    public static void BADCODE(ReadOnlySpan<char> message) => badCode();

    [DoesNotReturn]
    public static void BADCODE3(ReadOnlySpan<char> message, ReadOnlySpan<char> message2, int arg) => badCode();

    // Used for an assert that we want to convert into BADCODE to force minopts, or in minopts to force codegen.
    public static void noway_assert([DoesNotReturnIf(false)] bool condition, [CallerArgumentExpression(nameof(condition))] ReadOnlySpan<char> conditionExpression = "")
    {
        RECORD_NOWAY_ASSERT(conditionExpression);

        if (!condition)
        {
            noWayAssertBodyConditional();
        }
    }

    [DoesNotReturn]
    public static void unreached() => noWayAssertBody();

    [DoesNotReturn]
    public static void NO_WAY(ReadOnlySpan<char> message) => noWay();

    public static void NOWAY_MSG(ReadOnlySpan<char> message) => noWayAssertBodyConditional();

    public static void NOWAY_MSG_FILE_AND_LINE(ReadOnlySpan<char> message, ReadOnlySpan<char> filePath, int lineNumber) => noWayAssertBodyConditional();

    // IMPL_LIMITATION is called when we encounter valid IL that is not
    // supported by our current implementation because of various
    // limitations (that could be removed in the future)
    [DoesNotReturn]
    public static void IMPL_LIMITATION(ReadOnlySpan<char> message) => implLimitation();
#endif

    // This can return based on Config flag/Debugger
    public static unsafe void notYetImplemented(ReadOnlySpan<char> message, ReadOnlySpan<char> filePath, int lineNumber)
    {
        var compiler = JitTls.Compiler;

        if ((compiler is null) || (compiler.opts.jitFlags->IsSet(JitFlags.JIT_FLAG_ALT_JIT)))
        {
            NOWAY_MSG_FILE_AND_LINE(message, filePath, lineNumber);
            return;
        }

#if FUNC_INFO_LOGGING
#if DEBUG
        ref var logEnv = ref JitTls.LogEnv;

        if (logEnv.Compiler is not null)
        {
            compiler = logEnv.Compiler;

            if (compiler.verbose)
            {
                jitprintf($"\n\n{compiler.info.compFullName} - NYI ({filePath}:{lineNumber} - {message})\n");
            }
        }

        if (Compiler.compJitFuncInfoFile is StreamWriter compJitFuncInfoFile)
        {
            compJitFuncInfoFile.WriteLine($"{((logEnv.Compiler is null) ? "UNKNOWN" : logEnv.Compiler.info.compFullName)} - NYI ({filePath}:{lineNumber} - {message})");
            compJitFuncInfoFile.Flush();
        }
#else
        if (Compiler.compJitFuncInfoFile is StreamWriter compJitFuncInfoFile)
        {
            compJitFuncInfoFile.WriteLine($"NYI ({filePath}:{lineNumber} - {message})");
            compJitFuncInfoFile.Flush();
        }
#endif
#endif

#if DEBUG
        // Assume we're within a compFunctionTrace boundary, which might not be true.
        compiler.compFunctionTraceEnd(null, 0, true);
#endif

        var altJitAssertOnNyi = JitConfig[ConfigInteger.AltJitAssertOnNYI];

        // 0 means just silently skip, if we are in retail builds, assume ignore
        // 1 means popup the assert (abort=abort, retry=debugger, ignore=skip)
        // 2 means silently don't skip (same as 3 for retail)
        // 3 means popup the assert (abort=abort, retry=debugger, ignore=don't skip)
        if ((altJitAssertOnNyi & 1) != 0)
        {
#if DEBUG
            assertAbort(message, filePath, lineNumber);
#endif
        }

        if ((altJitAssertOnNyi & 2) == 0)
        {
#if MEASURE_FATAL
            s_fatalNyiCount++;
#endif

            fatal(CORJIT_SKIPPED);
        }
    }

    public static void NYIRAW(ReadOnlySpan<char> message, [CallerFilePath] ReadOnlySpan<char> filePath = "", [CallerLineNumber] int lineNumber = 0) => notYetImplemented(message, filePath, lineNumber);

    public static void NYI(ReadOnlySpan<char> message, [CallerFilePath] ReadOnlySpan<char> filePath = "", [CallerLineNumber] int lineNumber = 0) => NYIRAW($"NYI: {message}", filePath, lineNumber);

    public static void NYI_IF(bool condition, ReadOnlySpan<char> message, [CallerFilePath] ReadOnlySpan<char> filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        if (condition)
        {
            NYIRAW($"NYI: {message}", filePath, lineNumber);
        }
    }

    [Conditional("TARGET_AMD64")]
    public static void NYI_AMD64(ReadOnlySpan<char> message, [CallerFilePath] ReadOnlySpan<char> filePath = "", [CallerLineNumber] int lineNumber = 0)  => NYIRAW($"NYI_AMD64: {message}", filePath, lineNumber);

    [Conditional("TARGET_X86")]
    public static void NYI_X86(ReadOnlySpan<char> message, [CallerFilePath] ReadOnlySpan<char> filePath = "", [CallerLineNumber] int lineNumber = 0)    => NYIRAW($"NYI_X86: {message}", filePath, lineNumber);

    [Conditional("TARGET_ARM")]
    public static void NYI_ARM(ReadOnlySpan<char> message, [CallerFilePath] ReadOnlySpan<char> filePath = "", [CallerLineNumber] int lineNumber = 0)    => NYIRAW($"NYI_ARM: {message}", filePath, lineNumber);

    [Conditional("TARGET_ARM64")]
    public static void NYI_ARM64(ReadOnlySpan<char> message, [CallerFilePath] ReadOnlySpan<char> filePath = "", [CallerLineNumber] int lineNumber = 0)  => NYIRAW($"NYI_ARM64: {message}", filePath, lineNumber);

    [Conditional("TARGET_LOONGARCH64")]
    public static void NYI_LOONGARCH64(ReadOnlySpan<char> message, [CallerFilePath] ReadOnlySpan<char> filePath = "", [CallerLineNumber] int lineNumber = 0) => NYIRAW($"NYI_LOONGARCH64: {message}", filePath, lineNumber);

    [Conditional("TARGET_RISCV64")]
    public static void NYI_RISCV64(ReadOnlySpan<char> message, [CallerFilePath] ReadOnlySpan<char> filePath = "", [CallerLineNumber] int lineNumber = 0) => NYIRAW($"NYI_RISCV64: {message}", filePath, lineNumber);

    [Conditional("TARGET_WASM")]
    public static void NYI_WASM(ReadOnlySpan<char> message, [CallerFilePath] ReadOnlySpan<char> filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
#if TARGET_WASM
        if (JitConfig[ConfigInteger.JitWasmNyiToR2RUnsupported] > 0)
        {
            JITDUMP($"NYI_WASM: {message}");
            implReadyToRunUnsupported();
        }
        else
        {
            NYIRAW($"NYI_WASM: {message}", filePath, lineNumber);
        }
#endif
    }

    public static void BreakIfDebuggerPresent() => Debugger.Break();

#if DEBUG
    public static int getBreakOnBadCode() => JitConfig[ConfigInteger.JitBreakOnBadCode];
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ShouldThrowOnNoway() => (JitTls.Compiler is not Compiler compiler) || compiler.compShouldThrowOnNoway;

    private static void noWayAssertAbortHelper(ReadOnlySpan<char> message, ReadOnlySpan<char> filePath, int lineNumber)
    {
        if (JitConfig[ConfigInteger.JitEnableNoWayAssert] != 0)
        {
            // Show the assert UI.
            assertAbort(message, filePath, lineNumber);
        }
    }
}
