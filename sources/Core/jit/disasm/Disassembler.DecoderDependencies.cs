// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if LATE_DISASM
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace RyuJitSharp;

public partial struct Disassembler
{
#if USE_COREDISTOOLS
    // Match PAL errors for missing process paths and Unix dlopen/dlsym failures.
    private const uint ErrorInternalError = 0x0000054F;
    private const uint ErrorModuleNotFound = 0x0000007E;
    private const uint ErrorProcedureNotFound = 0x0000007F;

    private static int s_disCoreDisToolsLibraryInitializing;
    private static bool s_disCoreDisToolsLibraryInitialized;
    private static bool s_disCoreDisToolsLibraryLoadSuccessful;
    private static nint s_disCoreDisToolsLibrary;

    private static unsafe bool InitCoredistoolsLibrary()
    {
        while (Interlocked.CompareExchange(ref s_disCoreDisToolsLibraryInitializing, 1, 0) != 0)
        {
        }

        try
        {
            if (s_disCoreDisToolsLibraryInitialized)
            {
                return s_disCoreDisToolsLibraryLoadSuccessful;
            }

            s_disCoreDisToolsLibraryInitialized = true;
            s_disCoreDisToolsLibraryLoadSuccessful = false;

            var libraryName = OperatingSystem.IsWindows()
                ? "coredistools.dll"
                : OperatingSystem.IsMacOS() ? "libcoredistools.dylib" : "libcoredistools.so";
            var libraryPath = libraryName;

            if (!OperatingSystem.IsWindows())
            {
                var processPath = Environment.ProcessPath;
                var processDirectory = processPath is null ? null : Path.GetDirectoryName(processPath);
                if (processDirectory is null)
                {
                    jitprintf($"GetModuleFileNameW failed (0x{ErrorInternalError:x8})");
                    return false;
                }

                libraryPath = Path.Combine(processDirectory, libraryName);
            }

            uint errorCode;
            if (OperatingSystem.IsWindows())
            {
                s_disCoreDisToolsLibrary = LoadLibraryExW(libraryPath, 0, 0);
                errorCode = unchecked((uint)Marshal.GetLastPInvokeError());
            }
            else if (!NativeLibrary.TryLoad(libraryPath, out s_disCoreDisToolsLibrary))
            {
                errorCode = ErrorModuleNotFound;
            }
            else
            {
                errorCode = 0;
            }

            if (s_disCoreDisToolsLibrary == 0)
            {
                jitprintf($"LoadLibrary({libraryName}) failed (0x{errorCode:x8})");
                return false;
            }

            if (!TryGetCoredistoolsExport("InitBufferedDisasm", out var initBufferedDisasm, out errorCode))
            {
                jitprintf($"GetProcAddress 'InitBufferedDisasm' failed (0x{errorCode:x8})");
                return false;
            }
            s_PtrInitBufferedDisasm = (delegate* unmanaged[Cdecl]<CoreDisTarget, nuint>)initBufferedDisasm;

            if (!TryGetCoredistoolsExport("DumpInstruction", out var dumpInstruction, out errorCode))
            {
                jitprintf($"GetProcAddress 'DumpInstruction' failed (0x{errorCode:x8})");
                return false;
            }
            s_PtrDumpInstruction = (delegate* unmanaged[Cdecl]<nuint, byte*, byte*, nuint, nuint>)dumpInstruction;

            if (!TryGetCoredistoolsExport("GetOutputBuffer", out var getOutputBuffer, out errorCode))
            {
                jitprintf($"GetProcAddress 'GetOutputBuffer' failed (0x{errorCode:x8})");
                return false;
            }
            s_PtrGetOutputBuffer = (delegate* unmanaged[Cdecl]<byte*>)getOutputBuffer;

            if (!TryGetCoredistoolsExport("ClearOutputBuffer", out var clearOutputBuffer, out errorCode))
            {
                jitprintf($"GetProcAddress 'ClearOutputBuffer' failed (0x{errorCode:x8})");
                return false;
            }
            s_PtrClearOutputBuffer = (delegate* unmanaged[Cdecl]<void>)clearOutputBuffer;

            if (!TryGetCoredistoolsExport("FinishDisasm", out var finishDisasm, out errorCode))
            {
                jitprintf($"GetProcAddress 'FinishDisasm' failed (0x{errorCode:x8})");
                return false;
            }
            s_PtrFinishDisasm = (delegate* unmanaged[Cdecl]<nuint, void>)finishDisasm;

            s_disCoreDisToolsLibraryLoadSuccessful = true;
            return true;
        }
        finally
        {
            _ = Interlocked.Exchange(ref s_disCoreDisToolsLibraryInitializing, 0);
        }
    }

    private static unsafe bool TryGetCoredistoolsExport(string name, out nint address, out uint errorCode)
    {
        if (OperatingSystem.IsWindows())
        {
            var symbolNameBytes = Encoding.ASCII.GetBytes(name + '\0');
            fixed (byte* symbolName = symbolNameBytes)
            {
                address = GetProcAddress(s_disCoreDisToolsLibrary, (nint)symbolName);
            }
            errorCode = address == 0 ? unchecked((uint)Marshal.GetLastPInvokeError()) : 0;
            return address != 0;
        }

        if (NativeLibrary.TryGetExport(s_disCoreDisToolsLibrary, name, out address))
        {
            errorCode = 0;
            return true;
        }

        errorCode = ErrorProcedureNotFound;
        return false;
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", EntryPoint = "LoadLibraryExW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint LoadLibraryExW([MarshalAs(UnmanagedType.LPWStr)] string libraryName, nint file, uint flags);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", EntryPoint = "GetProcAddress", ExactSpelling = true, SetLastError = true)]
    private static extern nint GetProcAddress(nint library, nint symbolName);
#endif

#if DEBUG
    internal static unsafe StreamWriter? OpenLateDisassemblyFile(byte* fileName)
    {
        var path = Marshal.PtrToStringUTF8((nint)fileName);
        if (path is null)
        {
            return null;
        }

        // The native caller uses "a+" and falls back to jitstdout() when opening fails.
        FileStream? file = null;
        try
        {
            file = Compiler.OpenJitOutputFile(path);
            if (file is null)
            {
                return null;
            }

            var writer = new StreamWriter(file, new UTF8Encoding(false));
            file = null;
            return writer;
        }
        finally
        {
            file?.Dispose();
        }
    }
#endif

    // Decoder callbacks update this struct while emitting the buffer.
#pragma warning disable IDE0251
    private unsafe void DisasmBuffer(StreamWriter output, bool printit)
    {
#if USE_COREDISTOOLS
        DisasmBufferCoredistools(output, printit);
#elif USE_MSVCDIS
        DisasmBufferMsvc(output, printit);
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Late disassembly requires the native decoder and symbol callbacks.");
#endif
    }
#pragma warning restore IDE0251
}
#endif
