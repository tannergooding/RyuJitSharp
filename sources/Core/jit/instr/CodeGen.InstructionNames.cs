// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
using System;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public string genInsName(instruction ins)
    {
        assert((uint)ins < (uint)s_insNames.Length);
        return s_insNames[(int)ins];
    }

#if TARGET_AMD64
    public string genInsDisplayName(global::RyuJitSharp.Emitter.instrDesc id)
    {
        var ins = id.idIns();
        var insName = genInsName(ins);
        var emit = Emitter;

        string RemoveVexPrefixIfNeeded(string name)
        {
            return !emit.UseVexEncodings && name.StartsWith('v', StringComparison.Ordinal) ? name[1..] : name;
        }

        string GetFltCmpOpName(string suffix)
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

        string GetIntCmpOpName(string suffix)
        {
            ReadOnlySpan<string> names = ["eq", "lt", "le", "false", "neq", "ge", "gt", "true"];
            var control = unchecked((byte)global::RyuJitSharp.Emitter.emitGetInsSC(id));
            assert(control < names.Length);
            return RemoveVexPrefixIfNeeded($"vpcmp{names[control]}{suffix}");
        }

        string GetEvexOnlyName(string pseudoName)
        {
            return emit.TakesEvexPrefix(id) ? pseudoName : RemoveVexPrefixIfNeeded(insName);
        }

        if ((instInfo[(int)ins] & insFlags.INS_FLAGS_HasPseudoName) != 0)
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
                    return GetEvexOnlyName("vmovdqa32");
                }

                case INS_movdqu32:
                {
                    return GetEvexOnlyName("vmovdqu32");
                }

                case INS_pandd:
                {
                    return GetEvexOnlyName("vpandd");
                }

                case INS_pandnd:
                {
                    return GetEvexOnlyName("vpandnd");
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
                    return GetEvexOnlyName("vpord");
                }

                case INS_pxord:
                {
                    return GetEvexOnlyName("vpxord");
                }

                case INS_roundpd:
                {
                    return GetEvexOnlyName("vrndscalepd");
                }

                case INS_roundps:
                {
                    return GetEvexOnlyName("vrndscaleps");
                }

                case INS_roundsd:
                {
                    return GetEvexOnlyName("vrndscalesd");
                }

                case INS_roundss:
                {
                    return GetEvexOnlyName("vrndscaless");
                }

                case INS_vbroadcastf32x4:
                {
                    return GetEvexOnlyName("vbroadcastf32x4");
                }

                case INS_vbroadcasti32x4:
                {
                    return GetEvexOnlyName("vbroadcasti32x4");
                }

                case INS_vextractf32x4:
                {
                    return GetEvexOnlyName("vextractf32x4");
                }

                case INS_vextracti32x4:
                {
                    return GetEvexOnlyName("vextracti32x4");
                }

                case INS_vinsertf32x4:
                {
                    return GetEvexOnlyName("vinsertf32x4");
                }

                case INS_vinserti32x4:
                {
                    return GetEvexOnlyName("vinserti32x4");
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
            return insName + "p";
        }
        return insName;
    }
#endif
}
#endif
