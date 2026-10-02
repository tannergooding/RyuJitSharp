// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

public partial class Emitter
{
    private static bool isValidImmNRS(nuint value, emitAttr size)
    {
        return (value >= 0) && (value < 0x2000);
    }

    private static bool isValidImmHWVal(nuint value, emitAttr size)
    {
        return (value >= 0) && (value < 0x40000);
    }

    private static bool isValidImmCond(nint imm)
    {
        if ((imm < 0) || (imm > 0xF))
        {
            return false;
        }

        // condFlagsImm stores the condition in bits 0..3; INS_COND_LE is 13.
        const uint lastValidCondition = 13;
        var condition = unchecked((uint)imm) & 0xF;
        return condition <= lastValidCondition;
    }

    private static bool isValidImmCondFlags(nint imm)
    {
        if ((imm < 0) || (imm > 0xFF))
        {
            return false;
        }

        const uint lastValidCondition = 13;
        var condition = unchecked((uint)imm) & 0xF;
        return condition <= lastValidCondition;
    }

    private static bool isValidImmCondFlagsImm5(nint imm)
    {
        if ((imm < 0) || (imm > 0x1FFF))
        {
            return false;
        }

        const uint lastValidCondition = 13;
        var condition = unchecked((uint)imm) & 0xF;
        return condition <= lastValidCondition;
    }

    private static bool isValidArrangement(emitAttr datasize, insOpts opt)
    {
        if (datasize == EA_8BYTE)
        {
            if ((opt == INS_OPTS_8B) || (opt == INS_OPTS_4H) || (opt == INS_OPTS_2S) || (opt == INS_OPTS_1D))
            {
                return true;
            }
        }
        else if (datasize == EA_16BYTE)
        {
            if ((opt == INS_OPTS_16B) || (opt == INS_OPTS_8H) || (opt == INS_OPTS_4S) || (opt == INS_OPTS_2D))
            {
                return true;
            }
        }

        return false;
    }

    internal static emitAttr optGetElemsize(insOpts arrangement)
    {
        if ((arrangement == INS_OPTS_8B) || (arrangement == INS_OPTS_16B))
        {
            return EA_1BYTE;
        }
        else if ((arrangement == INS_OPTS_4H) || (arrangement == INS_OPTS_8H))
        {
            return EA_2BYTE;
        }
        else if ((arrangement == INS_OPTS_2S) || (arrangement == INS_OPTS_4S))
        {
            return EA_4BYTE;
        }
        else if ((arrangement == INS_OPTS_1D) || (arrangement == INS_OPTS_2D))
        {
            return EA_8BYTE;
        }
        else
        {
            assert(false, " invalid 'arrangement' value");
            return EA_UNKNOWN;
        }
    }

    private static emitAttr optGetDstsize(insOpts conversion)
    {
        switch (conversion)
        {
            case INS_OPTS_S_TO_8BYTE:
            case INS_OPTS_D_TO_8BYTE:
            case INS_OPTS_4BYTE_TO_D:
            case INS_OPTS_8BYTE_TO_D:
            case INS_OPTS_S_TO_D:
            case INS_OPTS_H_TO_D:
            case INS_OPTS_H_TO_8BYTE:
            {
                return EA_8BYTE;
            }

            case INS_OPTS_S_TO_4BYTE:
            case INS_OPTS_D_TO_4BYTE:
            case INS_OPTS_4BYTE_TO_S:
            case INS_OPTS_8BYTE_TO_S:
            case INS_OPTS_D_TO_S:
            case INS_OPTS_H_TO_S:
            case INS_OPTS_H_TO_4BYTE:
            {
                return EA_4BYTE;
            }

            case INS_OPTS_S_TO_H:
            case INS_OPTS_D_TO_H:
            case INS_OPTS_4BYTE_TO_H:
            case INS_OPTS_8BYTE_TO_H:
            {
                return EA_2BYTE;
            }

            default:
            {
                assert(false, " invalid 'conversion' value");
                return EA_UNKNOWN;
            }
        }
    }

    private static emitAttr optGetSrcsize(insOpts conversion)
    {
        switch (conversion)
        {
            case INS_OPTS_D_TO_8BYTE:
            case INS_OPTS_D_TO_4BYTE:
            case INS_OPTS_8BYTE_TO_D:
            case INS_OPTS_8BYTE_TO_S:
            case INS_OPTS_D_TO_S:
            case INS_OPTS_D_TO_H:
            case INS_OPTS_8BYTE_TO_H:
            {
                return EA_8BYTE;
            }

            case INS_OPTS_S_TO_8BYTE:
            case INS_OPTS_S_TO_4BYTE:
            case INS_OPTS_4BYTE_TO_S:
            case INS_OPTS_4BYTE_TO_D:
            case INS_OPTS_S_TO_D:
            case INS_OPTS_S_TO_H:
            case INS_OPTS_4BYTE_TO_H:
            {
                return EA_4BYTE;
            }

            case INS_OPTS_H_TO_S:
            case INS_OPTS_H_TO_D:
            case INS_OPTS_H_TO_4BYTE:
            case INS_OPTS_H_TO_8BYTE:
            {
                return EA_2BYTE;
            }

            default:
            {
                assert(false, " invalid 'conversion' value");
                return EA_UNKNOWN;
            }
        }
    }

    internal static bool isValidVectorIndex(emitAttr datasize, emitAttr elemsize, nint index)
    {
        assert(isValidVectorDatasize(datasize));
        assert(isValidVectorElemsize(elemsize));

        var result = false;
        if (index >= 0)
        {
            if (datasize == EA_8BYTE)
            {
                switch (elemsize)
                {
                    case EA_1BYTE:
                    {
                        result = index < 8;
                        break;
                    }

                    case EA_2BYTE:
                    {
                        result = index < 4;
                        break;
                    }

                    case EA_4BYTE:
                    {
                        result = index < 2;
                        break;
                    }

                    case EA_8BYTE:
                    {
                        result = index < 1;
                        break;
                    }

                    default:
                    {
                        unreached();
                        break;
                    }
                }
            }
            else if (datasize == EA_16BYTE)
            {
                switch (elemsize)
                {
                    case EA_1BYTE:
                    {
                        result = index < 16;
                        break;
                    }

                    case EA_2BYTE:
                    {
                        result = index < 8;
                        break;
                    }

                    case EA_4BYTE:
                    {
                        result = index < 4;
                        break;
                    }

                    case EA_8BYTE:
                    {
                        result = index < 2;
                        break;
                    }

                    default:
                    {
                        unreached();
                        break;
                    }
                }
            }
        }

        return result;
    }

    private bool emitInsIsVectorRightShift(instruction ins)
    {
        // CodeGen.instInfo's ARM64 RSH flag is bit 3 in the pinned instruction table.
        const byte vectorRightShiftFlag = 8;
        if ((uint)ins < (uint)CodeGen.instInfo.Length)
        {
            return (CodeGen.instInfo[(int)ins] & vectorRightShiftFlag) != 0;
        }
        else
        {
            return false;
        }
    }
}
#endif
