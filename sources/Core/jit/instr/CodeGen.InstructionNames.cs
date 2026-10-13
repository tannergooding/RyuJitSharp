// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public ReadOnlySpan<char> genInsName(instruction ins)
    {
        return GetInstructionName(ins);
    }

    private static ReadOnlySpan<char> GetInstructionName(instruction ins)
    {
        var index = InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins);
        assert((uint)index < (uint)(s_instructionNameRanges.Length / 2));

        var rangeIndex = index * 2;
        var characterIndex = GetRvaSpanEntry(s_instructionNameRanges, rangeIndex);
        var nameLength = GetRvaSpanEntry(s_instructionNameRanges, rangeIndex + 1);

        var name = GetRvaSpanSlice(s_instructionNameData, characterIndex, nameLength);
        assert(GetRvaSpanEntry(s_instructionNameData, characterIndex + nameLength) is '\0');
        return name;
    }

    private ReadOnlySpan<char> GetXarchInstructionDisplayName(global::RyuJitSharp.Emitter.instrDesc id)
    {
        var ins = id.idIns();
        var insName = genInsName(ins);
        var emit = Emitter;

        ReadOnlySpan<char> RemoveVexPrefixIfNeeded(ReadOnlySpan<char> name)
        {
            return !emit.UseVexEncodings && (name.Length > 0) && (name[0] is 'v') ? name[1..] : name;
        }

        ReadOnlySpan<char> GetFltCmpOpName(string suffix)
        {
            ReadOnlySpan<string> names = [
                "eq", "lt", "le", "unord", "neq", "nlt", "nle", "ord",
                "eq_uq", "nge", "ngt", "false", "neq_oq", "ge", "gt", "true",
                "eq_os", "lt_oq", "le_oq", "unord_s", "neq_us", "nlt_uq", "nle_uq", "ord_s",
                "eq_us", "nge_uq", "ngt_uq", "false_os", "neq_os", "ge_oq", "gt_oq", "true_us",
            ];
            var control = unchecked((byte)global::RyuJitSharp.Emitter.emitGetInsSC(id));
            assert(control < names.Length);
            return RemoveVexPrefixIfNeeded($"vcmp{names[control]}{suffix}");
        }

        ReadOnlySpan<char> GetIntCmpOpName(string suffix)
        {
            ReadOnlySpan<string> names = ["eq", "lt", "le", "false", "neq", "ge", "gt", "true"];
            var control = unchecked((byte)global::RyuJitSharp.Emitter.emitGetInsSC(id));
            assert(control < names.Length);
            return RemoveVexPrefixIfNeeded($"vpcmp{names[control]}{suffix}");
        }

        ReadOnlySpan<char> GetEvexOnlyName(ReadOnlySpan<char> name, string pseudoName)
        {
            return emit.TakesEvexPrefix(id) ? pseudoName : RemoveVexPrefixIfNeeded(name);
        }

        if (instHasPseudoName(ins))
        {
            switch (ins)
            {
                case INS_cdq:
                {
                    return id.idOpSize() switch
                    {
                        EA_8BYTE => "cqo",
                        EA_4BYTE => "cdq",
                        EA_2BYTE => "cwd",
                        _ => throw new FatalJitException("Invalid sign-extension size."),
                    };
                }

                case INS_cmppd:
                case INS_vcmppd:
                {
                    return GetFltCmpOpName("pd");
                }

                case INS_cmpps:
                case INS_vcmpps:
                {
                    return GetFltCmpOpName("ps");
                }

                case INS_cmpsd:
                case INS_vcmpsd:
                {
                    return GetFltCmpOpName("sd");
                }

                case INS_cmpss:
                case INS_vcmpss:
                {
                    return GetFltCmpOpName("ss");
                }

                case INS_cwde:
                {
                    return id.idOpSize() switch
                    {
                        EA_8BYTE => "cdqe",
                        EA_4BYTE => "cwde",
                        EA_2BYTE => "cbw",
                        _ => throw new FatalJitException("Invalid sign-extension size."),
                    };
                }

                case INS_movdqa32:
                {
                    return GetEvexOnlyName(insName, "vmovdqa32");
                }

                case INS_movdqu32:
                {
                    return GetEvexOnlyName(insName, "vmovdqu32");
                }

                case INS_pandd:
                {
                    return GetEvexOnlyName(insName, "vpandd");
                }

                case INS_pandnd:
                {
                    return GetEvexOnlyName(insName, "vpandnd");
                }

                case INS_pclmulqdq:
                {
                    return (unchecked((byte)global::RyuJitSharp.Emitter.emitGetInsSC(id)) & 0x11) switch
                    {
                        0 => RemoveVexPrefixIfNeeded("vpclmullqlqdq"),
                        1 => RemoveVexPrefixIfNeeded("vpclmulhqlqdq"),
                        0x10 => RemoveVexPrefixIfNeeded("vpclmullqhqdq"),
                        _ => RemoveVexPrefixIfNeeded("vpclmulhqhqdq"),
                    };
                }

                case INS_pord:
                {
                    return GetEvexOnlyName(insName, "vpord");
                }

                case INS_pxord:
                {
                    return GetEvexOnlyName(insName, "vpxord");
                }

                case INS_roundpd:
                {
                    return GetEvexOnlyName(insName, "vrndscalepd");
                }

                case INS_roundps:
                {
                    return GetEvexOnlyName(insName, "vrndscaleps");
                }

                case INS_roundsd:
                {
                    return GetEvexOnlyName(insName, "vrndscalesd");
                }

                case INS_roundss:
                {
                    return GetEvexOnlyName(insName, "vrndscaless");
                }

                case INS_vbroadcastf32x4:
                {
                    return GetEvexOnlyName(insName, "vbroadcastf32x4");
                }

                case INS_vbroadcasti32x4:
                {
                    return GetEvexOnlyName(insName, "vbroadcasti32x4");
                }

                case INS_vextractf32x4:
                {
                    return GetEvexOnlyName(insName, "vextractf32x4");
                }

                case INS_vextracti32x4:
                {
                    return GetEvexOnlyName(insName, "vextracti32x4");
                }

                case INS_vinsertf32x4:
                {
                    return GetEvexOnlyName(insName, "vinsertf32x4");
                }

                case INS_vinserti32x4:
                {
                    return GetEvexOnlyName(insName, "vinserti32x4");
                }

                case INS_vpcmpb:
                {
                    return GetIntCmpOpName("b");
                }

                case INS_vpcmpd:
                {
                    return GetIntCmpOpName("d");
                }

                case INS_vpcmpq:
                {
                    return GetIntCmpOpName("q");
                }

                case INS_vpcmpub:
                {
                    return GetIntCmpOpName("ub");
                }

                case INS_vpcmpud:
                {
                    return GetIntCmpOpName("ud");
                }

                case INS_vpcmpuq:
                {
                    return GetIntCmpOpName("uq");
                }

                case INS_vpcmpuw:
                {
                    return GetIntCmpOpName("uw");
                }

                case INS_vpcmpw:
                {
                    return GetIntCmpOpName("w");
                }

                default:
                {
                    throw new FatalJitException("Unexpected instruction with pseudo name.");
                }
            }
        }

        if (global::RyuJitSharp.Emitter.IsSimdInstruction(ins))
        {
            return RemoveVexPrefixIfNeeded(insName);
        }
        if (id.idIsApxPpxContextSet())
        {
            return insName.ToString() + "p";
        }
        return insName;
    }
}
