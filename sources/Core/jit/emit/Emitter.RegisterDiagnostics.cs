// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitDispRegSet(regMaskTP registers)
    {
#if HAS_FIXED_REGISTER_SET
        jitprintf(" {");
        var separator = "";
        for (var reg = REG_FIRST; reg < ACTUAL_REG_COUNT; reg++)
        {
            if (registers.IsEmpty)
            {
                break;
            }
            var mask = regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
            if ((registers & mask).IsEmpty)
            {
                continue;
            }
            registers ^= mask;
            jitprintf(separator);
            separator = " ";
            jitprintf(emitRegName(reg));
        }
        jitprintf("}");
#endif
    }
}
