// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG && TARGET_AMD64 && WINDOWS_AMD64_ABI
using System;
using System.Buffers.Binary;

namespace RyuJitSharp;

public partial class Compiler
{
    private static void DumpUnwindInfo(bool isHotCode, uint startOffset, uint endOffset, byte[]? storage, uint slot)
    {
        jitprintf($"Unwind Info{(isHotCode ? "" : " COLD")}:\n");
        jitprintf($"  >> Start offset   : 0x{startOffset:x6} (not in unwind data)\n");
        jitprintf($"  >>   End offset   : 0x{endOffset:x6} (not in unwind data)\n");

        if (storage is null)
        {
            // The EE supplies chained unwind information for the root cold section.
            assert(!isHotCode);
            return;
        }

        var bytes = storage.AsSpan((int)slot);
        var version = bytes[0] & 7;
        var flags = bytes[0] >> 3;
        var count = bytes[2];
        var frameReg = bytes[3] & 15;
        var frameOffset = bytes[3] >> 4;

        jitprintf($"  Version           : {version}\n");
        jitprintf($"  Flags             : 0x{flags:x2}");
        if (flags != 0)
        {
            jitprintf(" (");
            if ((flags & 1) != 0)
            {
                jitprintf(" UNW_FLAG_EHANDLER");
            }
            if ((flags & 2) != 0)
            {
                jitprintf(" UNW_FLAG_UHANDLER");
            }
            if ((flags & 4) != 0)
            {
                jitprintf(" UNW_FLAG_CHAININFO");
            }
            jitprintf(")");
        }
        jitprintf("\n");
        jitprintf($"  SizeOfProlog      : 0x{bytes[1]:X2}\n");
        jitprintf($"  CountOfUnwindCodes: {count}\n");
        jitprintf($"  FrameRegister     : {(frameReg == 0 ? "none" : ((regNumber)frameReg).Name)} ({frameReg})\n");
        if (frameReg == 0)
        {
            jitprintf($"  FrameOffset       : N/A (no FrameRegister) (Value={frameOffset})\n");
        }
        else
        {
            jitprintf($"  FrameOffset       : {frameOffset} * 16 = 0x{frameOffset * 16:X2}\n");
        }
        jitprintf("  UnwindCodes       :\n");

        for (var index = 0; index < count; index++)
        {
            var code = bytes.Slice(4 + (index * 2));
            var codeOffset = code[0];
            var op = code[1] & 15;
            var opInfo = code[1] >> 4;
            var reg = ((regNumber)opInfo).Name;

            switch (op)
            {
                case UWOP_PUSH_NONVOL:
                {
                    jitprintf($"    CodeOffset: 0x{codeOffset:X2} UnwindOp: UWOP_PUSH_NONVOL ({op})     OpInfo: {reg} ({opInfo})\n");
                    break;
                }
                case UWOP_ALLOC_LARGE:
                {
                    jitprintf($"    CodeOffset: 0x{codeOffset:X2} UnwindOp: UWOP_ALLOC_LARGE ({op})     OpInfo: {opInfo} - ");
                    if (opInfo == 0)
                    {
                        index++;
                        var size = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(4 + (index * 2)));
                        jitprintf($"Scaled small  \n      Size: {size} * 8 = {size * 8} = 0x{size * 8:X5}\n");
                    }
                    else if (opInfo == 1)
                    {
                        index++;
                        var size = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4 + (index * 2)));
                        jitprintf($"Unscaled large\n      Size: {size} = 0x{size:X8}\n\n");
                        index++;
                    }
                    else
                    {
                        jitprintf("Unknown\n");
                    }
                    break;
                }
                case UWOP_ALLOC_SMALL:
                {
                    jitprintf($"    CodeOffset: 0x{codeOffset:X2} UnwindOp: UWOP_ALLOC_SMALL ({op})     OpInfo: {opInfo} * 8 + 8 = {opInfo * 8 + 8} = 0x{opInfo * 8 + 8:X2}\n");
                    break;
                }
                case UWOP_SET_FPREG:
                {
                    jitprintf($"    CodeOffset: 0x{codeOffset:X2} UnwindOp: UWOP_SET_FPREG ({op})       OpInfo: Unused ({opInfo})\n");
                    break;
                }
                case UWOP_SAVE_NONVOL:
                case UWOP_SAVE_XMM128:
                {
                    var name = op == UWOP_SAVE_NONVOL ? "UWOP_SAVE_NONVOL" : "UWOP_SAVE_XMM128";
                    var scale = op == UWOP_SAVE_NONVOL ? 8 : 16;
                    var registerName = op == UWOP_SAVE_NONVOL ? reg : $"XMM{opInfo}";
                    jitprintf($"    CodeOffset: 0x{codeOffset:X2} UnwindOp: {name} ({op})     OpInfo: {registerName} ({opInfo})\n");
                    index++;
                    var offset = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(4 + (index * 2)));
                    jitprintf($"      Scaled Small Offset: {offset} * {scale} = {offset * scale} = 0x{offset * scale:X5}\n");
                    break;
                }
                case UWOP_SAVE_NONVOL_FAR:
                case UWOP_SAVE_XMM128_FAR:
                {
                    var name = op == UWOP_SAVE_NONVOL_FAR ? "UWOP_SAVE_NONVOL_FAR" : "UWOP_SAVE_XMM128_FAR";
                    var registerName = op == UWOP_SAVE_NONVOL_FAR ? reg : $"XMM{opInfo}";
                    jitprintf($"    CodeOffset: 0x{codeOffset:X2} UnwindOp: {name} ({op}) OpInfo: {registerName} ({opInfo})\n");
                    index++;
                    var offset = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4 + (index * 2)));
                    jitprintf($"      Unscaled Large Offset: 0x{offset:X8}\n\n");
                    index++;
                    break;
                }
                default:
                {
                    jitprintf($"    Unrecognized UNWIND_CODE: 0x{BinaryPrimitives.ReadUInt16LittleEndian(code):X4}\n");
                    break;
                }
            }
        }
    }
}
#endif
