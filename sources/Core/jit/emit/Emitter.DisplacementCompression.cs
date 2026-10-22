// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public static bool hasTupleTypeInfo(instruction ins)
    {
        if (!Configuration.TARGET_XARCH)
        {
            return false;
        }

        assert((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins) < (uint)s_tupleTypes.Length);
        return s_tupleTypes[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)] != INS_TT_NONE;
    }

    public static nint GetInputSizeInBytes(instrDesc id)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.InstructionInputSizesAreNotAvailableForThisTarget);
            return default!;
        }

        var nativeOrdinal = InstructionOrdinalMaps.GetNativeInstructionOrdinal(id.idIns());
        assert((uint)nativeOrdinal < (uint)CodeGen.instInfo.Length);
        var inputSize = CodeGen.instInfo[nativeOrdinal] & Input_Mask;

        switch (inputSize)
        {
            case 0:
            {
                return (nint)EA_SIZE_IN_BYTES(id.idOpSize());
            }

            case Input_8Bit:
            {
                return 1;
            }

            case Input_16Bit:
            {
                return 2;
            }

            case Input_32Bit:
            {
                return 4;
            }

            case Input_64Bit:
            {
                return 8;
            }

            default:
            {
                FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.InvalidInstructionInputSize);
                return default!;
            }
        }
    }

    public bool TryEvexCompressDisp8Byte(instrDesc id, nint dsp, out nint compressedDsp, out bool fitsInByte)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.EVEXDisplacementCompressionIsNotAvailableForThisTarget);
            compressedDsp = default!;
            fitsInByte = default!;
            return default!;
        }

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
            assert(compressedDsp == dsp);
            return fitsInByte;
        }

        if (fitsInByte)
        {
            if (!TakesEvexPrefix(id))
            {
                // Prefer the smaller VEX encoding when EVEX is not otherwise needed.
                assert(compressedDsp == dsp);
                return false;
            }
        }
        else
        {
            var compressedTest = dsp / 64;

            if (unchecked((sbyte)compressedTest) != compressedTest)
            {
                assert(compressedDsp == dsp);
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
                case IF_XARCH_RWR_RRD_ARD:
                case IF_XARCH_RWR_RRD_MRD:
                case IF_XARCH_RWR_RRD_SRD:
                {
                    tt &= INS_TT_MEM128;
                    break;
                }

                case IF_XARCH_RWR_ARD_CNS:
                case IF_XARCH_RWR_MRD_CNS:
                case IF_XARCH_RWR_SRD_CNS:
                {
                    tt &= ~INS_TT_MEM128;
                    break;
                }

                default:
                {
                    FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.InvalidMixedMemoryTupleFormat);
                    compressedDsp = default!;
                    fitsInByte = default!;
                    return default!;
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
                assert((inputSize == 2) || (inputSize == 4));
                disp8Compression = isEmbBroadcast ? inputSize : vectorLength / 2;
                break;
            }

            case INS_TT_QUARTER:
            {
                disp8Compression = isEmbBroadcast ? inputSize : vectorLength / 4;
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
                FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.InvalidInstructionTupleType);
                compressedDsp = default!;
                fitsInByte = default!;
                return default!;
            }
        }

        if ((dsp % disp8Compression) != 0)
        {
            assert(compressedDsp == dsp);
            fitsInByte = false;
            return false;
        }

        var compressedDisp = dsp / disp8Compression;

        if (unchecked((sbyte)compressedDisp) != compressedDisp)
        {
            assert(compressedDsp == dsp);
            fitsInByte = false;
            return false;
        }

        compressedDsp = compressedDisp;
        fitsInByte = true;
        return true;
    }
}
