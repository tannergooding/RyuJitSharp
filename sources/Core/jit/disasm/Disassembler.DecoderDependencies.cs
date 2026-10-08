// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if LATE_DISASM
using System.IO;

namespace RyuJitSharp;

public partial struct Disassembler
{
#if USE_COREDISTOOLS
    // B527: NewDisasm requires PrintControl's variadic native logger ABI.
    private bool InitCoredistoolsDisasm()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Late disassembly requires the native CoreDisTools callback ABI.");
    }

    private readonly void DoneCoredistoolsDisasm()
    {
        if (_corDisasm == 0)
        {
            return;
        }

        throw new FatalJitException(CORJIT_SKIPPED, "CoreDisTools decoder cleanup requires the native FinishDisasm lifecycle.");
    }
#endif

#if DEBUG
    // The residual utils.cpp owns fopen_utf8, including its append/update and failure contracts.
    private static unsafe StreamWriter? OpenLateDisassemblyFile(byte* fileName)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Late disassembly output requires the native fopen_utf8 append/update contract.");
    }
#endif

    // B527: retain both native backend bodies until decoding and callback output are represented.
    private void DisasmBuffer(StreamWriter output, bool printit)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Late disassembly requires the native decoder and symbol callbacks.");
    }
}
#endif
