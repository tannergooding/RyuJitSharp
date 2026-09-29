// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86
namespace RyuJitSharp;

public partial class Emitter
{
    private void emitAdjustStackDepth(instruction ins, nint val)
    {
        if (emitCntStackDepth == 0)
        {
            return;
        }

        if (ins == INS_sub)
        {
            var current = unchecked((uint)emitCurStackLvl);
            var operand = unchecked((int)val);
            var next = (ulong)current + unchecked((uint)operand);
            var overflow = operand < 0 || next > uint.MaxValue;
            noway_assert(!overflow);

            emitCurStackLvl = overflow ? 0 : unchecked((int)next);

            if (unchecked((uint)emitMaxStackDepth) < unchecked((uint)emitCurStackLvl))
            {
                JITDUMP($"Upping emitMaxStackDepth from {emitMaxStackDepth} to {emitCurStackLvl}\n");
                emitMaxStackDepth = emitCurStackLvl;
            }
        }
        else if (ins == INS_add)
        {
            var current = unchecked((uint)emitCurStackLvl);
            var operand = unchecked((int)val);
            var overflow = operand < 0 || current < unchecked((uint)operand);
            noway_assert(!overflow);

            emitCurStackLvl = overflow ? 0 : unchecked((int)(current - operand));
        }
    }
}
#endif
