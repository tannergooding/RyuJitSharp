// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86 && JIT32_GCENCODER && DUMP_GC_TABLES
namespace RyuJitSharp;

public partial struct GCInfo
{
    internal sealed unsafe partial class Jit32GCDump
    {
        internal static nuint decodeUnsigned(byte* source, out uint value)
        {
            nuint size = 1;
            var next = *source++;
            value = (uint)(next & 0x7F);
            while ((next & 0x80) != 0)
            {
                size++;
                next = *source++;
                value = unchecked((value << 7) + (uint)(next & 0x7F));
            }

            return size;
        }

        internal static nuint decodeUDelta(byte* source, out uint value, uint lastValue)
        {
            var size = decodeUnsigned(source, out var delta);
            value = unchecked(lastValue + delta);

            return size;
        }

        internal static nuint decodeSigned(byte* source, out int value)
        {
            nuint size = 1;
            var next = *source++;
            var first = next;
            value = next & 0x3F;
            while ((next & 0x80) != 0)
            {
                size++;
                next = *source++;
                value = unchecked((value << 7) + (next & 0x7F));
            }

            if ((first & 0x40) != 0)
            {
                value = unchecked(-value);
            }

            return size;
        }

        internal static byte* decodeHeader(byte* table, uint version, out InfoHdr header)
        {
            assert(version == GCINFO_VERSION);
            var nextByte = *table++;
            var encoding = nextByte & 0x7F;
            GetInfoHdr(encoding, out header);

            while ((nextByte & MORE_BYTES_TO_FOLLOW) != 0)
            {
                nextByte = *table++;
                encoding = nextByte & 0x7F;
                if (encoding < NEXT_FOUR_START)
                {
                    if (encoding < SET_ARGCOUNT)
                    {
                        header.frameSize = (uint)(encoding - SET_FRAMESIZE);
                    }
                    else if (encoding < SET_PROLOGSIZE)
                    {
                        header.argCount = (ushort)(encoding - SET_ARGCOUNT);
                    }
                    else if (encoding < SET_EPILOGSIZE)
                    {
                        header.prologSize = (byte)(encoding - SET_PROLOGSIZE);
                    }
                    else if (encoding < SET_EPILOGCNT)
                    {
                        header.epilogSize = (byte)(encoding - SET_EPILOGSIZE);
                    }
                    else if (encoding < SET_UNTRACKED)
                    {
                        header.epilogCount = (byte)((encoding - SET_EPILOGCNT) / 2);
                        header.epilogAtEnd = (byte)((encoding - SET_EPILOGCNT) & 1);
                        assert((header.epilogAtEnd == 0) || (header.epilogCount == 1));
                    }
                    else if (encoding < FIRST_FLIP)
                    {
                        header.untrackedCnt = (uint)(encoding - SET_UNTRACKED);
                        assert(header.untrackedCnt != HAS_UNTRACKED);
                    }
                    else
                    {
                        switch (encoding)
                        {
                            case FLIP_EDI_SAVED:
                            {
                                header.ediSaved ^= 1;
                                break;
                            }

                            case FLIP_ESI_SAVED:
                            {
                                header.esiSaved ^= 1;
                                break;
                            }

                            case FLIP_EBX_SAVED:
                            {
                                header.ebxSaved ^= 1;
                                break;
                            }

                            case FLIP_EBP_SAVED:
                            {
                                header.ebpSaved ^= 1;
                                break;
                            }

                            case FLIP_EBP_FRAME:
                            {
                                header.ebpFrame ^= 1;
                                break;
                            }

                            case FLIP_INTERRUPTIBLE:
                            {
                                header.interruptible ^= 1;
                                break;
                            }

                            case FLIP_DOUBLE_ALIGN:
                            {
                                header.doubleAlign ^= 1;
                                break;
                            }

                            case FLIP_SECURITY:
                            {
                                header.security ^= 1;
                                break;
                            }

                            case FLIP_HANDLERS:
                            {
                                header.handlers ^= 1;
                                break;
                            }

                            case FLIP_LOCALLOC:
                            {
                                header.localloc ^= 1;
                                break;
                            }

                            case FLIP_EDITNCONTINUE:
                            {
                                header.editNcontinue ^= 1;
                                break;
                            }

                            case FLIP_VAR_PTR_TABLE_SZ:
                            {
                                header.varPtrTableSize ^= HAS_VARPTR;
                                break;
                            }

                            case FFFF_UNTRACKED_CNT:
                            {
                                header.untrackedCnt = HAS_UNTRACKED;
                                break;
                            }

                            case FLIP_VARARGS:
                            {
                                header.varargs ^= 1;
                                break;
                            }

                            case FLIP_PROF_CALLBACKS:
                            {
                                header.profCallbacks ^= 1;
                                break;
                            }

                            case FLIP_HAS_GENERICS_CONTEXT:
                            {
                                header.genericsContext ^= 1;
                                break;
                            }

                            case FLIP_GENERICS_CONTEXT_IS_METHODDESC:
                            {
                                header.genericsContextIsMethodDesc ^= 1;
                                break;
                            }

                            case FLIP_HAS_GS_COOKIE:
                            {
                                header.gsCookieOffset ^= HAS_GS_COOKIE_OFFSET;
                                break;
                            }

                            case FLIP_SYNC:
                            {
                                header.syncStartOffset ^= HAS_SYNC_OFFSET;
                                break;
                            }

                            case FLIP_REV_PINVOKE_FRAME:
                            {
                                header.revPInvokeOffset ^= INVALID_REV_PINVOKE_OFFSET ^ HAS_REV_PINVOKE_FRAME_OFFSET;
                                break;
                            }

                            case NEXT_OPCODE:
                            {
                                assert((nextByte & MORE_BYTES_TO_FOLLOW) != 0);
                                nextByte = *table++;
                                encoding = nextByte & 0x7F;
                                if (encoding <= SET_RET_KIND_MAX_V5)
                                {
                                    header.returnKind = (byte)(encoding & 3);
                                    header.isAsync = (byte)((encoding & 4) != 0 ? 1 : 0);
                                }
                                else if (encoding < FFFF_NOGCREGION_CNT_V5)
                                {
                                    header.noGCRegionCnt = (uint)(encoding - SET_NOGCREGIONS_CNT_V5);
                                }
                                else if (encoding == FFFF_NOGCREGION_CNT_V5)
                                {
                                    header.noGCRegionCnt = HAS_NOGCREGIONS;
                                }
                                else
                                {
                                    assert(false, "Unexpected encoding");
                                }
                                break;
                            }

                            default:
                            {
                                assert(false, "Unexpected encoding");
                                break;
                            }
                        }
                    }
                }
                else
                {
                    switch (encoding >> 4)
                    {
                        case 5:
                        {
                            assert(NEXT_FOUR_FRAMESIZE == 0x50);
                            header.frameSize = unchecked((header.frameSize << 4) + (uint)(encoding & 0xF));
                            break;
                        }

                        case 6:
                        {
                            assert(NEXT_FOUR_ARGCOUNT == 0x60);
                            header.argCount = unchecked((ushort)((header.argCount << 4) + (encoding & 0xF)));
                            break;
                        }

                        case 7:
                        {
                            if ((encoding & 0x8) == 0)
                            {
                                assert(NEXT_THREE_PROLOGSIZE == 0x70);
                                header.prologSize = unchecked((byte)((header.prologSize << 3) + (encoding & 0x7)));
                            }
                            else
                            {
                                assert(NEXT_THREE_EPILOGSIZE == 0x78);
                                header.epilogSize = unchecked((byte)((header.epilogSize << 3) + (encoding & 0x7)));
                            }
                            break;
                        }

                        default:
                        {
                            assert(false, "Unexpected encoding");
                            break;
                        }
                    }
                }
            }

            return table;
        }

        private static void decodeCallPattern(uint pattern, out uint argCount, out uint regMask,
            out uint argMask, out uint codeDelta)
        {
            assert(pattern < 80);
            var value = CallPatternTable[pattern];
            argCount = value & 0xFF;
            regMask = (value >> 8) & 0xFF;
            argMask = (value >> 16) & 0xFF;
            codeDelta = value >> 24;
        }
    }
}
#endif
