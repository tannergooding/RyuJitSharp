// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;

#if DEBUG
using System.Globalization;
#endif

namespace RyuJitSharp;

public partial class Globals
{
    /// <summary>Like printf/logf, but only outputs to jitstdout -- skips call back into EE.</summary>
    public static void jitprintf(string message)
    {
        var jitstdout = Globals.jitstdout();

        if (message.Length == 0)
        {
            // 0-length string means flush
            jitstdout.Flush();
        }
        else
        {
            jitstdout.Write(message);
        }
    }

#if DEBUG
    public static bool vlogf(int level, string message)
    {
        // TODO: This can't be implemented without varargs support
        // return JitTls.GetLogEnv().jitInfo->logMsg(level, message);
        return false;
    }

    public static void vflogf(StreamWriter stream, string message)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(message);

        if (message.Length == 0)
        {
            // 0-length string means flush
            stream.Flush();
        }
        else if (JitConfig[ConfigInteger.JitDumpToDebugger] != 0)
        {
            Debug.Write(message);
            stream.Write(message);
        }
        else
        {
            stream.Write(message);
        }
    }

    private static bool s_logToEEfailed;

    public static void logf(string message)
    {
        // We remember when the EE failed to log, because vlogf()
        // is very slow in a checked build.
        //
        // If it fails to log an LL_INFO1000 message once
        // it will always fail when logging an LL_INFO1000 message.

        if (!s_logToEEfailed)
        {
            if (!vlogf(LL_INFO1000, message))
            {
                s_logToEEfailed = true;
            }
        }

        if (s_logToEEfailed)
        {
            // if the EE refuses to log it, we try to send it to stdout
            vflogf(jitstdout(), message);
        }
#if false  // Enable this only when you need it
        else
        {
            // The EE just successfully logged our message
            var breakOnDumpToken = s_fJitBreakOnDumpToken.val(CLRConfig.INTERNAL_BreakOnDumpToken);

            if ((breakOnDumpToken != 0xFFFFFFFF) && (s_forbidEntry == 0))
            {
                s_forbidEntry = 1;

                // Use value of 0 to get the dump
                if (s_currentLine == breakOnDumpToken)
                {
                    assert(false, "Dump token reached");
                }

                jitprintf($"(Token=0x{s_currentLine:X})");
                s_forbidEntry = 0;
            }
        }
#endif
    }

    public static void flogf(StreamWriter stream, string message) => vflogf(stream, message);

    public static void gcDump_logf(string message) => logf(message);

    public static void logf(int level, string message) => vlogf(level, message);
#endif

    [Conditional("DEBUG")]
    public static unsafe void assertAbort(ReadOnlySpan<char> reason, ReadOnlySpan<char> filePath, int lineNumber)
    {
#if DEBUG
        var message = reason;

        ref var logEnv = ref JitTls.LogEnv;
        var phaseName = "unknown phase";

        var isStartupAssert = Unsafe.IsNullRef(ref logEnv);
        var compiler = !isStartupAssert ? logEnv.Compiler : null;

        if (compiler is not null)
        {
            phaseName = compiler.mostRecentlyActivePhase.Name;
            message = $"Assertion failed '{reason}' in '{compiler.info.compFullName}' during '{phaseName}' (IL size {compiler.info.compILCodeSize}; hash 0x{compiler.info.compMethodHash():X8)}; {compiler.compGetTieringName(wantShortName: true)})\n";
        }

#if FUNC_INFO_LOGGING
        if (Compiler.compJitFuncInfoFile is StreamWriter compJitFuncInfoFile)
        {
            compJitFuncInfoFile.WriteLine($"{((compiler is null) ? "UNKNOWN" : compiler.info.compFullName)} - Assertion failed ({filePath}:{lineNumber} - {reason}) during {phaseName}");
        }
#endif // FUNC_INFO_LOGGING

        using (var utf8FilePath = new MarshaledUtf8String(filePath))
        using (var utf8Message = new MarshaledUtf8String(message))
        {
            fixed (byte* pUtf8FilePath = utf8FilePath)
            fixed (byte* pUtf8Message = utf8Message)
            {
                if (isStartupAssert)
                {
                    Debug.Fail($"Assertion failed ({filePath}:{lineNumber} - {reason}) during startup");
                }
                else if (logEnv.JitInfo->doAssert(pUtf8FilePath, lineNumber, pUtf8Message) != 0)
                {
                    Debugger.Break();
                }
            }
        }

        compiler = JitTls.Compiler;

        if ((compiler is not null) && compiler.opts.jitFlags->IsSet(JitFlags.JIT_FLAG_ALT_JIT))
        {
            // If we hit an assert, and we got here, it's either because the user hit "ignore" on the
            // dialog pop-up, or they set DOTNET_ContinueOnAssert=1 to not emit a pop-up, but just continue.
            // If we're an altjit, we have two options: (1) silently continue, as a normal JIT would, probably
            // leading to additional asserts, or (2) tell the VM that the AltJit wants to skip this function,
            // thus falling back to the fallback JIT. Setting DOTNET_AltJitSkipOnAssert=1 chooses this "skip"
            // to the fallback JIT behavior. This is useful when doing ASM diffs, where we only want to see
            // the first assert for any function, but we don't want to kill the whole process on the
            // first assert (which would happen if you used DOTNET_NoGuiOnAssert=1 for example).
            if (JitConfig[ConfigInteger.AltJitSkipOnAssert] != 0)
            {
                fatal(CORJIT_SKIPPED);
            }
        }
#endif
    }

    [Conditional("DEBUG")]
    public static void assert([DoesNotReturnIf(false)] bool condition, [CallerArgumentExpression(nameof(condition))] string conditionExpression = "", [CallerFilePath] ReadOnlySpan<char> filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        if (!condition)
        {
            assertAbort(conditionExpression, filePath, lineNumber);
        }
    }

    public static StreamWriter jitstdout()
    {
        var jitstdout = s_jitstdout;
        jitstdout ??= jitstdoutInit();
        return jitstdout;
    }
}
