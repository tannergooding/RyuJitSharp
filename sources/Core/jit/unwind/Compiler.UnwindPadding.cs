// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    internal void RecordUnwindPadding(uint codeSizeInBytes)
    {
        if (Configuration.TARGET_ARM)
        {
            unwindNop(codeSizeInBytes);
            return;
        }

        RequireUnwindEETarget();
        var unwindInfo = funCurrentFunc().GetUnwindInfo();
#if DEBUG
        if (verbose)
        {
            if (Configuration.TARGET_RISCV64)
            {
                jitprintf("unwindNop: adding NOP\n");
            }
            else
            {
                logf("unwindNop: adding NOP\n");
            }
        }

        unwindInfo.uwiAddingNOP = true;
#endif
        unwindInfo.AddCode(0xE3);
#if DEBUG
        unwindInfo.uwiAddingNOP = false;
#endif
    }

    public void unwindNop(uint codeSizeInBytes)
    {
        if (!Configuration.TARGET_ARM)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "Sized unwind padding requires ARM32.");
        }

        if (generateCFIUnwindCodes())
        {
            return;
        }

        var unwindInfo = funCurrentFunc().GetUnwindInfo();
#if DEBUG
        if (verbose)
        {
            logf($"unwindNop: adding NOP for {codeSizeInBytes} byte instruction\n");
        }

        unwindInfo.uwiAddingNOP = true;
#endif
        if (codeSizeInBytes == 2)
        {
            unwindInfo.AddCode(0xFB);
        }
        else
        {
            noway_assert(codeSizeInBytes == 4);
            unwindInfo.AddCode(0xFC);
        }

#if DEBUG
        unwindInfo.uwiAddingNOP = false;
#endif
    }
}
