// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Emitter
{
    public unsafe void emitDispInsHex(instrDesc id, byte* code, nuint size)
    {
        if (Configuration.TARGET_XARCH)
        {
            emitDispInsHexXarch(id, code, size);
            return;
        }
        if (Configuration.TARGET_ARM64)
        {
            emitDispInsHexArm64(id, code, size);
            return;
        }
        if (Configuration.TARGET_ARM)
        {
            emitDispInsHexArm32(id, code, size);
            return;
        }

        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Instruction byte display is not implemented for the active target.");
        return;
    }
}
