// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
#if DEBUG
    private static uint s_totalCseCount;

    internal bool optConfigDisableCSE2()
    {
        var jitNoCSE2 = unchecked((uint)JitConfig.JitNoCSE2);
        s_totalCseCount = unchecked(s_totalCseCount + 1);

        if (jitNoCSE2 > 0)
        {
            if ((jitNoCSE2 & 0x0F000000) == 0x0F000000)
            {
                var totalCseMask = s_totalCseCount & 0xFFF;
                var bitsZero = (jitNoCSE2 >> 12) & 0xFFF;
                var bitsOne = jitNoCSE2 & 0xFFF;
                if (((totalCseMask & bitsOne) == bitsOne) &&
                    ((~totalCseMask & bitsZero) == bitsZero))
                {
                    JITDUMP(" Disabled by jitNoCSE2 Ones/Zeros mask\n");
                    return true;
                }
            }
            else if ((jitNoCSE2 & 0x0F000000) == 0x0E000000)
            {
                var totalCseMask = s_totalCseCount & 0xFFF;
                var disableMask = jitNoCSE2 & 0xFFF;
                disableMask >>= (int)(totalCseMask % 12);
                if ((disableMask & 1) != 0)
                {
                    JITDUMP(" Disabled by jitNoCSE2 rotating disable mask\n");
                    return true;
                }
            }
            else if (jitNoCSE2 <= s_totalCseCount)
            {
                JITDUMP($" Disabled by jitNoCSE2 {jitNoCSE2} > totalCSEcount {s_totalCseCount}\n");
                return true;
            }
        }

        return false;
    }
#endif
}
