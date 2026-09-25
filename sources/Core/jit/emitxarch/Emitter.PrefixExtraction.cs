// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
namespace RyuJitSharp;

public partial class Emitter
{
    private ulong emitExtractEvexPrefix(instruction ins, ref ulong code)
    {
        assert(_compiler is not null);
        assert(IsEvexEncodableInstruction(ins));
        var evexPrefix = (code >> 32) & 0xFFFFFFFF;
        code &= 0xFFFFFFFF;
        uint leadingBytes;
        var check = (byte)((code >> 24) & 0xFF);

        if (check != 0)
        {
            var sizePrefix = (byte)((code >> 16) & 0xFF);
            if (sizePrefix == 0)
            {
                assert(_compiler.compIsaSupportedDebugOnly(InstructionSet_AVX10v2)
                    || _compiler.compIsaSupportedDebugOnly(InstructionSet_AVXVNNIINT)
                    || _compiler.compIsaSupportedDebugOnly(InstructionSet_AVXVNNIINT_V512));
            }
            #endif
            else if (isPrefix(sizePrefix))
            {
                // EVEX.pp encodes no prefix, 66, F3 and F2 as 0, 1, 2 and 3.
                switch (sizePrefix)
                {
                    case 0x66:
                    {
                        if (IsBMIInstruction(ins))
                        {
                            switch (ins)
                            {
                                case INS_rorx:
                                case INS_pdep:
                                case INS_mulx:
                                case INS_shrx:
                                {
                                    evexPrefix |= 0x0300;
                                    break;
                                }

                                case INS_pext:
                                case INS_sarx:
                                {
                                    evexPrefix |= 0x0200;
                                    break;
                                }

                                case INS_shlx:
                                {
                                    evexPrefix |= 0x0100;
                                    break;
                                }
                            }
                            break;
                        }
                        assert(!IsBMIInstruction(ins));
                        evexPrefix |= 0x0100;
                        break;
                    }

                    case 0xF3:
                    {
                        evexPrefix |= 0x0200;
                        break;
                    }

                    case 0xF2:
                    {
                        evexPrefix |= 0x0300;
                        break;
                    }

                    default:
                    {
                        unreached();
                        break;
                    }
                }
            }
            else
            {
                unreached();
            }

            leadingBytes = check;
            assert((leadingBytes == 0x0F)
                || ((_compiler.compIsaSupportedDebugOnly(InstructionSet_AVX10v1)
                    || _compiler.compIsaSupportedDebugOnly(InstructionSet_APX)) && (leadingBytes <= 7)));
            code &= 0xFFFF;
            check = (byte)(code & 0xFF);
            if (check is 0x3A or 0x38)
            {
                leadingBytes = (leadingBytes << 8) | check;
                code &= 0xFF00;
            }
        }
        else
        {
            leadingBytes = (uint)((code >> 16) & 0xFF);
            assert((leadingBytes == 0x0F)
                || ((_compiler.compIsaSupportedDebugOnly(InstructionSet_AVX10v1)
                    || _compiler.compIsaSupportedDebugOnly(InstructionSet_AVX512BMM)) && (leadingBytes <= 7))
                || (IsApxExtendedEvexInstruction(ins) && (leadingBytes == 0)));
            code &= 0xFFFF;
        }

        // EVEX.mmm replaces the escape bytes, or carries an AVX10/APX map number.
        switch (leadingBytes)
        {
            case 0:
            {
                break;
            }

            case 0x0F:
            {
                if (((evexPrefix >> 16) & 7) == 4)
                {
                    // Promoted legacy instructions already carry APX map 4.
                    break;
                }
                evexPrefix |= 0x010000;
                break;
            }

            case 0x0F38:
            {
                evexPrefix |= 0x020000;
                break;
            }

            case 0x0F3A:
            {
                evexPrefix |= 0x030000;
                break;
            }

            case 4:
            {
                assert(_compiler.compIsaSupportedDebugOnly(InstructionSet_APX));
                evexPrefix |= 0x040000;
                break;
            }

            case 5:
            {
                assert(_compiler.compIsaSupportedDebugOnly(InstructionSet_AVX10v1));
                evexPrefix |= 0x050000;
                break;
            }

            case 6:
            {
                assert(_compiler.compIsaSupportedDebugOnly(InstructionSet_AVX10v1)
                    || _compiler.compIsaSupportedDebugOnly(InstructionSet_AVX512BMM));
                evexPrefix |= 0x060000;
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        return evexPrefix;
    }

    private ulong emitExtractVexPrefix(instruction ins, ref ulong code)
    {
        assert(_compiler is not null);
        assert(IsVexEncodableInstruction(ins));
        var vexPrefix = (code >> 32) & 0x00FFFFFF;
        code &= 0xFFFFFFFF;
        uint leadingBytes;
        var check = (byte)((code >> 24) & 0xFF);

        if (check != 0)
        {
            var sizePrefix = (byte)((code >> 16) & 0xFF);
            if (sizePrefix == 0)
            {
                assert(_compiler.compIsaSupportedDebugOnly(InstructionSet_AVXVNNIINT)
                    || _compiler.compIsaSupportedDebugOnly(InstructionSet_AVXVNNIINT_V512));
            }
            else if (isPrefix(sizePrefix))
            {
                // VEX.pp uses the same SIMD prefix encoding as EVEX.pp.
                switch (sizePrefix)
                {
                    case 0x66:
                    {
                        if (IsBMIInstruction(ins))
                        {
                            switch (ins)
                            {
                                case INS_rorx:
                                case INS_pdep:
                                case INS_mulx:
                                case INS_shrx:
                                {
                                    vexPrefix |= 3;
                                    break;
                                }

                                case INS_pext:
                                case INS_sarx:
                                {
                                    vexPrefix |= 2;
                                    break;
                                }

                                case INS_shlx:
                                {
                                    vexPrefix |= 1;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            vexPrefix |= 1;
                        }
                        break;
                    }

                    case 0xF3:
                    {
                        vexPrefix |= 2;
                        break;
                    }

                    case 0xF2:
                    {
                        vexPrefix |= 3;
                        break;
                    }

                    default:
                    {
                        unreached();
                        break;
                    }
                }
            }
            else
            {
                unreached();
            }

            leadingBytes = check;
            assert(leadingBytes == 0x0F);
            code &= 0xFFFF;
            check = (byte)(code & 0xFF);
            if (check is 0x3A or 0x38)
            {
                leadingBytes = (leadingBytes << 8) | check;
                code &= 0xFF00;
            }
        }
        else
        {
            leadingBytes = (uint)((code >> 16) & 0xFF);
            assert(leadingBytes is 0x0F or 0);
            code &= 0xFFFF;
        }

        // Only the 0F map permits the two-byte VEX form.
        switch (leadingBytes)
        {
            case 0:
            {
                break;
            }

            case 0x0F:
            {
                vexPrefix |= 0x0100;
                break;
            }

            case 0x0F38:
            {
                vexPrefix |= 0x0200;
                break;
            }

            case 0x0F3A:
            {
                vexPrefix |= 0x0300;
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        return vexPrefix;
    }
}
