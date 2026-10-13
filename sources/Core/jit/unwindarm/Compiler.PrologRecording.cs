// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    private void unwindPushPopMaskInt(regMaskTP maskInt, bool useOpsize16)
    {
        assert((maskInt & RBM_ALLFLOAT).IsEmpty);

        var unwindInfo = funCurrentFunc().GetUnwindInfo();

        if (useOpsize16)
        {
            assert((maskInt & ~(RBM_R0 | RBM_R1 | RBM_R2 | RBM_R3 | RBM_R4 | RBM_R5 | RBM_R6 | RBM_R7 | RBM_LR))
                .IsEmpty);

            var shortFormat = false;
            byte value = 0;

            if ((maskInt & (RBM_R0 | RBM_R1 | RBM_R2 | RBM_R3)).IsEmpty)
            {
                var matchMask = maskInt & (RBM_R4 | RBM_R5 | RBM_R6 | RBM_R7);
                var valueMask = RBM_R4;

                while (value < 4)
                {
                    if (matchMask == valueMask)
                    {
                        shortFormat = true;
                        break;
                    }

                    valueMask <<= 1;
                    valueMask |= RBM_R4;
                    value++;
                }
            }

            if (shortFormat)
            {
                // D0-D7 encode pop {r4-rX, lr} for X=4-7.
                var lrBits = unchecked((byte)(maskInt >> 12));
                unwindInfo.AddCode(unchecked((byte)(0xD0 | (lrBits & 0x4) | value)));
            }
            else
            {
                // EC-ED encode pop {r0-r7, lr}.
                var lrBit = unchecked((byte)(maskInt >> 14));
                unwindInfo.AddCode(unchecked((byte)(0xEC | (lrBit & 0x1))), unchecked((byte)maskInt));
            }
        }
        else
        {
            assert((maskInt & ~(RBM_R0 | RBM_R1 | RBM_R2 | RBM_R3 | RBM_R4 | RBM_R5 | RBM_R6 | RBM_R7 | RBM_R8 |
                RBM_R9 | RBM_R10 | RBM_R11 | RBM_R12 | RBM_LR)).IsEmpty);

            var shortFormat = false;
            byte value = 0;

            if (((maskInt & (RBM_R0 | RBM_R1 | RBM_R2 | RBM_R3)).IsEmpty) &&
                ((maskInt & (RBM_R4 | RBM_R5 | RBM_R6 | RBM_R7 | RBM_R8)) ==
                    (RBM_R4 | RBM_R5 | RBM_R6 | RBM_R7 | RBM_R8)))
            {
                var matchMask = maskInt & (RBM_R4 | RBM_R5 | RBM_R6 | RBM_R7 | RBM_R8 | RBM_R9 | RBM_R10 | RBM_R11);
                var valueMask = RBM_R4 | RBM_R5 | RBM_R6 | RBM_R7 | RBM_R8;

                while (value < 4)
                {
                    if (matchMask == valueMask)
                    {
                        shortFormat = true;
                        break;
                    }

                    valueMask <<= 1;
                    valueMask |= RBM_R4;
                    value++;
                }
            }

            if (shortFormat)
            {
                // D8-DF encode pop {r4-rX, lr} for X=8-11.
                var lrBits = unchecked((byte)(maskInt >> 12));
                unwindInfo.AddCode(unchecked((byte)(0xD8 | (lrBits & 0x4) | value)));
            }
            else
            {
                // 80-BF encode pop {r0-r12, lr}.
                var firstByte = unchecked((byte)(0x80 |
                    ((unchecked((byte)(maskInt >> 8))) & 0x1F) |
                    ((unchecked((byte)(maskInt >> 9))) & 0x20)));
                unwindInfo.AddCode(firstByte, unchecked((byte)maskInt));
            }
        }
    }

    private void unwindPushPopMaskFloat(regMaskTP maskFloat)
    {
        assert((maskFloat & ~RBM_ALLFLOAT).IsEmpty);
        if (maskFloat.IsEmpty)
        {
            return;
        }

        var value = 0;
        var valueMask = RBM_F16 | RBM_F17;

        while (maskFloat != valueMask)
        {
            valueMask <<= 2;
            valueMask |= RBM_F16 | RBM_F17;
            value++;

            if (value == 8)
            {
                noway_assert(false, "!\"Illegal maskFloat\"");
            }
        }

        assert(value <= 7);
        funCurrentFunc().GetUnwindInfo().AddCode(unchecked((byte)(0xE0 | value)));
    }

    public void unwindPushMaskInt(regMaskTP maskInt)
    {
        assert((maskInt & ~(RBM_R0 | RBM_R1 | RBM_R2 | RBM_R3 | RBM_R4 | RBM_R5 | RBM_R6 | RBM_R7 | RBM_R8 |
            RBM_R9 | RBM_R10 | RBM_R11 | RBM_R12 | RBM_LR)).IsEmpty);

        if (generateCFIUnwindCodes())
        {
            unwindPushPopMaskCFI(maskInt, false);
            return;
        }

        var useOpsize16 = (maskInt & (RBM_LOW_REGS | RBM_LR)) == maskInt;
        unwindPushPopMaskInt(maskInt, useOpsize16);
    }

    public void unwindPushMaskFloat(regMaskTP maskFloat)
    {
        assert((maskFloat & RBM_ALLFLOAT) == maskFloat);

        if (generateCFIUnwindCodes())
        {
            unwindPushPopMaskCFI(maskFloat, true);
            return;
        }

        unwindPushPopMaskFloat(maskFloat);
    }

    public void unwindPopMaskInt(regMaskTP maskInt)
    {
        if (generateCFIUnwindCodes())
        {
            return;
        }

        assert((maskInt & ~(RBM_R0 | RBM_R1 | RBM_R2 | RBM_R3 | RBM_R4 | RBM_R5 | RBM_R6 | RBM_R7 | RBM_R8 |
            RBM_R9 | RBM_R10 | RBM_R11 | RBM_R12 | RBM_LR | RBM_PC)).IsEmpty);

        var useOpsize16 = (maskInt & (RBM_LOW_REGS | RBM_PC)) == maskInt;
        if ((maskInt & RBM_PC).IsNonEmpty)
        {
            maskInt = (maskInt & ~RBM_PC) | RBM_LR;
        }

        unwindPushPopMaskInt(maskInt, useOpsize16);
    }

    public void unwindPopMaskFloat(regMaskTP maskFloat)
    {
        if (generateCFIUnwindCodes())
        {
            return;
        }

        assert((maskFloat & RBM_ALLFLOAT) == maskFloat);
        unwindPushPopMaskFloat(maskFloat);
    }

    public void unwindAllocStackArm(uint size)
    {
        if (generateCFIUnwindCodes())
        {
            if (GetEmitter().emitGeneratingPrologOrFuncletProlog())
            {
                unwindAllocStackCFI(size);
            }
            return;
        }

        var unwindInfo = funCurrentFunc().GetUnwindInfo();
        assert(size % 4 == 0);
        size /= 4;

        if (size <= 0x7F)
        {
            unwindInfo.AddCode(unchecked((byte)size));
        }
        else if (size <= 0x3FF)
        {
            unwindInfo.AddCode(unchecked((byte)(0xE8 | (byte)(size >> 8))), unchecked((byte)size));
        }
        else if (size <= 0xFFFF)
        {
            var instructionSizeInBytes = unwindInfo.GetInstructionSize();
            var opcode = instructionSizeInBytes == 2 ? (byte)0xF7 : (byte)0xF9;
            unwindInfo.AddCode(opcode, unchecked((byte)(size >> 8)), unchecked((byte)size));
        }
        else
        {
            var instructionSizeInBytes = unwindInfo.GetInstructionSize();
            var opcode = instructionSizeInBytes == 2 ? (byte)0xF8 : (byte)0xFA;
            unwindInfo.AddCode(opcode, unchecked((byte)(size >> 16)), unchecked((byte)(size >> 8)), unchecked((byte)size));
        }
    }

    public void unwindSetFrameRegArm(regNumber reg, uint offset)
    {
        if (generateCFIUnwindCodes())
        {
            if (GetEmitter().emitGeneratingPrologOrFuncletProlog())
            {
                unwindSetFrameRegCFI(reg, offset);
            }
            return;
        }

        var unwindInfo = funCurrentFunc().GetUnwindInfo();
        assert(offset == 0);
        assert((int)reg >= 0 && (int)reg <= 15);
        unwindInfo.AddCode(unchecked((byte)(0xC0 + (int)reg)));
    }

    public void unwindSaveRegArm(regNumber reg, uint offset)
    {
        unreached();
    }

    public void unwindBranch16()
    {
        if (generateCFIUnwindCodes())
        {
            return;
        }

        // The epilog end code is currently followed by an extra end byte.
        funCurrentFunc().GetUnwindInfo().AddCode(0xFD);
    }
}
