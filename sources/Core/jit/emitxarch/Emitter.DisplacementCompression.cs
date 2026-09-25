// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_AMD64
    public static bool hasTupleTypeInfo(instruction ins)
    {
        assert((uint)ins < (uint)s_tupleTypes.Length);
        return s_tupleTypes[(int)ins] != INS_TT_NONE;
    }

    public static nint GetInputSizeInBytes(instrDesc id)
    {
        assert((uint)id.idIns() < (uint)CodeGen.instInfo.Length);
        var inputSize = CodeGen.instInfo[(int)id.idIns()] & Input_Mask;

        return inputSize switch
        {
            0 => (nint)EA_SIZE_IN_BYTES(id.idOpSize()),
            Input_8Bit => 1,
            Input_16Bit => 2,
            Input_32Bit => 4,
            Input_64Bit => 8,
            _ => throw new FatalJitException("Invalid instruction input size."),
        };
    }

    public bool TryEvexCompressDisp8Byte(instrDesc id, nint dsp, out nint compressedDsp, out bool fitsInByte)
    {
        var ins = id.idIns();
        assert(IsEvexEncodableInstruction(ins));
        assert(id.idHasMem() && !id.idHasMemGen());
        assert(!id.idIsDspReloc());

        compressedDsp = dsp;
        fitsInByte = unchecked((sbyte)dsp) == dsp;

        if (!hasTupleTypeInfo(ins))
        {
            // APX-EVEX instructions without tuple information always scale by one.
            assert(IsApxExtendedEvexInstruction(ins) || IsBMIInstruction(ins) || IsKMOVInstruction(ins));
            return fitsInByte;
        }

        if (fitsInByte)
        {
            if (!TakesEvexPrefix(id))
            {
                // Prefer the smaller VEX encoding when EVEX is not otherwise needed.
                return false;
            }
        }
        else
        {
            var compressedTest = dsp / 64;

            if (unchecked((sbyte)compressedTest) != compressedTest)
            {
                return false;
            }
        }

        var tt = insTupleTypeInfo(ins);
        var vectorLength = (nint)EA_SIZE_IN_BYTES(id.idOpSize());
        var inputSize = GetInputSizeInBytes(id);
        nint disp8Compression;

        if ((tt & INS_TT_MEM128) != 0)
        {
            var insFmt = id.idInsFmt();

            if ((tt & INS_TT_FULL) != 0)
            {
                assert(tt == (INS_TT_FULL | INS_TT_MEM128));
                assert(ins is INS_pslld or INS_psrad or INS_psrld or INS_psllq or INS_vpsraq or INS_psrlq);
            }
            else
            {
                assert(tt == (INS_TT_FULL_MEM | INS_TT_MEM128));
                assert(ins is INS_psllw or INS_psraw or INS_psrlw);
            }

            switch (insFmt)
            {
                case IF_RWR_RRD_ARD:
                case IF_RWR_RRD_MRD:
                case IF_RWR_RRD_SRD:
                {
                    tt &= INS_TT_MEM128;
                    break;
                }

                case IF_RWR_ARD_CNS:
                case IF_RWR_MRD_CNS:
                case IF_RWR_SRD_CNS:
                {
                    tt &= ~INS_TT_MEM128;
                    break;
                }

                default:
                {
                    throw new FatalJitException("Invalid instruction format for a mixed memory tuple.");
                }
            }
        }

        var isEmbBroadcast = HasEmbeddedBroadcast(id);

        switch (tt)
        {
            case INS_TT_FULL:
            {
                disp8Compression = isEmbBroadcast ? inputSize : vectorLength;
                break;
            }

            case INS_TT_HALF:
            {
                assert(inputSize == 4);
                disp8Compression = isEmbBroadcast ? inputSize : vectorLength / 2;
                break;
            }

            case INS_TT_FULL_MEM:
            {
                assert(!isEmbBroadcast);
                disp8Compression = vectorLength;
                break;
            }

            case INS_TT_TUPLE1_SCALAR:
            {
                assert(!isEmbBroadcast);
                disp8Compression = inputSize;
                break;
            }

            case INS_TT_TUPLE1_FIXED:
            {
                assert(!isEmbBroadcast);
                assert((inputSize == 4) || (inputSize == 8));
                disp8Compression = inputSize;
                break;
            }

            case INS_TT_TUPLE2:
            {
                assert(!isEmbBroadcast);
                assert((inputSize == 4) || ((inputSize == 8) && (vectorLength >= 32)));
                disp8Compression = inputSize * 2;
                break;
            }

            case INS_TT_TUPLE4:
            {
                assert(!isEmbBroadcast);
                assert(((inputSize == 4) && (vectorLength >= 32)) || ((inputSize == 8) && (vectorLength >= 64)));
                disp8Compression = inputSize * 4;
                break;
            }

            case INS_TT_TUPLE8:
            {
                assert(!isEmbBroadcast);
                assert((inputSize == 4) && (vectorLength >= 64));
                disp8Compression = inputSize * 8;
                break;
            }

            case INS_TT_HALF_MEM:
            {
                assert(!isEmbBroadcast);
                disp8Compression = vectorLength / 2;
                break;
            }

            case INS_TT_QUARTER_MEM:
            {
                assert(!isEmbBroadcast);
                disp8Compression = vectorLength / 4;
                break;
            }

            case INS_TT_EIGHTH_MEM:
            {
                assert(!isEmbBroadcast);
                disp8Compression = vectorLength / 8;
                break;
            }

            case INS_TT_MEM128:
            {
                assert(!isEmbBroadcast);
                disp8Compression = 16;
                break;
            }

            case INS_TT_MOVDDUP:
            {
                assert(!isEmbBroadcast);
                disp8Compression = (vectorLength == 16) ? vectorLength / 2 : vectorLength;
                break;
            }

            default:
            {
                throw new FatalJitException("Invalid instruction tuple type.");
            }
        }

        if ((dsp % disp8Compression) != 0)
        {
            fitsInByte = false;
            return false;
        }

        var compressedDisp = dsp / disp8Compression;

        if (unchecked((sbyte)compressedDisp) != compressedDisp)
        {
            fitsInByte = false;
            return false;
        }

        compressedDsp = compressedDisp;
        fitsInByte = true;
        return true;
    }
#endif
}
