// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if LATE_DISASM
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace RyuJitSharp;

public partial struct Disassembler
{
#if USE_COREDISTOOLS
    // B527: retain InitCoredistoolsLibrary until its loader/search and failure diagnostics are represented.
    private static bool InitCoredistoolsLibrary()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Late disassembly requires the native CoreDisTools callback ABI.");
    }

    private static nuint NewCoredistoolsDisasm(CoreDisTarget architecture)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "NewDisasm requires PrintControl's variadic native logger ABI.");
    }
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
