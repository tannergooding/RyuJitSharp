// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public partial class Emitter
{
    private unsafe byte* emitOutputShortBranch(byte* dst, instruction ins, insFormat format, nint distance,
        instrDescJmp? jump)
    {
        if (!Configuration.TARGET_ARM)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Thumb branch output is only supported on ARM32.");
            return default!;
        }

        var code = emitInsCodeArm32(ins, format);

        if (format is IF_ARM32_T1_K)
        {
            assert((distance & 1) == 0);
            assert(distance >= -256);
            assert(distance <= 254);

            if (distance < 0)
            {
                code |= 1u << 7;
            }
            code |= unchecked((uint)(distance >> 1)) & 0x7Fu;
        }
        else if (format is IF_ARM32_T1_M)
        {
            assert((distance & 1) == 0);
            assert(distance >= -2048);
            assert(distance <= 2046);

            if (distance < 0)
            {
                code |= 1u << 10;
            }
            code |= unchecked((uint)(distance >> 1)) & 0x3FFu;
        }
        else if (format is IF_ARM32_T1_I)
        {
            var jumpNode = jump;
            if (jumpNode is null)
            {
                FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR,
                    "Thumb CBZ/CBNZ output requires a jump descriptor.");
                return default!;
            }
            assert(ins is INS_cbz or INS_cbnz);
            assert((distance & 1) == 0);
            assert(distance >= 0);
            assert(distance <= 126);

            code |= unchecked((uint)distance << 3) & 0x0200u;
            code |= unchecked((uint)distance << 2) & 0x00F8u;
            code |= (uint)jumpNode.idReg1() & 0x0007u;
        }
        else
        {
            FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, "Unknown Thumb short-branch format.");
            return default!;
        }

        dst += emitOutput_Thumb1Instr(dst, code);

        return dst;
    }
}
