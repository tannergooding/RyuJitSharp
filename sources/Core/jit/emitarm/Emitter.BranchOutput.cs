// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public partial class Emitter
{
    private unsafe byte* emitOutputShortBranch(byte* dst, instruction ins, insFormat format, nint distance,
        instrDescJmp? jump)
    {
        var code = emitInsCode(ins, format);

        if (format is IF_T1_K)
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
        else if (format is IF_T1_M)
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
        else if (format is IF_T1_I)
        {
            var jumpNode = jump ?? throw new FatalJitException(CORJIT_INTERNALERROR,
                "Thumb CBZ/CBNZ output requires a jump descriptor.");
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
            throw new FatalJitException(CORJIT_INTERNALERROR, "Unknown Thumb short-branch format.");
        }

        dst += emitOutput_Thumb1Instr(dst, code);

        return dst;
    }

#if FEATURE_ITINSTRUCTION
    // Keep this deprecated encoder only for configurations that still request IT instructions.
    private unsafe byte* emitOutputIT(byte* dst, instruction ins, insFormat format, uint conditionCode)
    {
        var code = emitInsCode(ins, format);
        code |= conditionCode << 4;
        var firstConditionBit = conditionCode & 1;
        var mask = code & 0x0F;
        var bit = 0x08u;

        while ((mask & (bit - 1)) != 0)
        {
            if ((firstConditionBit == 1) ^ ((bit & mask) != 0))
            {
                code |= bit;
            }
            else
            {
                code &= ~bit;
            }
            bit >>= 1;
        }

        dst += emitOutput_Thumb1Instr(dst, code);

        return dst;
    }
#endif
}
#endif
