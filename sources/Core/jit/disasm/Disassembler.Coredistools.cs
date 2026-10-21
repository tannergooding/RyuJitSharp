// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public partial struct Disassembler
{
    // Values from the pinned CoreDisTools TargetArch ABI, not the JIT's target enum.
    internal enum CoreDisTarget
    {
        Host,
        X86,
        X64,
        Thumb,
        Arm64,
        LoongArch64,
        RiscV64,
        Wasm32,
    }

    internal static unsafe delegate* unmanaged[Cdecl]<CoreDisTarget, nuint> s_PtrInitBufferedDisasm;
    internal static unsafe delegate* unmanaged[Cdecl]<nuint, byte*, byte*, nuint, nuint> s_PtrDumpInstruction;
    internal static unsafe delegate* unmanaged[Cdecl]<byte*> s_PtrGetOutputBuffer;
    internal static unsafe delegate* unmanaged[Cdecl]<void> s_PtrClearOutputBuffer;
    internal static unsafe delegate* unmanaged[Cdecl]<nuint, void> s_PtrFinishDisasm;

    private unsafe bool InitCoredistoolsDisasm()
    {
        if (!InitCoredistoolsLibrary())
        {
            return false;
        }

        var architecture = GetCoreDisTargetForCurrentTarget();
        _corDisasm = s_PtrInitBufferedDisasm(architecture);
        if (_corDisasm == 0)
        {
#pragma warning disable CA2201 // CoreDisTools returns null when it cannot create the disassembler.
            throw new OutOfMemoryException();
#pragma warning restore CA2201
        }

        return true;
    }

    internal static CoreDisTarget GetCoreDisTargetForCurrentTarget()
    {
        if (Configuration.TARGET_ARM64)
        {
            return CoreDisTarget.Arm64;
        }

        if (Configuration.TARGET_ARM)
        {
            return CoreDisTarget.Thumb;
        }

        if (Configuration.TARGET_X86)
        {
            return CoreDisTarget.X86;
        }

        if (Configuration.TARGET_AMD64)
        {
            return CoreDisTarget.X64;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            return CoreDisTarget.LoongArch64;
        }

        if (Configuration.TARGET_RISCV64)
        {
            return CoreDisTarget.RiscV64;
        }

        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "CoreDisTools does not support this JIT target.");
        return default!;
    }

    private unsafe void DoneCoredistoolsDisasm()
    {
        if (_corDisasm == 0)
        {
            return;
        }

        if (s_PtrFinishDisasm == null)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "CoreDisTools decoder cleanup requires the FinishDisasm export.");
            return;
        }

        s_PtrFinishDisasm(_corDisasm);
        _corDisasm = 0;
    }

    private readonly unsafe void DisasmBufferCoredistools(StreamWriter output, bool printit)
    {
        uint errorCount = 0;

        DisasmRegionCoredistools(output, 0, _hotCodeBlock, _hotCodeSize, ref errorCount);
        DisasmRegionCoredistools(output, _hotCodeSize, _coldCodeBlock, _coldCodeSize, ref errorCount);
    }

    private readonly unsafe void DisasmRegionCoredistools(StreamWriter output, nuint executionAddress, nuint blockAddress,
        nuint blockSize, ref uint errorCount)
    {
        const uint maxErrorCount = 50;
        var address = (byte*)executionAddress;
        var codeBytes = (byte*)blockAddress;
        var codeSizeBytes = blockSize;

        while (codeSizeBytes > 0)
        {
            if (s_PtrDumpInstruction == null)
            {
                FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "CoreDisTools decoding requires the DumpInstruction export.");
                return;
            }

            var instrLen = s_PtrDumpInstruction(_corDisasm, address, codeBytes, codeSizeBytes);
            output.Write(GetCoredistoolsOutput());
            if (instrLen == 0)
            {
                errorCount++;
                if (Configuration.TARGET_ARM64)
                {
                    if (codeSizeBytes >= 4)
                    {
                        // Native uses %x for the address, intentionally retaining only its low 32 bits.
                        output.Write(
                            $"{unchecked((uint)(nuint)address):x}: {codeBytes[0]:x2} {codeBytes[1]:x2} {codeBytes[2]:x2} {codeBytes[3]:x2}\n");
                        address += 4;
                        codeBytes += 4;
                        codeSizeBytes -= 4;
                    }

                    if (errorCount < maxErrorCount)
                    {
                        continue;
                    }

                    output.Write("Too many failures\n");
                }
                break;
            }

            assert(instrLen <= codeSizeBytes);
            address += instrLen;
            codeBytes += instrLen;
            codeSizeBytes -= instrLen;
        }
    }

    private static unsafe string GetCoredistoolsOutput()
    {
        var outputBuffer = s_PtrGetOutputBuffer();
        try
        {
            if (outputBuffer == null)
            {
                FatalJitException.ThrowFatalJitException(
                    CORJIT_SKIPPED, "CoreDisTools returned a null buffered output pointer.");
                return default!;
            }

            var output = Marshal.PtrToStringUTF8((nint)outputBuffer);
            if (output is null)
            {
                FatalJitException.ThrowFatalJitException(
                    CORJIT_SKIPPED, "CoreDisTools returned invalid buffered output.");
                return default!;
            }

            return output;
        }
        finally
        {
            s_PtrClearOutputBuffer();
        }
    }
}
