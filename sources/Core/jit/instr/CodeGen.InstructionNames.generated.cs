// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private static ReadOnlySpan<char> s_instructionNameData =>
        Configuration.TARGET_XARCH ? s_instructionNameDataXarch : s_instructionNameDataOther;

    private static ReadOnlySpan<char> s_instructionNameDataXarch => [
'I', 'N', 'V', 'A', 'L', 'I', 'D', '\0', // "INVALID"
'p', 'u', 's', 'h', '\0', // "push"
'p', 'o', 'p', '\0', // "pop"
'p', 'u', 's', 'h', '2', '\0', // "push2"
'p', 'o', 'p', '2', '\0', // "pop2"
'i', 'n', 'c', '\0', // "inc"
'd', 'e', 'c', '\0', // "dec"
'b', 's', 'w', 'a', 'p', '\0', // "bswap"
'a', 'd', 'd', '\0', // "add"
'o', 'r', '\0', // "or"
'a', 'd', 'c', '\0', // "adc"
's', 'b', 'b', '\0', // "sbb"
'a', 'n', 'd', '\0', // "and"
's', 'u', 'b', '\0', // "sub"
'x', 'o', 'r', '\0', // "xor"
'c', 'm', 'p', '\0', // "cmp"
't', 'e', 's', 't', '\0', // "test"
'm', 'o', 'v', '\0', // "mov"
'l', 'e', 'a', '\0', // "lea"
'b', 't', '\0', // "bt"
'b', 't', 's', '\0', // "bts"
'b', 't', 'r', '\0', // "btr"
'b', 't', 'c', '\0', // "btc"
'b', 's', 'r', '\0', // "bsr"
'b', 's', 'f', '\0', // "bsf"
'm', 'o', 'v', 's', 'x', '\0', // "movsx"
'm', 'o', 'v', 's', 'x', 'd', '\0', // "movsxd"
'm', 'o', 'v', 'z', 'x', '\0', // "movzx"
'c', 'm', 'o', 'v', 'o', '\0', // "cmovo"
'c', 'm', 'o', 'v', 'n', 'o', '\0', // "cmovno"
'c', 'm', 'o', 'v', 'b', '\0', // "cmovb"
'c', 'm', 'o', 'v', 'a', 'e', '\0', // "cmovae"
'c', 'm', 'o', 'v', 'e', '\0', // "cmove"
'c', 'm', 'o', 'v', 'n', 'e', '\0', // "cmovne"
'c', 'm', 'o', 'v', 'b', 'e', '\0', // "cmovbe"
'c', 'm', 'o', 'v', 'a', '\0', // "cmova"
'c', 'm', 'o', 'v', 's', '\0', // "cmovs"
'c', 'm', 'o', 'v', 'n', 's', '\0', // "cmovns"
'c', 'm', 'o', 'v', 'p', '\0', // "cmovp"
'c', 'm', 'o', 'v', 'n', 'p', '\0', // "cmovnp"
'c', 'm', 'o', 'v', 'l', '\0', // "cmovl"
'c', 'm', 'o', 'v', 'g', 'e', '\0', // "cmovge"
'c', 'm', 'o', 'v', 'l', 'e', '\0', // "cmovle"
'c', 'm', 'o', 'v', 'g', '\0', // "cmovg"
'x', 'c', 'h', 'g', '\0', // "xchg"
'i', 'm', 'u', 'l', '\0', // "imul"
'v', 'a', 'd', 'd', 'p', 'd', '\0', // "vaddpd"
'v', 'a', 'd', 'd', 'p', 's', '\0', // "vaddps"
'v', 'a', 'd', 'd', 's', 'd', '\0', // "vaddsd"
'v', 'a', 'd', 'd', 's', 's', '\0', // "vaddss"
'v', 'a', 'd', 'd', 's', 'u', 'b', 'p', 'd', '\0', // "vaddsubpd"
'v', 'a', 'd', 'd', 's', 'u', 'b', 'p', 's', '\0', // "vaddsubps"
'v', 'a', 'n', 'd', 'n', 'p', 'd', '\0', // "vandnpd"
'v', 'a', 'n', 'd', 'n', 'p', 's', '\0', // "vandnps"
'v', 'a', 'n', 'd', 'p', 'd', '\0', // "vandpd"
'v', 'a', 'n', 'd', 'p', 's', '\0', // "vandps"
'v', 'b', 'l', 'e', 'n', 'd', 'p', 'd', '\0', // "vblendpd"
'v', 'b', 'l', 'e', 'n', 'd', 'p', 's', '\0', // "vblendps"
'b', 'l', 'e', 'n', 'd', 'v', 'p', 'd', '\0', // "blendvpd"
'b', 'l', 'e', 'n', 'd', 'v', 'p', 's', '\0', // "blendvps"
'v', 'c', 'm', 'p', 'p', 'd', '\0', // "vcmppd"
'v', 'c', 'm', 'p', 'p', 's', '\0', // "vcmpps"
'v', 'c', 'm', 'p', 's', 'd', '\0', // "vcmpsd"
'v', 'c', 'm', 'p', 's', 's', '\0', // "vcmpss"
'v', 'c', 'o', 'm', 'i', 's', 'd', '\0', // "vcomisd"
'v', 'c', 'o', 'm', 'i', 's', 's', '\0', // "vcomiss"
'v', 'c', 'v', 't', 'd', 'q', '2', 'p', 'd', '\0', // "vcvtdq2pd"
'v', 'c', 'v', 't', 'd', 'q', '2', 'p', 's', '\0', // "vcvtdq2ps"
'v', 'c', 'v', 't', 'p', 'd', '2', 'd', 'q', '\0', // "vcvtpd2dq"
'v', 'c', 'v', 't', 'p', 'd', '2', 'p', 's', '\0', // "vcvtpd2ps"
'v', 'c', 'v', 't', 'p', 's', '2', 'd', 'q', '\0', // "vcvtps2dq"
'v', 'c', 'v', 't', 'p', 's', '2', 'p', 'd', '\0', // "vcvtps2pd"
'v', 'c', 'v', 't', 's', 'd', '2', 's', 'i', '\0', // "vcvtsd2si"
'v', 'c', 'v', 't', 's', 'd', '2', 's', 's', '\0', // "vcvtsd2ss"
'v', 'c', 'v', 't', 's', 'i', '2', 's', 'd', '\0', // "vcvtsi2sd"
'v', 'c', 'v', 't', 's', 'i', '2', 's', 's', '\0', // "vcvtsi2ss"
'v', 'c', 'v', 't', 's', 's', '2', 's', 'd', '\0', // "vcvtss2sd"
'v', 'c', 'v', 't', 's', 's', '2', 's', 'i', '\0', // "vcvtss2si"
'v', 'c', 'v', 't', 't', 'p', 'd', '2', 'd', 'q', '\0', // "vcvttpd2dq"
'v', 'c', 'v', 't', 't', 'p', 's', '2', 'd', 'q', '\0', // "vcvttps2dq"
'v', 'c', 'v', 't', 't', 's', 'd', '2', 's', 'i', '\0', // "vcvttsd2si"
'v', 'c', 'v', 't', 't', 's', 's', '2', 's', 'i', '\0', // "vcvttss2si"
'v', 'd', 'i', 'v', 'p', 'd', '\0', // "vdivpd"
'v', 'd', 'i', 'v', 'p', 's', '\0', // "vdivps"
'v', 'd', 'i', 'v', 's', 'd', '\0', // "vdivsd"
'v', 'd', 'i', 'v', 's', 's', '\0', // "vdivss"
'v', 'd', 'p', 'p', 'd', '\0', // "vdppd"
'v', 'd', 'p', 'p', 's', '\0', // "vdpps"
'v', 'e', 'x', 't', 'r', 'a', 'c', 't', 'p', 's', '\0', // "vextractps"
'v', 'h', 'a', 'd', 'd', 'p', 'd', '\0', // "vhaddpd"
'v', 'h', 'a', 'd', 'd', 'p', 's', '\0', // "vhaddps"
'v', 'h', 's', 'u', 'b', 'p', 'd', '\0', // "vhsubpd"
'v', 'h', 's', 'u', 'b', 'p', 's', '\0', // "vhsubps"
'v', 'i', 'n', 's', 'e', 'r', 't', 'p', 's', '\0', // "vinsertps"
'v', 'l', 'd', 'd', 'q', 'u', '\0', // "vlddqu"
'l', 'f', 'e', 'n', 'c', 'e', '\0', // "lfence"
'v', 'm', 'a', 's', 'k', 'm', 'o', 'v', 'd', 'q', 'u', '\0', // "vmaskmovdqu"
'v', 'm', 'a', 'x', 'p', 'd', '\0', // "vmaxpd"
'v', 'm', 'a', 'x', 'p', 's', '\0', // "vmaxps"
'v', 'm', 'a', 'x', 's', 'd', '\0', // "vmaxsd"
'v', 'm', 'a', 'x', 's', 's', '\0', // "vmaxss"
'm', 'f', 'e', 'n', 'c', 'e', '\0', // "mfence"
'v', 'm', 'i', 'n', 'p', 'd', '\0', // "vminpd"
'v', 'm', 'i', 'n', 'p', 's', '\0', // "vminps"
'v', 'm', 'i', 'n', 's', 'd', '\0', // "vminsd"
'v', 'm', 'i', 'n', 's', 's', '\0', // "vminss"
'v', 'm', 'o', 'v', 'a', 'p', 'd', '\0', // "vmovapd"
'v', 'm', 'o', 'v', 'a', 'p', 's', '\0', // "vmovaps"
'v', 'm', 'o', 'v', 'd', '\0', // "vmovd"
'v', 'm', 'o', 'v', 'q', '\0', // "vmovq"
'v', 'm', 'o', 'v', 'd', 'd', 'u', 'p', '\0', // "vmovddup"
'v', 'm', 'o', 'v', 'd', 'q', 'a', '\0', // "vmovdqa"
'v', 'm', 'o', 'v', 'd', 'q', 'u', '\0', // "vmovdqu"
'v', 'm', 'o', 'v', 'h', 'l', 'p', 's', '\0', // "vmovhlps"
'v', 'm', 'o', 'v', 'h', 'p', 'd', '\0', // "vmovhpd"
'v', 'm', 'o', 'v', 'h', 'p', 's', '\0', // "vmovhps"
'v', 'm', 'o', 'v', 'l', 'h', 'p', 's', '\0', // "vmovlhps"
'v', 'm', 'o', 'v', 'l', 'p', 'd', '\0', // "vmovlpd"
'v', 'm', 'o', 'v', 'l', 'p', 's', '\0', // "vmovlps"
'v', 'm', 'o', 'v', 'm', 's', 'k', 'p', 'd', '\0', // "vmovmskpd"
'v', 'm', 'o', 'v', 'm', 's', 'k', 'p', 's', '\0', // "vmovmskps"
'v', 'm', 'o', 'v', 'n', 't', 'd', 'q', '\0', // "vmovntdq"
'v', 'm', 'o', 'v', 'n', 't', 'd', 'q', 'a', '\0', // "vmovntdqa"
'm', 'o', 'v', 'n', 't', 'i', '\0', // "movnti"
'v', 'm', 'o', 'v', 'n', 't', 'p', 'd', '\0', // "vmovntpd"
'v', 'm', 'o', 'v', 'n', 't', 'p', 's', '\0', // "vmovntps"
'v', 'm', 'o', 'v', 's', 'd', '\0', // "vmovsd"
'v', 'm', 'o', 'v', 's', 'h', 'd', 'u', 'p', '\0', // "vmovshdup"
'v', 'm', 'o', 'v', 's', 'l', 'd', 'u', 'p', '\0', // "vmovsldup"
'v', 'm', 'o', 'v', 's', 's', '\0', // "vmovss"
'v', 'm', 'o', 'v', 'u', 'p', 'd', '\0', // "vmovupd"
'v', 'm', 'o', 'v', 'u', 'p', 's', '\0', // "vmovups"
'v', 'm', 'p', 's', 'a', 'd', 'b', 'w', '\0', // "vmpsadbw"
'v', 'm', 'u', 'l', 'p', 'd', '\0', // "vmulpd"
'v', 'm', 'u', 'l', 'p', 's', '\0', // "vmulps"
'v', 'm', 'u', 'l', 's', 'd', '\0', // "vmulsd"
'v', 'm', 'u', 'l', 's', 's', '\0', // "vmulss"
'v', 'o', 'r', 'p', 'd', '\0', // "vorpd"
'v', 'o', 'r', 'p', 's', '\0', // "vorps"
'v', 'p', 'a', 'b', 's', 'b', '\0', // "vpabsb"
'v', 'p', 'a', 'b', 's', 'd', '\0', // "vpabsd"
'v', 'p', 'a', 'b', 's', 'w', '\0', // "vpabsw"
'v', 'p', 'a', 'c', 'k', 's', 's', 'd', 'w', '\0', // "vpackssdw"
'v', 'p', 'a', 'c', 'k', 's', 's', 'w', 'b', '\0', // "vpacksswb"
'v', 'p', 'a', 'c', 'k', 'u', 's', 'd', 'w', '\0', // "vpackusdw"
'v', 'p', 'a', 'c', 'k', 'u', 's', 'w', 'b', '\0', // "vpackuswb"
'v', 'p', 'a', 'd', 'd', 'b', '\0', // "vpaddb"
'v', 'p', 'a', 'd', 'd', 'd', '\0', // "vpaddd"
'v', 'p', 'a', 'd', 'd', 'q', '\0', // "vpaddq"
'v', 'p', 'a', 'd', 'd', 's', 'b', '\0', // "vpaddsb"
'v', 'p', 'a', 'd', 'd', 's', 'w', '\0', // "vpaddsw"
'v', 'p', 'a', 'd', 'd', 'u', 's', 'b', '\0', // "vpaddusb"
'v', 'p', 'a', 'd', 'd', 'u', 's', 'w', '\0', // "vpaddusw"
'v', 'p', 'a', 'd', 'd', 'w', '\0', // "vpaddw"
'v', 'p', 'a', 'l', 'i', 'g', 'n', 'r', '\0', // "vpalignr"
'v', 'p', 'a', 'n', 'd', '\0', // "vpand"
'v', 'p', 'a', 'n', 'd', 'n', '\0', // "vpandn"
'v', 'p', 'a', 'v', 'g', 'b', '\0', // "vpavgb"
'v', 'p', 'a', 'v', 'g', 'w', '\0', // "vpavgw"
'p', 'b', 'l', 'e', 'n', 'd', 'v', 'b', '\0', // "pblendvb"
'v', 'p', 'b', 'l', 'e', 'n', 'd', 'w', '\0', // "vpblendw"
'v', 'p', 'c', 'm', 'p', 'e', 'q', 'b', '\0', // "vpcmpeqb"
'v', 'p', 'c', 'm', 'p', 'e', 'q', 'd', '\0', // "vpcmpeqd"
'v', 'p', 'c', 'm', 'p', 'e', 'q', 'q', '\0', // "vpcmpeqq"
'v', 'p', 'c', 'm', 'p', 'e', 'q', 'w', '\0', // "vpcmpeqw"
'v', 'p', 'c', 'm', 'p', 'g', 't', 'b', '\0', // "vpcmpgtb"
'v', 'p', 'c', 'm', 'p', 'g', 't', 'd', '\0', // "vpcmpgtd"
'v', 'p', 'c', 'm', 'p', 'g', 't', 'q', '\0', // "vpcmpgtq"
'v', 'p', 'c', 'm', 'p', 'g', 't', 'w', '\0', // "vpcmpgtw"
'v', 'p', 'e', 'x', 't', 'r', 'b', '\0', // "vpextrb"
'v', 'p', 'e', 'x', 't', 'r', 'd', '\0', // "vpextrd"
'v', 'p', 'e', 'x', 't', 'r', 'q', '\0', // "vpextrq"
'v', 'p', 'h', 'a', 'd', 'd', 'd', '\0', // "vphaddd"
'v', 'p', 'e', 'x', 't', 'r', 'w', '\0', // "vpextrw"
'v', 'p', 'h', 'a', 'd', 'd', 's', 'w', '\0', // "vphaddsw"
'v', 'p', 'h', 'a', 'd', 'd', 'w', '\0', // "vphaddw"
'v', 'p', 'h', 'm', 'i', 'n', 'p', 'o', 's', 'u', 'w', '\0', // "vphminposuw"
'v', 'p', 'h', 's', 'u', 'b', 'd', '\0', // "vphsubd"
'v', 'p', 'h', 's', 'u', 'b', 's', 'w', '\0', // "vphsubsw"
'v', 'p', 'h', 's', 'u', 'b', 'w', '\0', // "vphsubw"
'v', 'p', 'i', 'n', 's', 'r', 'b', '\0', // "vpinsrb"
'v', 'p', 'i', 'n', 's', 'r', 'd', '\0', // "vpinsrd"
'v', 'p', 'i', 'n', 's', 'r', 'q', '\0', // "vpinsrq"
'v', 'p', 'i', 'n', 's', 'r', 'w', '\0', // "vpinsrw"
'v', 'p', 'm', 'a', 'd', 'd', 'u', 'b', 's', 'w', '\0', // "vpmaddubsw"
'v', 'p', 'm', 'a', 'd', 'd', 'w', 'd', '\0', // "vpmaddwd"
'v', 'p', 'm', 'a', 'x', 's', 'b', '\0', // "vpmaxsb"
'v', 'p', 'm', 'a', 'x', 's', 'd', '\0', // "vpmaxsd"
'v', 'p', 'm', 'a', 'x', 's', 'w', '\0', // "vpmaxsw"
'v', 'p', 'm', 'a', 'x', 'u', 'b', '\0', // "vpmaxub"
'v', 'p', 'm', 'a', 'x', 'u', 'd', '\0', // "vpmaxud"
'v', 'p', 'm', 'a', 'x', 'u', 'w', '\0', // "vpmaxuw"
'v', 'p', 'm', 'i', 'n', 's', 'b', '\0', // "vpminsb"
'v', 'p', 'm', 'i', 'n', 's', 'd', '\0', // "vpminsd"
'v', 'p', 'm', 'i', 'n', 's', 'w', '\0', // "vpminsw"
'v', 'p', 'm', 'i', 'n', 'u', 'b', '\0', // "vpminub"
'v', 'p', 'm', 'i', 'n', 'u', 'd', '\0', // "vpminud"
'v', 'p', 'm', 'i', 'n', 'u', 'w', '\0', // "vpminuw"
'v', 'p', 'm', 'o', 'v', 'm', 's', 'k', 'b', '\0', // "vpmovmskb"
'v', 'p', 'm', 'o', 'v', 's', 'x', 'b', 'd', '\0', // "vpmovsxbd"
'v', 'p', 'm', 'o', 'v', 's', 'x', 'b', 'q', '\0', // "vpmovsxbq"
'v', 'p', 'm', 'o', 'v', 's', 'x', 'b', 'w', '\0', // "vpmovsxbw"
'v', 'p', 'm', 'o', 'v', 's', 'x', 'd', 'q', '\0', // "vpmovsxdq"
'v', 'p', 'm', 'o', 'v', 's', 'x', 'w', 'd', '\0', // "vpmovsxwd"
'v', 'p', 'm', 'o', 'v', 's', 'x', 'w', 'q', '\0', // "vpmovsxwq"
'v', 'p', 'm', 'o', 'v', 'z', 'x', 'b', 'd', '\0', // "vpmovzxbd"
'v', 'p', 'm', 'o', 'v', 'z', 'x', 'b', 'q', '\0', // "vpmovzxbq"
'v', 'p', 'm', 'o', 'v', 'z', 'x', 'b', 'w', '\0', // "vpmovzxbw"
'v', 'p', 'm', 'o', 'v', 'z', 'x', 'd', 'q', '\0', // "vpmovzxdq"
'v', 'p', 'm', 'o', 'v', 'z', 'x', 'w', 'd', '\0', // "vpmovzxwd"
'v', 'p', 'm', 'o', 'v', 'z', 'x', 'w', 'q', '\0', // "vpmovzxwq"
'v', 'p', 'm', 'u', 'l', 'd', 'q', '\0', // "vpmuldq"
'v', 'p', 'm', 'u', 'l', 'h', 'r', 's', 'w', '\0', // "vpmulhrsw"
'v', 'p', 'm', 'u', 'l', 'h', 'u', 'w', '\0', // "vpmulhuw"
'v', 'p', 'm', 'u', 'l', 'h', 'w', '\0', // "vpmulhw"
'v', 'p', 'm', 'u', 'l', 'l', 'd', '\0', // "vpmulld"
'v', 'p', 'm', 'u', 'l', 'u', 'd', 'q', '\0', // "vpmuludq"
'v', 'p', 'm', 'u', 'l', 'l', 'w', '\0', // "vpmullw"
'v', 'p', 'o', 'r', '\0', // "vpor"
'p', 'r', 'e', 'f', 'e', 't', 'c', 'h', 'n', 't', 'a', '\0', // "prefetchnta"
'p', 'r', 'e', 'f', 'e', 't', 'c', 'h', 't', '0', '\0', // "prefetcht0"
'p', 'r', 'e', 'f', 'e', 't', 'c', 'h', 't', '1', '\0', // "prefetcht1"
'p', 'r', 'e', 'f', 'e', 't', 'c', 'h', 't', '2', '\0', // "prefetcht2"
'v', 'p', 's', 'a', 'd', 'b', 'w', '\0', // "vpsadbw"
'v', 'p', 's', 'h', 'u', 'f', 'b', '\0', // "vpshufb"
'v', 'p', 's', 'h', 'u', 'f', 'd', '\0', // "vpshufd"
'v', 'p', 's', 'h', 'u', 'f', 'h', 'w', '\0', // "vpshufhw"
'v', 'p', 's', 'h', 'u', 'f', 'l', 'w', '\0', // "vpshuflw"
'v', 'p', 's', 'i', 'g', 'n', 'b', '\0', // "vpsignb"
'v', 'p', 's', 'i', 'g', 'n', 'd', '\0', // "vpsignd"
'v', 'p', 's', 'i', 'g', 'n', 'w', '\0', // "vpsignw"
'v', 'p', 's', 'l', 'l', 'd', '\0', // "vpslld"
'v', 'p', 's', 'l', 'l', 'd', 'q', '\0', // "vpslldq"
'v', 'p', 's', 'l', 'l', 'q', '\0', // "vpsllq"
'v', 'p', 's', 'l', 'l', 'w', '\0', // "vpsllw"
'v', 'p', 's', 'r', 'a', 'd', '\0', // "vpsrad"
'v', 'p', 's', 'r', 'a', 'w', '\0', // "vpsraw"
'v', 'p', 's', 'r', 'l', 'd', '\0', // "vpsrld"
'v', 'p', 's', 'r', 'l', 'd', 'q', '\0', // "vpsrldq"
'v', 'p', 's', 'r', 'l', 'q', '\0', // "vpsrlq"
'v', 'p', 's', 'r', 'l', 'w', '\0', // "vpsrlw"
'v', 'p', 's', 'u', 'b', 'b', '\0', // "vpsubb"
'v', 'p', 's', 'u', 'b', 'd', '\0', // "vpsubd"
'v', 'p', 's', 'u', 'b', 'q', '\0', // "vpsubq"
'v', 'p', 's', 'u', 'b', 's', 'b', '\0', // "vpsubsb"
'v', 'p', 's', 'u', 'b', 's', 'w', '\0', // "vpsubsw"
'v', 'p', 's', 'u', 'b', 'u', 's', 'b', '\0', // "vpsubusb"
'v', 'p', 's', 'u', 'b', 'u', 's', 'w', '\0', // "vpsubusw"
'v', 'p', 's', 'u', 'b', 'w', '\0', // "vpsubw"
'v', 'p', 't', 'e', 's', 't', '\0', // "vptest"
'v', 'p', 'u', 'n', 'p', 'c', 'k', 'h', 'b', 'w', '\0', // "vpunpckhbw"
'v', 'p', 'u', 'n', 'p', 'c', 'k', 'h', 'd', 'q', '\0', // "vpunpckhdq"
'v', 'p', 'u', 'n', 'p', 'c', 'k', 'h', 'q', 'd', 'q', '\0', // "vpunpckhqdq"
'v', 'p', 'u', 'n', 'p', 'c', 'k', 'h', 'w', 'd', '\0', // "vpunpckhwd"
'v', 'p', 'u', 'n', 'p', 'c', 'k', 'l', 'b', 'w', '\0', // "vpunpcklbw"
'v', 'p', 'u', 'n', 'p', 'c', 'k', 'l', 'd', 'q', '\0', // "vpunpckldq"
'v', 'p', 'u', 'n', 'p', 'c', 'k', 'l', 'q', 'd', 'q', '\0', // "vpunpcklqdq"
'v', 'p', 'u', 'n', 'p', 'c', 'k', 'l', 'w', 'd', '\0', // "vpunpcklwd"
'v', 'p', 'x', 'o', 'r', '\0', // "vpxor"
'v', 'r', 'c', 'p', 'p', 's', '\0', // "vrcpps"
'v', 'r', 'c', 'p', 's', 's', '\0', // "vrcpss"
'v', 'r', 'o', 'u', 'n', 'd', 'p', 'd', '\0', // "vroundpd"
'v', 'r', 'o', 'u', 'n', 'd', 'p', 's', '\0', // "vroundps"
'v', 'r', 'o', 'u', 'n', 'd', 's', 'd', '\0', // "vroundsd"
'v', 'r', 'o', 'u', 'n', 'd', 's', 's', '\0', // "vroundss"
'v', 'r', 's', 'q', 'r', 't', 'p', 's', '\0', // "vrsqrtps"
'v', 'r', 's', 'q', 'r', 't', 's', 's', '\0', // "vrsqrtss"
's', 'f', 'e', 'n', 'c', 'e', '\0', // "sfence"
'v', 's', 'h', 'u', 'f', 'p', 'd', '\0', // "vshufpd"
'v', 's', 'h', 'u', 'f', 'p', 's', '\0', // "vshufps"
'v', 's', 'q', 'r', 't', 'p', 'd', '\0', // "vsqrtpd"
'v', 's', 'q', 'r', 't', 'p', 's', '\0', // "vsqrtps"
'v', 's', 'q', 'r', 't', 's', 'd', '\0', // "vsqrtsd"
'v', 's', 'q', 'r', 't', 's', 's', '\0', // "vsqrtss"
'v', 's', 'u', 'b', 'p', 'd', '\0', // "vsubpd"
'v', 's', 'u', 'b', 'p', 's', '\0', // "vsubps"
'v', 's', 'u', 'b', 's', 'd', '\0', // "vsubsd"
'v', 's', 'u', 'b', 's', 's', '\0', // "vsubss"
'v', 'u', 'c', 'o', 'm', 'i', 's', 'd', '\0', // "vucomisd"
'v', 'u', 'c', 'o', 'm', 'i', 's', 's', '\0', // "vucomiss"
'v', 'u', 'n', 'p', 'c', 'k', 'h', 'p', 'd', '\0', // "vunpckhpd"
'v', 'u', 'n', 'p', 'c', 'k', 'h', 'p', 's', '\0', // "vunpckhps"
'v', 'u', 'n', 'p', 'c', 'k', 'l', 'p', 'd', '\0', // "vunpcklpd"
'v', 'u', 'n', 'p', 'c', 'k', 'l', 'p', 's', '\0', // "vunpcklps"
'v', 'x', 'o', 'r', 'p', 'd', '\0', // "vxorpd"
'v', 'x', 'o', 'r', 'p', 's', '\0', // "vxorps"
'v', 'a', 'e', 's', 'd', 'e', 'c', '\0', // "vaesdec"
'v', 'a', 'e', 's', 'd', 'e', 'c', 'l', 'a', 's', 't', '\0', // "vaesdeclast"
'v', 'a', 'e', 's', 'e', 'n', 'c', '\0', // "vaesenc"
'v', 'a', 'e', 's', 'e', 'n', 'c', 'l', 'a', 's', 't', '\0', // "vaesenclast"
'v', 'a', 'e', 's', 'i', 'm', 'c', '\0', // "vaesimc"
'v', 'a', 'e', 's', 'k', 'e', 'y', 'g', 'e', 'n', 'a', 's', 's', 'i', 's', 't', '\0', // "vaeskeygenassist"
'v', 'p', 'c', 'l', 'm', 'u', 'l', 'q', 'd', 'q', '\0', // "vpclmulqdq"
's', 'h', 'a', '1', 'm', 's', 'g', '1', '\0', // "sha1msg1"
's', 'h', 'a', '1', 'm', 's', 'g', '2', '\0', // "sha1msg2"
's', 'h', 'a', '1', 'n', 'e', 'x', 't', 'e', '\0', // "sha1nexte"
's', 'h', 'a', '1', 'r', 'n', 'd', 's', '4', '\0', // "sha1rnds4"
's', 'h', 'a', '2', '5', '6', 'm', 's', 'g', '1', '\0', // "sha256msg1"
's', 'h', 'a', '2', '5', '6', 'm', 's', 'g', '2', '\0', // "sha256msg2"
's', 'h', 'a', '2', '5', '6', 'r', 'n', 'd', 's', '2', '\0', // "sha256rnds2"
'v', 'g', 'f', '2', 'p', '8', 'a', 'f', 'f', 'i', 'n', 'e', 'i', 'n', 'v', 'q', 'b', '\0', // "vgf2p8affineinvqb"
'v', 'g', 'f', '2', 'p', '8', 'a', 'f', 'f', 'i', 'n', 'e', 'q', 'b', '\0', // "vgf2p8affineqb"
'v', 'g', 'f', '2', 'p', '8', 'm', 'u', 'l', 'b', '\0', // "vgf2p8mulb"
'v', 'b', 'l', 'e', 'n', 'd', 'v', 'p', 'd', '\0', // "vblendvpd"
'v', 'b', 'l', 'e', 'n', 'd', 'v', 'p', 's', '\0', // "vblendvps"
'v', 'b', 'r', 'o', 'a', 'd', 'c', 'a', 's', 't', 'f', '1', '2', '8', '\0', // "vbroadcastf128"
'v', 'b', 'r', 'o', 'a', 'd', 'c', 'a', 's', 't', 's', 'd', '\0', // "vbroadcastsd"
'v', 'b', 'r', 'o', 'a', 'd', 'c', 'a', 's', 't', 's', 's', '\0', // "vbroadcastss"
'v', 'e', 'x', 't', 'r', 'a', 'c', 't', 'f', '1', '2', '8', '\0', // "vextractf128"
'v', 'i', 'n', 's', 'e', 'r', 't', 'f', '1', '2', '8', '\0', // "vinsertf128"
'v', 'm', 'a', 's', 'k', 'm', 'o', 'v', 'p', 'd', '\0', // "vmaskmovpd"
'v', 'm', 'a', 's', 'k', 'm', 'o', 'v', 'p', 's', '\0', // "vmaskmovps"
'v', 'p', 'b', 'l', 'e', 'n', 'd', 'v', 'b', '\0', // "vpblendvb"
'v', 'p', 'e', 'r', 'm', '2', 'f', '1', '2', '8', '\0', // "vperm2f128"
'v', 'p', 'e', 'r', 'm', 'i', 'l', 'p', 'd', '\0', // "vpermilpd"
'v', 'p', 'e', 'r', 'm', 'i', 'l', 'p', 's', '\0', // "vpermilps"
'v', 't', 'e', 's', 't', 'p', 'd', '\0', // "vtestpd"
'v', 't', 'e', 's', 't', 'p', 's', '\0', // "vtestps"
'v', 'z', 'e', 'r', 'o', 'u', 'p', 'p', 'e', 'r', '\0', // "vzeroupper"
'v', 'b', 'r', 'o', 'a', 'd', 'c', 'a', 's', 't', 'i', '1', '2', '8', '\0', // "vbroadcasti128"
'v', 'c', 'v', 't', 'p', 'h', '2', 'p', 's', '\0', // "vcvtph2ps"
'v', 'c', 'v', 't', 'p', 's', '2', 'p', 'h', '\0', // "vcvtps2ph"
'v', 'e', 'x', 't', 'r', 'a', 'c', 't', 'i', '1', '2', '8', '\0', // "vextracti128"
'v', 'g', 'a', 't', 'h', 'e', 'r', 'd', 'p', 'd', '\0', // "vgatherdpd"
'v', 'g', 'a', 't', 'h', 'e', 'r', 'd', 'p', 's', '\0', // "vgatherdps"
'v', 'g', 'a', 't', 'h', 'e', 'r', 'q', 'p', 'd', '\0', // "vgatherqpd"
'v', 'g', 'a', 't', 'h', 'e', 'r', 'q', 'p', 's', '\0', // "vgatherqps"
'v', 'i', 'n', 's', 'e', 'r', 't', 'i', '1', '2', '8', '\0', // "vinserti128"
'v', 'p', 'b', 'l', 'e', 'n', 'd', 'd', '\0', // "vpblendd"
'v', 'p', 'b', 'r', 'o', 'a', 'd', 'c', 'a', 's', 't', 'b', '\0', // "vpbroadcastb"
'v', 'p', 'b', 'r', 'o', 'a', 'd', 'c', 'a', 's', 't', 'd', '\0', // "vpbroadcastd"
'v', 'p', 'b', 'r', 'o', 'a', 'd', 'c', 'a', 's', 't', 'q', '\0', // "vpbroadcastq"
'v', 'p', 'b', 'r', 'o', 'a', 'd', 'c', 'a', 's', 't', 'w', '\0', // "vpbroadcastw"
'v', 'p', 'e', 'r', 'm', '2', 'i', '1', '2', '8', '\0', // "vperm2i128"
'v', 'p', 'e', 'r', 'm', 'd', '\0', // "vpermd"
'v', 'p', 'e', 'r', 'm', 'p', 'd', '\0', // "vpermpd"
'v', 'p', 'e', 'r', 'm', 'p', 's', '\0', // "vpermps"
'v', 'p', 'e', 'r', 'm', 'q', '\0', // "vpermq"
'v', 'p', 'g', 'a', 't', 'h', 'e', 'r', 'd', 'd', '\0', // "vpgatherdd"
'v', 'p', 'g', 'a', 't', 'h', 'e', 'r', 'd', 'q', '\0', // "vpgatherdq"
'v', 'p', 'g', 'a', 't', 'h', 'e', 'r', 'q', 'd', '\0', // "vpgatherqd"
'v', 'p', 'g', 'a', 't', 'h', 'e', 'r', 'q', 'q', '\0', // "vpgatherqq"
'v', 'p', 'm', 'a', 's', 'k', 'm', 'o', 'v', 'd', '\0', // "vpmaskmovd"
'v', 'p', 'm', 'a', 's', 'k', 'm', 'o', 'v', 'q', '\0', // "vpmaskmovq"
'v', 'p', 's', 'l', 'l', 'v', 'd', '\0', // "vpsllvd"
'v', 'p', 's', 'l', 'l', 'v', 'q', '\0', // "vpsllvq"
'v', 'p', 's', 'r', 'a', 'v', 'd', '\0', // "vpsravd"
'v', 'p', 's', 'r', 'l', 'v', 'd', '\0', // "vpsrlvd"
'v', 'p', 's', 'r', 'l', 'v', 'q', '\0', // "vpsrlvq"
'v', 'f', 'm', 'a', 'd', 'd', '1', '3', '2', 'p', 'd', '\0', // "vfmadd132pd"
'v', 'f', 'm', 'a', 'd', 'd', '2', '1', '3', 'p', 'd', '\0', // "vfmadd213pd"
'v', 'f', 'm', 'a', 'd', 'd', '2', '3', '1', 'p', 'd', '\0', // "vfmadd231pd"
'v', 'f', 'm', 'a', 'd', 'd', '1', '3', '2', 'p', 's', '\0', // "vfmadd132ps"
'v', 'f', 'm', 'a', 'd', 'd', '2', '1', '3', 'p', 's', '\0', // "vfmadd213ps"
'v', 'f', 'm', 'a', 'd', 'd', '2', '3', '1', 'p', 's', '\0', // "vfmadd231ps"
'v', 'f', 'm', 'a', 'd', 'd', '1', '3', '2', 's', 'd', '\0', // "vfmadd132sd"
'v', 'f', 'm', 'a', 'd', 'd', '2', '1', '3', 's', 'd', '\0', // "vfmadd213sd"
'v', 'f', 'm', 'a', 'd', 'd', '2', '3', '1', 's', 'd', '\0', // "vfmadd231sd"
'v', 'f', 'm', 'a', 'd', 'd', '1', '3', '2', 's', 's', '\0', // "vfmadd132ss"
'v', 'f', 'm', 'a', 'd', 'd', '2', '1', '3', 's', 's', '\0', // "vfmadd213ss"
'v', 'f', 'm', 'a', 'd', 'd', '2', '3', '1', 's', 's', '\0', // "vfmadd231ss"
'v', 'f', 'm', 'a', 'd', 'd', 's', 'u', 'b', '1', '3', '2', 'p', 'd', '\0', // "vfmaddsub132pd"
'v', 'f', 'm', 'a', 'd', 'd', 's', 'u', 'b', '2', '1', '3', 'p', 'd', '\0', // "vfmaddsub213pd"
'v', 'f', 'm', 'a', 'd', 'd', 's', 'u', 'b', '2', '3', '1', 'p', 'd', '\0', // "vfmaddsub231pd"
'v', 'f', 'm', 'a', 'd', 'd', 's', 'u', 'b', '1', '3', '2', 'p', 's', '\0', // "vfmaddsub132ps"
'v', 'f', 'm', 'a', 'd', 'd', 's', 'u', 'b', '2', '1', '3', 'p', 's', '\0', // "vfmaddsub213ps"
'v', 'f', 'm', 'a', 'd', 'd', 's', 'u', 'b', '2', '3', '1', 'p', 's', '\0', // "vfmaddsub231ps"
'v', 'f', 'm', 's', 'u', 'b', 'a', 'd', 'd', '1', '3', '2', 'p', 'd', '\0', // "vfmsubadd132pd"
'v', 'f', 'm', 's', 'u', 'b', 'a', 'd', 'd', '2', '1', '3', 'p', 'd', '\0', // "vfmsubadd213pd"
'v', 'f', 'm', 's', 'u', 'b', 'a', 'd', 'd', '2', '3', '1', 'p', 'd', '\0', // "vfmsubadd231pd"
'v', 'f', 'm', 's', 'u', 'b', 'a', 'd', 'd', '1', '3', '2', 'p', 's', '\0', // "vfmsubadd132ps"
'v', 'f', 'm', 's', 'u', 'b', 'a', 'd', 'd', '2', '1', '3', 'p', 's', '\0', // "vfmsubadd213ps"
'v', 'f', 'm', 's', 'u', 'b', 'a', 'd', 'd', '2', '3', '1', 'p', 's', '\0', // "vfmsubadd231ps"
'v', 'f', 'm', 's', 'u', 'b', '1', '3', '2', 'p', 'd', '\0', // "vfmsub132pd"
'v', 'f', 'm', 's', 'u', 'b', '2', '1', '3', 'p', 'd', '\0', // "vfmsub213pd"
'v', 'f', 'm', 's', 'u', 'b', '2', '3', '1', 'p', 'd', '\0', // "vfmsub231pd"
'v', 'f', 'm', 's', 'u', 'b', '1', '3', '2', 'p', 's', '\0', // "vfmsub132ps"
'v', 'f', 'm', 's', 'u', 'b', '2', '1', '3', 'p', 's', '\0', // "vfmsub213ps"
'v', 'f', 'm', 's', 'u', 'b', '2', '3', '1', 'p', 's', '\0', // "vfmsub231ps"
'v', 'f', 'm', 's', 'u', 'b', '1', '3', '2', 's', 'd', '\0', // "vfmsub132sd"
'v', 'f', 'm', 's', 'u', 'b', '2', '1', '3', 's', 'd', '\0', // "vfmsub213sd"
'v', 'f', 'm', 's', 'u', 'b', '2', '3', '1', 's', 'd', '\0', // "vfmsub231sd"
'v', 'f', 'm', 's', 'u', 'b', '1', '3', '2', 's', 's', '\0', // "vfmsub132ss"
'v', 'f', 'm', 's', 'u', 'b', '2', '1', '3', 's', 's', '\0', // "vfmsub213ss"
'v', 'f', 'm', 's', 'u', 'b', '2', '3', '1', 's', 's', '\0', // "vfmsub231ss"
'v', 'f', 'n', 'm', 'a', 'd', 'd', '1', '3', '2', 'p', 'd', '\0', // "vfnmadd132pd"
'v', 'f', 'n', 'm', 'a', 'd', 'd', '2', '1', '3', 'p', 'd', '\0', // "vfnmadd213pd"
'v', 'f', 'n', 'm', 'a', 'd', 'd', '2', '3', '1', 'p', 'd', '\0', // "vfnmadd231pd"
'v', 'f', 'n', 'm', 'a', 'd', 'd', '1', '3', '2', 'p', 's', '\0', // "vfnmadd132ps"
'v', 'f', 'n', 'm', 'a', 'd', 'd', '2', '1', '3', 'p', 's', '\0', // "vfnmadd213ps"
'v', 'f', 'n', 'm', 'a', 'd', 'd', '2', '3', '1', 'p', 's', '\0', // "vfnmadd231ps"
'v', 'f', 'n', 'm', 'a', 'd', 'd', '1', '3', '2', 's', 'd', '\0', // "vfnmadd132sd"
'v', 'f', 'n', 'm', 'a', 'd', 'd', '2', '1', '3', 's', 'd', '\0', // "vfnmadd213sd"
'v', 'f', 'n', 'm', 'a', 'd', 'd', '2', '3', '1', 's', 'd', '\0', // "vfnmadd231sd"
'v', 'f', 'n', 'm', 'a', 'd', 'd', '1', '3', '2', 's', 's', '\0', // "vfnmadd132ss"
'v', 'f', 'n', 'm', 'a', 'd', 'd', '2', '1', '3', 's', 's', '\0', // "vfnmadd213ss"
'v', 'f', 'n', 'm', 'a', 'd', 'd', '2', '3', '1', 's', 's', '\0', // "vfnmadd231ss"
'v', 'f', 'n', 'm', 's', 'u', 'b', '1', '3', '2', 'p', 'd', '\0', // "vfnmsub132pd"
'v', 'f', 'n', 'm', 's', 'u', 'b', '2', '1', '3', 'p', 'd', '\0', // "vfnmsub213pd"
'v', 'f', 'n', 'm', 's', 'u', 'b', '2', '3', '1', 'p', 'd', '\0', // "vfnmsub231pd"
'v', 'f', 'n', 'm', 's', 'u', 'b', '1', '3', '2', 'p', 's', '\0', // "vfnmsub132ps"
'v', 'f', 'n', 'm', 's', 'u', 'b', '2', '1', '3', 'p', 's', '\0', // "vfnmsub213ps"
'v', 'f', 'n', 'm', 's', 'u', 'b', '2', '3', '1', 'p', 's', '\0', // "vfnmsub231ps"
'v', 'f', 'n', 'm', 's', 'u', 'b', '1', '3', '2', 's', 'd', '\0', // "vfnmsub132sd"
'v', 'f', 'n', 'm', 's', 'u', 'b', '2', '1', '3', 's', 'd', '\0', // "vfnmsub213sd"
'v', 'f', 'n', 'm', 's', 'u', 'b', '2', '3', '1', 's', 'd', '\0', // "vfnmsub231sd"
'v', 'f', 'n', 'm', 's', 'u', 'b', '1', '3', '2', 's', 's', '\0', // "vfnmsub132ss"
'v', 'f', 'n', 'm', 's', 'u', 'b', '2', '1', '3', 's', 's', '\0', // "vfnmsub213ss"
'v', 'f', 'n', 'm', 's', 'u', 'b', '2', '3', '1', 's', 's', '\0', // "vfnmsub231ss"
'a', 'n', 'd', 'n', '\0', // "andn"
'b', 'e', 'x', 't', 'r', '\0', // "bextr"
'b', 'l', 's', 'i', '\0', // "blsi"
'b', 'l', 's', 'm', 's', 'k', '\0', // "blsmsk"
'b', 'l', 's', 'r', '\0', // "blsr"
'b', 'z', 'h', 'i', '\0', // "bzhi"
'm', 'u', 'l', 'x', '\0', // "mulx"
'p', 'd', 'e', 'p', '\0', // "pdep"
'p', 'e', 'x', 't', '\0', // "pext"
'r', 'o', 'r', 'x', '\0', // "rorx"
's', 'a', 'r', 'x', '\0', // "sarx"
's', 'h', 'l', 'x', '\0', // "shlx"
's', 'h', 'r', 'x', '\0', // "shrx"
'v', 'p', 'd', 'p', 'b', 'u', 's', 'd', '\0', // "vpdpbusd"
'v', 'p', 'd', 'p', 'b', 'u', 's', 'd', 's', '\0', // "vpdpbusds"
'v', 'p', 'd', 'p', 'w', 's', 's', 'd', '\0', // "vpdpwssd"
'v', 'p', 'd', 'p', 'w', 's', 's', 'd', 's', '\0', // "vpdpwssds"
'v', 'p', 'd', 'p', 'w', 's', 'u', 'd', '\0', // "vpdpwsud"
'v', 'p', 'd', 'p', 'w', 's', 'u', 'd', 's', '\0', // "vpdpwsuds"
'v', 'p', 'd', 'p', 'w', 'u', 's', 'd', '\0', // "vpdpwusd"
'v', 'p', 'd', 'p', 'w', 'u', 's', 'd', 's', '\0', // "vpdpwusds"
'v', 'p', 'd', 'p', 'w', 'u', 'u', 'd', '\0', // "vpdpwuud"
'v', 'p', 'd', 'p', 'w', 'u', 'u', 'd', 's', '\0', // "vpdpwuuds"
'v', 'p', 'd', 'p', 'b', 's', 's', 'd', '\0', // "vpdpbssd"
'v', 'p', 'd', 'p', 'b', 's', 's', 'd', 's', '\0', // "vpdpbssds"
'v', 'p', 'd', 'p', 'b', 's', 'u', 'd', '\0', // "vpdpbsud"
'v', 'p', 'd', 'p', 'b', 's', 'u', 'd', 's', '\0', // "vpdpbsuds"
'v', 'p', 'd', 'p', 'b', 'u', 'u', 'd', '\0', // "vpdpbuud"
'v', 'p', 'd', 'p', 'b', 'u', 'u', 'd', 's', '\0', // "vpdpbuuds"
'v', 'b', 'm', 'a', 'c', 'o', 'r', '1', '6', 'x', '1', '6', 'x', '1', '6', '\0', // "vbmacor16x16x16"
'v', 'b', 'm', 'a', 'c', 'x', 'o', 'r', '1', '6', 'x', '1', '6', 'x', '1', '6', '\0', // "vbmacxor16x16x16"
'v', 'b', 'i', 't', 'r', 'e', 'v', '\0', // "vbitrev"
'v', 'p', 'm', 'a', 'd', 'd', '5', '2', 'h', 'u', 'q', '\0', // "vpmadd52huq"
'v', 'p', 'm', 'a', 'd', 'd', '5', '2', 'l', 'u', 'q', '\0', // "vpmadd52luq"
'k', 'a', 'd', 'd', 'b', '\0', // "kaddb"
'k', 'a', 'd', 'd', 'd', '\0', // "kaddd"
'k', 'a', 'd', 'd', 'q', '\0', // "kaddq"
'k', 'a', 'd', 'd', 'w', '\0', // "kaddw"
'k', 'a', 'n', 'd', 'b', '\0', // "kandb"
'k', 'a', 'n', 'd', 'd', '\0', // "kandd"
'k', 'a', 'n', 'd', 'n', 'b', '\0', // "kandnb"
'k', 'a', 'n', 'd', 'n', 'd', '\0', // "kandnd"
'k', 'a', 'n', 'd', 'n', 'q', '\0', // "kandnq"
'k', 'a', 'n', 'd', 'n', 'w', '\0', // "kandnw"
'k', 'a', 'n', 'd', 'q', '\0', // "kandq"
'k', 'a', 'n', 'd', 'w', '\0', // "kandw"
'k', 'm', 'o', 'v', 'b', '\0', // "kmovb"
'k', 'm', 'o', 'v', 'd', '\0', // "kmovd"
'k', 'm', 'o', 'v', 'q', '\0', // "kmovq"
'k', 'm', 'o', 'v', 'w', '\0', // "kmovw"
'k', 'n', 'o', 't', 'b', '\0', // "knotb"
'k', 'n', 'o', 't', 'd', '\0', // "knotd"
'k', 'n', 'o', 't', 'q', '\0', // "knotq"
'k', 'n', 'o', 't', 'w', '\0', // "knotw"
'k', 'o', 'r', 'b', '\0', // "korb"
'k', 'o', 'r', 'd', '\0', // "kord"
'k', 'o', 'r', 'q', '\0', // "korq"
'k', 'o', 'r', 't', 'e', 's', 't', 'b', '\0', // "kortestb"
'k', 'o', 'r', 't', 'e', 's', 't', 'd', '\0', // "kortestd"
'k', 'o', 'r', 't', 'e', 's', 't', 'q', '\0', // "kortestq"
'k', 'o', 'r', 't', 'e', 's', 't', 'w', '\0', // "kortestw"
'k', 'o', 'r', 'w', '\0', // "korw"
'k', 's', 'h', 'i', 'f', 't', 'l', 'b', '\0', // "kshiftlb"
'k', 's', 'h', 'i', 'f', 't', 'l', 'd', '\0', // "kshiftld"
'k', 's', 'h', 'i', 'f', 't', 'l', 'q', '\0', // "kshiftlq"
'k', 's', 'h', 'i', 'f', 't', 'l', 'w', '\0', // "kshiftlw"
'k', 's', 'h', 'i', 'f', 't', 'r', 'b', '\0', // "kshiftrb"
'k', 's', 'h', 'i', 'f', 't', 'r', 'd', '\0', // "kshiftrd"
'k', 's', 'h', 'i', 'f', 't', 'r', 'q', '\0', // "kshiftrq"
'k', 's', 'h', 'i', 'f', 't', 'r', 'w', '\0', // "kshiftrw"
'k', 't', 'e', 's', 't', 'b', '\0', // "ktestb"
'k', 't', 'e', 's', 't', 'd', '\0', // "ktestd"
'k', 't', 'e', 's', 't', 'q', '\0', // "ktestq"
'k', 't', 'e', 's', 't', 'w', '\0', // "ktestw"
'k', 'u', 'n', 'p', 'c', 'k', 'b', 'w', '\0', // "kunpckbw"
'k', 'u', 'n', 'p', 'c', 'k', 'd', 'q', '\0', // "kunpckdq"
'k', 'u', 'n', 'p', 'c', 'k', 'w', 'd', '\0', // "kunpckwd"
'k', 'x', 'n', 'o', 'r', 'b', '\0', // "kxnorb"
'k', 'x', 'n', 'o', 'r', 'd', '\0', // "kxnord"
'k', 'x', 'n', 'o', 'r', 'q', '\0', // "kxnorq"
'k', 'x', 'n', 'o', 'r', 'w', '\0', // "kxnorw"
'k', 'x', 'o', 'r', 'b', '\0', // "kxorb"
'k', 'x', 'o', 'r', 'd', '\0', // "kxord"
'k', 'x', 'o', 'r', 'q', '\0', // "kxorq"
'k', 'x', 'o', 'r', 'w', '\0', // "kxorw"
'v', 'a', 'l', 'i', 'g', 'n', 'd', '\0', // "valignd"
'v', 'a', 'l', 'i', 'g', 'n', 'q', '\0', // "valignq"
'v', 'b', 'l', 'e', 'n', 'd', 'm', 'p', 'd', '\0', // "vblendmpd"
'v', 'b', 'l', 'e', 'n', 'd', 'm', 'p', 's', '\0', // "vblendmps"
'v', 'b', 'r', 'o', 'a', 'd', 'c', 'a', 's', 't', 'f', '3', '2', 'x', '2', '\0', // "vbroadcastf32x2"
'v', 'b', 'r', 'o', 'a', 'd', 'c', 'a', 's', 't', 'f', '3', '2', 'x', '8', '\0', // "vbroadcastf32x8"
'v', 'b', 'r', 'o', 'a', 'd', 'c', 'a', 's', 't', 'f', '6', '4', 'x', '2', '\0', // "vbroadcastf64x2"
'v', 'b', 'r', 'o', 'a', 'd', 'c', 'a', 's', 't', 'f', '6', '4', 'x', '4', '\0', // "vbroadcastf64x4"
'v', 'b', 'r', 'o', 'a', 'd', 'c', 'a', 's', 't', 'i', '3', '2', 'x', '2', '\0', // "vbroadcasti32x2"
'v', 'b', 'r', 'o', 'a', 'd', 'c', 'a', 's', 't', 'i', '3', '2', 'x', '8', '\0', // "vbroadcasti32x8"
'v', 'b', 'r', 'o', 'a', 'd', 'c', 'a', 's', 't', 'i', '6', '4', 'x', '2', '\0', // "vbroadcasti64x2"
'v', 'b', 'r', 'o', 'a', 'd', 'c', 'a', 's', 't', 'i', '6', '4', 'x', '4', '\0', // "vbroadcasti64x4"
'v', 'c', 'o', 'm', 'p', 'r', 'e', 's', 's', 'p', 'd', '\0', // "vcompresspd"
'v', 'c', 'o', 'm', 'p', 'r', 'e', 's', 's', 'p', 's', '\0', // "vcompressps"
'v', 'c', 'v', 't', 'p', 'd', '2', 'q', 'q', '\0', // "vcvtpd2qq"
'v', 'c', 'v', 't', 'p', 'd', '2', 'u', 'd', 'q', '\0', // "vcvtpd2udq"
'v', 'c', 'v', 't', 'p', 'd', '2', 'u', 'q', 'q', '\0', // "vcvtpd2uqq"
'v', 'c', 'v', 't', 'p', 's', '2', 'q', 'q', '\0', // "vcvtps2qq"
'v', 'c', 'v', 't', 'p', 's', '2', 'u', 'd', 'q', '\0', // "vcvtps2udq"
'v', 'c', 'v', 't', 'p', 's', '2', 'u', 'q', 'q', '\0', // "vcvtps2uqq"
'v', 'c', 'v', 't', 'q', 'q', '2', 'p', 'd', '\0', // "vcvtqq2pd"
'v', 'c', 'v', 't', 'q', 'q', '2', 'p', 's', '\0', // "vcvtqq2ps"
'v', 'c', 'v', 't', 's', 'd', '2', 'u', 's', 'i', '\0', // "vcvtsd2usi"
'v', 'c', 'v', 't', 's', 's', '2', 'u', 's', 'i', '\0', // "vcvtss2usi"
'v', 'c', 'v', 't', 't', 'p', 'd', '2', 'q', 'q', '\0', // "vcvttpd2qq"
'v', 'c', 'v', 't', 't', 'p', 'd', '2', 'u', 'd', 'q', '\0', // "vcvttpd2udq"
'v', 'c', 'v', 't', 't', 'p', 'd', '2', 'u', 'q', 'q', '\0', // "vcvttpd2uqq"
'v', 'c', 'v', 't', 't', 'p', 's', '2', 'q', 'q', '\0', // "vcvttps2qq"
'v', 'c', 'v', 't', 't', 'p', 's', '2', 'u', 'd', 'q', '\0', // "vcvttps2udq"
'v', 'c', 'v', 't', 't', 'p', 's', '2', 'u', 'q', 'q', '\0', // "vcvttps2uqq"
'v', 'c', 'v', 't', 't', 's', 'd', '2', 'u', 's', 'i', '\0', // "vcvttsd2usi"
'v', 'c', 'v', 't', 't', 's', 's', '2', 'u', 's', 'i', '\0', // "vcvttss2usi"
'v', 'c', 'v', 't', 'u', 'd', 'q', '2', 'p', 'd', '\0', // "vcvtudq2pd"
'v', 'c', 'v', 't', 'u', 'd', 'q', '2', 'p', 's', '\0', // "vcvtudq2ps"
'v', 'c', 'v', 't', 'u', 'q', 'q', '2', 'p', 'd', '\0', // "vcvtuqq2pd"
'v', 'c', 'v', 't', 'u', 'q', 'q', '2', 'p', 's', '\0', // "vcvtuqq2ps"
'v', 'c', 'v', 't', 'u', 's', 'i', '2', 's', 'd', '\0', // "vcvtusi2sd"
'v', 'c', 'v', 't', 'u', 's', 'i', '2', 's', 's', '\0', // "vcvtusi2ss"
'v', 'd', 'b', 'p', 's', 'a', 'd', 'b', 'w', '\0', // "vdbpsadbw"
'v', 'e', 'x', 'p', 'a', 'n', 'd', 'p', 'd', '\0', // "vexpandpd"
'v', 'e', 'x', 'p', 'a', 'n', 'd', 'p', 's', '\0', // "vexpandps"
'v', 'e', 'x', 't', 'r', 'a', 'c', 't', 'f', '3', '2', 'x', '8', '\0', // "vextractf32x8"
'v', 'e', 'x', 't', 'r', 'a', 'c', 't', 'f', '6', '4', 'x', '2', '\0', // "vextractf64x2"
'v', 'e', 'x', 't', 'r', 'a', 'c', 't', 'f', '6', '4', 'x', '4', '\0', // "vextractf64x4"
'v', 'e', 'x', 't', 'r', 'a', 'c', 't', 'i', '3', '2', 'x', '8', '\0', // "vextracti32x8"
'v', 'e', 'x', 't', 'r', 'a', 'c', 't', 'i', '6', '4', 'x', '2', '\0', // "vextracti64x2"
'v', 'e', 'x', 't', 'r', 'a', 'c', 't', 'i', '6', '4', 'x', '4', '\0', // "vextracti64x4"
'v', 'f', 'i', 'x', 'u', 'p', 'i', 'm', 'm', 'p', 'd', '\0', // "vfixupimmpd"
'v', 'f', 'i', 'x', 'u', 'p', 'i', 'm', 'm', 'p', 's', '\0', // "vfixupimmps"
'v', 'f', 'i', 'x', 'u', 'p', 'i', 'm', 'm', 's', 'd', '\0', // "vfixupimmsd"
'v', 'f', 'i', 'x', 'u', 'p', 'i', 'm', 'm', 's', 's', '\0', // "vfixupimmss"
'v', 'f', 'p', 'c', 'l', 'a', 's', 's', 'p', 'd', '\0', // "vfpclasspd"
'v', 'f', 'p', 'c', 'l', 'a', 's', 's', 'p', 's', '\0', // "vfpclassps"
'v', 'f', 'p', 'c', 'l', 'a', 's', 's', 's', 'd', '\0', // "vfpclasssd"
'v', 'f', 'p', 'c', 'l', 'a', 's', 's', 's', 's', '\0', // "vfpclassss"
'v', 'g', 'e', 't', 'e', 'x', 'p', 'p', 'd', '\0', // "vgetexppd"
'v', 'g', 'e', 't', 'e', 'x', 'p', 'p', 's', '\0', // "vgetexpps"
'v', 'g', 'e', 't', 'e', 'x', 'p', 's', 'd', '\0', // "vgetexpsd"
'v', 'g', 'e', 't', 'e', 'x', 'p', 's', 's', '\0', // "vgetexpss"
'v', 'g', 'e', 't', 'm', 'a', 'n', 't', 'p', 'd', '\0', // "vgetmantpd"
'v', 'g', 'e', 't', 'm', 'a', 'n', 't', 'p', 's', '\0', // "vgetmantps"
'v', 'g', 'e', 't', 'm', 'a', 'n', 't', 's', 'd', '\0', // "vgetmantsd"
'v', 'g', 'e', 't', 'm', 'a', 'n', 't', 's', 's', '\0', // "vgetmantss"
'v', 'i', 'n', 's', 'e', 'r', 't', 'f', '3', '2', 'x', '8', '\0', // "vinsertf32x8"
'v', 'i', 'n', 's', 'e', 'r', 't', 'f', '6', '4', 'x', '2', '\0', // "vinsertf64x2"
'v', 'i', 'n', 's', 'e', 'r', 't', 'f', '6', '4', 'x', '4', '\0', // "vinsertf64x4"
'v', 'i', 'n', 's', 'e', 'r', 't', 'i', '3', '2', 'x', '8', '\0', // "vinserti32x8"
'v', 'i', 'n', 's', 'e', 'r', 't', 'i', '6', '4', 'x', '2', '\0', // "vinserti64x2"
'v', 'i', 'n', 's', 'e', 'r', 't', 'i', '6', '4', 'x', '4', '\0', // "vinserti64x4"
'v', 'm', 'o', 'v', 'd', 'q', 'a', '6', '4', '\0', // "vmovdqa64"
'v', 'm', 'o', 'v', 'd', 'q', 'u', '1', '6', '\0', // "vmovdqu16"
'v', 'm', 'o', 'v', 'd', 'q', 'u', '6', '4', '\0', // "vmovdqu64"
'v', 'm', 'o', 'v', 'd', 'q', 'u', '8', '\0', // "vmovdqu8"
'v', 'p', 'a', 'b', 's', 'q', '\0', // "vpabsq"
'v', 'p', 'a', 'n', 'd', 'n', 'q', '\0', // "vpandnq"
'v', 'p', 'a', 'n', 'd', 'q', '\0', // "vpandq"
'v', 'p', 'b', 'l', 'e', 'n', 'd', 'm', 'b', '\0', // "vpblendmb"
'v', 'p', 'b', 'l', 'e', 'n', 'd', 'm', 'd', '\0', // "vpblendmd"
'v', 'p', 'b', 'l', 'e', 'n', 'd', 'm', 'q', '\0', // "vpblendmq"
'v', 'p', 'b', 'l', 'e', 'n', 'd', 'm', 'w', '\0', // "vpblendmw"
'v', 'p', 'c', 'm', 'p', 'b', '\0', // "vpcmpb"
'v', 'p', 'c', 'm', 'p', 'd', '\0', // "vpcmpd"
'v', 'p', 'c', 'm', 'p', 'q', '\0', // "vpcmpq"
'v', 'p', 'c', 'm', 'p', 'u', 'b', '\0', // "vpcmpub"
'v', 'p', 'c', 'm', 'p', 'u', 'd', '\0', // "vpcmpud"
'v', 'p', 'c', 'm', 'p', 'u', 'q', '\0', // "vpcmpuq"
'v', 'p', 'c', 'm', 'p', 'u', 'w', '\0', // "vpcmpuw"
'v', 'p', 'c', 'm', 'p', 'w', '\0', // "vpcmpw"
'v', 'p', 'c', 'o', 'm', 'p', 'r', 'e', 's', 's', 'd', '\0', // "vpcompressd"
'v', 'p', 'c', 'o', 'm', 'p', 'r', 'e', 's', 's', 'q', '\0', // "vpcompressq"
'v', 'p', 'c', 'o', 'n', 'f', 'l', 'i', 'c', 't', 'd', '\0', // "vpconflictd"
'v', 'p', 'c', 'o', 'n', 'f', 'l', 'i', 'c', 't', 'q', '\0', // "vpconflictq"
'v', 'p', 'e', 'r', 'm', 'i', '2', 'd', '\0', // "vpermi2d"
'v', 'p', 'e', 'r', 'm', 'i', '2', 'p', 'd', '\0', // "vpermi2pd"
'v', 'p', 'e', 'r', 'm', 'i', '2', 'p', 's', '\0', // "vpermi2ps"
'v', 'p', 'e', 'r', 'm', 'i', '2', 'q', '\0', // "vpermi2q"
'v', 'p', 'e', 'r', 'm', 'i', '2', 'w', '\0', // "vpermi2w"
'v', 'p', 'e', 'r', 'm', 't', '2', 'd', '\0', // "vpermt2d"
'v', 'p', 'e', 'r', 'm', 't', '2', 'p', 'd', '\0', // "vpermt2pd"
'v', 'p', 'e', 'r', 'm', 't', '2', 'p', 's', '\0', // "vpermt2ps"
'v', 'p', 'e', 'r', 'm', 't', '2', 'q', '\0', // "vpermt2q"
'v', 'p', 'e', 'r', 'm', 't', '2', 'w', '\0', // "vpermt2w"
'v', 'p', 'e', 'r', 'm', 'w', '\0', // "vpermw"
'v', 'p', 'e', 'x', 'p', 'a', 'n', 'd', 'd', '\0', // "vpexpandd"
'v', 'p', 'e', 'x', 'p', 'a', 'n', 'd', 'q', '\0', // "vpexpandq"
'v', 'p', 'l', 'z', 'c', 'n', 't', 'd', '\0', // "vplzcntd"
'v', 'p', 'l', 'z', 'c', 'n', 't', 'q', '\0', // "vplzcntq"
'v', 'p', 'm', 'a', 'x', 's', 'q', '\0', // "vpmaxsq"
'v', 'p', 'm', 'a', 'x', 'u', 'q', '\0', // "vpmaxuq"
'v', 'p', 'm', 'i', 'n', 's', 'q', '\0', // "vpminsq"
'v', 'p', 'm', 'i', 'n', 'u', 'q', '\0', // "vpminuq"
'v', 'p', 'm', 'o', 'v', 'b', '2', 'm', '\0', // "vpmovb2m"
'v', 'p', 'm', 'o', 'v', 'd', '2', 'm', '\0', // "vpmovd2m"
'v', 'p', 'm', 'o', 'v', 'd', 'b', '\0', // "vpmovdb"
'v', 'p', 'm', 'o', 'v', 'd', 'w', '\0', // "vpmovdw"
'v', 'p', 'm', 'o', 'v', 'm', '2', 'b', '\0', // "vpmovm2b"
'v', 'p', 'm', 'o', 'v', 'm', '2', 'd', '\0', // "vpmovm2d"
'v', 'p', 'm', 'o', 'v', 'm', '2', 'q', '\0', // "vpmovm2q"
'v', 'p', 'm', 'o', 'v', 'm', '2', 'w', '\0', // "vpmovm2w"
'v', 'p', 'm', 'o', 'v', 'q', '2', 'm', '\0', // "vpmovq2m"
'v', 'p', 'm', 'o', 'v', 'q', 'b', '\0', // "vpmovqb"
'v', 'p', 'm', 'o', 'v', 'q', 'd', '\0', // "vpmovqd"
'v', 'p', 'm', 'o', 'v', 'q', 'w', '\0', // "vpmovqw"
'v', 'p', 'm', 'o', 'v', 's', 'd', 'b', '\0', // "vpmovsdb"
'v', 'p', 'm', 'o', 'v', 's', 'd', 'w', '\0', // "vpmovsdw"
'v', 'p', 'm', 'o', 'v', 's', 'q', 'b', '\0', // "vpmovsqb"
'v', 'p', 'm', 'o', 'v', 's', 'q', 'd', '\0', // "vpmovsqd"
'v', 'p', 'm', 'o', 'v', 's', 'q', 'w', '\0', // "vpmovsqw"
'v', 'p', 'm', 'o', 'v', 's', 'w', 'b', '\0', // "vpmovswb"
'v', 'p', 'm', 'o', 'v', 'u', 's', 'd', 'b', '\0', // "vpmovusdb"
'v', 'p', 'm', 'o', 'v', 'u', 's', 'd', 'w', '\0', // "vpmovusdw"
'v', 'p', 'm', 'o', 'v', 'u', 's', 'q', 'b', '\0', // "vpmovusqb"
'v', 'p', 'm', 'o', 'v', 'u', 's', 'q', 'd', '\0', // "vpmovusqd"
'v', 'p', 'm', 'o', 'v', 'u', 's', 'q', 'w', '\0', // "vpmovusqw"
'v', 'p', 'm', 'o', 'v', 'u', 's', 'w', 'b', '\0', // "vpmovuswb"
'v', 'p', 'm', 'o', 'v', 'w', '2', 'm', '\0', // "vpmovw2m"
'v', 'p', 'm', 'o', 'v', 'w', 'b', '\0', // "vpmovwb"
'v', 'p', 'm', 'u', 'l', 'l', 'q', '\0', // "vpmullq"
'v', 'p', 'o', 'r', 'q', '\0', // "vporq"
'v', 'p', 'r', 'o', 'l', 'd', '\0', // "vprold"
'v', 'p', 'r', 'o', 'l', 'q', '\0', // "vprolq"
'v', 'p', 'r', 'o', 'l', 'v', 'd', '\0', // "vprolvd"
'v', 'p', 'r', 'o', 'l', 'v', 'q', '\0', // "vprolvq"
'v', 'p', 'r', 'o', 'r', 'd', '\0', // "vprord"
'v', 'p', 'r', 'o', 'r', 'q', '\0', // "vprorq"
'v', 'p', 'r', 'o', 'r', 'v', 'd', '\0', // "vprorvd"
'v', 'p', 'r', 'o', 'r', 'v', 'q', '\0', // "vprorvq"
'v', 'p', 's', 'c', 'a', 't', 't', 'e', 'r', 'd', 'd', '\0', // "vpscatterdd"
'v', 'p', 's', 'c', 'a', 't', 't', 'e', 'r', 'd', 'q', '\0', // "vpscatterdq"
'v', 'p', 's', 'c', 'a', 't', 't', 'e', 'r', 'q', 'd', '\0', // "vpscatterqd"
'v', 'p', 's', 'c', 'a', 't', 't', 'e', 'r', 'q', 'q', '\0', // "vpscatterqq"
'v', 'p', 's', 'l', 'l', 'v', 'w', '\0', // "vpsllvw"
'v', 'p', 's', 'r', 'a', 'q', '\0', // "vpsraq"
'v', 'p', 's', 'r', 'a', 'v', 'q', '\0', // "vpsravq"
'v', 'p', 's', 'r', 'a', 'v', 'w', '\0', // "vpsravw"
'v', 'p', 's', 'r', 'l', 'v', 'w', '\0', // "vpsrlvw"
'v', 'p', 't', 'e', 'r', 'n', 'l', 'o', 'g', 'd', '\0', // "vpternlogd"
'v', 'p', 't', 'e', 'r', 'n', 'l', 'o', 'g', 'q', '\0', // "vpternlogq"
'v', 'p', 't', 'e', 's', 't', 'm', 'b', '\0', // "vptestmb"
'v', 'p', 't', 'e', 's', 't', 'm', 'd', '\0', // "vptestmd"
'v', 'p', 't', 'e', 's', 't', 'm', 'q', '\0', // "vptestmq"
'v', 'p', 't', 'e', 's', 't', 'm', 'w', '\0', // "vptestmw"
'v', 'p', 't', 'e', 's', 't', 'n', 'm', 'b', '\0', // "vptestnmb"
'v', 'p', 't', 'e', 's', 't', 'n', 'm', 'd', '\0', // "vptestnmd"
'v', 'p', 't', 'e', 's', 't', 'n', 'm', 'q', '\0', // "vptestnmq"
'v', 'p', 't', 'e', 's', 't', 'n', 'm', 'w', '\0', // "vptestnmw"
'v', 'p', 'x', 'o', 'r', 'q', '\0', // "vpxorq"
'v', 'r', 'a', 'n', 'g', 'e', 'p', 'd', '\0', // "vrangepd"
'v', 'r', 'a', 'n', 'g', 'e', 'p', 's', '\0', // "vrangeps"
'v', 'r', 'a', 'n', 'g', 'e', 's', 'd', '\0', // "vrangesd"
'v', 'r', 'a', 'n', 'g', 'e', 's', 's', '\0', // "vrangess"
'v', 'r', 'c', 'p', '1', '4', 'p', 'd', '\0', // "vrcp14pd"
'v', 'r', 'c', 'p', '1', '4', 'p', 's', '\0', // "vrcp14ps"
'v', 'r', 'c', 'p', '1', '4', 's', 'd', '\0', // "vrcp14sd"
'v', 'r', 'c', 'p', '1', '4', 's', 's', '\0', // "vrcp14ss"
'v', 'r', 'e', 'd', 'u', 'c', 'e', 'p', 'd', '\0', // "vreducepd"
'v', 'r', 'e', 'd', 'u', 'c', 'e', 'p', 's', '\0', // "vreduceps"
'v', 'r', 'e', 'd', 'u', 'c', 'e', 's', 'd', '\0', // "vreducesd"
'v', 'r', 'e', 'd', 'u', 'c', 'e', 's', 's', '\0', // "vreducess"
'v', 'r', 'n', 'd', 's', 'c', 'a', 'l', 'e', 'p', 'd', '\0', // "vrndscalepd"
'v', 'r', 'n', 'd', 's', 'c', 'a', 'l', 'e', 'p', 's', '\0', // "vrndscaleps"
'v', 'r', 'n', 'd', 's', 'c', 'a', 'l', 'e', 's', 'd', '\0', // "vrndscalesd"
'v', 'r', 'n', 'd', 's', 'c', 'a', 'l', 'e', 's', 's', '\0', // "vrndscaless"
'v', 'r', 's', 'q', 'r', 't', '1', '4', 'p', 'd', '\0', // "vrsqrt14pd"
'v', 'r', 's', 'q', 'r', 't', '1', '4', 'p', 's', '\0', // "vrsqrt14ps"
'v', 'r', 's', 'q', 'r', 't', '1', '4', 's', 'd', '\0', // "vrsqrt14sd"
'v', 'r', 's', 'q', 'r', 't', '1', '4', 's', 's', '\0', // "vrsqrt14ss"
'v', 's', 'c', 'a', 'l', 'e', 'f', 'p', 'd', '\0', // "vscalefpd"
'v', 's', 'c', 'a', 'l', 'e', 'f', 'p', 's', '\0', // "vscalefps"
'v', 's', 'c', 'a', 'l', 'e', 'f', 's', 'd', '\0', // "vscalefsd"
'v', 's', 'c', 'a', 'l', 'e', 'f', 's', 's', '\0', // "vscalefss"
'v', 's', 'c', 'a', 't', 't', 'e', 'r', 'd', 'p', 'd', '\0', // "vscatterdpd"
'v', 's', 'c', 'a', 't', 't', 'e', 'r', 'd', 'p', 's', '\0', // "vscatterdps"
'v', 's', 'c', 'a', 't', 't', 'e', 'r', 'q', 'p', 'd', '\0', // "vscatterqpd"
'v', 's', 'c', 'a', 't', 't', 'e', 'r', 'q', 'p', 's', '\0', // "vscatterqps"
'v', 's', 'h', 'u', 'f', 'f', '3', '2', 'x', '4', '\0', // "vshuff32x4"
'v', 's', 'h', 'u', 'f', 'f', '6', '4', 'x', '2', '\0', // "vshuff64x2"
'v', 's', 'h', 'u', 'f', 'i', '3', '2', 'x', '4', '\0', // "vshufi32x4"
'v', 's', 'h', 'u', 'f', 'i', '6', '4', 'x', '2', '\0', // "vshufi64x2"
'v', 'p', 'e', 'r', 'm', 'b', '\0', // "vpermb"
'v', 'p', 'e', 'r', 'm', 'i', '2', 'b', '\0', // "vpermi2b"
'v', 'p', 'e', 'r', 'm', 't', '2', 'b', '\0', // "vpermt2b"
'v', 'p', 'm', 'u', 'l', 't', 'i', 's', 'h', 'i', 'f', 't', 'q', 'b', '\0', // "vpmultishiftqb"
'v', 'p', 'c', 'o', 'm', 'p', 'r', 'e', 's', 's', 'b', '\0', // "vpcompressb"
'v', 'p', 'c', 'o', 'm', 'p', 'r', 'e', 's', 's', 'w', '\0', // "vpcompressw"
'v', 'p', 'e', 'x', 'p', 'a', 'n', 'd', 'b', '\0', // "vpexpandb"
'v', 'p', 'e', 'x', 'p', 'a', 'n', 'd', 'w', '\0', // "vpexpandw"
'v', 'p', 'o', 'p', 'c', 'n', 't', 'b', '\0', // "vpopcntb"
'v', 'p', 'o', 'p', 'c', 'n', 't', 'd', '\0', // "vpopcntd"
'v', 'p', 'o', 'p', 'c', 'n', 't', 'q', '\0', // "vpopcntq"
'v', 'p', 'o', 'p', 'c', 'n', 't', 'w', '\0', // "vpopcntw"
'v', 'p', 's', 'h', 'l', 'd', 'd', '\0', // "vpshldd"
'v', 'p', 's', 'h', 'l', 'd', 'q', '\0', // "vpshldq"
'v', 'p', 's', 'h', 'l', 'd', 'v', 'd', '\0', // "vpshldvd"
'v', 'p', 's', 'h', 'l', 'd', 'v', 'q', '\0', // "vpshldvq"
'v', 'p', 's', 'h', 'l', 'd', 'v', 'w', '\0', // "vpshldvw"
'v', 'p', 's', 'h', 'l', 'd', 'w', '\0', // "vpshldw"
'v', 'p', 's', 'h', 'r', 'd', 'd', '\0', // "vpshrdd"
'v', 'p', 's', 'h', 'r', 'd', 'q', '\0', // "vpshrdq"
'v', 'p', 's', 'h', 'r', 'd', 'v', 'd', '\0', // "vpshrdvd"
'v', 'p', 's', 'h', 'r', 'd', 'v', 'q', '\0', // "vpshrdvq"
'v', 'p', 's', 'h', 'r', 'd', 'v', 'w', '\0', // "vpshrdvw"
'v', 'p', 's', 'h', 'r', 'd', 'w', '\0', // "vpshrdw"
'v', 'p', 's', 'h', 'u', 'f', 'b', 'i', 't', 'q', 'm', 'b', '\0', // "vpshufbitqmb"
'v', 'a', 'd', 'd', 'p', 'h', '\0', // "vaddph"
'v', 'a', 'd', 'd', 's', 'h', '\0', // "vaddsh"
'v', 'c', 'm', 'p', 'p', 'h', '\0', // "vcmpph"
'v', 'c', 'm', 'p', 's', 'h', '\0', // "vcmpsh"
'v', 'c', 'o', 'm', 'i', 's', 'h', '\0', // "vcomish"
'v', 'c', 'v', 't', 'd', 'q', '2', 'p', 'h', '\0', // "vcvtdq2ph"
'v', 'c', 'v', 't', 'n', 'e', '2', 'p', 's', '2', 'b', 'f', '1', '6', '\0', // "vcvtne2ps2bf16"
'v', 'c', 'v', 't', 'n', 'e', 'p', 's', '2', 'b', 'f', '1', '6', '\0', // "vcvtneps2bf16"
'v', 'c', 'v', 't', 'p', 'd', '2', 'p', 'h', '\0', // "vcvtpd2ph"
'v', 'c', 'v', 't', 'p', 'h', '2', 'd', 'q', '\0', // "vcvtph2dq"
'v', 'c', 'v', 't', 'p', 'h', '2', 'p', 'd', '\0', // "vcvtph2pd"
'v', 'c', 'v', 't', 'p', 'h', '2', 'p', 's', 'x', '\0', // "vcvtph2psx"
'v', 'c', 'v', 't', 'p', 'h', '2', 'q', 'q', '\0', // "vcvtph2qq"
'v', 'c', 'v', 't', 'p', 'h', '2', 'u', 'd', 'q', '\0', // "vcvtph2udq"
'v', 'c', 'v', 't', 'p', 'h', '2', 'u', 'q', 'q', '\0', // "vcvtph2uqq"
'v', 'c', 'v', 't', 'p', 'h', '2', 'u', 'w', '\0', // "vcvtph2uw"
'v', 'c', 'v', 't', 'p', 'h', '2', 'w', '\0', // "vcvtph2w"
'v', 'c', 'v', 't', 'p', 's', '2', 'p', 'h', 'x', '\0', // "vcvtps2phx"
'v', 'c', 'v', 't', 'q', 'q', '2', 'p', 'h', '\0', // "vcvtqq2ph"
'v', 'c', 'v', 't', 's', 'd', '2', 's', 'h', '\0', // "vcvtsd2sh"
'v', 'c', 'v', 't', 's', 'h', '2', 's', 'd', '\0', // "vcvtsh2sd"
'v', 'c', 'v', 't', 's', 'h', '2', 's', 'i', '\0', // "vcvtsh2si"
'v', 'c', 'v', 't', 's', 'h', '2', 's', 's', '\0', // "vcvtsh2ss"
'v', 'c', 'v', 't', 's', 'h', '2', 'u', 's', 'i', '\0', // "vcvtsh2usi"
'v', 'c', 'v', 't', 's', 'i', '2', 's', 'h', '\0', // "vcvtsi2sh"
'v', 'c', 'v', 't', 's', 's', '2', 's', 'h', '\0', // "vcvtss2sh"
'v', 'c', 'v', 't', 't', 'p', 'h', '2', 'd', 'q', '\0', // "vcvttph2dq"
'v', 'c', 'v', 't', 't', 'p', 'h', '2', 'q', 'q', '\0', // "vcvttph2qq"
'v', 'c', 'v', 't', 't', 'p', 'h', '2', 'u', 'd', 'q', '\0', // "vcvttph2udq"
'v', 'c', 'v', 't', 't', 'p', 'h', '2', 'u', 'q', 'q', '\0', // "vcvttph2uqq"
'v', 'c', 'v', 't', 't', 'p', 'h', '2', 'u', 'w', '\0', // "vcvttph2uw"
'v', 'c', 'v', 't', 't', 'p', 'h', '2', 'w', '\0', // "vcvttph2w"
'v', 'c', 'v', 't', 't', 's', 'h', '2', 's', 'i', '\0', // "vcvttsh2si"
'v', 'c', 'v', 't', 't', 's', 'h', '2', 'u', 's', 'i', '\0', // "vcvttsh2usi"
'v', 'c', 'v', 't', 'u', 'd', 'q', '2', 'p', 'h', '\0', // "vcvtudq2ph"
'v', 'c', 'v', 't', 'u', 'q', 'q', '2', 'p', 'h', '\0', // "vcvtuqq2ph"
'v', 'c', 'v', 't', 'u', 's', 'i', '2', 's', 'h', '\0', // "vcvtusi2sh"
'v', 'c', 'v', 't', 'u', 'w', '2', 'p', 'h', '\0', // "vcvtuw2ph"
'v', 'c', 'v', 't', 'w', '2', 'p', 'h', '\0', // "vcvtw2ph"
'v', 'd', 'i', 'v', 'p', 'h', '\0', // "vdivph"
'v', 'd', 'i', 'v', 's', 'h', '\0', // "vdivsh"
'v', 'd', 'p', 'b', 'f', '1', '6', 'p', 's', '\0', // "vdpbf16ps"
'v', 'f', 'c', 'm', 'a', 'd', 'd', 'c', 'p', 'h', '\0', // "vfcmaddcph"
'v', 'f', 'c', 'm', 'a', 'd', 'd', 'c', 's', 'h', '\0', // "vfcmaddcsh"
'v', 'f', 'c', 'm', 'u', 'l', 'c', 'p', 'h', '\0', // "vfcmulcph"
'v', 'f', 'c', 'm', 'u', 'l', 'c', 's', 'h', '\0', // "vfcmulcsh"
'v', 'f', 'm', 'u', 'l', 'c', 'p', 'h', '\0', // "vfmulcph"
'v', 'f', 'm', 'u', 'l', 'c', 's', 'h', '\0', // "vfmulcsh"
'v', 'f', 'm', 'a', 'd', 'd', '1', '3', '2', 'p', 'h', '\0', // "vfmadd132ph"
'v', 'f', 'm', 'a', 'd', 'd', '2', '1', '3', 'p', 'h', '\0', // "vfmadd213ph"
'v', 'f', 'm', 'a', 'd', 'd', '2', '3', '1', 'p', 'h', '\0', // "vfmadd231ph"
'v', 'f', 'm', 'a', 'd', 'd', '1', '3', '2', 's', 'h', '\0', // "vfmadd132sh"
'v', 'f', 'm', 'a', 'd', 'd', '2', '1', '3', 's', 'h', '\0', // "vfmadd213sh"
'v', 'f', 'm', 'a', 'd', 'd', '2', '3', '1', 's', 'h', '\0', // "vfmadd231sh"
'v', 'f', 'm', 'a', 'd', 'd', 'c', 'p', 'h', '\0', // "vfmaddcph"
'v', 'f', 'm', 'a', 'd', 'd', 'c', 's', 'h', '\0', // "vfmaddcsh"
'v', 'f', 'm', 'a', 'd', 'd', 's', 'u', 'b', '1', '3', '2', 'p', 'h', '\0', // "vfmaddsub132ph"
'v', 'f', 'm', 'a', 'd', 'd', 's', 'u', 'b', '2', '1', '3', 'p', 'h', '\0', // "vfmaddsub213ph"
'v', 'f', 'm', 'a', 'd', 'd', 's', 'u', 'b', '2', '3', '1', 'p', 'h', '\0', // "vfmaddsub231ph"
'v', 'f', 'm', 's', 'u', 'b', '1', '3', '2', 'p', 'h', '\0', // "vfmsub132ph"
'v', 'f', 'm', 's', 'u', 'b', '2', '1', '3', 'p', 'h', '\0', // "vfmsub213ph"
'v', 'f', 'm', 's', 'u', 'b', '2', '3', '1', 'p', 'h', '\0', // "vfmsub231ph"
'v', 'f', 'm', 's', 'u', 'b', '1', '3', '2', 's', 'h', '\0', // "vfmsub132sh"
'v', 'f', 'm', 's', 'u', 'b', '2', '1', '3', 's', 'h', '\0', // "vfmsub213sh"
'v', 'f', 'm', 's', 'u', 'b', '2', '3', '1', 's', 'h', '\0', // "vfmsub231sh"
'v', 'f', 'm', 's', 'u', 'b', 'a', 'd', 'd', '1', '3', '2', 'p', 'h', '\0', // "vfmsubadd132ph"
'v', 'f', 'm', 's', 'u', 'b', 'a', 'd', 'd', '2', '1', '3', 'p', 'h', '\0', // "vfmsubadd213ph"
'v', 'f', 'm', 's', 'u', 'b', 'a', 'd', 'd', '2', '3', '1', 'p', 'h', '\0', // "vfmsubadd231ph"
'v', 'f', 'n', 'm', 'a', 'd', 'd', '1', '3', '2', 'p', 'h', '\0', // "vfnmadd132ph"
'v', 'f', 'n', 'm', 'a', 'd', 'd', '2', '1', '3', 'p', 'h', '\0', // "vfnmadd213ph"
'v', 'f', 'n', 'm', 'a', 'd', 'd', '2', '3', '1', 'p', 'h', '\0', // "vfnmadd231ph"
'v', 'f', 'n', 'm', 'a', 'd', 'd', '1', '3', '2', 's', 'h', '\0', // "vfnmadd132sh"
'v', 'f', 'n', 'm', 'a', 'd', 'd', '2', '1', '3', 's', 'h', '\0', // "vfnmadd213sh"
'v', 'f', 'n', 'm', 'a', 'd', 'd', '2', '3', '1', 's', 'h', '\0', // "vfnmadd231sh"
'v', 'f', 'n', 'm', 's', 'u', 'b', '1', '3', '2', 'p', 'h', '\0', // "vfnmsub132ph"
'v', 'f', 'n', 'm', 's', 'u', 'b', '2', '1', '3', 'p', 'h', '\0', // "vfnmsub213ph"
'v', 'f', 'n', 'm', 's', 'u', 'b', '2', '3', '1', 'p', 'h', '\0', // "vfnmsub231ph"
'v', 'f', 'n', 'm', 's', 'u', 'b', '1', '3', '2', 's', 'h', '\0', // "vfnmsub132sh"
'v', 'f', 'n', 'm', 's', 'u', 'b', '2', '1', '3', 's', 'h', '\0', // "vfnmsub213sh"
'v', 'f', 'n', 'm', 's', 'u', 'b', '2', '3', '1', 's', 'h', '\0', // "vfnmsub231sh"
'v', 'f', 'p', 'c', 'l', 'a', 's', 's', 'p', 'h', '\0', // "vfpclassph"
'v', 'f', 'p', 'c', 'l', 'a', 's', 's', 's', 'h', '\0', // "vfpclasssh"
'v', 'g', 'e', 't', 'e', 'x', 'p', 'p', 'h', '\0', // "vgetexpph"
'v', 'g', 'e', 't', 'e', 'x', 'p', 's', 'h', '\0', // "vgetexpsh"
'v', 'g', 'e', 't', 'm', 'a', 'n', 't', 'p', 'h', '\0', // "vgetmantph"
'v', 'g', 'e', 't', 'm', 'a', 'n', 't', 's', 'h', '\0', // "vgetmantsh"
'v', 'm', 'a', 'x', 'p', 'h', '\0', // "vmaxph"
'v', 'm', 'a', 'x', 's', 'h', '\0', // "vmaxsh"
'v', 'm', 'i', 'n', 's', 'h', '\0', // "vminsh"
'v', 'm', 'i', 'n', 'p', 'h', '\0', // "vminph"
'v', 'm', 'o', 'v', 's', 'h', '\0', // "vmovsh"
'v', 'm', 'o', 'v', 'w', '\0', // "vmovw"
'v', 'm', 'u', 'l', 'p', 'h', '\0', // "vmulph"
'v', 'm', 'u', 'l', 's', 'h', '\0', // "vmulsh"
'v', 'r', 'c', 'p', 'p', 'h', '\0', // "vrcpph"
'v', 'r', 'c', 'p', 's', 'h', '\0', // "vrcpsh"
'v', 'r', 'e', 'd', 'u', 'c', 'e', 'p', 'h', '\0', // "vreduceph"
'v', 'r', 'e', 'd', 'u', 'c', 'e', 's', 'h', '\0', // "vreducesh"
'v', 'r', 'n', 'd', 's', 'c', 'a', 'l', 'e', 'p', 'h', '\0', // "vrndscaleph"
'v', 'r', 'n', 'd', 's', 'c', 'a', 'l', 'e', 's', 'h', '\0', // "vrndscalesh"
'v', 'r', 's', 'q', 'r', 't', 'p', 'h', '\0', // "vrsqrtph"
'v', 'r', 's', 'q', 'r', 't', 's', 'h', '\0', // "vrsqrtsh"
'v', 's', 'c', 'a', 'l', 'e', 'f', 'p', 'h', '\0', // "vscalefph"
'v', 's', 'c', 'a', 'l', 'e', 'f', 's', 'h', '\0', // "vscalefsh"
'v', 's', 'q', 'r', 't', 'p', 'h', '\0', // "vsqrtph"
'v', 's', 'q', 'r', 't', 's', 'h', '\0', // "vsqrtsh"
'v', 's', 'u', 'b', 'p', 'h', '\0', // "vsubph"
'v', 's', 'u', 'b', 's', 'h', '\0', // "vsubsh"
'v', 'u', 'c', 'o', 'm', 'i', 's', 'h', '\0', // "vucomish"
'v', 'p', '2', 'i', 'n', 't', 'e', 'r', 's', 'e', 'c', 't', 'd', '\0', // "vp2intersectd"
'v', 'p', '2', 'i', 'n', 't', 'e', 'r', 's', 'e', 'c', 't', 'q', '\0', // "vp2intersectq"
'v', 'c', 'o', 'm', 'x', 's', 'd', '\0', // "vcomxsd"
'v', 'c', 'o', 'm', 'x', 's', 's', '\0', // "vcomxss"
'v', 'c', 'v', 't', 'p', 's', '2', 'i', 'b', 's', '\0', // "vcvtps2ibs"
'v', 'c', 'v', 't', 'p', 's', '2', 'i', 'u', 'b', 's', '\0', // "vcvtps2iubs"
'v', 'c', 'v', 't', 't', 'p', 'd', '2', 'd', 'q', 's', '\0', // "vcvttpd2dqs"
'v', 'c', 'v', 't', 't', 'p', 'd', '2', 'q', 'q', 's', '\0', // "vcvttpd2qqs"
'v', 'c', 'v', 't', 't', 'p', 'd', '2', 'u', 'd', 'q', 's', '\0', // "vcvttpd2udqs"
'v', 'c', 'v', 't', 't', 'p', 'd', '2', 'u', 'q', 'q', 's', '\0', // "vcvttpd2uqqs"
'v', 'c', 'v', 't', 't', 'p', 's', '2', 'd', 'q', 's', '\0', // "vcvttps2dqs"
'v', 'c', 'v', 't', 't', 'p', 's', '2', 'i', 'b', 's', '\0', // "vcvttps2ibs"
'v', 'c', 'v', 't', 't', 'p', 's', '2', 'i', 'u', 'b', 's', '\0', // "vcvttps2iubs"
'v', 'c', 'v', 't', 't', 'p', 's', '2', 'q', 'q', 's', '\0', // "vcvttps2qqs"
'v', 'c', 'v', 't', 't', 'p', 's', '2', 'u', 'd', 'q', 's', '\0', // "vcvttps2udqs"
'v', 'c', 'v', 't', 't', 'p', 's', '2', 'u', 'q', 'q', 's', '\0', // "vcvttps2uqqs"
'v', 'c', 'v', 't', 't', 's', 'd', '2', 's', 'i', 's', '\0', // "vcvttsd2sis"
'v', 'c', 'v', 't', 't', 's', 'd', '2', 'u', 's', 'i', 's', '\0', // "vcvttsd2usis"
'v', 'c', 'v', 't', 't', 's', 's', '2', 's', 'i', 's', '\0', // "vcvttss2sis"
'v', 'c', 'v', 't', 't', 's', 's', '2', 'u', 's', 'i', 's', '\0', // "vcvttss2usis"
'v', 'm', 'i', 'n', 'm', 'a', 'x', 'p', 'd', '\0', // "vminmaxpd"
'v', 'm', 'i', 'n', 'm', 'a', 'x', 'p', 's', '\0', // "vminmaxps"
'v', 'm', 'i', 'n', 'm', 'a', 'x', 's', 'd', '\0', // "vminmaxsd"
'v', 'm', 'i', 'n', 'm', 'a', 'x', 's', 's', '\0', // "vminmaxss"
'v', 'u', 'c', 'o', 'm', 'x', 's', 'd', '\0', // "vucomxsd"
'v', 'u', 'c', 'o', 'm', 'x', 's', 's', '\0', // "vucomxss"
'c', 'c', 'm', 'p', 'o', '\0', // "ccmpo"
'c', 'c', 'm', 'p', 'n', 'o', '\0', // "ccmpno"
'c', 'c', 'm', 'p', 'b', '\0', // "ccmpb"
'c', 'c', 'm', 'p', 'a', 'e', '\0', // "ccmpae"
'c', 'c', 'm', 'p', 'e', '\0', // "ccmpe"
'c', 'c', 'm', 'p', 'n', 'e', '\0', // "ccmpne"
'c', 'c', 'm', 'p', 'b', 'e', '\0', // "ccmpbe"
'c', 'c', 'm', 'p', 'a', '\0', // "ccmpa"
'c', 'c', 'm', 'p', 's', '\0', // "ccmps"
'c', 'c', 'm', 'p', 'n', 's', '\0', // "ccmpns"
'c', 'c', 'm', 'p', 't', '\0', // "ccmpt"
'c', 'c', 'm', 'p', 'f', '\0', // "ccmpf"
'c', 'c', 'm', 'p', 'l', '\0', // "ccmpl"
'c', 'c', 'm', 'p', 'g', 'e', '\0', // "ccmpge"
'c', 'c', 'm', 'p', 'l', 'e', '\0', // "ccmple"
'c', 'c', 'm', 'p', 'g', '\0', // "ccmpg"
'c', 'f', 'c', 'm', 'o', 'v', 'o', '\0', // "cfcmovo"
'c', 'f', 'c', 'm', 'o', 'v', 'n', 'o', '\0', // "cfcmovno"
'c', 'f', 'c', 'm', 'o', 'v', 'b', '\0', // "cfcmovb"
'c', 'f', 'c', 'm', 'o', 'v', 'a', 'e', '\0', // "cfcmovae"
'c', 'f', 'c', 'm', 'o', 'v', 'e', '\0', // "cfcmove"
'c', 'f', 'c', 'm', 'o', 'v', 'n', 'e', '\0', // "cfcmovne"
'c', 'f', 'c', 'm', 'o', 'v', 'b', 'e', '\0', // "cfcmovbe"
'c', 'f', 'c', 'm', 'o', 'v', 'a', '\0', // "cfcmova"
'c', 'f', 'c', 'm', 'o', 'v', 's', '\0', // "cfcmovs"
'c', 'f', 'c', 'm', 'o', 'v', 'n', 's', '\0', // "cfcmovns"
'c', 'f', 'c', 'm', 'o', 'v', 'p', '\0', // "cfcmovp"
'c', 'f', 'c', 'm', 'o', 'v', 'n', 'p', '\0', // "cfcmovnp"
'c', 'f', 'c', 'm', 'o', 'v', 'l', '\0', // "cfcmovl"
'c', 'f', 'c', 'm', 'o', 'v', 'g', 'e', '\0', // "cfcmovge"
'c', 'f', 'c', 'm', 'o', 'v', 'l', 'e', '\0', // "cfcmovle"
'c', 'f', 'c', 'm', 'o', 'v', 'g', '\0', // "cfcmovg"
'c', 't', 'e', 's', 't', 'o', '\0', // "ctesto"
'c', 't', 'e', 's', 't', 'n', 'o', '\0', // "ctestno"
'c', 't', 'e', 's', 't', 'b', '\0', // "ctestb"
'c', 't', 'e', 's', 't', 'a', 'e', '\0', // "ctestae"
'c', 't', 'e', 's', 't', 'e', '\0', // "cteste"
'c', 't', 'e', 's', 't', 'n', 'e', '\0', // "ctestne"
'c', 't', 'e', 's', 't', 'b', 'e', '\0', // "ctestbe"
'c', 't', 'e', 's', 't', 'a', '\0', // "ctesta"
'c', 't', 'e', 's', 't', 's', '\0', // "ctests"
'c', 't', 'e', 's', 't', 'n', 's', '\0', // "ctestns"
'c', 't', 'e', 's', 't', 't', '\0', // "ctestt"
'c', 't', 'e', 's', 't', 'f', '\0', // "ctestf"
'c', 't', 'e', 's', 't', 'l', '\0', // "ctestl"
'c', 't', 'e', 's', 't', 'g', 'e', '\0', // "ctestge"
'c', 't', 'e', 's', 't', 'l', 'e', '\0', // "ctestle"
'c', 't', 'e', 's', 't', 'g', '\0', // "ctestg"
'c', 'r', 'c', '3', '2', '\0', // "crc32"
'm', 'o', 'v', 'b', 'e', '\0', // "movbe"
's', 'e', 't', 'z', 'u', 'o', '\0', // "setzuo"
's', 'e', 't', 'z', 'u', 'n', 'o', '\0', // "setzuno"
's', 'e', 't', 'z', 'u', 'b', '\0', // "setzub"
's', 'e', 't', 'z', 'u', 'a', 'e', '\0', // "setzuae"
's', 'e', 't', 'z', 'u', 'e', '\0', // "setzue"
's', 'e', 't', 'z', 'u', 'n', 'e', '\0', // "setzune"
's', 'e', 't', 'z', 'u', 'b', 'e', '\0', // "setzube"
's', 'e', 't', 'z', 'u', 'a', '\0', // "setzua"
's', 'e', 't', 'z', 'u', 's', '\0', // "setzus"
's', 'e', 't', 'z', 'u', 'n', 's', '\0', // "setzuns"
's', 'e', 't', 'z', 'u', 'p', '\0', // "setzup"
's', 'e', 't', 'z', 'u', 'n', 'p', '\0', // "setzunp"
's', 'e', 't', 'z', 'u', 'l', '\0', // "setzul"
's', 'e', 't', 'z', 'u', 'g', 'e', '\0', // "setzuge"
's', 'e', 't', 'z', 'u', 'l', 'e', '\0', // "setzule"
's', 'e', 't', 'z', 'u', 'g', '\0', // "setzug"
't', 'z', 'c', 'n', 't', '\0', // "tzcnt"
'l', 'z', 'c', 'n', 't', '\0', // "lzcnt"
'p', 'o', 'p', 'c', 'n', 't', '\0', // "popcnt"
't', 'p', 'a', 'u', 's', 'e', '\0', // "tpause"
'u', 'm', 'o', 'n', 'i', 't', 'o', 'r', '\0', // "umonitor"
'u', 'm', 'w', 'a', 'i', 't', '\0', // "umwait"
'n', 'e', 'g', '\0', // "neg"
'n', 'o', 't', '\0', // "not"
'r', 'o', 'l', '\0', // "rol"
'r', 'o', 'r', '\0', // "ror"
'r', 'c', 'l', '\0', // "rcl"
'r', 'c', 'r', '\0', // "rcr"
's', 'h', 'l', '\0', // "shl"
's', 'h', 'r', '\0', // "shr"
's', 'a', 'r', '\0', // "sar"
'r', 'e', 't', '\0', // "ret"
'l', 'o', 'o', 'p', '\0', // "loop"
'c', 'a', 'l', 'l', '\0', // "call"
'r', 'e', 'p', ' ', 'm', 'o', 'v', 's', 'b', '\0', // "rep movsb"
'r', 'e', 'p', ' ', 'm', 'o', 'v', 's', 'd', '\0', // "rep movsd"
'r', 'e', 'p', ' ', 'm', 'o', 'v', 's', 'q', '\0', // "rep movsq"
'm', 'o', 'v', 's', 'b', '\0', // "movsb"
'm', 'o', 'v', 's', 'd', '\0', // "movsd"
'm', 'o', 'v', 's', 'q', '\0', // "movsq"
'r', 'e', 'p', ' ', 's', 't', 'o', 's', 'b', '\0', // "rep stosb"
'r', 'e', 'p', ' ', 's', 't', 'o', 's', 'd', '\0', // "rep stosd"
'r', 'e', 'p', ' ', 's', 't', 'o', 's', 'q', '\0', // "rep stosq"
's', 't', 'o', 's', 'b', '\0', // "stosb"
's', 't', 'o', 's', 'd', '\0', // "stosd"
's', 't', 'o', 's', 'q', '\0', // "stosq"
'i', 'n', 't', '3', '\0', // "int3"
'n', 'o', 'p', '\0', // "nop"
'p', 'a', 'u', 's', 'e', '\0', // "pause"
'l', 'o', 'c', 'k', '\0', // "lock"
'l', 'e', 'a', 'v', 'e', '\0', // "leave"
's', 'e', 'r', 'i', 'a', 'l', 'i', 'z', 'e', '\0', // "serialize"
'c', 'w', 'd', 'e', '\0', // "cwde"
'c', 'd', 'q', '\0', // "cdq"
'i', 'd', 'i', 'v', '\0', // "idiv"
'd', 'i', 'v', '\0', // "div"
'm', 'u', 'l', '\0', // "mul"
's', 'a', 'h', 'f', '\0', // "sahf"
'x', 'a', 'd', 'd', '\0', // "xadd"
'c', 'm', 'p', 'x', 'c', 'h', 'g', '\0', // "cmpxchg"
's', 'h', 'l', 'd', '\0', // "shld"
's', 'h', 'r', 'd', '\0', // "shrd"
'f', 'l', 'd', '\0', // "fld"
'f', 's', 't', 'p', '\0', // "fstp"
's', 'e', 't', 'o', '\0', // "seto"
's', 'e', 't', 'n', 'o', '\0', // "setno"
's', 'e', 't', 'b', '\0', // "setb"
's', 'e', 't', 'a', 'e', '\0', // "setae"
's', 'e', 't', 'e', '\0', // "sete"
's', 'e', 't', 'n', 'e', '\0', // "setne"
's', 'e', 't', 'b', 'e', '\0', // "setbe"
's', 'e', 't', 'a', '\0', // "seta"
's', 'e', 't', 's', '\0', // "sets"
's', 'e', 't', 'n', 's', '\0', // "setns"
's', 'e', 't', 'p', '\0', // "setp"
's', 'e', 't', 'n', 'p', '\0', // "setnp"
's', 'e', 't', 'l', '\0', // "setl"
's', 'e', 't', 'g', 'e', '\0', // "setge"
's', 'e', 't', 'l', 'e', '\0', // "setle"
's', 'e', 't', 'g', '\0', // "setg"
't', 'a', 'i', 'l', '.', 'j', 'm', 'p', '\0', // "tail.jmp"
'j', 'm', 'p', '\0', // "jmp"
'j', 'o', '\0', // "jo"
'j', 'n', 'o', '\0', // "jno"
'j', 'b', '\0', // "jb"
'j', 'a', 'e', '\0', // "jae"
'j', 'e', '\0', // "je"
'j', 'n', 'e', '\0', // "jne"
'j', 'b', 'e', '\0', // "jbe"
'j', 'a', '\0', // "ja"
'j', 's', '\0', // "js"
'j', 'n', 's', '\0', // "jns"
'j', 'p', '\0', // "jp"
'j', 'n', 'p', '\0', // "jnp"
'j', 'l', '\0', // "jl"
'j', 'g', 'e', '\0', // "jge"
'j', 'l', 'e', '\0', // "jle"
'j', 'g', '\0', // "jg"
'a', 'l', 'i', 'g', 'n', '\0', // "align"
'd', 'a', 't', 'a', '1', '6', '\0', // "data16"
    ];

    private static ReadOnlySpan<char> s_instructionNameDataOther => [
'I', 'N', 'V', 'A', 'L', 'I', 'D', '\0', // "INVALID"
'a', 'd', 'd', '\0', // "add"
's', 'u', 'b', '\0', // "sub"
'l', 'd', 'r', '\0', // "ldr"
's', 't', 'r', '\0', // "str"
'l', 'd', 'r', 'b', '\0', // "ldrb"
's', 't', 'r', 'b', '\0', // "strb"
'l', 'd', 'r', 'h', '\0', // "ldrh"
's', 't', 'r', 'h', '\0', // "strh"
'l', 'd', 'r', 's', 'b', '\0', // "ldrsb"
'l', 'd', 'r', 's', 'h', '\0', // "ldrsh"
'm', 'o', 'v', '\0', // "mov"
'c', 'm', 'p', '\0', // "cmp"
'l', 's', 'l', '\0', // "lsl"
'l', 's', 'r', '\0', // "lsr"
'a', 's', 'r', '\0', // "asr"
'r', 'o', 'r', '\0', // "ror"
'p', 'l', 'd', '\0', // "pld"
'p', 'l', 'd', 'w', '\0', // "pldw"
'p', 'l', 'i', '\0', // "pli"
'm', 'o', 'v', 't', '\0', // "movt"
'm', 'o', 'v', 'w', '\0', // "movw"
'a', 'n', 'd', '\0', // "and"
'e', 'o', 'r', '\0', // "eor"
'o', 'r', 'r', '\0', // "orr"
'o', 'r', 'n', '\0', // "orn"
'b', 'i', 'c', '\0', // "bic"
'a', 'd', 'c', '\0', // "adc"
's', 'b', 'c', '\0', // "sbc"
'r', 's', 'b', '\0', // "rsb"
't', 's', 't', '\0', // "tst"
't', 'e', 'q', '\0', // "teq"
'c', 'm', 'n', '\0', // "cmn"
'm', 'v', 'n', '\0', // "mvn"
'p', 'u', 's', 'h', '\0', // "push"
'p', 'o', 'p', '\0', // "pop"
'b', '\0', // "b"
'b', 'e', 'q', '\0', // "beq"
'b', 'n', 'e', '\0', // "bne"
'b', 'h', 's', '\0', // "bhs"
'b', 'l', 'o', '\0', // "blo"
'b', 'm', 'i', '\0', // "bmi"
'b', 'p', 'l', '\0', // "bpl"
'b', 'v', 's', '\0', // "bvs"
'b', 'v', 'c', '\0', // "bvc"
'b', 'h', 'i', '\0', // "bhi"
'b', 'l', 's', '\0', // "bls"
'b', 'g', 'e', '\0', // "bge"
'b', 'l', 't', '\0', // "blt"
'b', 'g', 't', '\0', // "bgt"
'b', 'l', 'e', '\0', // "ble"
'b', 'x', '\0', // "bx"
'b', 'l', 'x', '\0', // "blx"
'l', 'd', 'm', '\0', // "ldm"
's', 't', 'm', '\0', // "stm"
's', 'x', 't', 'b', '\0', // "sxtb"
's', 'x', 't', 'h', '\0', // "sxth"
'u', 'x', 't', 'b', '\0', // "uxtb"
'u', 'x', 't', 'h', '\0', // "uxth"
'm', 'u', 'l', '\0', // "mul"
'a', 'd', 'r', '\0', // "adr"
'a', 'd', 'd', 'w', '\0', // "addw"
'b', 'f', 'c', '\0', // "bfc"
'b', 'f', 'i', '\0', // "bfi"
'b', 'l', '\0', // "bl"
'b', 'k', 'p', 't', '\0', // "bkpt"
'c', 'b', 'n', 'z', '\0', // "cbnz"
'c', 'b', 'z', '\0', // "cbz"
'c', 'l', 'z', '\0', // "clz"
'd', 'm', 'b', '\0', // "dmb"
'i', 's', 'm', '\0', // "ism"
'l', 'd', 'm', 'd', 'b', '\0', // "ldmdb"
'l', 'd', 'r', 'd', '\0', // "ldrd"
'l', 'd', 'r', 'e', 'x', '\0', // "ldrex"
'l', 'd', 'r', 'e', 'x', 'b', '\0', // "ldrexb"
'l', 'd', 'r', 'e', 'x', 'd', '\0', // "ldrexd"
'l', 'd', 'r', 'e', 'x', 'h', '\0', // "ldrexh"
'm', 'l', 'a', '\0', // "mla"
'm', 'l', 's', '\0', // "mls"
'n', 'o', 'p', '\0', // "nop"
'n', 'o', 'p', '.', 'w', '\0', // "nop.w"
's', 'b', 'f', 'x', '\0', // "sbfx"
's', 'd', 'i', 'v', '\0', // "sdiv"
's', 's', 'a', 't', '\0', // "ssat"
's', 'm', 'l', 'a', 'l', '\0', // "smlal"
's', 'm', 'u', 'l', 'l', '\0', // "smull"
's', 't', 'm', 'd', 'b', '\0', // "stmdb"
's', 't', 'r', 'd', '\0', // "strd"
's', 't', 'r', 'e', 'x', '\0', // "strex"
's', 't', 'r', 'e', 'x', 'b', '\0', // "strexb"
's', 't', 'r', 'e', 'x', 'd', '\0', // "strexd"
's', 't', 'r', 'e', 'x', 'h', '\0', // "strexh"
's', 'u', 'b', 'w', '\0', // "subw"
't', 'b', 'b', '\0', // "tbb"
't', 'b', 'h', '\0', // "tbh"
'u', 'b', 'f', 'x', '\0', // "ubfx"
'u', 'd', 'i', 'v', '\0', // "udiv"
'u', 'm', 'l', 'a', 'l', '\0', // "umlal"
'u', 'm', 'u', 'l', 'l', '\0', // "umull"
'u', 's', 'a', 't', '\0', // "usat"
'i', 't', '\0', // "it"
'i', 't', 't', '\0', // "itt"
'i', 't', 'e', '\0', // "ite"
'i', 't', 't', 't', '\0', // "ittt"
'i', 't', 't', 'e', '\0', // "itte"
'i', 't', 'e', 't', '\0', // "itet"
'i', 't', 'e', 'e', '\0', // "itee"
'i', 't', 't', 't', 't', '\0', // "itttt"
'i', 't', 't', 't', 'e', '\0', // "ittte"
'i', 't', 't', 'e', 't', '\0', // "ittet"
'i', 't', 't', 'e', 'e', '\0', // "ittee"
'i', 't', 'e', 't', 't', '\0', // "itett"
'i', 't', 'e', 't', 'e', '\0', // "itete"
'i', 't', 'e', 'e', 't', '\0', // "iteet"
'i', 't', 'e', 'e', 'e', '\0', // "iteee"
'v', 's', 't', 'r', '\0', // "vstr"
'v', 'l', 'd', 'r', '\0', // "vldr"
'v', 's', 't', 'm', '\0', // "vstm"
'v', 'l', 'd', 'm', '\0', // "vldm"
'v', 'p', 'u', 's', 'h', '\0', // "vpush"
'v', 'p', 'o', 'p', '\0', // "vpop"
'v', 'm', 'r', 's', '\0', // "vmrs"
'v', 'a', 'd', 'd', '\0', // "vadd"
'v', 's', 'u', 'b', '\0', // "vsub"
'v', 'm', 'u', 'l', '\0', // "vmul"
'v', 'd', 'i', 'v', '\0', // "vdiv"
'v', 'm', 'o', 'v', '\0', // "vmov"
'v', 'a', 'b', 's', '\0', // "vabs"
'v', 's', 'q', 'r', 't', '\0', // "vsqrt"
'v', 'n', 'e', 'g', '\0', // "vneg"
'v', 'c', 'm', 'p', '\0', // "vcmp"
'v', 'c', 'm', 'p', '.', '0', '\0', // "vcmp.0"
'v', 'c', 'v', 't', '.', 'd', '2', 'i', '\0', // "vcvt.d2i"
'v', 'c', 'v', 't', '.', 'f', '2', 'i', '\0', // "vcvt.f2i"
'v', 'c', 'v', 't', '.', 'd', '2', 'u', '\0', // "vcvt.d2u"
'v', 'c', 'v', 't', '.', 'f', '2', 'u', '\0', // "vcvt.f2u"
'v', 'c', 'v', 't', '.', 'i', '2', 'f', '\0', // "vcvt.i2f"
'v', 'c', 'v', 't', '.', 'i', '2', 'd', '\0', // "vcvt.i2d"
'v', 'c', 'v', 't', '.', 'u', '2', 'f', '\0', // "vcvt.u2f"
'v', 'c', 'v', 't', '.', 'u', '2', 'd', '\0', // "vcvt.u2d"
'v', 'c', 'v', 't', '.', 'd', '2', 'f', '\0', // "vcvt.d2f"
'v', 'c', 'v', 't', '.', 'f', '2', 'd', '\0', // "vcvt.f2d"
'v', 'm', 'o', 'v', '.', 'i', '2', 'd', '\0', // "vmov.i2d"
'v', 'm', 'o', 'v', '.', 'd', '2', 'i', '\0', // "vmov.d2i"
'v', 'm', 'o', 'v', '.', 'i', '2', 'f', '\0', // "vmov.i2f"
'v', 'm', 'o', 'v', '.', 'f', '2', 'i', '\0', // "vmov.f2i"
'l', 'd', '1', '\0', // "ld1"
'l', 'd', '2', '\0', // "ld2"
'l', 'd', '3', '\0', // "ld3"
'l', 'd', '4', '\0', // "ld4"
's', 't', '1', '\0', // "st1"
's', 't', '2', '\0', // "st2"
's', 't', '3', '\0', // "st3"
's', 't', '4', '\0', // "st4"
'l', 'd', 'r', 's', 'w', '\0', // "ldrsw"
'f', 'm', 'o', 'v', '\0', // "fmov"
'a', 'd', 'd', 's', '\0', // "adds"
's', 'u', 'b', 's', '\0', // "subs"
'f', 'm', 'u', 'l', '\0', // "fmul"
'f', 'm', 'u', 'l', 'x', '\0', // "fmulx"
'n', 'e', 'g', '\0', // "neg"
'c', 'm', 'e', 'q', '\0', // "cmeq"
'c', 'm', 'g', 'e', '\0', // "cmge"
'c', 'm', 'g', 't', '\0', // "cmgt"
'f', 'c', 'm', 'e', 'q', '\0', // "fcmeq"
'f', 'c', 'm', 'g', 'e', '\0', // "fcmge"
'f', 'c', 'm', 'g', 't', '\0', // "fcmgt"
's', 'q', 's', 'h', 'l', '\0', // "sqshl"
'u', 'q', 's', 'h', 'l', '\0', // "uqshl"
's', 'q', 'd', 'm', 'l', 'a', 'l', '\0', // "sqdmlal"
's', 'q', 'd', 'm', 'l', 's', 'l', '\0', // "sqdmlsl"
's', 'q', 'd', 'm', 'u', 'l', 'h', '\0', // "sqdmulh"
's', 'q', 'd', 'm', 'u', 'l', 'l', '\0', // "sqdmull"
's', 'q', 'r', 'd', 'm', 'l', 'a', 'h', '\0', // "sqrdmlah"
's', 'q', 'r', 'd', 'm', 'l', 's', 'h', '\0', // "sqrdmlsh"
's', 'q', 'r', 'd', 'm', 'u', 'l', 'h', '\0', // "sqrdmulh"
'a', 'n', 'd', 's', '\0', // "ands"
'd', 'u', 'p', '\0', // "dup"
'f', 'm', 'l', 'a', '\0', // "fmla"
'f', 'm', 'l', 's', '\0', // "fmls"
'f', 'c', 'v', 't', 'a', 's', '\0', // "fcvtas"
'f', 'c', 'v', 't', 'a', 'u', '\0', // "fcvtau"
'f', 'c', 'v', 't', 'm', 's', '\0', // "fcvtms"
'f', 'c', 'v', 't', 'm', 'u', '\0', // "fcvtmu"
'f', 'c', 'v', 't', 'n', 's', '\0', // "fcvtns"
'f', 'c', 'v', 't', 'n', 'u', '\0', // "fcvtnu"
'f', 'c', 'v', 't', 'p', 's', '\0', // "fcvtps"
'f', 'c', 'v', 't', 'p', 'u', '\0', // "fcvtpu"
'f', 'c', 'v', 't', 'z', 's', '\0', // "fcvtzs"
'f', 'c', 'v', 't', 'z', 'u', '\0', // "fcvtzu"
's', 'c', 'v', 't', 'f', '\0', // "scvtf"
'u', 'c', 'v', 't', 'f', '\0', // "ucvtf"
'l', 'd', '1', 'r', '\0', // "ld1r"
'l', 'd', '2', 'r', '\0', // "ld2r"
'l', 'd', '3', 'r', '\0', // "ld3r"
'l', 'd', '4', 'r', '\0', // "ld4r"
'n', 'e', 'g', 's', '\0', // "negs"
'b', 'i', 'c', 's', '\0', // "bics"
'e', 'o', 'n', '\0', // "eon"
'l', 'd', 'p', '\0', // "ldp"
'l', 'd', 'p', 's', 'w', '\0', // "ldpsw"
's', 't', 'p', '\0', // "stp"
'l', 'd', 'n', 'p', '\0', // "ldnp"
's', 't', 'n', 'p', '\0', // "stnp"
'c', 'c', 'm', 'p', '\0', // "ccmp"
'c', 'c', 'm', 'n', '\0', // "ccmn"
'i', 'n', 's', '\0', // "ins"
'f', 'a', 'd', 'd', '\0', // "fadd"
'f', 's', 'u', 'b', '\0', // "fsub"
'f', 'd', 'i', 'v', '\0', // "fdiv"
'f', 'm', 'a', 'x', '\0', // "fmax"
'f', 'm', 'a', 'x', 'n', 'm', '\0', // "fmaxnm"
'f', 'm', 'i', 'n', '\0', // "fmin"
'f', 'm', 'i', 'n', 'n', 'm', '\0', // "fminnm"
'f', 'a', 'b', 'd', '\0', // "fabd"
'f', 'a', 'c', 'g', 'e', '\0', // "facge"
'f', 'a', 'c', 'g', 't', '\0', // "facgt"
'f', 'r', 'e', 'c', 'p', 's', '\0', // "frecps"
'f', 'r', 's', 'q', 'r', 't', 's', '\0', // "frsqrts"
'f', 'c', 'm', 'p', '\0', // "fcmp"
'f', 'c', 'm', 'p', 'e', '\0', // "fcmpe"
'f', 'a', 'b', 's', '\0', // "fabs"
'f', 'c', 'm', 'l', 'e', '\0', // "fcmle"
'f', 'c', 'm', 'l', 't', '\0', // "fcmlt"
'f', 'c', 'v', 't', 'x', 'n', '\0', // "fcvtxn"
'f', 'n', 'e', 'g', '\0', // "fneg"
'f', 'r', 'e', 'c', 'p', 'e', '\0', // "frecpe"
'f', 'r', 'i', 'n', 't', 'n', '\0', // "frintn"
'f', 'r', 'i', 'n', 't', 'p', '\0', // "frintp"
'f', 'r', 'i', 'n', 't', 'm', '\0', // "frintm"
'f', 'r', 'i', 'n', 't', 'z', '\0', // "frintz"
'f', 'r', 'i', 'n', 't', 'a', '\0', // "frinta"
'f', 'r', 'i', 'n', 't', 'x', '\0', // "frintx"
'f', 'r', 'i', 'n', 't', 'i', '\0', // "frinti"
'f', 'r', 's', 'q', 'r', 't', 'e', '\0', // "frsqrte"
'f', 's', 'q', 'r', 't', '\0', // "fsqrt"
'a', 'b', 's', '\0', // "abs"
'c', 'm', 'l', 'e', '\0', // "cmle"
'c', 'm', 'l', 't', '\0', // "cmlt"
's', 'q', 'a', 'b', 's', '\0', // "sqabs"
's', 'q', 'n', 'e', 'g', '\0', // "sqneg"
's', 'q', 'x', 't', 'n', '\0', // "sqxtn"
's', 'q', 'x', 't', 'u', 'n', '\0', // "sqxtun"
's', 'u', 'q', 'a', 'd', 'd', '\0', // "suqadd"
'u', 's', 'q', 'a', 'd', 'd', '\0', // "usqadd"
'u', 'q', 'x', 't', 'n', '\0', // "uqxtn"
'c', 'l', 's', '\0', // "cls"
'r', 'b', 'i', 't', '\0', // "rbit"
'c', 'n', 't', '\0', // "cnt"
'r', 'e', 'v', '1', '6', '\0', // "rev16"
'r', 'e', 'v', '3', '2', '\0', // "rev32"
's', 'm', 'l', 'a', 'l', '2', '\0', // "smlal2"
's', 'm', 'l', 's', 'l', '\0', // "smlsl"
's', 'm', 'l', 's', 'l', '2', '\0', // "smlsl2"
's', 'm', 'u', 'l', 'l', '2', '\0', // "smull2"
's', 'q', 'd', 'm', 'l', 'a', 'l', '2', '\0', // "sqdmlal2"
's', 'q', 'd', 'm', 'l', 's', 'l', '2', '\0', // "sqdmlsl2"
's', 'q', 'd', 'm', 'u', 'l', 'l', '2', '\0', // "sqdmull2"
's', 'd', 'o', 't', '\0', // "sdot"
'u', 'd', 'o', 't', '\0', // "udot"
'u', 'm', 'l', 'a', 'l', '2', '\0', // "umlal2"
'u', 'm', 'l', 's', 'l', '\0', // "umlsl"
'u', 'm', 'l', 's', 'l', '2', '\0', // "umlsl2"
'u', 'm', 'u', 'l', 'l', '2', '\0', // "umull2"
's', 's', 'h', 'r', '\0', // "sshr"
's', 's', 'r', 'a', '\0', // "ssra"
's', 'r', 's', 'h', 'r', '\0', // "srshr"
's', 'r', 's', 'r', 'a', '\0', // "srsra"
's', 'h', 'l', '\0', // "shl"
'u', 's', 'h', 'r', '\0', // "ushr"
'u', 's', 'r', 'a', '\0', // "usra"
'u', 'r', 's', 'h', 'r', '\0', // "urshr"
'u', 'r', 's', 'r', 'a', '\0', // "ursra"
's', 'r', 'i', '\0', // "sri"
's', 'l', 'i', '\0', // "sli"
's', 'q', 's', 'h', 'l', 'u', '\0', // "sqshlu"
's', 'q', 'r', 's', 'h', 'r', 'n', '\0', // "sqrshrn"
's', 'q', 'r', 's', 'h', 'r', 'u', 'n', '\0', // "sqrshrun"
's', 'q', 's', 'h', 'r', 'n', '\0', // "sqshrn"
's', 'q', 's', 'h', 'r', 'u', 'n', '\0', // "sqshrun"
'u', 'q', 'r', 's', 'h', 'r', 'n', '\0', // "uqrshrn"
'u', 'q', 's', 'h', 'r', 'n', '\0', // "uqshrn"
'c', 'm', 'h', 'i', '\0', // "cmhi"
'c', 'm', 'h', 's', '\0', // "cmhs"
'c', 'm', 't', 's', 't', '\0', // "cmtst"
's', 'q', 'a', 'd', 'd', '\0', // "sqadd"
's', 'q', 'r', 's', 'h', 'l', '\0', // "sqrshl"
's', 'q', 's', 'u', 'b', '\0', // "sqsub"
's', 'r', 's', 'h', 'l', '\0', // "srshl"
's', 's', 'h', 'l', '\0', // "sshl"
'u', 'q', 'a', 'd', 'd', '\0', // "uqadd"
'u', 'q', 'r', 's', 'h', 'l', '\0', // "uqrshl"
'u', 'q', 's', 'u', 'b', '\0', // "uqsub"
'u', 'r', 's', 'h', 'l', '\0', // "urshl"
'u', 's', 'h', 'l', '\0', // "ushl"
'f', 'a', 'd', 'd', 'p', '\0', // "faddp"
'f', 'm', 'a', 'x', 'n', 'm', 'p', '\0', // "fmaxnmp"
'f', 'm', 'a', 'x', 'p', '\0', // "fmaxp"
'f', 'm', 'i', 'n', 'n', 'm', 'p', '\0', // "fminnmp"
'f', 'm', 'i', 'n', 'p', '\0', // "fminp"
'a', 'd', 'd', 'p', '\0', // "addp"
'l', 'd', 'a', 'r', '\0', // "ldar"
'l', 'd', 'a', 'r', 'b', '\0', // "ldarb"
'l', 'd', 'a', 'r', 'h', '\0', // "ldarh"
'l', 'd', 'a', 'p', 'r', '\0', // "ldapr"
'l', 'd', 'a', 'p', 'r', 'b', '\0', // "ldaprb"
'l', 'd', 'a', 'p', 'r', 'h', '\0', // "ldaprh"
'l', 'd', 'x', 'r', '\0', // "ldxr"
'l', 'd', 'x', 'r', 'b', '\0', // "ldxrb"
'l', 'd', 'x', 'r', 'h', '\0', // "ldxrh"
'l', 'd', 'a', 'x', 'r', '\0', // "ldaxr"
'l', 'd', 'a', 'x', 'r', 'b', '\0', // "ldaxrb"
'l', 'd', 'a', 'x', 'r', 'h', '\0', // "ldaxrh"
'l', 'd', 'u', 'r', '\0', // "ldur"
'l', 'd', 'u', 'r', 'b', '\0', // "ldurb"
'l', 'd', 'u', 'r', 'h', '\0', // "ldurh"
'l', 'd', 'u', 'r', 's', 'b', '\0', // "ldursb"
'l', 'd', 'u', 'r', 's', 'h', '\0', // "ldursh"
'l', 'd', 'u', 'r', 's', 'w', '\0', // "ldursw"
'l', 'd', 'a', 'p', 'u', 'r', '\0', // "ldapur"
'l', 'd', 'a', 'p', 'u', 'r', 'b', '\0', // "ldapurb"
'l', 'd', 'a', 'p', 'u', 'r', 'h', '\0', // "ldapurh"
's', 't', 'l', 'r', '\0', // "stlr"
's', 't', 'l', 'r', 'b', '\0', // "stlrb"
's', 't', 'l', 'r', 'h', '\0', // "stlrh"
's', 't', 'x', 'r', '\0', // "stxr"
's', 't', 'x', 'r', 'b', '\0', // "stxrb"
's', 't', 'x', 'r', 'h', '\0', // "stxrh"
's', 't', 'l', 'x', 'r', '\0', // "stlxr"
's', 't', 'l', 'x', 'r', 'b', '\0', // "stlxrb"
's', 't', 'l', 'x', 'r', 'h', '\0', // "stlxrh"
's', 't', 'u', 'r', '\0', // "stur"
's', 't', 'u', 'r', 'b', '\0', // "sturb"
's', 't', 'u', 'r', 'h', '\0', // "sturh"
's', 't', 'l', 'u', 'r', '\0', // "stlur"
's', 't', 'l', 'u', 'r', 'b', '\0', // "stlurb"
's', 't', 'l', 'u', 'r', 'h', '\0', // "stlurh"
'c', 'a', 's', 'b', '\0', // "casb"
'c', 'a', 's', 'a', 'b', '\0', // "casab"
'c', 'a', 's', 'a', 'l', 'b', '\0', // "casalb"
'c', 'a', 's', 'l', 'b', '\0', // "caslb"
'c', 'a', 's', 'h', '\0', // "cash"
'c', 'a', 's', 'a', 'h', '\0', // "casah"
'c', 'a', 's', 'a', 'l', 'h', '\0', // "casalh"
'c', 'a', 's', 'l', 'h', '\0', // "caslh"
'c', 'a', 's', '\0', // "cas"
'c', 'a', 's', 'a', '\0', // "casa"
'c', 'a', 's', 'a', 'l', '\0', // "casal"
'c', 'a', 's', 'l', '\0', // "casl"
'l', 'd', 'a', 'd', 'd', 'b', '\0', // "ldaddb"
'l', 'd', 'a', 'd', 'd', 'a', 'b', '\0', // "ldaddab"
'l', 'd', 'a', 'd', 'd', 'a', 'l', 'b', '\0', // "ldaddalb"
'l', 'd', 'a', 'd', 'd', 'l', 'b', '\0', // "ldaddlb"
'l', 'd', 'a', 'd', 'd', 'h', '\0', // "ldaddh"
'l', 'd', 'a', 'd', 'd', 'a', 'h', '\0', // "ldaddah"
'l', 'd', 'a', 'd', 'd', 'a', 'l', 'h', '\0', // "ldaddalh"
'l', 'd', 'a', 'd', 'd', 'l', 'h', '\0', // "ldaddlh"
'l', 'd', 'a', 'd', 'd', '\0', // "ldadd"
'l', 'd', 'a', 'd', 'd', 'a', '\0', // "ldadda"
'l', 'd', 'a', 'd', 'd', 'a', 'l', '\0', // "ldaddal"
'l', 'd', 'c', 'l', 'r', 'a', 'l', '\0', // "ldclral"
'l', 'd', 's', 'e', 't', 'a', 'l', '\0', // "ldsetal"
'l', 'd', 'a', 'd', 'd', 'l', '\0', // "ldaddl"
's', 't', 'a', 'd', 'd', 'b', '\0', // "staddb"
's', 't', 'a', 'd', 'd', 'l', 'b', '\0', // "staddlb"
's', 't', 'a', 'd', 'd', 'h', '\0', // "staddh"
's', 't', 'a', 'd', 'd', 'l', 'h', '\0', // "staddlh"
's', 't', 'a', 'd', 'd', '\0', // "stadd"
's', 't', 'a', 'd', 'd', 'l', '\0', // "staddl"
's', 'w', 'p', 'b', '\0', // "swpb"
's', 'w', 'p', 'a', 'b', '\0', // "swpab"
's', 'w', 'p', 'a', 'l', 'b', '\0', // "swpalb"
's', 'w', 'p', 'l', 'b', '\0', // "swplb"
's', 'w', 'p', 'h', '\0', // "swph"
's', 'w', 'p', 'a', 'h', '\0', // "swpah"
's', 'w', 'p', 'a', 'l', 'h', '\0', // "swpalh"
's', 'w', 'p', 'l', 'h', '\0', // "swplh"
's', 'w', 'p', '\0', // "swp"
's', 'w', 'p', 'a', '\0', // "swpa"
's', 'w', 'p', 'a', 'l', '\0', // "swpal"
's', 'w', 'p', 'l', '\0', // "swpl"
'a', 'd', 'r', 'p', '\0', // "adrp"
'b', 'r', '\0', // "br"
'b', 'l', 'r', '\0', // "blr"
'r', 'e', 't', '\0', // "ret"
'r', 'e', 't', 'a', 'a', '\0', // "retaa"
'r', 'e', 't', 'a', 'b', '\0', // "retab"
't', 'b', 'z', '\0', // "tbz"
't', 'b', 'n', 'z', '\0', // "tbnz"
'm', 'o', 'v', 'k', '\0', // "movk"
'm', 'o', 'v', 'n', '\0', // "movn"
'm', 'o', 'v', 'z', '\0', // "movz"
'c', 's', 'e', 'l', '\0', // "csel"
'c', 's', 'i', 'n', 'c', '\0', // "csinc"
'c', 's', 'i', 'n', 'v', '\0', // "csinv"
'c', 's', 'n', 'e', 'g', '\0', // "csneg"
'c', 'i', 'n', 'c', '\0', // "cinc"
'c', 'i', 'n', 'v', '\0', // "cinv"
'c', 'n', 'e', 'g', '\0', // "cneg"
'c', 's', 'e', 't', '\0', // "cset"
'c', 's', 'e', 't', 'm', '\0', // "csetm"
'a', 'e', 's', 'e', '\0', // "aese"
'a', 'e', 's', 'd', '\0', // "aesd"
'a', 'e', 's', 'm', 'c', '\0', // "aesmc"
'a', 'e', 's', 'i', 'm', 'c', '\0', // "aesimc"
'r', 'e', 'v', '\0', // "rev"
'r', 'e', 'v', '6', '4', '\0', // "rev64"
'a', 'd', 'c', 's', '\0', // "adcs"
's', 'b', 'c', 's', '\0', // "sbcs"
'm', 'n', 'e', 'g', '\0', // "mneg"
'm', 'a', 'd', 'd', '\0', // "madd"
'm', 's', 'u', 'b', '\0', // "msub"
's', 'm', 'a', 'd', 'd', 'l', '\0', // "smaddl"
's', 'm', 'n', 'e', 'g', 'l', '\0', // "smnegl"
's', 'm', 's', 'u', 'b', 'l', '\0', // "smsubl"
's', 'm', 'u', 'l', 'h', '\0', // "smulh"
'u', 'm', 'a', 'd', 'd', 'l', '\0', // "umaddl"
'u', 'm', 'n', 'e', 'g', 'l', '\0', // "umnegl"
'u', 'm', 's', 'u', 'b', 'l', '\0', // "umsubl"
'u', 'm', 'u', 'l', 'h', '\0', // "umulh"
'e', 'x', 't', 'r', '\0', // "extr"
'l', 's', 'l', 'v', '\0', // "lslv"
'l', 's', 'r', 'v', '\0', // "lsrv"
'a', 's', 'r', 'v', '\0', // "asrv"
'r', 'o', 'r', 'v', '\0', // "rorv"
'c', 'r', 'c', '3', '2', 'b', '\0', // "crc32b"
'c', 'r', 'c', '3', '2', 'h', '\0', // "crc32h"
'c', 'r', 'c', '3', '2', 'w', '\0', // "crc32w"
'c', 'r', 'c', '3', '2', 'x', '\0', // "crc32x"
'c', 'r', 'c', '3', '2', 'c', 'b', '\0', // "crc32cb"
'c', 'r', 'c', '3', '2', 'c', 'h', '\0', // "crc32ch"
'c', 'r', 'c', '3', '2', 'c', 'w', '\0', // "crc32cw"
'c', 'r', 'c', '3', '2', 'c', 'x', '\0', // "crc32cx"
's', 'h', 'a', '1', 'c', '\0', // "sha1c"
's', 'h', 'a', '1', 'm', '\0', // "sha1m"
's', 'h', 'a', '1', 'p', '\0', // "sha1p"
's', 'h', 'a', '1', 'h', '\0', // "sha1h"
's', 'h', 'a', '1', 's', 'u', '0', '\0', // "sha1su0"
's', 'h', 'a', '1', 's', 'u', '1', '\0', // "sha1su1"
's', 'h', 'a', '2', '5', '6', 'h', '\0', // "sha256h"
's', 'h', 'a', '2', '5', '6', 'h', '2', '\0', // "sha256h2"
's', 'h', 'a', '2', '5', '6', 's', 'u', '0', '\0', // "sha256su0"
's', 'h', 'a', '2', '5', '6', 's', 'u', '1', '\0', // "sha256su1"
'e', 'x', 't', '\0', // "ext"
's', 'b', 'f', 'm', '\0', // "sbfm"
'b', 'f', 'm', '\0', // "bfm"
'u', 'b', 'f', 'm', '\0', // "ubfm"
's', 'b', 'f', 'i', 'z', '\0', // "sbfiz"
'u', 'b', 'f', 'i', 'z', '\0', // "ubfiz"
'b', 'f', 'x', 'i', 'l', '\0', // "bfxil"
's', 'x', 't', 'w', '\0', // "sxtw"
'a', 'u', 't', 'i', 'a', '1', '7', '1', '6', '\0', // "autia1716"
'a', 'u', 't', 'i', 'a', 's', 'p', '\0', // "autiasp"
'a', 'u', 't', 'i', 'b', '1', '7', '1', '6', '\0', // "autib1716"
'a', 'u', 't', 'i', 'b', 's', 'p', '\0', // "autibsp"
'a', 'u', 't', 'i', 'b', 'z', '\0', // "autibz"
'a', 'u', 't', 'i', 'a', 'z', '\0', // "autiaz"
'p', 'a', 'c', 'i', 'a', '1', '7', '1', '6', '\0', // "pacia1716"
'p', 'a', 'c', 'i', 'a', 's', 'p', '\0', // "paciasp"
'p', 'a', 'c', 'i', 'b', '1', '7', '1', '6', '\0', // "pacib1716"
'p', 'a', 'c', 'i', 'b', 's', 'p', '\0', // "pacibsp"
'p', 'a', 'c', 'i', 'b', 'z', '\0', // "pacibz"
'p', 'a', 'c', 'i', 'a', 'z', '\0', // "paciaz"
'x', 'p', 'a', 'c', 'l', 'r', 'i', '\0', // "xpaclri"
'a', 'u', 't', 'i', 'z', 'a', '\0', // "autiza"
'a', 'u', 't', 'i', 'z', 'b', '\0', // "autizb"
'p', 'a', 'c', 'i', 'z', 'a', '\0', // "paciza"
'p', 'a', 'c', 'i', 'z', 'b', '\0', // "pacizb"
'x', 'p', 'a', 'c', 'd', '\0', // "xpacd"
'x', 'p', 'a', 'c', 'i', '\0', // "xpaci"
'a', 'u', 't', 'i', 'a', '\0', // "autia"
'a', 'u', 't', 'i', 'b', '\0', // "autib"
'p', 'a', 'c', 'i', 'a', '\0', // "pacia"
'p', 'a', 'c', 'i', 'b', '\0', // "pacib"
'y', 'i', 'e', 'l', 'd', '\0', // "yield"
'b', 'r', 'k', '\0', // "brk"
'd', 's', 'b', '\0', // "dsb"
'i', 's', 'b', '\0', // "isb"
'd', 'c', 'z', 'v', 'a', '\0', // "dczva"
'm', 'r', 's', '\0', // "mrs"
'u', 'm', 'o', 'v', '\0', // "umov"
's', 'm', 'o', 'v', '\0', // "smov"
'm', 'o', 'v', 'i', '\0', // "movi"
'm', 'v', 'n', 'i', '\0', // "mvni"
'u', 'r', 'e', 'c', 'p', 'e', '\0', // "urecpe"
'u', 'r', 's', 'q', 'r', 't', 'e', '\0', // "ursqrte"
'b', 's', 'l', '\0', // "bsl"
'b', 'i', 't', '\0', // "bit"
'b', 'i', 'f', '\0', // "bif"
'a', 'd', 'd', 'v', '\0', // "addv"
'c', 't', 'z', '\0', // "ctz"
'n', 'o', 't', '\0', // "not"
's', 'a', 'd', 'd', 'l', 'v', '\0', // "saddlv"
's', 'm', 'a', 'x', 'v', '\0', // "smaxv"
's', 'm', 'i', 'n', 'v', '\0', // "sminv"
'u', 'a', 'd', 'd', 'l', 'v', '\0', // "uaddlv"
'u', 'm', 'a', 'x', 'v', '\0', // "umaxv"
'u', 'm', 'i', 'n', 'v', '\0', // "uminv"
'f', 'm', 'a', 'x', 'n', 'm', 'v', '\0', // "fmaxnmv"
'f', 'm', 'a', 'x', 'v', '\0', // "fmaxv"
'f', 'm', 'i', 'n', 'n', 'm', 'v', '\0', // "fminnmv"
'f', 'm', 'i', 'n', 'v', '\0', // "fminv"
'u', 'z', 'p', '1', '\0', // "uzp1"
'u', 'z', 'p', '2', '\0', // "uzp2"
'z', 'i', 'p', '1', '\0', // "zip1"
'z', 'i', 'p', '2', '\0', // "zip2"
't', 'r', 'n', '1', '\0', // "trn1"
't', 'r', 'n', '2', '\0', // "trn2"
's', 'q', 'x', 't', 'n', '2', '\0', // "sqxtn2"
's', 'q', 'x', 't', 'u', 'n', '2', '\0', // "sqxtun2"
'u', 'q', 'x', 't', 'n', '2', '\0', // "uqxtn2"
'x', 't', 'n', '\0', // "xtn"
'x', 't', 'n', '2', '\0', // "xtn2"
'f', 'n', 'm', 'u', 'l', '\0', // "fnmul"
'f', 'm', 'a', 'd', 'd', '\0', // "fmadd"
'f', 'm', 's', 'u', 'b', '\0', // "fmsub"
'f', 'n', 'm', 'a', 'd', 'd', '\0', // "fnmadd"
'f', 'n', 'm', 's', 'u', 'b', '\0', // "fnmsub"
'f', 'c', 'v', 't', '\0', // "fcvt"
'p', 'm', 'u', 'l', '\0', // "pmul"
's', 'a', 'b', 'a', '\0', // "saba"
's', 'a', 'b', 'd', '\0', // "sabd"
's', 'm', 'a', 'x', '\0', // "smax"
's', 'm', 'a', 'x', 'p', '\0', // "smaxp"
's', 'm', 'i', 'n', '\0', // "smin"
's', 'm', 'i', 'n', 'p', '\0', // "sminp"
'u', 'a', 'b', 'a', '\0', // "uaba"
'u', 'a', 'b', 'd', '\0', // "uabd"
'u', 'm', 'a', 'x', '\0', // "umax"
'u', 'm', 'a', 'x', 'p', '\0', // "umaxp"
'u', 'm', 'i', 'n', '\0', // "umin"
'u', 'm', 'i', 'n', 'p', '\0', // "uminp"
'f', 'c', 'v', 't', 'l', '\0', // "fcvtl"
'f', 'c', 'v', 't', 'l', '2', '\0', // "fcvtl2"
'f', 'c', 'v', 't', 'n', '\0', // "fcvtn"
'f', 'c', 'v', 't', 'n', '2', '\0', // "fcvtn2"
'f', 'c', 'v', 't', 'x', 'n', '2', '\0', // "fcvtxn2"
'f', 'r', 'e', 'c', 'p', 'x', '\0', // "frecpx"
'a', 'd', 'd', 'h', 'n', '\0', // "addhn"
'a', 'd', 'd', 'h', 'n', '2', '\0', // "addhn2"
'p', 'm', 'u', 'l', 'l', '\0', // "pmull"
'p', 'm', 'u', 'l', 'l', '2', '\0', // "pmull2"
'r', 'a', 'd', 'd', 'h', 'n', '\0', // "raddhn"
'r', 'a', 'd', 'd', 'h', 'n', '2', '\0', // "raddhn2"
'r', 's', 'u', 'b', 'h', 'n', '\0', // "rsubhn"
'r', 's', 'u', 'b', 'h', 'n', '2', '\0', // "rsubhn2"
's', 'a', 'b', 'a', 'l', '\0', // "sabal"
's', 'a', 'b', 'a', 'l', '2', '\0', // "sabal2"
's', 'a', 'b', 'd', 'l', '\0', // "sabdl"
's', 'a', 'b', 'd', 'l', '2', '\0', // "sabdl2"
's', 'a', 'd', 'a', 'l', 'p', '\0', // "sadalp"
's', 'a', 'd', 'd', 'l', '\0', // "saddl"
's', 'a', 'd', 'd', 'l', '2', '\0', // "saddl2"
's', 'a', 'd', 'd', 'l', 'p', '\0', // "saddlp"
's', 'a', 'd', 'd', 'w', '\0', // "saddw"
's', 'a', 'd', 'd', 'w', '2', '\0', // "saddw2"
's', 'h', 'a', 'd', 'd', '\0', // "shadd"
's', 'h', 's', 'u', 'b', '\0', // "shsub"
's', 'r', 'h', 'a', 'd', 'd', '\0', // "srhadd"
's', 's', 'u', 'b', 'l', '\0', // "ssubl"
's', 's', 'u', 'b', 'l', '2', '\0', // "ssubl2"
's', 's', 'u', 'b', 'w', '\0', // "ssubw"
's', 's', 'u', 'b', 'w', '2', '\0', // "ssubw2"
's', 'u', 'b', 'h', 'n', '\0', // "subhn"
's', 'u', 'b', 'h', 'n', '2', '\0', // "subhn2"
'u', 'a', 'b', 'a', 'l', '\0', // "uabal"
'u', 'a', 'b', 'a', 'l', '2', '\0', // "uabal2"
'u', 'a', 'b', 'd', 'l', '\0', // "uabdl"
'u', 'a', 'b', 'd', 'l', '2', '\0', // "uabdl2"
'u', 'a', 'd', 'a', 'l', 'p', '\0', // "uadalp"
'u', 'a', 'd', 'd', 'l', '\0', // "uaddl"
'u', 'a', 'd', 'd', 'l', '2', '\0', // "uaddl2"
'u', 'a', 'd', 'd', 'l', 'p', '\0', // "uaddlp"
'u', 'a', 'd', 'd', 'w', '\0', // "uaddw"
'u', 'a', 'd', 'd', 'w', '2', '\0', // "uaddw2"
'u', 'h', 'a', 'd', 'd', '\0', // "uhadd"
'u', 'h', 's', 'u', 'b', '\0', // "uhsub"
'u', 'r', 'h', 'a', 'd', 'd', '\0', // "urhadd"
'u', 's', 'u', 'b', 'l', '\0', // "usubl"
'u', 's', 'u', 'b', 'l', '2', '\0', // "usubl2"
'u', 's', 'u', 'b', 'w', '\0', // "usubw"
'u', 's', 'u', 'b', 'w', '2', '\0', // "usubw2"
's', 'h', 'l', 'l', '\0', // "shll"
's', 'h', 'l', 'l', '2', '\0', // "shll2"
's', 's', 'h', 'l', 'l', '\0', // "sshll"
's', 's', 'h', 'l', 'l', '2', '\0', // "sshll2"
'u', 's', 'h', 'l', 'l', '\0', // "ushll"
'u', 's', 'h', 'l', 'l', '2', '\0', // "ushll2"
's', 'h', 'r', 'n', '\0', // "shrn"
's', 'h', 'r', 'n', '2', '\0', // "shrn2"
'r', 's', 'h', 'r', 'n', '\0', // "rshrn"
'r', 's', 'h', 'r', 'n', '2', '\0', // "rshrn2"
's', 'q', 'r', 's', 'h', 'r', 'n', '2', '\0', // "sqrshrn2"
's', 'q', 'r', 's', 'h', 'r', 'u', 'n', '2', '\0', // "sqrshrun2"
's', 'q', 's', 'h', 'r', 'n', '2', '\0', // "sqshrn2"
's', 'q', 's', 'h', 'r', 'u', 'n', '2', '\0', // "sqshrun2"
'u', 'q', 'r', 's', 'h', 'r', 'n', '2', '\0', // "uqrshrn2"
'u', 'q', 's', 'h', 'r', 'n', '2', '\0', // "uqshrn2"
's', 'x', 't', 'l', '\0', // "sxtl"
's', 'x', 't', 'l', '2', '\0', // "sxtl2"
'u', 'x', 't', 'l', '\0', // "uxtl"
'u', 'x', 't', 'l', '2', '\0', // "uxtl2"
't', 'b', 'l', '\0', // "tbl"
't', 'b', 'x', '\0', // "tbx"
'a', 'l', 'i', 'g', 'n', '\0', // "align"
'e', 'o', 'r', '3', '\0', // "eor3"
'b', 'c', 'a', 'x', '\0', // "bcax"
's', 'm', '3', 's', 's', '1', '\0', // "sm3ss1"
's', 'h', 'a', '5', '1', '2', 'h', '\0', // "sha512h"
's', 'h', 'a', '5', '1', '2', 'h', '2', '\0', // "sha512h2"
's', 'h', 'a', '5', '1', '2', 's', 'u', '1', '\0', // "sha512su1"
'r', 'a', 'x', '1', '\0', // "rax1"
's', 'm', '3', 'p', 'a', 'r', 't', 'w', '1', '\0', // "sm3partw1"
's', 'm', '3', 'p', 'a', 'r', 't', 'w', '2', '\0', // "sm3partw2"
's', 'm', '4', 'e', 'k', 'e', 'y', '\0', // "sm4ekey"
'x', 'a', 'r', '\0', // "xar"
's', 'h', 'a', '5', '1', '2', 's', 'u', '0', '\0', // "sha512su0"
's', 'm', '4', 'e', '\0', // "sm4e"
's', 't', '1', 'w', '\0', // "st1w"
'l', 'd', '1', 's', 'h', '\0', // "ld1sh"
'l', 'd', '1', 'h', '\0', // "ld1h"
'l', 'd', '1', 'w', '\0', // "ld1w"
'l', 'd', '1', 'd', '\0', // "ld1d"
's', 't', '1', 'h', '\0', // "st1h"
's', 't', '1', 'd', '\0', // "st1d"
'p', 'm', 'o', 'v', '\0', // "pmov"
'l', 'd', 'f', 'f', '1', 's', 'h', '\0', // "ldff1sh"
'l', 'd', 'f', 'f', '1', 'w', '\0', // "ldff1w"
'l', 'd', 'f', 'f', '1', 'h', '\0', // "ldff1h"
'l', 'd', '1', 's', 'w', '\0', // "ld1sw"
'f', 'd', 'o', 't', '\0', // "fdot"
'l', 'd', '1', 's', 'b', '\0', // "ld1sb"
'l', 'd', '1', 'b', '\0', // "ld1b"
'p', 'r', 'f', 'b', '\0', // "prfb"
'p', 'r', 'f', 'd', '\0', // "prfd"
'p', 'r', 'f', 'h', '\0', // "prfh"
'p', 'r', 'f', 'w', '\0', // "prfw"
'l', 'd', 'f', 'f', '1', 'd', '\0', // "ldff1d"
'l', 'd', 'f', 'f', '1', 's', 'w', '\0', // "ldff1sw"
's', 't', '1', 'b', '\0', // "st1b"
'l', 'd', 'f', 'f', '1', 's', 'b', '\0', // "ldff1sb"
'l', 'd', 'f', 'f', '1', 'b', '\0', // "ldff1b"
'f', 'm', 'l', 'a', 'l', 'b', '\0', // "fmlalb"
'f', 'm', 'l', 'a', 'l', 't', '\0', // "fmlalt"
'i', 'n', 'd', 'e', 'x', '\0', // "index"
'c', 'p', 'y', '\0', // "cpy"
'l', 'd', 'n', 't', '1', 'b', '\0', // "ldnt1b"
'l', 'd', 'n', 't', '1', 'h', '\0', // "ldnt1h"
'l', 'd', 'n', 't', '1', 'w', '\0', // "ldnt1w"
's', 't', 'n', 't', '1', 'b', '\0', // "stnt1b"
's', 't', 'n', 't', '1', 'h', '\0', // "stnt1h"
's', 't', 'n', 't', '1', 'w', '\0', // "stnt1w"
'l', 'u', 't', 'i', '4', '\0', // "luti4"
'c', 'l', 'a', 's', 't', 'a', '\0', // "clasta"
'c', 'l', 'a', 's', 't', 'b', '\0', // "clastb"
'c', 'm', 'p', 'e', 'q', '\0', // "cmpeq"
'c', 'm', 'p', 'g', 'e', '\0', // "cmpge"
'c', 'm', 'p', 'g', 't', '\0', // "cmpgt"
'c', 'm', 'p', 'l', 'e', '\0', // "cmple"
'c', 'm', 'p', 'l', 't', '\0', // "cmplt"
'c', 'm', 'p', 'n', 'e', '\0', // "cmpne"
'c', 'm', 'p', 'h', 'i', '\0', // "cmphi"
'c', 'm', 'p', 'h', 's', '\0', // "cmphs"
'c', 'm', 'p', 'l', 'o', '\0', // "cmplo"
'c', 'm', 'p', 'l', 's', '\0', // "cmpls"
'w', 'h', 'i', 'l', 'e', 'g', 'e', '\0', // "whilege"
'w', 'h', 'i', 'l', 'e', 'g', 't', '\0', // "whilegt"
'w', 'h', 'i', 'l', 'e', 'h', 'i', '\0', // "whilehi"
'w', 'h', 'i', 'l', 'e', 'h', 's', '\0', // "whilehs"
'w', 'h', 'i', 'l', 'e', 'l', 'e', '\0', // "whilele"
'w', 'h', 'i', 'l', 'e', 'l', 'o', '\0', // "whilelo"
'w', 'h', 'i', 'l', 'e', 'l', 's', '\0', // "whilels"
'w', 'h', 'i', 'l', 'e', 'l', 't', '\0', // "whilelt"
'c', 'd', 'o', 't', '\0', // "cdot"
'c', 'm', 'l', 'a', '\0', // "cmla"
's', 'q', 'r', 'd', 'c', 'm', 'l', 'a', 'h', '\0', // "sqrdcmlah"
's', 'm', 'l', 'a', 'l', 'b', '\0', // "smlalb"
's', 'm', 'l', 'a', 'l', 't', '\0', // "smlalt"
's', 'm', 'l', 's', 'l', 'b', '\0', // "smlslb"
's', 'm', 'l', 's', 'l', 't', '\0', // "smlslt"
'u', 'm', 'l', 'a', 'l', 'b', '\0', // "umlalb"
'u', 'm', 'l', 'a', 'l', 't', '\0', // "umlalt"
'u', 'm', 'l', 's', 'l', 'b', '\0', // "umlslb"
'u', 'm', 'l', 's', 'l', 't', '\0', // "umlslt"
's', 'q', 'd', 'm', 'l', 'a', 'l', 'b', '\0', // "sqdmlalb"
's', 'q', 'd', 'm', 'l', 'a', 'l', 't', '\0', // "sqdmlalt"
's', 'q', 'd', 'm', 'l', 's', 'l', 'b', '\0', // "sqdmlslb"
's', 'q', 'd', 'm', 'l', 's', 'l', 't', '\0', // "sqdmlslt"
's', 'm', 'u', 'l', 'l', 'b', '\0', // "smullb"
's', 'm', 'u', 'l', 'l', 't', '\0', // "smullt"
'u', 'm', 'u', 'l', 'l', 'b', '\0', // "umullb"
'u', 'm', 'u', 'l', 'l', 't', '\0', // "umullt"
's', 'q', 'd', 'm', 'u', 'l', 'l', 'b', '\0', // "sqdmullb"
's', 'q', 'd', 'm', 'u', 'l', 'l', 't', '\0', // "sqdmullt"
'b', 'f', 'm', 'u', 'l', '\0', // "bfmul"
'l', 'd', 'n', 't', '1', 'd', '\0', // "ldnt1d"
's', 't', 'n', 't', '1', 'd', '\0', // "stnt1d"
'a', 'd', 'd', 'p', 't', '\0', // "addpt"
's', 'u', 'b', 'p', 't', '\0', // "subpt"
'f', 'c', 'm', 'n', 'e', '\0', // "fcmne"
'l', 'u', 't', 'i', '2', '\0', // "luti2"
'f', 's', 'u', 'b', 'r', '\0', // "fsubr"
'u', 's', 'd', 'o', 't', '\0', // "usdot"
'f', 'c', 'm', 'l', 'a', '\0', // "fcmla"
'b', 'f', 'd', 'o', 't', '\0', // "bfdot"
'f', 'm', 'l', 'a', 'l', 'l', 'b', 'b', '\0', // "fmlallbb"
'f', 'm', 'l', 'a', 'l', 'l', 'b', 't', '\0', // "fmlallbt"
'f', 'm', 'l', 'a', 'l', 'l', 't', 'b', '\0', // "fmlalltb"
'f', 'm', 'l', 'a', 'l', 'l', 't', 't', '\0', // "fmlalltt"
's', 'u', 'b', 'r', '\0', // "subr"
'm', 'o', 'v', 'p', 'r', 'f', 'x', '\0', // "movprfx"
'd', 'e', 'c', 'd', '\0', // "decd"
'd', 'e', 'c', 'h', '\0', // "dech"
'd', 'e', 'c', 'w', '\0', // "decw"
'i', 'n', 'c', 'd', '\0', // "incd"
'i', 'n', 'c', 'h', '\0', // "inch"
'i', 'n', 'c', 'w', '\0', // "incw"
's', 'q', 'd', 'e', 'c', 'd', '\0', // "sqdecd"
's', 'q', 'd', 'e', 'c', 'h', '\0', // "sqdech"
's', 'q', 'd', 'e', 'c', 'w', '\0', // "sqdecw"
's', 'q', 'i', 'n', 'c', 'd', '\0', // "sqincd"
's', 'q', 'i', 'n', 'c', 'h', '\0', // "sqinch"
's', 'q', 'i', 'n', 'c', 'w', '\0', // "sqincw"
'u', 'q', 'd', 'e', 'c', 'd', '\0', // "uqdecd"
'u', 'q', 'd', 'e', 'c', 'h', '\0', // "uqdech"
'u', 'q', 'd', 'e', 'c', 'w', '\0', // "uqdecw"
'u', 'q', 'i', 'n', 'c', 'd', '\0', // "uqincd"
'u', 'q', 'i', 'n', 'c', 'h', '\0', // "uqinch"
'u', 'q', 'i', 'n', 'c', 'w', '\0', // "uqincw"
'i', 'n', 's', 'r', '\0', // "insr"
'l', 'a', 's', 't', 'a', '\0', // "lasta"
'l', 'a', 's', 't', 'b', '\0', // "lastb"
's', 'p', 'l', 'i', 'c', 'e', '\0', // "splice"
's', 'e', 'l', '\0', // "sel"
'm', 'o', 'v', 's', '\0', // "movs"
'p', 't', 'r', 'u', 'e', '\0', // "ptrue"
'r', 'd', 'f', 'f', 'r', '\0', // "rdffr"
'c', 'n', 't', 'p', '\0', // "cntp"
'd', 'e', 'c', 'p', '\0', // "decp"
'i', 'n', 'c', 'p', '\0', // "incp"
's', 'q', 'd', 'e', 'c', 'p', '\0', // "sqdecp"
's', 'q', 'i', 'n', 'c', 'p', '\0', // "sqincp"
'u', 'q', 'd', 'e', 'c', 'p', '\0', // "uqdecp"
'u', 'q', 'i', 'n', 'c', 'p', '\0', // "uqincp"
'p', 'e', 'x', 't', '\0', // "pext"
'p', 'm', 'u', 'l', 'l', 'b', '\0', // "pmullb"
'p', 'm', 'u', 'l', 'l', 't', '\0', // "pmullt"
'f', 'c', 'v', 't', 'n', 't', '\0', // "fcvtnt"
'b', 'f', 'm', 'l', 'a', '\0', // "bfmla"
'b', 'f', 'm', 'l', 's', '\0', // "bfmls"
'b', 'f', 'm', 'l', 'a', 'l', 'b', '\0', // "bfmlalb"
'b', 'f', 'm', 'l', 'a', 'l', 't', '\0', // "bfmlalt"
'b', 'f', 'm', 'l', 's', 'l', 'b', '\0', // "bfmlslb"
'b', 'f', 'm', 'l', 's', 'l', 't', '\0', // "bfmlslt"
'f', 'm', 'l', 's', 'l', 'b', '\0', // "fmlslb"
'f', 'm', 'l', 's', 'l', 't', '\0', // "fmlslt"
'b', 'f', 'a', 'd', 'd', '\0', // "bfadd"
'b', 'f', 's', 'u', 'b', '\0', // "bfsub"
'l', 'd', 'n', 't', '1', 's', 'b', '\0', // "ldnt1sb"
'l', 'd', 'n', 't', '1', 's', 'h', '\0', // "ldnt1sh"
'l', 'd', '1', 'r', 'o', 'b', '\0', // "ld1rob"
'l', 'd', '1', 'r', 'o', 'd', '\0', // "ld1rod"
'l', 'd', '1', 'r', 'o', 'h', '\0', // "ld1roh"
'l', 'd', '1', 'r', 'o', 'w', '\0', // "ld1row"
'l', 'd', '1', 'r', 'q', 'b', '\0', // "ld1rqb"
'l', 'd', '1', 'r', 'q', 'd', '\0', // "ld1rqd"
'l', 'd', '1', 'r', 'q', 'h', '\0', // "ld1rqh"
'l', 'd', '1', 'r', 'q', 'w', '\0', // "ld1rqw"
'l', 'd', '2', 'q', '\0', // "ld2q"
'l', 'd', '3', 'q', '\0', // "ld3q"
'l', 'd', '4', 'q', '\0', // "ld4q"
'l', 'd', '2', 'b', '\0', // "ld2b"
'l', 'd', '2', 'd', '\0', // "ld2d"
'l', 'd', '2', 'h', '\0', // "ld2h"
'l', 'd', '2', 'w', '\0', // "ld2w"
'l', 'd', '3', 'b', '\0', // "ld3b"
'l', 'd', '3', 'd', '\0', // "ld3d"
'l', 'd', '3', 'h', '\0', // "ld3h"
'l', 'd', '3', 'w', '\0', // "ld3w"
'l', 'd', '4', 'b', '\0', // "ld4b"
'l', 'd', '4', 'd', '\0', // "ld4d"
'l', 'd', '4', 'h', '\0', // "ld4h"
'l', 'd', '4', 'w', '\0', // "ld4w"
's', 't', '2', 'b', '\0', // "st2b"
's', 't', '2', 'd', '\0', // "st2d"
's', 't', '2', 'h', '\0', // "st2h"
's', 't', '2', 'w', '\0', // "st2w"
's', 't', '3', 'b', '\0', // "st3b"
's', 't', '3', 'd', '\0', // "st3d"
's', 't', '3', 'h', '\0', // "st3h"
's', 't', '3', 'w', '\0', // "st3w"
's', 't', '4', 'b', '\0', // "st4b"
's', 't', '4', 'd', '\0', // "st4d"
's', 't', '4', 'h', '\0', // "st4h"
's', 't', '4', 'w', '\0', // "st4w"
's', 't', '2', 'q', '\0', // "st2q"
's', 't', '3', 'q', '\0', // "st3q"
's', 't', '4', 'q', '\0', // "st4q"
'u', 'x', 't', 'w', '\0', // "uxtw"
'e', 'o', 'r', 's', '\0', // "eors"
'n', 'a', 'n', 'd', '\0', // "nand"
'n', 'a', 'n', 'd', 's', '\0', // "nands"
'n', 'o', 'r', '\0', // "nor"
'n', 'o', 'r', 's', '\0', // "nors"
'n', 'o', 't', 's', '\0', // "nots"
'o', 'r', 'n', 's', '\0', // "orns"
'o', 'r', 'r', 's', '\0', // "orrs"
'r', 'e', 'v', 'b', '\0', // "revb"
'r', 'e', 'v', 'h', '\0', // "revh"
'r', 'e', 'v', 'w', '\0', // "revw"
'c', 'n', 'o', 't', '\0', // "cnot"
's', 'd', 'i', 'v', 'r', '\0', // "sdivr"
'u', 'd', 'i', 'v', 'r', '\0', // "udivr"
'f', 'a', 'd', 'd', 'v', '\0', // "faddv"
'a', 's', 'r', 'd', '\0', // "asrd"
's', 'q', 'r', 's', 'h', 'l', 'r', '\0', // "sqrshlr"
's', 'q', 's', 'h', 'l', 'r', '\0', // "sqshlr"
's', 'r', 's', 'h', 'l', 'r', '\0', // "srshlr"
'u', 'q', 'r', 's', 'h', 'l', 'r', '\0', // "uqrshlr"
'u', 'q', 's', 'h', 'l', 'r', '\0', // "uqshlr"
'u', 'r', 's', 'h', 'l', 'r', '\0', // "urshlr"
'f', 'a', 'm', 'a', 'x', '\0', // "famax"
'f', 'a', 'm', 'i', 'n', '\0', // "famin"
'f', 'd', 'i', 'v', 'r', '\0', // "fdivr"
'f', 's', 'c', 'a', 'l', 'e', '\0', // "fscale"
'f', 't', 's', 'm', 'u', 'l', '\0', // "ftsmul"
'f', 'a', 'c', 'l', 'e', '\0', // "facle"
'f', 'a', 'c', 'l', 't', '\0', // "faclt"
'f', 'c', 'm', 'u', 'o', '\0', // "fcmuo"
's', 'q', 's', 'u', 'b', 'r', '\0', // "sqsubr"
'u', 'q', 's', 'u', 'b', 'r', '\0', // "uqsubr"
's', 'h', 's', 'u', 'b', 'r', '\0', // "shsubr"
'u', 'h', 's', 'u', 'b', 'r', '\0', // "uhsubr"
'b', 's', 'l', '1', 'n', '\0', // "bsl1n"
'b', 's', 'l', '2', 'n', '\0', // "bsl2n"
'n', 'b', 's', 'l', '\0', // "nbsl"
'b', 'f', 'c', 'v', 't', 'n', '\0', // "bfcvtn"
'f', 'c', 'v', 't', 'n', 'b', '\0', // "fcvtnb"
'f', 'c', 'a', 'd', 'd', '\0', // "fcadd"
's', 'm', 'm', 'l', 'a', '\0', // "smmla"
'u', 'm', 'm', 'l', 'a', '\0', // "ummla"
'u', 's', 'm', 'm', 'l', 'a', '\0', // "usmmla"
'b', 'f', 'm', 'm', 'l', 'a', '\0', // "bfmmla"
's', 'u', 'd', 'o', 't', '\0', // "sudot"
'a', 'n', 'd', 'v', '\0', // "andv"
'e', 'o', 'r', 'v', '\0', // "eorv"
'o', 'r', 'v', '\0', // "orv"
'a', 'n', 'd', 'q', 'v', '\0', // "andqv"
'e', 'o', 'r', 'q', 'v', '\0', // "eorqv"
'o', 'r', 'q', 'v', '\0', // "orqv"
's', 'a', 'd', 'd', 'v', '\0', // "saddv"
'u', 'a', 'd', 'd', 'v', '\0', // "uaddv"
'a', 'd', 'd', 'q', 'v', '\0', // "addqv"
's', 'm', 'a', 'x', 'q', 'v', '\0', // "smaxqv"
's', 'm', 'i', 'n', 'q', 'v', '\0', // "sminqv"
'u', 'm', 'a', 'x', 'q', 'v', '\0', // "umaxqv"
'u', 'm', 'i', 'n', 'q', 'v', '\0', // "uminqv"
'a', 's', 'r', 'r', '\0', // "asrr"
'l', 's', 'l', 'r', '\0', // "lslr"
'l', 's', 'r', 'r', '\0', // "lsrr"
'm', 'a', 'd', '\0', // "mad"
'm', 's', 'b', '\0', // "msb"
'a', 'd', 'd', 'p', 'l', '\0', // "addpl"
'a', 'd', 'd', 'v', 'l', '\0', // "addvl"
'r', 'd', 'v', 'l', '\0', // "rdvl"
'f', 'e', 'x', 'p', 'a', '\0', // "fexpa"
'f', 't', 's', 's', 'e', 'l', '\0', // "ftssel"
'c', 'n', 't', 'b', '\0', // "cntb"
'c', 'n', 't', 'd', '\0', // "cntd"
'c', 'n', 't', 'h', '\0', // "cnth"
'c', 'n', 't', 'w', '\0', // "cntw"
'd', 'e', 'c', 'b', '\0', // "decb"
'i', 'n', 'c', 'b', '\0', // "incb"
's', 'q', 'd', 'e', 'c', 'b', '\0', // "sqdecb"
's', 'q', 'i', 'n', 'c', 'b', '\0', // "sqincb"
'u', 'q', 'd', 'e', 'c', 'b', '\0', // "uqdecb"
'u', 'q', 'i', 'n', 'c', 'b', '\0', // "uqincb"
'd', 'u', 'p', 'm', '\0', // "dupm"
'f', 'c', 'p', 'y', '\0', // "fcpy"
'd', 'u', 'p', 'q', '\0', // "dupq"
'e', 'x', 't', 'q', '\0', // "extq"
't', 'b', 'x', 'q', '\0', // "tbxq"
's', 'u', 'n', 'p', 'k', 'h', 'i', '\0', // "sunpkhi"
's', 'u', 'n', 'p', 'k', 'l', 'o', '\0', // "sunpklo"
'u', 'u', 'n', 'p', 'k', 'h', 'i', '\0', // "uunpkhi"
'u', 'u', 'n', 'p', 'k', 'l', 'o', '\0', // "uunpklo"
'p', 'u', 'n', 'p', 'k', 'h', 'i', '\0', // "punpkhi"
'p', 'u', 'n', 'p', 'k', 'l', 'o', '\0', // "punpklo"
'c', 'o', 'm', 'p', 'a', 'c', 't', '\0', // "compact"
'r', 'e', 'v', 'd', '\0', // "revd"
'b', 'r', 'k', 'p', 'a', '\0', // "brkpa"
'b', 'r', 'k', 'p', 'a', 's', '\0', // "brkpas"
'b', 'r', 'k', 'p', 'b', '\0', // "brkpb"
'b', 'r', 'k', 'p', 'b', 's', '\0', // "brkpbs"
'b', 'r', 'k', 'a', '\0', // "brka"
'b', 'r', 'k', 'b', '\0', // "brkb"
'b', 'r', 'k', 'a', 's', '\0', // "brkas"
'b', 'r', 'k', 'b', 's', '\0', // "brkbs"
'b', 'r', 'k', 'n', '\0', // "brkn"
'b', 'r', 'k', 'n', 's', '\0', // "brkns"
'p', 'f', 'i', 'r', 's', 't', '\0', // "pfirst"
'p', 't', 'r', 'u', 'e', 's', '\0', // "ptrues"
'p', 'n', 'e', 'x', 't', '\0', // "pnext"
'r', 'd', 'f', 'f', 'r', 's', '\0', // "rdffrs"
'p', 't', 'e', 's', 't', '\0', // "ptest"
'p', 'f', 'a', 'l', 's', 'e', '\0', // "pfalse"
's', 'e', 't', 'f', 'f', 'r', '\0', // "setffr"
'w', 'r', 'f', 'f', 'r', '\0', // "wrffr"
'c', 't', 'e', 'r', 'm', 'e', 'q', '\0', // "ctermeq"
'c', 't', 'e', 'r', 'm', 'n', 'e', '\0', // "ctermne"
'w', 'h', 'i', 'l', 'e', 'r', 'w', '\0', // "whilerw"
'w', 'h', 'i', 'l', 'e', 'w', 'r', '\0', // "whilewr"
'p', 's', 'e', 'l', '\0', // "psel"
'f', 'd', 'u', 'p', '\0', // "fdup"
's', 'q', 'd', 'm', 'l', 'a', 'l', 'b', 't', '\0', // "sqdmlalbt"
's', 'q', 'd', 'm', 'l', 's', 'l', 'b', 't', '\0', // "sqdmlslbt"
's', 'c', 'l', 'a', 'm', 'p', '\0', // "sclamp"
'u', 'c', 'l', 'a', 'm', 'p', '\0', // "uclamp"
'm', 'l', 'a', 'p', 't', '\0', // "mlapt"
'm', 'a', 'd', 'p', 't', '\0', // "madpt"
't', 'b', 'l', 'q', '\0', // "tblq"
'u', 'z', 'p', 'q', '1', '\0', // "uzpq1"
'u', 'z', 'p', 'q', '2', '\0', // "uzpq2"
'z', 'i', 'p', 'q', '1', '\0', // "zipq1"
'z', 'i', 'p', 'q', '2', '\0', // "zipq2"
's', 'a', 'b', 'd', 'l', 'b', '\0', // "sabdlb"
's', 'a', 'b', 'd', 'l', 't', '\0', // "sabdlt"
's', 'a', 'd', 'd', 'l', 'b', '\0', // "saddlb"
's', 'a', 'd', 'd', 'l', 't', '\0', // "saddlt"
's', 's', 'u', 'b', 'l', 'b', '\0', // "ssublb"
's', 's', 'u', 'b', 'l', 't', '\0', // "ssublt"
'u', 'a', 'b', 'd', 'l', 'b', '\0', // "uabdlb"
'u', 'a', 'b', 'd', 'l', 't', '\0', // "uabdlt"
'u', 'a', 'd', 'd', 'l', 'b', '\0', // "uaddlb"
'u', 'a', 'd', 'd', 'l', 't', '\0', // "uaddlt"
'u', 's', 'u', 'b', 'l', 'b', '\0', // "usublb"
'u', 's', 'u', 'b', 'l', 't', '\0', // "usublt"
's', 'a', 'd', 'd', 'w', 'b', '\0', // "saddwb"
's', 'a', 'd', 'd', 'w', 't', '\0', // "saddwt"
's', 's', 'u', 'b', 'w', 'b', '\0', // "ssubwb"
's', 's', 'u', 'b', 'w', 't', '\0', // "ssubwt"
'u', 'a', 'd', 'd', 'w', 'b', '\0', // "uaddwb"
'u', 'a', 'd', 'd', 'w', 't', '\0', // "uaddwt"
'u', 's', 'u', 'b', 'w', 'b', '\0', // "usubwb"
'u', 's', 'u', 'b', 'w', 't', '\0', // "usubwt"
'e', 'o', 'r', 'b', 't', '\0', // "eorbt"
'e', 'o', 'r', 't', 'b', '\0', // "eortb"
'b', 'd', 'e', 'p', '\0', // "bdep"
'b', 'e', 'x', 't', '\0', // "bext"
'b', 'g', 'r', 'p', '\0', // "bgrp"
's', 's', 'h', 'l', 'l', 'b', '\0', // "sshllb"
's', 's', 'h', 'l', 'l', 't', '\0', // "sshllt"
'u', 's', 'h', 'l', 'l', 'b', '\0', // "ushllb"
'u', 's', 'h', 'l', 'l', 't', '\0', // "ushllt"
's', 'a', 'd', 'd', 'l', 'b', 't', '\0', // "saddlbt"
's', 's', 'u', 'b', 'l', 'b', 't', '\0', // "ssublbt"
's', 's', 'u', 'b', 'l', 't', 'b', '\0', // "ssubltb"
'c', 'a', 'd', 'd', '\0', // "cadd"
's', 'q', 'c', 'a', 'd', 'd', '\0', // "sqcadd"
's', 'a', 'b', 'a', 'l', 'b', '\0', // "sabalb"
's', 'a', 'b', 'a', 'l', 't', '\0', // "sabalt"
'u', 'a', 'b', 'a', 'l', 'b', '\0', // "uabalb"
'u', 'a', 'b', 'a', 'l', 't', '\0', // "uabalt"
'a', 'd', 'c', 'l', 'b', '\0', // "adclb"
'a', 'd', 'c', 'l', 't', '\0', // "adclt"
's', 'b', 'c', 'l', 'b', '\0', // "sbclb"
's', 'b', 'c', 'l', 't', '\0', // "sbclt"
's', 'q', 'c', 'v', 't', 'n', '\0', // "sqcvtn"
's', 'q', 'c', 'v', 't', 'u', 'n', '\0', // "sqcvtun"
'u', 'q', 'c', 'v', 't', 'n', '\0', // "uqcvtn"
'r', 's', 'h', 'r', 'n', 'b', '\0', // "rshrnb"
'r', 's', 'h', 'r', 'n', 't', '\0', // "rshrnt"
's', 'h', 'r', 'n', 'b', '\0', // "shrnb"
's', 'h', 'r', 'n', 't', '\0', // "shrnt"
's', 'q', 'r', 's', 'h', 'r', 'n', 'b', '\0', // "sqrshrnb"
's', 'q', 'r', 's', 'h', 'r', 'n', 't', '\0', // "sqrshrnt"
's', 'q', 'r', 's', 'h', 'r', 'u', 'n', 'b', '\0', // "sqrshrunb"
's', 'q', 'r', 's', 'h', 'r', 'u', 'n', 't', '\0', // "sqrshrunt"
's', 'q', 's', 'h', 'r', 'n', 'b', '\0', // "sqshrnb"
's', 'q', 's', 'h', 'r', 'n', 't', '\0', // "sqshrnt"
's', 'q', 's', 'h', 'r', 'u', 'n', 'b', '\0', // "sqshrunb"
's', 'q', 's', 'h', 'r', 'u', 'n', 't', '\0', // "sqshrunt"
'u', 'q', 'r', 's', 'h', 'r', 'n', 'b', '\0', // "uqrshrnb"
'u', 'q', 'r', 's', 'h', 'r', 'n', 't', '\0', // "uqrshrnt"
'u', 'q', 's', 'h', 'r', 'n', 'b', '\0', // "uqshrnb"
'u', 'q', 's', 'h', 'r', 'n', 't', '\0', // "uqshrnt"
'a', 'd', 'd', 'h', 'n', 'b', '\0', // "addhnb"
'a', 'd', 'd', 'h', 'n', 't', '\0', // "addhnt"
'r', 'a', 'd', 'd', 'h', 'n', 'b', '\0', // "raddhnb"
'r', 'a', 'd', 'd', 'h', 'n', 't', '\0', // "raddhnt"
'r', 's', 'u', 'b', 'h', 'n', 'b', '\0', // "rsubhnb"
'r', 's', 'u', 'b', 'h', 'n', 't', '\0', // "rsubhnt"
's', 'u', 'b', 'h', 'n', 'b', '\0', // "subhnb"
's', 'u', 'b', 'h', 'n', 't', '\0', // "subhnt"
's', 'q', 'x', 't', 'n', 'b', '\0', // "sqxtnb"
's', 'q', 'x', 't', 'n', 't', '\0', // "sqxtnt"
's', 'q', 'x', 't', 'u', 'n', 'b', '\0', // "sqxtunb"
's', 'q', 'x', 't', 'u', 'n', 't', '\0', // "sqxtunt"
'u', 'q', 'x', 't', 'n', 'b', '\0', // "uqxtnb"
'u', 'q', 'x', 't', 'n', 't', '\0', // "uqxtnt"
'm', 'a', 't', 'c', 'h', '\0', // "match"
'n', 'm', 'a', 't', 'c', 'h', '\0', // "nmatch"
'h', 'i', 's', 't', 's', 'e', 'g', '\0', // "histseg"
'h', 'i', 's', 't', 'c', 'n', 't', '\0', // "histcnt"
'b', 'f', 'c', 'v', 't', 'n', 't', '\0', // "bfcvtnt"
'f', 'c', 'v', 't', 'l', 't', '\0', // "fcvtlt"
'f', 'c', 'v', 't', 'x', 'n', 't', '\0', // "fcvtxnt"
'f', 'a', 'd', 'd', 'q', 'v', '\0', // "faddqv"
'f', 'm', 'a', 'x', 'n', 'm', 'q', 'v', '\0', // "fmaxnmqv"
'f', 'm', 'a', 'x', 'q', 'v', '\0', // "fmaxqv"
'f', 'm', 'i', 'n', 'n', 'm', 'q', 'v', '\0', // "fminnmqv"
'f', 'm', 'i', 'n', 'q', 'v', '\0', // "fminqv"
'f', 'c', 'l', 'a', 'm', 'p', '\0', // "fclamp"
'b', 'f', 'c', 'l', 'a', 'm', 'p', '\0', // "bfclamp"
'f', 'm', 'm', 'l', 'a', '\0', // "fmmla"
'b', 'f', '1', 'c', 'v', 't', '\0', // "bf1cvt"
'b', 'f', '1', 'c', 'v', 't', 'l', 't', '\0', // "bf1cvtlt"
'b', 'f', '2', 'c', 'v', 't', '\0', // "bf2cvt"
'b', 'f', '2', 'c', 'v', 't', 'l', 't', '\0', // "bf2cvtlt"
'f', '1', 'c', 'v', 't', '\0', // "f1cvt"
'f', '1', 'c', 'v', 't', 'l', 't', '\0', // "f1cvtlt"
'f', '2', 'c', 'v', 't', '\0', // "f2cvt"
'f', '2', 'c', 'v', 't', 'l', 't', '\0', // "f2cvtlt"
'f', 'a', 'd', 'd', 'a', '\0', // "fadda"
'b', 'f', 'm', 'a', 'x', '\0', // "bfmax"
'b', 'f', 'm', 'a', 'x', 'n', 'm', '\0', // "bfmaxnm"
'b', 'f', 'm', 'i', 'n', '\0', // "bfmin"
'b', 'f', 'm', 'i', 'n', 'n', 'm', '\0', // "bfminnm"
'f', 't', 'm', 'a', 'd', '\0', // "ftmad"
'b', 'f', 'c', 'v', 't', '\0', // "bfcvt"
'f', 'c', 'v', 't', 'x', '\0', // "fcvtx"
'f', 'l', 'o', 'g', 'b', '\0', // "flogb"
'f', 'n', 'm', 'l', 'a', '\0', // "fnmla"
'f', 'n', 'm', 'l', 's', '\0', // "fnmls"
'f', 'm', 'a', 'd', '\0', // "fmad"
'f', 'm', 's', 'b', '\0', // "fmsb"
'f', 'n', 'm', 'a', 'd', '\0', // "fnmad"
'f', 'n', 'm', 's', 'b', '\0', // "fnmsb"
'l', 'd', '1', 'r', 'b', '\0', // "ld1rb"
'l', 'd', '1', 'r', 'd', '\0', // "ld1rd"
'l', 'd', '1', 'r', 's', 'w', '\0', // "ld1rsw"
'l', 'd', '1', 'r', 'h', '\0', // "ld1rh"
'l', 'd', '1', 'r', 's', 'b', '\0', // "ld1rsb"
'l', 'd', '1', 'r', 's', 'h', '\0', // "ld1rsh"
'l', 'd', '1', 'r', 'w', '\0', // "ld1rw"
'l', 'd', 'n', 'f', '1', 'b', '\0', // "ldnf1b"
'l', 'd', 'n', 'f', '1', 'd', '\0', // "ldnf1d"
'l', 'd', 'n', 'f', '1', 's', 'w', '\0', // "ldnf1sw"
'l', 'd', 'n', 'f', '1', 'h', '\0', // "ldnf1h"
'l', 'd', 'n', 'f', '1', 's', 'b', '\0', // "ldnf1sb"
'l', 'd', 'n', 'f', '1', 's', 'h', '\0', // "ldnf1sh"
'l', 'd', 'n', 'f', '1', 'w', '\0', // "ldnf1w"
'l', 'd', '1', 'q', '\0', // "ld1q"
'l', 'd', 'n', 't', '1', 's', 'w', '\0', // "ldnt1sw"
's', 't', '1', 'q', '\0', // "st1q"
'b', 'c', 'e', 'q', 'z', '\0', // "bceqz"
'b', 'c', 'n', 'e', 'z', '\0', // "bcnez"
'b', 'l', 't', 'u', '\0', // "bltu"
'b', 'g', 'e', 'u', '\0', // "bgeu"
'b', 'e', 'q', 'z', '\0', // "beqz"
'b', 'n', 'e', 'z', '\0', // "bnez"
'd', 'n', 'e', 'g', '\0', // "dneg"
'a', 'd', 'd', '.', 'w', '\0', // "add.w"
'a', 'd', 'd', '.', 'd', '\0', // "add.d"
's', 'u', 'b', '.', 'w', '\0', // "sub.w"
's', 'u', 'b', '.', 'd', '\0', // "sub.d"
'o', 'r', '\0', // "or"
'x', 'o', 'r', '\0', // "xor"
'a', 'n', 'd', 'n', '\0', // "andn"
'm', 'u', 'l', '.', 'w', '\0', // "mul.w"
'm', 'u', 'l', '.', 'd', '\0', // "mul.d"
'm', 'u', 'l', 'h', '.', 'w', '\0', // "mulh.w"
'm', 'u', 'l', 'h', '.', 'w', 'u', '\0', // "mulh.wu"
'm', 'u', 'l', 'h', '.', 'd', '\0', // "mulh.d"
'm', 'u', 'l', 'h', '.', 'd', 'u', '\0', // "mulh.du"
'm', 'u', 'l', 'w', '.', 'd', '.', 'w', '\0', // "mulw.d.w"
'm', 'u', 'l', 'w', '.', 'd', '.', 'w', 'u', '\0', // "mulw.d.wu"
'd', 'i', 'v', '.', 'w', '\0', // "div.w"
'm', 'o', 'd', '.', 'w', '\0', // "mod.w"
'd', 'i', 'v', '.', 'w', 'u', '\0', // "div.wu"
'm', 'o', 'd', '.', 'w', 'u', '\0', // "mod.wu"
'd', 'i', 'v', '.', 'd', '\0', // "div.d"
'm', 'o', 'd', '.', 'd', '\0', // "mod.d"
'd', 'i', 'v', '.', 'd', 'u', '\0', // "div.du"
'm', 'o', 'd', '.', 'd', 'u', '\0', // "mod.du"
's', 'l', 'l', '.', 'w', '\0', // "sll.w"
's', 'r', 'l', '.', 'w', '\0', // "srl.w"
's', 'r', 'a', '.', 'w', '\0', // "sra.w"
'r', 'o', 't', 'r', '.', 'w', '\0', // "rotr.w"
's', 'l', 'l', '.', 'd', '\0', // "sll.d"
's', 'r', 'l', '.', 'd', '\0', // "srl.d"
's', 'r', 'a', '.', 'd', '\0', // "sra.d"
'r', 'o', 't', 'r', '.', 'd', '\0', // "rotr.d"
'm', 'a', 's', 'k', 'e', 'q', 'z', '\0', // "maskeqz"
'm', 'a', 's', 'k', 'n', 'e', 'z', '\0', // "masknez"
's', 'l', 't', '\0', // "slt"
's', 'l', 't', 'u', '\0', // "sltu"
'c', 'r', 'c', '.', 'w', '.', 'b', '.', 'w', '\0', // "crc.w.b.w"
'c', 'r', 'c', '.', 'w', '.', 'h', '.', 'w', '\0', // "crc.w.h.w"
'c', 'r', 'c', '.', 'w', '.', 'w', '.', 'w', '\0', // "crc.w.w.w"
'c', 'r', 'c', '.', 'w', '.', 'd', '.', 'w', '\0', // "crc.w.d.w"
'c', 'r', 'c', 'c', '.', 'w', '.', 'b', '.', 'w', '\0', // "crcc.w.b.w"
'c', 'r', 'c', 'c', '.', 'w', '.', 'h', '.', 'w', '\0', // "crcc.w.h.w"
'c', 'r', 'c', 'c', '.', 'w', '.', 'w', '.', 'w', '\0', // "crcc.w.w.w"
'c', 'r', 'c', 'c', '.', 'w', '.', 'd', '.', 'w', '\0', // "crcc.w.d.w"
'a', 'm', 'c', 'a', 's', '_', 'b', '\0', // "amcas_b"
'a', 'm', 'c', 'a', 's', '_', 'h', '\0', // "amcas_h"
'a', 'm', 'c', 'a', 's', '_', 'w', '\0', // "amcas_w"
'a', 'm', 'c', 'a', 's', '_', 'd', '\0', // "amcas_d"
'a', 'm', 's', 'w', 'a', 'p', '.', 'b', '\0', // "amswap.b"
'a', 'm', 's', 'w', 'a', 'p', '.', 'h', '\0', // "amswap.h"
'a', 'm', 's', 'w', 'a', 'p', '.', 'w', '\0', // "amswap.w"
'a', 'm', 's', 'w', 'a', 'p', '.', 'd', '\0', // "amswap.d"
'a', 'm', 'a', 'd', 'd', '.', 'b', '\0', // "amadd.b"
'a', 'm', 'a', 'd', 'd', '.', 'h', '\0', // "amadd.h"
'a', 'm', 'a', 'd', 'd', '.', 'w', '\0', // "amadd.w"
'a', 'm', 'a', 'd', 'd', '.', 'd', '\0', // "amadd.d"
'a', 'm', 'a', 'n', 'd', '.', 'w', '\0', // "amand.w"
'a', 'm', 'a', 'n', 'd', '.', 'd', '\0', // "amand.d"
'a', 'm', 'o', 'r', '.', 'w', '\0', // "amor.w"
'a', 'm', 'o', 'r', '.', 'd', '\0', // "amor.d"
'a', 'm', 'x', 'o', 'r', '.', 'w', '\0', // "amxor.w"
'a', 'm', 'x', 'o', 'r', '.', 'd', '\0', // "amxor.d"
'a', 'm', 'm', 'a', 'x', '.', 'w', '\0', // "ammax.w"
'a', 'm', 'm', 'a', 'x', '.', 'd', '\0', // "ammax.d"
'a', 'm', 'm', 'i', 'n', '.', 'w', '\0', // "ammin.w"
'a', 'm', 'm', 'i', 'n', '.', 'd', '\0', // "ammin.d"
'a', 'm', 'm', 'a', 'x', '.', 'w', 'u', '\0', // "ammax.wu"
'a', 'm', 'm', 'a', 'x', '.', 'd', 'u', '\0', // "ammax.du"
'a', 'm', 'm', 'i', 'n', '.', 'w', 'u', '\0', // "ammin.wu"
'a', 'm', 'm', 'i', 'n', '.', 'd', 'u', '\0', // "ammin.du"
'a', 'm', 'c', 'a', 's', '_', 'd', 'b', '_', 'b', '\0', // "amcas_db_b"
'a', 'm', 'c', 'a', 's', '_', 'd', 'b', '.', 'h', '\0', // "amcas_db.h"
'a', 'm', 'c', 'a', 's', '_', 'd', 'b', '.', 'w', '\0', // "amcas_db.w"
'a', 'm', 'c', 'a', 's', '_', 'd', 'b', '.', 'd', '\0', // "amcas_db.d"
'a', 'm', 's', 'w', 'a', 'p', '_', 'd', 'b', '.', 'b', '\0', // "amswap_db.b"
'a', 'm', 's', 'w', 'a', 'p', '_', 'd', 'b', '.', 'h', '\0', // "amswap_db.h"
'a', 'm', 's', 'w', 'a', 'p', '_', 'd', 'b', '.', 'w', '\0', // "amswap_db.w"
'a', 'm', 's', 'w', 'a', 'p', '_', 'd', 'b', '.', 'd', '\0', // "amswap_db.d"
'a', 'm', 'a', 'd', 'd', '_', 'd', 'b', '.', 'b', '\0', // "amadd_db.b"
'a', 'm', 'a', 'd', 'd', '_', 'd', 'b', '.', 'h', '\0', // "amadd_db.h"
'a', 'm', 'a', 'd', 'd', '_', 'd', 'b', '.', 'w', '\0', // "amadd_db.w"
'a', 'm', 'a', 'd', 'd', '_', 'd', 'b', '.', 'd', '\0', // "amadd_db.d"
'a', 'm', 'a', 'n', 'd', '_', 'd', 'b', '.', 'w', '\0', // "amand_db.w"
'a', 'm', 'a', 'n', 'd', '_', 'd', 'b', '.', 'd', '\0', // "amand_db.d"
'a', 'm', 'o', 'r', '_', 'd', 'b', '.', 'w', '\0', // "amor_db.w"
'a', 'm', 'o', 'r', '_', 'd', 'b', '.', 'd', '\0', // "amor_db.d"
'a', 'm', 'x', 'o', 'r', '_', 'd', 'b', '.', 'w', '\0', // "amxor_db.w"
'a', 'm', 'x', 'o', 'r', '_', 'd', 'b', '.', 'd', '\0', // "amxor_db.d"
'a', 'm', 'm', 'a', 'x', '_', 'd', 'b', '.', 'w', '\0', // "ammax_db.w"
'a', 'm', 'm', 'a', 'x', '_', 'd', 'b', '.', 'd', '\0', // "ammax_db.d"
'a', 'm', 'm', 'i', 'n', '_', 'd', 'b', '.', 'w', '\0', // "ammin_db.w"
'a', 'm', 'm', 'i', 'n', '_', 'd', 'b', '.', 'd', '\0', // "ammin_db.d"
'a', 'm', 'm', 'a', 'x', '_', 'd', 'b', '.', 'w', 'u', '\0', // "ammax_db.wu"
'a', 'm', 'm', 'a', 'x', '_', 'd', 'b', '.', 'd', 'u', '\0', // "ammax_db.du"
'a', 'm', 'm', 'i', 'n', '_', 'd', 'b', '.', 'w', 'u', '\0', // "ammin_db.wu"
'a', 'm', 'm', 'i', 'n', '_', 'd', 'b', '.', 'd', 'u', '\0', // "ammin_db.du"
'a', 'l', 's', 'l', '.', 'w', '\0', // "alsl.w"
'a', 'l', 's', 'l', '.', 'w', 'u', '\0', // "alsl.wu"
'a', 'l', 's', 'l', '.', 'd', '\0', // "alsl.d"
'b', 'y', 't', 'e', 'p', 'i', 'c', 'k', '.', 'w', '\0', // "bytepick.w"
'b', 'y', 't', 'e', 'p', 'i', 'c', 'k', '.', 'd', '\0', // "bytepick.d"
'f', 's', 'e', 'l', '\0', // "fsel"
'l', 'u', '1', '2', 'i', '.', 'w', '\0', // "lu12i.w"
'l', 'u', '3', '2', 'i', '.', 'd', '\0', // "lu32i.d"
'p', 'c', 'a', 'd', 'd', 'i', '\0', // "pcaddi"
'p', 'c', 'a', 'd', 'd', 'u', '1', '2', 'i', '\0', // "pcaddu12i"
'p', 'c', 'a', 'l', 'a', 'u', '1', '2', 'i', '\0', // "pcalau12i"
'p', 'c', 'a', 'd', 'd', 'u', '1', '8', 'i', '\0', // "pcaddu18i"
'e', 'x', 't', '.', 'w', '.', 'b', '\0', // "ext.w.b"
'e', 'x', 't', '.', 'w', '.', 'h', '\0', // "ext.w.h"
'c', 'l', 'o', '.', 'w', '\0', // "clo.w"
'c', 'l', 'z', '.', 'w', '\0', // "clz.w"
'c', 't', 'o', '.', 'w', '\0', // "cto.w"
'c', 't', 'z', '.', 'w', '\0', // "ctz.w"
'c', 'l', 'o', '.', 'd', '\0', // "clo.d"
'c', 'l', 'z', '.', 'd', '\0', // "clz.d"
'c', 't', 'o', '.', 'd', '\0', // "cto.d"
'c', 't', 'z', '.', 'd', '\0', // "ctz.d"
'r', 'e', 'v', 'b', '.', '2', 'h', '\0', // "revb.2h"
'r', 'e', 'v', 'b', '.', '4', 'h', '\0', // "revb.4h"
'r', 'e', 'v', 'b', '.', '2', 'w', '\0', // "revb.2w"
'r', 'e', 'v', 'b', '.', 'd', '\0', // "revb.d"
'r', 'e', 'v', 'h', '.', '2', 'w', '\0', // "revh.2w"
'r', 'e', 'v', 'h', '.', 'd', '\0', // "revh.d"
'b', 'i', 't', 'r', 'e', 'v', '.', '4', 'b', '\0', // "bitrev.4b"
'b', 'i', 't', 'r', 'e', 'v', '.', '8', 'b', '\0', // "bitrev.8b"
'b', 'i', 't', 'r', 'e', 'v', '.', 'w', '\0', // "bitrev.w"
'b', 'i', 't', 'r', 'e', 'v', '.', 'd', '\0', // "bitrev.d"
'r', 'd', 't', 'i', 'm', 'e', 'l', '.', 'w', '\0', // "rdtimel.w"
'r', 'd', 't', 'i', 'm', 'e', 'h', '.', 'w', '\0', // "rdtimeh.w"
'r', 'd', 't', 'i', 'm', 'e', '.', 'd', '\0', // "rdtime.d"
'c', 'p', 'u', 'c', 'f', 'g', '\0', // "cpucfg"
'm', 'o', 'v', 'f', 'r', '2', 'g', 'r', '.', 's', '\0', // "movfr2gr.s"
'm', 'o', 'v', 'f', 'r', '2', 'g', 'r', '.', 'd', '\0', // "movfr2gr.d"
'm', 'o', 'v', 'f', 'r', 'h', '2', 'g', 'r', '.', 's', '\0', // "movfrh2gr.s"
'b', 's', 't', 'r', 'i', 'n', 's', '.', 'w', '\0', // "bstrins.w"
'b', 's', 't', 'r', 'i', 'n', 's', '.', 'd', '\0', // "bstrins.d"
'b', 's', 't', 'r', 'p', 'i', 'c', 'k', '.', 'w', '\0', // "bstrpick.w"
'b', 's', 't', 'r', 'p', 'i', 'c', 'k', '.', 'd', '\0', // "bstrpick.d"
'l', 'd', '.', 'b', '\0', // "ld.b"
'l', 'd', '.', 'h', '\0', // "ld.h"
'l', 'd', '.', 'w', '\0', // "ld.w"
'l', 'd', '.', 'd', '\0', // "ld.d"
'l', 'd', '.', 'b', 'u', '\0', // "ld.bu"
'l', 'd', '.', 'h', 'u', '\0', // "ld.hu"
'l', 'd', '.', 'w', 'u', '\0', // "ld.wu"
'l', 'd', 'p', 't', 'r', '.', 'w', '\0', // "ldptr.w"
'l', 'd', 'p', 't', 'r', '.', 'd', '\0', // "ldptr.d"
'l', 'l', '.', 'w', '\0', // "ll.w"
'l', 'l', '.', 'd', '\0', // "ll.d"
'l', 'd', 'x', '.', 'b', '\0', // "ldx.b"
'l', 'd', 'x', '.', 'h', '\0', // "ldx.h"
'l', 'd', 'x', '.', 'w', '\0', // "ldx.w"
'l', 'd', 'x', '.', 'd', '\0', // "ldx.d"
'l', 'd', 'x', '.', 'b', 'u', '\0', // "ldx.bu"
'l', 'd', 'x', '.', 'h', 'u', '\0', // "ldx.hu"
'l', 'd', 'x', '.', 'w', 'u', '\0', // "ldx.wu"
'l', 'd', 'g', 't', '.', 'b', '\0', // "ldgt.b"
'l', 'd', 'g', 't', '.', 'h', '\0', // "ldgt.h"
'l', 'd', 'g', 't', '.', 'w', '\0', // "ldgt.w"
'l', 'd', 'g', 't', '.', 'd', '\0', // "ldgt.d"
'l', 'd', 'l', 'e', '.', 'b', '\0', // "ldle.b"
'l', 'd', 'l', 'e', '.', 'h', '\0', // "ldle.h"
'l', 'd', 'l', 'e', '.', 'w', '\0', // "ldle.w"
'l', 'd', 'l', 'e', '.', 'd', '\0', // "ldle.d"
'a', 'd', 'd', 'i', '.', 'w', '\0', // "addi.w"
'a', 'd', 'd', 'i', '.', 'd', '\0', // "addi.d"
'l', 'u', '5', '2', 'i', '.', 'd', '\0', // "lu52i.d"
's', 'l', 't', 'i', '\0', // "slti"
's', 'l', 't', 'u', 'i', '\0', // "sltui"
'a', 'n', 'd', 'i', '\0', // "andi"
'o', 'r', 'i', '\0', // "ori"
'x', 'o', 'r', 'i', '\0', // "xori"
's', 'l', 'l', 'i', '.', 'w', '\0', // "slli.w"
's', 'r', 'l', 'i', '.', 'w', '\0', // "srli.w"
's', 'r', 'a', 'i', '.', 'w', '\0', // "srai.w"
'r', 'o', 't', 'r', 'i', '.', 'w', '\0', // "rotri.w"
's', 'l', 'l', 'i', '.', 'd', '\0', // "slli.d"
's', 'r', 'l', 'i', '.', 'd', '\0', // "srli.d"
's', 'r', 'a', 'i', '.', 'd', '\0', // "srai.d"
'r', 'o', 't', 'r', 'i', '.', 'd', '\0', // "rotri.d"
'a', 'd', 'd', 'u', '1', '6', 'i', '.', 'd', '\0', // "addu16i.d"
'j', 'i', 'r', 'l', '\0', // "jirl"
's', 't', '.', 'b', '\0', // "st.b"
's', 't', '.', 'h', '\0', // "st.h"
's', 't', '.', 'w', '\0', // "st.w"
's', 't', '.', 'd', '\0', // "st.d"
's', 't', 'p', 't', 'r', '.', 'w', '\0', // "stptr.w"
's', 't', 'p', 't', 'r', '.', 'd', '\0', // "stptr.d"
's', 'c', '.', 'w', '\0', // "sc.w"
's', 'c', '.', 'd', '\0', // "sc.d"
's', 't', 'x', '.', 'b', '\0', // "stx.b"
's', 't', 'x', '.', 'h', '\0', // "stx.h"
's', 't', 'x', '.', 'w', '\0', // "stx.w"
's', 't', 'x', '.', 'd', '\0', // "stx.d"
's', 't', 'g', 't', '.', 'b', '\0', // "stgt.b"
's', 't', 'g', 't', '.', 'h', '\0', // "stgt.h"
's', 't', 'g', 't', '.', 'w', '\0', // "stgt.w"
's', 't', 'g', 't', '.', 'd', '\0', // "stgt.d"
's', 't', 'l', 'e', '.', 'b', '\0', // "stle.b"
's', 't', 'l', 'e', '.', 'h', '\0', // "stle.h"
's', 't', 'l', 'e', '.', 'w', '\0', // "stle.w"
's', 't', 'l', 'e', '.', 'd', '\0', // "stle.d"
'd', 'b', 'a', 'r', '\0', // "dbar"
'i', 'b', 'a', 'r', '\0', // "ibar"
's', 'y', 's', 'c', 'a', 'l', 'l', '\0', // "syscall"
'b', 'r', 'e', 'a', 'k', '\0', // "break"
'p', 'r', 'e', 'l', 'd', '\0', // "preld"
'p', 'r', 'e', 'l', 'd', 'x', '\0', // "preldx"
'f', 'a', 'd', 'd', '.', 's', '\0', // "fadd.s"
'f', 'a', 'd', 'd', '.', 'd', '\0', // "fadd.d"
'f', 's', 'u', 'b', '.', 's', '\0', // "fsub.s"
'f', 's', 'u', 'b', '.', 'd', '\0', // "fsub.d"
'f', 'm', 'u', 'l', '.', 's', '\0', // "fmul.s"
'f', 'm', 'u', 'l', '.', 'd', '\0', // "fmul.d"
'f', 'd', 'i', 'v', '.', 's', '\0', // "fdiv.s"
'f', 'd', 'i', 'v', '.', 'd', '\0', // "fdiv.d"
'f', 'm', 'a', 'x', '.', 's', '\0', // "fmax.s"
'f', 'm', 'a', 'x', '.', 'd', '\0', // "fmax.d"
'f', 'm', 'i', 'n', '.', 's', '\0', // "fmin.s"
'f', 'm', 'i', 'n', '.', 'd', '\0', // "fmin.d"
'f', 'm', 'a', 'x', 'a', '.', 's', '\0', // "fmaxa.s"
'f', 'm', 'a', 'x', 'a', '.', 'd', '\0', // "fmaxa.d"
'f', 'm', 'i', 'n', 'a', '.', 's', '\0', // "fmina.s"
'f', 'm', 'i', 'n', 'a', '.', 'd', '\0', // "fmina.d"
'f', 's', 'c', 'a', 'l', 'e', 'b', '.', 's', '\0', // "fscaleb.s"
'f', 's', 'c', 'a', 'l', 'e', 'b', '.', 'd', '\0', // "fscaleb.d"
'f', 'c', 'o', 'p', 'y', 's', 'i', 'g', 'n', '.', 's', '\0', // "fcopysign.s"
'f', 'c', 'o', 'p', 'y', 's', 'i', 'g', 'n', '.', 'd', '\0', // "fcopysign.d"
'f', 'l', 'd', 'x', '.', 's', '\0', // "fldx.s"
'f', 'l', 'd', 'x', '.', 'd', '\0', // "fldx.d"
'f', 's', 't', 'x', '.', 's', '\0', // "fstx.s"
'f', 's', 't', 'x', '.', 'd', '\0', // "fstx.d"
'f', 'l', 'd', 'g', 't', '.', 's', '\0', // "fldgt.s"
'f', 'l', 'd', 'g', 't', '.', 'd', '\0', // "fldgt.d"
'f', 'l', 'd', 'l', 'e', '.', 's', '\0', // "fldle.s"
'f', 'l', 'd', 'l', 'e', '.', 'd', '\0', // "fldle.d"
'f', 's', 't', 'g', 't', '.', 's', '\0', // "fstgt.s"
'f', 's', 't', 'g', 't', '.', 'd', '\0', // "fstgt.d"
'f', 's', 't', 'l', 'e', '.', 's', '\0', // "fstle.s"
'f', 's', 't', 'l', 'e', '.', 'd', '\0', // "fstle.d"
'f', 'm', 'a', 'd', 'd', '.', 's', '\0', // "fmadd.s"
'f', 'm', 'a', 'd', 'd', '.', 'd', '\0', // "fmadd.d"
'f', 'm', 's', 'u', 'b', '.', 's', '\0', // "fmsub.s"
'f', 'm', 's', 'u', 'b', '.', 'd', '\0', // "fmsub.d"
'f', 'n', 'm', 'a', 'd', 'd', '.', 's', '\0', // "fnmadd.s"
'f', 'n', 'm', 'a', 'd', 'd', '.', 'd', '\0', // "fnmadd.d"
'f', 'n', 'm', 's', 'u', 'b', '.', 's', '\0', // "fnmsub.s"
'f', 'n', 'm', 's', 'u', 'b', '.', 'd', '\0', // "fnmsub.d"
'f', 'a', 'b', 's', '.', 's', '\0', // "fabs.s"
'f', 'a', 'b', 's', '.', 'd', '\0', // "fabs.d"
'f', 'n', 'e', 'g', '.', 's', '\0', // "fneg.s"
'f', 'n', 'e', 'g', '.', 'd', '\0', // "fneg.d"
'f', 's', 'q', 'r', 't', '.', 's', '\0', // "fsqrt.s"
'f', 's', 'q', 'r', 't', '.', 'd', '\0', // "fsqrt.d"
'f', 'r', 's', 'q', 'r', 't', '.', 's', '\0', // "frsqrt.s"
'f', 'r', 's', 'q', 'r', 't', '.', 'd', '\0', // "frsqrt.d"
'f', 'r', 's', 'q', 'r', 't', 'e', '.', 's', '\0', // "frsqrte.s"
'f', 'r', 's', 'q', 'r', 't', 'e', '.', 'd', '\0', // "frsqrte.d"
'f', 'r', 'e', 'c', 'i', 'p', '.', 's', '\0', // "frecip.s"
'f', 'r', 'e', 'c', 'i', 'p', '.', 'd', '\0', // "frecip.d"
'f', 'r', 'e', 'c', 'i', 'p', 'e', '.', 's', '\0', // "frecipe.s"
'f', 'r', 'e', 'c', 'i', 'p', 'e', '.', 'd', '\0', // "frecipe.d"
'f', 'l', 'o', 'g', 'b', '.', 's', '\0', // "flogb.s"
'f', 'l', 'o', 'g', 'b', '.', 'd', '\0', // "flogb.d"
'f', 'c', 'l', 'a', 's', 's', '.', 's', '\0', // "fclass.s"
'f', 'c', 'l', 'a', 's', 's', '.', 'd', '\0', // "fclass.d"
'f', 'c', 'v', 't', '.', 's', '.', 'd', '\0', // "fcvt.s.d"
'f', 'c', 'v', 't', '.', 'd', '.', 's', '\0', // "fcvt.d.s"
'f', 'f', 'i', 'n', 't', '.', 's', '.', 'w', '\0', // "ffint.s.w"
'f', 'f', 'i', 'n', 't', '.', 's', '.', 'l', '\0', // "ffint.s.l"
'f', 'f', 'i', 'n', 't', '.', 'd', '.', 'w', '\0', // "ffint.d.w"
'f', 'f', 'i', 'n', 't', '.', 'd', '.', 'l', '\0', // "ffint.d.l"
'f', 't', 'i', 'n', 't', '.', 'w', '.', 's', '\0', // "ftint.w.s"
'f', 't', 'i', 'n', 't', '.', 'w', '.', 'd', '\0', // "ftint.w.d"
'f', 't', 'i', 'n', 't', '.', 'l', '.', 's', '\0', // "ftint.l.s"
'f', 't', 'i', 'n', 't', '.', 'l', '.', 'd', '\0', // "ftint.l.d"
'f', 't', 'i', 'n', 't', 'r', 'm', '.', 'w', '.', 's', '\0', // "ftintrm.w.s"
'f', 't', 'i', 'n', 't', 'r', 'm', '.', 'w', '.', 'd', '\0', // "ftintrm.w.d"
'f', 't', 'i', 'n', 't', 'r', 'm', '.', 'l', '.', 's', '\0', // "ftintrm.l.s"
'f', 't', 'i', 'n', 't', 'r', 'm', '.', 'l', '.', 'd', '\0', // "ftintrm.l.d"
'f', 't', 'i', 'n', 't', 'r', 'p', '.', 'w', '.', 's', '\0', // "ftintrp.w.s"
'f', 't', 'i', 'n', 't', 'r', 'p', '.', 'w', '.', 'd', '\0', // "ftintrp.w.d"
'f', 't', 'i', 'n', 't', 'r', 'p', '.', 'l', '.', 's', '\0', // "ftintrp.l.s"
'f', 't', 'i', 'n', 't', 'r', 'p', '.', 'l', '.', 'd', '\0', // "ftintrp.l.d"
'f', 't', 'i', 'n', 't', 'r', 'z', '.', 'w', '.', 's', '\0', // "ftintrz.w.s"
'f', 't', 'i', 'n', 't', 'r', 'z', '.', 'w', '.', 'd', '\0', // "ftintrz.w.d"
'f', 't', 'i', 'n', 't', 'r', 'z', '.', 'l', '.', 's', '\0', // "ftintrz.l.s"
'f', 't', 'i', 'n', 't', 'r', 'z', '.', 'l', '.', 'd', '\0', // "ftintrz.l.d"
'f', 't', 'i', 'n', 't', 'r', 'n', 'e', '.', 'w', '.', 's', '\0', // "ftintrne.w.s"
'f', 't', 'i', 'n', 't', 'r', 'n', 'e', '.', 'w', '.', 'd', '\0', // "ftintrne.w.d"
'f', 't', 'i', 'n', 't', 'r', 'n', 'e', '.', 'l', '.', 's', '\0', // "ftintrne.l.s"
'f', 't', 'i', 'n', 't', 'r', 'n', 'e', '.', 'l', '.', 'd', '\0', // "ftintrne.l.d"
'f', 'r', 'i', 'n', 't', '.', 's', '\0', // "frint.s"
'f', 'r', 'i', 'n', 't', '.', 'd', '\0', // "frint.d"
'f', 'm', 'o', 'v', '.', 's', '\0', // "fmov.s"
'f', 'm', 'o', 'v', '.', 'd', '\0', // "fmov.d"
'm', 'o', 'v', 'g', 'r', '2', 'f', 'r', '.', 'w', '\0', // "movgr2fr.w"
'm', 'o', 'v', 'g', 'r', '2', 'f', 'r', '.', 'd', '\0', // "movgr2fr.d"
'm', 'o', 'v', 'g', 'r', '2', 'f', 'r', 'h', '.', 'w', '\0', // "movgr2frh.w"
'm', 'o', 'v', 'g', 'r', '2', 'f', 'c', 's', 'r', '\0', // "movgr2fcsr"
'm', 'o', 'v', 'f', 'c', 's', 'r', '2', 'g', 'r', '\0', // "movfcsr2gr"
'm', 'o', 'v', 'f', 'r', '2', 'c', 'f', '\0', // "movfr2cf"
'm', 'o', 'v', 'c', 'f', '2', 'f', 'r', '\0', // "movcf2fr"
'm', 'o', 'v', 'g', 'r', '2', 'c', 'f', '\0', // "movgr2cf"
'm', 'o', 'v', 'c', 'f', '2', 'g', 'r', '\0', // "movcf2gr"
'f', 'c', 'm', 'p', '.', 'c', 'a', 'f', '.', 's', '\0', // "fcmp.caf.s"
'f', 'c', 'm', 'p', '.', 'c', 'u', 'n', '.', 's', '\0', // "fcmp.cun.s"
'f', 'c', 'm', 'p', '.', 'c', 'e', 'q', '.', 's', '\0', // "fcmp.ceq.s"
'f', 'c', 'm', 'p', '.', 'c', 'u', 'e', 'q', '.', 's', '\0', // "fcmp.cueq.s"
'f', 'c', 'm', 'p', '.', 'c', 'l', 't', '.', 's', '\0', // "fcmp.clt.s"
'f', 'c', 'm', 'p', '.', 'c', 'u', 'l', 't', '.', 's', '\0', // "fcmp.cult.s"
'f', 'c', 'm', 'p', '.', 'c', 'l', 'e', '.', 's', '\0', // "fcmp.cle.s"
'f', 'c', 'm', 'p', '.', 'c', 'u', 'l', 'e', '.', 's', '\0', // "fcmp.cule.s"
'f', 'c', 'm', 'p', '.', 'c', 'n', 'e', '.', 's', '\0', // "fcmp.cne.s"
'f', 'c', 'm', 'p', '.', 'c', 'o', 'r', '.', 's', '\0', // "fcmp.cor.s"
'f', 'c', 'm', 'p', '.', 'c', 'u', 'n', 'e', '.', 's', '\0', // "fcmp.cune.s"
'f', 'c', 'm', 'p', '.', 's', 'a', 'f', '.', 'd', '\0', // "fcmp.saf.d"
'f', 'c', 'm', 'p', '.', 's', 'u', 'n', '.', 'd', '\0', // "fcmp.sun.d"
'f', 'c', 'm', 'p', '.', 's', 'e', 'q', '.', 'd', '\0', // "fcmp.seq.d"
'f', 'c', 'm', 'p', '.', 's', 'u', 'e', 'q', '.', 'd', '\0', // "fcmp.sueq.d"
'f', 'c', 'm', 'p', '.', 's', 'l', 't', '.', 'd', '\0', // "fcmp.slt.d"
'f', 'c', 'm', 'p', '.', 's', 'u', 'l', 't', '.', 'd', '\0', // "fcmp.sult.d"
'f', 'c', 'm', 'p', '.', 's', 'l', 'e', '.', 'd', '\0', // "fcmp.sle.d"
'f', 'c', 'm', 'p', '.', 's', 'u', 'l', 'e', '.', 'd', '\0', // "fcmp.sule.d"
'f', 'c', 'm', 'p', '.', 's', 'n', 'e', '.', 'd', '\0', // "fcmp.sne.d"
'f', 'c', 'm', 'p', '.', 's', 'o', 'r', '.', 'd', '\0', // "fcmp.sor.d"
'f', 'c', 'm', 'p', '.', 's', 'u', 'n', 'e', '.', 'd', '\0', // "fcmp.sune.d"
'f', 'c', 'm', 'p', '.', 'c', 'a', 'f', '.', 'd', '\0', // "fcmp.caf.d"
'f', 'c', 'm', 'p', '.', 'c', 'u', 'n', '.', 'd', '\0', // "fcmp.cun.d"
'f', 'c', 'm', 'p', '.', 'c', 'e', 'q', '.', 'd', '\0', // "fcmp.ceq.d"
'f', 'c', 'm', 'p', '.', 'c', 'u', 'e', 'q', '.', 'd', '\0', // "fcmp.cueq.d"
'f', 'c', 'm', 'p', '.', 'c', 'l', 't', '.', 'd', '\0', // "fcmp.clt.d"
'f', 'c', 'm', 'p', '.', 'c', 'u', 'l', 't', '.', 'd', '\0', // "fcmp.cult.d"
'f', 'c', 'm', 'p', '.', 'c', 'l', 'e', '.', 'd', '\0', // "fcmp.cle.d"
'f', 'c', 'm', 'p', '.', 'c', 'u', 'l', 'e', '.', 'd', '\0', // "fcmp.cule.d"
'f', 'c', 'm', 'p', '.', 'c', 'n', 'e', '.', 'd', '\0', // "fcmp.cne.d"
'f', 'c', 'm', 'p', '.', 'c', 'o', 'r', '.', 'd', '\0', // "fcmp.cor.d"
'f', 'c', 'm', 'p', '.', 'c', 'u', 'n', 'e', '.', 'd', '\0', // "fcmp.cune.d"
'f', 'c', 'm', 'p', '.', 's', 'a', 'f', '.', 's', '\0', // "fcmp.saf.s"
'f', 'c', 'm', 'p', '.', 's', 'u', 'n', '.', 's', '\0', // "fcmp.sun.s"
'f', 'c', 'm', 'p', '.', 's', 'e', 'q', '.', 's', '\0', // "fcmp.seq.s"
'f', 'c', 'm', 'p', '.', 's', 'u', 'e', 'q', '.', 's', '\0', // "fcmp.sueq.s"
'f', 'c', 'm', 'p', '.', 's', 'l', 't', '.', 's', '\0', // "fcmp.slt.s"
'f', 'c', 'm', 'p', '.', 's', 'u', 'l', 't', '.', 's', '\0', // "fcmp.sult.s"
'f', 'c', 'm', 'p', '.', 's', 'l', 'e', '.', 's', '\0', // "fcmp.sle.s"
'f', 'c', 'm', 'p', '.', 's', 'u', 'l', 'e', '.', 's', '\0', // "fcmp.sule.s"
'f', 'c', 'm', 'p', '.', 's', 'n', 'e', '.', 's', '\0', // "fcmp.sne.s"
'f', 'c', 'm', 'p', '.', 's', 'o', 'r', '.', 's', '\0', // "fcmp.sor.s"
'f', 'c', 'm', 'p', '.', 's', 'u', 'n', 'e', '.', 's', '\0', // "fcmp.sune.s"
'f', 'l', 'd', '.', 's', '\0', // "fld.s"
'f', 'l', 'd', '.', 'd', '\0', // "fld.d"
'f', 's', 't', '.', 's', '\0', // "fst.s"
'f', 's', 't', '.', 'd', '\0', // "fst.d"
'v', 'f', 'm', 'a', 'd', 'd', '.', 's', '\0', // "vfmadd.s"
'v', 'f', 'm', 'a', 'd', 'd', '.', 'd', '\0', // "vfmadd.d"
'v', 'f', 'm', 's', 'u', 'b', '.', 's', '\0', // "vfmsub.s"
'v', 'f', 'm', 's', 'u', 'b', '.', 'd', '\0', // "vfmsub.d"
'v', 'f', 'n', 'm', 'a', 'd', 'd', '.', 's', '\0', // "vfnmadd.s"
'v', 'f', 'n', 'm', 'a', 'd', 'd', '.', 'd', '\0', // "vfnmadd.d"
'v', 'f', 'n', 'm', 's', 'u', 'b', '.', 's', '\0', // "vfnmsub.s"
'v', 'f', 'n', 'm', 's', 'u', 'b', '.', 'd', '\0', // "vfnmsub.d"
'v', 'b', 'i', 't', 's', 'e', 'l', '.', 'v', '\0', // "vbitsel.v"
'v', 's', 'h', 'u', 'f', '.', 'b', '\0', // "vshuf.b"
'v', 'l', 'd', 'x', '\0', // "vldx"
'v', 's', 't', 'x', '\0', // "vstx"
'v', 'r', 'e', 'p', 'l', 'v', 'e', '.', 'b', '\0', // "vreplve.b"
'v', 'r', 'e', 'p', 'l', 'v', 'e', '.', 'h', '\0', // "vreplve.h"
'v', 'r', 'e', 'p', 'l', 'v', 'e', '.', 'w', '\0', // "vreplve.w"
'v', 'r', 'e', 'p', 'l', 'v', 'e', '.', 'd', '\0', // "vreplve.d"
'v', 'f', 'c', 'm', 'p', '.', 'c', 'a', 'f', '.', 's', '\0', // "vfcmp.caf.s"
'v', 'f', 'c', 'm', 'p', '.', 'c', 'u', 'n', '.', 's', '\0', // "vfcmp.cun.s"
'v', 'f', 'c', 'm', 'p', '.', 'c', 'e', 'q', '.', 's', '\0', // "vfcmp.ceq.s"
'v', 'f', 'c', 'm', 'p', '.', 'c', 'u', 'e', 'q', '.', 's', '\0', // "vfcmp.cueq.s"
'v', 'f', 'c', 'm', 'p', '.', 'c', 'l', 't', '.', 's', '\0', // "vfcmp.clt.s"
'v', 'f', 'c', 'm', 'p', '.', 'c', 'u', 'l', 't', '.', 's', '\0', // "vfcmp.cult.s"
'v', 'f', 'c', 'm', 'p', '.', 'c', 'l', 'e', '.', 's', '\0', // "vfcmp.cle.s"
'v', 'f', 'c', 'm', 'p', '.', 'c', 'u', 'l', 'e', '.', 's', '\0', // "vfcmp.cule.s"
'v', 'f', 'c', 'm', 'p', '.', 'c', 'n', 'e', '.', 's', '\0', // "vfcmp.cne.s"
'v', 'f', 'c', 'm', 'p', '.', 'c', 'o', 'r', '.', 's', '\0', // "vfcmp.cor.s"
'v', 'f', 'c', 'm', 'p', '.', 'c', 'u', 'n', 'e', '.', 's', '\0', // "vfcmp.cune.s"
'v', 'f', 'c', 'm', 'p', '.', 's', 'a', 'f', '.', 's', '\0', // "vfcmp.saf.s"
'v', 'f', 'c', 'm', 'p', '.', 's', 'u', 'n', '.', 's', '\0', // "vfcmp.sun.s"
'v', 'f', 'c', 'm', 'p', '.', 's', 'e', 'q', '.', 's', '\0', // "vfcmp.seq.s"
'v', 'f', 'c', 'm', 'p', '.', 's', 'u', 'e', 'q', '.', 's', '\0', // "vfcmp.sueq.s"
'v', 'f', 'c', 'm', 'p', '.', 's', 'l', 't', '.', 's', '\0', // "vfcmp.slt.s"
'v', 'f', 'c', 'm', 'p', '.', 's', 'u', 'l', 't', '.', 's', '\0', // "vfcmp.sult.s"
'v', 'f', 'c', 'm', 'p', '.', 's', 'l', 'e', '.', 's', '\0', // "vfcmp.sle.s"
'v', 'f', 'c', 'm', 'p', '.', 's', 'u', 'l', 'e', '.', 's', '\0', // "vfcmp.sule.s"
'v', 'f', 'c', 'm', 'p', '.', 's', 'n', 'e', '.', 's', '\0', // "vfcmp.sne.s"
'v', 'f', 'c', 'm', 'p', '.', 's', 'o', 'r', '.', 's', '\0', // "vfcmp.sor.s"
'v', 'f', 'c', 'm', 'p', '.', 's', 'u', 'n', 'e', '.', 's', '\0', // "vfcmp.sune.s"
'v', 'f', 'c', 'm', 'p', '.', 'c', 'a', 'f', '.', 'd', '\0', // "vfcmp.caf.d"
'v', 'f', 'c', 'm', 'p', '.', 'c', 'u', 'n', '.', 'd', '\0', // "vfcmp.cun.d"
'v', 'f', 'c', 'm', 'p', '.', 'c', 'e', 'q', '.', 'd', '\0', // "vfcmp.ceq.d"
'v', 'f', 'c', 'm', 'p', '.', 'c', 'u', 'e', 'q', '.', 'd', '\0', // "vfcmp.cueq.d"
'v', 'f', 'c', 'm', 'p', '.', 'c', 'l', 't', '.', 'd', '\0', // "vfcmp.clt.d"
'v', 'f', 'c', 'm', 'p', '.', 'c', 'u', 'l', 't', '.', 'd', '\0', // "vfcmp.cult.d"
'v', 'f', 'c', 'm', 'p', '.', 'c', 'l', 'e', '.', 'd', '\0', // "vfcmp.cle.d"
'v', 'f', 'c', 'm', 'p', '.', 'c', 'u', 'l', 'e', '.', 'd', '\0', // "vfcmp.cule.d"
'v', 'f', 'c', 'm', 'p', '.', 'c', 'n', 'e', '.', 'd', '\0', // "vfcmp.cne.d"
'v', 'f', 'c', 'm', 'p', '.', 'c', 'o', 'r', '.', 'd', '\0', // "vfcmp.cor.d"
'v', 'f', 'c', 'm', 'p', '.', 'c', 'u', 'n', 'e', '.', 'd', '\0', // "vfcmp.cune.d"
'v', 'f', 'c', 'm', 'p', '.', 's', 'a', 'f', '.', 'd', '\0', // "vfcmp.saf.d"
'v', 'f', 'c', 'm', 'p', '.', 's', 'u', 'n', '.', 'd', '\0', // "vfcmp.sun.d"
'v', 'f', 'c', 'm', 'p', '.', 's', 'e', 'q', '.', 'd', '\0', // "vfcmp.seq.d"
'v', 'f', 'c', 'm', 'p', '.', 's', 'u', 'e', 'q', '.', 'd', '\0', // "vfcmp.sueq.d"
'v', 'f', 'c', 'm', 'p', '.', 's', 'l', 't', '.', 'd', '\0', // "vfcmp.slt.d"
'v', 'f', 'c', 'm', 'p', '.', 's', 'u', 'l', 't', '.', 'd', '\0', // "vfcmp.sult.d"
'v', 'f', 'c', 'm', 'p', '.', 's', 'l', 'e', '.', 'd', '\0', // "vfcmp.sle.d"
'v', 'f', 'c', 'm', 'p', '.', 's', 'u', 'l', 'e', '.', 'd', '\0', // "vfcmp.sule.d"
'v', 'f', 'c', 'm', 'p', '.', 's', 'n', 'e', '.', 'd', '\0', // "vfcmp.sne.d"
'v', 'f', 'c', 'm', 'p', '.', 's', 'o', 'r', '.', 'd', '\0', // "vfcmp.sor.d"
'v', 'f', 'c', 'm', 'p', '.', 's', 'u', 'n', 'e', '.', 'd', '\0', // "vfcmp.sune.d"
'v', 's', 'e', 'q', '.', 'b', '\0', // "vseq.b"
'v', 's', 'e', 'q', '.', 'h', '\0', // "vseq.h"
'v', 's', 'e', 'q', '.', 'w', '\0', // "vseq.w"
'v', 's', 'e', 'q', '.', 'd', '\0', // "vseq.d"
'v', 's', 'l', 'e', '.', 'b', '\0', // "vsle.b"
'v', 's', 'l', 'e', '.', 'h', '\0', // "vsle.h"
'v', 's', 'l', 'e', '.', 'w', '\0', // "vsle.w"
'v', 's', 'l', 'e', '.', 'd', '\0', // "vsle.d"
'v', 's', 'l', 'e', '.', 'b', 'u', '\0', // "vsle.bu"
'v', 's', 'l', 'e', '.', 'h', 'u', '\0', // "vsle.hu"
'v', 's', 'l', 'e', '.', 'w', 'u', '\0', // "vsle.wu"
'v', 's', 'l', 'e', '.', 'd', 'u', '\0', // "vsle.du"
'v', 's', 'l', 't', '.', 'b', '\0', // "vslt.b"
'v', 's', 'l', 't', '.', 'h', '\0', // "vslt.h"
'v', 's', 'l', 't', '.', 'w', '\0', // "vslt.w"
'v', 's', 'l', 't', '.', 'd', '\0', // "vslt.d"
'v', 's', 'l', 't', '.', 'b', 'u', '\0', // "vslt.bu"
'v', 's', 'l', 't', '.', 'h', 'u', '\0', // "vslt.hu"
'v', 's', 'l', 't', '.', 'w', 'u', '\0', // "vslt.wu"
'v', 's', 'l', 't', '.', 'd', 'u', '\0', // "vslt.du"
'v', 'a', 'd', 'd', '.', 'b', '\0', // "vadd.b"
'v', 'a', 'd', 'd', '.', 'h', '\0', // "vadd.h"
'v', 'a', 'd', 'd', '.', 'w', '\0', // "vadd.w"
'v', 'a', 'd', 'd', '.', 'd', '\0', // "vadd.d"
'v', 's', 'u', 'b', '.', 'b', '\0', // "vsub.b"
'v', 's', 'u', 'b', '.', 'h', '\0', // "vsub.h"
'v', 's', 'u', 'b', '.', 'w', '\0', // "vsub.w"
'v', 's', 'u', 'b', '.', 'd', '\0', // "vsub.d"
'v', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'h', '.', 'b', '\0', // "vaddwev.h.b"
'v', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'w', '.', 'h', '\0', // "vaddwev.w.h"
'v', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'd', '.', 'w', '\0', // "vaddwev.d.w"
'v', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'q', '.', 'd', '\0', // "vaddwev.q.d"
'v', 's', 'u', 'b', 'w', 'e', 'v', '.', 'h', '.', 'b', '\0', // "vsubwev.h.b"
'v', 's', 'u', 'b', 'w', 'e', 'v', '.', 'w', '.', 'h', '\0', // "vsubwev.w.h"
'v', 's', 'u', 'b', 'w', 'e', 'v', '.', 'd', '.', 'w', '\0', // "vsubwev.d.w"
'v', 's', 'u', 'b', 'w', 'e', 'v', '.', 'q', '.', 'd', '\0', // "vsubwev.q.d"
'v', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'h', '.', 'b', '\0', // "vaddwod.h.b"
'v', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'w', '.', 'h', '\0', // "vaddwod.w.h"
'v', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'd', '.', 'w', '\0', // "vaddwod.d.w"
'v', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'q', '.', 'd', '\0', // "vaddwod.q.d"
'v', 's', 'u', 'b', 'w', 'o', 'd', '.', 'h', '.', 'b', '\0', // "vsubwod.h.b"
'v', 's', 'u', 'b', 'w', 'o', 'd', '.', 'w', '.', 'h', '\0', // "vsubwod.w.h"
'v', 's', 'u', 'b', 'w', 'o', 'd', '.', 'd', '.', 'w', '\0', // "vsubwod.d.w"
'v', 's', 'u', 'b', 'w', 'o', 'd', '.', 'q', '.', 'd', '\0', // "vsubwod.q.d"
'v', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'h', '.', 'b', 'u', '\0', // "vaddwev.h.bu"
'v', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'w', '.', 'h', 'u', '\0', // "vaddwev.w.hu"
'v', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'd', '.', 'w', 'u', '\0', // "vaddwev.d.wu"
'v', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'q', '.', 'd', 'u', '\0', // "vaddwev.q.du"
'v', 's', 'u', 'b', 'w', 'e', 'v', '.', 'h', '.', 'b', 'u', '\0', // "vsubwev.h.bu"
'v', 's', 'u', 'b', 'w', 'e', 'v', '.', 'w', '.', 'h', 'u', '\0', // "vsubwev.w.hu"
'v', 's', 'u', 'b', 'w', 'e', 'v', '.', 'd', '.', 'w', 'u', '\0', // "vsubwev.d.wu"
'v', 's', 'u', 'b', 'w', 'e', 'v', '.', 'q', '.', 'd', 'u', '\0', // "vsubwev.q.du"
'v', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'h', '.', 'b', 'u', '\0', // "vaddwod.h.bu"
'v', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'w', '.', 'h', 'u', '\0', // "vaddwod.w.hu"
'v', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'd', '.', 'w', 'u', '\0', // "vaddwod.d.wu"
'v', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'q', '.', 'd', 'u', '\0', // "vaddwod.q.du"
'v', 's', 'u', 'b', 'w', 'o', 'd', '.', 'h', '.', 'b', 'u', '\0', // "vsubwod.h.bu"
'v', 's', 'u', 'b', 'w', 'o', 'd', '.', 'w', '.', 'h', 'u', '\0', // "vsubwod.w.hu"
'v', 's', 'u', 'b', 'w', 'o', 'd', '.', 'd', '.', 'w', 'u', '\0', // "vsubwod.d.wu"
'v', 's', 'u', 'b', 'w', 'o', 'd', '.', 'q', '.', 'd', 'u', '\0', // "vsubwod.q.du"
'v', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'h', '.', 'b', 'u', '.', 'b', '\0', // "vaddwev.h.bu.b"
'v', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'w', '.', 'h', 'u', '.', 'h', '\0', // "vaddwev.w.hu.h"
'v', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'd', '.', 'w', 'u', '.', 'w', '\0', // "vaddwev.d.wu.w"
'v', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'q', '.', 'd', 'u', '.', 'd', '\0', // "vaddwev.q.du.d"
'v', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'h', '.', 'b', 'u', '.', 'b', '\0', // "vaddwod.h.bu.b"
'v', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'w', '.', 'h', 'u', '.', 'h', '\0', // "vaddwod.w.hu.h"
'v', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'd', '.', 'w', 'u', '.', 'w', '\0', // "vaddwod.d.wu.w"
'v', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'q', '.', 'd', 'u', '.', 'd', '\0', // "vaddwod.q.du.d"
'v', 's', 'a', 'd', 'd', '.', 'b', '\0', // "vsadd.b"
'v', 's', 'a', 'd', 'd', '.', 'h', '\0', // "vsadd.h"
'v', 's', 'a', 'd', 'd', '.', 'w', '\0', // "vsadd.w"
'v', 's', 'a', 'd', 'd', '.', 'd', '\0', // "vsadd.d"
'v', 's', 's', 'u', 'b', '.', 'b', '\0', // "vssub.b"
'v', 's', 's', 'u', 'b', '.', 'h', '\0', // "vssub.h"
'v', 's', 's', 'u', 'b', '.', 'w', '\0', // "vssub.w"
'v', 's', 's', 'u', 'b', '.', 'd', '\0', // "vssub.d"
'v', 's', 'a', 'd', 'd', '.', 'b', 'u', '\0', // "vsadd.bu"
'v', 's', 'a', 'd', 'd', '.', 'h', 'u', '\0', // "vsadd.hu"
'v', 's', 'a', 'd', 'd', '.', 'w', 'u', '\0', // "vsadd.wu"
'v', 's', 'a', 'd', 'd', '.', 'd', 'u', '\0', // "vsadd.du"
'v', 's', 's', 'u', 'b', '.', 'b', 'u', '\0', // "vssub.bu"
'v', 's', 's', 'u', 'b', '.', 'h', 'u', '\0', // "vssub.hu"
'v', 's', 's', 'u', 'b', '.', 'w', 'u', '\0', // "vssub.wu"
'v', 's', 's', 'u', 'b', '.', 'd', 'u', '\0', // "vssub.du"
'v', 'h', 'a', 'd', 'd', 'w', '.', 'h', '.', 'b', '\0', // "vhaddw.h.b"
'v', 'h', 'a', 'd', 'd', 'w', '.', 'w', '.', 'h', '\0', // "vhaddw.w.h"
'v', 'h', 'a', 'd', 'd', 'w', '.', 'd', '.', 'w', '\0', // "vhaddw.d.w"
'v', 'h', 'a', 'd', 'd', 'w', '.', 'q', '.', 'd', '\0', // "vhaddw.q.d"
'v', 'h', 's', 'u', 'b', 'w', '.', 'h', '.', 'b', '\0', // "vhsubw.h.b"
'v', 'h', 's', 'u', 'b', 'w', '.', 'w', '.', 'h', '\0', // "vhsubw.w.h"
'v', 'h', 's', 'u', 'b', 'w', '.', 'd', '.', 'w', '\0', // "vhsubw.d.w"
'v', 'h', 's', 'u', 'b', 'w', '.', 'q', '.', 'd', '\0', // "vhsubw.q.d"
'v', 'h', 'a', 'd', 'd', 'w', '.', 'h', 'u', '.', 'b', 'u', '\0', // "vhaddw.hu.bu"
'v', 'h', 'a', 'd', 'd', 'w', '.', 'w', 'u', '.', 'h', 'u', '\0', // "vhaddw.wu.hu"
'v', 'h', 'a', 'd', 'd', 'w', '.', 'd', 'u', '.', 'w', 'u', '\0', // "vhaddw.du.wu"
'v', 'h', 'a', 'd', 'd', 'w', '.', 'q', 'u', '.', 'd', 'u', '\0', // "vhaddw.qu.du"
'v', 'h', 's', 'u', 'b', 'w', '.', 'h', 'u', '.', 'b', 'u', '\0', // "vhsubw.hu.bu"
'v', 'h', 's', 'u', 'b', 'w', '.', 'w', 'u', '.', 'h', 'u', '\0', // "vhsubw.wu.hu"
'v', 'h', 's', 'u', 'b', 'w', '.', 'd', 'u', '.', 'w', 'u', '\0', // "vhsubw.du.wu"
'v', 'h', 's', 'u', 'b', 'w', '.', 'q', 'u', '.', 'd', 'u', '\0', // "vhsubw.qu.du"
'v', 'a', 'd', 'd', 'a', '.', 'b', '\0', // "vadda.b"
'v', 'a', 'd', 'd', 'a', '.', 'h', '\0', // "vadda.h"
'v', 'a', 'd', 'd', 'a', '.', 'w', '\0', // "vadda.w"
'v', 'a', 'd', 'd', 'a', '.', 'd', '\0', // "vadda.d"
'v', 'a', 'b', 's', 'd', '.', 'b', '\0', // "vabsd.b"
'v', 'a', 'b', 's', 'd', '.', 'h', '\0', // "vabsd.h"
'v', 'a', 'b', 's', 'd', '.', 'w', '\0', // "vabsd.w"
'v', 'a', 'b', 's', 'd', '.', 'd', '\0', // "vabsd.d"
'v', 'a', 'b', 's', 'd', '.', 'b', 'u', '\0', // "vabsd.bu"
'v', 'a', 'b', 's', 'd', '.', 'h', 'u', '\0', // "vabsd.hu"
'v', 'a', 'b', 's', 'd', '.', 'w', 'u', '\0', // "vabsd.wu"
'v', 'a', 'b', 's', 'd', '.', 'd', 'u', '\0', // "vabsd.du"
'v', 'a', 'v', 'g', '.', 'b', '\0', // "vavg.b"
'v', 'a', 'v', 'g', '.', 'h', '\0', // "vavg.h"
'v', 'a', 'v', 'g', '.', 'w', '\0', // "vavg.w"
'v', 'a', 'v', 'g', '.', 'd', '\0', // "vavg.d"
'v', 'a', 'v', 'g', '.', 'b', 'u', '\0', // "vavg.bu"
'v', 'a', 'v', 'g', '.', 'h', 'u', '\0', // "vavg.hu"
'v', 'a', 'v', 'g', '.', 'w', 'u', '\0', // "vavg.wu"
'v', 'a', 'v', 'g', '.', 'd', 'u', '\0', // "vavg.du"
'v', 'a', 'v', 'g', 'r', '.', 'b', '\0', // "vavgr.b"
'v', 'a', 'v', 'g', 'r', '.', 'h', '\0', // "vavgr.h"
'v', 'a', 'v', 'g', 'r', '.', 'w', '\0', // "vavgr.w"
'v', 'a', 'v', 'g', 'r', '.', 'd', '\0', // "vavgr.d"
'v', 'a', 'v', 'g', 'r', '.', 'b', 'u', '\0', // "vavgr.bu"
'v', 'a', 'v', 'g', 'r', '.', 'h', 'u', '\0', // "vavgr.hu"
'v', 'a', 'v', 'g', 'r', '.', 'w', 'u', '\0', // "vavgr.wu"
'v', 'a', 'v', 'g', 'r', '.', 'd', 'u', '\0', // "vavgr.du"
'v', 'm', 'a', 'x', '.', 'b', '\0', // "vmax.b"
'v', 'm', 'a', 'x', '.', 'h', '\0', // "vmax.h"
'v', 'm', 'a', 'x', '.', 'w', '\0', // "vmax.w"
'v', 'm', 'a', 'x', '.', 'd', '\0', // "vmax.d"
'v', 'm', 'i', 'n', '.', 'b', '\0', // "vmin.b"
'v', 'm', 'i', 'n', '.', 'h', '\0', // "vmin.h"
'v', 'm', 'i', 'n', '.', 'w', '\0', // "vmin.w"
'v', 'm', 'i', 'n', '.', 'd', '\0', // "vmin.d"
'v', 'm', 'a', 'x', '.', 'b', 'u', '\0', // "vmax.bu"
'v', 'm', 'a', 'x', '.', 'h', 'u', '\0', // "vmax.hu"
'v', 'm', 'a', 'x', '.', 'w', 'u', '\0', // "vmax.wu"
'v', 'm', 'a', 'x', '.', 'd', 'u', '\0', // "vmax.du"
'v', 'm', 'i', 'n', '.', 'b', 'u', '\0', // "vmin.bu"
'v', 'm', 'i', 'n', '.', 'h', 'u', '\0', // "vmin.hu"
'v', 'm', 'i', 'n', '.', 'w', 'u', '\0', // "vmin.wu"
'v', 'm', 'i', 'n', '.', 'd', 'u', '\0', // "vmin.du"
'v', 'm', 'u', 'l', '.', 'b', '\0', // "vmul.b"
'v', 'm', 'u', 'l', '.', 'h', '\0', // "vmul.h"
'v', 'm', 'u', 'l', '.', 'w', '\0', // "vmul.w"
'v', 'm', 'u', 'l', '.', 'd', '\0', // "vmul.d"
'v', 'm', 'u', 'h', '.', 'b', '\0', // "vmuh.b"
'v', 'm', 'u', 'h', '.', 'h', '\0', // "vmuh.h"
'v', 'm', 'u', 'h', '.', 'w', '\0', // "vmuh.w"
'v', 'm', 'u', 'h', '.', 'd', '\0', // "vmuh.d"
'v', 'm', 'u', 'h', '.', 'b', 'u', '\0', // "vmuh.bu"
'v', 'm', 'u', 'h', '.', 'h', 'u', '\0', // "vmuh.hu"
'v', 'm', 'u', 'h', '.', 'w', 'u', '\0', // "vmuh.wu"
'v', 'm', 'u', 'h', '.', 'd', 'u', '\0', // "vmuh.du"
'v', 'm', 'u', 'l', 'w', 'e', 'v', '.', 'h', '.', 'b', '\0', // "vmulwev.h.b"
'v', 'm', 'u', 'l', 'w', 'e', 'v', '.', 'w', '.', 'h', '\0', // "vmulwev.w.h"
'v', 'm', 'u', 'l', 'w', 'e', 'v', '.', 'd', '.', 'w', '\0', // "vmulwev.d.w"
'v', 'm', 'u', 'l', 'w', 'e', 'v', '.', 'q', '.', 'd', '\0', // "vmulwev.q.d"
'v', 'm', 'u', 'l', 'w', 'o', 'd', '.', 'h', '.', 'b', '\0', // "vmulwod.h.b"
'v', 'm', 'u', 'l', 'w', 'o', 'd', '.', 'w', '.', 'h', '\0', // "vmulwod.w.h"
'v', 'm', 'u', 'l', 'w', 'o', 'd', '.', 'd', '.', 'w', '\0', // "vmulwod.d.w"
'v', 'm', 'u', 'l', 'w', 'o', 'd', '.', 'q', '.', 'd', '\0', // "vmulwod.q.d"
'v', 'm', 'u', 'l', 'w', 'e', 'v', '.', 'h', '.', 'b', 'u', '\0', // "vmulwev.h.bu"
'v', 'm', 'u', 'l', 'w', 'e', 'v', '.', 'w', '.', 'h', 'u', '\0', // "vmulwev.w.hu"
'v', 'm', 'u', 'l', 'w', 'e', 'v', '.', 'd', '.', 'w', 'u', '\0', // "vmulwev.d.wu"
'v', 'm', 'u', 'l', 'w', 'e', 'v', '.', 'q', '.', 'd', 'u', '\0', // "vmulwev.q.du"
'v', 'm', 'u', 'l', 'w', 'o', 'd', '.', 'h', '.', 'b', 'u', '\0', // "vmulwod.h.bu"
'v', 'm', 'u', 'l', 'w', 'o', 'd', '.', 'w', '.', 'h', 'u', '\0', // "vmulwod.w.hu"
'v', 'm', 'u', 'l', 'w', 'o', 'd', '.', 'd', '.', 'w', 'u', '\0', // "vmulwod.d.wu"
'v', 'm', 'u', 'l', 'w', 'o', 'd', '.', 'q', '.', 'd', 'u', '\0', // "vmulwod.q.du"
'v', 'm', 'u', 'l', 'w', 'e', 'v', '.', 'h', '.', 'b', 'u', '.', 'b', '\0', // "vmulwev.h.bu.b"
'v', 'm', 'u', 'l', 'w', 'e', 'v', '.', 'w', '.', 'h', 'u', '.', 'h', '\0', // "vmulwev.w.hu.h"
'v', 'm', 'u', 'l', 'w', 'e', 'v', '.', 'd', '.', 'w', 'u', '.', 'w', '\0', // "vmulwev.d.wu.w"
'v', 'm', 'u', 'l', 'w', 'e', 'v', '.', 'q', '.', 'd', 'u', '.', 'd', '\0', // "vmulwev.q.du.d"
'v', 'm', 'u', 'l', 'w', 'o', 'd', '.', 'h', '.', 'b', 'u', '.', 'b', '\0', // "vmulwod.h.bu.b"
'v', 'm', 'u', 'l', 'w', 'o', 'd', '.', 'w', '.', 'h', 'u', '.', 'h', '\0', // "vmulwod.w.hu.h"
'v', 'm', 'u', 'l', 'w', 'o', 'd', '.', 'd', '.', 'w', 'u', '.', 'w', '\0', // "vmulwod.d.wu.w"
'v', 'm', 'u', 'l', 'w', 'o', 'd', '.', 'q', '.', 'd', 'u', '.', 'd', '\0', // "vmulwod.q.du.d"
'v', 'm', 'a', 'd', 'd', '.', 'b', '\0', // "vmadd.b"
'v', 'm', 'a', 'd', 'd', '.', 'h', '\0', // "vmadd.h"
'v', 'm', 'a', 'd', 'd', '.', 'w', '\0', // "vmadd.w"
'v', 'm', 'a', 'd', 'd', '.', 'd', '\0', // "vmadd.d"
'v', 'm', 's', 'u', 'b', '.', 'b', '\0', // "vmsub.b"
'v', 'm', 's', 'u', 'b', '.', 'h', '\0', // "vmsub.h"
'v', 'm', 's', 'u', 'b', '.', 'w', '\0', // "vmsub.w"
'v', 'm', 's', 'u', 'b', '.', 'd', '\0', // "vmsub.d"
'v', 'm', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'h', '.', 'b', '\0', // "vmaddwev.h.b"
'v', 'm', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'w', '.', 'h', '\0', // "vmaddwev.w.h"
'v', 'm', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'd', '.', 'w', '\0', // "vmaddwev.d.w"
'v', 'm', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'q', '.', 'd', '\0', // "vmaddwev.q.d"
'v', 'm', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'h', '.', 'b', '\0', // "vmaddwod.h.b"
'v', 'm', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'w', '.', 'h', '\0', // "vmaddwod.w.h"
'v', 'm', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'd', '.', 'w', '\0', // "vmaddwod.d.w"
'v', 'm', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'q', '.', 'd', '\0', // "vmaddwod.q.d"
'v', 'm', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'h', '.', 'b', 'u', '\0', // "vmaddwev.h.bu"
'v', 'm', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'w', '.', 'h', 'u', '\0', // "vmaddwev.w.hu"
'v', 'm', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'd', '.', 'w', 'u', '\0', // "vmaddwev.d.wu"
'v', 'm', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'q', '.', 'd', 'u', '\0', // "vmaddwev.q.du"
'v', 'm', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'h', '.', 'b', 'u', '\0', // "vmaddwod.h.bu"
'v', 'm', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'w', '.', 'h', 'u', '\0', // "vmaddwod.w.hu"
'v', 'm', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'd', '.', 'w', 'u', '\0', // "vmaddwod.d.wu"
'v', 'm', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'q', '.', 'd', 'u', '\0', // "vmaddwod.q.du"
'v', 'm', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'h', '.', 'b', 'u', '.', 'b', '\0', // "vmaddwev.h.bu.b"
'v', 'm', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'w', '.', 'h', 'u', '.', 'h', '\0', // "vmaddwev.w.hu.h"
'v', 'm', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'd', '.', 'w', 'u', '.', 'w', '\0', // "vmaddwev.d.wu.w"
'v', 'm', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'q', '.', 'd', 'u', '.', 'd', '\0', // "vmaddwev.q.du.d"
'v', 'm', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'h', '.', 'b', 'u', '.', 'b', '\0', // "vmaddwod.h.bu.b"
'v', 'm', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'w', '.', 'h', 'u', '.', 'h', '\0', // "vmaddwod.w.hu.h"
'v', 'm', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'd', '.', 'w', 'u', '.', 'w', '\0', // "vmaddwod.d.wu.w"
'v', 'm', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'q', '.', 'd', 'u', '.', 'd', '\0', // "vmaddwod.q.du.d"
'v', 'd', 'i', 'v', '.', 'b', '\0', // "vdiv.b"
'v', 'd', 'i', 'v', '.', 'h', '\0', // "vdiv.h"
'v', 'd', 'i', 'v', '.', 'w', '\0', // "vdiv.w"
'v', 'd', 'i', 'v', '.', 'd', '\0', // "vdiv.d"
'v', 'm', 'o', 'd', '.', 'b', '\0', // "vmod.b"
'v', 'm', 'o', 'd', '.', 'h', '\0', // "vmod.h"
'v', 'm', 'o', 'd', '.', 'w', '\0', // "vmod.w"
'v', 'm', 'o', 'd', '.', 'd', '\0', // "vmod.d"
'v', 'd', 'i', 'v', '.', 'b', 'u', '\0', // "vdiv.bu"
'v', 'd', 'i', 'v', '.', 'h', 'u', '\0', // "vdiv.hu"
'v', 'd', 'i', 'v', '.', 'w', 'u', '\0', // "vdiv.wu"
'v', 'd', 'i', 'v', '.', 'd', 'u', '\0', // "vdiv.du"
'v', 'm', 'o', 'd', '.', 'b', 'u', '\0', // "vmod.bu"
'v', 'm', 'o', 'd', '.', 'h', 'u', '\0', // "vmod.hu"
'v', 'm', 'o', 'd', '.', 'w', 'u', '\0', // "vmod.wu"
'v', 'm', 'o', 'd', '.', 'd', 'u', '\0', // "vmod.du"
'v', 's', 'l', 'l', '.', 'b', '\0', // "vsll.b"
'v', 's', 'l', 'l', '.', 'h', '\0', // "vsll.h"
'v', 's', 'l', 'l', '.', 'w', '\0', // "vsll.w"
'v', 's', 'l', 'l', '.', 'd', '\0', // "vsll.d"
'v', 's', 'r', 'l', '.', 'b', '\0', // "vsrl.b"
'v', 's', 'r', 'l', '.', 'h', '\0', // "vsrl.h"
'v', 's', 'r', 'l', '.', 'w', '\0', // "vsrl.w"
'v', 's', 'r', 'l', '.', 'd', '\0', // "vsrl.d"
'v', 's', 'r', 'a', '.', 'b', '\0', // "vsra.b"
'v', 's', 'r', 'a', '.', 'h', '\0', // "vsra.h"
'v', 's', 'r', 'a', '.', 'w', '\0', // "vsra.w"
'v', 's', 'r', 'a', '.', 'd', '\0', // "vsra.d"
'v', 'r', 'o', 't', 'r', '.', 'b', '\0', // "vrotr.b"
'v', 'r', 'o', 't', 'r', '.', 'h', '\0', // "vrotr.h"
'v', 'r', 'o', 't', 'r', '.', 'w', '\0', // "vrotr.w"
'v', 'r', 'o', 't', 'r', '.', 'd', '\0', // "vrotr.d"
'v', 's', 'r', 'l', 'r', '.', 'b', '\0', // "vsrlr.b"
'v', 's', 'r', 'l', 'r', '.', 'h', '\0', // "vsrlr.h"
'v', 's', 'r', 'l', 'r', '.', 'w', '\0', // "vsrlr.w"
'v', 's', 'r', 'l', 'r', '.', 'd', '\0', // "vsrlr.d"
'v', 's', 'r', 'a', 'r', '.', 'b', '\0', // "vsrar.b"
'v', 's', 'r', 'a', 'r', '.', 'h', '\0', // "vsrar.h"
'v', 's', 'r', 'a', 'r', '.', 'w', '\0', // "vsrar.w"
'v', 's', 'r', 'a', 'r', '.', 'd', '\0', // "vsrar.d"
'v', 's', 'r', 'l', 'n', '.', 'b', '.', 'h', '\0', // "vsrln.b.h"
'v', 's', 'r', 'l', 'n', '.', 'h', '.', 'w', '\0', // "vsrln.h.w"
'v', 's', 'r', 'l', 'n', '.', 'w', '.', 'd', '\0', // "vsrln.w.d"
'v', 's', 'r', 'a', 'n', '.', 'b', '.', 'h', '\0', // "vsran.b.h"
'v', 's', 'r', 'a', 'n', '.', 'h', '.', 'w', '\0', // "vsran.h.w"
'v', 's', 'r', 'a', 'n', '.', 'w', '.', 'd', '\0', // "vsran.w.d"
'v', 's', 'r', 'l', 'r', 'n', '.', 'b', '.', 'h', '\0', // "vsrlrn.b.h"
'v', 's', 'r', 'l', 'r', 'n', '.', 'h', '.', 'w', '\0', // "vsrlrn.h.w"
'v', 's', 'r', 'l', 'r', 'n', '.', 'w', '.', 'd', '\0', // "vsrlrn.w.d"
'v', 's', 'r', 'a', 'r', 'n', '.', 'b', '.', 'h', '\0', // "vsrarn.b.h"
'v', 's', 'r', 'a', 'r', 'n', '.', 'h', '.', 'w', '\0', // "vsrarn.h.w"
'v', 's', 'r', 'a', 'r', 'n', '.', 'w', '.', 'd', '\0', // "vsrarn.w.d"
'v', 's', 's', 'r', 'l', 'n', '.', 'b', '.', 'h', '\0', // "vssrln.b.h"
'v', 's', 's', 'r', 'l', 'n', '.', 'h', '.', 'w', '\0', // "vssrln.h.w"
'v', 's', 's', 'r', 'l', 'n', '.', 'w', '.', 'd', '\0', // "vssrln.w.d"
'v', 's', 's', 'r', 'a', 'n', '.', 'b', '.', 'h', '\0', // "vssran.b.h"
'v', 's', 's', 'r', 'a', 'n', '.', 'h', '.', 'w', '\0', // "vssran.h.w"
'v', 's', 's', 'r', 'a', 'n', '.', 'w', '.', 'd', '\0', // "vssran.w.d"
'v', 's', 's', 'r', 'l', 'r', 'n', '.', 'b', '.', 'h', '\0', // "vssrlrn.b.h"
'v', 's', 's', 'r', 'l', 'r', 'n', '.', 'h', '.', 'w', '\0', // "vssrlrn.h.w"
'v', 's', 's', 'r', 'l', 'r', 'n', '.', 'w', '.', 'd', '\0', // "vssrlrn.w.d"
'v', 's', 's', 'r', 'a', 'r', 'n', '.', 'b', '.', 'h', '\0', // "vssrarn.b.h"
'v', 's', 's', 'r', 'a', 'r', 'n', '.', 'h', '.', 'w', '\0', // "vssrarn.h.w"
'v', 's', 's', 'r', 'a', 'r', 'n', '.', 'w', '.', 'd', '\0', // "vssrarn.w.d"
'v', 's', 's', 'r', 'l', 'n', '.', 'b', 'u', '.', 'h', '\0', // "vssrln.bu.h"
'v', 's', 's', 'r', 'l', 'n', '.', 'h', 'u', '.', 'w', '\0', // "vssrln.hu.w"
'v', 's', 's', 'r', 'l', 'n', '.', 'w', 'u', '.', 'd', '\0', // "vssrln.wu.d"
'v', 's', 's', 'r', 'a', 'n', '.', 'b', 'u', '.', 'h', '\0', // "vssran.bu.h"
'v', 's', 's', 'r', 'a', 'n', '.', 'h', 'u', '.', 'w', '\0', // "vssran.hu.w"
'v', 's', 's', 'r', 'a', 'n', '.', 'w', 'u', '.', 'd', '\0', // "vssran.wu.d"
'v', 's', 's', 'r', 'l', 'r', 'n', '.', 'b', 'u', '.', 'h', '\0', // "vssrlrn.bu.h"
'v', 's', 's', 'r', 'l', 'r', 'n', '.', 'h', 'u', '.', 'w', '\0', // "vssrlrn.hu.w"
'v', 's', 's', 'r', 'l', 'r', 'n', '.', 'w', 'u', '.', 'd', '\0', // "vssrlrn.wu.d"
'v', 's', 's', 'r', 'a', 'r', 'n', '.', 'b', 'u', '.', 'h', '\0', // "vssrarn.bu.h"
'v', 's', 's', 'r', 'a', 'r', 'n', '.', 'h', 'u', '.', 'w', '\0', // "vssrarn.hu.w"
'v', 's', 's', 'r', 'a', 'r', 'n', '.', 'w', 'u', '.', 'd', '\0', // "vssrarn.wu.d"
'v', 'b', 'i', 't', 'c', 'l', 'r', '.', 'b', '\0', // "vbitclr.b"
'v', 'b', 'i', 't', 'c', 'l', 'r', '.', 'h', '\0', // "vbitclr.h"
'v', 'b', 'i', 't', 'c', 'l', 'r', '.', 'w', '\0', // "vbitclr.w"
'v', 'b', 'i', 't', 'c', 'l', 'r', '.', 'd', '\0', // "vbitclr.d"
'v', 'b', 'i', 't', 's', 'e', 't', '.', 'b', '\0', // "vbitset.b"
'v', 'b', 'i', 't', 's', 'e', 't', '.', 'h', '\0', // "vbitset.h"
'v', 'b', 'i', 't', 's', 'e', 't', '.', 'w', '\0', // "vbitset.w"
'v', 'b', 'i', 't', 's', 'e', 't', '.', 'd', '\0', // "vbitset.d"
'v', 'b', 'i', 't', 'r', 'e', 'v', '.', 'b', '\0', // "vbitrev.b"
'v', 'b', 'i', 't', 'r', 'e', 'v', '.', 'h', '\0', // "vbitrev.h"
'v', 'b', 'i', 't', 'r', 'e', 'v', '.', 'w', '\0', // "vbitrev.w"
'v', 'b', 'i', 't', 'r', 'e', 'v', '.', 'd', '\0', // "vbitrev.d"
'v', 'p', 'a', 'c', 'k', 'e', 'v', '.', 'b', '\0', // "vpackev.b"
'v', 'p', 'a', 'c', 'k', 'e', 'v', '.', 'h', '\0', // "vpackev.h"
'v', 'p', 'a', 'c', 'k', 'e', 'v', '.', 'w', '\0', // "vpackev.w"
'v', 'p', 'a', 'c', 'k', 'e', 'v', '.', 'd', '\0', // "vpackev.d"
'v', 'p', 'a', 'c', 'k', 'o', 'd', '.', 'b', '\0', // "vpackod.b"
'v', 'p', 'a', 'c', 'k', 'o', 'd', '.', 'h', '\0', // "vpackod.h"
'v', 'p', 'a', 'c', 'k', 'o', 'd', '.', 'w', '\0', // "vpackod.w"
'v', 'p', 'a', 'c', 'k', 'o', 'd', '.', 'd', '\0', // "vpackod.d"
'v', 'i', 'l', 'v', 'l', '.', 'b', '\0', // "vilvl.b"
'v', 'i', 'l', 'v', 'l', '.', 'h', '\0', // "vilvl.h"
'v', 'i', 'l', 'v', 'l', '.', 'w', '\0', // "vilvl.w"
'v', 'i', 'l', 'v', 'l', '.', 'd', '\0', // "vilvl.d"
'v', 'i', 'l', 'v', 'h', '.', 'b', '\0', // "vilvh.b"
'v', 'i', 'l', 'v', 'h', '.', 'h', '\0', // "vilvh.h"
'v', 'i', 'l', 'v', 'h', '.', 'w', '\0', // "vilvh.w"
'v', 'i', 'l', 'v', 'h', '.', 'd', '\0', // "vilvh.d"
'v', 'p', 'i', 'c', 'k', 'e', 'v', '.', 'b', '\0', // "vpickev.b"
'v', 'p', 'i', 'c', 'k', 'e', 'v', '.', 'h', '\0', // "vpickev.h"
'v', 'p', 'i', 'c', 'k', 'e', 'v', '.', 'w', '\0', // "vpickev.w"
'v', 'p', 'i', 'c', 'k', 'e', 'v', '.', 'd', '\0', // "vpickev.d"
'v', 'p', 'i', 'c', 'k', 'o', 'd', '.', 'b', '\0', // "vpickod.b"
'v', 'p', 'i', 'c', 'k', 'o', 'd', '.', 'h', '\0', // "vpickod.h"
'v', 'p', 'i', 'c', 'k', 'o', 'd', '.', 'w', '\0', // "vpickod.w"
'v', 'p', 'i', 'c', 'k', 'o', 'd', '.', 'd', '\0', // "vpickod.d"
'v', 'a', 'n', 'd', '.', 'v', '\0', // "vand.v"
'v', 'o', 'r', '.', 'v', '\0', // "vor.v"
'v', 'x', 'o', 'r', '.', 'v', '\0', // "vxor.v"
'v', 'n', 'o', 'r', '.', 'v', '\0', // "vnor.v"
'v', 'a', 'n', 'd', 'n', '.', 'v', '\0', // "vandn.v"
'v', 'o', 'r', 'n', '.', 'v', '\0', // "vorn.v"
'v', 'f', 'r', 's', 't', 'p', '.', 'b', '\0', // "vfrstp.b"
'v', 'f', 'r', 's', 't', 'p', '.', 'h', '\0', // "vfrstp.h"
'v', 'a', 'd', 'd', '.', 'q', '\0', // "vadd.q"
'v', 's', 'u', 'b', '.', 'q', '\0', // "vsub.q"
'v', 's', 'i', 'g', 'n', 'c', 'o', 'v', '.', 'b', '\0', // "vsigncov.b"
'v', 's', 'i', 'g', 'n', 'c', 'o', 'v', '.', 'h', '\0', // "vsigncov.h"
'v', 's', 'i', 'g', 'n', 'c', 'o', 'v', '.', 'w', '\0', // "vsigncov.w"
'v', 's', 'i', 'g', 'n', 'c', 'o', 'v', '.', 'd', '\0', // "vsigncov.d"
'v', 'f', 'a', 'd', 'd', '.', 's', '\0', // "vfadd.s"
'v', 'f', 'a', 'd', 'd', '.', 'd', '\0', // "vfadd.d"
'v', 'f', 's', 'u', 'b', '.', 's', '\0', // "vfsub.s"
'v', 'f', 's', 'u', 'b', '.', 'd', '\0', // "vfsub.d"
'v', 'f', 'm', 'u', 'l', '.', 's', '\0', // "vfmul.s"
'v', 'f', 'm', 'u', 'l', '.', 'd', '\0', // "vfmul.d"
'v', 'f', 'd', 'i', 'v', '.', 's', '\0', // "vfdiv.s"
'v', 'f', 'd', 'i', 'v', '.', 'd', '\0', // "vfdiv.d"
'v', 'f', 'm', 'a', 'x', '.', 's', '\0', // "vfmax.s"
'v', 'f', 'm', 'a', 'x', '.', 'd', '\0', // "vfmax.d"
'v', 'f', 'm', 'i', 'n', '.', 's', '\0', // "vfmin.s"
'v', 'f', 'm', 'i', 'n', '.', 'd', '\0', // "vfmin.d"
'v', 'f', 'm', 'a', 'x', 'a', '.', 's', '\0', // "vfmaxa.s"
'v', 'f', 'm', 'a', 'x', 'a', '.', 'd', '\0', // "vfmaxa.d"
'v', 'f', 'm', 'i', 'n', 'a', '.', 's', '\0', // "vfmina.s"
'v', 'f', 'm', 'i', 'n', 'a', '.', 'd', '\0', // "vfmina.d"
'v', 'f', 'c', 'v', 't', '.', 'h', '.', 's', '\0', // "vfcvt.h.s"
'v', 'f', 'c', 'v', 't', '.', 's', '.', 'd', '\0', // "vfcvt.s.d"
'v', 'f', 'f', 'i', 'n', 't', '.', 's', '.', 'l', '\0', // "vffint.s.l"
'v', 'f', 't', 'i', 'n', 't', '.', 'w', '.', 'd', '\0', // "vftint.w.d"
'v', 'f', 't', 'i', 'n', 't', 'r', 'm', '.', 'w', '.', 'd', '\0', // "vftintrm.w.d"
'v', 'f', 't', 'i', 'n', 't', 'r', 'p', '.', 'w', '.', 'd', '\0', // "vftintrp.w.d"
'v', 'f', 't', 'i', 'n', 't', 'r', 'z', '.', 'w', '.', 'd', '\0', // "vftintrz.w.d"
'v', 'f', 't', 'i', 'n', 't', 'r', 'n', 'e', '.', 'w', '.', 'd', '\0', // "vftintrne.w.d"
'v', 's', 'h', 'u', 'f', '.', 'h', '\0', // "vshuf.h"
'v', 's', 'h', 'u', 'f', '.', 'w', '\0', // "vshuf.w"
'v', 's', 'h', 'u', 'f', '.', 'd', '\0', // "vshuf.d"
'v', 'r', 'e', 'p', 'l', 'g', 'r', '2', 'v', 'r', '.', 'b', '\0', // "vreplgr2vr.b"
'v', 'r', 'e', 'p', 'l', 'g', 'r', '2', 'v', 'r', '.', 'h', '\0', // "vreplgr2vr.h"
'v', 'r', 'e', 'p', 'l', 'g', 'r', '2', 'v', 'r', '.', 'w', '\0', // "vreplgr2vr.w"
'v', 'r', 'e', 'p', 'l', 'g', 'r', '2', 'v', 'r', '.', 'd', '\0', // "vreplgr2vr.d"
'v', 'c', 'l', 'o', '.', 'b', '\0', // "vclo.b"
'v', 'c', 'l', 'o', '.', 'h', '\0', // "vclo.h"
'v', 'c', 'l', 'o', '.', 'w', '\0', // "vclo.w"
'v', 'c', 'l', 'o', '.', 'd', '\0', // "vclo.d"
'v', 'c', 'l', 'z', '.', 'b', '\0', // "vclz.b"
'v', 'c', 'l', 'z', '.', 'h', '\0', // "vclz.h"
'v', 'c', 'l', 'z', '.', 'w', '\0', // "vclz.w"
'v', 'c', 'l', 'z', '.', 'd', '\0', // "vclz.d"
'v', 'p', 'c', 'n', 't', '.', 'b', '\0', // "vpcnt.b"
'v', 'p', 'c', 'n', 't', '.', 'h', '\0', // "vpcnt.h"
'v', 'p', 'c', 'n', 't', '.', 'w', '\0', // "vpcnt.w"
'v', 'p', 'c', 'n', 't', '.', 'd', '\0', // "vpcnt.d"
'v', 'n', 'e', 'g', '.', 'b', '\0', // "vneg.b"
'v', 'n', 'e', 'g', '.', 'h', '\0', // "vneg.h"
'v', 'n', 'e', 'g', '.', 'w', '\0', // "vneg.w"
'v', 'n', 'e', 'g', '.', 'd', '\0', // "vneg.d"
'v', 'm', 's', 'k', 'l', 't', 'z', '.', 'b', '\0', // "vmskltz.b"
'v', 'm', 's', 'k', 'l', 't', 'z', '.', 'h', '\0', // "vmskltz.h"
'v', 'm', 's', 'k', 'l', 't', 'z', '.', 'w', '\0', // "vmskltz.w"
'v', 'm', 's', 'k', 'l', 't', 'z', '.', 'd', '\0', // "vmskltz.d"
'v', 'm', 's', 'k', 'g', 'e', 'z', '.', 'b', '\0', // "vmskgez.b"
'v', 'm', 's', 'k', 'n', 'z', '.', 'b', '\0', // "vmsknz.b"
'v', 'f', 'l', 'o', 'g', 'b', '.', 's', '\0', // "vflogb.s"
'v', 'f', 'l', 'o', 'g', 'b', '.', 'd', '\0', // "vflogb.d"
'v', 'f', 'c', 'l', 'a', 's', 's', '.', 's', '\0', // "vfclass.s"
'v', 'f', 'c', 'l', 'a', 's', 's', '.', 'd', '\0', // "vfclass.d"
'v', 'f', 's', 'q', 'r', 't', '.', 's', '\0', // "vfsqrt.s"
'v', 'f', 's', 'q', 'r', 't', '.', 'd', '\0', // "vfsqrt.d"
'v', 'f', 'r', 'e', 'c', 'i', 'p', '.', 's', '\0', // "vfrecip.s"
'v', 'f', 'r', 'e', 'c', 'i', 'p', '.', 'd', '\0', // "vfrecip.d"
'v', 'f', 'r', 's', 'q', 'r', 't', '.', 's', '\0', // "vfrsqrt.s"
'v', 'f', 'r', 's', 'q', 'r', 't', '.', 'd', '\0', // "vfrsqrt.d"
'v', 'f', 'r', 'e', 'c', 'i', 'p', 'e', '.', 's', '\0', // "vfrecipe.s"
'v', 'f', 'r', 'e', 'c', 'i', 'p', 'e', '.', 'd', '\0', // "vfrecipe.d"
'v', 'f', 'r', 's', 'q', 'r', 't', 'e', '.', 's', '\0', // "vfrsqrte.s"
'v', 'f', 'r', 's', 'q', 'r', 't', 'e', '.', 'd', '\0', // "vfrsqrte.d"
'v', 'f', 'r', 'i', 'n', 't', '.', 's', '\0', // "vfrint.s"
'v', 'f', 'r', 'i', 'n', 't', '.', 'd', '\0', // "vfrint.d"
'v', 'f', 'r', 'i', 'n', 't', 'r', 'm', '.', 's', '\0', // "vfrintrm.s"
'v', 'f', 'r', 'i', 'n', 't', 'r', 'm', '.', 'd', '\0', // "vfrintrm.d"
'v', 'f', 'r', 'i', 'n', 't', 'r', 'p', '.', 's', '\0', // "vfrintrp.s"
'v', 'f', 'r', 'i', 'n', 't', 'r', 'p', '.', 'd', '\0', // "vfrintrp.d"
'v', 'f', 'r', 'i', 'n', 't', 'r', 'z', '.', 's', '\0', // "vfrintrz.s"
'v', 'f', 'r', 'i', 'n', 't', 'r', 'z', '.', 'd', '\0', // "vfrintrz.d"
'v', 'f', 'r', 'i', 'n', 't', 'r', 'n', 'e', '.', 's', '\0', // "vfrintrne.s"
'v', 'f', 'r', 'i', 'n', 't', 'r', 'n', 'e', '.', 'd', '\0', // "vfrintrne.d"
'v', 'f', 'c', 'v', 't', 'l', '.', 's', '.', 'h', '\0', // "vfcvtl.s.h"
'v', 'f', 'c', 'v', 't', 'h', '.', 's', '.', 'h', '\0', // "vfcvth.s.h"
'v', 'f', 'c', 'v', 't', 'l', '.', 'd', '.', 's', '\0', // "vfcvtl.d.s"
'v', 'f', 'c', 'v', 't', 'h', '.', 'd', '.', 's', '\0', // "vfcvth.d.s"
'v', 'f', 'f', 'i', 'n', 't', '.', 's', '.', 'w', '\0', // "vffint.s.w"
'v', 'f', 'f', 'i', 'n', 't', '.', 's', '.', 'w', 'u', '\0', // "vffint.s.wu"
'v', 'f', 'f', 'i', 'n', 't', '.', 'd', '.', 'l', '\0', // "vffint.d.l"
'v', 'f', 'f', 'i', 'n', 't', '.', 'd', '.', 'l', 'u', '\0', // "vffint.d.lu"
'v', 'f', 'f', 'i', 'n', 't', 'l', '.', 'd', '.', 'w', '\0', // "vffintl.d.w"
'v', 'f', 'f', 'i', 'n', 't', 'h', '.', 'd', '.', 'w', '\0', // "vffinth.d.w"
'v', 'f', 't', 'i', 'n', 't', '.', 'w', '.', 's', '\0', // "vftint.w.s"
'v', 'f', 't', 'i', 'n', 't', '.', 'l', '.', 'd', '\0', // "vftint.l.d"
'v', 'f', 't', 'i', 'n', 't', 'r', 'm', '.', 'w', '.', 's', '\0', // "vftintrm.w.s"
'v', 'f', 't', 'i', 'n', 't', 'r', 'm', '.', 'l', '.', 'd', '\0', // "vftintrm.l.d"
'v', 'f', 't', 'i', 'n', 't', 'r', 'p', '.', 'w', '.', 's', '\0', // "vftintrp.w.s"
'v', 'f', 't', 'i', 'n', 't', 'r', 'p', '.', 'l', '.', 'd', '\0', // "vftintrp.l.d"
'v', 'f', 't', 'i', 'n', 't', 'r', 'z', '.', 'w', '.', 's', '\0', // "vftintrz.w.s"
'v', 'f', 't', 'i', 'n', 't', 'r', 'z', '.', 'l', '.', 'd', '\0', // "vftintrz.l.d"
'v', 'f', 't', 'i', 'n', 't', 'r', 'n', 'e', '.', 'w', '.', 's', '\0', // "vftintrne.w.s"
'v', 'f', 't', 'i', 'n', 't', 'r', 'n', 'e', '.', 'l', '.', 'd', '\0', // "vftintrne.l.d"
'v', 'f', 't', 'i', 'n', 't', '.', 'w', 'u', '.', 's', '\0', // "vftint.wu.s"
'v', 'f', 't', 'i', 'n', 't', '.', 'l', 'u', '.', 'd', '\0', // "vftint.lu.d"
'v', 'f', 't', 'i', 'n', 't', 'r', 'z', '.', 'w', 'u', '.', 's', '\0', // "vftintrz.wu.s"
'v', 'f', 't', 'i', 'n', 't', 'r', 'z', '.', 'l', 'u', '.', 'd', '\0', // "vftintrz.lu.d"
'v', 'f', 't', 'i', 'n', 't', 'l', '.', 'l', '.', 's', '\0', // "vftintl.l.s"
'v', 'f', 't', 'i', 'n', 't', 'h', '.', 'l', '.', 's', '\0', // "vftinth.l.s"
'v', 'f', 't', 'i', 'n', 't', 'r', 'm', 'l', '.', 'l', '.', 's', '\0', // "vftintrml.l.s"
'v', 'f', 't', 'i', 'n', 't', 'r', 'm', 'h', '.', 'l', '.', 's', '\0', // "vftintrmh.l.s"
'v', 'f', 't', 'i', 'n', 't', 'r', 'p', 'l', '.', 'l', '.', 's', '\0', // "vftintrpl.l.s"
'v', 'f', 't', 'i', 'n', 't', 'r', 'p', 'h', '.', 'l', '.', 's', '\0', // "vftintrph.l.s"
'v', 'f', 't', 'i', 'n', 't', 'r', 'z', 'l', '.', 'l', '.', 's', '\0', // "vftintrzl.l.s"
'v', 'f', 't', 'i', 'n', 't', 'r', 'z', 'h', '.', 'l', '.', 's', '\0', // "vftintrzh.l.s"
'v', 'f', 't', 'i', 'n', 't', 'r', 'n', 'e', 'l', '.', 'l', '.', 's', '\0', // "vftintrnel.l.s"
'v', 'f', 't', 'i', 'n', 't', 'r', 'n', 'e', 'h', '.', 'l', '.', 's', '\0', // "vftintrneh.l.s"
'v', 'e', 'x', 't', 'h', '.', 'h', '.', 'b', '\0', // "vexth.h.b"
'v', 'e', 'x', 't', 'h', '.', 'w', '.', 'h', '\0', // "vexth.w.h"
'v', 'e', 'x', 't', 'h', '.', 'd', '.', 'w', '\0', // "vexth.d.w"
'v', 'e', 'x', 't', 'h', '.', 'q', '.', 'd', '\0', // "vexth.q.d"
'v', 'e', 'x', 't', 'h', '.', 'h', 'u', '.', 'b', 'u', '\0', // "vexth.hu.bu"
'v', 'e', 'x', 't', 'h', '.', 'w', 'u', '.', 'h', 'u', '\0', // "vexth.wu.hu"
'v', 'e', 'x', 't', 'h', '.', 'd', 'u', '.', 'w', 'u', '\0', // "vexth.du.wu"
'v', 'e', 'x', 't', 'h', '.', 'q', 'u', '.', 'd', 'u', '\0', // "vexth.qu.du"
'v', 'e', 'x', 't', 'l', '.', 'q', '.', 'd', '\0', // "vextl.q.d"
'v', 'e', 'x', 't', 'l', '.', 'q', 'u', '.', 'd', 'u', '\0', // "vextl.qu.du"
'v', 'l', 'd', 'i', '\0', // "vldi"
'v', 's', 'e', 't', 'e', 'q', 'z', '.', 'v', '\0', // "vseteqz.v"
'v', 's', 'e', 't', 'n', 'e', 'z', '.', 'v', '\0', // "vsetnez.v"
'v', 's', 'e', 't', 'a', 'n', 'y', 'e', 'q', 'z', '.', 'b', '\0', // "vsetanyeqz.b"
'v', 's', 'e', 't', 'a', 'n', 'y', 'e', 'q', 'z', '.', 'h', '\0', // "vsetanyeqz.h"
'v', 's', 'e', 't', 'a', 'n', 'y', 'e', 'q', 'z', '.', 'w', '\0', // "vsetanyeqz.w"
'v', 's', 'e', 't', 'a', 'n', 'y', 'e', 'q', 'z', '.', 'd', '\0', // "vsetanyeqz.d"
'v', 's', 'e', 't', 'a', 'l', 'l', 'n', 'e', 'z', '.', 'b', '\0', // "vsetallnez.b"
'v', 's', 'e', 't', 'a', 'l', 'l', 'n', 'e', 'z', '.', 'h', '\0', // "vsetallnez.h"
'v', 's', 'e', 't', 'a', 'l', 'l', 'n', 'e', 'z', '.', 'w', '\0', // "vsetallnez.w"
'v', 's', 'e', 't', 'a', 'l', 'l', 'n', 'e', 'z', '.', 'd', '\0', // "vsetallnez.d"
'v', 's', 't', 'e', 'l', 'm', '.', 'b', '\0', // "vstelm.b"
'v', 's', 't', 'e', 'l', 'm', '.', 'h', '\0', // "vstelm.h"
'v', 's', 't', 'e', 'l', 'm', '.', 'w', '\0', // "vstelm.w"
'v', 's', 't', 'e', 'l', 'm', '.', 'd', '\0', // "vstelm.d"
'v', 's', 'e', 'q', 'i', '.', 'b', '\0', // "vseqi.b"
'v', 's', 'e', 'q', 'i', '.', 'h', '\0', // "vseqi.h"
'v', 's', 'e', 'q', 'i', '.', 'w', '\0', // "vseqi.w"
'v', 's', 'e', 'q', 'i', '.', 'd', '\0', // "vseqi.d"
'v', 's', 'l', 'e', 'i', '.', 'b', '\0', // "vslei.b"
'v', 's', 'l', 'e', 'i', '.', 'h', '\0', // "vslei.h"
'v', 's', 'l', 'e', 'i', '.', 'w', '\0', // "vslei.w"
'v', 's', 'l', 'e', 'i', '.', 'd', '\0', // "vslei.d"
'v', 's', 'l', 't', 'i', '.', 'b', '\0', // "vslti.b"
'v', 's', 'l', 't', 'i', '.', 'h', '\0', // "vslti.h"
'v', 's', 'l', 't', 'i', '.', 'w', '\0', // "vslti.w"
'v', 's', 'l', 't', 'i', '.', 'd', '\0', // "vslti.d"
'v', 'm', 'a', 'x', 'i', '.', 'b', '\0', // "vmaxi.b"
'v', 'm', 'a', 'x', 'i', '.', 'h', '\0', // "vmaxi.h"
'v', 'm', 'a', 'x', 'i', '.', 'w', '\0', // "vmaxi.w"
'v', 'm', 'a', 'x', 'i', '.', 'd', '\0', // "vmaxi.d"
'v', 'm', 'i', 'n', 'i', '.', 'b', '\0', // "vmini.b"
'v', 'm', 'i', 'n', 'i', '.', 'h', '\0', // "vmini.h"
'v', 'm', 'i', 'n', 'i', '.', 'w', '\0', // "vmini.w"
'v', 'm', 'i', 'n', 'i', '.', 'd', '\0', // "vmini.d"
'v', 'l', 'd', 'r', 'e', 'p', 'l', '.', 'd', '\0', // "vldrepl.d"
'v', 'l', 'd', 'r', 'e', 'p', 'l', '.', 'w', '\0', // "vldrepl.w"
'v', 'l', 'd', 'r', 'e', 'p', 'l', '.', 'h', '\0', // "vldrepl.h"
'v', 'l', 'd', '\0', // "vld"
'v', 'l', 'd', 'r', 'e', 'p', 'l', '.', 'b', '\0', // "vldrepl.b"
'v', 's', 't', '\0', // "vst"
'v', 'i', 'n', 's', 'g', 'r', '2', 'v', 'r', '.', 'd', '\0', // "vinsgr2vr.d"
'v', 'r', 'e', 'p', 'l', 'v', 'e', 'i', '.', 'd', '\0', // "vreplvei.d"
'v', 'p', 'i', 'c', 'k', 'v', 'e', '2', 'g', 'r', '.', 'd', '\0', // "vpickve2gr.d"
'v', 'p', 'i', 'c', 'k', 'v', 'e', '2', 'g', 'r', '.', 'd', 'u', '\0', // "vpickve2gr.du"
'v', 'p', 'i', 'c', 'k', 'v', 'e', '2', 'g', 'r', '.', 'w', '\0', // "vpickve2gr.w"
'v', 'p', 'i', 'c', 'k', 'v', 'e', '2', 'g', 'r', '.', 'w', 'u', '\0', // "vpickve2gr.wu"
'v', 'i', 'n', 's', 'g', 'r', '2', 'v', 'r', '.', 'w', '\0', // "vinsgr2vr.w"
'v', 'r', 'e', 'p', 'l', 'v', 'e', 'i', '.', 'w', '\0', // "vreplvei.w"
'v', 's', 'l', 'l', 'i', '.', 'b', '\0', // "vslli.b"
'v', 's', 'r', 'l', 'i', '.', 'b', '\0', // "vsrli.b"
'v', 's', 'r', 'a', 'i', '.', 'b', '\0', // "vsrai.b"
'v', 's', 'r', 'l', 'r', 'i', '.', 'b', '\0', // "vsrlri.b"
'v', 's', 'r', 'a', 'r', 'i', '.', 'b', '\0', // "vsrari.b"
'v', 'r', 'o', 't', 'r', 'i', '.', 'b', '\0', // "vrotri.b"
'v', 's', 'l', 'l', 'w', 'i', 'l', '.', 'h', '.', 'b', '\0', // "vsllwil.h.b"
'v', 's', 'l', 'l', 'w', 'i', 'l', '.', 'h', 'u', '.', 'b', 'u', '\0', // "vsllwil.hu.bu"
'v', 'b', 'i', 't', 'c', 'l', 'r', 'i', '.', 'b', '\0', // "vbitclri.b"
'v', 'b', 'i', 't', 's', 'e', 't', 'i', '.', 'b', '\0', // "vbitseti.b"
'v', 'b', 'i', 't', 'r', 'e', 'v', 'i', '.', 'b', '\0', // "vbitrevi.b"
'v', 's', 'a', 't', '.', 'b', '\0', // "vsat.b"
'v', 's', 'a', 't', '.', 'b', 'u', '\0', // "vsat.bu"
'v', 'r', 'e', 'p', 'l', 'v', 'e', 'i', '.', 'h', '\0', // "vreplvei.h"
'v', 'i', 'n', 's', 'g', 'r', '2', 'v', 'r', '.', 'h', '\0', // "vinsgr2vr.h"
'v', 'p', 'i', 'c', 'k', 'v', 'e', '2', 'g', 'r', '.', 'h', '\0', // "vpickve2gr.h"
'v', 'p', 'i', 'c', 'k', 'v', 'e', '2', 'g', 'r', '.', 'h', 'u', '\0', // "vpickve2gr.hu"
'v', 'p', 'i', 'c', 'k', 'v', 'e', '2', 'g', 'r', '.', 'b', '\0', // "vpickve2gr.b"
'v', 'p', 'i', 'c', 'k', 'v', 'e', '2', 'g', 'r', '.', 'b', 'u', '\0', // "vpickve2gr.bu"
'v', 's', 'l', 'l', 'i', '.', 'h', '\0', // "vslli.h"
'v', 's', 'r', 'l', 'i', '.', 'h', '\0', // "vsrli.h"
'v', 's', 'r', 'a', 'i', '.', 'h', '\0', // "vsrai.h"
'v', 's', 'r', 'l', 'r', 'i', '.', 'h', '\0', // "vsrlri.h"
'v', 's', 'r', 'a', 'r', 'i', '.', 'h', '\0', // "vsrari.h"
'v', 'r', 'o', 't', 'r', 'i', '.', 'h', '\0', // "vrotri.h"
'v', 's', 'l', 'l', 'w', 'i', 'l', '.', 'w', '.', 'h', '\0', // "vsllwil.w.h"
'v', 's', 'l', 'l', 'w', 'i', 'l', '.', 'w', 'u', '.', 'h', 'u', '\0', // "vsllwil.wu.hu"
'v', 's', 'r', 'l', 'n', 'i', '.', 'b', '.', 'h', '\0', // "vsrlni.b.h"
'v', 's', 'r', 'l', 'r', 'n', 'i', '.', 'b', '.', 'h', '\0', // "vsrlrni.b.h"
'v', 's', 's', 'r', 'l', 'n', 'i', '.', 'b', '.', 'h', '\0', // "vssrlni.b.h"
'v', 's', 's', 'r', 'l', 'n', 'i', '.', 'b', 'u', '.', 'h', '\0', // "vssrlni.bu.h"
'v', 's', 's', 'r', 'l', 'r', 'n', 'i', '.', 'b', '.', 'h', '\0', // "vssrlrni.b.h"
'v', 's', 's', 'r', 'l', 'r', 'n', 'i', '.', 'b', 'u', '.', 'h', '\0', // "vssrlrni.bu.h"
'v', 's', 'r', 'a', 'n', 'i', '.', 'b', '.', 'h', '\0', // "vsrani.b.h"
'v', 's', 'r', 'a', 'r', 'n', 'i', '.', 'b', '.', 'h', '\0', // "vsrarni.b.h"
'v', 's', 's', 'r', 'a', 'n', 'i', '.', 'b', '.', 'h', '\0', // "vssrani.b.h"
'v', 's', 's', 'r', 'a', 'n', 'i', '.', 'b', 'u', '.', 'h', '\0', // "vssrani.bu.h"
'v', 's', 's', 'r', 'a', 'r', 'n', 'i', '.', 'b', '.', 'h', '\0', // "vssrarni.b.h"
'v', 's', 's', 'r', 'a', 'r', 'n', 'i', '.', 'b', 'u', '.', 'h', '\0', // "vssrarni.bu.h"
'v', 'b', 'i', 't', 'c', 'l', 'r', 'i', '.', 'h', '\0', // "vbitclri.h"
'v', 'b', 'i', 't', 's', 'e', 't', 'i', '.', 'h', '\0', // "vbitseti.h"
'v', 'b', 'i', 't', 'r', 'e', 'v', 'i', '.', 'h', '\0', // "vbitrevi.h"
'v', 's', 'a', 't', '.', 'h', '\0', // "vsat.h"
'v', 's', 'a', 't', '.', 'h', 'u', '\0', // "vsat.hu"
'v', 'i', 'n', 's', 'g', 'r', '2', 'v', 'r', '.', 'b', '\0', // "vinsgr2vr.b"
'v', 'r', 'e', 'p', 'l', 'v', 'e', 'i', '.', 'b', '\0', // "vreplvei.b"
'v', 's', 'l', 'e', 'i', '.', 'b', 'u', '\0', // "vslei.bu"
'v', 's', 'l', 'e', 'i', '.', 'h', 'u', '\0', // "vslei.hu"
'v', 's', 'l', 'e', 'i', '.', 'w', 'u', '\0', // "vslei.wu"
'v', 's', 'l', 'e', 'i', '.', 'd', 'u', '\0', // "vslei.du"
'v', 's', 'l', 't', 'i', '.', 'b', 'u', '\0', // "vslti.bu"
'v', 's', 'l', 't', 'i', '.', 'h', 'u', '\0', // "vslti.hu"
'v', 's', 'l', 't', 'i', '.', 'w', 'u', '\0', // "vslti.wu"
'v', 's', 'l', 't', 'i', '.', 'd', 'u', '\0', // "vslti.du"
'v', 'a', 'd', 'd', 'i', '.', 'b', 'u', '\0', // "vaddi.bu"
'v', 'a', 'd', 'd', 'i', '.', 'h', 'u', '\0', // "vaddi.hu"
'v', 'a', 'd', 'd', 'i', '.', 'w', 'u', '\0', // "vaddi.wu"
'v', 'a', 'd', 'd', 'i', '.', 'd', 'u', '\0', // "vaddi.du"
'v', 's', 'u', 'b', 'i', '.', 'b', 'u', '\0', // "vsubi.bu"
'v', 's', 'u', 'b', 'i', '.', 'h', 'u', '\0', // "vsubi.hu"
'v', 's', 'u', 'b', 'i', '.', 'w', 'u', '\0', // "vsubi.wu"
'v', 's', 'u', 'b', 'i', '.', 'd', 'u', '\0', // "vsubi.du"
'v', 's', 'l', 'l', 'i', '.', 'w', '\0', // "vslli.w"
'v', 's', 'r', 'l', 'i', '.', 'w', '\0', // "vsrli.w"
'v', 's', 'r', 'a', 'i', '.', 'w', '\0', // "vsrai.w"
'v', 'b', 's', 'l', 'l', '.', 'v', '\0', // "vbsll.v"
'v', 'b', 's', 'r', 'l', '.', 'v', '\0', // "vbsrl.v"
'v', 's', 'r', 'l', 'r', 'i', '.', 'w', '\0', // "vsrlri.w"
'v', 's', 'r', 'a', 'r', 'i', '.', 'w', '\0', // "vsrari.w"
'v', 'r', 'o', 't', 'r', 'i', '.', 'w', '\0', // "vrotri.w"
'v', 's', 'l', 'l', 'w', 'i', 'l', '.', 'd', '.', 'w', '\0', // "vsllwil.d.w"
'v', 's', 'l', 'l', 'w', 'i', 'l', '.', 'd', 'u', '.', 'w', 'u', '\0', // "vsllwil.du.wu"
'v', 's', 'r', 'l', 'n', 'i', '.', 'h', '.', 'w', '\0', // "vsrlni.h.w"
'v', 's', 'r', 'l', 'r', 'n', 'i', '.', 'h', '.', 'w', '\0', // "vsrlrni.h.w"
'v', 's', 's', 'r', 'l', 'n', 'i', '.', 'h', '.', 'w', '\0', // "vssrlni.h.w"
'v', 's', 's', 'r', 'l', 'n', 'i', '.', 'h', 'u', '.', 'w', '\0', // "vssrlni.hu.w"
'v', 's', 's', 'r', 'l', 'r', 'n', 'i', '.', 'h', '.', 'w', '\0', // "vssrlrni.h.w"
'v', 's', 's', 'r', 'l', 'r', 'n', 'i', '.', 'h', 'u', '.', 'w', '\0', // "vssrlrni.hu.w"
'v', 's', 'r', 'a', 'n', 'i', '.', 'h', '.', 'w', '\0', // "vsrani.h.w"
'v', 's', 'r', 'a', 'r', 'n', 'i', '.', 'h', '.', 'w', '\0', // "vsrarni.h.w"
'v', 's', 's', 'r', 'a', 'n', 'i', '.', 'h', '.', 'w', '\0', // "vssrani.h.w"
'v', 's', 's', 'r', 'a', 'n', 'i', '.', 'h', 'u', '.', 'w', '\0', // "vssrani.hu.w"
'v', 's', 's', 'r', 'a', 'r', 'n', 'i', '.', 'h', '.', 'w', '\0', // "vssrarni.h.w"
'v', 's', 's', 'r', 'a', 'r', 'n', 'i', '.', 'h', 'u', '.', 'w', '\0', // "vssrarni.hu.w"
'v', 'b', 'i', 't', 'c', 'l', 'r', 'i', '.', 'w', '\0', // "vbitclri.w"
'v', 'b', 'i', 't', 's', 'e', 't', 'i', '.', 'w', '\0', // "vbitseti.w"
'v', 'b', 'i', 't', 'r', 'e', 'v', 'i', '.', 'w', '\0', // "vbitrevi.w"
'v', 'm', 'a', 'x', 'i', '.', 'b', 'u', '\0', // "vmaxi.bu"
'v', 'm', 'a', 'x', 'i', '.', 'h', 'u', '\0', // "vmaxi.hu"
'v', 'm', 'a', 'x', 'i', '.', 'w', 'u', '\0', // "vmaxi.wu"
'v', 'm', 'a', 'x', 'i', '.', 'd', 'u', '\0', // "vmaxi.du"
'v', 'm', 'i', 'n', 'i', '.', 'b', 'u', '\0', // "vmini.bu"
'v', 'm', 'i', 'n', 'i', '.', 'h', 'u', '\0', // "vmini.hu"
'v', 'm', 'i', 'n', 'i', '.', 'w', 'u', '\0', // "vmini.wu"
'v', 'm', 'i', 'n', 'i', '.', 'd', 'u', '\0', // "vmini.du"
'v', 'f', 'r', 's', 't', 'p', 'i', '.', 'b', '\0', // "vfrstpi.b"
'v', 'f', 'r', 's', 't', 'p', 'i', '.', 'h', '\0', // "vfrstpi.h"
'v', 's', 'a', 't', '.', 'w', '\0', // "vsat.w"
'v', 's', 'a', 't', '.', 'w', 'u', '\0', // "vsat.wu"
'v', 's', 'l', 'l', 'i', '.', 'd', '\0', // "vslli.d"
'v', 's', 'r', 'l', 'i', '.', 'd', '\0', // "vsrli.d"
'v', 's', 'r', 'a', 'i', '.', 'd', '\0', // "vsrai.d"
'v', 'r', 'o', 't', 'r', 'i', '.', 'd', '\0', // "vrotri.d"
'v', 's', 'r', 'l', 'r', 'i', '.', 'd', '\0', // "vsrlri.d"
'v', 's', 'r', 'a', 'r', 'i', '.', 'd', '\0', // "vsrari.d"
'v', 's', 'r', 'l', 'n', 'i', '.', 'w', '.', 'd', '\0', // "vsrlni.w.d"
'v', 's', 'r', 'l', 'r', 'n', 'i', '.', 'w', '.', 'd', '\0', // "vsrlrni.w.d"
'v', 's', 's', 'r', 'l', 'n', 'i', '.', 'w', '.', 'd', '\0', // "vssrlni.w.d"
'v', 's', 's', 'r', 'l', 'n', 'i', '.', 'w', 'u', '.', 'd', '\0', // "vssrlni.wu.d"
'v', 's', 's', 'r', 'l', 'r', 'n', 'i', '.', 'w', '.', 'd', '\0', // "vssrlrni.w.d"
'v', 's', 's', 'r', 'l', 'r', 'n', 'i', '.', 'w', 'u', '.', 'd', '\0', // "vssrlrni.wu.d"
'v', 's', 'r', 'a', 'n', 'i', '.', 'w', '.', 'd', '\0', // "vsrani.w.d"
'v', 's', 'r', 'a', 'r', 'n', 'i', '.', 'w', '.', 'd', '\0', // "vsrarni.w.d"
'v', 's', 's', 'r', 'a', 'n', 'i', '.', 'w', '.', 'd', '\0', // "vssrani.w.d"
'v', 's', 's', 'r', 'a', 'n', 'i', '.', 'w', 'u', '.', 'd', '\0', // "vssrani.wu.d"
'v', 's', 's', 'r', 'a', 'r', 'n', 'i', '.', 'w', '.', 'd', '\0', // "vssrarni.w.d"
'v', 's', 's', 'r', 'a', 'r', 'n', 'i', '.', 'w', 'u', '.', 'd', '\0', // "vssrarni.wu.d"
'v', 'b', 'i', 't', 'c', 'l', 'r', 'i', '.', 'd', '\0', // "vbitclri.d"
'v', 'b', 'i', 't', 's', 'e', 't', 'i', '.', 'd', '\0', // "vbitseti.d"
'v', 'b', 'i', 't', 'r', 'e', 'v', 'i', '.', 'd', '\0', // "vbitrevi.d"
'v', 's', 'a', 't', '.', 'd', '\0', // "vsat.d"
'v', 's', 'a', 't', '.', 'd', 'u', '\0', // "vsat.du"
'v', 's', 'r', 'l', 'n', 'i', '.', 'd', '.', 'q', '\0', // "vsrlni.d.q"
'v', 's', 'r', 'l', 'r', 'n', 'i', '.', 'd', '.', 'q', '\0', // "vsrlrni.d.q"
'v', 's', 's', 'r', 'l', 'n', 'i', '.', 'd', '.', 'q', '\0', // "vssrlni.d.q"
'v', 's', 's', 'r', 'l', 'n', 'i', '.', 'd', 'u', '.', 'q', '\0', // "vssrlni.du.q"
'v', 's', 's', 'r', 'l', 'r', 'n', 'i', '.', 'd', '.', 'q', '\0', // "vssrlrni.d.q"
'v', 's', 's', 'r', 'l', 'r', 'n', 'i', '.', 'd', 'u', '.', 'q', '\0', // "vssrlrni.du.q"
'v', 's', 'r', 'a', 'n', 'i', '.', 'd', '.', 'q', '\0', // "vsrani.d.q"
'v', 's', 'r', 'a', 'r', 'n', 'i', '.', 'd', '.', 'q', '\0', // "vsrarni.d.q"
'v', 's', 's', 'r', 'a', 'n', 'i', '.', 'd', '.', 'q', '\0', // "vssrani.d.q"
'v', 's', 's', 'r', 'a', 'n', 'i', '.', 'd', 'u', '.', 'q', '\0', // "vssrani.du.q"
'v', 's', 's', 'r', 'a', 'r', 'n', 'i', '.', 'd', '.', 'q', '\0', // "vssrarni.d.q"
'v', 's', 's', 'r', 'a', 'r', 'n', 'i', '.', 'd', 'u', '.', 'q', '\0', // "vssrarni.du.q"
'v', 'e', 'x', 't', 'r', 'i', 'n', 's', '.', 'd', '\0', // "vextrins.d"
'v', 'e', 'x', 't', 'r', 'i', 'n', 's', '.', 'w', '\0', // "vextrins.w"
'v', 'e', 'x', 't', 'r', 'i', 'n', 's', '.', 'h', '\0', // "vextrins.h"
'v', 'e', 'x', 't', 'r', 'i', 'n', 's', '.', 'b', '\0', // "vextrins.b"
'v', 's', 'h', 'u', 'f', '4', 'i', '.', 'b', '\0', // "vshuf4i.b"
'v', 's', 'h', 'u', 'f', '4', 'i', '.', 'h', '\0', // "vshuf4i.h"
'v', 's', 'h', 'u', 'f', '4', 'i', '.', 'w', '\0', // "vshuf4i.w"
'v', 's', 'h', 'u', 'f', '4', 'i', '.', 'd', '\0', // "vshuf4i.d"
'v', 'b', 'i', 't', 's', 'e', 'l', 'i', '.', 'b', '\0', // "vbitseli.b"
'v', 'a', 'n', 'd', 'i', '.', 'b', '\0', // "vandi.b"
'v', 'o', 'r', 'i', '.', 'b', '\0', // "vori.b"
'v', 'x', 'o', 'r', 'i', '.', 'b', '\0', // "vxori.b"
'v', 'n', 'o', 'r', 'i', '.', 'b', '\0', // "vnori.b"
'v', 'p', 'e', 'r', 'm', 'i', '.', 'w', '\0', // "vpermi.w"
'x', 'v', 'f', 'm', 'a', 'd', 'd', '.', 's', '\0', // "xvfmadd.s"
'x', 'v', 'f', 'm', 'a', 'd', 'd', '.', 'd', '\0', // "xvfmadd.d"
'x', 'v', 'f', 'm', 's', 'u', 'b', '.', 's', '\0', // "xvfmsub.s"
'x', 'v', 'f', 'm', 's', 'u', 'b', '.', 'd', '\0', // "xvfmsub.d"
'x', 'v', 'f', 'n', 'm', 'a', 'd', 'd', '.', 's', '\0', // "xvfnmadd.s"
'x', 'v', 'f', 'n', 'm', 'a', 'd', 'd', '.', 'd', '\0', // "xvfnmadd.d"
'x', 'v', 'f', 'n', 'm', 's', 'u', 'b', '.', 's', '\0', // "xvfnmsub.s"
'x', 'v', 'f', 'n', 'm', 's', 'u', 'b', '.', 'd', '\0', // "xvfnmsub.d"
'x', 'v', 'b', 'i', 't', 's', 'e', 'l', '.', 'v', '\0', // "xvbitsel.v"
'x', 'v', 's', 'h', 'u', 'f', '.', 'b', '\0', // "xvshuf.b"
'x', 'v', 'l', 'd', 'x', '\0', // "xvldx"
'x', 'v', 's', 't', 'x', '\0', // "xvstx"
'x', 'v', 'r', 'e', 'p', 'l', 'v', 'e', '.', 'b', '\0', // "xvreplve.b"
'x', 'v', 'r', 'e', 'p', 'l', 'v', 'e', '.', 'h', '\0', // "xvreplve.h"
'x', 'v', 'r', 'e', 'p', 'l', 'v', 'e', '.', 'w', '\0', // "xvreplve.w"
'x', 'v', 'r', 'e', 'p', 'l', 'v', 'e', '.', 'd', '\0', // "xvreplve.d"
'x', 'v', 'f', 'c', 'm', 'p', '.', 'c', 'a', 'f', '.', 's', '\0', // "xvfcmp.caf.s"
'x', 'v', 'f', 'c', 'm', 'p', '.', 'c', 'u', 'n', '.', 's', '\0', // "xvfcmp.cun.s"
'x', 'v', 'f', 'c', 'm', 'p', '.', 'c', 'e', 'q', '.', 's', '\0', // "xvfcmp.ceq.s"
'x', 'v', 'f', 'c', 'm', 'p', '.', 'c', 'u', 'e', 'q', '.', 's', '\0', // "xvfcmp.cueq.s"
'x', 'v', 'f', 'c', 'm', 'p', '.', 'c', 'l', 't', '.', 's', '\0', // "xvfcmp.clt.s"
'x', 'v', 'f', 'c', 'm', 'p', '.', 'c', 'u', 'l', 't', '.', 's', '\0', // "xvfcmp.cult.s"
'x', 'v', 'f', 'c', 'm', 'p', '.', 'c', 'l', 'e', '.', 's', '\0', // "xvfcmp.cle.s"
'x', 'v', 'f', 'c', 'm', 'p', '.', 'c', 'u', 'l', 'e', '.', 's', '\0', // "xvfcmp.cule.s"
'x', 'v', 'f', 'c', 'm', 'p', '.', 'c', 'n', 'e', '.', 's', '\0', // "xvfcmp.cne.s"
'x', 'v', 'f', 'c', 'm', 'p', '.', 'c', 'o', 'r', '.', 's', '\0', // "xvfcmp.cor.s"
'x', 'v', 'f', 'c', 'm', 'p', '.', 'c', 'u', 'n', 'e', '.', 's', '\0', // "xvfcmp.cune.s"
'x', 'v', 'f', 'c', 'm', 'p', '.', 's', 'a', 'f', '.', 's', '\0', // "xvfcmp.saf.s"
'x', 'v', 'f', 'c', 'm', 'p', '.', 's', 'u', 'n', '.', 's', '\0', // "xvfcmp.sun.s"
'x', 'v', 'f', 'c', 'm', 'p', '.', 's', 'e', 'q', '.', 's', '\0', // "xvfcmp.seq.s"
'x', 'v', 'f', 'c', 'm', 'p', '.', 's', 'u', 'e', 'q', '.', 's', '\0', // "xvfcmp.sueq.s"
'x', 'v', 'f', 'c', 'm', 'p', '.', 's', 'l', 't', '.', 's', '\0', // "xvfcmp.slt.s"
'x', 'v', 'f', 'c', 'm', 'p', '.', 's', 'u', 'l', 't', '.', 's', '\0', // "xvfcmp.sult.s"
'x', 'v', 'f', 'c', 'm', 'p', '.', 's', 'l', 'e', '.', 's', '\0', // "xvfcmp.sle.s"
'x', 'v', 'f', 'c', 'm', 'p', '.', 's', 'u', 'l', 'e', '.', 's', '\0', // "xvfcmp.sule.s"
'x', 'v', 'f', 'c', 'm', 'p', '.', 's', 'n', 'e', '.', 's', '\0', // "xvfcmp.sne.s"
'x', 'v', 'f', 'c', 'm', 'p', '.', 's', 'o', 'r', '.', 's', '\0', // "xvfcmp.sor.s"
'x', 'v', 'f', 'c', 'm', 'p', '.', 's', 'u', 'n', 'e', '.', 's', '\0', // "xvfcmp.sune.s"
'x', 'v', 'f', 'c', 'm', 'p', '.', 'c', 'a', 'f', '.', 'd', '\0', // "xvfcmp.caf.d"
'x', 'v', 'f', 'c', 'm', 'p', '.', 'c', 'u', 'n', '.', 'd', '\0', // "xvfcmp.cun.d"
'x', 'v', 'f', 'c', 'm', 'p', '.', 'c', 'e', 'q', '.', 'd', '\0', // "xvfcmp.ceq.d"
'x', 'v', 'f', 'c', 'm', 'p', '.', 'c', 'u', 'e', 'q', '.', 'd', '\0', // "xvfcmp.cueq.d"
'x', 'v', 'f', 'c', 'm', 'p', '.', 'c', 'l', 't', '.', 'd', '\0', // "xvfcmp.clt.d"
'x', 'v', 'f', 'c', 'm', 'p', '.', 'c', 'u', 'l', 't', '.', 'd', '\0', // "xvfcmp.cult.d"
'x', 'v', 'f', 'c', 'm', 'p', '.', 'c', 'l', 'e', '.', 'd', '\0', // "xvfcmp.cle.d"
'x', 'v', 'f', 'c', 'm', 'p', '.', 'c', 'u', 'l', 'e', '.', 'd', '\0', // "xvfcmp.cule.d"
'x', 'v', 'f', 'c', 'm', 'p', '.', 'c', 'n', 'e', '.', 'd', '\0', // "xvfcmp.cne.d"
'x', 'v', 'f', 'c', 'm', 'p', '.', 'c', 'o', 'r', '.', 'd', '\0', // "xvfcmp.cor.d"
'x', 'v', 'f', 'c', 'm', 'p', '.', 'c', 'u', 'n', 'e', '.', 'd', '\0', // "xvfcmp.cune.d"
'x', 'v', 'f', 'c', 'm', 'p', '.', 's', 'a', 'f', '.', 'd', '\0', // "xvfcmp.saf.d"
'x', 'v', 'f', 'c', 'm', 'p', '.', 's', 'u', 'n', '.', 'd', '\0', // "xvfcmp.sun.d"
'x', 'v', 'f', 'c', 'm', 'p', '.', 's', 'e', 'q', '.', 'd', '\0', // "xvfcmp.seq.d"
'x', 'v', 'f', 'c', 'm', 'p', '.', 's', 'u', 'e', 'q', '.', 'd', '\0', // "xvfcmp.sueq.d"
'x', 'v', 'f', 'c', 'm', 'p', '.', 's', 'l', 't', '.', 'd', '\0', // "xvfcmp.slt.d"
'x', 'v', 'f', 'c', 'm', 'p', '.', 's', 'u', 'l', 't', '.', 'd', '\0', // "xvfcmp.sult.d"
'x', 'v', 'f', 'c', 'm', 'p', '.', 's', 'l', 'e', '.', 'd', '\0', // "xvfcmp.sle.d"
'x', 'v', 'f', 'c', 'm', 'p', '.', 's', 'u', 'l', 'e', '.', 'd', '\0', // "xvfcmp.sule.d"
'x', 'v', 'f', 'c', 'm', 'p', '.', 's', 'n', 'e', '.', 'd', '\0', // "xvfcmp.sne.d"
'x', 'v', 'f', 'c', 'm', 'p', '.', 's', 'o', 'r', '.', 'd', '\0', // "xvfcmp.sor.d"
'x', 'v', 'f', 'c', 'm', 'p', '.', 's', 'u', 'n', 'e', '.', 'd', '\0', // "xvfcmp.sune.d"
'x', 'v', 's', 'e', 'q', '.', 'b', '\0', // "xvseq.b"
'x', 'v', 's', 'e', 'q', '.', 'h', '\0', // "xvseq.h"
'x', 'v', 's', 'e', 'q', '.', 'w', '\0', // "xvseq.w"
'x', 'v', 's', 'e', 'q', '.', 'd', '\0', // "xvseq.d"
'x', 'v', 's', 'l', 'e', '.', 'b', '\0', // "xvsle.b"
'x', 'v', 's', 'l', 'e', '.', 'h', '\0', // "xvsle.h"
'x', 'v', 's', 'l', 'e', '.', 'w', '\0', // "xvsle.w"
'x', 'v', 's', 'l', 'e', '.', 'd', '\0', // "xvsle.d"
'x', 'v', 's', 'l', 'e', '.', 'b', 'u', '\0', // "xvsle.bu"
'x', 'v', 's', 'l', 'e', '.', 'h', 'u', '\0', // "xvsle.hu"
'x', 'v', 's', 'l', 'e', '.', 'w', 'u', '\0', // "xvsle.wu"
'x', 'v', 's', 'l', 'e', '.', 'd', 'u', '\0', // "xvsle.du"
'x', 'v', 's', 'l', 't', '.', 'b', '\0', // "xvslt.b"
'x', 'v', 's', 'l', 't', '.', 'h', '\0', // "xvslt.h"
'x', 'v', 's', 'l', 't', '.', 'w', '\0', // "xvslt.w"
'x', 'v', 's', 'l', 't', '.', 'd', '\0', // "xvslt.d"
'x', 'v', 's', 'l', 't', '.', 'b', 'u', '\0', // "xvslt.bu"
'x', 'v', 's', 'l', 't', '.', 'h', 'u', '\0', // "xvslt.hu"
'x', 'v', 's', 'l', 't', '.', 'w', 'u', '\0', // "xvslt.wu"
'x', 'v', 's', 'l', 't', '.', 'd', 'u', '\0', // "xvslt.du"
'x', 'v', 'a', 'd', 'd', '.', 'b', '\0', // "xvadd.b"
'x', 'v', 'a', 'd', 'd', '.', 'h', '\0', // "xvadd.h"
'x', 'v', 'a', 'd', 'd', '.', 'w', '\0', // "xvadd.w"
'x', 'v', 'a', 'd', 'd', '.', 'd', '\0', // "xvadd.d"
'x', 'v', 's', 'u', 'b', '.', 'b', '\0', // "xvsub.b"
'x', 'v', 's', 'u', 'b', '.', 'h', '\0', // "xvsub.h"
'x', 'v', 's', 'u', 'b', '.', 'w', '\0', // "xvsub.w"
'x', 'v', 's', 'u', 'b', '.', 'd', '\0', // "xvsub.d"
'x', 'v', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'h', '.', 'b', '\0', // "xvaddwev.h.b"
'x', 'v', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'w', '.', 'h', '\0', // "xvaddwev.w.h"
'x', 'v', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'd', '.', 'w', '\0', // "xvaddwev.d.w"
'x', 'v', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'q', '.', 'd', '\0', // "xvaddwev.q.d"
'x', 'v', 's', 'u', 'b', 'w', 'e', 'v', '.', 'h', '.', 'b', '\0', // "xvsubwev.h.b"
'x', 'v', 's', 'u', 'b', 'w', 'e', 'v', '.', 'w', '.', 'h', '\0', // "xvsubwev.w.h"
'x', 'v', 's', 'u', 'b', 'w', 'e', 'v', '.', 'd', '.', 'w', '\0', // "xvsubwev.d.w"
'x', 'v', 's', 'u', 'b', 'w', 'e', 'v', '.', 'q', '.', 'd', '\0', // "xvsubwev.q.d"
'x', 'v', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'h', '.', 'b', '\0', // "xvaddwod.h.b"
'x', 'v', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'w', '.', 'h', '\0', // "xvaddwod.w.h"
'x', 'v', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'd', '.', 'w', '\0', // "xvaddwod.d.w"
'x', 'v', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'q', '.', 'd', '\0', // "xvaddwod.q.d"
'x', 'v', 's', 'u', 'b', 'w', 'o', 'd', '.', 'h', '.', 'b', '\0', // "xvsubwod.h.b"
'x', 'v', 's', 'u', 'b', 'w', 'o', 'd', '.', 'w', '.', 'h', '\0', // "xvsubwod.w.h"
'x', 'v', 's', 'u', 'b', 'w', 'o', 'd', '.', 'd', '.', 'w', '\0', // "xvsubwod.d.w"
'x', 'v', 's', 'u', 'b', 'w', 'o', 'd', '.', 'q', '.', 'd', '\0', // "xvsubwod.q.d"
'x', 'v', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'h', '.', 'b', 'u', '\0', // "xvaddwev.h.bu"
'x', 'v', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'w', '.', 'h', 'u', '\0', // "xvaddwev.w.hu"
'x', 'v', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'd', '.', 'w', 'u', '\0', // "xvaddwev.d.wu"
'x', 'v', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'q', '.', 'd', 'u', '\0', // "xvaddwev.q.du"
'x', 'v', 's', 'u', 'b', 'w', 'e', 'v', '.', 'h', '.', 'b', 'u', '\0', // "xvsubwev.h.bu"
'x', 'v', 's', 'u', 'b', 'w', 'e', 'v', '.', 'w', '.', 'h', 'u', '\0', // "xvsubwev.w.hu"
'x', 'v', 's', 'u', 'b', 'w', 'e', 'v', '.', 'd', '.', 'w', 'u', '\0', // "xvsubwev.d.wu"
'x', 'v', 's', 'u', 'b', 'w', 'e', 'v', '.', 'q', '.', 'd', 'u', '\0', // "xvsubwev.q.du"
'x', 'v', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'h', '.', 'b', 'u', '\0', // "xvaddwod.h.bu"
'x', 'v', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'w', '.', 'h', 'u', '\0', // "xvaddwod.w.hu"
'x', 'v', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'd', '.', 'w', 'u', '\0', // "xvaddwod.d.wu"
'x', 'v', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'q', '.', 'd', 'u', '\0', // "xvaddwod.q.du"
'x', 'v', 's', 'u', 'b', 'w', 'o', 'd', '.', 'h', '.', 'b', 'u', '\0', // "xvsubwod.h.bu"
'x', 'v', 's', 'u', 'b', 'w', 'o', 'd', '.', 'w', '.', 'h', 'u', '\0', // "xvsubwod.w.hu"
'x', 'v', 's', 'u', 'b', 'w', 'o', 'd', '.', 'd', '.', 'w', 'u', '\0', // "xvsubwod.d.wu"
'x', 'v', 's', 'u', 'b', 'w', 'o', 'd', '.', 'q', '.', 'd', 'u', '\0', // "xvsubwod.q.du"
'x', 'v', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'h', '.', 'b', 'u', '.', 'b', '\0', // "xvaddwev.h.bu.b"
'x', 'v', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'w', '.', 'h', 'u', '.', 'h', '\0', // "xvaddwev.w.hu.h"
'x', 'v', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'd', '.', 'w', 'u', '.', 'w', '\0', // "xvaddwev.d.wu.w"
'x', 'v', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'q', '.', 'd', 'u', '.', 'd', '\0', // "xvaddwev.q.du.d"
'x', 'v', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'h', '.', 'b', 'u', '.', 'b', '\0', // "xvaddwod.h.bu.b"
'x', 'v', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'w', '.', 'h', 'u', '.', 'h', '\0', // "xvaddwod.w.hu.h"
'x', 'v', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'd', '.', 'w', 'u', '.', 'w', '\0', // "xvaddwod.d.wu.w"
'x', 'v', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'q', '.', 'd', 'u', '.', 'd', '\0', // "xvaddwod.q.du.d"
'x', 'v', 's', 'a', 'd', 'd', '.', 'b', '\0', // "xvsadd.b"
'x', 'v', 's', 'a', 'd', 'd', '.', 'h', '\0', // "xvsadd.h"
'x', 'v', 's', 'a', 'd', 'd', '.', 'w', '\0', // "xvsadd.w"
'x', 'v', 's', 'a', 'd', 'd', '.', 'd', '\0', // "xvsadd.d"
'x', 'v', 's', 's', 'u', 'b', '.', 'b', '\0', // "xvssub.b"
'x', 'v', 's', 's', 'u', 'b', '.', 'h', '\0', // "xvssub.h"
'x', 'v', 's', 's', 'u', 'b', '.', 'w', '\0', // "xvssub.w"
'x', 'v', 's', 's', 'u', 'b', '.', 'd', '\0', // "xvssub.d"
'x', 'v', 's', 'a', 'd', 'd', '.', 'b', 'u', '\0', // "xvsadd.bu"
'x', 'v', 's', 'a', 'd', 'd', '.', 'h', 'u', '\0', // "xvsadd.hu"
'x', 'v', 's', 'a', 'd', 'd', '.', 'w', 'u', '\0', // "xvsadd.wu"
'x', 'v', 's', 'a', 'd', 'd', '.', 'd', 'u', '\0', // "xvsadd.du"
'x', 'v', 's', 's', 'u', 'b', '.', 'b', 'u', '\0', // "xvssub.bu"
'x', 'v', 's', 's', 'u', 'b', '.', 'h', 'u', '\0', // "xvssub.hu"
'x', 'v', 's', 's', 'u', 'b', '.', 'w', 'u', '\0', // "xvssub.wu"
'x', 'v', 's', 's', 'u', 'b', '.', 'd', 'u', '\0', // "xvssub.du"
'x', 'v', 'h', 'a', 'd', 'd', 'w', '.', 'h', '.', 'b', '\0', // "xvhaddw.h.b"
'x', 'v', 'h', 'a', 'd', 'd', 'w', '.', 'w', '.', 'h', '\0', // "xvhaddw.w.h"
'x', 'v', 'h', 'a', 'd', 'd', 'w', '.', 'd', '.', 'w', '\0', // "xvhaddw.d.w"
'x', 'v', 'h', 'a', 'd', 'd', 'w', '.', 'q', '.', 'd', '\0', // "xvhaddw.q.d"
'x', 'v', 'h', 's', 'u', 'b', 'w', '.', 'h', '.', 'b', '\0', // "xvhsubw.h.b"
'x', 'v', 'h', 's', 'u', 'b', 'w', '.', 'w', '.', 'h', '\0', // "xvhsubw.w.h"
'x', 'v', 'h', 's', 'u', 'b', 'w', '.', 'd', '.', 'w', '\0', // "xvhsubw.d.w"
'x', 'v', 'h', 's', 'u', 'b', 'w', '.', 'q', '.', 'd', '\0', // "xvhsubw.q.d"
'x', 'v', 'h', 'a', 'd', 'd', 'w', '.', 'h', 'u', '.', 'b', 'u', '\0', // "xvhaddw.hu.bu"
'x', 'v', 'h', 'a', 'd', 'd', 'w', '.', 'w', 'u', '.', 'h', 'u', '\0', // "xvhaddw.wu.hu"
'x', 'v', 'h', 'a', 'd', 'd', 'w', '.', 'd', 'u', '.', 'w', 'u', '\0', // "xvhaddw.du.wu"
'x', 'v', 'h', 'a', 'd', 'd', 'w', '.', 'q', 'u', '.', 'd', 'u', '\0', // "xvhaddw.qu.du"
'x', 'v', 'h', 's', 'u', 'b', 'w', '.', 'h', 'u', '.', 'b', 'u', '\0', // "xvhsubw.hu.bu"
'x', 'v', 'h', 's', 'u', 'b', 'w', '.', 'w', 'u', '.', 'h', 'u', '\0', // "xvhsubw.wu.hu"
'x', 'v', 'h', 's', 'u', 'b', 'w', '.', 'd', 'u', '.', 'w', 'u', '\0', // "xvhsubw.du.wu"
'x', 'v', 'h', 's', 'u', 'b', 'w', '.', 'q', 'u', '.', 'd', 'u', '\0', // "xvhsubw.qu.du"
'x', 'v', 'a', 'd', 'd', 'a', '.', 'b', '\0', // "xvadda.b"
'x', 'v', 'a', 'd', 'd', 'a', '.', 'h', '\0', // "xvadda.h"
'x', 'v', 'a', 'd', 'd', 'a', '.', 'w', '\0', // "xvadda.w"
'x', 'v', 'a', 'd', 'd', 'a', '.', 'd', '\0', // "xvadda.d"
'x', 'v', 'a', 'b', 's', 'd', '.', 'b', '\0', // "xvabsd.b"
'x', 'v', 'a', 'b', 's', 'd', '.', 'h', '\0', // "xvabsd.h"
'x', 'v', 'a', 'b', 's', 'd', '.', 'w', '\0', // "xvabsd.w"
'x', 'v', 'a', 'b', 's', 'd', '.', 'd', '\0', // "xvabsd.d"
'x', 'v', 'a', 'b', 's', 'd', '.', 'b', 'u', '\0', // "xvabsd.bu"
'x', 'v', 'a', 'b', 's', 'd', '.', 'h', 'u', '\0', // "xvabsd.hu"
'x', 'v', 'a', 'b', 's', 'd', '.', 'w', 'u', '\0', // "xvabsd.wu"
'x', 'v', 'a', 'b', 's', 'd', '.', 'd', 'u', '\0', // "xvabsd.du"
'x', 'v', 'a', 'v', 'g', '.', 'b', '\0', // "xvavg.b"
'x', 'v', 'a', 'v', 'g', '.', 'h', '\0', // "xvavg.h"
'x', 'v', 'a', 'v', 'g', '.', 'w', '\0', // "xvavg.w"
'x', 'v', 'a', 'v', 'g', '.', 'd', '\0', // "xvavg.d"
'x', 'v', 'a', 'v', 'g', '.', 'b', 'u', '\0', // "xvavg.bu"
'x', 'v', 'a', 'v', 'g', '.', 'h', 'u', '\0', // "xvavg.hu"
'x', 'v', 'a', 'v', 'g', '.', 'w', 'u', '\0', // "xvavg.wu"
'x', 'v', 'a', 'v', 'g', '.', 'd', 'u', '\0', // "xvavg.du"
'x', 'v', 'a', 'v', 'g', 'r', '.', 'b', '\0', // "xvavgr.b"
'x', 'v', 'a', 'v', 'g', 'r', '.', 'h', '\0', // "xvavgr.h"
'x', 'v', 'a', 'v', 'g', 'r', '.', 'w', '\0', // "xvavgr.w"
'x', 'v', 'a', 'v', 'g', 'r', '.', 'd', '\0', // "xvavgr.d"
'x', 'v', 'a', 'v', 'g', 'r', '.', 'b', 'u', '\0', // "xvavgr.bu"
'x', 'v', 'a', 'v', 'g', 'r', '.', 'h', 'u', '\0', // "xvavgr.hu"
'x', 'v', 'a', 'v', 'g', 'r', '.', 'w', 'u', '\0', // "xvavgr.wu"
'x', 'v', 'a', 'v', 'g', 'r', '.', 'd', 'u', '\0', // "xvavgr.du"
'x', 'v', 'm', 'a', 'x', '.', 'b', '\0', // "xvmax.b"
'x', 'v', 'm', 'a', 'x', '.', 'h', '\0', // "xvmax.h"
'x', 'v', 'm', 'a', 'x', '.', 'w', '\0', // "xvmax.w"
'x', 'v', 'm', 'a', 'x', '.', 'd', '\0', // "xvmax.d"
'x', 'v', 'm', 'i', 'n', '.', 'b', '\0', // "xvmin.b"
'x', 'v', 'm', 'i', 'n', '.', 'h', '\0', // "xvmin.h"
'x', 'v', 'm', 'i', 'n', '.', 'w', '\0', // "xvmin.w"
'x', 'v', 'm', 'i', 'n', '.', 'd', '\0', // "xvmin.d"
'x', 'v', 'm', 'a', 'x', '.', 'b', 'u', '\0', // "xvmax.bu"
'x', 'v', 'm', 'a', 'x', '.', 'h', 'u', '\0', // "xvmax.hu"
'x', 'v', 'm', 'a', 'x', '.', 'w', 'u', '\0', // "xvmax.wu"
'x', 'v', 'm', 'a', 'x', '.', 'd', 'u', '\0', // "xvmax.du"
'x', 'v', 'm', 'i', 'n', '.', 'b', 'u', '\0', // "xvmin.bu"
'x', 'v', 'm', 'i', 'n', '.', 'h', 'u', '\0', // "xvmin.hu"
'x', 'v', 'm', 'i', 'n', '.', 'w', 'u', '\0', // "xvmin.wu"
'x', 'v', 'm', 'i', 'n', '.', 'd', 'u', '\0', // "xvmin.du"
'x', 'v', 'm', 'u', 'l', '.', 'b', '\0', // "xvmul.b"
'x', 'v', 'm', 'u', 'l', '.', 'h', '\0', // "xvmul.h"
'x', 'v', 'm', 'u', 'l', '.', 'w', '\0', // "xvmul.w"
'x', 'v', 'm', 'u', 'l', '.', 'd', '\0', // "xvmul.d"
'x', 'v', 'm', 'u', 'h', '.', 'b', '\0', // "xvmuh.b"
'x', 'v', 'm', 'u', 'h', '.', 'h', '\0', // "xvmuh.h"
'x', 'v', 'm', 'u', 'h', '.', 'w', '\0', // "xvmuh.w"
'x', 'v', 'm', 'u', 'h', '.', 'd', '\0', // "xvmuh.d"
'x', 'v', 'm', 'u', 'h', '.', 'b', 'u', '\0', // "xvmuh.bu"
'x', 'v', 'm', 'u', 'h', '.', 'h', 'u', '\0', // "xvmuh.hu"
'x', 'v', 'm', 'u', 'h', '.', 'w', 'u', '\0', // "xvmuh.wu"
'x', 'v', 'm', 'u', 'h', '.', 'd', 'u', '\0', // "xvmuh.du"
'x', 'v', 'm', 'u', 'l', 'w', 'e', 'v', '.', 'h', '.', 'b', '\0', // "xvmulwev.h.b"
'x', 'v', 'm', 'u', 'l', 'w', 'e', 'v', '.', 'w', '.', 'h', '\0', // "xvmulwev.w.h"
'x', 'v', 'm', 'u', 'l', 'w', 'e', 'v', '.', 'd', '.', 'w', '\0', // "xvmulwev.d.w"
'x', 'v', 'm', 'u', 'l', 'w', 'e', 'v', '.', 'q', '.', 'd', '\0', // "xvmulwev.q.d"
'x', 'v', 'm', 'u', 'l', 'w', 'o', 'd', '.', 'h', '.', 'b', '\0', // "xvmulwod.h.b"
'x', 'v', 'm', 'u', 'l', 'w', 'o', 'd', '.', 'w', '.', 'h', '\0', // "xvmulwod.w.h"
'x', 'v', 'm', 'u', 'l', 'w', 'o', 'd', '.', 'd', '.', 'w', '\0', // "xvmulwod.d.w"
'x', 'v', 'm', 'u', 'l', 'w', 'o', 'd', '.', 'q', '.', 'd', '\0', // "xvmulwod.q.d"
'x', 'v', 'm', 'u', 'l', 'w', 'e', 'v', '.', 'h', '.', 'b', 'u', '\0', // "xvmulwev.h.bu"
'x', 'v', 'm', 'u', 'l', 'w', 'e', 'v', '.', 'w', '.', 'h', 'u', '\0', // "xvmulwev.w.hu"
'x', 'v', 'm', 'u', 'l', 'w', 'e', 'v', '.', 'd', '.', 'w', 'u', '\0', // "xvmulwev.d.wu"
'x', 'v', 'm', 'u', 'l', 'w', 'e', 'v', '.', 'q', '.', 'd', 'u', '\0', // "xvmulwev.q.du"
'x', 'v', 'm', 'u', 'l', 'w', 'o', 'd', '.', 'h', '.', 'b', 'u', '\0', // "xvmulwod.h.bu"
'x', 'v', 'm', 'u', 'l', 'w', 'o', 'd', '.', 'w', '.', 'h', 'u', '\0', // "xvmulwod.w.hu"
'x', 'v', 'm', 'u', 'l', 'w', 'o', 'd', '.', 'd', '.', 'w', 'u', '\0', // "xvmulwod.d.wu"
'x', 'v', 'm', 'u', 'l', 'w', 'o', 'd', '.', 'q', '.', 'd', 'u', '\0', // "xvmulwod.q.du"
'x', 'v', 'm', 'u', 'l', 'w', 'e', 'v', '.', 'h', '.', 'b', 'u', '.', 'b', '\0', // "xvmulwev.h.bu.b"
'x', 'v', 'm', 'u', 'l', 'w', 'e', 'v', '.', 'w', '.', 'h', 'u', '.', 'h', '\0', // "xvmulwev.w.hu.h"
'x', 'v', 'm', 'u', 'l', 'w', 'e', 'v', '.', 'd', '.', 'w', 'u', '.', 'w', '\0', // "xvmulwev.d.wu.w"
'x', 'v', 'm', 'u', 'l', 'w', 'e', 'v', '.', 'q', '.', 'd', 'u', '.', 'd', '\0', // "xvmulwev.q.du.d"
'x', 'v', 'm', 'u', 'l', 'w', 'o', 'd', '.', 'h', '.', 'b', 'u', '.', 'b', '\0', // "xvmulwod.h.bu.b"
'x', 'v', 'm', 'u', 'l', 'w', 'o', 'd', '.', 'w', '.', 'h', 'u', '.', 'h', '\0', // "xvmulwod.w.hu.h"
'x', 'v', 'm', 'u', 'l', 'w', 'o', 'd', '.', 'd', '.', 'w', 'u', '.', 'w', '\0', // "xvmulwod.d.wu.w"
'x', 'v', 'm', 'u', 'l', 'w', 'o', 'd', '.', 'q', '.', 'd', 'u', '.', 'd', '\0', // "xvmulwod.q.du.d"
'x', 'v', 'm', 'a', 'd', 'd', '.', 'b', '\0', // "xvmadd.b"
'x', 'v', 'm', 'a', 'd', 'd', '.', 'h', '\0', // "xvmadd.h"
'x', 'v', 'm', 'a', 'd', 'd', '.', 'w', '\0', // "xvmadd.w"
'x', 'v', 'm', 'a', 'd', 'd', '.', 'd', '\0', // "xvmadd.d"
'x', 'v', 'm', 's', 'u', 'b', '.', 'b', '\0', // "xvmsub.b"
'x', 'v', 'm', 's', 'u', 'b', '.', 'h', '\0', // "xvmsub.h"
'x', 'v', 'm', 's', 'u', 'b', '.', 'w', '\0', // "xvmsub.w"
'x', 'v', 'm', 's', 'u', 'b', '.', 'd', '\0', // "xvmsub.d"
'x', 'v', 'm', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'h', '.', 'b', '\0', // "xvmaddwev.h.b"
'x', 'v', 'm', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'w', '.', 'h', '\0', // "xvmaddwev.w.h"
'x', 'v', 'm', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'd', '.', 'w', '\0', // "xvmaddwev.d.w"
'x', 'v', 'm', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'q', '.', 'd', '\0', // "xvmaddwev.q.d"
'x', 'v', 'm', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'h', '.', 'b', '\0', // "xvmaddwod.h.b"
'x', 'v', 'm', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'w', '.', 'h', '\0', // "xvmaddwod.w.h"
'x', 'v', 'm', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'd', '.', 'w', '\0', // "xvmaddwod.d.w"
'x', 'v', 'm', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'q', '.', 'd', '\0', // "xvmaddwod.q.d"
'x', 'v', 'm', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'h', '.', 'b', 'u', '\0', // "xvmaddwev.h.bu"
'x', 'v', 'm', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'w', '.', 'h', 'u', '\0', // "xvmaddwev.w.hu"
'x', 'v', 'm', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'd', '.', 'w', 'u', '\0', // "xvmaddwev.d.wu"
'x', 'v', 'm', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'q', '.', 'd', 'u', '\0', // "xvmaddwev.q.du"
'x', 'v', 'm', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'h', '.', 'b', 'u', '\0', // "xvmaddwod.h.bu"
'x', 'v', 'm', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'w', '.', 'h', 'u', '\0', // "xvmaddwod.w.hu"
'x', 'v', 'm', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'd', '.', 'w', 'u', '\0', // "xvmaddwod.d.wu"
'x', 'v', 'm', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'q', '.', 'd', 'u', '\0', // "xvmaddwod.q.du"
'x', 'v', 'm', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'h', '.', 'b', 'u', '.', 'b', '\0', // "xvmaddwev.h.bu.b"
'x', 'v', 'm', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'w', '.', 'h', 'u', '.', 'h', '\0', // "xvmaddwev.w.hu.h"
'x', 'v', 'm', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'd', '.', 'w', 'u', '.', 'w', '\0', // "xvmaddwev.d.wu.w"
'x', 'v', 'm', 'a', 'd', 'd', 'w', 'e', 'v', '.', 'q', '.', 'd', 'u', '.', 'd', '\0', // "xvmaddwev.q.du.d"
'x', 'v', 'm', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'h', '.', 'b', 'u', '.', 'b', '\0', // "xvmaddwod.h.bu.b"
'x', 'v', 'm', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'w', '.', 'h', 'u', '.', 'h', '\0', // "xvmaddwod.w.hu.h"
'x', 'v', 'm', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'd', '.', 'w', 'u', '.', 'w', '\0', // "xvmaddwod.d.wu.w"
'x', 'v', 'm', 'a', 'd', 'd', 'w', 'o', 'd', '.', 'q', '.', 'd', 'u', '.', 'd', '\0', // "xvmaddwod.q.du.d"
'x', 'v', 'd', 'i', 'v', '.', 'b', '\0', // "xvdiv.b"
'x', 'v', 'd', 'i', 'v', '.', 'h', '\0', // "xvdiv.h"
'x', 'v', 'd', 'i', 'v', '.', 'w', '\0', // "xvdiv.w"
'x', 'v', 'd', 'i', 'v', '.', 'd', '\0', // "xvdiv.d"
'x', 'v', 'm', 'o', 'd', '.', 'b', '\0', // "xvmod.b"
'x', 'v', 'm', 'o', 'd', '.', 'h', '\0', // "xvmod.h"
'x', 'v', 'm', 'o', 'd', '.', 'w', '\0', // "xvmod.w"
'x', 'v', 'm', 'o', 'd', '.', 'd', '\0', // "xvmod.d"
'x', 'v', 'd', 'i', 'v', '.', 'b', 'u', '\0', // "xvdiv.bu"
'x', 'v', 'd', 'i', 'v', '.', 'h', 'u', '\0', // "xvdiv.hu"
'x', 'v', 'd', 'i', 'v', '.', 'w', 'u', '\0', // "xvdiv.wu"
'x', 'v', 'd', 'i', 'v', '.', 'd', 'u', '\0', // "xvdiv.du"
'x', 'v', 'm', 'o', 'd', '.', 'b', 'u', '\0', // "xvmod.bu"
'x', 'v', 'm', 'o', 'd', '.', 'h', 'u', '\0', // "xvmod.hu"
'x', 'v', 'm', 'o', 'd', '.', 'w', 'u', '\0', // "xvmod.wu"
'x', 'v', 'm', 'o', 'd', '.', 'd', 'u', '\0', // "xvmod.du"
'x', 'v', 's', 'l', 'l', '.', 'b', '\0', // "xvsll.b"
'x', 'v', 's', 'l', 'l', '.', 'h', '\0', // "xvsll.h"
'x', 'v', 's', 'l', 'l', '.', 'w', '\0', // "xvsll.w"
'x', 'v', 's', 'l', 'l', '.', 'd', '\0', // "xvsll.d"
'x', 'v', 's', 'r', 'l', '.', 'b', '\0', // "xvsrl.b"
'x', 'v', 's', 'r', 'l', '.', 'h', '\0', // "xvsrl.h"
'x', 'v', 's', 'r', 'l', '.', 'w', '\0', // "xvsrl.w"
'x', 'v', 's', 'r', 'l', '.', 'd', '\0', // "xvsrl.d"
'x', 'v', 's', 'r', 'a', '.', 'b', '\0', // "xvsra.b"
'x', 'v', 's', 'r', 'a', '.', 'h', '\0', // "xvsra.h"
'x', 'v', 's', 'r', 'a', '.', 'w', '\0', // "xvsra.w"
'x', 'v', 's', 'r', 'a', '.', 'd', '\0', // "xvsra.d"
'x', 'v', 'r', 'o', 't', 'r', '.', 'b', '\0', // "xvrotr.b"
'x', 'v', 'r', 'o', 't', 'r', '.', 'h', '\0', // "xvrotr.h"
'x', 'v', 'r', 'o', 't', 'r', '.', 'w', '\0', // "xvrotr.w"
'x', 'v', 'r', 'o', 't', 'r', '.', 'd', '\0', // "xvrotr.d"
'x', 'v', 's', 'r', 'l', 'r', '.', 'b', '\0', // "xvsrlr.b"
'x', 'v', 's', 'r', 'l', 'r', '.', 'h', '\0', // "xvsrlr.h"
'x', 'v', 's', 'r', 'l', 'r', '.', 'w', '\0', // "xvsrlr.w"
'x', 'v', 's', 'r', 'l', 'r', '.', 'd', '\0', // "xvsrlr.d"
'x', 'v', 's', 'r', 'a', 'r', '.', 'b', '\0', // "xvsrar.b"
'x', 'v', 's', 'r', 'a', 'r', '.', 'h', '\0', // "xvsrar.h"
'x', 'v', 's', 'r', 'a', 'r', '.', 'w', '\0', // "xvsrar.w"
'x', 'v', 's', 'r', 'a', 'r', '.', 'd', '\0', // "xvsrar.d"
'x', 'v', 's', 'r', 'l', 'n', '.', 'b', '.', 'h', '\0', // "xvsrln.b.h"
'x', 'v', 's', 'r', 'l', 'n', '.', 'h', '.', 'w', '\0', // "xvsrln.h.w"
'x', 'v', 's', 'r', 'l', 'n', '.', 'w', '.', 'd', '\0', // "xvsrln.w.d"
'x', 'v', 's', 'r', 'a', 'n', '.', 'b', '.', 'h', '\0', // "xvsran.b.h"
'x', 'v', 's', 'r', 'a', 'n', '.', 'h', '.', 'w', '\0', // "xvsran.h.w"
'x', 'v', 's', 'r', 'a', 'n', '.', 'w', '.', 'd', '\0', // "xvsran.w.d"
'x', 'v', 's', 'r', 'l', 'r', 'n', '.', 'b', '.', 'h', '\0', // "xvsrlrn.b.h"
'x', 'v', 's', 'r', 'l', 'r', 'n', '.', 'h', '.', 'w', '\0', // "xvsrlrn.h.w"
'x', 'v', 's', 'r', 'l', 'r', 'n', '.', 'w', '.', 'd', '\0', // "xvsrlrn.w.d"
'x', 'v', 's', 'r', 'a', 'r', 'n', '.', 'b', '.', 'h', '\0', // "xvsrarn.b.h"
'x', 'v', 's', 'r', 'a', 'r', 'n', '.', 'h', '.', 'w', '\0', // "xvsrarn.h.w"
'x', 'v', 's', 'r', 'a', 'r', 'n', '.', 'w', '.', 'd', '\0', // "xvsrarn.w.d"
'x', 'v', 's', 's', 'r', 'l', 'n', '.', 'b', '.', 'h', '\0', // "xvssrln.b.h"
'x', 'v', 's', 's', 'r', 'l', 'n', '.', 'h', '.', 'w', '\0', // "xvssrln.h.w"
'x', 'v', 's', 's', 'r', 'l', 'n', '.', 'w', '.', 'd', '\0', // "xvssrln.w.d"
'x', 'v', 's', 's', 'r', 'a', 'n', '.', 'b', '.', 'h', '\0', // "xvssran.b.h"
'x', 'v', 's', 's', 'r', 'a', 'n', '.', 'h', '.', 'w', '\0', // "xvssran.h.w"
'x', 'v', 's', 's', 'r', 'a', 'n', '.', 'w', '.', 'd', '\0', // "xvssran.w.d"
'x', 'v', 's', 's', 'r', 'l', 'r', 'n', '.', 'b', '.', 'h', '\0', // "xvssrlrn.b.h"
'x', 'v', 's', 's', 'r', 'l', 'r', 'n', '.', 'h', '.', 'w', '\0', // "xvssrlrn.h.w"
'x', 'v', 's', 's', 'r', 'l', 'r', 'n', '.', 'w', '.', 'd', '\0', // "xvssrlrn.w.d"
'x', 'v', 's', 's', 'r', 'a', 'r', 'n', '.', 'b', '.', 'h', '\0', // "xvssrarn.b.h"
'x', 'v', 's', 's', 'r', 'a', 'r', 'n', '.', 'h', '.', 'w', '\0', // "xvssrarn.h.w"
'x', 'v', 's', 's', 'r', 'a', 'r', 'n', '.', 'w', '.', 'd', '\0', // "xvssrarn.w.d"
'x', 'v', 's', 's', 'r', 'l', 'n', '.', 'b', 'u', '.', 'h', '\0', // "xvssrln.bu.h"
'x', 'v', 's', 's', 'r', 'l', 'n', '.', 'h', 'u', '.', 'w', '\0', // "xvssrln.hu.w"
'x', 'v', 's', 's', 'r', 'l', 'n', '.', 'w', 'u', '.', 'd', '\0', // "xvssrln.wu.d"
'x', 'v', 's', 's', 'r', 'a', 'n', '.', 'b', 'u', '.', 'h', '\0', // "xvssran.bu.h"
'x', 'v', 's', 's', 'r', 'a', 'n', '.', 'h', 'u', '.', 'w', '\0', // "xvssran.hu.w"
'x', 'v', 's', 's', 'r', 'a', 'n', '.', 'w', 'u', '.', 'd', '\0', // "xvssran.wu.d"
'x', 'v', 's', 's', 'r', 'l', 'r', 'n', '.', 'b', 'u', '.', 'h', '\0', // "xvssrlrn.bu.h"
'x', 'v', 's', 's', 'r', 'l', 'r', 'n', '.', 'h', 'u', '.', 'w', '\0', // "xvssrlrn.hu.w"
'x', 'v', 's', 's', 'r', 'l', 'r', 'n', '.', 'w', 'u', '.', 'd', '\0', // "xvssrlrn.wu.d"
'x', 'v', 's', 's', 'r', 'a', 'r', 'n', '.', 'b', 'u', '.', 'h', '\0', // "xvssrarn.bu.h"
'x', 'v', 's', 's', 'r', 'a', 'r', 'n', '.', 'h', 'u', '.', 'w', '\0', // "xvssrarn.hu.w"
'x', 'v', 's', 's', 'r', 'a', 'r', 'n', '.', 'w', 'u', '.', 'd', '\0', // "xvssrarn.wu.d"
'x', 'v', 'b', 'i', 't', 'c', 'l', 'r', '.', 'b', '\0', // "xvbitclr.b"
'x', 'v', 'b', 'i', 't', 'c', 'l', 'r', '.', 'h', '\0', // "xvbitclr.h"
'x', 'v', 'b', 'i', 't', 'c', 'l', 'r', '.', 'w', '\0', // "xvbitclr.w"
'x', 'v', 'b', 'i', 't', 'c', 'l', 'r', '.', 'd', '\0', // "xvbitclr.d"
'x', 'v', 'b', 'i', 't', 's', 'e', 't', '.', 'b', '\0', // "xvbitset.b"
'x', 'v', 'b', 'i', 't', 's', 'e', 't', '.', 'h', '\0', // "xvbitset.h"
'x', 'v', 'b', 'i', 't', 's', 'e', 't', '.', 'w', '\0', // "xvbitset.w"
'x', 'v', 'b', 'i', 't', 's', 'e', 't', '.', 'd', '\0', // "xvbitset.d"
'x', 'v', 'b', 'i', 't', 'r', 'e', 'v', '.', 'b', '\0', // "xvbitrev.b"
'x', 'v', 'b', 'i', 't', 'r', 'e', 'v', '.', 'h', '\0', // "xvbitrev.h"
'x', 'v', 'b', 'i', 't', 'r', 'e', 'v', '.', 'w', '\0', // "xvbitrev.w"
'x', 'v', 'b', 'i', 't', 'r', 'e', 'v', '.', 'd', '\0', // "xvbitrev.d"
'x', 'v', 'p', 'a', 'c', 'k', 'e', 'v', '.', 'b', '\0', // "xvpackev.b"
'x', 'v', 'p', 'a', 'c', 'k', 'e', 'v', '.', 'h', '\0', // "xvpackev.h"
'x', 'v', 'p', 'a', 'c', 'k', 'e', 'v', '.', 'w', '\0', // "xvpackev.w"
'x', 'v', 'p', 'a', 'c', 'k', 'e', 'v', '.', 'd', '\0', // "xvpackev.d"
'x', 'v', 'p', 'a', 'c', 'k', 'o', 'd', '.', 'b', '\0', // "xvpackod.b"
'x', 'v', 'p', 'a', 'c', 'k', 'o', 'd', '.', 'h', '\0', // "xvpackod.h"
'x', 'v', 'p', 'a', 'c', 'k', 'o', 'd', '.', 'w', '\0', // "xvpackod.w"
'x', 'v', 'p', 'a', 'c', 'k', 'o', 'd', '.', 'd', '\0', // "xvpackod.d"
'x', 'v', 'i', 'l', 'v', 'l', '.', 'b', '\0', // "xvilvl.b"
'x', 'v', 'i', 'l', 'v', 'l', '.', 'h', '\0', // "xvilvl.h"
'x', 'v', 'i', 'l', 'v', 'l', '.', 'w', '\0', // "xvilvl.w"
'x', 'v', 'i', 'l', 'v', 'l', '.', 'd', '\0', // "xvilvl.d"
'x', 'v', 'i', 'l', 'v', 'h', '.', 'b', '\0', // "xvilvh.b"
'x', 'v', 'i', 'l', 'v', 'h', '.', 'h', '\0', // "xvilvh.h"
'x', 'v', 'i', 'l', 'v', 'h', '.', 'w', '\0', // "xvilvh.w"
'x', 'v', 'i', 'l', 'v', 'h', '.', 'd', '\0', // "xvilvh.d"
'x', 'v', 'p', 'i', 'c', 'k', 'e', 'v', '.', 'b', '\0', // "xvpickev.b"
'x', 'v', 'p', 'i', 'c', 'k', 'e', 'v', '.', 'h', '\0', // "xvpickev.h"
'x', 'v', 'p', 'i', 'c', 'k', 'e', 'v', '.', 'w', '\0', // "xvpickev.w"
'x', 'v', 'p', 'i', 'c', 'k', 'e', 'v', '.', 'd', '\0', // "xvpickev.d"
'x', 'v', 'p', 'i', 'c', 'k', 'o', 'd', '.', 'b', '\0', // "xvpickod.b"
'x', 'v', 'p', 'i', 'c', 'k', 'o', 'd', '.', 'h', '\0', // "xvpickod.h"
'x', 'v', 'p', 'i', 'c', 'k', 'o', 'd', '.', 'w', '\0', // "xvpickod.w"
'x', 'v', 'p', 'i', 'c', 'k', 'o', 'd', '.', 'd', '\0', // "xvpickod.d"
'x', 'v', 'a', 'n', 'd', '.', 'v', '\0', // "xvand.v"
'x', 'v', 'o', 'r', '.', 'v', '\0', // "xvor.v"
'x', 'v', 'x', 'o', 'r', '.', 'v', '\0', // "xvxor.v"
'x', 'v', 'n', 'o', 'r', '.', 'v', '\0', // "xvnor.v"
'x', 'v', 'a', 'n', 'd', 'n', '.', 'v', '\0', // "xvandn.v"
'x', 'v', 'o', 'r', 'n', '.', 'v', '\0', // "xvorn.v"
'x', 'v', 'f', 'r', 's', 't', 'p', '.', 'b', '\0', // "xvfrstp.b"
'x', 'v', 'f', 'r', 's', 't', 'p', '.', 'h', '\0', // "xvfrstp.h"
'x', 'v', 'a', 'd', 'd', '.', 'q', '\0', // "xvadd.q"
'x', 'v', 's', 'u', 'b', '.', 'q', '\0', // "xvsub.q"
'x', 'v', 's', 'i', 'g', 'n', 'c', 'o', 'v', '.', 'b', '\0', // "xvsigncov.b"
'x', 'v', 's', 'i', 'g', 'n', 'c', 'o', 'v', '.', 'h', '\0', // "xvsigncov.h"
'x', 'v', 's', 'i', 'g', 'n', 'c', 'o', 'v', '.', 'w', '\0', // "xvsigncov.w"
'x', 'v', 's', 'i', 'g', 'n', 'c', 'o', 'v', '.', 'd', '\0', // "xvsigncov.d"
'x', 'v', 'f', 'a', 'd', 'd', '.', 's', '\0', // "xvfadd.s"
'x', 'v', 'f', 'a', 'd', 'd', '.', 'd', '\0', // "xvfadd.d"
'x', 'v', 'f', 's', 'u', 'b', '.', 's', '\0', // "xvfsub.s"
'x', 'v', 'f', 's', 'u', 'b', '.', 'd', '\0', // "xvfsub.d"
'x', 'v', 'f', 'm', 'u', 'l', '.', 's', '\0', // "xvfmul.s"
'x', 'v', 'f', 'm', 'u', 'l', '.', 'd', '\0', // "xvfmul.d"
'x', 'v', 'f', 'd', 'i', 'v', '.', 's', '\0', // "xvfdiv.s"
'x', 'v', 'f', 'd', 'i', 'v', '.', 'd', '\0', // "xvfdiv.d"
'x', 'v', 'f', 'm', 'a', 'x', '.', 's', '\0', // "xvfmax.s"
'x', 'v', 'f', 'm', 'a', 'x', '.', 'd', '\0', // "xvfmax.d"
'x', 'v', 'f', 'm', 'i', 'n', '.', 's', '\0', // "xvfmin.s"
'x', 'v', 'f', 'm', 'i', 'n', '.', 'd', '\0', // "xvfmin.d"
'x', 'v', 'f', 'm', 'a', 'x', 'a', '.', 's', '\0', // "xvfmaxa.s"
'x', 'v', 'f', 'm', 'a', 'x', 'a', '.', 'd', '\0', // "xvfmaxa.d"
'x', 'v', 'f', 'm', 'i', 'n', 'a', '.', 's', '\0', // "xvfmina.s"
'x', 'v', 'f', 'm', 'i', 'n', 'a', '.', 'd', '\0', // "xvfmina.d"
'x', 'v', 'f', 'c', 'v', 't', '.', 'h', '.', 's', '\0', // "xvfcvt.h.s"
'x', 'v', 'f', 'c', 'v', 't', '.', 's', '.', 'd', '\0', // "xvfcvt.s.d"
'x', 'v', 'f', 'f', 'i', 'n', 't', '.', 's', '.', 'l', '\0', // "xvffint.s.l"
'x', 'v', 'f', 't', 'i', 'n', 't', '.', 'w', '.', 'd', '\0', // "xvftint.w.d"
'x', 'v', 'f', 't', 'i', 'n', 't', 'r', 'm', '.', 'w', '.', 'd', '\0', // "xvftintrm.w.d"
'x', 'v', 'f', 't', 'i', 'n', 't', 'r', 'p', '.', 'w', '.', 'd', '\0', // "xvftintrp.w.d"
'x', 'v', 'f', 't', 'i', 'n', 't', 'r', 'z', '.', 'w', '.', 'd', '\0', // "xvftintrz.w.d"
'x', 'v', 'f', 't', 'i', 'n', 't', 'r', 'n', 'e', '.', 'w', '.', 'd', '\0', // "xvftintrne.w.d"
'x', 'v', 's', 'h', 'u', 'f', '.', 'h', '\0', // "xvshuf.h"
'x', 'v', 's', 'h', 'u', 'f', '.', 'w', '\0', // "xvshuf.w"
'x', 'v', 's', 'h', 'u', 'f', '.', 'd', '\0', // "xvshuf.d"
'x', 'v', 'p', 'e', 'r', 'm', '.', 'w', '\0', // "xvperm.w"
'x', 'v', 'r', 'e', 'p', 'l', 'g', 'r', '2', 'v', 'r', '.', 'b', '\0', // "xvreplgr2vr.b"
'x', 'v', 'r', 'e', 'p', 'l', 'g', 'r', '2', 'v', 'r', '.', 'h', '\0', // "xvreplgr2vr.h"
'x', 'v', 'r', 'e', 'p', 'l', 'g', 'r', '2', 'v', 'r', '.', 'w', '\0', // "xvreplgr2vr.w"
'x', 'v', 'r', 'e', 'p', 'l', 'g', 'r', '2', 'v', 'r', '.', 'd', '\0', // "xvreplgr2vr.d"
'x', 'v', 'c', 'l', 'o', '.', 'b', '\0', // "xvclo.b"
'x', 'v', 'c', 'l', 'o', '.', 'h', '\0', // "xvclo.h"
'x', 'v', 'c', 'l', 'o', '.', 'w', '\0', // "xvclo.w"
'x', 'v', 'c', 'l', 'o', '.', 'd', '\0', // "xvclo.d"
'x', 'v', 'c', 'l', 'z', '.', 'b', '\0', // "xvclz.b"
'x', 'v', 'c', 'l', 'z', '.', 'h', '\0', // "xvclz.h"
'x', 'v', 'c', 'l', 'z', '.', 'w', '\0', // "xvclz.w"
'x', 'v', 'c', 'l', 'z', '.', 'd', '\0', // "xvclz.d"
'x', 'v', 'p', 'c', 'n', 't', '.', 'b', '\0', // "xvpcnt.b"
'x', 'v', 'p', 'c', 'n', 't', '.', 'h', '\0', // "xvpcnt.h"
'x', 'v', 'p', 'c', 'n', 't', '.', 'w', '\0', // "xvpcnt.w"
'x', 'v', 'p', 'c', 'n', 't', '.', 'd', '\0', // "xvpcnt.d"
'x', 'v', 'n', 'e', 'g', '.', 'b', '\0', // "xvneg.b"
'x', 'v', 'n', 'e', 'g', '.', 'h', '\0', // "xvneg.h"
'x', 'v', 'n', 'e', 'g', '.', 'w', '\0', // "xvneg.w"
'x', 'v', 'n', 'e', 'g', '.', 'd', '\0', // "xvneg.d"
'x', 'v', 'm', 's', 'k', 'l', 't', 'z', '.', 'b', '\0', // "xvmskltz.b"
'x', 'v', 'm', 's', 'k', 'l', 't', 'z', '.', 'h', '\0', // "xvmskltz.h"
'x', 'v', 'm', 's', 'k', 'l', 't', 'z', '.', 'w', '\0', // "xvmskltz.w"
'x', 'v', 'm', 's', 'k', 'l', 't', 'z', '.', 'd', '\0', // "xvmskltz.d"
'x', 'v', 'm', 's', 'k', 'g', 'e', 'z', '.', 'b', '\0', // "xvmskgez.b"
'x', 'v', 'm', 's', 'k', 'n', 'z', '.', 'b', '\0', // "xvmsknz.b"
'x', 'v', 'f', 'l', 'o', 'g', 'b', '.', 's', '\0', // "xvflogb.s"
'x', 'v', 'f', 'l', 'o', 'g', 'b', '.', 'd', '\0', // "xvflogb.d"
'x', 'v', 'f', 'c', 'l', 'a', 's', 's', '.', 's', '\0', // "xvfclass.s"
'x', 'v', 'f', 'c', 'l', 'a', 's', 's', '.', 'd', '\0', // "xvfclass.d"
'x', 'v', 'f', 's', 'q', 'r', 't', '.', 's', '\0', // "xvfsqrt.s"
'x', 'v', 'f', 's', 'q', 'r', 't', '.', 'd', '\0', // "xvfsqrt.d"
'x', 'v', 'f', 'r', 'e', 'c', 'i', 'p', '.', 's', '\0', // "xvfrecip.s"
'x', 'v', 'f', 'r', 'e', 'c', 'i', 'p', '.', 'd', '\0', // "xvfrecip.d"
'x', 'v', 'f', 'r', 's', 'q', 'r', 't', '.', 's', '\0', // "xvfrsqrt.s"
'x', 'v', 'f', 'r', 's', 'q', 'r', 't', '.', 'd', '\0', // "xvfrsqrt.d"
'x', 'v', 'f', 'r', 'e', 'c', 'i', 'p', 'e', '.', 's', '\0', // "xvfrecipe.s"
'x', 'v', 'f', 'r', 'e', 'c', 'i', 'p', 'e', '.', 'd', '\0', // "xvfrecipe.d"
'x', 'v', 'f', 'r', 's', 'q', 'r', 't', 'e', '.', 's', '\0', // "xvfrsqrte.s"
'x', 'v', 'f', 'r', 's', 'q', 'r', 't', 'e', '.', 'd', '\0', // "xvfrsqrte.d"
'x', 'v', 'f', 'r', 'i', 'n', 't', '.', 's', '\0', // "xvfrint.s"
'x', 'v', 'f', 'r', 'i', 'n', 't', '.', 'd', '\0', // "xvfrint.d"
'x', 'v', 'f', 'r', 'i', 'n', 't', 'r', 'm', '.', 's', '\0', // "xvfrintrm.s"
'x', 'v', 'f', 'r', 'i', 'n', 't', 'r', 'm', '.', 'd', '\0', // "xvfrintrm.d"
'x', 'v', 'f', 'r', 'i', 'n', 't', 'r', 'p', '.', 's', '\0', // "xvfrintrp.s"
'x', 'v', 'f', 'r', 'i', 'n', 't', 'r', 'p', '.', 'd', '\0', // "xvfrintrp.d"
'x', 'v', 'f', 'r', 'i', 'n', 't', 'r', 'z', '.', 's', '\0', // "xvfrintrz.s"
'x', 'v', 'f', 'r', 'i', 'n', 't', 'r', 'z', '.', 'd', '\0', // "xvfrintrz.d"
'x', 'v', 'f', 'r', 'i', 'n', 't', 'r', 'n', 'e', '.', 's', '\0', // "xvfrintrne.s"
'x', 'v', 'f', 'r', 'i', 'n', 't', 'r', 'n', 'e', '.', 'd', '\0', // "xvfrintrne.d"
'x', 'v', 'f', 'c', 'v', 't', 'l', '.', 's', '.', 'h', '\0', // "xvfcvtl.s.h"
'x', 'v', 'f', 'c', 'v', 't', 'h', '.', 's', '.', 'h', '\0', // "xvfcvth.s.h"
'x', 'v', 'f', 'c', 'v', 't', 'l', '.', 'd', '.', 's', '\0', // "xvfcvtl.d.s"
'x', 'v', 'f', 'c', 'v', 't', 'h', '.', 'd', '.', 's', '\0', // "xvfcvth.d.s"
'x', 'v', 'f', 'f', 'i', 'n', 't', '.', 's', '.', 'w', '\0', // "xvffint.s.w"
'x', 'v', 'f', 'f', 'i', 'n', 't', '.', 's', '.', 'w', 'u', '\0', // "xvffint.s.wu"
'x', 'v', 'f', 'f', 'i', 'n', 't', '.', 'd', '.', 'l', '\0', // "xvffint.d.l"
'x', 'v', 'f', 'f', 'i', 'n', 't', '.', 'd', '.', 'l', 'u', '\0', // "xvffint.d.lu"
'x', 'v', 'f', 'f', 'i', 'n', 't', 'l', '.', 'd', '.', 'w', '\0', // "xvffintl.d.w"
'x', 'v', 'f', 'f', 'i', 'n', 't', 'h', '.', 'd', '.', 'w', '\0', // "xvffinth.d.w"
'x', 'v', 'f', 't', 'i', 'n', 't', '.', 'w', '.', 's', '\0', // "xvftint.w.s"
'x', 'v', 'f', 't', 'i', 'n', 't', '.', 'l', '.', 'd', '\0', // "xvftint.l.d"
'x', 'v', 'f', 't', 'i', 'n', 't', 'r', 'm', '.', 'w', '.', 's', '\0', // "xvftintrm.w.s"
'x', 'v', 'f', 't', 'i', 'n', 't', 'r', 'm', '.', 'l', '.', 'd', '\0', // "xvftintrm.l.d"
'x', 'v', 'f', 't', 'i', 'n', 't', 'r', 'p', '.', 'w', '.', 's', '\0', // "xvftintrp.w.s"
'x', 'v', 'f', 't', 'i', 'n', 't', 'r', 'p', '.', 'l', '.', 'd', '\0', // "xvftintrp.l.d"
'x', 'v', 'f', 't', 'i', 'n', 't', 'r', 'z', '.', 'w', '.', 's', '\0', // "xvftintrz.w.s"
'x', 'v', 'f', 't', 'i', 'n', 't', 'r', 'z', '.', 'l', '.', 'd', '\0', // "xvftintrz.l.d"
'x', 'v', 'f', 't', 'i', 'n', 't', 'r', 'n', 'e', '.', 'w', '.', 's', '\0', // "xvftintrne.w.s"
'x', 'v', 'f', 't', 'i', 'n', 't', 'r', 'n', 'e', '.', 'l', '.', 'd', '\0', // "xvftintrne.l.d"
'x', 'v', 'f', 't', 'i', 'n', 't', '.', 'w', 'u', '.', 's', '\0', // "xvftint.wu.s"
'x', 'v', 'f', 't', 'i', 'n', 't', '.', 'l', 'u', '.', 'd', '\0', // "xvftint.lu.d"
'x', 'v', 'f', 't', 'i', 'n', 't', 'r', 'z', '.', 'w', 'u', '.', 's', '\0', // "xvftintrz.wu.s"
'x', 'v', 'f', 't', 'i', 'n', 't', 'r', 'z', '.', 'l', 'u', '.', 'd', '\0', // "xvftintrz.lu.d"
'x', 'v', 'f', 't', 'i', 'n', 't', 'l', '.', 'l', '.', 's', '\0', // "xvftintl.l.s"
'x', 'v', 'f', 't', 'i', 'n', 't', 'h', '.', 'l', '.', 's', '\0', // "xvftinth.l.s"
'x', 'v', 'f', 't', 'i', 'n', 't', 'r', 'm', 'l', '.', 'l', '.', 's', '\0', // "xvftintrml.l.s"
'x', 'v', 'f', 't', 'i', 'n', 't', 'r', 'm', 'h', '.', 'l', '.', 's', '\0', // "xvftintrmh.l.s"
'x', 'v', 'f', 't', 'i', 'n', 't', 'r', 'p', 'l', '.', 'l', '.', 's', '\0', // "xvftintrpl.l.s"
'x', 'v', 'f', 't', 'i', 'n', 't', 'r', 'p', 'h', '.', 'l', '.', 's', '\0', // "xvftintrph.l.s"
'x', 'v', 'f', 't', 'i', 'n', 't', 'r', 'z', 'l', '.', 'l', '.', 's', '\0', // "xvftintrzl.l.s"
'x', 'v', 'f', 't', 'i', 'n', 't', 'r', 'z', 'h', '.', 'l', '.', 's', '\0', // "xvftintrzh.l.s"
'x', 'v', 'f', 't', 'i', 'n', 't', 'r', 'n', 'e', 'l', '.', 'l', '.', 's', '\0', // "xvftintrnel.l.s"
'x', 'v', 'f', 't', 'i', 'n', 't', 'r', 'n', 'e', 'h', '.', 'l', '.', 's', '\0', // "xvftintrneh.l.s"
'x', 'v', 'e', 'x', 't', 'h', '.', 'h', '.', 'b', '\0', // "xvexth.h.b"
'x', 'v', 'e', 'x', 't', 'h', '.', 'w', '.', 'h', '\0', // "xvexth.w.h"
'x', 'v', 'e', 'x', 't', 'h', '.', 'd', '.', 'w', '\0', // "xvexth.d.w"
'x', 'v', 'e', 'x', 't', 'h', '.', 'q', '.', 'd', '\0', // "xvexth.q.d"
'x', 'v', 'e', 'x', 't', 'h', '.', 'h', 'u', '.', 'b', 'u', '\0', // "xvexth.hu.bu"
'x', 'v', 'e', 'x', 't', 'h', '.', 'w', 'u', '.', 'h', 'u', '\0', // "xvexth.wu.hu"
'x', 'v', 'e', 'x', 't', 'h', '.', 'd', 'u', '.', 'w', 'u', '\0', // "xvexth.du.wu"
'x', 'v', 'e', 'x', 't', 'h', '.', 'q', 'u', '.', 'd', 'u', '\0', // "xvexth.qu.du"
'v', 'e', 'x', 't', '2', 'x', 'v', '.', 'h', '.', 'b', '\0', // "vext2xv.h.b"
'v', 'e', 'x', 't', '2', 'x', 'v', '.', 'w', '.', 'b', '\0', // "vext2xv.w.b"
'v', 'e', 'x', 't', '2', 'x', 'v', '.', 'd', '.', 'b', '\0', // "vext2xv.d.b"
'v', 'e', 'x', 't', '2', 'x', 'v', '.', 'w', '.', 'h', '\0', // "vext2xv.w.h"
'v', 'e', 'x', 't', '2', 'x', 'v', '.', 'd', '.', 'h', '\0', // "vext2xv.d.h"
'v', 'e', 'x', 't', '2', 'x', 'v', '.', 'd', '.', 'w', '\0', // "vext2xv.d.w"
'v', 'e', 'x', 't', '2', 'x', 'v', '.', 'h', 'u', '.', 'b', 'u', '\0', // "vext2xv.hu.bu"
'v', 'e', 'x', 't', '2', 'x', 'v', '.', 'w', 'u', '.', 'b', 'u', '\0', // "vext2xv.wu.bu"
'v', 'e', 'x', 't', '2', 'x', 'v', '.', 'd', 'u', '.', 'b', 'u', '\0', // "vext2xv.du.bu"
'v', 'e', 'x', 't', '2', 'x', 'v', '.', 'w', 'u', '.', 'h', 'u', '\0', // "vext2xv.wu.hu"
'v', 'e', 'x', 't', '2', 'x', 'v', '.', 'd', 'u', '.', 'h', 'u', '\0', // "vext2xv.du.hu"
'v', 'e', 'x', 't', '2', 'x', 'v', '.', 'd', 'u', '.', 'w', 'u', '\0', // "vext2xv.du.wu"
'x', 'v', 'r', 'e', 'p', 'l', 'v', 'e', '0', '.', 'b', '\0', // "xvreplve0.b"
'x', 'v', 'r', 'e', 'p', 'l', 'v', 'e', '0', '.', 'h', '\0', // "xvreplve0.h"
'x', 'v', 'r', 'e', 'p', 'l', 'v', 'e', '0', '.', 'w', '\0', // "xvreplve0.w"
'x', 'v', 'r', 'e', 'p', 'l', 'v', 'e', '0', '.', 'd', '\0', // "xvreplve0.d"
'x', 'v', 'r', 'e', 'p', 'l', 'v', 'e', '0', '.', 'q', '\0', // "xvreplve0.q"
'x', 'v', 'e', 'x', 't', 'l', '.', 'q', '.', 'd', '\0', // "xvextl.q.d"
'x', 'v', 'e', 'x', 't', 'l', '.', 'q', 'u', '.', 'd', 'u', '\0', // "xvextl.qu.du"
'x', 'v', 's', 't', 'e', 'l', 'm', '.', 'b', '\0', // "xvstelm.b"
'x', 'v', 's', 't', 'e', 'l', 'm', '.', 'h', '\0', // "xvstelm.h"
'x', 'v', 's', 't', 'e', 'l', 'm', '.', 'w', '\0', // "xvstelm.w"
'x', 'v', 's', 't', 'e', 'l', 'm', '.', 'd', '\0', // "xvstelm.d"
'x', 'v', 's', 'e', 'q', 'i', '.', 'b', '\0', // "xvseqi.b"
'x', 'v', 's', 'e', 'q', 'i', '.', 'h', '\0', // "xvseqi.h"
'x', 'v', 's', 'e', 'q', 'i', '.', 'w', '\0', // "xvseqi.w"
'x', 'v', 's', 'e', 'q', 'i', '.', 'd', '\0', // "xvseqi.d"
'x', 'v', 's', 'l', 'e', 'i', '.', 'b', '\0', // "xvslei.b"
'x', 'v', 's', 'l', 'e', 'i', '.', 'h', '\0', // "xvslei.h"
'x', 'v', 's', 'l', 'e', 'i', '.', 'w', '\0', // "xvslei.w"
'x', 'v', 's', 'l', 'e', 'i', '.', 'd', '\0', // "xvslei.d"
'x', 'v', 's', 'l', 't', 'i', '.', 'b', '\0', // "xvslti.b"
'x', 'v', 's', 'l', 't', 'i', '.', 'h', '\0', // "xvslti.h"
'x', 'v', 's', 'l', 't', 'i', '.', 'w', '\0', // "xvslti.w"
'x', 'v', 's', 'l', 't', 'i', '.', 'd', '\0', // "xvslti.d"
'x', 'v', 'm', 'a', 'x', 'i', '.', 'b', '\0', // "xvmaxi.b"
'x', 'v', 'm', 'a', 'x', 'i', '.', 'h', '\0', // "xvmaxi.h"
'x', 'v', 'm', 'a', 'x', 'i', '.', 'w', '\0', // "xvmaxi.w"
'x', 'v', 'm', 'a', 'x', 'i', '.', 'd', '\0', // "xvmaxi.d"
'x', 'v', 'm', 'i', 'n', 'i', '.', 'b', '\0', // "xvmini.b"
'x', 'v', 'm', 'i', 'n', 'i', '.', 'h', '\0', // "xvmini.h"
'x', 'v', 'm', 'i', 'n', 'i', '.', 'w', '\0', // "xvmini.w"
'x', 'v', 'm', 'i', 'n', 'i', '.', 'd', '\0', // "xvmini.d"
'x', 'v', 'l', 'd', 'r', 'e', 'p', 'l', '.', 'd', '\0', // "xvldrepl.d"
'x', 'v', 'l', 'd', 'r', 'e', 'p', 'l', '.', 'w', '\0', // "xvldrepl.w"
'x', 'v', 'l', 'd', 'r', 'e', 'p', 'l', '.', 'h', '\0', // "xvldrepl.h"
'x', 'v', 'l', 'd', '\0', // "xvld"
'x', 'v', 'l', 'd', 'r', 'e', 'p', 'l', '.', 'b', '\0', // "xvldrepl.b"
'x', 'v', 's', 't', '\0', // "xvst"
'x', 'v', 'r', 'e', 'p', 'l', '1', '2', '8', 'v', 'e', 'i', '.', 'd', '\0', // "xvrepl128vei.d"
'x', 'v', 'i', 'n', 's', 'v', 'e', '0', '.', 'd', '\0', // "xvinsve0.d"
'x', 'v', 'r', 'e', 'p', 'l', '1', '2', '8', 'v', 'e', 'i', '.', 'w', '\0', // "xvrepl128vei.w"
'x', 'v', 'p', 'i', 'c', 'k', 'v', 'e', '.', 'd', '\0', // "xvpickve.d"
'x', 'v', 'i', 'n', 's', 'g', 'r', '2', 'v', 'r', '.', 'd', '\0', // "xvinsgr2vr.d"
'x', 'v', 'p', 'i', 'c', 'k', 'v', 'e', '2', 'g', 'r', '.', 'd', '\0', // "xvpickve2gr.d"
'x', 'v', 'p', 'i', 'c', 'k', 'v', 'e', '2', 'g', 'r', '.', 'd', 'u', '\0', // "xvpickve2gr.du"
'x', 'v', 'p', 'i', 'c', 'k', 'v', 'e', '2', 'g', 'r', '.', 'w', '\0', // "xvpickve2gr.w"
'x', 'v', 'p', 'i', 'c', 'k', 'v', 'e', '2', 'g', 'r', '.', 'w', 'u', '\0', // "xvpickve2gr.wu"
'x', 'v', 's', 'l', 'l', 'i', '.', 'b', '\0', // "xvslli.b"
'x', 'v', 's', 'r', 'l', 'i', '.', 'b', '\0', // "xvsrli.b"
'x', 'v', 's', 'r', 'a', 'i', '.', 'b', '\0', // "xvsrai.b"
'x', 'v', 'r', 'o', 't', 'r', 'i', '.', 'b', '\0', // "xvrotri.b"
'x', 'v', 's', 'r', 'l', 'r', 'i', '.', 'b', '\0', // "xvsrlri.b"
'x', 'v', 's', 'r', 'a', 'r', 'i', '.', 'b', '\0', // "xvsrari.b"
'x', 'v', 's', 'l', 'l', 'w', 'i', 'l', '.', 'h', '.', 'b', '\0', // "xvsllwil.h.b"
'x', 'v', 's', 'l', 'l', 'w', 'i', 'l', '.', 'h', 'u', '.', 'b', 'u', '\0', // "xvsllwil.hu.bu"
'x', 'v', 's', 'r', 'l', 'n', 'i', '.', 'b', '.', 'h', '\0', // "xvsrlni.b.h"
'x', 'v', 's', 'r', 'l', 'r', 'n', 'i', '.', 'b', '.', 'h', '\0', // "xvsrlrni.b.h"
'x', 'v', 's', 's', 'r', 'l', 'n', 'i', '.', 'b', '.', 'h', '\0', // "xvssrlni.b.h"
'x', 'v', 's', 's', 'r', 'l', 'n', 'i', '.', 'b', 'u', '.', 'h', '\0', // "xvssrlni.bu.h"
'x', 'v', 's', 's', 'r', 'l', 'r', 'n', 'i', '.', 'b', '.', 'h', '\0', // "xvssrlrni.b.h"
'x', 'v', 's', 's', 'r', 'l', 'r', 'n', 'i', '.', 'b', 'u', '.', 'h', '\0', // "xvssrlrni.bu.h"
'x', 'v', 's', 'r', 'a', 'n', 'i', '.', 'b', '.', 'h', '\0', // "xvsrani.b.h"
'x', 'v', 's', 'r', 'a', 'r', 'n', 'i', '.', 'b', '.', 'h', '\0', // "xvsrarni.b.h"
'x', 'v', 's', 's', 'r', 'a', 'n', 'i', '.', 'b', '.', 'h', '\0', // "xvssrani.b.h"
'x', 'v', 's', 's', 'r', 'a', 'n', 'i', '.', 'b', 'u', '.', 'h', '\0', // "xvssrani.bu.h"
'x', 'v', 's', 's', 'r', 'a', 'r', 'n', 'i', '.', 'b', '.', 'h', '\0', // "xvssrarni.b.h"
'x', 'v', 's', 's', 'r', 'a', 'r', 'n', 'i', '.', 'b', 'u', '.', 'h', '\0', // "xvssrarni.bu.h"
'x', 'v', 'i', 'n', 's', 'v', 'e', '0', '.', 'w', '\0', // "xvinsve0.w"
'x', 'v', 'r', 'e', 'p', 'l', '1', '2', '8', 'v', 'e', 'i', '.', 'h', '\0', // "xvrepl128vei.h"
'x', 'v', 'p', 'i', 'c', 'k', 'v', 'e', '.', 'w', '\0', // "xvpickve.w"
'x', 'v', 'b', 'i', 't', 'c', 'l', 'r', 'i', '.', 'b', '\0', // "xvbitclri.b"
'x', 'v', 'b', 'i', 't', 's', 'e', 't', 'i', '.', 'b', '\0', // "xvbitseti.b"
'x', 'v', 'b', 'i', 't', 'r', 'e', 'v', 'i', '.', 'b', '\0', // "xvbitrevi.b"
'x', 'v', 'i', 'n', 's', 'g', 'r', '2', 'v', 'r', '.', 'w', '\0', // "xvinsgr2vr.w"
'x', 'v', 's', 'a', 't', '.', 'b', '\0', // "xvsat.b"
'x', 'v', 's', 'a', 't', '.', 'b', 'u', '\0', // "xvsat.bu"
'x', 'v', 's', 'l', 'l', 'i', '.', 'h', '\0', // "xvslli.h"
'x', 'v', 's', 'r', 'l', 'i', '.', 'h', '\0', // "xvsrli.h"
'x', 'v', 's', 'r', 'a', 'i', '.', 'h', '\0', // "xvsrai.h"
'x', 'v', 'r', 'o', 't', 'r', 'i', '.', 'h', '\0', // "xvrotri.h"
'x', 'v', 'r', 'e', 'p', 'l', '1', '2', '8', 'v', 'e', 'i', '.', 'b', '\0', // "xvrepl128vei.b"
'x', 'v', 's', 'r', 'l', 'r', 'i', '.', 'h', '\0', // "xvsrlri.h"
'x', 'v', 's', 'r', 'a', 'r', 'i', '.', 'h', '\0', // "xvsrari.h"
'x', 'v', 's', 'l', 'l', 'w', 'i', 'l', '.', 'w', '.', 'h', '\0', // "xvsllwil.w.h"
'x', 'v', 's', 'l', 'l', 'w', 'i', 'l', '.', 'w', 'u', '.', 'h', 'u', '\0', // "xvsllwil.wu.hu"
'x', 'v', 'b', 'i', 't', 'c', 'l', 'r', 'i', '.', 'h', '\0', // "xvbitclri.h"
'x', 'v', 'b', 'i', 't', 's', 'e', 't', 'i', '.', 'h', '\0', // "xvbitseti.h"
'x', 'v', 'b', 'i', 't', 'r', 'e', 'v', 'i', '.', 'h', '\0', // "xvbitrevi.h"
'x', 'v', 's', 'a', 't', '.', 'h', '\0', // "xvsat.h"
'x', 'v', 's', 'a', 't', '.', 'h', 'u', '\0', // "xvsat.hu"
'x', 'v', 's', 'l', 'e', 'i', '.', 'b', 'u', '\0', // "xvslei.bu"
'x', 'v', 's', 'l', 'e', 'i', '.', 'h', 'u', '\0', // "xvslei.hu"
'x', 'v', 's', 'l', 'e', 'i', '.', 'w', 'u', '\0', // "xvslei.wu"
'x', 'v', 's', 'l', 'e', 'i', '.', 'd', 'u', '\0', // "xvslei.du"
'x', 'v', 's', 'l', 't', 'i', '.', 'b', 'u', '\0', // "xvslti.bu"
'x', 'v', 's', 'l', 't', 'i', '.', 'h', 'u', '\0', // "xvslti.hu"
'x', 'v', 's', 'l', 't', 'i', '.', 'w', 'u', '\0', // "xvslti.wu"
'x', 'v', 's', 'l', 't', 'i', '.', 'd', 'u', '\0', // "xvslti.du"
'x', 'v', 'a', 'd', 'd', 'i', '.', 'b', 'u', '\0', // "xvaddi.bu"
'x', 'v', 'a', 'd', 'd', 'i', '.', 'h', 'u', '\0', // "xvaddi.hu"
'x', 'v', 'a', 'd', 'd', 'i', '.', 'w', 'u', '\0', // "xvaddi.wu"
'x', 'v', 'a', 'd', 'd', 'i', '.', 'd', 'u', '\0', // "xvaddi.du"
'x', 'v', 's', 'u', 'b', 'i', '.', 'b', 'u', '\0', // "xvsubi.bu"
'x', 'v', 's', 'u', 'b', 'i', '.', 'h', 'u', '\0', // "xvsubi.hu"
'x', 'v', 's', 'u', 'b', 'i', '.', 'w', 'u', '\0', // "xvsubi.wu"
'x', 'v', 's', 'u', 'b', 'i', '.', 'd', 'u', '\0', // "xvsubi.du"
'x', 'v', 'm', 'a', 'x', 'i', '.', 'b', 'u', '\0', // "xvmaxi.bu"
'x', 'v', 'm', 'a', 'x', 'i', '.', 'h', 'u', '\0', // "xvmaxi.hu"
'x', 'v', 'm', 'a', 'x', 'i', '.', 'w', 'u', '\0', // "xvmaxi.wu"
'x', 'v', 'm', 'a', 'x', 'i', '.', 'd', 'u', '\0', // "xvmaxi.du"
'x', 'v', 'm', 'i', 'n', 'i', '.', 'b', 'u', '\0', // "xvmini.bu"
'x', 'v', 'm', 'i', 'n', 'i', '.', 'h', 'u', '\0', // "xvmini.hu"
'x', 'v', 'm', 'i', 'n', 'i', '.', 'w', 'u', '\0', // "xvmini.wu"
'x', 'v', 'm', 'i', 'n', 'i', '.', 'd', 'u', '\0', // "xvmini.du"
'x', 'v', 'f', 'r', 's', 't', 'p', 'i', '.', 'b', '\0', // "xvfrstpi.b"
'x', 'v', 'f', 'r', 's', 't', 'p', 'i', '.', 'h', '\0', // "xvfrstpi.h"
'x', 'v', 's', 'l', 'l', 'i', '.', 'w', '\0', // "xvslli.w"
'x', 'v', 's', 'r', 'l', 'i', '.', 'w', '\0', // "xvsrli.w"
'x', 'v', 's', 'r', 'a', 'i', '.', 'w', '\0', // "xvsrai.w"
'x', 'v', 'b', 's', 'l', 'l', '.', 'v', '\0', // "xvbsll.v"
'x', 'v', 'b', 's', 'r', 'l', '.', 'v', '\0', // "xvbsrl.v"
'x', 'v', 's', 'r', 'l', 'r', 'i', '.', 'w', '\0', // "xvsrlri.w"
'x', 'v', 's', 'r', 'a', 'r', 'i', '.', 'w', '\0', // "xvsrari.w"
'x', 'v', 'r', 'o', 't', 'r', 'i', '.', 'w', '\0', // "xvrotri.w"
'x', 'v', 's', 'l', 'l', 'w', 'i', 'l', '.', 'd', '.', 'w', '\0', // "xvsllwil.d.w"
'x', 'v', 's', 'l', 'l', 'w', 'i', 'l', '.', 'd', 'u', '.', 'w', 'u', '\0', // "xvsllwil.du.wu"
'x', 'v', 's', 'r', 'l', 'n', 'i', '.', 'h', '.', 'w', '\0', // "xvsrlni.h.w"
'x', 'v', 's', 'r', 'l', 'r', 'n', 'i', '.', 'h', '.', 'w', '\0', // "xvsrlrni.h.w"
'x', 'v', 's', 's', 'r', 'l', 'n', 'i', '.', 'h', '.', 'w', '\0', // "xvssrlni.h.w"
'x', 'v', 's', 's', 'r', 'l', 'n', 'i', '.', 'h', 'u', '.', 'w', '\0', // "xvssrlni.hu.w"
'x', 'v', 's', 's', 'r', 'l', 'r', 'n', 'i', '.', 'h', '.', 'w', '\0', // "xvssrlrni.h.w"
'x', 'v', 's', 's', 'r', 'l', 'r', 'n', 'i', '.', 'h', 'u', '.', 'w', '\0', // "xvssrlrni.hu.w"
'x', 'v', 's', 'r', 'a', 'n', 'i', '.', 'h', '.', 'w', '\0', // "xvsrani.h.w"
'x', 'v', 's', 'r', 'a', 'r', 'n', 'i', '.', 'h', '.', 'w', '\0', // "xvsrarni.h.w"
'x', 'v', 's', 's', 'r', 'a', 'n', 'i', '.', 'h', '.', 'w', '\0', // "xvssrani.h.w"
'x', 'v', 's', 's', 'r', 'a', 'n', 'i', '.', 'h', 'u', '.', 'w', '\0', // "xvssrani.hu.w"
'x', 'v', 's', 's', 'r', 'a', 'r', 'n', 'i', '.', 'h', '.', 'w', '\0', // "xvssrarni.h.w"
'x', 'v', 's', 's', 'r', 'a', 'r', 'n', 'i', '.', 'h', 'u', '.', 'w', '\0', // "xvssrarni.hu.w"
'x', 'v', 'b', 'i', 't', 'c', 'l', 'r', 'i', '.', 'w', '\0', // "xvbitclri.w"
'x', 'v', 'b', 'i', 't', 's', 'e', 't', 'i', '.', 'w', '\0', // "xvbitseti.w"
'x', 'v', 'b', 'i', 't', 'r', 'e', 'v', 'i', '.', 'w', '\0', // "xvbitrevi.w"
'x', 'v', 's', 'a', 't', '.', 'w', '\0', // "xvsat.w"
'x', 'v', 's', 'a', 't', '.', 'w', 'u', '\0', // "xvsat.wu"
'x', 'v', 's', 'l', 'l', 'i', '.', 'd', '\0', // "xvslli.d"
'x', 'v', 's', 'r', 'l', 'i', '.', 'd', '\0', // "xvsrli.d"
'x', 'v', 's', 'r', 'a', 'i', '.', 'd', '\0', // "xvsrai.d"
'x', 'v', 'r', 'o', 't', 'r', 'i', '.', 'd', '\0', // "xvrotri.d"
'x', 'v', 's', 'r', 'l', 'r', 'i', '.', 'd', '\0', // "xvsrlri.d"
'x', 'v', 's', 'r', 'a', 'r', 'i', '.', 'd', '\0', // "xvsrari.d"
'x', 'v', 's', 'r', 'l', 'n', 'i', '.', 'w', '.', 'd', '\0', // "xvsrlni.w.d"
'x', 'v', 's', 'r', 'l', 'r', 'n', 'i', '.', 'w', '.', 'd', '\0', // "xvsrlrni.w.d"
'x', 'v', 's', 's', 'r', 'l', 'n', 'i', '.', 'w', '.', 'd', '\0', // "xvssrlni.w.d"
'x', 'v', 's', 's', 'r', 'l', 'n', 'i', '.', 'w', 'u', '.', 'd', '\0', // "xvssrlni.wu.d"
'x', 'v', 's', 's', 'r', 'l', 'r', 'n', 'i', '.', 'w', '.', 'd', '\0', // "xvssrlrni.w.d"
'x', 'v', 's', 's', 'r', 'l', 'r', 'n', 'i', '.', 'w', 'u', '.', 'd', '\0', // "xvssrlrni.wu.d"
'x', 'v', 's', 'r', 'a', 'n', 'i', '.', 'w', '.', 'd', '\0', // "xvsrani.w.d"
'x', 'v', 's', 'r', 'a', 'r', 'n', 'i', '.', 'w', '.', 'd', '\0', // "xvsrarni.w.d"
'x', 'v', 's', 's', 'r', 'a', 'n', 'i', '.', 'w', '.', 'd', '\0', // "xvssrani.w.d"
'x', 'v', 's', 's', 'r', 'a', 'n', 'i', '.', 'w', 'u', '.', 'd', '\0', // "xvssrani.wu.d"
'x', 'v', 's', 's', 'r', 'a', 'r', 'n', 'i', '.', 'w', '.', 'd', '\0', // "xvssrarni.w.d"
'x', 'v', 's', 's', 'r', 'a', 'r', 'n', 'i', '.', 'w', 'u', '.', 'd', '\0', // "xvssrarni.wu.d"
'x', 'v', 'b', 'i', 't', 'c', 'l', 'r', 'i', '.', 'd', '\0', // "xvbitclri.d"
'x', 'v', 'b', 'i', 't', 's', 'e', 't', 'i', '.', 'd', '\0', // "xvbitseti.d"
'x', 'v', 'b', 'i', 't', 'r', 'e', 'v', 'i', '.', 'd', '\0', // "xvbitrevi.d"
'x', 'v', 's', 'a', 't', '.', 'd', '\0', // "xvsat.d"
'x', 'v', 's', 'a', 't', '.', 'd', 'u', '\0', // "xvsat.du"
'x', 'v', 's', 'r', 'l', 'n', 'i', '.', 'd', '.', 'q', '\0', // "xvsrlni.d.q"
'x', 'v', 's', 'r', 'l', 'r', 'n', 'i', '.', 'd', '.', 'q', '\0', // "xvsrlrni.d.q"
'x', 'v', 's', 's', 'r', 'l', 'n', 'i', '.', 'd', '.', 'q', '\0', // "xvssrlni.d.q"
'x', 'v', 's', 's', 'r', 'l', 'n', 'i', '.', 'd', 'u', '.', 'q', '\0', // "xvssrlni.du.q"
'x', 'v', 's', 's', 'r', 'l', 'r', 'n', 'i', '.', 'd', '.', 'q', '\0', // "xvssrlrni.d.q"
'x', 'v', 's', 's', 'r', 'l', 'r', 'n', 'i', '.', 'd', 'u', '.', 'q', '\0', // "xvssrlrni.du.q"
'x', 'v', 's', 'r', 'a', 'n', 'i', '.', 'd', '.', 'q', '\0', // "xvsrani.d.q"
'x', 'v', 's', 'r', 'a', 'r', 'n', 'i', '.', 'd', '.', 'q', '\0', // "xvsrarni.d.q"
'x', 'v', 's', 's', 'r', 'a', 'n', 'i', '.', 'd', '.', 'q', '\0', // "xvssrani.d.q"
'x', 'v', 's', 's', 'r', 'a', 'n', 'i', '.', 'd', 'u', '.', 'q', '\0', // "xvssrani.du.q"
'x', 'v', 's', 's', 'r', 'a', 'r', 'n', 'i', '.', 'd', '.', 'q', '\0', // "xvssrarni.d.q"
'x', 'v', 's', 's', 'r', 'a', 'r', 'n', 'i', '.', 'd', 'u', '.', 'q', '\0', // "xvssrarni.du.q"
'x', 'v', 'e', 'x', 't', 'r', 'i', 'n', 's', '.', 'd', '\0', // "xvextrins.d"
'x', 'v', 'e', 'x', 't', 'r', 'i', 'n', 's', '.', 'w', '\0', // "xvextrins.w"
'x', 'v', 'e', 'x', 't', 'r', 'i', 'n', 's', '.', 'h', '\0', // "xvextrins.h"
'x', 'v', 'e', 'x', 't', 'r', 'i', 'n', 's', '.', 'b', '\0', // "xvextrins.b"
'x', 'v', 's', 'h', 'u', 'f', '4', 'i', '.', 'b', '\0', // "xvshuf4i.b"
'x', 'v', 's', 'h', 'u', 'f', '4', 'i', '.', 'h', '\0', // "xvshuf4i.h"
'x', 'v', 's', 'h', 'u', 'f', '4', 'i', '.', 'w', '\0', // "xvshuf4i.w"
'x', 'v', 's', 'h', 'u', 'f', '4', 'i', '.', 'd', '\0', // "xvshuf4i.d"
'x', 'v', 'b', 'i', 't', 's', 'e', 'l', 'i', '.', 'b', '\0', // "xvbitseli.b"
'x', 'v', 'a', 'n', 'd', 'i', '.', 'b', '\0', // "xvandi.b"
'x', 'v', 'o', 'r', 'i', '.', 'b', '\0', // "xvori.b"
'x', 'v', 'x', 'o', 'r', 'i', '.', 'b', '\0', // "xvxori.b"
'x', 'v', 'n', 'o', 'r', 'i', '.', 'b', '\0', // "xvnori.b"
'x', 'v', 'p', 'e', 'r', 'm', 'i', '.', 'w', '\0', // "xvpermi.w"
'x', 'v', 'p', 'e', 'r', 'm', 'i', '.', 'd', '\0', // "xvpermi.d"
'x', 'v', 'p', 'e', 'r', 'm', 'i', '.', 'q', '\0', // "xvpermi.q"
'x', 'v', 'l', 'd', 'i', '\0', // "xvldi"
'x', 'v', 's', 'e', 't', 'e', 'q', 'z', '.', 'v', '\0', // "xvseteqz.v"
'x', 'v', 's', 'e', 't', 'n', 'e', 'z', '.', 'v', '\0', // "xvsetnez.v"
'x', 'v', 's', 'e', 't', 'a', 'n', 'y', 'e', 'q', 'z', '.', 'b', '\0', // "xvsetanyeqz.b"
'x', 'v', 's', 'e', 't', 'a', 'n', 'y', 'e', 'q', 'z', '.', 'h', '\0', // "xvsetanyeqz.h"
'x', 'v', 's', 'e', 't', 'a', 'n', 'y', 'e', 'q', 'z', '.', 'w', '\0', // "xvsetanyeqz.w"
'x', 'v', 's', 'e', 't', 'a', 'n', 'y', 'e', 'q', 'z', '.', 'd', '\0', // "xvsetanyeqz.d"
'x', 'v', 's', 'e', 't', 'a', 'l', 'l', 'n', 'e', 'z', '.', 'b', '\0', // "xvsetallnez.b"
'x', 'v', 's', 'e', 't', 'a', 'l', 'l', 'n', 'e', 'z', '.', 'h', '\0', // "xvsetallnez.h"
'x', 'v', 's', 'e', 't', 'a', 'l', 'l', 'n', 'e', 'z', '.', 'w', '\0', // "xvsetallnez.w"
'x', 'v', 's', 'e', 't', 'a', 'l', 'l', 'n', 'e', 'z', '.', 'd', '\0', // "xvsetallnez.d"
'm', 'v', '\0', // "mv"
's', 'e', 'x', 't', '.', 'w', '\0', // "sext.w"
'l', 'u', 'i', '\0', // "lui"
'a', 'u', 'i', 'p', 'c', '\0', // "auipc"
'a', 'd', 'd', 'i', '\0', // "addi"
's', 'l', 't', 'i', 'u', '\0', // "sltiu"
's', 'l', 'l', 'i', '\0', // "slli"
's', 'r', 'l', 'i', '\0', // "srli"
's', 'r', 'a', 'i', '\0', // "srai"
's', 'l', 'l', '\0', // "sll"
's', 'r', 'l', '\0', // "srl"
's', 'r', 'a', '\0', // "sra"
'f', 'e', 'n', 'c', 'e', '\0', // "fence"
'f', 'e', 'n', 'c', 'e', '.', 'i', '\0', // "fence.i"
'c', 's', 'r', 'r', 'w', '\0', // "csrrw"
'c', 's', 'r', 'r', 's', '\0', // "csrrs"
'c', 's', 'r', 'r', 'c', '\0', // "csrrc"
'c', 's', 'r', 'r', 'w', 'i', '\0', // "csrrwi"
'c', 's', 'r', 'r', 's', 'i', '\0', // "csrrsi"
'c', 's', 'r', 'r', 'c', 'i', '\0', // "csrrci"
'e', 'c', 'a', 'l', 'l', '\0', // "ecall"
'e', 'b', 'r', 'e', 'a', 'k', '\0', // "ebreak"
'l', 'b', '\0', // "lb"
'l', 'h', '\0', // "lh"
'l', 'w', '\0', // "lw"
'l', 'b', 'u', '\0', // "lbu"
'l', 'h', 'u', '\0', // "lhu"
's', 'b', '\0', // "sb"
's', 'h', '\0', // "sh"
's', 'w', '\0', // "sw"
'j', 'a', 'l', '\0', // "jal"
'j', '\0', // "j"
'j', 'a', 'l', 'r', '\0', // "jalr"
'a', 'd', 'd', 'i', 'w', '\0', // "addiw"
's', 'l', 'l', 'i', 'w', '\0', // "slliw"
's', 'r', 'l', 'i', 'w', '\0', // "srliw"
's', 'r', 'a', 'i', 'w', '\0', // "sraiw"
's', 'l', 'l', 'w', '\0', // "sllw"
's', 'r', 'l', 'w', '\0', // "srlw"
's', 'r', 'a', 'w', '\0', // "sraw"
'l', 'w', 'u', '\0', // "lwu"
'l', 'd', '\0', // "ld"
's', 'd', '\0', // "sd"
'm', 'u', 'l', 'h', '\0', // "mulh"
'm', 'u', 'l', 'h', 's', 'u', '\0', // "mulhsu"
'm', 'u', 'l', 'h', 'u', '\0', // "mulhu"
'd', 'i', 'v', '\0', // "div"
'd', 'i', 'v', 'u', '\0', // "divu"
'r', 'e', 'm', '\0', // "rem"
'r', 'e', 'm', 'u', '\0', // "remu"
'm', 'u', 'l', 'w', '\0', // "mulw"
'd', 'i', 'v', 'w', '\0', // "divw"
'd', 'i', 'v', 'u', 'w', '\0', // "divuw"
'r', 'e', 'm', 'w', '\0', // "remw"
'r', 'e', 'm', 'u', 'w', '\0', // "remuw"
'f', 's', 'g', 'n', 'j', '.', 's', '\0', // "fsgnj.s"
'f', 's', 'g', 'n', 'j', 'n', '.', 's', '\0', // "fsgnjn.s"
'f', 's', 'g', 'n', 'j', 'x', '.', 's', '\0', // "fsgnjx.s"
'f', 'c', 'v', 't', '.', 'w', '.', 's', '\0', // "fcvt.w.s"
'f', 'c', 'v', 't', '.', 'w', 'u', '.', 's', '\0', // "fcvt.wu.s"
'f', 'm', 'v', '.', 'x', '.', 'w', '\0', // "fmv.x.w"
'f', 'e', 'q', '.', 's', '\0', // "feq.s"
'f', 'l', 't', '.', 's', '\0', // "flt.s"
'f', 'l', 'e', '.', 's', '\0', // "fle.s"
'f', 'c', 'v', 't', '.', 's', '.', 'w', '\0', // "fcvt.s.w"
'f', 'c', 'v', 't', '.', 's', '.', 'w', 'u', '\0', // "fcvt.s.wu"
'f', 'm', 'v', '.', 'w', '.', 'x', '\0', // "fmv.w.x"
'f', 's', 'g', 'n', 'j', '.', 'd', '\0', // "fsgnj.d"
'f', 's', 'g', 'n', 'j', 'n', '.', 'd', '\0', // "fsgnjn.d"
'f', 's', 'g', 'n', 'j', 'x', '.', 'd', '\0', // "fsgnjx.d"
'f', 'e', 'q', '.', 'd', '\0', // "feq.d"
'f', 'l', 't', '.', 'd', '\0', // "flt.d"
'f', 'l', 'e', '.', 'd', '\0', // "fle.d"
'f', 'c', 'v', 't', '.', 'w', '.', 'd', '\0', // "fcvt.w.d"
'f', 'c', 'v', 't', '.', 'w', 'u', '.', 'd', '\0', // "fcvt.wu.d"
'f', 'c', 'v', 't', '.', 'd', '.', 'w', '\0', // "fcvt.d.w"
'f', 'c', 'v', 't', '.', 'd', '.', 'w', 'u', '\0', // "fcvt.d.wu"
'f', 'l', 'w', '\0', // "flw"
'f', 's', 'w', '\0', // "fsw"
'f', 'l', 'd', '\0', // "fld"
'f', 's', 'd', '\0', // "fsd"
'f', 'c', 'v', 't', '.', 'l', '.', 's', '\0', // "fcvt.l.s"
'f', 'c', 'v', 't', '.', 'l', 'u', '.', 's', '\0', // "fcvt.lu.s"
'f', 'c', 'v', 't', '.', 's', '.', 'l', '\0', // "fcvt.s.l"
'f', 'c', 'v', 't', '.', 's', '.', 'l', 'u', '\0', // "fcvt.s.lu"
'f', 'c', 'v', 't', '.', 'l', '.', 'd', '\0', // "fcvt.l.d"
'f', 'c', 'v', 't', '.', 'l', 'u', '.', 'd', '\0', // "fcvt.lu.d"
'f', 'm', 'v', '.', 'x', '.', 'd', '\0', // "fmv.x.d"
'f', 'c', 'v', 't', '.', 'd', '.', 'l', '\0', // "fcvt.d.l"
'f', 'c', 'v', 't', '.', 'd', '.', 'l', 'u', '\0', // "fcvt.d.lu"
'f', 'm', 'v', '.', 'd', '.', 'x', '\0', // "fmv.d.x"
'l', 'r', '.', 'w', '\0', // "lr.w"
'l', 'r', '.', 'd', '\0', // "lr.d"
'a', 'm', 'o', 's', 'w', 'a', 'p', '.', 'w', '\0', // "amoswap.w"
'a', 'm', 'o', 's', 'w', 'a', 'p', '.', 'd', '\0', // "amoswap.d"
'a', 'm', 'o', 'a', 'd', 'd', '.', 'w', '\0', // "amoadd.w"
'a', 'm', 'o', 'a', 'd', 'd', '.', 'd', '\0', // "amoadd.d"
'a', 'm', 'o', 'x', 'o', 'r', '.', 'w', '\0', // "amoxor.w"
'a', 'm', 'o', 'x', 'o', 'r', '.', 'd', '\0', // "amoxor.d"
'a', 'm', 'o', 'a', 'n', 'd', '.', 'w', '\0', // "amoand.w"
'a', 'm', 'o', 'a', 'n', 'd', '.', 'd', '\0', // "amoand.d"
'a', 'm', 'o', 'o', 'r', '.', 'w', '\0', // "amoor.w"
'a', 'm', 'o', 'o', 'r', '.', 'd', '\0', // "amoor.d"
'a', 'm', 'o', 'm', 'i', 'n', '.', 'w', '\0', // "amomin.w"
'a', 'm', 'o', 'm', 'i', 'n', '.', 'd', '\0', // "amomin.d"
'a', 'm', 'o', 'm', 'a', 'x', '.', 'w', '\0', // "amomax.w"
'a', 'm', 'o', 'm', 'a', 'x', '.', 'd', '\0', // "amomax.d"
'a', 'm', 'o', 'm', 'i', 'n', 'u', '.', 'w', '\0', // "amominu.w"
'a', 'm', 'o', 'm', 'i', 'n', 'u', '.', 'd', '\0', // "amominu.d"
'a', 'm', 'o', 'm', 'a', 'x', 'u', '.', 'w', '\0', // "amomaxu.w"
'a', 'm', 'o', 'm', 'a', 'x', 'u', '.', 'd', '\0', // "amomaxu.d"
'c', 'l', 'z', 'w', '\0', // "clzw"
'c', 't', 'z', 'w', '\0', // "ctzw"
'c', 'p', 'o', 'p', '\0', // "cpop"
'c', 'p', 'o', 'p', 'w', '\0', // "cpopw"
's', 'e', 'x', 't', '.', 'b', '\0', // "sext.b"
's', 'e', 'x', 't', '.', 'h', '\0', // "sext.h"
'z', 'e', 'x', 't', '.', 'h', '\0', // "zext.h"
'r', 'e', 'v', '8', '\0', // "rev8"
'r', 'o', 'l', '\0', // "rol"
'r', 'o', 'l', 'w', '\0', // "rolw"
'r', 'o', 'r', 'w', '\0', // "rorw"
'x', 'n', 'o', 'r', '\0', // "xnor"
'm', 'i', 'n', '\0', // "min"
'm', 'i', 'n', 'u', '\0', // "minu"
'm', 'a', 'x', '\0', // "max"
'm', 'a', 'x', 'u', '\0', // "maxu"
'r', 'o', 'r', 'i', '\0', // "rori"
'r', 'o', 'r', 'i', 'w', '\0', // "roriw"
's', 'h', '1', 'a', 'd', 'd', '\0', // "sh1add"
's', 'h', '2', 'a', 'd', 'd', '\0', // "sh2add"
's', 'h', '3', 'a', 'd', 'd', '\0', // "sh3add"
'a', 'd', 'd', '.', 'u', 'w', '\0', // "add.uw"
's', 'h', '1', 'a', 'd', 'd', '_', 'u', 'w', '\0', // "sh1add_uw"
's', 'h', '2', 'a', 'd', 'd', '_', 'u', 'w', '\0', // "sh2add_uw"
's', 'h', '3', 'a', 'd', 'd', '_', 'u', 'w', '\0', // "sh3add_uw"
's', 'l', 'l', 'i', '_', 'u', 'w', '\0', // "slli_uw"
'b', 's', 'e', 't', '\0', // "bset"
'b', 'c', 'l', 'r', '\0', // "bclr"
'b', 'i', 'n', 'v', '\0', // "binv"
'b', 's', 'e', 't', 'i', '\0', // "bseti"
'b', 'c', 'l', 'r', 'i', '\0', // "bclri"
'b', 'e', 'x', 't', 'i', '\0', // "bexti"
'b', 'i', 'n', 'v', 'i', '\0', // "binvi"
'c', 'z', 'e', 'r', 'o', '.', 'e', 'q', 'z', '\0', // "czero.eqz"
'c', 'z', 'e', 'r', 'o', '.', 'n', 'e', 'z', '\0', // "czero.nez"
'c', '.', 'm', 'v', '\0', // "c.mv"
'c', '.', 'a', 'd', 'd', '\0', // "c.add"
'c', '.', 'a', 'n', 'd', '\0', // "c.and"
'c', '.', 'o', 'r', '\0', // "c.or"
'c', '.', 'x', 'o', 'r', '\0', // "c.xor"
'c', '.', 's', 'u', 'b', '\0', // "c.sub"
'c', '.', 'a', 'd', 'd', 'w', '\0', // "c.addw"
'c', '.', 's', 'u', 'b', 'w', '\0', // "c.subw"
'u', 'n', 'r', 'e', 'a', 'c', 'h', 'a', 'b', 'l', 'e', '\0', // "unreachable"
'l', 'a', 'b', 'e', 'l', '\0', // "label"
'c', 'a', 't', 'c', 'h', '_', 'r', 'e', 'f', '\0', // "catch_ref"
'l', 'o', 'c', 'a', 'l', '.', 'c', 'n', 't', '\0', // "local.cnt"
'l', 'o', 'c', 'a', 'l', '\0', // "local"
'c', 'o', 'd', 'e', '.', 's', 'i', 'z', 'e', '\0', // "code.size"
'b', 'l', 'o', 'c', 'k', '\0', // "block"
'l', 'o', 'o', 'p', '\0', // "loop"
'i', 'f', '\0', // "if"
'e', 'l', 's', 'e', '\0', // "else"
't', 'h', 'r', 'o', 'w', '_', 'r', 'e', 'f', '\0', // "throw_ref"
'e', 'n', 'd', '\0', // "end"
'b', 'r', '_', 'i', 'f', '\0', // "br_if"
'b', 'r', '_', 't', 'a', 'b', 'l', 'e', '\0', // "br_table"
'r', 'e', 't', 'u', 'r', 'n', '\0', // "return"
'c', 'a', 'l', 'l', '\0', // "call"
'c', 'a', 'l', 'l', '_', 'i', 'n', 'd', 'i', 'r', 'e', 'c', 't', '\0', // "call_indirect"
'r', 'e', 't', 'u', 'r', 'n', '_', 'c', 'a', 'l', 'l', '\0', // "return_call"
'r', 'e', 't', 'u', 'r', 'n', '_', 'c', 'a', 'l', 'l', '_', 'i', 'n', 'd', 'i', 'r', 'e', 'c', 't', '\0', // "return_call_indirect"
'c', 'a', 'l', 'l', '.', 'f', 'u', 'n', 'c', 'l', 'e', 't', '\0', // "call.funclet"
'd', 'r', 'o', 'p', '\0', // "drop"
't', 'r', 'y', '_', 't', 'a', 'b', 'l', 'e', '\0', // "try_table"
'l', 'o', 'c', 'a', 'l', '.', 'g', 'e', 't', '\0', // "local.get"
'l', 'o', 'c', 'a', 'l', '.', 's', 'e', 't', '\0', // "local.set"
'l', 'o', 'c', 'a', 'l', '.', 't', 'e', 'e', '\0', // "local.tee"
'g', 'l', 'o', 'b', 'a', 'l', '.', 'g', 'e', 't', '\0', // "global.get"
'g', 'l', 'o', 'b', 'a', 'l', '.', 's', 'e', 't', '\0', // "global.set"
'i', '3', '2', '.', 'l', 'o', 'a', 'd', '\0', // "i32.load"
'i', '6', '4', '.', 'l', 'o', 'a', 'd', '\0', // "i64.load"
'f', '3', '2', '.', 'l', 'o', 'a', 'd', '\0', // "f32.load"
'f', '6', '4', '.', 'l', 'o', 'a', 'd', '\0', // "f64.load"
'i', '3', '2', '.', 'l', 'o', 'a', 'd', '8', '_', 's', '\0', // "i32.load8_s"
'i', '3', '2', '.', 'l', 'o', 'a', 'd', '8', '_', 'u', '\0', // "i32.load8_u"
'i', '3', '2', '.', 'l', 'o', 'a', 'd', '1', '6', '_', 's', '\0', // "i32.load16_s"
'i', '3', '2', '.', 'l', 'o', 'a', 'd', '1', '6', '_', 'u', '\0', // "i32.load16_u"
'i', '6', '4', '.', 'l', 'o', 'a', 'd', '8', '_', 's', '\0', // "i64.load8_s"
'i', '6', '4', '.', 'l', 'o', 'a', 'd', '8', '_', 'u', '\0', // "i64.load8_u"
'i', '6', '4', '.', 'l', 'o', 'a', 'd', '1', '6', '_', 's', '\0', // "i64.load16_s"
'i', '6', '4', '.', 'l', 'o', 'a', 'd', '1', '6', '_', 'u', '\0', // "i64.load16_u"
'i', '6', '4', '.', 'l', 'o', 'a', 'd', '3', '2', '_', 's', '\0', // "i64.load32_s"
'i', '6', '4', '.', 'l', 'o', 'a', 'd', '3', '2', '_', 'u', '\0', // "i64.load32_u"
'i', '3', '2', '.', 's', 't', 'o', 'r', 'e', '\0', // "i32.store"
'i', '6', '4', '.', 's', 't', 'o', 'r', 'e', '\0', // "i64.store"
'f', '3', '2', '.', 's', 't', 'o', 'r', 'e', '\0', // "f32.store"
'f', '6', '4', '.', 's', 't', 'o', 'r', 'e', '\0', // "f64.store"
'i', '3', '2', '.', 's', 't', 'o', 'r', 'e', '8', '\0', // "i32.store8"
'i', '3', '2', '.', 's', 't', 'o', 'r', 'e', '1', '6', '\0', // "i32.store16"
'i', '3', '2', '.', 'c', 'o', 'n', 's', 't', '\0', // "i32.const"
'i', '3', '2', '.', 'c', 'o', 'n', 's', 't', '_', 'a', 'd', 'd', 'r', 'e', 's', 's', '\0', // "i32.const_address"
'i', '3', '2', '.', 'c', 'o', 'n', 's', 't', '_', 'f', 'u', 'n', 'c', 'p', 't', 'r', '\0', // "i32.const_funcptr"
'i', '3', '2', '.', 'c', 'o', 'n', 's', 't', '_', 'f', 'u', 'n', 'c', 'l', 'e', 't', 'p', 't', 'r', '\0', // "i32.const_funcletptr"
'i', '3', '2', '.', 'c', 'o', 'n', 's', 't', '_', 'f', 'r', 'a', 'm', 'e', 'i', 'd', 'e', 'n', 't', 'i', 't', 'y', '\0', // "i32.const_frameidentity"
'i', '3', '2', '.', 'c', 'o', 'n', 's', 't', '_', 'd', 'a', 't', 'a', 'o', 'f', 'f', 's', '\0', // "i32.const_dataoffs"
'i', '6', '4', '.', 'c', 'o', 'n', 's', 't', '\0', // "i64.const"
'f', '3', '2', '.', 'c', 'o', 'n', 's', 't', '\0', // "f32.const"
'f', '6', '4', '.', 'c', 'o', 'n', 's', 't', '\0', // "f64.const"
'i', '3', '2', '.', 'e', 'q', 'z', '\0', // "i32.eqz"
'i', '3', '2', '.', 'e', 'q', '\0', // "i32.eq"
'i', '3', '2', '.', 'n', 'e', '\0', // "i32.ne"
'i', '3', '2', '.', 'l', 't', '_', 's', '\0', // "i32.lt_s"
'i', '3', '2', '.', 'l', 't', '_', 'u', '\0', // "i32.lt_u"
'i', '3', '2', '.', 'g', 't', '_', 's', '\0', // "i32.gt_s"
'i', '3', '2', '.', 'g', 't', '_', 'u', '\0', // "i32.gt_u"
'i', '3', '2', '.', 'l', 'e', '_', 's', '\0', // "i32.le_s"
'i', '3', '2', '.', 'l', 'e', '_', 'u', '\0', // "i32.le_u"
'i', '3', '2', '.', 'g', 'e', '_', 's', '\0', // "i32.ge_s"
'i', '3', '2', '.', 'g', 'e', '_', 'u', '\0', // "i32.ge_u"
'i', '6', '4', '.', 'e', 'q', 'z', '\0', // "i64.eqz"
'i', '6', '4', '.', 'e', 'q', '\0', // "i64.eq"
'i', '6', '4', '.', 'n', 'e', '\0', // "i64.ne"
'i', '6', '4', '.', 'l', 't', '_', 's', '\0', // "i64.lt_s"
'i', '6', '4', '.', 'l', 't', '_', 'u', '\0', // "i64.lt_u"
'i', '6', '4', '.', 'g', 't', '_', 's', '\0', // "i64.gt_s"
'i', '6', '4', '.', 'g', 't', '_', 'u', '\0', // "i64.gt_u"
'i', '6', '4', '.', 'l', 'e', '_', 's', '\0', // "i64.le_s"
'i', '6', '4', '.', 'l', 'e', '_', 'u', '\0', // "i64.le_u"
'i', '6', '4', '.', 'g', 'e', '_', 's', '\0', // "i64.ge_s"
'i', '6', '4', '.', 'g', 'e', '_', 'u', '\0', // "i64.ge_u"
'f', '3', '2', '.', 'e', 'q', '\0', // "f32.eq"
'f', '3', '2', '.', 'n', 'e', '\0', // "f32.ne"
'f', '3', '2', '.', 'l', 't', '\0', // "f32.lt"
'f', '3', '2', '.', 'g', 't', '\0', // "f32.gt"
'f', '3', '2', '.', 'l', 'e', '\0', // "f32.le"
'f', '3', '2', '.', 'g', 'e', '\0', // "f32.ge"
'f', '6', '4', '.', 'e', 'q', '\0', // "f64.eq"
'f', '6', '4', '.', 'n', 'e', '\0', // "f64.ne"
'f', '6', '4', '.', 'l', 't', '\0', // "f64.lt"
'f', '6', '4', '.', 'g', 't', '\0', // "f64.gt"
'f', '6', '4', '.', 'l', 'e', '\0', // "f64.le"
'f', '6', '4', '.', 'g', 'e', '\0', // "f64.ge"
'i', '3', '2', '.', 'c', 'l', 'z', '\0', // "i32.clz"
'i', '3', '2', '.', 'c', 't', 'z', '\0', // "i32.ctz"
'i', '3', '2', '.', 'p', 'o', 'p', 'c', 'n', 't', '\0', // "i32.popcnt"
'i', '3', '2', '.', 'a', 'd', 'd', '\0', // "i32.add"
'i', '3', '2', '.', 's', 'u', 'b', '\0', // "i32.sub"
'i', '3', '2', '.', 'm', 'u', 'l', '\0', // "i32.mul"
'i', '3', '2', '.', 'd', 'i', 'v', '_', 's', '\0', // "i32.div_s"
'i', '3', '2', '.', 'd', 'i', 'v', '_', 'u', '\0', // "i32.div_u"
'i', '3', '2', '.', 'r', 'e', 'm', '_', 's', '\0', // "i32.rem_s"
'i', '3', '2', '.', 'r', 'e', 'm', '_', 'u', '\0', // "i32.rem_u"
'i', '3', '2', '.', 'a', 'n', 'd', '\0', // "i32.and"
'i', '3', '2', '.', 'o', 'r', '\0', // "i32.or"
'i', '3', '2', '.', 'x', 'o', 'r', '\0', // "i32.xor"
'i', '3', '2', '.', 's', 'h', 'l', '\0', // "i32.shl"
'i', '3', '2', '.', 's', 'h', 'r', '_', 's', '\0', // "i32.shr_s"
'i', '3', '2', '.', 's', 'h', 'r', '_', 'u', '\0', // "i32.shr_u"
'i', '3', '2', '.', 'r', 'o', 't', 'l', '\0', // "i32.rotl"
'i', '3', '2', '.', 'r', 'o', 't', 'r', '\0', // "i32.rotr"
'i', '6', '4', '.', 'c', 'l', 'z', '\0', // "i64.clz"
'i', '6', '4', '.', 'c', 't', 'z', '\0', // "i64.ctz"
'i', '6', '4', '.', 'p', 'o', 'p', 'c', 'n', 't', '\0', // "i64.popcnt"
'i', '6', '4', '.', 'a', 'd', 'd', '\0', // "i64.add"
'i', '6', '4', '.', 's', 'u', 'b', '\0', // "i64.sub"
'i', '6', '4', '.', 'm', 'u', 'l', '\0', // "i64.mul"
'i', '6', '4', '.', 'd', 'i', 'v', '_', 's', '\0', // "i64.div_s"
'i', '6', '4', '.', 'd', 'i', 'v', '_', 'u', '\0', // "i64.div_u"
'i', '6', '4', '.', 'r', 'e', 'm', '_', 's', '\0', // "i64.rem_s"
'i', '6', '4', '.', 'r', 'e', 'm', '_', 'u', '\0', // "i64.rem_u"
'i', '6', '4', '.', 'a', 'n', 'd', '\0', // "i64.and"
'i', '6', '4', '.', 'o', 'r', '\0', // "i64.or"
'i', '6', '4', '.', 'x', 'o', 'r', '\0', // "i64.xor"
'i', '6', '4', '.', 's', 'h', 'l', '\0', // "i64.shl"
'i', '6', '4', '.', 's', 'h', 'r', '_', 's', '\0', // "i64.shr_s"
'i', '6', '4', '.', 's', 'h', 'r', '_', 'u', '\0', // "i64.shr_u"
'i', '6', '4', '.', 'r', 'o', 't', 'l', '\0', // "i64.rotl"
'i', '6', '4', '.', 'r', 'o', 't', 'r', '\0', // "i64.rotr"
'f', '3', '2', '.', 'a', 'b', 's', '\0', // "f32.abs"
'f', '3', '2', '.', 'n', 'e', 'g', '\0', // "f32.neg"
'f', '3', '2', '.', 'c', 'e', 'i', 'l', '\0', // "f32.ceil"
'f', '3', '2', '.', 'f', 'l', 'o', 'o', 'r', '\0', // "f32.floor"
'f', '3', '2', '.', 't', 'r', 'u', 'n', 'c', '\0', // "f32.trunc"
'f', '3', '2', '.', 'n', 'e', 'a', 'r', 'e', 's', 't', '\0', // "f32.nearest"
'f', '3', '2', '.', 's', 'q', 'r', 't', '\0', // "f32.sqrt"
'f', '3', '2', '.', 'a', 'd', 'd', '\0', // "f32.add"
'f', '3', '2', '.', 's', 'u', 'b', '\0', // "f32.sub"
'f', '3', '2', '.', 'm', 'u', 'l', '\0', // "f32.mul"
'f', '3', '2', '.', 'd', 'i', 'v', '\0', // "f32.div"
'f', '3', '2', '.', 'm', 'i', 'n', '\0', // "f32.min"
'f', '3', '2', '.', 'm', 'a', 'x', '\0', // "f32.max"
'f', '3', '2', '.', 'c', 'o', 'p', 'y', 's', 'i', 'g', 'n', '\0', // "f32.copysign"
'f', '6', '4', '.', 'a', 'b', 's', '\0', // "f64.abs"
'f', '6', '4', '.', 'n', 'e', 'g', '\0', // "f64.neg"
'f', '6', '4', '.', 'c', 'e', 'i', 'l', '\0', // "f64.ceil"
'f', '6', '4', '.', 'f', 'l', 'o', 'o', 'r', '\0', // "f64.floor"
'f', '6', '4', '.', 't', 'r', 'u', 'n', 'c', '\0', // "f64.trunc"
'f', '6', '4', '.', 'n', 'e', 'a', 'r', 'e', 's', 't', '\0', // "f64.nearest"
'f', '6', '4', '.', 's', 'q', 'r', 't', '\0', // "f64.sqrt"
'f', '6', '4', '.', 'a', 'd', 'd', '\0', // "f64.add"
'f', '6', '4', '.', 's', 'u', 'b', '\0', // "f64.sub"
'f', '6', '4', '.', 'm', 'u', 'l', '\0', // "f64.mul"
'f', '6', '4', '.', 'd', 'i', 'v', '\0', // "f64.div"
'f', '6', '4', '.', 'm', 'i', 'n', '\0', // "f64.min"
'f', '6', '4', '.', 'm', 'a', 'x', '\0', // "f64.max"
'f', '6', '4', '.', 'c', 'o', 'p', 'y', 's', 'i', 'g', 'n', '\0', // "f64.copysign"
'i', '3', '2', '.', 'w', 'r', 'a', 'p', '_', 'i', '6', '4', '\0', // "i32.wrap_i64"
'i', '3', '2', '.', 't', 'r', 'u', 'n', 'c', '_', 's', '_', 'f', '3', '2', '\0', // "i32.trunc_s_f32"
'i', '3', '2', '.', 't', 'r', 'u', 'n', 'c', '_', 'u', '_', 'f', '3', '2', '\0', // "i32.trunc_u_f32"
'i', '3', '2', '.', 't', 'r', 'u', 'n', 'c', '_', 's', '_', 'f', '6', '4', '\0', // "i32.trunc_s_f64"
'i', '3', '2', '.', 't', 'r', 'u', 'n', 'c', '_', 'u', '_', 'f', '6', '4', '\0', // "i32.trunc_u_f64"
'i', '6', '4', '.', 'e', 'x', 't', 'e', 'n', 'd', '_', 's', '_', 'i', '3', '2', '\0', // "i64.extend_s_i32"
'i', '6', '4', '.', 'e', 'x', 't', 'e', 'n', 'd', '_', 'u', '_', 'i', '3', '2', '\0', // "i64.extend_u_i32"
'i', '6', '4', '.', 't', 'r', 'u', 'n', 'c', '_', 's', '_', 'f', '3', '2', '\0', // "i64.trunc_s_f32"
'i', '6', '4', '.', 't', 'r', 'u', 'n', 'c', '_', 'u', '_', 'f', '3', '2', '\0', // "i64.trunc_u_f32"
'i', '6', '4', '.', 't', 'r', 'u', 'n', 'c', '_', 's', '_', 'f', '6', '4', '\0', // "i64.trunc_s_f64"
'i', '6', '4', '.', 't', 'r', 'u', 'n', 'c', '_', 'u', '_', 'f', '6', '4', '\0', // "i64.trunc_u_f64"
'f', '3', '2', '.', 'c', 'o', 'n', 'v', 'e', 'r', 't', '_', 's', '_', 'i', '3', '2', '\0', // "f32.convert_s_i32"
'f', '3', '2', '.', 'c', 'o', 'n', 'v', 'e', 'r', 't', '_', 'u', '_', 'i', '3', '2', '\0', // "f32.convert_u_i32"
'f', '3', '2', '.', 'c', 'o', 'n', 'v', 'e', 'r', 't', '_', 's', '_', 'i', '6', '4', '\0', // "f32.convert_s_i64"
'f', '3', '2', '.', 'c', 'o', 'n', 'v', 'e', 'r', 't', '_', 'u', '_', 'i', '6', '4', '\0', // "f32.convert_u_i64"
'f', '3', '2', '.', 'd', 'e', 'm', 'o', 't', 'e', '_', 'f', '6', '4', '\0', // "f32.demote_f64"
'f', '6', '4', '.', 'c', 'o', 'n', 'v', 'e', 'r', 't', '_', 's', '_', 'i', '3', '2', '\0', // "f64.convert_s_i32"
'f', '6', '4', '.', 'c', 'o', 'n', 'v', 'e', 'r', 't', '_', 'u', '_', 'i', '3', '2', '\0', // "f64.convert_u_i32"
'f', '6', '4', '.', 'c', 'o', 'n', 'v', 'e', 'r', 't', '_', 's', '_', 'i', '6', '4', '\0', // "f64.convert_s_i64"
'f', '6', '4', '.', 'c', 'o', 'n', 'v', 'e', 'r', 't', '_', 'u', '_', 'i', '6', '4', '\0', // "f64.convert_u_i64"
'f', '6', '4', '.', 'p', 'r', 'o', 'm', 'o', 't', 'e', '_', 'f', '3', '2', '\0', // "f64.promote_f32"
'i', '3', '2', '.', 'r', 'e', 'i', 'n', 't', 'e', 'r', 'p', 'r', 'e', 't', '_', 'f', '3', '2', '\0', // "i32.reinterpret_f32"
'i', '6', '4', '.', 'r', 'e', 'i', 'n', 't', 'e', 'r', 'p', 'r', 'e', 't', '_', 'f', '6', '4', '\0', // "i64.reinterpret_f64"
'f', '3', '2', '.', 'r', 'e', 'i', 'n', 't', 'e', 'r', 'p', 'r', 'e', 't', '_', 'i', '3', '2', '\0', // "f32.reinterpret_i32"
'f', '6', '4', '.', 'r', 'e', 'i', 'n', 't', 'e', 'r', 'p', 'r', 'e', 't', '_', 'i', '6', '4', '\0', // "f64.reinterpret_i64"
'i', '3', '2', '.', 'e', 'x', 't', 'e', 'n', 'd', '8', '_', 's', '\0', // "i32.extend8_s"
'i', '3', '2', '.', 'e', 'x', 't', 'e', 'n', 'd', '1', '6', '_', 's', '\0', // "i32.extend16_s"
'i', '6', '4', '.', 'e', 'x', 't', 'e', 'n', 'd', '8', '_', 's', '\0', // "i64.extend8_s"
'i', '6', '4', '.', 'e', 'x', 't', 'e', 'n', 'd', '1', '6', '_', 's', '\0', // "i64.extend16_s"
'i', '6', '4', '.', 'e', 'x', 't', 'e', 'n', 'd', '3', '2', '_', 's', '\0', // "i64.extend32_s"
'i', '3', '2', '.', 't', 'r', 'u', 'n', 'c', '_', 's', 'a', 't', '_', 'f', '3', '2', '_', 's', '\0', // "i32.trunc_sat_f32_s"
'i', '3', '2', '.', 't', 'r', 'u', 'n', 'c', '_', 's', 'a', 't', '_', 'f', '3', '2', '_', 'u', '\0', // "i32.trunc_sat_f32_u"
'i', '3', '2', '.', 't', 'r', 'u', 'n', 'c', '_', 's', 'a', 't', '_', 'f', '6', '4', '_', 's', '\0', // "i32.trunc_sat_f64_s"
'i', '3', '2', '.', 't', 'r', 'u', 'n', 'c', '_', 's', 'a', 't', '_', 'f', '6', '4', '_', 'u', '\0', // "i32.trunc_sat_f64_u"
'i', '6', '4', '.', 't', 'r', 'u', 'n', 'c', '_', 's', 'a', 't', '_', 'f', '3', '2', '_', 's', '\0', // "i64.trunc_sat_f32_s"
'i', '6', '4', '.', 't', 'r', 'u', 'n', 'c', '_', 's', 'a', 't', '_', 'f', '3', '2', '_', 'u', '\0', // "i64.trunc_sat_f32_u"
'i', '6', '4', '.', 't', 'r', 'u', 'n', 'c', '_', 's', 'a', 't', '_', 'f', '6', '4', '_', 's', '\0', // "i64.trunc_sat_f64_s"
'i', '6', '4', '.', 't', 'r', 'u', 'n', 'c', '_', 's', 'a', 't', '_', 'f', '6', '4', '_', 'u', '\0', // "i64.trunc_sat_f64_u"
'm', 'e', 'm', 'o', 'r', 'y', '.', 'c', 'o', 'p', 'y', '\0', // "memory.copy"
'm', 'e', 'm', 'o', 'r', 'y', '.', 'f', 'i', 'l', 'l', '\0', // "memory.fill"
'v', '1', '2', '8', '.', 'l', 'o', 'a', 'd', '\0', // "v128.load"
'v', '1', '2', '8', '.', 'l', 'o', 'a', 'd', '8', 'x', '8', '_', 's', '\0', // "v128.load8x8_s"
'v', '1', '2', '8', '.', 'l', 'o', 'a', 'd', '8', 'x', '8', '_', 'u', '\0', // "v128.load8x8_u"
'v', '1', '2', '8', '.', 'l', 'o', 'a', 'd', '1', '6', 'x', '4', '_', 's', '\0', // "v128.load16x4_s"
'v', '1', '2', '8', '.', 'l', 'o', 'a', 'd', '1', '6', 'x', '4', '_', 'u', '\0', // "v128.load16x4_u"
'v', '1', '2', '8', '.', 'l', 'o', 'a', 'd', '3', '2', 'x', '2', '_', 's', '\0', // "v128.load32x2_s"
'v', '1', '2', '8', '.', 'l', 'o', 'a', 'd', '3', '2', 'x', '2', '_', 'u', '\0', // "v128.load32x2_u"
'v', '1', '2', '8', '.', 'l', 'o', 'a', 'd', '8', '_', 's', 'p', 'l', 'a', 't', '\0', // "v128.load8_splat"
'v', '1', '2', '8', '.', 'l', 'o', 'a', 'd', '1', '6', '_', 's', 'p', 'l', 'a', 't', '\0', // "v128.load16_splat"
'v', '1', '2', '8', '.', 'l', 'o', 'a', 'd', '3', '2', '_', 's', 'p', 'l', 'a', 't', '\0', // "v128.load32_splat"
'v', '1', '2', '8', '.', 'l', 'o', 'a', 'd', '6', '4', '_', 's', 'p', 'l', 'a', 't', '\0', // "v128.load64_splat"
'v', '1', '2', '8', '.', 's', 't', 'o', 'r', 'e', '\0', // "v128.store"
'v', '1', '2', '8', '.', 'c', 'o', 'n', 's', 't', '\0', // "v128.const"
'i', '8', 'x', '1', '6', '.', 's', 'h', 'u', 'f', 'f', 'l', 'e', '\0', // "i8x16.shuffle"
'i', '8', 'x', '1', '6', '.', 's', 'w', 'i', 'z', 'z', 'l', 'e', '\0', // "i8x16.swizzle"
'i', '8', 'x', '1', '6', '.', 's', 'p', 'l', 'a', 't', '\0', // "i8x16.splat"
'i', '1', '6', 'x', '8', '.', 's', 'p', 'l', 'a', 't', '\0', // "i16x8.splat"
'i', '3', '2', 'x', '4', '.', 's', 'p', 'l', 'a', 't', '\0', // "i32x4.splat"
'i', '6', '4', 'x', '2', '.', 's', 'p', 'l', 'a', 't', '\0', // "i64x2.splat"
'f', '3', '2', 'x', '4', '.', 's', 'p', 'l', 'a', 't', '\0', // "f32x4.splat"
'f', '6', '4', 'x', '2', '.', 's', 'p', 'l', 'a', 't', '\0', // "f64x2.splat"
'i', '8', 'x', '1', '6', '.', 'e', 'x', 't', 'r', 'a', 'c', 't', '_', 'l', 'a', 'n', 'e', '_', 's', '\0', // "i8x16.extract_lane_s"
'i', '8', 'x', '1', '6', '.', 'e', 'x', 't', 'r', 'a', 'c', 't', '_', 'l', 'a', 'n', 'e', '_', 'u', '\0', // "i8x16.extract_lane_u"
'i', '8', 'x', '1', '6', '.', 'r', 'e', 'p', 'l', 'a', 'c', 'e', '_', 'l', 'a', 'n', 'e', '\0', // "i8x16.replace_lane"
'i', '1', '6', 'x', '8', '.', 'e', 'x', 't', 'r', 'a', 'c', 't', '_', 'l', 'a', 'n', 'e', '_', 's', '\0', // "i16x8.extract_lane_s"
'i', '1', '6', 'x', '8', '.', 'e', 'x', 't', 'r', 'a', 'c', 't', '_', 'l', 'a', 'n', 'e', '_', 'u', '\0', // "i16x8.extract_lane_u"
'i', '1', '6', 'x', '8', '.', 'r', 'e', 'p', 'l', 'a', 'c', 'e', '_', 'l', 'a', 'n', 'e', '\0', // "i16x8.replace_lane"
'i', '3', '2', 'x', '4', '.', 'e', 'x', 't', 'r', 'a', 'c', 't', '_', 'l', 'a', 'n', 'e', '\0', // "i32x4.extract_lane"
'i', '3', '2', 'x', '4', '.', 'r', 'e', 'p', 'l', 'a', 'c', 'e', '_', 'l', 'a', 'n', 'e', '\0', // "i32x4.replace_lane"
'i', '6', '4', 'x', '2', '.', 'e', 'x', 't', 'r', 'a', 'c', 't', '_', 'l', 'a', 'n', 'e', '\0', // "i64x2.extract_lane"
'i', '6', '4', 'x', '2', '.', 'r', 'e', 'p', 'l', 'a', 'c', 'e', '_', 'l', 'a', 'n', 'e', '\0', // "i64x2.replace_lane"
'f', '3', '2', 'x', '4', '.', 'e', 'x', 't', 'r', 'a', 'c', 't', '_', 'l', 'a', 'n', 'e', '\0', // "f32x4.extract_lane"
'f', '3', '2', 'x', '4', '.', 'r', 'e', 'p', 'l', 'a', 'c', 'e', '_', 'l', 'a', 'n', 'e', '\0', // "f32x4.replace_lane"
'f', '6', '4', 'x', '2', '.', 'e', 'x', 't', 'r', 'a', 'c', 't', '_', 'l', 'a', 'n', 'e', '\0', // "f64x2.extract_lane"
'f', '6', '4', 'x', '2', '.', 'r', 'e', 'p', 'l', 'a', 'c', 'e', '_', 'l', 'a', 'n', 'e', '\0', // "f64x2.replace_lane"
'i', '8', 'x', '1', '6', '.', 'e', 'q', '\0', // "i8x16.eq"
'i', '8', 'x', '1', '6', '.', 'n', 'e', '\0', // "i8x16.ne"
'i', '8', 'x', '1', '6', '.', 'l', 't', '_', 's', '\0', // "i8x16.lt_s"
'i', '8', 'x', '1', '6', '.', 'l', 't', '_', 'u', '\0', // "i8x16.lt_u"
'i', '8', 'x', '1', '6', '.', 'g', 't', '_', 's', '\0', // "i8x16.gt_s"
'i', '8', 'x', '1', '6', '.', 'g', 't', '_', 'u', '\0', // "i8x16.gt_u"
'i', '8', 'x', '1', '6', '.', 'l', 'e', '_', 's', '\0', // "i8x16.le_s"
'i', '8', 'x', '1', '6', '.', 'l', 'e', '_', 'u', '\0', // "i8x16.le_u"
'i', '8', 'x', '1', '6', '.', 'g', 'e', '_', 's', '\0', // "i8x16.ge_s"
'i', '8', 'x', '1', '6', '.', 'g', 'e', '_', 'u', '\0', // "i8x16.ge_u"
'i', '1', '6', 'x', '8', '.', 'e', 'q', '\0', // "i16x8.eq"
'i', '1', '6', 'x', '8', '.', 'n', 'e', '\0', // "i16x8.ne"
'i', '1', '6', 'x', '8', '.', 'l', 't', '_', 's', '\0', // "i16x8.lt_s"
'i', '1', '6', 'x', '8', '.', 'l', 't', '_', 'u', '\0', // "i16x8.lt_u"
'i', '1', '6', 'x', '8', '.', 'g', 't', '_', 's', '\0', // "i16x8.gt_s"
'i', '1', '6', 'x', '8', '.', 'g', 't', '_', 'u', '\0', // "i16x8.gt_u"
'i', '1', '6', 'x', '8', '.', 'l', 'e', '_', 's', '\0', // "i16x8.le_s"
'i', '1', '6', 'x', '8', '.', 'l', 'e', '_', 'u', '\0', // "i16x8.le_u"
'i', '1', '6', 'x', '8', '.', 'g', 'e', '_', 's', '\0', // "i16x8.ge_s"
'i', '1', '6', 'x', '8', '.', 'g', 'e', '_', 'u', '\0', // "i16x8.ge_u"
'i', '3', '2', 'x', '4', '.', 'e', 'q', '\0', // "i32x4.eq"
'i', '3', '2', 'x', '4', '.', 'n', 'e', '\0', // "i32x4.ne"
'i', '3', '2', 'x', '4', '.', 'l', 't', '_', 's', '\0', // "i32x4.lt_s"
'i', '3', '2', 'x', '4', '.', 'l', 't', '_', 'u', '\0', // "i32x4.lt_u"
'i', '3', '2', 'x', '4', '.', 'g', 't', '_', 's', '\0', // "i32x4.gt_s"
'i', '3', '2', 'x', '4', '.', 'g', 't', '_', 'u', '\0', // "i32x4.gt_u"
'i', '3', '2', 'x', '4', '.', 'l', 'e', '_', 's', '\0', // "i32x4.le_s"
'i', '3', '2', 'x', '4', '.', 'l', 'e', '_', 'u', '\0', // "i32x4.le_u"
'i', '3', '2', 'x', '4', '.', 'g', 'e', '_', 's', '\0', // "i32x4.ge_s"
'i', '3', '2', 'x', '4', '.', 'g', 'e', '_', 'u', '\0', // "i32x4.ge_u"
'i', '6', '4', 'x', '2', '.', 'e', 'q', '\0', // "i64x2.eq"
'i', '6', '4', 'x', '2', '.', 'n', 'e', '\0', // "i64x2.ne"
'i', '6', '4', 'x', '2', '.', 'l', 't', '_', 's', '\0', // "i64x2.lt_s"
'i', '6', '4', 'x', '2', '.', 'g', 't', '_', 's', '\0', // "i64x2.gt_s"
'i', '6', '4', 'x', '2', '.', 'l', 'e', '_', 's', '\0', // "i64x2.le_s"
'i', '6', '4', 'x', '2', '.', 'g', 'e', '_', 's', '\0', // "i64x2.ge_s"
'f', '3', '2', 'x', '4', '.', 'e', 'q', '\0', // "f32x4.eq"
'f', '3', '2', 'x', '4', '.', 'n', 'e', '\0', // "f32x4.ne"
'f', '3', '2', 'x', '4', '.', 'l', 't', '\0', // "f32x4.lt"
'f', '3', '2', 'x', '4', '.', 'g', 't', '\0', // "f32x4.gt"
'f', '3', '2', 'x', '4', '.', 'l', 'e', '\0', // "f32x4.le"
'f', '3', '2', 'x', '4', '.', 'g', 'e', '\0', // "f32x4.ge"
'f', '6', '4', 'x', '2', '.', 'e', 'q', '\0', // "f64x2.eq"
'f', '6', '4', 'x', '2', '.', 'n', 'e', '\0', // "f64x2.ne"
'f', '6', '4', 'x', '2', '.', 'l', 't', '\0', // "f64x2.lt"
'f', '6', '4', 'x', '2', '.', 'g', 't', '\0', // "f64x2.gt"
'f', '6', '4', 'x', '2', '.', 'l', 'e', '\0', // "f64x2.le"
'f', '6', '4', 'x', '2', '.', 'g', 'e', '\0', // "f64x2.ge"
'v', '1', '2', '8', '.', 'n', 'o', 't', '\0', // "v128.not"
'v', '1', '2', '8', '.', 'a', 'n', 'd', '\0', // "v128.and"
'v', '1', '2', '8', '.', 'a', 'n', 'd', 'n', 'o', 't', '\0', // "v128.andnot"
'v', '1', '2', '8', '.', 'o', 'r', '\0', // "v128.or"
'v', '1', '2', '8', '.', 'x', 'o', 'r', '\0', // "v128.xor"
'v', '1', '2', '8', '.', 'b', 'i', 't', 's', 'e', 'l', 'e', 'c', 't', '\0', // "v128.bitselect"
'v', '1', '2', '8', '.', 'a', 'n', 'y', '_', 't', 'r', 'u', 'e', '\0', // "v128.any_true"
'v', '1', '2', '8', '.', 'l', 'o', 'a', 'd', '8', '_', 'l', 'a', 'n', 'e', '\0', // "v128.load8_lane"
'v', '1', '2', '8', '.', 'l', 'o', 'a', 'd', '1', '6', '_', 'l', 'a', 'n', 'e', '\0', // "v128.load16_lane"
'v', '1', '2', '8', '.', 'l', 'o', 'a', 'd', '3', '2', '_', 'l', 'a', 'n', 'e', '\0', // "v128.load32_lane"
'v', '1', '2', '8', '.', 'l', 'o', 'a', 'd', '6', '4', '_', 'l', 'a', 'n', 'e', '\0', // "v128.load64_lane"
'v', '1', '2', '8', '.', 's', 't', 'o', 'r', 'e', '8', '_', 'l', 'a', 'n', 'e', '\0', // "v128.store8_lane"
'v', '1', '2', '8', '.', 's', 't', 'o', 'r', 'e', '1', '6', '_', 'l', 'a', 'n', 'e', '\0', // "v128.store16_lane"
'v', '1', '2', '8', '.', 's', 't', 'o', 'r', 'e', '3', '2', '_', 'l', 'a', 'n', 'e', '\0', // "v128.store32_lane"
'v', '1', '2', '8', '.', 's', 't', 'o', 'r', 'e', '6', '4', '_', 'l', 'a', 'n', 'e', '\0', // "v128.store64_lane"
'v', '1', '2', '8', '.', 'l', 'o', 'a', 'd', '3', '2', '_', 'z', 'e', 'r', 'o', '\0', // "v128.load32_zero"
'v', '1', '2', '8', '.', 'l', 'o', 'a', 'd', '6', '4', '_', 'z', 'e', 'r', 'o', '\0', // "v128.load64_zero"
'f', '3', '2', 'x', '4', '.', 'd', 'e', 'm', 'o', 't', 'e', '_', 'f', '6', '4', 'x', '2', '_', 'z', 'e', 'r', 'o', '\0', // "f32x4.demote_f64x2_zero"
'f', '6', '4', 'x', '2', '.', 'p', 'r', 'o', 'm', 'o', 't', 'e', '_', 'l', 'o', 'w', '_', 'f', '3', '2', 'x', '4', '\0', // "f64x2.promote_low_f32x4"
'i', '8', 'x', '1', '6', '.', 'a', 'b', 's', '\0', // "i8x16.abs"
'i', '8', 'x', '1', '6', '.', 'n', 'e', 'g', '\0', // "i8x16.neg"
'i', '8', 'x', '1', '6', '.', 'p', 'o', 'p', 'c', 'n', 't', '\0', // "i8x16.popcnt"
'i', '8', 'x', '1', '6', '.', 'a', 'l', 'l', '_', 't', 'r', 'u', 'e', '\0', // "i8x16.all_true"
'i', '8', 'x', '1', '6', '.', 'b', 'i', 't', 'm', 'a', 's', 'k', '\0', // "i8x16.bitmask"
'i', '8', 'x', '1', '6', '.', 'n', 'a', 'r', 'r', 'o', 'w', '_', 'i', '1', '6', 'x', '8', '_', 's', '\0', // "i8x16.narrow_i16x8_s"
'i', '8', 'x', '1', '6', '.', 'n', 'a', 'r', 'r', 'o', 'w', '_', 'i', '1', '6', 'x', '8', '_', 'u', '\0', // "i8x16.narrow_i16x8_u"
'f', '3', '2', 'x', '4', '.', 'c', 'e', 'i', 'l', '\0', // "f32x4.ceil"
'f', '3', '2', 'x', '4', '.', 'f', 'l', 'o', 'o', 'r', '\0', // "f32x4.floor"
'f', '3', '2', 'x', '4', '.', 't', 'r', 'u', 'n', 'c', '\0', // "f32x4.trunc"
'f', '3', '2', 'x', '4', '.', 'n', 'e', 'a', 'r', 'e', 's', 't', '\0', // "f32x4.nearest"
'i', '8', 'x', '1', '6', '.', 's', 'h', 'l', '\0', // "i8x16.shl"
'i', '8', 'x', '1', '6', '.', 's', 'h', 'r', '_', 's', '\0', // "i8x16.shr_s"
'i', '8', 'x', '1', '6', '.', 's', 'h', 'r', '_', 'u', '\0', // "i8x16.shr_u"
'i', '8', 'x', '1', '6', '.', 'a', 'd', 'd', '\0', // "i8x16.add"
'i', '8', 'x', '1', '6', '.', 'a', 'd', 'd', '_', 's', 'a', 't', '_', 's', '\0', // "i8x16.add_sat_s"
'i', '8', 'x', '1', '6', '.', 'a', 'd', 'd', '_', 's', 'a', 't', '_', 'u', '\0', // "i8x16.add_sat_u"
'i', '8', 'x', '1', '6', '.', 's', 'u', 'b', '\0', // "i8x16.sub"
'i', '8', 'x', '1', '6', '.', 's', 'u', 'b', '_', 's', 'a', 't', '_', 's', '\0', // "i8x16.sub_sat_s"
'i', '8', 'x', '1', '6', '.', 's', 'u', 'b', '_', 's', 'a', 't', '_', 'u', '\0', // "i8x16.sub_sat_u"
'f', '6', '4', 'x', '2', '.', 'c', 'e', 'i', 'l', '\0', // "f64x2.ceil"
'f', '6', '4', 'x', '2', '.', 'f', 'l', 'o', 'o', 'r', '\0', // "f64x2.floor"
'i', '8', 'x', '1', '6', '.', 'm', 'i', 'n', '_', 's', '\0', // "i8x16.min_s"
'i', '8', 'x', '1', '6', '.', 'm', 'i', 'n', '_', 'u', '\0', // "i8x16.min_u"
'i', '8', 'x', '1', '6', '.', 'm', 'a', 'x', '_', 's', '\0', // "i8x16.max_s"
'i', '8', 'x', '1', '6', '.', 'm', 'a', 'x', '_', 'u', '\0', // "i8x16.max_u"
'f', '6', '4', 'x', '2', '.', 't', 'r', 'u', 'n', 'c', '\0', // "f64x2.trunc"
'i', '8', 'x', '1', '6', '.', 'a', 'v', 'g', 'r', '_', 'u', '\0', // "i8x16.avgr_u"
'i', '1', '6', 'x', '8', '.', 'e', 'x', 't', 'a', 'd', 'd', '_', 'p', 'a', 'i', 'r', 'w', 'i', 's', 'e', '_', 's', '_', 'i', '8', 'x', '1', '6', '\0', // "i16x8.extadd_pairwise_s_i8x16"
'i', '1', '6', 'x', '8', '.', 'e', 'x', 't', 'a', 'd', 'd', '_', 'p', 'a', 'i', 'r', 'w', 'i', 's', 'e', '_', 'u', '_', 'i', '8', 'x', '1', '6', '\0', // "i16x8.extadd_pairwise_u_i8x16"
'i', '3', '2', 'x', '4', '.', 'e', 'x', 't', 'a', 'd', 'd', '_', 'p', 'a', 'i', 'r', 'w', 'i', 's', 'e', '_', 's', '_', 'i', '1', '6', 'x', '8', '\0', // "i32x4.extadd_pairwise_s_i16x8"
'i', '3', '2', 'x', '4', '.', 'e', 'x', 't', 'a', 'd', 'd', '_', 'p', 'a', 'i', 'r', 'w', 'i', 's', 'e', '_', 'u', '_', 'i', '1', '6', 'x', '8', '\0', // "i32x4.extadd_pairwise_u_i16x8"
'i', '1', '6', 'x', '8', '.', 'a', 'b', 's', '\0', // "i16x8.abs"
'i', '1', '6', 'x', '8', '.', 'n', 'e', 'g', '\0', // "i16x8.neg"
'i', '1', '6', 'x', '8', '.', 'q', '1', '5', 'm', 'u', 'l', 'r', '_', 's', 'a', 't', '_', 's', '\0', // "i16x8.q15mulr_sat_s"
'i', '1', '6', 'x', '8', '.', 'a', 'l', 'l', '_', 't', 'r', 'u', 'e', '\0', // "i16x8.all_true"
'i', '1', '6', 'x', '8', '.', 'b', 'i', 't', 'm', 'a', 's', 'k', '\0', // "i16x8.bitmask"
'i', '1', '6', 'x', '8', '.', 'n', 'a', 'r', 'r', 'o', 'w', '_', 'i', '3', '2', 'x', '4', '_', 's', '\0', // "i16x8.narrow_i32x4_s"
'i', '1', '6', 'x', '8', '.', 'n', 'a', 'r', 'r', 'o', 'w', '_', 'i', '3', '2', 'x', '4', '_', 'u', '\0', // "i16x8.narrow_i32x4_u"
'i', '1', '6', 'x', '8', '.', 'e', 'x', 't', 'e', 'n', 'd', '_', 'l', 'o', 'w', '_', 's', '_', 'i', '8', 'x', '1', '6', '\0', // "i16x8.extend_low_s_i8x16"
'i', '1', '6', 'x', '8', '.', 'e', 'x', 't', 'e', 'n', 'd', '_', 'h', 'i', 'g', 'h', '_', 's', '_', 'i', '8', 'x', '1', '6', '\0', // "i16x8.extend_high_s_i8x16"
'i', '1', '6', 'x', '8', '.', 'e', 'x', 't', 'e', 'n', 'd', '_', 'l', 'o', 'w', '_', 'u', '_', 'i', '8', 'x', '1', '6', '\0', // "i16x8.extend_low_u_i8x16"
'i', '1', '6', 'x', '8', '.', 'e', 'x', 't', 'e', 'n', 'd', '_', 'h', 'i', 'g', 'h', '_', 'u', '_', 'i', '8', 'x', '1', '6', '\0', // "i16x8.extend_high_u_i8x16"
'i', '1', '6', 'x', '8', '.', 's', 'h', 'l', '\0', // "i16x8.shl"
'i', '1', '6', 'x', '8', '.', 's', 'h', 'r', '_', 's', '\0', // "i16x8.shr_s"
'i', '1', '6', 'x', '8', '.', 's', 'h', 'r', '_', 'u', '\0', // "i16x8.shr_u"
'i', '1', '6', 'x', '8', '.', 'a', 'd', 'd', '\0', // "i16x8.add"
'i', '1', '6', 'x', '8', '.', 'a', 'd', 'd', '_', 's', 'a', 't', '_', 's', '\0', // "i16x8.add_sat_s"
'i', '1', '6', 'x', '8', '.', 'a', 'd', 'd', '_', 's', 'a', 't', '_', 'u', '\0', // "i16x8.add_sat_u"
'i', '1', '6', 'x', '8', '.', 's', 'u', 'b', '\0', // "i16x8.sub"
'i', '1', '6', 'x', '8', '.', 's', 'u', 'b', '_', 's', 'a', 't', '_', 's', '\0', // "i16x8.sub_sat_s"
'i', '1', '6', 'x', '8', '.', 's', 'u', 'b', '_', 's', 'a', 't', '_', 'u', '\0', // "i16x8.sub_sat_u"
'f', '6', '4', 'x', '2', '.', 'n', 'e', 'a', 'r', 'e', 's', 't', '\0', // "f64x2.nearest"
'i', '1', '6', 'x', '8', '.', 'm', 'u', 'l', '\0', // "i16x8.mul"
'i', '1', '6', 'x', '8', '.', 'm', 'i', 'n', '_', 's', '\0', // "i16x8.min_s"
'i', '1', '6', 'x', '8', '.', 'm', 'i', 'n', '_', 'u', '\0', // "i16x8.min_u"
'i', '1', '6', 'x', '8', '.', 'm', 'a', 'x', '_', 's', '\0', // "i16x8.max_s"
'i', '1', '6', 'x', '8', '.', 'm', 'a', 'x', '_', 'u', '\0', // "i16x8.max_u"
'i', '1', '6', 'x', '8', '.', 'a', 'v', 'g', 'r', '_', 'u', '\0', // "i16x8.avgr_u"
'i', '1', '6', 'x', '8', '.', 'e', 'x', 't', 'm', 'u', 'l', '_', 'l', 'o', 'w', '_', 's', '_', 'i', '8', 'x', '1', '6', '\0', // "i16x8.extmul_low_s_i8x16"
'i', '1', '6', 'x', '8', '.', 'e', 'x', 't', 'm', 'u', 'l', '_', 'h', 'i', 'g', 'h', '_', 's', '_', 'i', '8', 'x', '1', '6', '\0', // "i16x8.extmul_high_s_i8x16"
'i', '1', '6', 'x', '8', '.', 'e', 'x', 't', 'm', 'u', 'l', '_', 'l', 'o', 'w', '_', 'u', '_', 'i', '8', 'x', '1', '6', '\0', // "i16x8.extmul_low_u_i8x16"
'i', '1', '6', 'x', '8', '.', 'e', 'x', 't', 'm', 'u', 'l', '_', 'h', 'i', 'g', 'h', '_', 'u', '_', 'i', '8', 'x', '1', '6', '\0', // "i16x8.extmul_high_u_i8x16"
'i', '3', '2', 'x', '4', '.', 'a', 'b', 's', '\0', // "i32x4.abs"
'i', '3', '2', 'x', '4', '.', 'n', 'e', 'g', '\0', // "i32x4.neg"
'i', '3', '2', 'x', '4', '.', 'a', 'l', 'l', '_', 't', 'r', 'u', 'e', '\0', // "i32x4.all_true"
'i', '3', '2', 'x', '4', '.', 'b', 'i', 't', 'm', 'a', 's', 'k', '\0', // "i32x4.bitmask"
'i', '3', '2', 'x', '4', '.', 'e', 'x', 't', 'e', 'n', 'd', '_', 'l', 'o', 'w', '_', 's', '_', 'i', '1', '6', 'x', '8', '\0', // "i32x4.extend_low_s_i16x8"
'i', '3', '2', 'x', '4', '.', 'e', 'x', 't', 'e', 'n', 'd', '_', 'h', 'i', 'g', 'h', '_', 's', '_', 'i', '1', '6', 'x', '8', '\0', // "i32x4.extend_high_s_i16x8"
'i', '3', '2', 'x', '4', '.', 'e', 'x', 't', 'e', 'n', 'd', '_', 'l', 'o', 'w', '_', 'u', '_', 'i', '1', '6', 'x', '8', '\0', // "i32x4.extend_low_u_i16x8"
'i', '3', '2', 'x', '4', '.', 'e', 'x', 't', 'e', 'n', 'd', '_', 'h', 'i', 'g', 'h', '_', 'u', '_', 'i', '1', '6', 'x', '8', '\0', // "i32x4.extend_high_u_i16x8"
'i', '3', '2', 'x', '4', '.', 's', 'h', 'l', '\0', // "i32x4.shl"
'i', '3', '2', 'x', '4', '.', 's', 'h', 'r', '_', 's', '\0', // "i32x4.shr_s"
'i', '3', '2', 'x', '4', '.', 's', 'h', 'r', '_', 'u', '\0', // "i32x4.shr_u"
'i', '3', '2', 'x', '4', '.', 'a', 'd', 'd', '\0', // "i32x4.add"
'i', '3', '2', 'x', '4', '.', 's', 'u', 'b', '\0', // "i32x4.sub"
'i', '3', '2', 'x', '4', '.', 'm', 'u', 'l', '\0', // "i32x4.mul"
'i', '3', '2', 'x', '4', '.', 'm', 'i', 'n', '_', 's', '\0', // "i32x4.min_s"
'i', '3', '2', 'x', '4', '.', 'm', 'i', 'n', '_', 'u', '\0', // "i32x4.min_u"
'i', '3', '2', 'x', '4', '.', 'm', 'a', 'x', '_', 's', '\0', // "i32x4.max_s"
'i', '3', '2', 'x', '4', '.', 'm', 'a', 'x', '_', 'u', '\0', // "i32x4.max_u"
'i', '3', '2', 'x', '4', '.', 'd', 'o', 't', '_', 'i', '1', '6', 'x', '8', '_', 's', '\0', // "i32x4.dot_i16x8_s"
'i', '3', '2', 'x', '4', '.', 'e', 'x', 't', 'm', 'u', 'l', '_', 'l', 'o', 'w', '_', 's', '_', 'i', '1', '6', 'x', '8', '\0', // "i32x4.extmul_low_s_i16x8"
'i', '3', '2', 'x', '4', '.', 'e', 'x', 't', 'm', 'u', 'l', '_', 'h', 'i', 'g', 'h', '_', 's', '_', 'i', '1', '6', 'x', '8', '\0', // "i32x4.extmul_high_s_i16x8"
'i', '3', '2', 'x', '4', '.', 'e', 'x', 't', 'm', 'u', 'l', '_', 'l', 'o', 'w', '_', 'u', '_', 'i', '1', '6', 'x', '8', '\0', // "i32x4.extmul_low_u_i16x8"
'i', '3', '2', 'x', '4', '.', 'e', 'x', 't', 'm', 'u', 'l', '_', 'h', 'i', 'g', 'h', '_', 'u', '_', 'i', '1', '6', 'x', '8', '\0', // "i32x4.extmul_high_u_i16x8"
'i', '6', '4', 'x', '2', '.', 'a', 'b', 's', '\0', // "i64x2.abs"
'i', '6', '4', 'x', '2', '.', 'n', 'e', 'g', '\0', // "i64x2.neg"
'i', '6', '4', 'x', '2', '.', 'a', 'l', 'l', '_', 't', 'r', 'u', 'e', '\0', // "i64x2.all_true"
'i', '6', '4', 'x', '2', '.', 'b', 'i', 't', 'm', 'a', 's', 'k', '\0', // "i64x2.bitmask"
'i', '6', '4', 'x', '2', '.', 'e', 'x', 't', 'e', 'n', 'd', '_', 'l', 'o', 'w', '_', 's', '_', 'i', '3', '2', 'x', '4', '\0', // "i64x2.extend_low_s_i32x4"
'i', '6', '4', 'x', '2', '.', 'e', 'x', 't', 'e', 'n', 'd', '_', 'h', 'i', 'g', 'h', '_', 's', '_', 'i', '3', '2', 'x', '4', '\0', // "i64x2.extend_high_s_i32x4"
'i', '6', '4', 'x', '2', '.', 'e', 'x', 't', 'e', 'n', 'd', '_', 'l', 'o', 'w', '_', 'u', '_', 'i', '3', '2', 'x', '4', '\0', // "i64x2.extend_low_u_i32x4"
'i', '6', '4', 'x', '2', '.', 'e', 'x', 't', 'e', 'n', 'd', '_', 'h', 'i', 'g', 'h', '_', 'u', '_', 'i', '3', '2', 'x', '4', '\0', // "i64x2.extend_high_u_i32x4"
'i', '6', '4', 'x', '2', '.', 's', 'h', 'l', '\0', // "i64x2.shl"
'i', '6', '4', 'x', '2', '.', 's', 'h', 'r', '_', 's', '\0', // "i64x2.shr_s"
'i', '6', '4', 'x', '2', '.', 's', 'h', 'r', '_', 'u', '\0', // "i64x2.shr_u"
'i', '6', '4', 'x', '2', '.', 'a', 'd', 'd', '\0', // "i64x2.add"
'i', '6', '4', 'x', '2', '.', 's', 'u', 'b', '\0', // "i64x2.sub"
'i', '6', '4', 'x', '2', '.', 'm', 'u', 'l', '\0', // "i64x2.mul"
'i', '6', '4', 'x', '2', '.', 'e', 'x', 't', 'm', 'u', 'l', '_', 'l', 'o', 'w', '_', 's', '_', 'i', '3', '2', 'x', '4', '\0', // "i64x2.extmul_low_s_i32x4"
'i', '6', '4', 'x', '2', '.', 'e', 'x', 't', 'm', 'u', 'l', '_', 'h', 'i', 'g', 'h', '_', 's', '_', 'i', '3', '2', 'x', '4', '\0', // "i64x2.extmul_high_s_i32x4"
'i', '6', '4', 'x', '2', '.', 'e', 'x', 't', 'm', 'u', 'l', '_', 'l', 'o', 'w', '_', 'u', '_', 'i', '3', '2', 'x', '4', '\0', // "i64x2.extmul_low_u_i32x4"
'i', '6', '4', 'x', '2', '.', 'e', 'x', 't', 'm', 'u', 'l', '_', 'h', 'i', 'g', 'h', '_', 'u', '_', 'i', '3', '2', 'x', '4', '\0', // "i64x2.extmul_high_u_i32x4"
'f', '3', '2', 'x', '4', '.', 'a', 'b', 's', '\0', // "f32x4.abs"
'f', '3', '2', 'x', '4', '.', 'n', 'e', 'g', '\0', // "f32x4.neg"
'f', '3', '2', 'x', '4', '.', 's', 'q', 'r', 't', '\0', // "f32x4.sqrt"
'f', '3', '2', 'x', '4', '.', 'a', 'd', 'd', '\0', // "f32x4.add"
'f', '3', '2', 'x', '4', '.', 's', 'u', 'b', '\0', // "f32x4.sub"
'f', '3', '2', 'x', '4', '.', 'm', 'u', 'l', '\0', // "f32x4.mul"
'f', '3', '2', 'x', '4', '.', 'd', 'i', 'v', '\0', // "f32x4.div"
'f', '3', '2', 'x', '4', '.', 'm', 'i', 'n', '\0', // "f32x4.min"
'f', '3', '2', 'x', '4', '.', 'm', 'a', 'x', '\0', // "f32x4.max"
'f', '3', '2', 'x', '4', '.', 'p', 'm', 'i', 'n', '\0', // "f32x4.pmin"
'f', '3', '2', 'x', '4', '.', 'p', 'm', 'a', 'x', '\0', // "f32x4.pmax"
'f', '6', '4', 'x', '2', '.', 'a', 'b', 's', '\0', // "f64x2.abs"
'f', '6', '4', 'x', '2', '.', 'n', 'e', 'g', '\0', // "f64x2.neg"
'f', '6', '4', 'x', '2', '.', 's', 'q', 'r', 't', '\0', // "f64x2.sqrt"
'f', '6', '4', 'x', '2', '.', 'a', 'd', 'd', '\0', // "f64x2.add"
'f', '6', '4', 'x', '2', '.', 's', 'u', 'b', '\0', // "f64x2.sub"
'f', '6', '4', 'x', '2', '.', 'm', 'u', 'l', '\0', // "f64x2.mul"
'f', '6', '4', 'x', '2', '.', 'd', 'i', 'v', '\0', // "f64x2.div"
'f', '6', '4', 'x', '2', '.', 'm', 'i', 'n', '\0', // "f64x2.min"
'f', '6', '4', 'x', '2', '.', 'm', 'a', 'x', '\0', // "f64x2.max"
'f', '6', '4', 'x', '2', '.', 'p', 'm', 'i', 'n', '\0', // "f64x2.pmin"
'f', '6', '4', 'x', '2', '.', 'p', 'm', 'a', 'x', '\0', // "f64x2.pmax"
'i', '3', '2', 'x', '4', '.', 't', 'r', 'u', 'n', 'c', '_', 's', 'a', 't', '_', 's', '_', 'f', '3', '2', 'x', '4', '\0', // "i32x4.trunc_sat_s_f32x4"
'i', '3', '2', 'x', '4', '.', 't', 'r', 'u', 'n', 'c', '_', 's', 'a', 't', '_', 'u', '_', 'f', '3', '2', 'x', '4', '\0', // "i32x4.trunc_sat_u_f32x4"
'f', '3', '2', 'x', '4', '.', 'c', 'o', 'n', 'v', 'e', 'r', 't', '_', 's', '_', 'i', '3', '2', 'x', '4', '\0', // "f32x4.convert_s_i32x4"
'f', '3', '2', 'x', '4', '.', 'c', 'o', 'n', 'v', 'e', 'r', 't', '_', 'u', '_', 'i', '3', '2', 'x', '4', '\0', // "f32x4.convert_u_i32x4"
'i', '3', '2', 'x', '4', '.', 't', 'r', 'u', 'n', 'c', '_', 's', 'a', 't', '_', 's', '_', 'f', '6', '4', 'x', '2', '_', 'z', 'e', 'r', 'o', '\0', // "i32x4.trunc_sat_s_f64x2_zero"
'i', '3', '2', 'x', '4', '.', 't', 'r', 'u', 'n', 'c', '_', 's', 'a', 't', '_', 'u', '_', 'f', '6', '4', 'x', '2', '_', 'z', 'e', 'r', 'o', '\0', // "i32x4.trunc_sat_u_f64x2_zero"
'f', '6', '4', 'x', '2', '.', 'c', 'o', 'n', 'v', 'e', 'r', 't', '_', 'l', 'o', 'w', '_', 's', '_', 'i', '3', '2', 'x', '4', '\0', // "f64x2.convert_low_s_i32x4"
'f', '6', '4', 'x', '2', '.', 'c', 'o', 'n', 'v', 'e', 'r', 't', '_', 'l', 'o', 'w', '_', 'u', '_', 'i', '3', '2', 'x', '4', '\0', // "f64x2.convert_low_u_i32x4"
    ];

    private static ReadOnlySpan<int> s_instructionNameRanges
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return s_instructionNameRangesX86;
            }

            if (Configuration.TARGET_AMD64)
            {
                return s_instructionNameRangesAmd64;
            }

            if (Configuration.TARGET_ARM)
            {
                if (Configuration.FEATURE_PLI_INSTRUCTION)
                {
                    return Configuration.FEATURE_ITINSTRUCTION
                        ? s_instructionNameRangesArmPliIt
                        : s_instructionNameRangesArmPli;
                }

                return Configuration.FEATURE_ITINSTRUCTION
                    ? s_instructionNameRangesArmIt
                    : s_instructionNameRangesArm;
            }

            if (Configuration.TARGET_ARM64)
            {
                return s_instructionNameRangesArm64;
            }

            if (Configuration.TARGET_LOONGARCH64)
            {
                return Configuration.FEATURE_SIMD
                    ? s_instructionNameRangesLoongArch64
                    : s_instructionNameRangesLoongArch64NoSimd;
            }

            if (Configuration.TARGET_RISCV64)
            {
                return s_instructionNameRangesRiscv64;
            }

            if (Configuration.TARGET_WASM)
            {
                return s_instructionNameRangesWasm;
            }

            FatalJitException.ThrowFatalJitException(
                FatalJitErrorCode.NoInstructionNameTableIsDefinedForTheConfiguredTarget);
            return default;
        }
    }

    private static ReadOnlySpan<int> s_instructionNameRangesX86 => [
        0, 7, // INS_invalid
        8, 4, // INS_push
        13, 3, // INS_pop
        8, 4, // INS_push_hide
        13, 3, // INS_pop_hide
        17, 5, // INS_push2
        23, 4, // INS_pop2
        28, 3, // INS_inc
        28, 3, // INS_inc_l
        32, 3, // INS_dec
        32, 3, // INS_dec_l
        36, 5, // INS_bswap
        42, 3, // INS_add
        46, 2, // INS_or
        49, 3, // INS_adc
        53, 3, // INS_sbb
        57, 3, // INS_and
        61, 3, // INS_sub
        61, 3, // INS_sub_hide
        65, 3, // INS_xor
        69, 3, // INS_cmp
        73, 4, // INS_test
        78, 3, // INS_mov
        82, 3, // INS_lea
        86, 2, // INS_bt
        89, 3, // INS_bts
        93, 3, // INS_btr
        97, 3, // INS_btc
        101, 3, // INS_bsr
        105, 3, // INS_bsf
        109, 5, // INS_movsx
        122, 5, // INS_movzx
        128, 5, // INS_cmovo
        134, 6, // INS_cmovno
        141, 5, // INS_cmovb
        147, 6, // INS_cmovae
        154, 5, // INS_cmove
        160, 6, // INS_cmovne
        167, 6, // INS_cmovbe
        174, 5, // INS_cmova
        180, 5, // INS_cmovs
        186, 6, // INS_cmovns
        193, 5, // INS_cmovp
        199, 6, // INS_cmovnp
        206, 5, // INS_cmovl
        212, 6, // INS_cmovge
        219, 6, // INS_cmovle
        226, 5, // INS_cmovg
        232, 4, // INS_xchg
        237, 4, // INS_imul
        237, 4, // INS_imul_AX
        237, 4, // INS_imul_CX
        237, 4, // INS_imul_DX
        237, 4, // INS_imul_BX
        237, 4, // INS_imul_SP
        237, 4, // INS_imul_BP
        237, 4, // INS_imul_SI
        237, 4, // INS_imul_DI
        242, 6, // INS_addpd
        249, 6, // INS_addps
        256, 6, // INS_addsd
        263, 6, // INS_addss
        270, 9, // INS_addsubpd
        280, 9, // INS_addsubps
        290, 7, // INS_andnpd
        298, 7, // INS_andnps
        306, 6, // INS_andpd
        313, 6, // INS_andps
        320, 8, // INS_blendpd
        329, 8, // INS_blendps
        338, 8, // INS_blendvpd
        347, 8, // INS_blendvps
        356, 6, // INS_cmppd
        363, 6, // INS_cmpps
        370, 6, // INS_cmpsd
        377, 6, // INS_cmpss
        384, 7, // INS_comisd
        392, 7, // INS_comiss
        400, 9, // INS_cvtdq2pd
        410, 9, // INS_cvtdq2ps
        420, 9, // INS_cvtpd2dq
        430, 9, // INS_cvtpd2ps
        440, 9, // INS_cvtps2dq
        450, 9, // INS_cvtps2pd
        460, 9, // INS_cvtsd2si32
        460, 9, // INS_cvtsd2si64
        470, 9, // INS_cvtsd2ss
        480, 9, // INS_cvtsi2sd32
        480, 9, // INS_cvtsi2sd64
        490, 9, // INS_cvtsi2ss32
        490, 9, // INS_cvtsi2ss64
        500, 9, // INS_cvtss2sd
        510, 9, // INS_cvtss2si32
        510, 9, // INS_cvtss2si64
        520, 10, // INS_cvttpd2dq
        531, 10, // INS_cvttps2dq
        542, 10, // INS_cvttsd2si32
        542, 10, // INS_cvttsd2si64
        553, 10, // INS_cvttss2si32
        553, 10, // INS_cvttss2si64
        564, 6, // INS_divpd
        571, 6, // INS_divps
        578, 6, // INS_divsd
        585, 6, // INS_divss
        592, 5, // INS_dppd
        598, 5, // INS_dpps
        604, 10, // INS_extractps
        615, 7, // INS_haddpd
        623, 7, // INS_haddps
        631, 7, // INS_hsubpd
        639, 7, // INS_hsubps
        647, 9, // INS_insertps
        657, 6, // INS_lddqu
        664, 6, // INS_lfence
        671, 11, // INS_maskmovdqu
        683, 6, // INS_maxpd
        690, 6, // INS_maxps
        697, 6, // INS_maxsd
        704, 6, // INS_maxss
        711, 6, // INS_mfence
        718, 6, // INS_minpd
        725, 6, // INS_minps
        732, 6, // INS_minsd
        739, 6, // INS_minss
        746, 7, // INS_movapd
        754, 7, // INS_movaps
        762, 5, // INS_movd32
        768, 5, // INS_movd64
        774, 8, // INS_movddup
        783, 7, // INS_movdqa32
        791, 7, // INS_movdqu32
        799, 8, // INS_movhlps
        808, 7, // INS_movhpd
        816, 7, // INS_movhps
        824, 8, // INS_movlhps
        833, 7, // INS_movlpd
        841, 7, // INS_movlps
        849, 9, // INS_movmskpd
        859, 9, // INS_movmskps
        869, 8, // INS_movntdq
        878, 9, // INS_movntdqa
        888, 6, // INS_movnti32
        888, 6, // INS_movnti64
        895, 8, // INS_movntpd
        904, 8, // INS_movntps
        768, 5, // INS_movq
        913, 6, // INS_movsd_simd
        920, 9, // INS_movshdup
        930, 9, // INS_movsldup
        940, 6, // INS_movss
        947, 7, // INS_movupd
        955, 7, // INS_movups
        963, 8, // INS_mpsadbw
        972, 6, // INS_mulpd
        979, 6, // INS_mulps
        986, 6, // INS_mulsd
        993, 6, // INS_mulss
        1000, 5, // INS_orpd
        1006, 5, // INS_orps
        1012, 6, // INS_pabsb
        1019, 6, // INS_pabsd
        1026, 6, // INS_pabsw
        1033, 9, // INS_packssdw
        1043, 9, // INS_packsswb
        1053, 9, // INS_packusdw
        1063, 9, // INS_packuswb
        1073, 6, // INS_paddb
        1080, 6, // INS_paddd
        1087, 6, // INS_paddq
        1094, 7, // INS_paddsb
        1102, 7, // INS_paddsw
        1110, 8, // INS_paddusb
        1119, 8, // INS_paddusw
        1128, 6, // INS_paddw
        1135, 8, // INS_palignr
        1144, 5, // INS_pandd
        1150, 6, // INS_pandnd
        1157, 6, // INS_pavgb
        1164, 6, // INS_pavgw
        1171, 8, // INS_pblendvb
        1180, 8, // INS_pblendw
        1189, 8, // INS_pcmpeqb
        1198, 8, // INS_pcmpeqd
        1207, 8, // INS_pcmpeqq
        1216, 8, // INS_pcmpeqw
        1225, 8, // INS_pcmpgtb
        1234, 8, // INS_pcmpgtd
        1243, 8, // INS_pcmpgtq
        1252, 8, // INS_pcmpgtw
        1261, 7, // INS_pextrb
        1269, 7, // INS_pextrd
        1277, 7, // INS_pextrq
        1285, 7, // INS_phaddd
        1293, 7, // INS_pextrw
        1301, 8, // INS_phaddsw
        1310, 7, // INS_phaddw
        1318, 11, // INS_phminposuw
        1330, 7, // INS_phsubd
        1338, 8, // INS_phsubsw
        1347, 7, // INS_phsubw
        1355, 7, // INS_pinsrb
        1363, 7, // INS_pinsrd
        1371, 7, // INS_pinsrq
        1379, 7, // INS_pinsrw
        1387, 10, // INS_pmaddubsw
        1398, 8, // INS_pmaddwd
        1407, 7, // INS_pmaxsb
        1415, 7, // INS_pmaxsd
        1423, 7, // INS_pmaxsw
        1431, 7, // INS_pmaxub
        1439, 7, // INS_pmaxud
        1447, 7, // INS_pmaxuw
        1455, 7, // INS_pminsb
        1463, 7, // INS_pminsd
        1471, 7, // INS_pminsw
        1479, 7, // INS_pminub
        1487, 7, // INS_pminud
        1495, 7, // INS_pminuw
        1503, 9, // INS_pmovmskb
        1513, 9, // INS_pmovsxbd
        1523, 9, // INS_pmovsxbq
        1533, 9, // INS_pmovsxbw
        1543, 9, // INS_pmovsxdq
        1553, 9, // INS_pmovsxwd
        1563, 9, // INS_pmovsxwq
        1573, 9, // INS_pmovzxbd
        1583, 9, // INS_pmovzxbq
        1593, 9, // INS_pmovzxbw
        1603, 9, // INS_pmovzxdq
        1613, 9, // INS_pmovzxwd
        1623, 9, // INS_pmovzxwq
        1633, 7, // INS_pmuldq
        1641, 9, // INS_pmulhrsw
        1651, 8, // INS_pmulhuw
        1660, 7, // INS_pmulhw
        1668, 7, // INS_pmulld
        1676, 8, // INS_pmuludq
        1685, 7, // INS_pmullw
        1693, 4, // INS_pord
        1698, 11, // INS_prefetchnta
        1710, 10, // INS_prefetcht0
        1721, 10, // INS_prefetcht1
        1732, 10, // INS_prefetcht2
        1743, 7, // INS_psadbw
        1751, 7, // INS_pshufb
        1759, 7, // INS_pshufd
        1767, 8, // INS_pshufhw
        1776, 8, // INS_pshuflw
        1785, 7, // INS_psignb
        1793, 7, // INS_psignd
        1801, 7, // INS_psignw
        1809, 6, // INS_pslld
        1816, 7, // INS_pslldq
        1824, 6, // INS_psllq
        1831, 6, // INS_psllw
        1838, 6, // INS_psrad
        1845, 6, // INS_psraw
        1852, 6, // INS_psrld
        1859, 7, // INS_psrldq
        1867, 6, // INS_psrlq
        1874, 6, // INS_psrlw
        1881, 6, // INS_psubb
        1888, 6, // INS_psubd
        1895, 6, // INS_psubq
        1902, 7, // INS_psubsb
        1910, 7, // INS_psubsw
        1918, 8, // INS_psubusb
        1927, 8, // INS_psubusw
        1936, 6, // INS_psubw
        1943, 6, // INS_ptest
        1950, 10, // INS_punpckhbw
        1961, 10, // INS_punpckhdq
        1972, 11, // INS_punpckhqdq
        1984, 10, // INS_punpckhwd
        1995, 10, // INS_punpcklbw
        2006, 10, // INS_punpckldq
        2017, 11, // INS_punpcklqdq
        2029, 10, // INS_punpcklwd
        2040, 5, // INS_pxord
        2046, 6, // INS_rcpps
        2053, 6, // INS_rcpss
        2060, 8, // INS_roundpd
        2069, 8, // INS_roundps
        2078, 8, // INS_roundsd
        2087, 8, // INS_roundss
        2096, 8, // INS_rsqrtps
        2105, 8, // INS_rsqrtss
        2114, 6, // INS_sfence
        2121, 7, // INS_shufpd
        2129, 7, // INS_shufps
        2137, 7, // INS_sqrtpd
        2145, 7, // INS_sqrtps
        2153, 7, // INS_sqrtsd
        2161, 7, // INS_sqrtss
        2169, 6, // INS_subpd
        2176, 6, // INS_subps
        2183, 6, // INS_subsd
        2190, 6, // INS_subss
        2197, 8, // INS_ucomisd
        2206, 8, // INS_ucomiss
        2215, 9, // INS_unpckhpd
        2225, 9, // INS_unpckhps
        2235, 9, // INS_unpcklpd
        2245, 9, // INS_unpcklps
        2255, 6, // INS_xorpd
        2262, 6, // INS_xorps
        2269, 7, // INS_aesdec
        2277, 11, // INS_aesdeclast
        2289, 7, // INS_aesenc
        2297, 11, // INS_aesenclast
        2309, 7, // INS_aesimc
        2317, 16, // INS_aeskeygenassist
        2334, 10, // INS_pclmulqdq
        2345, 8, // INS_sha1msg1
        2354, 8, // INS_sha1msg2
        2363, 9, // INS_sha1nexte
        2373, 9, // INS_sha1rnds4
        2383, 10, // INS_sha256msg1
        2394, 10, // INS_sha256msg2
        2405, 11, // INS_sha256rnds2
        2417, 17, // INS_gf2p8affineinvqb
        2435, 14, // INS_gf2p8affineqb
        2450, 10, // INS_gf2p8mulb
        2461, 9, // INS_vblendvpd
        2471, 9, // INS_vblendvps
        2481, 14, // INS_vbroadcastf32x4
        2496, 12, // INS_vbroadcastsd
        2509, 12, // INS_vbroadcastss
        2522, 12, // INS_vextractf32x4
        2535, 11, // INS_vinsertf32x4
        2547, 10, // INS_vmaskmovpd
        2558, 10, // INS_vmaskmovps
        2569, 9, // INS_vpblendvb
        2579, 10, // INS_vperm2f128
        2590, 9, // INS_vpermilpd
        2590, 9, // INS_vpermilpdvar
        2600, 9, // INS_vpermilps
        2600, 9, // INS_vpermilpsvar
        2610, 7, // INS_vtestpd
        2618, 7, // INS_vtestps
        2626, 10, // INS_vzeroupper
        2637, 14, // INS_vbroadcasti32x4
        2652, 9, // INS_vcvtph2ps
        2662, 9, // INS_vcvtps2ph
        2672, 12, // INS_vextracti32x4
        2685, 10, // INS_vgatherdpd
        2696, 10, // INS_vgatherdps
        2707, 10, // INS_vgatherqpd
        2718, 10, // INS_vgatherqps
        2729, 11, // INS_vinserti32x4
        2741, 8, // INS_vpblendd
        2750, 12, // INS_vpbroadcastb
        2763, 12, // INS_vpbroadcastd
        2776, 12, // INS_vpbroadcastq
        2789, 12, // INS_vpbroadcastw
        2802, 10, // INS_vperm2i128
        2813, 6, // INS_vpermd
        2820, 7, // INS_vpermpd
        2828, 7, // INS_vpermps
        2836, 6, // INS_vpermq
        2843, 10, // INS_vpgatherdd
        2854, 10, // INS_vpgatherdq
        2865, 10, // INS_vpgatherqd
        2876, 10, // INS_vpgatherqq
        2887, 10, // INS_vpmaskmovd
        2898, 10, // INS_vpmaskmovq
        2909, 7, // INS_vpsllvd
        2917, 7, // INS_vpsllvq
        2925, 7, // INS_vpsravd
        2933, 7, // INS_vpsrlvd
        2941, 7, // INS_vpsrlvq
        2949, 11, // INS_vfmadd132pd
        2961, 11, // INS_vfmadd213pd
        2973, 11, // INS_vfmadd231pd
        2985, 11, // INS_vfmadd132ps
        2997, 11, // INS_vfmadd213ps
        3009, 11, // INS_vfmadd231ps
        3021, 11, // INS_vfmadd132sd
        3033, 11, // INS_vfmadd213sd
        3045, 11, // INS_vfmadd231sd
        3057, 11, // INS_vfmadd132ss
        3069, 11, // INS_vfmadd213ss
        3081, 11, // INS_vfmadd231ss
        3093, 14, // INS_vfmaddsub132pd
        3108, 14, // INS_vfmaddsub213pd
        3123, 14, // INS_vfmaddsub231pd
        3138, 14, // INS_vfmaddsub132ps
        3153, 14, // INS_vfmaddsub213ps
        3168, 14, // INS_vfmaddsub231ps
        3183, 14, // INS_vfmsubadd132pd
        3198, 14, // INS_vfmsubadd213pd
        3213, 14, // INS_vfmsubadd231pd
        3228, 14, // INS_vfmsubadd132ps
        3243, 14, // INS_vfmsubadd213ps
        3258, 14, // INS_vfmsubadd231ps
        3273, 11, // INS_vfmsub132pd
        3285, 11, // INS_vfmsub213pd
        3297, 11, // INS_vfmsub231pd
        3309, 11, // INS_vfmsub132ps
        3321, 11, // INS_vfmsub213ps
        3333, 11, // INS_vfmsub231ps
        3345, 11, // INS_vfmsub132sd
        3357, 11, // INS_vfmsub213sd
        3369, 11, // INS_vfmsub231sd
        3381, 11, // INS_vfmsub132ss
        3393, 11, // INS_vfmsub213ss
        3405, 11, // INS_vfmsub231ss
        3417, 12, // INS_vfnmadd132pd
        3430, 12, // INS_vfnmadd213pd
        3443, 12, // INS_vfnmadd231pd
        3456, 12, // INS_vfnmadd132ps
        3469, 12, // INS_vfnmadd213ps
        3482, 12, // INS_vfnmadd231ps
        3495, 12, // INS_vfnmadd132sd
        3508, 12, // INS_vfnmadd213sd
        3521, 12, // INS_vfnmadd231sd
        3534, 12, // INS_vfnmadd132ss
        3547, 12, // INS_vfnmadd213ss
        3560, 12, // INS_vfnmadd231ss
        3573, 12, // INS_vfnmsub132pd
        3586, 12, // INS_vfnmsub213pd
        3599, 12, // INS_vfnmsub231pd
        3612, 12, // INS_vfnmsub132ps
        3625, 12, // INS_vfnmsub213ps
        3638, 12, // INS_vfnmsub231ps
        3651, 12, // INS_vfnmsub132sd
        3664, 12, // INS_vfnmsub213sd
        3677, 12, // INS_vfnmsub231sd
        3690, 12, // INS_vfnmsub132ss
        3703, 12, // INS_vfnmsub213ss
        3716, 12, // INS_vfnmsub231ss
        3729, 4, // INS_andn
        3734, 5, // INS_bextr
        3740, 4, // INS_blsi
        3745, 6, // INS_blsmsk
        3752, 4, // INS_blsr
        3757, 4, // INS_bzhi
        3762, 4, // INS_mulx
        3767, 4, // INS_pdep
        3772, 4, // INS_pext
        3777, 4, // INS_rorx
        3782, 4, // INS_sarx
        3787, 4, // INS_shlx
        3792, 4, // INS_shrx
        3797, 8, // INS_vpdpbusd
        3806, 9, // INS_vpdpbusds
        3816, 8, // INS_vpdpwssd
        3825, 9, // INS_vpdpwssds
        3835, 8, // INS_vpdpwsud
        3844, 9, // INS_vpdpwsuds
        3854, 8, // INS_vpdpwusd
        3863, 9, // INS_vpdpwusds
        3873, 8, // INS_vpdpwuud
        3882, 9, // INS_vpdpwuuds
        3892, 8, // INS_vpdpbssd
        3901, 9, // INS_vpdpbssds
        3911, 8, // INS_vpdpbsud
        3920, 9, // INS_vpdpbsuds
        3930, 8, // INS_vpdpbuud
        3939, 9, // INS_vpdpbuuds
        3949, 15, // INS_vbmacor16x16x16
        3965, 16, // INS_vbmacxor16x16x16
        3982, 7, // INS_vbitrev
        3990, 11, // INS_vpmadd52huq
        4002, 11, // INS_vpmadd52luq
        4014, 5, // INS_kaddb
        4020, 5, // INS_kaddd
        4026, 5, // INS_kaddq
        4032, 5, // INS_kaddw
        4038, 5, // INS_kandb
        4044, 5, // INS_kandd
        4050, 6, // INS_kandnb
        4057, 6, // INS_kandnd
        4064, 6, // INS_kandnq
        4071, 6, // INS_kandnw
        4078, 5, // INS_kandq
        4084, 5, // INS_kandw
        4090, 5, // INS_kmovb_gpr
        4090, 5, // INS_kmovb_msk
        4096, 5, // INS_kmovd_gpr
        4096, 5, // INS_kmovd_msk
        4102, 5, // INS_kmovq_gpr
        4102, 5, // INS_kmovq_msk
        4108, 5, // INS_kmovw_gpr
        4108, 5, // INS_kmovw_msk
        4114, 5, // INS_knotb
        4120, 5, // INS_knotd
        4126, 5, // INS_knotq
        4132, 5, // INS_knotw
        4138, 4, // INS_korb
        4143, 4, // INS_kord
        4148, 4, // INS_korq
        4153, 8, // INS_kortestb
        4162, 8, // INS_kortestd
        4171, 8, // INS_kortestq
        4180, 8, // INS_kortestw
        4189, 4, // INS_korw
        4194, 8, // INS_kshiftlb
        4203, 8, // INS_kshiftld
        4212, 8, // INS_kshiftlq
        4221, 8, // INS_kshiftlw
        4230, 8, // INS_kshiftrb
        4239, 8, // INS_kshiftrd
        4248, 8, // INS_kshiftrq
        4257, 8, // INS_kshiftrw
        4266, 6, // INS_ktestb
        4273, 6, // INS_ktestd
        4280, 6, // INS_ktestq
        4287, 6, // INS_ktestw
        4294, 8, // INS_kunpckbw
        4303, 8, // INS_kunpckdq
        4312, 8, // INS_kunpckwd
        4321, 6, // INS_kxnorb
        4328, 6, // INS_kxnord
        4335, 6, // INS_kxnorq
        4342, 6, // INS_kxnorw
        4349, 5, // INS_kxorb
        4355, 5, // INS_kxord
        4361, 5, // INS_kxorq
        4367, 5, // INS_kxorw
        4373, 7, // INS_valignd
        4381, 7, // INS_valignq
        4389, 9, // INS_vblendmpd
        4399, 9, // INS_vblendmps
        4409, 15, // INS_vbroadcastf32x2
        4425, 15, // INS_vbroadcastf32x8
        4441, 15, // INS_vbroadcastf64x2
        4457, 15, // INS_vbroadcastf64x4
        4473, 15, // INS_vbroadcasti32x2
        4489, 15, // INS_vbroadcasti32x8
        4505, 15, // INS_vbroadcasti64x2
        4521, 15, // INS_vbroadcasti64x4
        356, 6, // INS_vcmppd
        363, 6, // INS_vcmpps
        370, 6, // INS_vcmpsd
        377, 6, // INS_vcmpss
        4537, 11, // INS_vcompresspd
        4549, 11, // INS_vcompressps
        4561, 9, // INS_vcvtpd2qq
        4571, 10, // INS_vcvtpd2udq
        4582, 10, // INS_vcvtpd2uqq
        4593, 9, // INS_vcvtps2qq
        4603, 10, // INS_vcvtps2udq
        4614, 10, // INS_vcvtps2uqq
        4625, 9, // INS_vcvtqq2pd
        4635, 9, // INS_vcvtqq2ps
        4645, 10, // INS_vcvtsd2usi32
        4645, 10, // INS_vcvtsd2usi64
        4656, 10, // INS_vcvtss2usi32
        4656, 10, // INS_vcvtss2usi64
        4667, 10, // INS_vcvttpd2qq
        4678, 11, // INS_vcvttpd2udq
        4690, 11, // INS_vcvttpd2uqq
        4702, 10, // INS_vcvttps2qq
        4713, 11, // INS_vcvttps2udq
        4725, 11, // INS_vcvttps2uqq
        4737, 11, // INS_vcvttsd2usi32
        4737, 11, // INS_vcvttsd2usi64
        4749, 11, // INS_vcvttss2usi32
        4749, 11, // INS_vcvttss2usi64
        4761, 10, // INS_vcvtudq2pd
        4772, 10, // INS_vcvtudq2ps
        4783, 10, // INS_vcvtuqq2pd
        4794, 10, // INS_vcvtuqq2ps
        4805, 10, // INS_vcvtusi2sd32
        4805, 10, // INS_vcvtusi2sd64
        4816, 10, // INS_vcvtusi2ss32
        4816, 10, // INS_vcvtusi2ss64
        4827, 9, // INS_vdbpsadbw
        4837, 9, // INS_vexpandpd
        4847, 9, // INS_vexpandps
        4857, 13, // INS_vextractf32x8
        4871, 13, // INS_vextractf64x2
        4885, 13, // INS_vextractf64x4
        4899, 13, // INS_vextracti32x8
        4913, 13, // INS_vextracti64x2
        4927, 13, // INS_vextracti64x4
        4941, 11, // INS_vfixupimmpd
        4953, 11, // INS_vfixupimmps
        4965, 11, // INS_vfixupimmsd
        4977, 11, // INS_vfixupimmss
        4989, 10, // INS_vfpclasspd
        5000, 10, // INS_vfpclassps
        5011, 10, // INS_vfpclasssd
        5022, 10, // INS_vfpclassss
        2685, 10, // INS_vgatherdpd_msk
        2696, 10, // INS_vgatherdps_msk
        2707, 10, // INS_vgatherqpd_msk
        2718, 10, // INS_vgatherqps_msk
        5033, 9, // INS_vgetexppd
        5043, 9, // INS_vgetexpps
        5053, 9, // INS_vgetexpsd
        5063, 9, // INS_vgetexpss
        5073, 10, // INS_vgetmantpd
        5084, 10, // INS_vgetmantps
        5095, 10, // INS_vgetmantsd
        5106, 10, // INS_vgetmantss
        5117, 12, // INS_vinsertf32x8
        5130, 12, // INS_vinsertf64x2
        5143, 12, // INS_vinsertf64x4
        5156, 12, // INS_vinserti32x8
        5169, 12, // INS_vinserti64x2
        5182, 12, // INS_vinserti64x4
        5195, 9, // INS_vmovdqa64
        5205, 9, // INS_vmovdqu16
        5215, 9, // INS_vmovdqu64
        5225, 8, // INS_vmovdqu8
        5234, 6, // INS_vpabsq
        5241, 7, // INS_vpandnq
        5249, 6, // INS_vpandq
        5256, 9, // INS_vpblendmb
        5266, 9, // INS_vpblendmd
        5276, 9, // INS_vpblendmq
        5286, 9, // INS_vpblendmw
        2750, 12, // INS_vpbroadcastb_gpr
        2763, 12, // INS_vpbroadcastd_gpr
        2776, 12, // INS_vpbroadcastq_gpr
        2789, 12, // INS_vpbroadcastw_gpr
        5296, 6, // INS_vpcmpb
        5303, 6, // INS_vpcmpd
        1189, 8, // INS_vpcmpeqb
        1198, 8, // INS_vpcmpeqd
        1207, 8, // INS_vpcmpeqq
        1216, 8, // INS_vpcmpeqw
        1225, 8, // INS_vpcmpgtb
        1234, 8, // INS_vpcmpgtd
        1243, 8, // INS_vpcmpgtq
        1252, 8, // INS_vpcmpgtw
        5310, 6, // INS_vpcmpq
        5317, 7, // INS_vpcmpub
        5325, 7, // INS_vpcmpud
        5333, 7, // INS_vpcmpuq
        5341, 7, // INS_vpcmpuw
        5349, 6, // INS_vpcmpw
        5356, 11, // INS_vpcompressd
        5368, 11, // INS_vpcompressq
        5380, 11, // INS_vpconflictd
        5392, 11, // INS_vpconflictq
        5404, 8, // INS_vpermi2d
        5413, 9, // INS_vpermi2pd
        5423, 9, // INS_vpermi2ps
        5433, 8, // INS_vpermi2q
        5442, 8, // INS_vpermi2w
        2820, 7, // INS_vpermpd_reg
        2836, 6, // INS_vpermq_reg
        5451, 8, // INS_vpermt2d
        5460, 9, // INS_vpermt2pd
        5470, 9, // INS_vpermt2ps
        5480, 8, // INS_vpermt2q
        5489, 8, // INS_vpermt2w
        5498, 6, // INS_vpermw
        5505, 9, // INS_vpexpandd
        5515, 9, // INS_vpexpandq
        2843, 10, // INS_vpgatherdd_msk
        2854, 10, // INS_vpgatherdq_msk
        2865, 10, // INS_vpgatherqd_msk
        2876, 10, // INS_vpgatherqq_msk
        5525, 8, // INS_vplzcntd
        5534, 8, // INS_vplzcntq
        5543, 7, // INS_vpmaxsq
        5551, 7, // INS_vpmaxuq
        5559, 7, // INS_vpminsq
        5567, 7, // INS_vpminuq
        5575, 8, // INS_vpmovb2m
        5584, 8, // INS_vpmovd2m
        5593, 7, // INS_vpmovdb
        5601, 7, // INS_vpmovdw
        5609, 8, // INS_vpmovm2b
        5618, 8, // INS_vpmovm2d
        5627, 8, // INS_vpmovm2q
        5636, 8, // INS_vpmovm2w
        5645, 8, // INS_vpmovq2m
        5654, 7, // INS_vpmovqb
        5662, 7, // INS_vpmovqd
        5670, 7, // INS_vpmovqw
        5678, 8, // INS_vpmovsdb
        5687, 8, // INS_vpmovsdw
        5696, 8, // INS_vpmovsqb
        5705, 8, // INS_vpmovsqd
        5714, 8, // INS_vpmovsqw
        5723, 8, // INS_vpmovswb
        5732, 9, // INS_vpmovusdb
        5742, 9, // INS_vpmovusdw
        5752, 9, // INS_vpmovusqb
        5762, 9, // INS_vpmovusqd
        5772, 9, // INS_vpmovusqw
        5782, 9, // INS_vpmovuswb
        5792, 8, // INS_vpmovw2m
        5801, 7, // INS_vpmovwb
        5809, 7, // INS_vpmullq
        5817, 5, // INS_vporq
        5823, 6, // INS_vprold
        5830, 6, // INS_vprolq
        5837, 7, // INS_vprolvd
        5845, 7, // INS_vprolvq
        5853, 6, // INS_vprord
        5860, 6, // INS_vprorq
        5867, 7, // INS_vprorvd
        5875, 7, // INS_vprorvq
        5883, 11, // INS_vpscatterdd_msk
        5895, 11, // INS_vpscatterdq_msk
        5907, 11, // INS_vpscatterqd_msk
        5919, 11, // INS_vpscatterqq_msk
        5931, 7, // INS_vpsllvw
        5939, 6, // INS_vpsraq
        5946, 7, // INS_vpsravq
        5954, 7, // INS_vpsravw
        5962, 7, // INS_vpsrlvw
        5970, 10, // INS_vpternlogd
        5981, 10, // INS_vpternlogq
        5992, 8, // INS_vptestmb
        6001, 8, // INS_vptestmd
        6010, 8, // INS_vptestmq
        6019, 8, // INS_vptestmw
        6028, 9, // INS_vptestnmb
        6038, 9, // INS_vptestnmd
        6048, 9, // INS_vptestnmq
        6058, 9, // INS_vptestnmw
        6068, 6, // INS_vpxorq
        6075, 8, // INS_vrangepd
        6084, 8, // INS_vrangeps
        6093, 8, // INS_vrangesd
        6102, 8, // INS_vrangess
        6111, 8, // INS_vrcp14pd
        6120, 8, // INS_vrcp14ps
        6129, 8, // INS_vrcp14sd
        6138, 8, // INS_vrcp14ss
        6147, 9, // INS_vreducepd
        6157, 9, // INS_vreduceps
        6167, 9, // INS_vreducesd
        6177, 9, // INS_vreducess
        6187, 11, // INS_vrndscalepd
        6199, 11, // INS_vrndscaleps
        6211, 11, // INS_vrndscalesd
        6223, 11, // INS_vrndscaless
        6235, 10, // INS_vrsqrt14pd
        6246, 10, // INS_vrsqrt14ps
        6257, 10, // INS_vrsqrt14sd
        6268, 10, // INS_vrsqrt14ss
        6279, 9, // INS_vscalefpd
        6289, 9, // INS_vscalefps
        6299, 9, // INS_vscalefsd
        6309, 9, // INS_vscalefss
        6319, 11, // INS_vscatterdpd_msk
        6331, 11, // INS_vscatterdps_msk
        6343, 11, // INS_vscatterqpd_msk
        6355, 11, // INS_vscatterqps_msk
        6367, 10, // INS_vshuff32x4
        6378, 10, // INS_vshuff64x2
        6389, 10, // INS_vshufi32x4
        6400, 10, // INS_vshufi64x2
        6411, 6, // INS_vpermb
        6418, 8, // INS_vpermi2b
        6427, 8, // INS_vpermt2b
        6436, 14, // INS_vpmultishiftqb
        6451, 11, // INS_vpcompressb
        6463, 11, // INS_vpcompressw
        6475, 9, // INS_vpexpandb
        6485, 9, // INS_vpexpandw
        6495, 8, // INS_vpopcntb
        6504, 8, // INS_vpopcntd
        6513, 8, // INS_vpopcntq
        6522, 8, // INS_vpopcntw
        6531, 7, // INS_vpshldd
        6539, 7, // INS_vpshldq
        6547, 8, // INS_vpshldvd
        6556, 8, // INS_vpshldvq
        6565, 8, // INS_vpshldvw
        6574, 7, // INS_vpshldw
        6582, 7, // INS_vpshrdd
        6590, 7, // INS_vpshrdq
        6598, 8, // INS_vpshrdvd
        6607, 8, // INS_vpshrdvq
        6616, 8, // INS_vpshrdvw
        6625, 7, // INS_vpshrdw
        6633, 12, // INS_vpshufbitqmb
        6646, 6, // INS_vaddph
        6653, 6, // INS_vaddsh
        6660, 6, // INS_vcmpph
        6667, 6, // INS_vcmpsh
        6674, 7, // INS_vcomish
        6682, 9, // INS_vcvtdq2ph
        6692, 14, // INS_vcvtne2ps2bf16
        6707, 13, // INS_vcvtneps2bf16
        6721, 9, // INS_vcvtpd2ph
        6731, 9, // INS_vcvtph2dq
        6741, 9, // INS_vcvtph2pd
        6751, 10, // INS_vcvtph2psx
        6762, 9, // INS_vcvtph2qq
        6772, 10, // INS_vcvtph2udq
        6783, 10, // INS_vcvtph2uqq
        6794, 9, // INS_vcvtph2uw
        6804, 8, // INS_vcvtph2w
        6813, 10, // INS_vcvtps2phx
        6824, 9, // INS_vcvtqq2ph
        6834, 9, // INS_vcvtsd2sh
        6844, 9, // INS_vcvtsh2sd
        6854, 9, // INS_vcvtsh2si32
        6854, 9, // INS_vcvtsh2si64
        6864, 9, // INS_vcvtsh2ss
        6874, 10, // INS_vcvtsh2usi32
        6874, 10, // INS_vcvtsh2usi64
        6885, 9, // INS_vcvtsi2sh32
        6885, 9, // INS_vcvtsi2sh64
        6895, 9, // INS_vcvtss2sh
        6905, 10, // INS_vcvttph2dq
        6916, 10, // INS_vcvttph2qq
        6927, 11, // INS_vcvttph2udq
        6939, 11, // INS_vcvttph2uqq
        6951, 10, // INS_vcvttph2uw
        6962, 9, // INS_vcvttph2w
        6972, 10, // INS_vcvttsh2si32
        6972, 10, // INS_vcvttsh2si64
        6983, 11, // INS_vcvttsh2usi32
        6983, 11, // INS_vcvttsh2usi64
        6995, 10, // INS_vcvtudq2ph
        7006, 10, // INS_vcvtuqq2ph
        7017, 10, // INS_vcvtusi2sh32
        7017, 10, // INS_vcvtusi2sh64
        7028, 9, // INS_vcvtuw2ph
        7038, 8, // INS_vcvtw2ph
        7047, 6, // INS_vdivph
        7054, 6, // INS_vdivsh
        7061, 9, // INS_vdpbf16ps
        7071, 10, // INS_vfcmaddcph
        7082, 10, // INS_vfcmaddcsh
        7093, 9, // INS_vfcmulcph
        7103, 9, // INS_vfcmulcsh
        7113, 8, // INS_vfmulcph
        7122, 8, // INS_vfmulcsh
        7131, 11, // INS_vfmadd132ph
        7143, 11, // INS_vfmadd213ph
        7155, 11, // INS_vfmadd231ph
        7167, 11, // INS_vfmadd132sh
        7179, 11, // INS_vfmadd213sh
        7191, 11, // INS_vfmadd231sh
        7203, 9, // INS_vfmaddcph
        7213, 9, // INS_vfmaddcsh
        7223, 14, // INS_vfmaddsub132ph
        7238, 14, // INS_vfmaddsub213ph
        7253, 14, // INS_vfmaddsub231ph
        7268, 11, // INS_vfmsub132ph
        7280, 11, // INS_vfmsub213ph
        7292, 11, // INS_vfmsub231ph
        7304, 11, // INS_vfmsub132sh
        7316, 11, // INS_vfmsub213sh
        7328, 11, // INS_vfmsub231sh
        7340, 14, // INS_vfmsubadd132ph
        7355, 14, // INS_vfmsubadd213ph
        7370, 14, // INS_vfmsubadd231ph
        7385, 12, // INS_vfnmadd132ph
        7398, 12, // INS_vfnmadd213ph
        7411, 12, // INS_vfnmadd231ph
        7424, 12, // INS_vfnmadd132sh
        7437, 12, // INS_vfnmadd213sh
        7450, 12, // INS_vfnmadd231sh
        7463, 12, // INS_vfnmsub132ph
        7476, 12, // INS_vfnmsub213ph
        7489, 12, // INS_vfnmsub231ph
        7502, 12, // INS_vfnmsub132sh
        7515, 12, // INS_vfnmsub213sh
        7528, 12, // INS_vfnmsub231sh
        7541, 10, // INS_vfpclassph
        7552, 10, // INS_vfpclasssh
        7563, 9, // INS_vgetexpph
        7573, 9, // INS_vgetexpsh
        7583, 10, // INS_vgetmantph
        7594, 10, // INS_vgetmantsh
        7605, 6, // INS_vmaxph
        7612, 6, // INS_vmaxsh
        7619, 6, // INS_vminsh
        7626, 6, // INS_vminph
        7633, 6, // INS_vmovsh
        7640, 5, // INS_vmovw
        7646, 6, // INS_vmulph
        7653, 6, // INS_vmulsh
        7660, 6, // INS_vrcpph
        7667, 6, // INS_vrcpsh
        7674, 9, // INS_vreduceph
        7684, 9, // INS_vreducesh
        7694, 11, // INS_vrndscaleph
        7706, 11, // INS_vrndscalesh
        7718, 8, // INS_vrsqrtph
        7727, 8, // INS_vrsqrtsh
        7736, 9, // INS_vscalefph
        7746, 9, // INS_vscalefsh
        7756, 7, // INS_vsqrtph
        7764, 7, // INS_vsqrtsh
        7772, 6, // INS_vsubph
        7779, 6, // INS_vsubsh
        7786, 8, // INS_vucomish
        7795, 13, // INS_vp2intersectd
        7809, 13, // INS_vp2intersectq
        7823, 7, // INS_vcomxsd
        7831, 7, // INS_vcomxss
        7839, 10, // INS_vcvtps2ibs
        7850, 11, // INS_vcvtps2iubs
        7862, 11, // INS_vcvttpd2dqs
        7874, 11, // INS_vcvttpd2qqs
        7886, 12, // INS_vcvttpd2udqs
        7899, 12, // INS_vcvttpd2uqqs
        7912, 11, // INS_vcvttps2dqs
        7924, 11, // INS_vcvttps2ibs
        7936, 12, // INS_vcvttps2iubs
        7949, 11, // INS_vcvttps2qqs
        7961, 12, // INS_vcvttps2udqs
        7974, 12, // INS_vcvttps2uqqs
        7987, 11, // INS_vcvttsd2sis32
        7987, 11, // INS_vcvttsd2sis64
        7999, 12, // INS_vcvttsd2usis32
        7999, 12, // INS_vcvttsd2usis64
        8012, 11, // INS_vcvttss2sis32
        8012, 11, // INS_vcvttss2sis64
        8024, 12, // INS_vcvttss2usis32
        8024, 12, // INS_vcvttss2usis64
        8037, 9, // INS_vminmaxpd
        8047, 9, // INS_vminmaxps
        8057, 9, // INS_vminmaxsd
        8067, 9, // INS_vminmaxss
        762, 5, // INS_vmovd_simd
        7640, 5, // INS_vmovw_simd
        963, 8, // INS_vmpsadbw
        8077, 8, // INS_vucomxsd
        8086, 8, // INS_vucomxss
        8453, 5, // INS_crc32
        8585, 5, // INS_tzcnt
        8591, 5, // INS_lzcnt
        8459, 5, // INS_movbe
        8597, 6, // INS_popcnt
        8604, 6, // INS_tpause
        8611, 8, // INS_umonitor
        8620, 6, // INS_umwait
        8627, 3, // INS_neg
        8631, 3, // INS_not
        8635, 3, // INS_rol
        8635, 3, // INS_rol_1
        8635, 3, // INS_rol_N
        8639, 3, // INS_ror
        8639, 3, // INS_ror_1
        8639, 3, // INS_ror_N
        8643, 3, // INS_rcl
        8643, 3, // INS_rcl_1
        8643, 3, // INS_rcl_N
        8647, 3, // INS_rcr
        8647, 3, // INS_rcr_1
        8647, 3, // INS_rcr_N
        8651, 3, // INS_shl
        8651, 3, // INS_shl_1
        8651, 3, // INS_shl_N
        8655, 3, // INS_shr
        8655, 3, // INS_shr_1
        8655, 3, // INS_shr_N
        8659, 3, // INS_sar
        8659, 3, // INS_sar_1
        8659, 3, // INS_sar_N
        8663, 3, // INS_ret
        8667, 4, // INS_loop
        8672, 4, // INS_call
        8677, 9, // INS_r_movsb
        8687, 9, // INS_r_movsd
        8707, 5, // INS_movsb
        8713, 5, // INS_movsd
        8725, 9, // INS_r_stosb
        8735, 9, // INS_r_stosd
        8755, 5, // INS_stosb
        8761, 5, // INS_stosd
        8773, 4, // INS_int3
        8778, 3, // INS_nop
        8782, 5, // INS_pause
        8788, 4, // INS_lock
        8793, 5, // INS_leave
        8799, 9, // INS_serialize
        8809, 4, // INS_cwde
        8814, 3, // INS_cdq
        8818, 4, // INS_idiv
        237, 4, // INS_imulEAX
        8823, 3, // INS_div
        8827, 3, // INS_mulEAX
        8831, 4, // INS_sahf
        8836, 4, // INS_xadd
        8841, 7, // INS_cmpxchg
        8849, 4, // INS_shld
        8854, 4, // INS_shrd
        8859, 3, // INS_fld
        8863, 4, // INS_fstp
        8868, 4, // INS_seto
        8873, 5, // INS_setno
        8879, 4, // INS_setb
        8884, 5, // INS_setae
        8890, 4, // INS_sete
        8895, 5, // INS_setne
        8901, 5, // INS_setbe
        8907, 4, // INS_seta
        8912, 4, // INS_sets
        8917, 5, // INS_setns
        8923, 4, // INS_setp
        8928, 5, // INS_setnp
        8934, 4, // INS_setl
        8939, 5, // INS_setge
        8945, 5, // INS_setle
        8951, 4, // INS_setg
        8956, 8, // INS_tail_i_jmp
        8965, 3, // INS_i_jmp
        8965, 3, // INS_jmp
        8969, 2, // INS_jo
        8972, 3, // INS_jno
        8976, 2, // INS_jb
        8979, 3, // INS_jae
        8983, 2, // INS_je
        8986, 3, // INS_jne
        8990, 3, // INS_jbe
        8994, 2, // INS_ja
        8997, 2, // INS_js
        9000, 3, // INS_jns
        9004, 2, // INS_jp
        9007, 3, // INS_jnp
        9011, 2, // INS_jl
        9014, 3, // INS_jge
        9018, 3, // INS_jle
        9022, 2, // INS_jg
        8965, 3, // INS_l_jmp
        8969, 2, // INS_l_jo
        8972, 3, // INS_l_jno
        8976, 2, // INS_l_jb
        8979, 3, // INS_l_jae
        8983, 2, // INS_l_je
        8986, 3, // INS_l_jne
        8990, 3, // INS_l_jbe
        8994, 2, // INS_l_ja
        8997, 2, // INS_l_js
        9000, 3, // INS_l_jns
        9004, 2, // INS_l_jp
        9007, 3, // INS_l_jnp
        9011, 2, // INS_l_jl
        9014, 3, // INS_l_jge
        9018, 3, // INS_l_jle
        9022, 2, // INS_l_jg
        9025, 5, // INS_align
        9031, 6, // INS_data16
    ];

    private static ReadOnlySpan<int> s_instructionNameRangesAmd64 => [
        0, 7, // INS_invalid
        8, 4, // INS_push
        13, 3, // INS_pop
        8, 4, // INS_push_hide
        13, 3, // INS_pop_hide
        17, 5, // INS_push2
        23, 4, // INS_pop2
        28, 3, // INS_inc
        28, 3, // INS_inc_l
        32, 3, // INS_dec
        32, 3, // INS_dec_l
        36, 5, // INS_bswap
        42, 3, // INS_add
        46, 2, // INS_or
        49, 3, // INS_adc
        53, 3, // INS_sbb
        57, 3, // INS_and
        61, 3, // INS_sub
        61, 3, // INS_sub_hide
        65, 3, // INS_xor
        69, 3, // INS_cmp
        73, 4, // INS_test
        78, 3, // INS_mov
        82, 3, // INS_lea
        86, 2, // INS_bt
        89, 3, // INS_bts
        93, 3, // INS_btr
        97, 3, // INS_btc
        101, 3, // INS_bsr
        105, 3, // INS_bsf
        109, 5, // INS_movsx
        115, 6, // INS_movsxd
        122, 5, // INS_movzx
        128, 5, // INS_cmovo
        134, 6, // INS_cmovno
        141, 5, // INS_cmovb
        147, 6, // INS_cmovae
        154, 5, // INS_cmove
        160, 6, // INS_cmovne
        167, 6, // INS_cmovbe
        174, 5, // INS_cmova
        180, 5, // INS_cmovs
        186, 6, // INS_cmovns
        193, 5, // INS_cmovp
        199, 6, // INS_cmovnp
        206, 5, // INS_cmovl
        212, 6, // INS_cmovge
        219, 6, // INS_cmovle
        226, 5, // INS_cmovg
        232, 4, // INS_xchg
        237, 4, // INS_imul
        237, 4, // INS_imul_AX
        237, 4, // INS_imul_CX
        237, 4, // INS_imul_DX
        237, 4, // INS_imul_BX
        237, 4, // INS_imul_SP
        237, 4, // INS_imul_BP
        237, 4, // INS_imul_SI
        237, 4, // INS_imul_DI
        237, 4, // INS_imul_08
        237, 4, // INS_imul_09
        237, 4, // INS_imul_10
        237, 4, // INS_imul_11
        237, 4, // INS_imul_12
        237, 4, // INS_imul_13
        237, 4, // INS_imul_14
        237, 4, // INS_imul_15
        237, 4, // INS_imul_16
        237, 4, // INS_imul_17
        237, 4, // INS_imul_18
        237, 4, // INS_imul_19
        237, 4, // INS_imul_20
        237, 4, // INS_imul_21
        237, 4, // INS_imul_22
        237, 4, // INS_imul_23
        237, 4, // INS_imul_24
        237, 4, // INS_imul_25
        237, 4, // INS_imul_26
        237, 4, // INS_imul_27
        237, 4, // INS_imul_28
        237, 4, // INS_imul_29
        237, 4, // INS_imul_30
        237, 4, // INS_imul_31
        242, 6, // INS_addpd
        249, 6, // INS_addps
        256, 6, // INS_addsd
        263, 6, // INS_addss
        270, 9, // INS_addsubpd
        280, 9, // INS_addsubps
        290, 7, // INS_andnpd
        298, 7, // INS_andnps
        306, 6, // INS_andpd
        313, 6, // INS_andps
        320, 8, // INS_blendpd
        329, 8, // INS_blendps
        338, 8, // INS_blendvpd
        347, 8, // INS_blendvps
        356, 6, // INS_cmppd
        363, 6, // INS_cmpps
        370, 6, // INS_cmpsd
        377, 6, // INS_cmpss
        384, 7, // INS_comisd
        392, 7, // INS_comiss
        400, 9, // INS_cvtdq2pd
        410, 9, // INS_cvtdq2ps
        420, 9, // INS_cvtpd2dq
        430, 9, // INS_cvtpd2ps
        440, 9, // INS_cvtps2dq
        450, 9, // INS_cvtps2pd
        460, 9, // INS_cvtsd2si32
        460, 9, // INS_cvtsd2si64
        470, 9, // INS_cvtsd2ss
        480, 9, // INS_cvtsi2sd32
        480, 9, // INS_cvtsi2sd64
        490, 9, // INS_cvtsi2ss32
        490, 9, // INS_cvtsi2ss64
        500, 9, // INS_cvtss2sd
        510, 9, // INS_cvtss2si32
        510, 9, // INS_cvtss2si64
        520, 10, // INS_cvttpd2dq
        531, 10, // INS_cvttps2dq
        542, 10, // INS_cvttsd2si32
        542, 10, // INS_cvttsd2si64
        553, 10, // INS_cvttss2si32
        553, 10, // INS_cvttss2si64
        564, 6, // INS_divpd
        571, 6, // INS_divps
        578, 6, // INS_divsd
        585, 6, // INS_divss
        592, 5, // INS_dppd
        598, 5, // INS_dpps
        604, 10, // INS_extractps
        615, 7, // INS_haddpd
        623, 7, // INS_haddps
        631, 7, // INS_hsubpd
        639, 7, // INS_hsubps
        647, 9, // INS_insertps
        657, 6, // INS_lddqu
        664, 6, // INS_lfence
        671, 11, // INS_maskmovdqu
        683, 6, // INS_maxpd
        690, 6, // INS_maxps
        697, 6, // INS_maxsd
        704, 6, // INS_maxss
        711, 6, // INS_mfence
        718, 6, // INS_minpd
        725, 6, // INS_minps
        732, 6, // INS_minsd
        739, 6, // INS_minss
        746, 7, // INS_movapd
        754, 7, // INS_movaps
        762, 5, // INS_movd32
        768, 5, // INS_movd64
        774, 8, // INS_movddup
        783, 7, // INS_movdqa32
        791, 7, // INS_movdqu32
        799, 8, // INS_movhlps
        808, 7, // INS_movhpd
        816, 7, // INS_movhps
        824, 8, // INS_movlhps
        833, 7, // INS_movlpd
        841, 7, // INS_movlps
        849, 9, // INS_movmskpd
        859, 9, // INS_movmskps
        869, 8, // INS_movntdq
        878, 9, // INS_movntdqa
        888, 6, // INS_movnti32
        888, 6, // INS_movnti64
        895, 8, // INS_movntpd
        904, 8, // INS_movntps
        768, 5, // INS_movq
        913, 6, // INS_movsd_simd
        920, 9, // INS_movshdup
        930, 9, // INS_movsldup
        940, 6, // INS_movss
        947, 7, // INS_movupd
        955, 7, // INS_movups
        963, 8, // INS_mpsadbw
        972, 6, // INS_mulpd
        979, 6, // INS_mulps
        986, 6, // INS_mulsd
        993, 6, // INS_mulss
        1000, 5, // INS_orpd
        1006, 5, // INS_orps
        1012, 6, // INS_pabsb
        1019, 6, // INS_pabsd
        1026, 6, // INS_pabsw
        1033, 9, // INS_packssdw
        1043, 9, // INS_packsswb
        1053, 9, // INS_packusdw
        1063, 9, // INS_packuswb
        1073, 6, // INS_paddb
        1080, 6, // INS_paddd
        1087, 6, // INS_paddq
        1094, 7, // INS_paddsb
        1102, 7, // INS_paddsw
        1110, 8, // INS_paddusb
        1119, 8, // INS_paddusw
        1128, 6, // INS_paddw
        1135, 8, // INS_palignr
        1144, 5, // INS_pandd
        1150, 6, // INS_pandnd
        1157, 6, // INS_pavgb
        1164, 6, // INS_pavgw
        1171, 8, // INS_pblendvb
        1180, 8, // INS_pblendw
        1189, 8, // INS_pcmpeqb
        1198, 8, // INS_pcmpeqd
        1207, 8, // INS_pcmpeqq
        1216, 8, // INS_pcmpeqw
        1225, 8, // INS_pcmpgtb
        1234, 8, // INS_pcmpgtd
        1243, 8, // INS_pcmpgtq
        1252, 8, // INS_pcmpgtw
        1261, 7, // INS_pextrb
        1269, 7, // INS_pextrd
        1277, 7, // INS_pextrq
        1285, 7, // INS_phaddd
        1293, 7, // INS_pextrw
        1301, 8, // INS_phaddsw
        1310, 7, // INS_phaddw
        1318, 11, // INS_phminposuw
        1330, 7, // INS_phsubd
        1338, 8, // INS_phsubsw
        1347, 7, // INS_phsubw
        1355, 7, // INS_pinsrb
        1363, 7, // INS_pinsrd
        1371, 7, // INS_pinsrq
        1379, 7, // INS_pinsrw
        1387, 10, // INS_pmaddubsw
        1398, 8, // INS_pmaddwd
        1407, 7, // INS_pmaxsb
        1415, 7, // INS_pmaxsd
        1423, 7, // INS_pmaxsw
        1431, 7, // INS_pmaxub
        1439, 7, // INS_pmaxud
        1447, 7, // INS_pmaxuw
        1455, 7, // INS_pminsb
        1463, 7, // INS_pminsd
        1471, 7, // INS_pminsw
        1479, 7, // INS_pminub
        1487, 7, // INS_pminud
        1495, 7, // INS_pminuw
        1503, 9, // INS_pmovmskb
        1513, 9, // INS_pmovsxbd
        1523, 9, // INS_pmovsxbq
        1533, 9, // INS_pmovsxbw
        1543, 9, // INS_pmovsxdq
        1553, 9, // INS_pmovsxwd
        1563, 9, // INS_pmovsxwq
        1573, 9, // INS_pmovzxbd
        1583, 9, // INS_pmovzxbq
        1593, 9, // INS_pmovzxbw
        1603, 9, // INS_pmovzxdq
        1613, 9, // INS_pmovzxwd
        1623, 9, // INS_pmovzxwq
        1633, 7, // INS_pmuldq
        1641, 9, // INS_pmulhrsw
        1651, 8, // INS_pmulhuw
        1660, 7, // INS_pmulhw
        1668, 7, // INS_pmulld
        1676, 8, // INS_pmuludq
        1685, 7, // INS_pmullw
        1693, 4, // INS_pord
        1698, 11, // INS_prefetchnta
        1710, 10, // INS_prefetcht0
        1721, 10, // INS_prefetcht1
        1732, 10, // INS_prefetcht2
        1743, 7, // INS_psadbw
        1751, 7, // INS_pshufb
        1759, 7, // INS_pshufd
        1767, 8, // INS_pshufhw
        1776, 8, // INS_pshuflw
        1785, 7, // INS_psignb
        1793, 7, // INS_psignd
        1801, 7, // INS_psignw
        1809, 6, // INS_pslld
        1816, 7, // INS_pslldq
        1824, 6, // INS_psllq
        1831, 6, // INS_psllw
        1838, 6, // INS_psrad
        1845, 6, // INS_psraw
        1852, 6, // INS_psrld
        1859, 7, // INS_psrldq
        1867, 6, // INS_psrlq
        1874, 6, // INS_psrlw
        1881, 6, // INS_psubb
        1888, 6, // INS_psubd
        1895, 6, // INS_psubq
        1902, 7, // INS_psubsb
        1910, 7, // INS_psubsw
        1918, 8, // INS_psubusb
        1927, 8, // INS_psubusw
        1936, 6, // INS_psubw
        1943, 6, // INS_ptest
        1950, 10, // INS_punpckhbw
        1961, 10, // INS_punpckhdq
        1972, 11, // INS_punpckhqdq
        1984, 10, // INS_punpckhwd
        1995, 10, // INS_punpcklbw
        2006, 10, // INS_punpckldq
        2017, 11, // INS_punpcklqdq
        2029, 10, // INS_punpcklwd
        2040, 5, // INS_pxord
        2046, 6, // INS_rcpps
        2053, 6, // INS_rcpss
        2060, 8, // INS_roundpd
        2069, 8, // INS_roundps
        2078, 8, // INS_roundsd
        2087, 8, // INS_roundss
        2096, 8, // INS_rsqrtps
        2105, 8, // INS_rsqrtss
        2114, 6, // INS_sfence
        2121, 7, // INS_shufpd
        2129, 7, // INS_shufps
        2137, 7, // INS_sqrtpd
        2145, 7, // INS_sqrtps
        2153, 7, // INS_sqrtsd
        2161, 7, // INS_sqrtss
        2169, 6, // INS_subpd
        2176, 6, // INS_subps
        2183, 6, // INS_subsd
        2190, 6, // INS_subss
        2197, 8, // INS_ucomisd
        2206, 8, // INS_ucomiss
        2215, 9, // INS_unpckhpd
        2225, 9, // INS_unpckhps
        2235, 9, // INS_unpcklpd
        2245, 9, // INS_unpcklps
        2255, 6, // INS_xorpd
        2262, 6, // INS_xorps
        2269, 7, // INS_aesdec
        2277, 11, // INS_aesdeclast
        2289, 7, // INS_aesenc
        2297, 11, // INS_aesenclast
        2309, 7, // INS_aesimc
        2317, 16, // INS_aeskeygenassist
        2334, 10, // INS_pclmulqdq
        2345, 8, // INS_sha1msg1
        2354, 8, // INS_sha1msg2
        2363, 9, // INS_sha1nexte
        2373, 9, // INS_sha1rnds4
        2383, 10, // INS_sha256msg1
        2394, 10, // INS_sha256msg2
        2405, 11, // INS_sha256rnds2
        2417, 17, // INS_gf2p8affineinvqb
        2435, 14, // INS_gf2p8affineqb
        2450, 10, // INS_gf2p8mulb
        2461, 9, // INS_vblendvpd
        2471, 9, // INS_vblendvps
        2481, 14, // INS_vbroadcastf32x4
        2496, 12, // INS_vbroadcastsd
        2509, 12, // INS_vbroadcastss
        2522, 12, // INS_vextractf32x4
        2535, 11, // INS_vinsertf32x4
        2547, 10, // INS_vmaskmovpd
        2558, 10, // INS_vmaskmovps
        2569, 9, // INS_vpblendvb
        2579, 10, // INS_vperm2f128
        2590, 9, // INS_vpermilpd
        2590, 9, // INS_vpermilpdvar
        2600, 9, // INS_vpermilps
        2600, 9, // INS_vpermilpsvar
        2610, 7, // INS_vtestpd
        2618, 7, // INS_vtestps
        2626, 10, // INS_vzeroupper
        2637, 14, // INS_vbroadcasti32x4
        2652, 9, // INS_vcvtph2ps
        2662, 9, // INS_vcvtps2ph
        2672, 12, // INS_vextracti32x4
        2685, 10, // INS_vgatherdpd
        2696, 10, // INS_vgatherdps
        2707, 10, // INS_vgatherqpd
        2718, 10, // INS_vgatherqps
        2729, 11, // INS_vinserti32x4
        2741, 8, // INS_vpblendd
        2750, 12, // INS_vpbroadcastb
        2763, 12, // INS_vpbroadcastd
        2776, 12, // INS_vpbroadcastq
        2789, 12, // INS_vpbroadcastw
        2802, 10, // INS_vperm2i128
        2813, 6, // INS_vpermd
        2820, 7, // INS_vpermpd
        2828, 7, // INS_vpermps
        2836, 6, // INS_vpermq
        2843, 10, // INS_vpgatherdd
        2854, 10, // INS_vpgatherdq
        2865, 10, // INS_vpgatherqd
        2876, 10, // INS_vpgatherqq
        2887, 10, // INS_vpmaskmovd
        2898, 10, // INS_vpmaskmovq
        2909, 7, // INS_vpsllvd
        2917, 7, // INS_vpsllvq
        2925, 7, // INS_vpsravd
        2933, 7, // INS_vpsrlvd
        2941, 7, // INS_vpsrlvq
        2949, 11, // INS_vfmadd132pd
        2961, 11, // INS_vfmadd213pd
        2973, 11, // INS_vfmadd231pd
        2985, 11, // INS_vfmadd132ps
        2997, 11, // INS_vfmadd213ps
        3009, 11, // INS_vfmadd231ps
        3021, 11, // INS_vfmadd132sd
        3033, 11, // INS_vfmadd213sd
        3045, 11, // INS_vfmadd231sd
        3057, 11, // INS_vfmadd132ss
        3069, 11, // INS_vfmadd213ss
        3081, 11, // INS_vfmadd231ss
        3093, 14, // INS_vfmaddsub132pd
        3108, 14, // INS_vfmaddsub213pd
        3123, 14, // INS_vfmaddsub231pd
        3138, 14, // INS_vfmaddsub132ps
        3153, 14, // INS_vfmaddsub213ps
        3168, 14, // INS_vfmaddsub231ps
        3183, 14, // INS_vfmsubadd132pd
        3198, 14, // INS_vfmsubadd213pd
        3213, 14, // INS_vfmsubadd231pd
        3228, 14, // INS_vfmsubadd132ps
        3243, 14, // INS_vfmsubadd213ps
        3258, 14, // INS_vfmsubadd231ps
        3273, 11, // INS_vfmsub132pd
        3285, 11, // INS_vfmsub213pd
        3297, 11, // INS_vfmsub231pd
        3309, 11, // INS_vfmsub132ps
        3321, 11, // INS_vfmsub213ps
        3333, 11, // INS_vfmsub231ps
        3345, 11, // INS_vfmsub132sd
        3357, 11, // INS_vfmsub213sd
        3369, 11, // INS_vfmsub231sd
        3381, 11, // INS_vfmsub132ss
        3393, 11, // INS_vfmsub213ss
        3405, 11, // INS_vfmsub231ss
        3417, 12, // INS_vfnmadd132pd
        3430, 12, // INS_vfnmadd213pd
        3443, 12, // INS_vfnmadd231pd
        3456, 12, // INS_vfnmadd132ps
        3469, 12, // INS_vfnmadd213ps
        3482, 12, // INS_vfnmadd231ps
        3495, 12, // INS_vfnmadd132sd
        3508, 12, // INS_vfnmadd213sd
        3521, 12, // INS_vfnmadd231sd
        3534, 12, // INS_vfnmadd132ss
        3547, 12, // INS_vfnmadd213ss
        3560, 12, // INS_vfnmadd231ss
        3573, 12, // INS_vfnmsub132pd
        3586, 12, // INS_vfnmsub213pd
        3599, 12, // INS_vfnmsub231pd
        3612, 12, // INS_vfnmsub132ps
        3625, 12, // INS_vfnmsub213ps
        3638, 12, // INS_vfnmsub231ps
        3651, 12, // INS_vfnmsub132sd
        3664, 12, // INS_vfnmsub213sd
        3677, 12, // INS_vfnmsub231sd
        3690, 12, // INS_vfnmsub132ss
        3703, 12, // INS_vfnmsub213ss
        3716, 12, // INS_vfnmsub231ss
        3729, 4, // INS_andn
        3734, 5, // INS_bextr
        3740, 4, // INS_blsi
        3745, 6, // INS_blsmsk
        3752, 4, // INS_blsr
        3757, 4, // INS_bzhi
        3762, 4, // INS_mulx
        3767, 4, // INS_pdep
        3772, 4, // INS_pext
        3777, 4, // INS_rorx
        3782, 4, // INS_sarx
        3787, 4, // INS_shlx
        3792, 4, // INS_shrx
        3797, 8, // INS_vpdpbusd
        3806, 9, // INS_vpdpbusds
        3816, 8, // INS_vpdpwssd
        3825, 9, // INS_vpdpwssds
        3835, 8, // INS_vpdpwsud
        3844, 9, // INS_vpdpwsuds
        3854, 8, // INS_vpdpwusd
        3863, 9, // INS_vpdpwusds
        3873, 8, // INS_vpdpwuud
        3882, 9, // INS_vpdpwuuds
        3892, 8, // INS_vpdpbssd
        3901, 9, // INS_vpdpbssds
        3911, 8, // INS_vpdpbsud
        3920, 9, // INS_vpdpbsuds
        3930, 8, // INS_vpdpbuud
        3939, 9, // INS_vpdpbuuds
        3949, 15, // INS_vbmacor16x16x16
        3965, 16, // INS_vbmacxor16x16x16
        3982, 7, // INS_vbitrev
        3990, 11, // INS_vpmadd52huq
        4002, 11, // INS_vpmadd52luq
        4014, 5, // INS_kaddb
        4020, 5, // INS_kaddd
        4026, 5, // INS_kaddq
        4032, 5, // INS_kaddw
        4038, 5, // INS_kandb
        4044, 5, // INS_kandd
        4050, 6, // INS_kandnb
        4057, 6, // INS_kandnd
        4064, 6, // INS_kandnq
        4071, 6, // INS_kandnw
        4078, 5, // INS_kandq
        4084, 5, // INS_kandw
        4090, 5, // INS_kmovb_gpr
        4090, 5, // INS_kmovb_msk
        4096, 5, // INS_kmovd_gpr
        4096, 5, // INS_kmovd_msk
        4102, 5, // INS_kmovq_gpr
        4102, 5, // INS_kmovq_msk
        4108, 5, // INS_kmovw_gpr
        4108, 5, // INS_kmovw_msk
        4114, 5, // INS_knotb
        4120, 5, // INS_knotd
        4126, 5, // INS_knotq
        4132, 5, // INS_knotw
        4138, 4, // INS_korb
        4143, 4, // INS_kord
        4148, 4, // INS_korq
        4153, 8, // INS_kortestb
        4162, 8, // INS_kortestd
        4171, 8, // INS_kortestq
        4180, 8, // INS_kortestw
        4189, 4, // INS_korw
        4194, 8, // INS_kshiftlb
        4203, 8, // INS_kshiftld
        4212, 8, // INS_kshiftlq
        4221, 8, // INS_kshiftlw
        4230, 8, // INS_kshiftrb
        4239, 8, // INS_kshiftrd
        4248, 8, // INS_kshiftrq
        4257, 8, // INS_kshiftrw
        4266, 6, // INS_ktestb
        4273, 6, // INS_ktestd
        4280, 6, // INS_ktestq
        4287, 6, // INS_ktestw
        4294, 8, // INS_kunpckbw
        4303, 8, // INS_kunpckdq
        4312, 8, // INS_kunpckwd
        4321, 6, // INS_kxnorb
        4328, 6, // INS_kxnord
        4335, 6, // INS_kxnorq
        4342, 6, // INS_kxnorw
        4349, 5, // INS_kxorb
        4355, 5, // INS_kxord
        4361, 5, // INS_kxorq
        4367, 5, // INS_kxorw
        4373, 7, // INS_valignd
        4381, 7, // INS_valignq
        4389, 9, // INS_vblendmpd
        4399, 9, // INS_vblendmps
        4409, 15, // INS_vbroadcastf32x2
        4425, 15, // INS_vbroadcastf32x8
        4441, 15, // INS_vbroadcastf64x2
        4457, 15, // INS_vbroadcastf64x4
        4473, 15, // INS_vbroadcasti32x2
        4489, 15, // INS_vbroadcasti32x8
        4505, 15, // INS_vbroadcasti64x2
        4521, 15, // INS_vbroadcasti64x4
        356, 6, // INS_vcmppd
        363, 6, // INS_vcmpps
        370, 6, // INS_vcmpsd
        377, 6, // INS_vcmpss
        4537, 11, // INS_vcompresspd
        4549, 11, // INS_vcompressps
        4561, 9, // INS_vcvtpd2qq
        4571, 10, // INS_vcvtpd2udq
        4582, 10, // INS_vcvtpd2uqq
        4593, 9, // INS_vcvtps2qq
        4603, 10, // INS_vcvtps2udq
        4614, 10, // INS_vcvtps2uqq
        4625, 9, // INS_vcvtqq2pd
        4635, 9, // INS_vcvtqq2ps
        4645, 10, // INS_vcvtsd2usi32
        4645, 10, // INS_vcvtsd2usi64
        4656, 10, // INS_vcvtss2usi32
        4656, 10, // INS_vcvtss2usi64
        4667, 10, // INS_vcvttpd2qq
        4678, 11, // INS_vcvttpd2udq
        4690, 11, // INS_vcvttpd2uqq
        4702, 10, // INS_vcvttps2qq
        4713, 11, // INS_vcvttps2udq
        4725, 11, // INS_vcvttps2uqq
        4737, 11, // INS_vcvttsd2usi32
        4737, 11, // INS_vcvttsd2usi64
        4749, 11, // INS_vcvttss2usi32
        4749, 11, // INS_vcvttss2usi64
        4761, 10, // INS_vcvtudq2pd
        4772, 10, // INS_vcvtudq2ps
        4783, 10, // INS_vcvtuqq2pd
        4794, 10, // INS_vcvtuqq2ps
        4805, 10, // INS_vcvtusi2sd32
        4805, 10, // INS_vcvtusi2sd64
        4816, 10, // INS_vcvtusi2ss32
        4816, 10, // INS_vcvtusi2ss64
        4827, 9, // INS_vdbpsadbw
        4837, 9, // INS_vexpandpd
        4847, 9, // INS_vexpandps
        4857, 13, // INS_vextractf32x8
        4871, 13, // INS_vextractf64x2
        4885, 13, // INS_vextractf64x4
        4899, 13, // INS_vextracti32x8
        4913, 13, // INS_vextracti64x2
        4927, 13, // INS_vextracti64x4
        4941, 11, // INS_vfixupimmpd
        4953, 11, // INS_vfixupimmps
        4965, 11, // INS_vfixupimmsd
        4977, 11, // INS_vfixupimmss
        4989, 10, // INS_vfpclasspd
        5000, 10, // INS_vfpclassps
        5011, 10, // INS_vfpclasssd
        5022, 10, // INS_vfpclassss
        2685, 10, // INS_vgatherdpd_msk
        2696, 10, // INS_vgatherdps_msk
        2707, 10, // INS_vgatherqpd_msk
        2718, 10, // INS_vgatherqps_msk
        5033, 9, // INS_vgetexppd
        5043, 9, // INS_vgetexpps
        5053, 9, // INS_vgetexpsd
        5063, 9, // INS_vgetexpss
        5073, 10, // INS_vgetmantpd
        5084, 10, // INS_vgetmantps
        5095, 10, // INS_vgetmantsd
        5106, 10, // INS_vgetmantss
        5117, 12, // INS_vinsertf32x8
        5130, 12, // INS_vinsertf64x2
        5143, 12, // INS_vinsertf64x4
        5156, 12, // INS_vinserti32x8
        5169, 12, // INS_vinserti64x2
        5182, 12, // INS_vinserti64x4
        5195, 9, // INS_vmovdqa64
        5205, 9, // INS_vmovdqu16
        5215, 9, // INS_vmovdqu64
        5225, 8, // INS_vmovdqu8
        5234, 6, // INS_vpabsq
        5241, 7, // INS_vpandnq
        5249, 6, // INS_vpandq
        5256, 9, // INS_vpblendmb
        5266, 9, // INS_vpblendmd
        5276, 9, // INS_vpblendmq
        5286, 9, // INS_vpblendmw
        2750, 12, // INS_vpbroadcastb_gpr
        2763, 12, // INS_vpbroadcastd_gpr
        2776, 12, // INS_vpbroadcastq_gpr
        2789, 12, // INS_vpbroadcastw_gpr
        5296, 6, // INS_vpcmpb
        5303, 6, // INS_vpcmpd
        1189, 8, // INS_vpcmpeqb
        1198, 8, // INS_vpcmpeqd
        1207, 8, // INS_vpcmpeqq
        1216, 8, // INS_vpcmpeqw
        1225, 8, // INS_vpcmpgtb
        1234, 8, // INS_vpcmpgtd
        1243, 8, // INS_vpcmpgtq
        1252, 8, // INS_vpcmpgtw
        5310, 6, // INS_vpcmpq
        5317, 7, // INS_vpcmpub
        5325, 7, // INS_vpcmpud
        5333, 7, // INS_vpcmpuq
        5341, 7, // INS_vpcmpuw
        5349, 6, // INS_vpcmpw
        5356, 11, // INS_vpcompressd
        5368, 11, // INS_vpcompressq
        5380, 11, // INS_vpconflictd
        5392, 11, // INS_vpconflictq
        5404, 8, // INS_vpermi2d
        5413, 9, // INS_vpermi2pd
        5423, 9, // INS_vpermi2ps
        5433, 8, // INS_vpermi2q
        5442, 8, // INS_vpermi2w
        2820, 7, // INS_vpermpd_reg
        2836, 6, // INS_vpermq_reg
        5451, 8, // INS_vpermt2d
        5460, 9, // INS_vpermt2pd
        5470, 9, // INS_vpermt2ps
        5480, 8, // INS_vpermt2q
        5489, 8, // INS_vpermt2w
        5498, 6, // INS_vpermw
        5505, 9, // INS_vpexpandd
        5515, 9, // INS_vpexpandq
        2843, 10, // INS_vpgatherdd_msk
        2854, 10, // INS_vpgatherdq_msk
        2865, 10, // INS_vpgatherqd_msk
        2876, 10, // INS_vpgatherqq_msk
        5525, 8, // INS_vplzcntd
        5534, 8, // INS_vplzcntq
        5543, 7, // INS_vpmaxsq
        5551, 7, // INS_vpmaxuq
        5559, 7, // INS_vpminsq
        5567, 7, // INS_vpminuq
        5575, 8, // INS_vpmovb2m
        5584, 8, // INS_vpmovd2m
        5593, 7, // INS_vpmovdb
        5601, 7, // INS_vpmovdw
        5609, 8, // INS_vpmovm2b
        5618, 8, // INS_vpmovm2d
        5627, 8, // INS_vpmovm2q
        5636, 8, // INS_vpmovm2w
        5645, 8, // INS_vpmovq2m
        5654, 7, // INS_vpmovqb
        5662, 7, // INS_vpmovqd
        5670, 7, // INS_vpmovqw
        5678, 8, // INS_vpmovsdb
        5687, 8, // INS_vpmovsdw
        5696, 8, // INS_vpmovsqb
        5705, 8, // INS_vpmovsqd
        5714, 8, // INS_vpmovsqw
        5723, 8, // INS_vpmovswb
        5732, 9, // INS_vpmovusdb
        5742, 9, // INS_vpmovusdw
        5752, 9, // INS_vpmovusqb
        5762, 9, // INS_vpmovusqd
        5772, 9, // INS_vpmovusqw
        5782, 9, // INS_vpmovuswb
        5792, 8, // INS_vpmovw2m
        5801, 7, // INS_vpmovwb
        5809, 7, // INS_vpmullq
        5817, 5, // INS_vporq
        5823, 6, // INS_vprold
        5830, 6, // INS_vprolq
        5837, 7, // INS_vprolvd
        5845, 7, // INS_vprolvq
        5853, 6, // INS_vprord
        5860, 6, // INS_vprorq
        5867, 7, // INS_vprorvd
        5875, 7, // INS_vprorvq
        5883, 11, // INS_vpscatterdd_msk
        5895, 11, // INS_vpscatterdq_msk
        5907, 11, // INS_vpscatterqd_msk
        5919, 11, // INS_vpscatterqq_msk
        5931, 7, // INS_vpsllvw
        5939, 6, // INS_vpsraq
        5946, 7, // INS_vpsravq
        5954, 7, // INS_vpsravw
        5962, 7, // INS_vpsrlvw
        5970, 10, // INS_vpternlogd
        5981, 10, // INS_vpternlogq
        5992, 8, // INS_vptestmb
        6001, 8, // INS_vptestmd
        6010, 8, // INS_vptestmq
        6019, 8, // INS_vptestmw
        6028, 9, // INS_vptestnmb
        6038, 9, // INS_vptestnmd
        6048, 9, // INS_vptestnmq
        6058, 9, // INS_vptestnmw
        6068, 6, // INS_vpxorq
        6075, 8, // INS_vrangepd
        6084, 8, // INS_vrangeps
        6093, 8, // INS_vrangesd
        6102, 8, // INS_vrangess
        6111, 8, // INS_vrcp14pd
        6120, 8, // INS_vrcp14ps
        6129, 8, // INS_vrcp14sd
        6138, 8, // INS_vrcp14ss
        6147, 9, // INS_vreducepd
        6157, 9, // INS_vreduceps
        6167, 9, // INS_vreducesd
        6177, 9, // INS_vreducess
        6187, 11, // INS_vrndscalepd
        6199, 11, // INS_vrndscaleps
        6211, 11, // INS_vrndscalesd
        6223, 11, // INS_vrndscaless
        6235, 10, // INS_vrsqrt14pd
        6246, 10, // INS_vrsqrt14ps
        6257, 10, // INS_vrsqrt14sd
        6268, 10, // INS_vrsqrt14ss
        6279, 9, // INS_vscalefpd
        6289, 9, // INS_vscalefps
        6299, 9, // INS_vscalefsd
        6309, 9, // INS_vscalefss
        6319, 11, // INS_vscatterdpd_msk
        6331, 11, // INS_vscatterdps_msk
        6343, 11, // INS_vscatterqpd_msk
        6355, 11, // INS_vscatterqps_msk
        6367, 10, // INS_vshuff32x4
        6378, 10, // INS_vshuff64x2
        6389, 10, // INS_vshufi32x4
        6400, 10, // INS_vshufi64x2
        6411, 6, // INS_vpermb
        6418, 8, // INS_vpermi2b
        6427, 8, // INS_vpermt2b
        6436, 14, // INS_vpmultishiftqb
        6451, 11, // INS_vpcompressb
        6463, 11, // INS_vpcompressw
        6475, 9, // INS_vpexpandb
        6485, 9, // INS_vpexpandw
        6495, 8, // INS_vpopcntb
        6504, 8, // INS_vpopcntd
        6513, 8, // INS_vpopcntq
        6522, 8, // INS_vpopcntw
        6531, 7, // INS_vpshldd
        6539, 7, // INS_vpshldq
        6547, 8, // INS_vpshldvd
        6556, 8, // INS_vpshldvq
        6565, 8, // INS_vpshldvw
        6574, 7, // INS_vpshldw
        6582, 7, // INS_vpshrdd
        6590, 7, // INS_vpshrdq
        6598, 8, // INS_vpshrdvd
        6607, 8, // INS_vpshrdvq
        6616, 8, // INS_vpshrdvw
        6625, 7, // INS_vpshrdw
        6633, 12, // INS_vpshufbitqmb
        6646, 6, // INS_vaddph
        6653, 6, // INS_vaddsh
        6660, 6, // INS_vcmpph
        6667, 6, // INS_vcmpsh
        6674, 7, // INS_vcomish
        6682, 9, // INS_vcvtdq2ph
        6692, 14, // INS_vcvtne2ps2bf16
        6707, 13, // INS_vcvtneps2bf16
        6721, 9, // INS_vcvtpd2ph
        6731, 9, // INS_vcvtph2dq
        6741, 9, // INS_vcvtph2pd
        6751, 10, // INS_vcvtph2psx
        6762, 9, // INS_vcvtph2qq
        6772, 10, // INS_vcvtph2udq
        6783, 10, // INS_vcvtph2uqq
        6794, 9, // INS_vcvtph2uw
        6804, 8, // INS_vcvtph2w
        6813, 10, // INS_vcvtps2phx
        6824, 9, // INS_vcvtqq2ph
        6834, 9, // INS_vcvtsd2sh
        6844, 9, // INS_vcvtsh2sd
        6854, 9, // INS_vcvtsh2si32
        6854, 9, // INS_vcvtsh2si64
        6864, 9, // INS_vcvtsh2ss
        6874, 10, // INS_vcvtsh2usi32
        6874, 10, // INS_vcvtsh2usi64
        6885, 9, // INS_vcvtsi2sh32
        6885, 9, // INS_vcvtsi2sh64
        6895, 9, // INS_vcvtss2sh
        6905, 10, // INS_vcvttph2dq
        6916, 10, // INS_vcvttph2qq
        6927, 11, // INS_vcvttph2udq
        6939, 11, // INS_vcvttph2uqq
        6951, 10, // INS_vcvttph2uw
        6962, 9, // INS_vcvttph2w
        6972, 10, // INS_vcvttsh2si32
        6972, 10, // INS_vcvttsh2si64
        6983, 11, // INS_vcvttsh2usi32
        6983, 11, // INS_vcvttsh2usi64
        6995, 10, // INS_vcvtudq2ph
        7006, 10, // INS_vcvtuqq2ph
        7017, 10, // INS_vcvtusi2sh32
        7017, 10, // INS_vcvtusi2sh64
        7028, 9, // INS_vcvtuw2ph
        7038, 8, // INS_vcvtw2ph
        7047, 6, // INS_vdivph
        7054, 6, // INS_vdivsh
        7061, 9, // INS_vdpbf16ps
        7071, 10, // INS_vfcmaddcph
        7082, 10, // INS_vfcmaddcsh
        7093, 9, // INS_vfcmulcph
        7103, 9, // INS_vfcmulcsh
        7113, 8, // INS_vfmulcph
        7122, 8, // INS_vfmulcsh
        7131, 11, // INS_vfmadd132ph
        7143, 11, // INS_vfmadd213ph
        7155, 11, // INS_vfmadd231ph
        7167, 11, // INS_vfmadd132sh
        7179, 11, // INS_vfmadd213sh
        7191, 11, // INS_vfmadd231sh
        7203, 9, // INS_vfmaddcph
        7213, 9, // INS_vfmaddcsh
        7223, 14, // INS_vfmaddsub132ph
        7238, 14, // INS_vfmaddsub213ph
        7253, 14, // INS_vfmaddsub231ph
        7268, 11, // INS_vfmsub132ph
        7280, 11, // INS_vfmsub213ph
        7292, 11, // INS_vfmsub231ph
        7304, 11, // INS_vfmsub132sh
        7316, 11, // INS_vfmsub213sh
        7328, 11, // INS_vfmsub231sh
        7340, 14, // INS_vfmsubadd132ph
        7355, 14, // INS_vfmsubadd213ph
        7370, 14, // INS_vfmsubadd231ph
        7385, 12, // INS_vfnmadd132ph
        7398, 12, // INS_vfnmadd213ph
        7411, 12, // INS_vfnmadd231ph
        7424, 12, // INS_vfnmadd132sh
        7437, 12, // INS_vfnmadd213sh
        7450, 12, // INS_vfnmadd231sh
        7463, 12, // INS_vfnmsub132ph
        7476, 12, // INS_vfnmsub213ph
        7489, 12, // INS_vfnmsub231ph
        7502, 12, // INS_vfnmsub132sh
        7515, 12, // INS_vfnmsub213sh
        7528, 12, // INS_vfnmsub231sh
        7541, 10, // INS_vfpclassph
        7552, 10, // INS_vfpclasssh
        7563, 9, // INS_vgetexpph
        7573, 9, // INS_vgetexpsh
        7583, 10, // INS_vgetmantph
        7594, 10, // INS_vgetmantsh
        7605, 6, // INS_vmaxph
        7612, 6, // INS_vmaxsh
        7619, 6, // INS_vminsh
        7626, 6, // INS_vminph
        7633, 6, // INS_vmovsh
        7640, 5, // INS_vmovw
        7646, 6, // INS_vmulph
        7653, 6, // INS_vmulsh
        7660, 6, // INS_vrcpph
        7667, 6, // INS_vrcpsh
        7674, 9, // INS_vreduceph
        7684, 9, // INS_vreducesh
        7694, 11, // INS_vrndscaleph
        7706, 11, // INS_vrndscalesh
        7718, 8, // INS_vrsqrtph
        7727, 8, // INS_vrsqrtsh
        7736, 9, // INS_vscalefph
        7746, 9, // INS_vscalefsh
        7756, 7, // INS_vsqrtph
        7764, 7, // INS_vsqrtsh
        7772, 6, // INS_vsubph
        7779, 6, // INS_vsubsh
        7786, 8, // INS_vucomish
        7795, 13, // INS_vp2intersectd
        7809, 13, // INS_vp2intersectq
        7823, 7, // INS_vcomxsd
        7831, 7, // INS_vcomxss
        7839, 10, // INS_vcvtps2ibs
        7850, 11, // INS_vcvtps2iubs
        7862, 11, // INS_vcvttpd2dqs
        7874, 11, // INS_vcvttpd2qqs
        7886, 12, // INS_vcvttpd2udqs
        7899, 12, // INS_vcvttpd2uqqs
        7912, 11, // INS_vcvttps2dqs
        7924, 11, // INS_vcvttps2ibs
        7936, 12, // INS_vcvttps2iubs
        7949, 11, // INS_vcvttps2qqs
        7961, 12, // INS_vcvttps2udqs
        7974, 12, // INS_vcvttps2uqqs
        7987, 11, // INS_vcvttsd2sis32
        7987, 11, // INS_vcvttsd2sis64
        7999, 12, // INS_vcvttsd2usis32
        7999, 12, // INS_vcvttsd2usis64
        8012, 11, // INS_vcvttss2sis32
        8012, 11, // INS_vcvttss2sis64
        8024, 12, // INS_vcvttss2usis32
        8024, 12, // INS_vcvttss2usis64
        8037, 9, // INS_vminmaxpd
        8047, 9, // INS_vminmaxps
        8057, 9, // INS_vminmaxsd
        8067, 9, // INS_vminmaxss
        762, 5, // INS_vmovd_simd
        7640, 5, // INS_vmovw_simd
        963, 8, // INS_vmpsadbw
        8077, 8, // INS_vucomxsd
        8086, 8, // INS_vucomxss
        8095, 5, // INS_ccmpo
        8101, 6, // INS_ccmpno
        8108, 5, // INS_ccmpb
        8114, 6, // INS_ccmpae
        8121, 5, // INS_ccmpe
        8127, 6, // INS_ccmpne
        8134, 6, // INS_ccmpbe
        8141, 5, // INS_ccmpa
        8147, 5, // INS_ccmps
        8153, 6, // INS_ccmpns
        8160, 5, // INS_ccmpt
        8166, 5, // INS_ccmpf
        8172, 5, // INS_ccmpl
        8178, 6, // INS_ccmpge
        8185, 6, // INS_ccmple
        8192, 5, // INS_ccmpg
        8198, 7, // INS_cfcmovo
        8206, 8, // INS_cfcmovno
        8215, 7, // INS_cfcmovb
        8223, 8, // INS_cfcmovae
        8232, 7, // INS_cfcmove
        8240, 8, // INS_cfcmovne
        8249, 8, // INS_cfcmovbe
        8258, 7, // INS_cfcmova
        8266, 7, // INS_cfcmovs
        8274, 8, // INS_cfcmovns
        8283, 7, // INS_cfcmovp
        8291, 8, // INS_cfcmovnp
        8300, 7, // INS_cfcmovl
        8308, 8, // INS_cfcmovge
        8317, 8, // INS_cfcmovle
        8326, 7, // INS_cfcmovg
        8334, 6, // INS_ctesto
        8341, 7, // INS_ctestno
        8349, 6, // INS_ctestb
        8356, 7, // INS_ctestae
        8364, 6, // INS_cteste
        8371, 7, // INS_ctestne
        8379, 7, // INS_ctestbe
        8387, 6, // INS_ctesta
        8394, 6, // INS_ctests
        8401, 7, // INS_ctestns
        8409, 6, // INS_ctestt
        8416, 6, // INS_ctestf
        8423, 6, // INS_ctestl
        8430, 7, // INS_ctestge
        8438, 7, // INS_ctestle
        8446, 6, // INS_ctestg
        8453, 5, // INS_crc32_apx
        8459, 5, // INS_movbe_apx
        8465, 6, // INS_seto_apx
        8472, 7, // INS_setno_apx
        8480, 6, // INS_setb_apx
        8487, 7, // INS_setae_apx
        8495, 6, // INS_sete_apx
        8502, 7, // INS_setne_apx
        8510, 7, // INS_setbe_apx
        8518, 6, // INS_seta_apx
        8525, 6, // INS_sets_apx
        8532, 7, // INS_setns_apx
        8540, 6, // INS_setp_apx
        8547, 7, // INS_setnp_apx
        8555, 6, // INS_setl_apx
        8562, 7, // INS_setge_apx
        8570, 7, // INS_setle_apx
        8578, 6, // INS_setg_apx
        8453, 5, // INS_crc32
        8585, 5, // INS_tzcnt
        8585, 5, // INS_tzcnt_apx
        8591, 5, // INS_lzcnt
        8591, 5, // INS_lzcnt_apx
        8459, 5, // INS_movbe
        8597, 6, // INS_popcnt
        8597, 6, // INS_popcnt_apx
        8604, 6, // INS_tpause
        8611, 8, // INS_umonitor
        8620, 6, // INS_umwait
        8627, 3, // INS_neg
        8631, 3, // INS_not
        8635, 3, // INS_rol
        8635, 3, // INS_rol_1
        8635, 3, // INS_rol_N
        8639, 3, // INS_ror
        8639, 3, // INS_ror_1
        8639, 3, // INS_ror_N
        8643, 3, // INS_rcl
        8643, 3, // INS_rcl_1
        8643, 3, // INS_rcl_N
        8647, 3, // INS_rcr
        8647, 3, // INS_rcr_1
        8647, 3, // INS_rcr_N
        8651, 3, // INS_shl
        8651, 3, // INS_shl_1
        8651, 3, // INS_shl_N
        8655, 3, // INS_shr
        8655, 3, // INS_shr_1
        8655, 3, // INS_shr_N
        8659, 3, // INS_sar
        8659, 3, // INS_sar_1
        8659, 3, // INS_sar_N
        8663, 3, // INS_ret
        8667, 4, // INS_loop
        8672, 4, // INS_call
        8677, 9, // INS_r_movsb
        8687, 9, // INS_r_movsd
        8697, 9, // INS_r_movsq
        8707, 5, // INS_movsb
        8713, 5, // INS_movsd
        8719, 5, // INS_movsq
        8725, 9, // INS_r_stosb
        8735, 9, // INS_r_stosd
        8745, 9, // INS_r_stosq
        8755, 5, // INS_stosb
        8761, 5, // INS_stosd
        8767, 5, // INS_stosq
        8773, 4, // INS_int3
        8778, 3, // INS_nop
        8782, 5, // INS_pause
        8788, 4, // INS_lock
        8793, 5, // INS_leave
        8799, 9, // INS_serialize
        8809, 4, // INS_cwde
        8814, 3, // INS_cdq
        8818, 4, // INS_idiv
        237, 4, // INS_imulEAX
        8823, 3, // INS_div
        8827, 3, // INS_mulEAX
        8831, 4, // INS_sahf
        8836, 4, // INS_xadd
        8841, 7, // INS_cmpxchg
        8849, 4, // INS_shld
        8854, 4, // INS_shrd
        8868, 4, // INS_seto
        8873, 5, // INS_setno
        8879, 4, // INS_setb
        8884, 5, // INS_setae
        8890, 4, // INS_sete
        8895, 5, // INS_setne
        8901, 5, // INS_setbe
        8907, 4, // INS_seta
        8912, 4, // INS_sets
        8917, 5, // INS_setns
        8923, 4, // INS_setp
        8928, 5, // INS_setnp
        8934, 4, // INS_setl
        8939, 5, // INS_setge
        8945, 5, // INS_setle
        8951, 4, // INS_setg
        8956, 8, // INS_tail_i_jmp
        8965, 3, // INS_i_jmp
        8965, 3, // INS_jmp
        8969, 2, // INS_jo
        8972, 3, // INS_jno
        8976, 2, // INS_jb
        8979, 3, // INS_jae
        8983, 2, // INS_je
        8986, 3, // INS_jne
        8990, 3, // INS_jbe
        8994, 2, // INS_ja
        8997, 2, // INS_js
        9000, 3, // INS_jns
        9004, 2, // INS_jp
        9007, 3, // INS_jnp
        9011, 2, // INS_jl
        9014, 3, // INS_jge
        9018, 3, // INS_jle
        9022, 2, // INS_jg
        8965, 3, // INS_l_jmp
        8969, 2, // INS_l_jo
        8972, 3, // INS_l_jno
        8976, 2, // INS_l_jb
        8979, 3, // INS_l_jae
        8983, 2, // INS_l_je
        8986, 3, // INS_l_jne
        8990, 3, // INS_l_jbe
        8994, 2, // INS_l_ja
        8997, 2, // INS_l_js
        9000, 3, // INS_l_jns
        9004, 2, // INS_l_jp
        9007, 3, // INS_l_jnp
        9011, 2, // INS_l_jl
        9014, 3, // INS_l_jge
        9018, 3, // INS_l_jle
        9022, 2, // INS_l_jg
        9025, 5, // INS_align
        9031, 6, // INS_data16
    ];

    private static ReadOnlySpan<int> s_instructionNameRangesArm => [
        0, 7, // INS_invalid
        8, 3, // INS_add
        12, 3, // INS_sub
        16, 3, // INS_ldr
        20, 3, // INS_str
        24, 4, // INS_ldrb
        29, 4, // INS_strb
        34, 4, // INS_ldrh
        39, 4, // INS_strh
        44, 5, // INS_ldrsb
        50, 5, // INS_ldrsh
        56, 3, // INS_mov
        60, 3, // INS_cmp
        64, 3, // INS_lsl
        68, 3, // INS_lsr
        72, 3, // INS_asr
        76, 3, // INS_ror
        80, 3, // INS_pld
        84, 4, // INS_pldw
        93, 4, // INS_movt
        98, 4, // INS_movw
        103, 3, // INS_and
        107, 3, // INS_eor
        111, 3, // INS_orr
        115, 3, // INS_orn
        119, 3, // INS_bic
        123, 3, // INS_adc
        127, 3, // INS_sbc
        131, 3, // INS_rsb
        135, 3, // INS_tst
        139, 3, // INS_teq
        143, 3, // INS_cmn
        147, 3, // INS_mvn
        151, 4, // INS_push
        156, 3, // INS_pop
        160, 1, // INS_b
        162, 3, // INS_beq
        166, 3, // INS_bne
        170, 3, // INS_bhs
        174, 3, // INS_blo
        178, 3, // INS_bmi
        182, 3, // INS_bpl
        186, 3, // INS_bvs
        190, 3, // INS_bvc
        194, 3, // INS_bhi
        198, 3, // INS_bls
        202, 3, // INS_bge
        206, 3, // INS_blt
        210, 3, // INS_bgt
        214, 3, // INS_ble
        218, 2, // INS_bx
        221, 3, // INS_blx
        225, 3, // INS_ldm
        229, 3, // INS_stm
        233, 4, // INS_sxtb
        238, 4, // INS_sxth
        243, 4, // INS_uxtb
        248, 4, // INS_uxth
        253, 3, // INS_mul
        257, 3, // INS_adr
        261, 4, // INS_addw
        266, 3, // INS_bfc
        270, 3, // INS_bfi
        274, 2, // INS_bl
        277, 4, // INS_bkpt
        282, 4, // INS_cbnz
        287, 3, // INS_cbz
        291, 3, // INS_clz
        295, 3, // INS_dmb
        299, 3, // INS_ism
        303, 5, // INS_ldmdb
        309, 4, // INS_ldrd
        314, 5, // INS_ldrex
        320, 6, // INS_ldrexb
        327, 6, // INS_ldrexd
        334, 6, // INS_ldrexh
        341, 3, // INS_mla
        345, 3, // INS_mls
        349, 3, // INS_nop
        353, 5, // INS_nopw
        359, 4, // INS_sbfx
        364, 4, // INS_sdiv
        369, 4, // INS_ssat
        374, 5, // INS_smlal
        380, 5, // INS_smull
        386, 5, // INS_stmdb
        392, 4, // INS_strd
        397, 5, // INS_strex
        403, 6, // INS_strexb
        410, 6, // INS_strexd
        417, 6, // INS_strexh
        424, 4, // INS_subw
        429, 3, // INS_tbb
        433, 3, // INS_tbh
        437, 4, // INS_ubfx
        442, 4, // INS_udiv
        447, 5, // INS_umlal
        453, 5, // INS_umull
        459, 4, // INS_usat
        543, 4, // INS_vstr
        548, 4, // INS_vldr
        553, 4, // INS_vstm
        558, 4, // INS_vldm
        563, 5, // INS_vpush
        569, 4, // INS_vpop
        574, 4, // INS_vmrs
        579, 4, // INS_vadd
        584, 4, // INS_vsub
        589, 4, // INS_vmul
        594, 4, // INS_vdiv
        599, 4, // INS_vmov
        604, 4, // INS_vabs
        609, 5, // INS_vsqrt
        615, 4, // INS_vneg
        620, 4, // INS_vcmp
        625, 6, // INS_vcmp0
        632, 8, // INS_vcvt_d2i
        641, 8, // INS_vcvt_f2i
        650, 8, // INS_vcvt_d2u
        659, 8, // INS_vcvt_f2u
        668, 8, // INS_vcvt_i2f
        677, 8, // INS_vcvt_i2d
        686, 8, // INS_vcvt_u2f
        695, 8, // INS_vcvt_u2d
        704, 8, // INS_vcvt_d2f
        713, 8, // INS_vcvt_f2d
        722, 8, // INS_vmov_i2d
        731, 8, // INS_vmov_d2i
        740, 8, // INS_vmov_i2f
        749, 8, // INS_vmov_f2i
    ];

    private static ReadOnlySpan<int> s_instructionNameRangesArmPli => [
        0, 7, // INS_invalid
        8, 3, // INS_add
        12, 3, // INS_sub
        16, 3, // INS_ldr
        20, 3, // INS_str
        24, 4, // INS_ldrb
        29, 4, // INS_strb
        34, 4, // INS_ldrh
        39, 4, // INS_strh
        44, 5, // INS_ldrsb
        50, 5, // INS_ldrsh
        56, 3, // INS_mov
        60, 3, // INS_cmp
        64, 3, // INS_lsl
        68, 3, // INS_lsr
        72, 3, // INS_asr
        76, 3, // INS_ror
        80, 3, // INS_pld
        84, 4, // INS_pldw
        89, 3, // INS_pli
        93, 4, // INS_movt
        98, 4, // INS_movw
        103, 3, // INS_and
        107, 3, // INS_eor
        111, 3, // INS_orr
        115, 3, // INS_orn
        119, 3, // INS_bic
        123, 3, // INS_adc
        127, 3, // INS_sbc
        131, 3, // INS_rsb
        135, 3, // INS_tst
        139, 3, // INS_teq
        143, 3, // INS_cmn
        147, 3, // INS_mvn
        151, 4, // INS_push
        156, 3, // INS_pop
        160, 1, // INS_b
        162, 3, // INS_beq
        166, 3, // INS_bne
        170, 3, // INS_bhs
        174, 3, // INS_blo
        178, 3, // INS_bmi
        182, 3, // INS_bpl
        186, 3, // INS_bvs
        190, 3, // INS_bvc
        194, 3, // INS_bhi
        198, 3, // INS_bls
        202, 3, // INS_bge
        206, 3, // INS_blt
        210, 3, // INS_bgt
        214, 3, // INS_ble
        218, 2, // INS_bx
        221, 3, // INS_blx
        225, 3, // INS_ldm
        229, 3, // INS_stm
        233, 4, // INS_sxtb
        238, 4, // INS_sxth
        243, 4, // INS_uxtb
        248, 4, // INS_uxth
        253, 3, // INS_mul
        257, 3, // INS_adr
        261, 4, // INS_addw
        266, 3, // INS_bfc
        270, 3, // INS_bfi
        274, 2, // INS_bl
        277, 4, // INS_bkpt
        282, 4, // INS_cbnz
        287, 3, // INS_cbz
        291, 3, // INS_clz
        295, 3, // INS_dmb
        299, 3, // INS_ism
        303, 5, // INS_ldmdb
        309, 4, // INS_ldrd
        314, 5, // INS_ldrex
        320, 6, // INS_ldrexb
        327, 6, // INS_ldrexd
        334, 6, // INS_ldrexh
        341, 3, // INS_mla
        345, 3, // INS_mls
        349, 3, // INS_nop
        353, 5, // INS_nopw
        359, 4, // INS_sbfx
        364, 4, // INS_sdiv
        369, 4, // INS_ssat
        374, 5, // INS_smlal
        380, 5, // INS_smull
        386, 5, // INS_stmdb
        392, 4, // INS_strd
        397, 5, // INS_strex
        403, 6, // INS_strexb
        410, 6, // INS_strexd
        417, 6, // INS_strexh
        424, 4, // INS_subw
        429, 3, // INS_tbb
        433, 3, // INS_tbh
        437, 4, // INS_ubfx
        442, 4, // INS_udiv
        447, 5, // INS_umlal
        453, 5, // INS_umull
        459, 4, // INS_usat
        543, 4, // INS_vstr
        548, 4, // INS_vldr
        553, 4, // INS_vstm
        558, 4, // INS_vldm
        563, 5, // INS_vpush
        569, 4, // INS_vpop
        574, 4, // INS_vmrs
        579, 4, // INS_vadd
        584, 4, // INS_vsub
        589, 4, // INS_vmul
        594, 4, // INS_vdiv
        599, 4, // INS_vmov
        604, 4, // INS_vabs
        609, 5, // INS_vsqrt
        615, 4, // INS_vneg
        620, 4, // INS_vcmp
        625, 6, // INS_vcmp0
        632, 8, // INS_vcvt_d2i
        641, 8, // INS_vcvt_f2i
        650, 8, // INS_vcvt_d2u
        659, 8, // INS_vcvt_f2u
        668, 8, // INS_vcvt_i2f
        677, 8, // INS_vcvt_i2d
        686, 8, // INS_vcvt_u2f
        695, 8, // INS_vcvt_u2d
        704, 8, // INS_vcvt_d2f
        713, 8, // INS_vcvt_f2d
        722, 8, // INS_vmov_i2d
        731, 8, // INS_vmov_d2i
        740, 8, // INS_vmov_i2f
        749, 8, // INS_vmov_f2i
    ];

    private static ReadOnlySpan<int> s_instructionNameRangesArmIt => [
        0, 7, // INS_invalid
        8, 3, // INS_add
        12, 3, // INS_sub
        16, 3, // INS_ldr
        20, 3, // INS_str
        24, 4, // INS_ldrb
        29, 4, // INS_strb
        34, 4, // INS_ldrh
        39, 4, // INS_strh
        44, 5, // INS_ldrsb
        50, 5, // INS_ldrsh
        56, 3, // INS_mov
        60, 3, // INS_cmp
        64, 3, // INS_lsl
        68, 3, // INS_lsr
        72, 3, // INS_asr
        76, 3, // INS_ror
        80, 3, // INS_pld
        84, 4, // INS_pldw
        93, 4, // INS_movt
        98, 4, // INS_movw
        103, 3, // INS_and
        107, 3, // INS_eor
        111, 3, // INS_orr
        115, 3, // INS_orn
        119, 3, // INS_bic
        123, 3, // INS_adc
        127, 3, // INS_sbc
        131, 3, // INS_rsb
        135, 3, // INS_tst
        139, 3, // INS_teq
        143, 3, // INS_cmn
        147, 3, // INS_mvn
        151, 4, // INS_push
        156, 3, // INS_pop
        160, 1, // INS_b
        162, 3, // INS_beq
        166, 3, // INS_bne
        170, 3, // INS_bhs
        174, 3, // INS_blo
        178, 3, // INS_bmi
        182, 3, // INS_bpl
        186, 3, // INS_bvs
        190, 3, // INS_bvc
        194, 3, // INS_bhi
        198, 3, // INS_bls
        202, 3, // INS_bge
        206, 3, // INS_blt
        210, 3, // INS_bgt
        214, 3, // INS_ble
        218, 2, // INS_bx
        221, 3, // INS_blx
        225, 3, // INS_ldm
        229, 3, // INS_stm
        233, 4, // INS_sxtb
        238, 4, // INS_sxth
        243, 4, // INS_uxtb
        248, 4, // INS_uxth
        253, 3, // INS_mul
        257, 3, // INS_adr
        261, 4, // INS_addw
        266, 3, // INS_bfc
        270, 3, // INS_bfi
        274, 2, // INS_bl
        277, 4, // INS_bkpt
        282, 4, // INS_cbnz
        287, 3, // INS_cbz
        291, 3, // INS_clz
        295, 3, // INS_dmb
        299, 3, // INS_ism
        303, 5, // INS_ldmdb
        309, 4, // INS_ldrd
        314, 5, // INS_ldrex
        320, 6, // INS_ldrexb
        327, 6, // INS_ldrexd
        334, 6, // INS_ldrexh
        341, 3, // INS_mla
        345, 3, // INS_mls
        349, 3, // INS_nop
        353, 5, // INS_nopw
        359, 4, // INS_sbfx
        364, 4, // INS_sdiv
        369, 4, // INS_ssat
        374, 5, // INS_smlal
        380, 5, // INS_smull
        386, 5, // INS_stmdb
        392, 4, // INS_strd
        397, 5, // INS_strex
        403, 6, // INS_strexb
        410, 6, // INS_strexd
        417, 6, // INS_strexh
        424, 4, // INS_subw
        429, 3, // INS_tbb
        433, 3, // INS_tbh
        437, 4, // INS_ubfx
        442, 4, // INS_udiv
        447, 5, // INS_umlal
        453, 5, // INS_umull
        459, 4, // INS_usat
        464, 2, // INS_it
        467, 3, // INS_itt
        471, 3, // INS_ite
        475, 4, // INS_ittt
        480, 4, // INS_itte
        485, 4, // INS_itet
        490, 4, // INS_itee
        495, 5, // INS_itttt
        501, 5, // INS_ittte
        507, 5, // INS_ittet
        513, 5, // INS_ittee
        519, 5, // INS_itett
        525, 5, // INS_itete
        531, 5, // INS_iteet
        537, 5, // INS_iteee
        543, 4, // INS_vstr
        548, 4, // INS_vldr
        553, 4, // INS_vstm
        558, 4, // INS_vldm
        563, 5, // INS_vpush
        569, 4, // INS_vpop
        574, 4, // INS_vmrs
        579, 4, // INS_vadd
        584, 4, // INS_vsub
        589, 4, // INS_vmul
        594, 4, // INS_vdiv
        599, 4, // INS_vmov
        604, 4, // INS_vabs
        609, 5, // INS_vsqrt
        615, 4, // INS_vneg
        620, 4, // INS_vcmp
        625, 6, // INS_vcmp0
        632, 8, // INS_vcvt_d2i
        641, 8, // INS_vcvt_f2i
        650, 8, // INS_vcvt_d2u
        659, 8, // INS_vcvt_f2u
        668, 8, // INS_vcvt_i2f
        677, 8, // INS_vcvt_i2d
        686, 8, // INS_vcvt_u2f
        695, 8, // INS_vcvt_u2d
        704, 8, // INS_vcvt_d2f
        713, 8, // INS_vcvt_f2d
        722, 8, // INS_vmov_i2d
        731, 8, // INS_vmov_d2i
        740, 8, // INS_vmov_i2f
        749, 8, // INS_vmov_f2i
    ];

    private static ReadOnlySpan<int> s_instructionNameRangesArmPliIt => [
        0, 7, // INS_invalid
        8, 3, // INS_add
        12, 3, // INS_sub
        16, 3, // INS_ldr
        20, 3, // INS_str
        24, 4, // INS_ldrb
        29, 4, // INS_strb
        34, 4, // INS_ldrh
        39, 4, // INS_strh
        44, 5, // INS_ldrsb
        50, 5, // INS_ldrsh
        56, 3, // INS_mov
        60, 3, // INS_cmp
        64, 3, // INS_lsl
        68, 3, // INS_lsr
        72, 3, // INS_asr
        76, 3, // INS_ror
        80, 3, // INS_pld
        84, 4, // INS_pldw
        89, 3, // INS_pli
        93, 4, // INS_movt
        98, 4, // INS_movw
        103, 3, // INS_and
        107, 3, // INS_eor
        111, 3, // INS_orr
        115, 3, // INS_orn
        119, 3, // INS_bic
        123, 3, // INS_adc
        127, 3, // INS_sbc
        131, 3, // INS_rsb
        135, 3, // INS_tst
        139, 3, // INS_teq
        143, 3, // INS_cmn
        147, 3, // INS_mvn
        151, 4, // INS_push
        156, 3, // INS_pop
        160, 1, // INS_b
        162, 3, // INS_beq
        166, 3, // INS_bne
        170, 3, // INS_bhs
        174, 3, // INS_blo
        178, 3, // INS_bmi
        182, 3, // INS_bpl
        186, 3, // INS_bvs
        190, 3, // INS_bvc
        194, 3, // INS_bhi
        198, 3, // INS_bls
        202, 3, // INS_bge
        206, 3, // INS_blt
        210, 3, // INS_bgt
        214, 3, // INS_ble
        218, 2, // INS_bx
        221, 3, // INS_blx
        225, 3, // INS_ldm
        229, 3, // INS_stm
        233, 4, // INS_sxtb
        238, 4, // INS_sxth
        243, 4, // INS_uxtb
        248, 4, // INS_uxth
        253, 3, // INS_mul
        257, 3, // INS_adr
        261, 4, // INS_addw
        266, 3, // INS_bfc
        270, 3, // INS_bfi
        274, 2, // INS_bl
        277, 4, // INS_bkpt
        282, 4, // INS_cbnz
        287, 3, // INS_cbz
        291, 3, // INS_clz
        295, 3, // INS_dmb
        299, 3, // INS_ism
        303, 5, // INS_ldmdb
        309, 4, // INS_ldrd
        314, 5, // INS_ldrex
        320, 6, // INS_ldrexb
        327, 6, // INS_ldrexd
        334, 6, // INS_ldrexh
        341, 3, // INS_mla
        345, 3, // INS_mls
        349, 3, // INS_nop
        353, 5, // INS_nopw
        359, 4, // INS_sbfx
        364, 4, // INS_sdiv
        369, 4, // INS_ssat
        374, 5, // INS_smlal
        380, 5, // INS_smull
        386, 5, // INS_stmdb
        392, 4, // INS_strd
        397, 5, // INS_strex
        403, 6, // INS_strexb
        410, 6, // INS_strexd
        417, 6, // INS_strexh
        424, 4, // INS_subw
        429, 3, // INS_tbb
        433, 3, // INS_tbh
        437, 4, // INS_ubfx
        442, 4, // INS_udiv
        447, 5, // INS_umlal
        453, 5, // INS_umull
        459, 4, // INS_usat
        464, 2, // INS_it
        467, 3, // INS_itt
        471, 3, // INS_ite
        475, 4, // INS_ittt
        480, 4, // INS_itte
        485, 4, // INS_itet
        490, 4, // INS_itee
        495, 5, // INS_itttt
        501, 5, // INS_ittte
        507, 5, // INS_ittet
        513, 5, // INS_ittee
        519, 5, // INS_itett
        525, 5, // INS_itete
        531, 5, // INS_iteet
        537, 5, // INS_iteee
        543, 4, // INS_vstr
        548, 4, // INS_vldr
        553, 4, // INS_vstm
        558, 4, // INS_vldm
        563, 5, // INS_vpush
        569, 4, // INS_vpop
        574, 4, // INS_vmrs
        579, 4, // INS_vadd
        584, 4, // INS_vsub
        589, 4, // INS_vmul
        594, 4, // INS_vdiv
        599, 4, // INS_vmov
        604, 4, // INS_vabs
        609, 5, // INS_vsqrt
        615, 4, // INS_vneg
        620, 4, // INS_vcmp
        625, 6, // INS_vcmp0
        632, 8, // INS_vcvt_d2i
        641, 8, // INS_vcvt_f2i
        650, 8, // INS_vcvt_d2u
        659, 8, // INS_vcvt_f2u
        668, 8, // INS_vcvt_i2f
        677, 8, // INS_vcvt_i2d
        686, 8, // INS_vcvt_u2f
        695, 8, // INS_vcvt_u2d
        704, 8, // INS_vcvt_d2f
        713, 8, // INS_vcvt_f2d
        722, 8, // INS_vmov_i2d
        731, 8, // INS_vmov_d2i
        740, 8, // INS_vmov_i2f
        749, 8, // INS_vmov_f2i
    ];

    private static ReadOnlySpan<int> s_instructionNameRangesArm64 => [
        0, 7, // INS_invalid
        56, 3, // INS_mov
        8, 3, // INS_add
        12, 3, // INS_sub
        758, 3, // INS_ld1
        762, 3, // INS_ld2
        766, 3, // INS_ld3
        770, 3, // INS_ld4
        774, 3, // INS_st1
        778, 3, // INS_st2
        782, 3, // INS_st3
        786, 3, // INS_st4
        16, 3, // INS_ldr
        790, 5, // INS_ldrsw
        796, 4, // INS_fmov
        111, 3, // INS_orr
        24, 4, // INS_ldrb
        34, 4, // INS_ldrh
        44, 5, // INS_ldrsb
        50, 5, // INS_ldrsh
        20, 3, // INS_str
        29, 4, // INS_strb
        39, 4, // INS_strh
        801, 4, // INS_adds
        806, 4, // INS_subs
        60, 3, // INS_cmp
        143, 3, // INS_cmn
        811, 4, // INS_fmul
        816, 5, // INS_fmulx
        103, 3, // INS_and
        107, 3, // INS_eor
        119, 3, // INS_bic
        822, 3, // INS_neg
        826, 4, // INS_cmeq
        831, 4, // INS_cmge
        836, 4, // INS_cmgt
        841, 5, // INS_fcmeq
        847, 5, // INS_fcmge
        853, 5, // INS_fcmgt
        859, 5, // INS_sqshl
        865, 5, // INS_uqshl
        871, 7, // INS_sqdmlal
        879, 7, // INS_sqdmlsl
        887, 7, // INS_sqdmulh
        895, 7, // INS_sqdmull
        903, 8, // INS_sqrdmlah
        912, 8, // INS_sqrdmlsh
        921, 8, // INS_sqrdmulh
        930, 4, // INS_ands
        135, 3, // INS_tst
        115, 3, // INS_orn
        935, 3, // INS_dup
        939, 4, // INS_fmla
        944, 4, // INS_fmls
        949, 6, // INS_fcvtas
        956, 6, // INS_fcvtau
        963, 6, // INS_fcvtms
        970, 6, // INS_fcvtmu
        977, 6, // INS_fcvtns
        984, 6, // INS_fcvtnu
        991, 6, // INS_fcvtps
        998, 6, // INS_fcvtpu
        1005, 6, // INS_fcvtzs
        1012, 6, // INS_fcvtzu
        1019, 5, // INS_scvtf
        1025, 5, // INS_ucvtf
        253, 3, // INS_mul
        380, 5, // INS_smull
        453, 5, // INS_umull
        147, 3, // INS_mvn
        758, 3, // INS_ld1_2regs
        758, 3, // INS_ld1_3regs
        758, 3, // INS_ld1_4regs
        774, 3, // INS_st1_2regs
        774, 3, // INS_st1_3regs
        774, 3, // INS_st1_4regs
        1031, 4, // INS_ld1r
        1036, 4, // INS_ld2r
        1041, 4, // INS_ld3r
        1046, 4, // INS_ld4r
        1051, 4, // INS_negs
        1056, 4, // INS_bics
        1061, 3, // INS_eon
        64, 3, // INS_lsl
        68, 3, // INS_lsr
        72, 3, // INS_asr
        76, 3, // INS_ror
        1065, 3, // INS_ldp
        1069, 5, // INS_ldpsw
        1075, 3, // INS_stp
        1079, 4, // INS_ldnp
        1084, 4, // INS_stnp
        1089, 4, // INS_ccmp
        1094, 4, // INS_ccmn
        1099, 3, // INS_ins
        1103, 4, // INS_fadd
        1108, 4, // INS_fsub
        1113, 4, // INS_fdiv
        1118, 4, // INS_fmax
        1123, 6, // INS_fmaxnm
        1130, 4, // INS_fmin
        1135, 6, // INS_fminnm
        1142, 4, // INS_fabd
        1147, 5, // INS_facge
        1153, 5, // INS_facgt
        1159, 6, // INS_frecps
        1166, 7, // INS_frsqrts
        1174, 4, // INS_fcmp
        1179, 5, // INS_fcmpe
        1185, 4, // INS_fabs
        1190, 5, // INS_fcmle
        1196, 5, // INS_fcmlt
        1202, 6, // INS_fcvtxn
        1209, 4, // INS_fneg
        1214, 6, // INS_frecpe
        1221, 6, // INS_frintn
        1228, 6, // INS_frintp
        1235, 6, // INS_frintm
        1242, 6, // INS_frintz
        1249, 6, // INS_frinta
        1256, 6, // INS_frintx
        1263, 6, // INS_frinti
        1270, 7, // INS_frsqrte
        1278, 5, // INS_fsqrt
        1284, 3, // INS_abs
        1288, 4, // INS_cmle
        1293, 4, // INS_cmlt
        1298, 5, // INS_sqabs
        1304, 5, // INS_sqneg
        1310, 5, // INS_sqxtn
        1316, 6, // INS_sqxtun
        1323, 6, // INS_suqadd
        1330, 6, // INS_usqadd
        1337, 5, // INS_uqxtn
        1343, 3, // INS_cls
        291, 3, // INS_clz
        1347, 4, // INS_rbit
        1352, 3, // INS_cnt
        1356, 5, // INS_rev16
        1362, 5, // INS_rev32
        341, 3, // INS_mla
        345, 3, // INS_mls
        374, 5, // INS_smlal
        1368, 6, // INS_smlal2
        1375, 5, // INS_smlsl
        1381, 6, // INS_smlsl2
        1388, 6, // INS_smull2
        1395, 8, // INS_sqdmlal2
        1404, 8, // INS_sqdmlsl2
        1413, 8, // INS_sqdmull2
        1422, 4, // INS_sdot
        1427, 4, // INS_udot
        447, 5, // INS_umlal
        1432, 6, // INS_umlal2
        1439, 5, // INS_umlsl
        1445, 6, // INS_umlsl2
        1452, 6, // INS_umull2
        1459, 4, // INS_sshr
        1464, 4, // INS_ssra
        1469, 5, // INS_srshr
        1475, 5, // INS_srsra
        1481, 3, // INS_shl
        1485, 4, // INS_ushr
        1490, 4, // INS_usra
        1495, 5, // INS_urshr
        1501, 5, // INS_ursra
        1507, 3, // INS_sri
        1511, 3, // INS_sli
        1515, 6, // INS_sqshlu
        1522, 7, // INS_sqrshrn
        1530, 8, // INS_sqrshrun
        1539, 6, // INS_sqshrn
        1546, 7, // INS_sqshrun
        1554, 7, // INS_uqrshrn
        1562, 6, // INS_uqshrn
        1569, 4, // INS_cmhi
        1574, 4, // INS_cmhs
        1579, 5, // INS_cmtst
        1585, 5, // INS_sqadd
        1591, 6, // INS_sqrshl
        1598, 5, // INS_sqsub
        1604, 5, // INS_srshl
        1610, 4, // INS_sshl
        1615, 5, // INS_uqadd
        1621, 6, // INS_uqrshl
        1628, 5, // INS_uqsub
        1634, 5, // INS_urshl
        1640, 4, // INS_ushl
        1645, 5, // INS_faddp
        1651, 7, // INS_fmaxnmp
        1659, 5, // INS_fmaxp
        1665, 7, // INS_fminnmp
        1673, 5, // INS_fminp
        1679, 4, // INS_addp
        1684, 4, // INS_ldar
        1689, 5, // INS_ldarb
        1695, 5, // INS_ldarh
        1701, 5, // INS_ldapr
        1707, 6, // INS_ldaprb
        1714, 6, // INS_ldaprh
        1721, 4, // INS_ldxr
        1726, 5, // INS_ldxrb
        1732, 5, // INS_ldxrh
        1738, 5, // INS_ldaxr
        1744, 6, // INS_ldaxrb
        1751, 6, // INS_ldaxrh
        1758, 4, // INS_ldur
        1763, 5, // INS_ldurb
        1769, 5, // INS_ldurh
        1775, 6, // INS_ldursb
        1782, 6, // INS_ldursh
        1789, 6, // INS_ldursw
        1796, 6, // INS_ldapur
        1803, 7, // INS_ldapurb
        1811, 7, // INS_ldapurh
        1819, 4, // INS_stlr
        1824, 5, // INS_stlrb
        1830, 5, // INS_stlrh
        1836, 4, // INS_stxr
        1841, 5, // INS_stxrb
        1847, 5, // INS_stxrh
        1853, 5, // INS_stlxr
        1859, 6, // INS_stlxrb
        1866, 6, // INS_stlxrh
        1873, 4, // INS_stur
        1878, 5, // INS_sturb
        1884, 5, // INS_sturh
        1890, 5, // INS_stlur
        1896, 6, // INS_stlurb
        1903, 6, // INS_stlurh
        1910, 4, // INS_casb
        1915, 5, // INS_casab
        1921, 6, // INS_casalb
        1928, 5, // INS_caslb
        1934, 4, // INS_cash
        1939, 5, // INS_casah
        1945, 6, // INS_casalh
        1952, 5, // INS_caslh
        1958, 3, // INS_cas
        1962, 4, // INS_casa
        1967, 5, // INS_casal
        1973, 4, // INS_casl
        1978, 6, // INS_ldaddb
        1985, 7, // INS_ldaddab
        1993, 8, // INS_ldaddalb
        2002, 7, // INS_ldaddlb
        2010, 6, // INS_ldaddh
        2017, 7, // INS_ldaddah
        2025, 8, // INS_ldaddalh
        2034, 7, // INS_ldaddlh
        2042, 5, // INS_ldadd
        2048, 6, // INS_ldadda
        2055, 7, // INS_ldaddal
        2063, 7, // INS_ldclral
        2071, 7, // INS_ldsetal
        2079, 6, // INS_ldaddl
        2086, 6, // INS_staddb
        2093, 7, // INS_staddlb
        2101, 6, // INS_staddh
        2108, 7, // INS_staddlh
        2116, 5, // INS_stadd
        2122, 6, // INS_staddl
        2129, 4, // INS_swpb
        2134, 5, // INS_swpab
        2140, 6, // INS_swpalb
        2147, 5, // INS_swplb
        2153, 4, // INS_swph
        2158, 5, // INS_swpah
        2164, 6, // INS_swpalh
        2171, 5, // INS_swplh
        2177, 3, // INS_swp
        2181, 4, // INS_swpa
        2186, 5, // INS_swpal
        2192, 4, // INS_swpl
        257, 3, // INS_adr
        2197, 4, // INS_adrp
        160, 1, // INS_b
        160, 1, // INS_b_tail
        274, 2, // INS_bl_local
        274, 2, // INS_bl
        2202, 2, // INS_br
        2202, 2, // INS_br_tail
        2205, 3, // INS_blr
        2209, 3, // INS_ret
        2213, 5, // INS_retaa
        2219, 5, // INS_retab
        162, 3, // INS_beq
        166, 3, // INS_bne
        170, 3, // INS_bhs
        174, 3, // INS_blo
        178, 3, // INS_bmi
        182, 3, // INS_bpl
        186, 3, // INS_bvs
        190, 3, // INS_bvc
        194, 3, // INS_bhi
        198, 3, // INS_bls
        202, 3, // INS_bge
        206, 3, // INS_blt
        210, 3, // INS_bgt
        214, 3, // INS_ble
        287, 3, // INS_cbz
        282, 4, // INS_cbnz
        2225, 3, // INS_tbz
        2229, 4, // INS_tbnz
        2234, 4, // INS_movk
        2239, 4, // INS_movn
        2244, 4, // INS_movz
        2249, 4, // INS_csel
        2254, 5, // INS_csinc
        2260, 5, // INS_csinv
        2266, 5, // INS_csneg
        2272, 4, // INS_cinc
        2277, 4, // INS_cinv
        2282, 4, // INS_cneg
        2287, 4, // INS_cset
        2292, 5, // INS_csetm
        2298, 4, // INS_aese
        2303, 4, // INS_aesd
        2308, 5, // INS_aesmc
        2314, 6, // INS_aesimc
        2321, 3, // INS_rev
        2325, 5, // INS_rev64
        123, 3, // INS_adc
        2331, 4, // INS_adcs
        127, 3, // INS_sbc
        2336, 4, // INS_sbcs
        442, 4, // INS_udiv
        364, 4, // INS_sdiv
        2341, 4, // INS_mneg
        2346, 4, // INS_madd
        2351, 4, // INS_msub
        2356, 6, // INS_smaddl
        2363, 6, // INS_smnegl
        2370, 6, // INS_smsubl
        2377, 5, // INS_smulh
        2383, 6, // INS_umaddl
        2390, 6, // INS_umnegl
        2397, 6, // INS_umsubl
        2404, 5, // INS_umulh
        2410, 4, // INS_extr
        2415, 4, // INS_lslv
        2420, 4, // INS_lsrv
        2425, 4, // INS_asrv
        2430, 4, // INS_rorv
        2435, 6, // INS_crc32b
        2442, 6, // INS_crc32h
        2449, 6, // INS_crc32w
        2456, 6, // INS_crc32x
        2463, 7, // INS_crc32cb
        2471, 7, // INS_crc32ch
        2479, 7, // INS_crc32cw
        2487, 7, // INS_crc32cx
        2495, 5, // INS_sha1c
        2501, 5, // INS_sha1m
        2507, 5, // INS_sha1p
        2513, 5, // INS_sha1h
        2519, 7, // INS_sha1su0
        2527, 7, // INS_sha1su1
        2535, 7, // INS_sha256h
        2543, 8, // INS_sha256h2
        2552, 9, // INS_sha256su0
        2562, 9, // INS_sha256su1
        2572, 3, // INS_ext
        2576, 4, // INS_sbfm
        2581, 3, // INS_bfm
        2585, 4, // INS_ubfm
        2590, 5, // INS_sbfiz
        270, 3, // INS_bfi
        2596, 5, // INS_ubfiz
        359, 4, // INS_sbfx
        2602, 5, // INS_bfxil
        437, 4, // INS_ubfx
        233, 4, // INS_sxtb
        238, 4, // INS_sxth
        2608, 4, // INS_sxtw
        243, 4, // INS_uxtb
        248, 4, // INS_uxth
        2613, 9, // INS_autia1716
        2623, 7, // INS_autiasp
        2631, 9, // INS_autib1716
        2641, 7, // INS_autibsp
        2649, 6, // INS_autibz
        2656, 6, // INS_autiaz
        2663, 9, // INS_pacia1716
        2673, 7, // INS_paciasp
        2681, 9, // INS_pacib1716
        2691, 7, // INS_pacibsp
        2699, 6, // INS_pacibz
        2706, 6, // INS_paciaz
        2713, 7, // INS_xpaclri
        2721, 6, // INS_autiza
        2728, 6, // INS_autizb
        2735, 6, // INS_paciza
        2742, 6, // INS_pacizb
        2749, 5, // INS_xpacd
        2755, 5, // INS_xpaci
        2761, 5, // INS_autia
        2767, 5, // INS_autib
        2773, 5, // INS_pacia
        2779, 5, // INS_pacib
        349, 3, // INS_nop
        2785, 5, // INS_yield
        2791, 3, // INS_brk
        2795, 3, // INS_dsb
        295, 3, // INS_dmb
        2799, 3, // INS_isb
        2803, 5, // INS_dczva
        2809, 3, // INS_mrs_tpid0
        2813, 4, // INS_umov
        2818, 4, // INS_smov
        2823, 4, // INS_movi
        2828, 4, // INS_mvni
        2833, 6, // INS_urecpe
        2840, 7, // INS_ursqrte
        2848, 3, // INS_bsl
        2852, 3, // INS_bit
        2856, 3, // INS_bif
        2860, 4, // INS_addv
        2865, 3, // INS_ctz
        2869, 3, // INS_not
        2873, 6, // INS_saddlv
        2880, 5, // INS_smaxv
        2886, 5, // INS_sminv
        2892, 6, // INS_uaddlv
        2899, 5, // INS_umaxv
        2905, 5, // INS_uminv
        2911, 7, // INS_fmaxnmv
        2919, 5, // INS_fmaxv
        2925, 7, // INS_fminnmv
        2933, 5, // INS_fminv
        2939, 4, // INS_uzp1
        2944, 4, // INS_uzp2
        2949, 4, // INS_zip1
        2954, 4, // INS_zip2
        2959, 4, // INS_trn1
        2964, 4, // INS_trn2
        2969, 6, // INS_sqxtn2
        2976, 7, // INS_sqxtun2
        2984, 6, // INS_uqxtn2
        2991, 3, // INS_xtn
        2995, 4, // INS_xtn2
        3000, 5, // INS_fnmul
        3006, 5, // INS_fmadd
        3012, 5, // INS_fmsub
        3018, 6, // INS_fnmadd
        3025, 6, // INS_fnmsub
        3032, 4, // INS_fcvt
        3037, 4, // INS_pmul
        3042, 4, // INS_saba
        3047, 4, // INS_sabd
        3052, 4, // INS_smax
        3057, 5, // INS_smaxp
        3063, 4, // INS_smin
        3068, 5, // INS_sminp
        3074, 4, // INS_uaba
        3079, 4, // INS_uabd
        3084, 4, // INS_umax
        3089, 5, // INS_umaxp
        3095, 4, // INS_umin
        3100, 5, // INS_uminp
        3106, 5, // INS_fcvtl
        3112, 6, // INS_fcvtl2
        3119, 5, // INS_fcvtn
        3125, 6, // INS_fcvtn2
        3132, 7, // INS_fcvtxn2
        3140, 6, // INS_frecpx
        3147, 5, // INS_addhn
        3153, 6, // INS_addhn2
        3160, 5, // INS_pmull
        3166, 6, // INS_pmull2
        3173, 6, // INS_raddhn
        3180, 7, // INS_raddhn2
        3188, 6, // INS_rsubhn
        3195, 7, // INS_rsubhn2
        3203, 5, // INS_sabal
        3209, 6, // INS_sabal2
        3216, 5, // INS_sabdl
        3222, 6, // INS_sabdl2
        3229, 6, // INS_sadalp
        3236, 5, // INS_saddl
        3242, 6, // INS_saddl2
        3249, 6, // INS_saddlp
        3256, 5, // INS_saddw
        3262, 6, // INS_saddw2
        3269, 5, // INS_shadd
        3275, 5, // INS_shsub
        3281, 6, // INS_srhadd
        3288, 5, // INS_ssubl
        3294, 6, // INS_ssubl2
        3301, 5, // INS_ssubw
        3307, 6, // INS_ssubw2
        3314, 5, // INS_subhn
        3320, 6, // INS_subhn2
        3327, 5, // INS_uabal
        3333, 6, // INS_uabal2
        3340, 5, // INS_uabdl
        3346, 6, // INS_uabdl2
        3353, 6, // INS_uadalp
        3360, 5, // INS_uaddl
        3366, 6, // INS_uaddl2
        3373, 6, // INS_uaddlp
        3380, 5, // INS_uaddw
        3386, 6, // INS_uaddw2
        3393, 5, // INS_uhadd
        3399, 5, // INS_uhsub
        3405, 6, // INS_urhadd
        3412, 5, // INS_usubl
        3418, 6, // INS_usubl2
        3425, 5, // INS_usubw
        3431, 6, // INS_usubw2
        3438, 4, // INS_shll
        3443, 5, // INS_shll2
        3449, 5, // INS_sshll
        3455, 6, // INS_sshll2
        3462, 5, // INS_ushll
        3468, 6, // INS_ushll2
        3475, 4, // INS_shrn
        3480, 5, // INS_shrn2
        3486, 5, // INS_rshrn
        3492, 6, // INS_rshrn2
        3499, 8, // INS_sqrshrn2
        3508, 9, // INS_sqrshrun2
        3518, 7, // INS_sqshrn2
        3526, 8, // INS_sqshrun2
        3535, 8, // INS_uqrshrn2
        3544, 7, // INS_uqshrn2
        3552, 4, // INS_sxtl
        3557, 5, // INS_sxtl2
        3563, 4, // INS_uxtl
        3568, 5, // INS_uxtl2
        3574, 3, // INS_tbl
        3574, 3, // INS_tbl_2regs
        3574, 3, // INS_tbl_3regs
        3574, 3, // INS_tbl_4regs
        3578, 3, // INS_tbx
        3578, 3, // INS_tbx_2regs
        3578, 3, // INS_tbx_3regs
        3578, 3, // INS_tbx_4regs
        3582, 5, // INS_align
        3588, 4, // INS_eor3
        3593, 4, // INS_bcax
        3598, 6, // INS_sm3ss1
        3605, 7, // INS_sha512h
        3613, 8, // INS_sha512h2
        3622, 9, // INS_sha512su1
        3632, 4, // INS_rax1
        3637, 9, // INS_sm3partw1
        3647, 9, // INS_sm3partw2
        3657, 7, // INS_sm4ekey
        3665, 3, // INS_xar
        3669, 9, // INS_sha512su0
        3679, 4, // INS_sm4e
        0, 7, // INS_sve_invalid
        56, 3, // INS_sve_mov
        3684, 4, // INS_sve_st1w
        3689, 5, // INS_sve_ld1sh
        3695, 4, // INS_sve_ld1h
        3700, 4, // INS_sve_ld1w
        3705, 4, // INS_sve_ld1d
        3710, 4, // INS_sve_st1h
        3715, 4, // INS_sve_st1d
        3720, 4, // INS_sve_pmov
        3725, 7, // INS_sve_ldff1sh
        3733, 6, // INS_sve_ldff1w
        3740, 6, // INS_sve_ldff1h
        3747, 5, // INS_sve_ld1sw
        253, 3, // INS_sve_mul
        3753, 4, // INS_sve_fdot
        3758, 5, // INS_sve_ld1sb
        3764, 4, // INS_sve_ld1b
        3769, 4, // INS_sve_prfb
        3774, 4, // INS_sve_prfd
        3779, 4, // INS_sve_prfh
        3784, 4, // INS_sve_prfw
        3789, 6, // INS_sve_ldff1d
        3796, 7, // INS_sve_ldff1sw
        3804, 4, // INS_sve_st1b
        72, 3, // INS_sve_asr
        64, 3, // INS_sve_lsl
        68, 3, // INS_sve_lsr
        811, 4, // INS_sve_fmul
        1422, 4, // INS_sve_sdot
        1427, 4, // INS_sve_udot
        3809, 7, // INS_sve_ldff1sb
        3817, 6, // INS_sve_ldff1b
        103, 3, // INS_sve_and
        119, 3, // INS_sve_bic
        107, 3, // INS_sve_eor
        111, 3, // INS_sve_orr
        796, 4, // INS_sve_fmov
        887, 7, // INS_sve_sqdmulh
        921, 8, // INS_sve_sqrdmulh
        903, 8, // INS_sve_sqrdmlah
        912, 8, // INS_sve_sqrdmlsh
        341, 3, // INS_sve_mla
        345, 3, // INS_sve_mls
        3824, 6, // INS_sve_fmlalb
        3831, 6, // INS_sve_fmlalt
        3838, 5, // INS_sve_index
        3844, 3, // INS_sve_cpy
        3848, 6, // INS_sve_ldnt1b
        3855, 6, // INS_sve_ldnt1h
        3862, 6, // INS_sve_ldnt1w
        3869, 6, // INS_sve_stnt1b
        3876, 6, // INS_sve_stnt1h
        3883, 6, // INS_sve_stnt1w
        8, 3, // INS_sve_add
        12, 3, // INS_sve_sub
        257, 3, // INS_sve_adr
        935, 3, // INS_sve_dup
        2959, 4, // INS_sve_trn1
        2964, 4, // INS_sve_trn2
        2939, 4, // INS_sve_uzp1
        2944, 4, // INS_sve_uzp2
        2949, 4, // INS_sve_zip1
        2954, 4, // INS_sve_zip2
        1585, 5, // INS_sve_sqadd
        1598, 5, // INS_sve_sqsub
        1615, 5, // INS_sve_uqadd
        1628, 5, // INS_sve_uqsub
        939, 4, // INS_sve_fmla
        944, 4, // INS_sve_fmls
        3890, 5, // INS_sve_luti4
        1103, 4, // INS_sve_fadd
        1108, 4, // INS_sve_fsub
        3896, 6, // INS_sve_clasta
        3903, 6, // INS_sve_clastb
        3910, 5, // INS_sve_cmpeq
        3916, 5, // INS_sve_cmpge
        3922, 5, // INS_sve_cmpgt
        3928, 5, // INS_sve_cmple
        3934, 5, // INS_sve_cmplt
        3940, 5, // INS_sve_cmpne
        3946, 5, // INS_sve_cmphi
        3952, 5, // INS_sve_cmphs
        3958, 5, // INS_sve_cmplo
        3964, 5, // INS_sve_cmpls
        3970, 7, // INS_sve_whilege
        3978, 7, // INS_sve_whilegt
        3986, 7, // INS_sve_whilehi
        3994, 7, // INS_sve_whilehs
        4002, 7, // INS_sve_whilele
        4010, 7, // INS_sve_whilelo
        4018, 7, // INS_sve_whilels
        4026, 7, // INS_sve_whilelt
        4034, 4, // INS_sve_cdot
        4039, 4, // INS_sve_cmla
        4044, 9, // INS_sve_sqrdcmlah
        4054, 6, // INS_sve_smlalb
        4061, 6, // INS_sve_smlalt
        4068, 6, // INS_sve_smlslb
        4075, 6, // INS_sve_smlslt
        4082, 6, // INS_sve_umlalb
        4089, 6, // INS_sve_umlalt
        4096, 6, // INS_sve_umlslb
        4103, 6, // INS_sve_umlslt
        4110, 8, // INS_sve_sqdmlalb
        4119, 8, // INS_sve_sqdmlalt
        4128, 8, // INS_sve_sqdmlslb
        4137, 8, // INS_sve_sqdmlslt
        4146, 6, // INS_sve_smullb
        4153, 6, // INS_sve_smullt
        4160, 6, // INS_sve_umullb
        4167, 6, // INS_sve_umullt
        4174, 8, // INS_sve_sqdmullb
        4183, 8, // INS_sve_sqdmullt
        4192, 5, // INS_sve_bfmul
        4198, 6, // INS_sve_ldnt1d
        4205, 6, // INS_sve_stnt1d
        16, 3, // INS_sve_ldr
        20, 3, // INS_sve_str
        3052, 4, // INS_sve_smax
        3063, 4, // INS_sve_smin
        3084, 4, // INS_sve_umax
        3095, 4, // INS_sve_umin
        4212, 5, // INS_sve_addpt
        4218, 5, // INS_sve_subpt
        2321, 3, // INS_sve_rev
        2377, 5, // INS_sve_smulh
        2404, 5, // INS_sve_umulh
        115, 3, // INS_sve_orn
        2572, 3, // INS_sve_ext
        859, 5, // INS_sve_sqshl
        865, 5, // INS_sve_uqshl
        841, 5, // INS_sve_fcmeq
        847, 5, // INS_sve_fcmge
        853, 5, // INS_sve_fcmgt
        1190, 5, // INS_sve_fcmle
        1196, 5, // INS_sve_fcmlt
        4224, 5, // INS_sve_fcmne
        3574, 3, // INS_sve_tbl
        4230, 5, // INS_sve_luti2
        1118, 4, // INS_sve_fmax
        1123, 6, // INS_sve_fmaxnm
        1130, 4, // INS_sve_fmin
        1135, 6, // INS_sve_fminnm
        4236, 5, // INS_sve_fsubr
        4242, 5, // INS_sve_usdot
        4248, 5, // INS_sve_fcmla
        4254, 5, // INS_sve_bfdot
        4260, 8, // INS_sve_fmlallbb
        4269, 8, // INS_sve_fmlallbt
        4278, 8, // INS_sve_fmlalltb
        4287, 8, // INS_sve_fmlalltt
        2869, 3, // INS_sve_not
        4296, 4, // INS_sve_subr
        4301, 7, // INS_sve_movprfx
        4309, 4, // INS_sve_decd
        4314, 4, // INS_sve_dech
        4319, 4, // INS_sve_decw
        4324, 4, // INS_sve_incd
        4329, 4, // INS_sve_inch
        4334, 4, // INS_sve_incw
        4339, 6, // INS_sve_sqdecd
        4346, 6, // INS_sve_sqdech
        4353, 6, // INS_sve_sqdecw
        4360, 6, // INS_sve_sqincd
        4367, 6, // INS_sve_sqinch
        4374, 6, // INS_sve_sqincw
        4381, 6, // INS_sve_uqdecd
        4388, 6, // INS_sve_uqdech
        4395, 6, // INS_sve_uqdecw
        4402, 6, // INS_sve_uqincd
        4409, 6, // INS_sve_uqinch
        4416, 6, // INS_sve_uqincw
        4423, 4, // INS_sve_insr
        4428, 5, // INS_sve_lasta
        4434, 5, // INS_sve_lastb
        4440, 6, // INS_sve_splice
        4447, 3, // INS_sve_sel
        4451, 4, // INS_sve_movs
        4456, 5, // INS_sve_ptrue
        4462, 5, // INS_sve_rdffr
        4468, 4, // INS_sve_cntp
        4473, 4, // INS_sve_decp
        4478, 4, // INS_sve_incp
        4483, 6, // INS_sve_sqdecp
        4490, 6, // INS_sve_sqincp
        4497, 6, // INS_sve_uqdecp
        4504, 6, // INS_sve_uqincp
        4511, 4, // INS_sve_pext
        4516, 6, // INS_sve_pmullb
        4523, 6, // INS_sve_pmullt
        4530, 6, // INS_sve_fcvtnt
        4537, 5, // INS_sve_bfmla
        4543, 5, // INS_sve_bfmls
        4549, 7, // INS_sve_bfmlalb
        4557, 7, // INS_sve_bfmlalt
        4565, 7, // INS_sve_bfmlslb
        4573, 7, // INS_sve_bfmlslt
        4581, 6, // INS_sve_fmlslb
        4588, 6, // INS_sve_fmlslt
        4595, 5, // INS_sve_bfadd
        4601, 5, // INS_sve_bfsub
        4607, 7, // INS_sve_ldnt1sb
        4615, 7, // INS_sve_ldnt1sh
        4623, 6, // INS_sve_ld1rob
        4630, 6, // INS_sve_ld1rod
        4637, 6, // INS_sve_ld1roh
        4644, 6, // INS_sve_ld1row
        4651, 6, // INS_sve_ld1rqb
        4658, 6, // INS_sve_ld1rqd
        4665, 6, // INS_sve_ld1rqh
        4672, 6, // INS_sve_ld1rqw
        4679, 4, // INS_sve_ld2q
        4684, 4, // INS_sve_ld3q
        4689, 4, // INS_sve_ld4q
        4694, 4, // INS_sve_ld2b
        4699, 4, // INS_sve_ld2d
        4704, 4, // INS_sve_ld2h
        4709, 4, // INS_sve_ld2w
        4714, 4, // INS_sve_ld3b
        4719, 4, // INS_sve_ld3d
        4724, 4, // INS_sve_ld3h
        4729, 4, // INS_sve_ld3w
        4734, 4, // INS_sve_ld4b
        4739, 4, // INS_sve_ld4d
        4744, 4, // INS_sve_ld4h
        4749, 4, // INS_sve_ld4w
        4754, 4, // INS_sve_st2b
        4759, 4, // INS_sve_st2d
        4764, 4, // INS_sve_st2h
        4769, 4, // INS_sve_st2w
        4774, 4, // INS_sve_st3b
        4779, 4, // INS_sve_st3d
        4784, 4, // INS_sve_st3h
        4789, 4, // INS_sve_st3w
        4794, 4, // INS_sve_st4b
        4799, 4, // INS_sve_st4d
        4804, 4, // INS_sve_st4h
        4809, 4, // INS_sve_st4w
        4814, 4, // INS_sve_st2q
        4819, 4, // INS_sve_st3q
        4824, 4, // INS_sve_st4q
        1284, 3, // INS_sve_abs
        822, 3, // INS_sve_neg
        233, 4, // INS_sve_sxtb
        238, 4, // INS_sve_sxth
        2608, 4, // INS_sve_sxtw
        243, 4, // INS_sve_uxtb
        248, 4, // INS_sve_uxth
        4829, 4, // INS_sve_uxtw
        930, 4, // INS_sve_ands
        1056, 4, // INS_sve_bics
        4834, 4, // INS_sve_eors
        4839, 4, // INS_sve_nand
        4844, 5, // INS_sve_nands
        4850, 3, // INS_sve_nor
        4854, 4, // INS_sve_nors
        4859, 4, // INS_sve_nots
        4864, 4, // INS_sve_orns
        4869, 4, // INS_sve_orrs
        1347, 4, // INS_sve_rbit
        4874, 4, // INS_sve_revb
        4879, 4, // INS_sve_revh
        4884, 4, // INS_sve_revw
        1343, 3, // INS_sve_cls
        291, 3, // INS_sve_clz
        4889, 4, // INS_sve_cnot
        1352, 3, // INS_sve_cnt
        1185, 4, // INS_sve_fabs
        1209, 4, // INS_sve_fneg
        364, 4, // INS_sve_sdiv
        4894, 5, // INS_sve_sdivr
        442, 4, // INS_sve_udiv
        4900, 5, // INS_sve_udivr
        1061, 3, // INS_sve_eon
        2880, 5, // INS_sve_smaxv
        2886, 5, // INS_sve_sminv
        2899, 5, // INS_sve_umaxv
        2905, 5, // INS_sve_uminv
        4906, 5, // INS_sve_faddv
        2911, 7, // INS_sve_fmaxnmv
        2919, 5, // INS_sve_fmaxv
        2925, 7, // INS_sve_fminnmv
        2933, 5, // INS_sve_fminv
        1679, 4, // INS_sve_addp
        3057, 5, // INS_sve_smaxp
        3068, 5, // INS_sve_sminp
        3089, 5, // INS_sve_umaxp
        3100, 5, // INS_sve_uminp
        1645, 5, // INS_sve_faddp
        1651, 7, // INS_sve_fmaxnmp
        1659, 5, // INS_sve_fmaxp
        1665, 7, // INS_sve_fminnmp
        1673, 5, // INS_sve_fminp
        1475, 5, // INS_sve_srsra
        1464, 4, // INS_sve_ssra
        1501, 5, // INS_sve_ursra
        1490, 4, // INS_sve_usra
        4912, 4, // INS_sve_asrd
        1515, 6, // INS_sve_sqshlu
        1469, 5, // INS_sve_srshr
        1495, 5, // INS_sve_urshr
        1522, 7, // INS_sve_sqrshrn
        1530, 8, // INS_sve_sqrshrun
        1554, 7, // INS_sve_uqrshrn
        1511, 3, // INS_sve_sli
        1507, 3, // INS_sve_sri
        1591, 6, // INS_sve_sqrshl
        4917, 7, // INS_sve_sqrshlr
        4925, 6, // INS_sve_sqshlr
        1604, 5, // INS_sve_srshl
        4932, 6, // INS_sve_srshlr
        1621, 6, // INS_sve_uqrshl
        4939, 7, // INS_sve_uqrshlr
        4947, 6, // INS_sve_uqshlr
        1634, 5, // INS_sve_urshl
        4954, 6, // INS_sve_urshlr
        1142, 4, // INS_sve_fabd
        4961, 5, // INS_sve_famax
        4967, 5, // INS_sve_famin
        1113, 4, // INS_sve_fdiv
        4973, 5, // INS_sve_fdivr
        816, 5, // INS_sve_fmulx
        4979, 6, // INS_sve_fscale
        1159, 6, // INS_sve_frecps
        1166, 7, // INS_sve_frsqrts
        4986, 6, // INS_sve_ftsmul
        1147, 5, // INS_sve_facge
        1153, 5, // INS_sve_facgt
        4993, 5, // INS_sve_facle
        4999, 5, // INS_sve_faclt
        5005, 5, // INS_sve_fcmuo
        5011, 6, // INS_sve_sqsubr
        1323, 6, // INS_sve_suqadd
        5018, 6, // INS_sve_uqsubr
        1330, 6, // INS_sve_usqadd
        1298, 5, // INS_sve_sqabs
        1304, 5, // INS_sve_sqneg
        2833, 6, // INS_sve_urecpe
        2840, 7, // INS_sve_ursqrte
        1214, 6, // INS_sve_frecpe
        1270, 7, // INS_sve_frsqrte
        3140, 6, // INS_sve_frecpx
        1278, 5, // INS_sve_fsqrt
        3578, 3, // INS_sve_tbx
        3269, 5, // INS_sve_shadd
        3275, 5, // INS_sve_shsub
        5025, 6, // INS_sve_shsubr
        3281, 6, // INS_sve_srhadd
        3393, 5, // INS_sve_uhadd
        3399, 5, // INS_sve_uhsub
        5032, 6, // INS_sve_uhsubr
        3405, 6, // INS_sve_urhadd
        3047, 4, // INS_sve_sabd
        3079, 4, // INS_sve_uabd
        3042, 4, // INS_sve_saba
        3074, 4, // INS_sve_uaba
        3037, 4, // INS_sve_pmul
        3593, 4, // INS_sve_bcax
        2848, 3, // INS_sve_bsl
        5039, 5, // INS_sve_bsl1n
        5045, 5, // INS_sve_bsl2n
        3588, 4, // INS_sve_eor3
        5051, 4, // INS_sve_nbsl
        5056, 6, // INS_sve_bfcvtn
        3119, 5, // INS_sve_fcvtn
        5063, 6, // INS_sve_fcvtnb
        5070, 5, // INS_sve_fcadd
        5076, 5, // INS_sve_smmla
        5082, 5, // INS_sve_ummla
        5088, 6, // INS_sve_usmmla
        5095, 6, // INS_sve_bfmmla
        3229, 6, // INS_sve_sadalp
        3353, 6, // INS_sve_uadalp
        1249, 6, // INS_sve_frinta
        1263, 6, // INS_sve_frinti
        1235, 6, // INS_sve_frintm
        1221, 6, // INS_sve_frintn
        1228, 6, // INS_sve_frintp
        1256, 6, // INS_sve_frintx
        1242, 6, // INS_sve_frintz
        5102, 5, // INS_sve_sudot
        2303, 4, // INS_sve_aesd
        2298, 4, // INS_sve_aese
        3679, 4, // INS_sve_sm4e
        2314, 6, // INS_sve_aesimc
        2308, 5, // INS_sve_aesmc
        3632, 4, // INS_sve_rax1
        3657, 7, // INS_sve_sm4ekey
        3665, 3, // INS_sve_xar
        5108, 4, // INS_sve_andv
        5113, 4, // INS_sve_eorv
        5118, 3, // INS_sve_orv
        5122, 5, // INS_sve_andqv
        5128, 5, // INS_sve_eorqv
        5134, 4, // INS_sve_orqv
        5139, 5, // INS_sve_saddv
        5145, 5, // INS_sve_uaddv
        5151, 5, // INS_sve_addqv
        5157, 6, // INS_sve_smaxqv
        5164, 6, // INS_sve_sminqv
        5171, 6, // INS_sve_umaxqv
        5178, 6, // INS_sve_uminqv
        5185, 4, // INS_sve_asrr
        5190, 4, // INS_sve_lslr
        5195, 4, // INS_sve_lsrr
        5200, 3, // INS_sve_mad
        5204, 3, // INS_sve_msb
        5208, 5, // INS_sve_addpl
        5214, 5, // INS_sve_addvl
        5220, 4, // INS_sve_rdvl
        5225, 5, // INS_sve_fexpa
        5231, 6, // INS_sve_ftssel
        5238, 4, // INS_sve_cntb
        5243, 4, // INS_sve_cntd
        5248, 4, // INS_sve_cnth
        5253, 4, // INS_sve_cntw
        5258, 4, // INS_sve_decb
        5263, 4, // INS_sve_incb
        5268, 6, // INS_sve_sqdecb
        5275, 6, // INS_sve_sqincb
        5282, 6, // INS_sve_uqdecb
        5289, 6, // INS_sve_uqincb
        5296, 4, // INS_sve_dupm
        5301, 4, // INS_sve_fcpy
        5306, 4, // INS_sve_dupq
        5311, 4, // INS_sve_extq
        5316, 4, // INS_sve_tbxq
        5321, 7, // INS_sve_sunpkhi
        5329, 7, // INS_sve_sunpklo
        5337, 7, // INS_sve_uunpkhi
        5345, 7, // INS_sve_uunpklo
        5353, 7, // INS_sve_punpkhi
        5361, 7, // INS_sve_punpklo
        5369, 7, // INS_sve_compact
        5377, 4, // INS_sve_revd
        5382, 5, // INS_sve_brkpa
        5388, 6, // INS_sve_brkpas
        5395, 5, // INS_sve_brkpb
        5401, 6, // INS_sve_brkpbs
        5408, 4, // INS_sve_brka
        5413, 4, // INS_sve_brkb
        5418, 5, // INS_sve_brkas
        5424, 5, // INS_sve_brkbs
        5430, 4, // INS_sve_brkn
        5435, 5, // INS_sve_brkns
        5441, 6, // INS_sve_pfirst
        5448, 6, // INS_sve_ptrues
        5455, 5, // INS_sve_pnext
        5461, 6, // INS_sve_rdffrs
        5468, 5, // INS_sve_ptest
        5474, 6, // INS_sve_pfalse
        5481, 6, // INS_sve_setffr
        5488, 5, // INS_sve_wrffr
        5494, 7, // INS_sve_ctermeq
        5502, 7, // INS_sve_ctermne
        5510, 7, // INS_sve_whilerw
        5518, 7, // INS_sve_whilewr
        5526, 4, // INS_sve_psel
        5531, 4, // INS_sve_fdup
        5536, 9, // INS_sve_sqdmlalbt
        5546, 9, // INS_sve_sqdmlslbt
        5556, 6, // INS_sve_sclamp
        5563, 6, // INS_sve_uclamp
        5570, 5, // INS_sve_mlapt
        5576, 5, // INS_sve_madpt
        5582, 4, // INS_sve_tblq
        5587, 5, // INS_sve_uzpq1
        5593, 5, // INS_sve_uzpq2
        5599, 5, // INS_sve_zipq1
        5605, 5, // INS_sve_zipq2
        5611, 6, // INS_sve_sabdlb
        5618, 6, // INS_sve_sabdlt
        5625, 6, // INS_sve_saddlb
        5632, 6, // INS_sve_saddlt
        5639, 6, // INS_sve_ssublb
        5646, 6, // INS_sve_ssublt
        5653, 6, // INS_sve_uabdlb
        5660, 6, // INS_sve_uabdlt
        5667, 6, // INS_sve_uaddlb
        5674, 6, // INS_sve_uaddlt
        5681, 6, // INS_sve_usublb
        5688, 6, // INS_sve_usublt
        5695, 6, // INS_sve_saddwb
        5702, 6, // INS_sve_saddwt
        5709, 6, // INS_sve_ssubwb
        5716, 6, // INS_sve_ssubwt
        5723, 6, // INS_sve_uaddwb
        5730, 6, // INS_sve_uaddwt
        5737, 6, // INS_sve_usubwb
        5744, 6, // INS_sve_usubwt
        5751, 5, // INS_sve_eorbt
        5757, 5, // INS_sve_eortb
        5763, 4, // INS_sve_bdep
        5768, 4, // INS_sve_bext
        5773, 4, // INS_sve_bgrp
        5778, 6, // INS_sve_sshllb
        5785, 6, // INS_sve_sshllt
        5792, 6, // INS_sve_ushllb
        5799, 6, // INS_sve_ushllt
        5806, 7, // INS_sve_saddlbt
        5814, 7, // INS_sve_ssublbt
        5822, 7, // INS_sve_ssubltb
        5830, 4, // INS_sve_cadd
        5835, 6, // INS_sve_sqcadd
        5842, 6, // INS_sve_sabalb
        5849, 6, // INS_sve_sabalt
        5856, 6, // INS_sve_uabalb
        5863, 6, // INS_sve_uabalt
        5870, 5, // INS_sve_adclb
        5876, 5, // INS_sve_adclt
        5882, 5, // INS_sve_sbclb
        5888, 5, // INS_sve_sbclt
        5894, 6, // INS_sve_sqcvtn
        5901, 7, // INS_sve_sqcvtun
        5909, 6, // INS_sve_uqcvtn
        5916, 6, // INS_sve_rshrnb
        5923, 6, // INS_sve_rshrnt
        5930, 5, // INS_sve_shrnb
        5936, 5, // INS_sve_shrnt
        5942, 8, // INS_sve_sqrshrnb
        5951, 8, // INS_sve_sqrshrnt
        5960, 9, // INS_sve_sqrshrunb
        5970, 9, // INS_sve_sqrshrunt
        5980, 7, // INS_sve_sqshrnb
        5988, 7, // INS_sve_sqshrnt
        5996, 8, // INS_sve_sqshrunb
        6005, 8, // INS_sve_sqshrunt
        6014, 8, // INS_sve_uqrshrnb
        6023, 8, // INS_sve_uqrshrnt
        6032, 7, // INS_sve_uqshrnb
        6040, 7, // INS_sve_uqshrnt
        6048, 6, // INS_sve_addhnb
        6055, 6, // INS_sve_addhnt
        6062, 7, // INS_sve_raddhnb
        6070, 7, // INS_sve_raddhnt
        6078, 7, // INS_sve_rsubhnb
        6086, 7, // INS_sve_rsubhnt
        6094, 6, // INS_sve_subhnb
        6101, 6, // INS_sve_subhnt
        6108, 6, // INS_sve_sqxtnb
        6115, 6, // INS_sve_sqxtnt
        6122, 7, // INS_sve_sqxtunb
        6130, 7, // INS_sve_sqxtunt
        6138, 6, // INS_sve_uqxtnb
        6145, 6, // INS_sve_uqxtnt
        6152, 5, // INS_sve_match
        6158, 6, // INS_sve_nmatch
        6165, 7, // INS_sve_histseg
        6173, 7, // INS_sve_histcnt
        6181, 7, // INS_sve_bfcvtnt
        6189, 6, // INS_sve_fcvtlt
        6196, 7, // INS_sve_fcvtxnt
        6204, 6, // INS_sve_faddqv
        6211, 8, // INS_sve_fmaxnmqv
        6220, 6, // INS_sve_fmaxqv
        6227, 8, // INS_sve_fminnmqv
        6236, 6, // INS_sve_fminqv
        6243, 6, // INS_sve_fclamp
        6250, 7, // INS_sve_bfclamp
        6258, 5, // INS_sve_fmmla
        6264, 6, // INS_sve_bf1cvt
        6271, 8, // INS_sve_bf1cvtlt
        6280, 6, // INS_sve_bf2cvt
        6287, 8, // INS_sve_bf2cvtlt
        6296, 5, // INS_sve_f1cvt
        6302, 7, // INS_sve_f1cvtlt
        6310, 5, // INS_sve_f2cvt
        6316, 7, // INS_sve_f2cvtlt
        6324, 5, // INS_sve_fadda
        6330, 5, // INS_sve_bfmax
        6336, 7, // INS_sve_bfmaxnm
        6344, 5, // INS_sve_bfmin
        6350, 7, // INS_sve_bfminnm
        6358, 5, // INS_sve_ftmad
        6364, 5, // INS_sve_bfcvt
        3032, 4, // INS_sve_fcvt
        6370, 5, // INS_sve_fcvtx
        6376, 5, // INS_sve_flogb
        1005, 6, // INS_sve_fcvtzs
        1012, 6, // INS_sve_fcvtzu
        1019, 5, // INS_sve_scvtf
        1025, 5, // INS_sve_ucvtf
        6382, 5, // INS_sve_fnmla
        6388, 5, // INS_sve_fnmls
        6394, 4, // INS_sve_fmad
        6399, 4, // INS_sve_fmsb
        6404, 5, // INS_sve_fnmad
        6410, 5, // INS_sve_fnmsb
        6416, 5, // INS_sve_ld1rb
        6422, 5, // INS_sve_ld1rd
        6428, 6, // INS_sve_ld1rsw
        6435, 5, // INS_sve_ld1rh
        6441, 6, // INS_sve_ld1rsb
        6448, 6, // INS_sve_ld1rsh
        6455, 5, // INS_sve_ld1rw
        6461, 6, // INS_sve_ldnf1b
        6468, 6, // INS_sve_ldnf1d
        6475, 7, // INS_sve_ldnf1sw
        6483, 6, // INS_sve_ldnf1h
        6490, 7, // INS_sve_ldnf1sb
        6498, 7, // INS_sve_ldnf1sh
        6506, 6, // INS_sve_ldnf1w
        6513, 4, // INS_sve_ld1q
        6518, 7, // INS_sve_ldnt1sw
        6526, 4, // INS_sve_st1q
    ];

    private static ReadOnlySpan<int> s_instructionNameRangesLoongArch64 => [
        0, 7, // INS_invalid
        349, 3, // INS_nop
        6531, 5, // INS_bceqz
        6537, 5, // INS_bcnez
        162, 3, // INS_beq
        166, 3, // INS_bne
        206, 3, // INS_blt
        202, 3, // INS_bge
        6543, 4, // INS_bltu
        6548, 4, // INS_bgeu
        6553, 4, // INS_beqz
        6558, 4, // INS_bnez
        160, 1, // INS_b
        274, 2, // INS_bl
        56, 3, // INS_mov
        6563, 4, // INS_dneg
        822, 3, // INS_neg
        2869, 3, // INS_not
        6568, 5, // INS_add_w
        6574, 5, // INS_add_d
        6580, 5, // INS_sub_w
        6586, 5, // INS_sub_d
        103, 3, // INS_and
        6592, 2, // INS_or
        4850, 3, // INS_nor
        6595, 3, // INS_xor
        6599, 4, // INS_andn
        115, 3, // INS_orn
        6604, 5, // INS_mul_w
        6610, 5, // INS_mul_d
        6616, 6, // INS_mulh_w
        6623, 7, // INS_mulh_wu
        6631, 6, // INS_mulh_d
        6638, 7, // INS_mulh_du
        6646, 8, // INS_mulw_d_w
        6655, 9, // INS_mulw_d_wu
        6665, 5, // INS_div_w
        6671, 5, // INS_mod_w
        6677, 6, // INS_div_wu
        6684, 6, // INS_mod_wu
        6691, 5, // INS_div_d
        6697, 5, // INS_mod_d
        6703, 6, // INS_div_du
        6710, 6, // INS_mod_du
        6717, 5, // INS_sll_w
        6723, 5, // INS_srl_w
        6729, 5, // INS_sra_w
        6735, 6, // INS_rotr_w
        6742, 5, // INS_sll_d
        6748, 5, // INS_srl_d
        6754, 5, // INS_sra_d
        6760, 6, // INS_rotr_d
        6767, 7, // INS_maskeqz
        6775, 7, // INS_masknez
        6783, 3, // INS_slt
        6787, 4, // INS_sltu
        6792, 9, // INS_crc_w_b_w
        6802, 9, // INS_crc_w_h_w
        6812, 9, // INS_crc_w_w_w
        6822, 9, // INS_crc_w_d_w
        6832, 10, // INS_crcc_w_b_w
        6843, 10, // INS_crcc_w_h_w
        6854, 10, // INS_crcc_w_w_w
        6865, 10, // INS_crcc_w_d_w
        6876, 7, // INS_amcas_b
        6884, 7, // INS_amcas_h
        6892, 7, // INS_amcas_w
        6900, 7, // INS_amcas_d
        6908, 8, // INS_amswap_b
        6917, 8, // INS_amswap_h
        6926, 8, // INS_amswap_w
        6935, 8, // INS_amswap_d
        6944, 7, // INS_amadd_b
        6952, 7, // INS_amadd_h
        6960, 7, // INS_amadd_w
        6968, 7, // INS_amadd_d
        6976, 7, // INS_amand_w
        6984, 7, // INS_amand_d
        6992, 6, // INS_amor_w
        6999, 6, // INS_amor_d
        7006, 7, // INS_amxor_w
        7014, 7, // INS_amxor_d
        7022, 7, // INS_ammax_w
        7030, 7, // INS_ammax_d
        7038, 7, // INS_ammin_w
        7046, 7, // INS_ammin_d
        7054, 8, // INS_ammax_wu
        7063, 8, // INS_ammax_du
        7072, 8, // INS_ammin_wu
        7081, 8, // INS_ammin_du
        7090, 10, // INS_amcas_db_b
        7101, 10, // INS_amcas_db_h
        7112, 10, // INS_amcas_db_w
        7123, 10, // INS_amcas_db_d
        7134, 11, // INS_amswap_db_b
        7146, 11, // INS_amswap_db_h
        7158, 11, // INS_amswap_db_w
        7170, 11, // INS_amswap_db_d
        7182, 10, // INS_amadd_db_b
        7193, 10, // INS_amadd_db_h
        7204, 10, // INS_amadd_db_w
        7215, 10, // INS_amadd_db_d
        7226, 10, // INS_amand_db_w
        7237, 10, // INS_amand_db_d
        7248, 9, // INS_amor_db_w
        7258, 9, // INS_amor_db_d
        7268, 10, // INS_amxor_db_w
        7279, 10, // INS_amxor_db_d
        7290, 10, // INS_ammax_db_w
        7301, 10, // INS_ammax_db_d
        7312, 10, // INS_ammin_db_w
        7323, 10, // INS_ammin_db_d
        7334, 11, // INS_ammax_db_wu
        7346, 11, // INS_ammax_db_du
        7358, 11, // INS_ammin_db_wu
        7370, 11, // INS_ammin_db_du
        7382, 6, // INS_alsl_w
        7389, 7, // INS_alsl_wu
        7397, 6, // INS_alsl_d
        7404, 10, // INS_bytepick_w
        7415, 10, // INS_bytepick_d
        7426, 4, // INS_fsel
        7431, 7, // INS_lu12i_w
        7439, 7, // INS_lu32i_d
        7447, 6, // INS_pcaddi
        7454, 9, // INS_pcaddu12i
        7464, 9, // INS_pcalau12i
        7474, 9, // INS_pcaddu18i
        7484, 7, // INS_ext_w_b
        7492, 7, // INS_ext_w_h
        7500, 5, // INS_clo_w
        7506, 5, // INS_clz_w
        7512, 5, // INS_cto_w
        7518, 5, // INS_ctz_w
        7524, 5, // INS_clo_d
        7530, 5, // INS_clz_d
        7536, 5, // INS_cto_d
        7542, 5, // INS_ctz_d
        7548, 7, // INS_revb_2h
        7556, 7, // INS_revb_4h
        7564, 7, // INS_revb_2w
        7572, 6, // INS_revb_d
        7579, 7, // INS_revh_2w
        7587, 6, // INS_revh_d
        7594, 9, // INS_bitrev_4b
        7604, 9, // INS_bitrev_8b
        7614, 8, // INS_bitrev_w
        7623, 8, // INS_bitrev_d
        7632, 9, // INS_rdtimel_w
        7642, 9, // INS_rdtimeh_w
        7652, 8, // INS_rdtime_d
        7661, 6, // INS_cpucfg
        7668, 10, // INS_movfr2gr_s
        7679, 10, // INS_movfr2gr_d
        7690, 11, // INS_movfrh2gr_s
        7702, 9, // INS_bstrins_w
        7712, 9, // INS_bstrins_d
        7722, 10, // INS_bstrpick_w
        7733, 10, // INS_bstrpick_d
        7744, 4, // INS_ld_b
        7749, 4, // INS_ld_h
        7754, 4, // INS_ld_w
        7759, 4, // INS_ld_d
        7764, 5, // INS_ld_bu
        7770, 5, // INS_ld_hu
        7776, 5, // INS_ld_wu
        7782, 7, // INS_ldptr_w
        7790, 7, // INS_ldptr_d
        7798, 4, // INS_ll_w
        7803, 4, // INS_ll_d
        7808, 5, // INS_ldx_b
        7814, 5, // INS_ldx_h
        7820, 5, // INS_ldx_w
        7826, 5, // INS_ldx_d
        7832, 6, // INS_ldx_bu
        7839, 6, // INS_ldx_hu
        7846, 6, // INS_ldx_wu
        7853, 6, // INS_ldgt_b
        7860, 6, // INS_ldgt_h
        7867, 6, // INS_ldgt_w
        7874, 6, // INS_ldgt_d
        7881, 6, // INS_ldle_b
        7888, 6, // INS_ldle_h
        7895, 6, // INS_ldle_w
        7902, 6, // INS_ldle_d
        7909, 6, // INS_addi_w
        7916, 6, // INS_addi_d
        7923, 7, // INS_lu52i_d
        7931, 4, // INS_slti
        7936, 5, // INS_sltui
        7942, 4, // INS_andi
        7947, 3, // INS_ori
        7951, 4, // INS_xori
        7956, 6, // INS_slli_w
        7963, 6, // INS_srli_w
        7970, 6, // INS_srai_w
        7977, 7, // INS_rotri_w
        7985, 6, // INS_slli_d
        7992, 6, // INS_srli_d
        7999, 6, // INS_srai_d
        8006, 7, // INS_rotri_d
        8014, 9, // INS_addu16i_d
        8024, 4, // INS_jirl
        8029, 4, // INS_st_b
        8034, 4, // INS_st_h
        8039, 4, // INS_st_w
        8044, 4, // INS_st_d
        8049, 7, // INS_stptr_w
        8057, 7, // INS_stptr_d
        8065, 4, // INS_sc_w
        8070, 4, // INS_sc_d
        8075, 5, // INS_stx_b
        8081, 5, // INS_stx_h
        8087, 5, // INS_stx_w
        8093, 5, // INS_stx_d
        8099, 6, // INS_stgt_b
        8106, 6, // INS_stgt_h
        8113, 6, // INS_stgt_w
        8120, 6, // INS_stgt_d
        8127, 6, // INS_stle_b
        8134, 6, // INS_stle_h
        8141, 6, // INS_stle_w
        8148, 6, // INS_stle_d
        8155, 4, // INS_dbar
        8160, 4, // INS_ibar
        8165, 7, // INS_syscall
        8173, 5, // INS_break
        8179, 5, // INS_preld
        8185, 6, // INS_preldx
        8192, 6, // INS_fadd_s
        8199, 6, // INS_fadd_d
        8206, 6, // INS_fsub_s
        8213, 6, // INS_fsub_d
        8220, 6, // INS_fmul_s
        8227, 6, // INS_fmul_d
        8234, 6, // INS_fdiv_s
        8241, 6, // INS_fdiv_d
        8248, 6, // INS_fmax_s
        8255, 6, // INS_fmax_d
        8262, 6, // INS_fmin_s
        8269, 6, // INS_fmin_d
        8276, 7, // INS_fmaxa_s
        8284, 7, // INS_fmaxa_d
        8292, 7, // INS_fmina_s
        8300, 7, // INS_fmina_d
        8308, 9, // INS_fscaleb_s
        8318, 9, // INS_fscaleb_d
        8328, 11, // INS_fcopysign_s
        8340, 11, // INS_fcopysign_d
        8352, 6, // INS_fldx_s
        8359, 6, // INS_fldx_d
        8366, 6, // INS_fstx_s
        8373, 6, // INS_fstx_d
        8380, 7, // INS_fldgt_s
        8388, 7, // INS_fldgt_d
        8396, 7, // INS_fldle_s
        8404, 7, // INS_fldle_d
        8412, 7, // INS_fstgt_s
        8420, 7, // INS_fstgt_d
        8428, 7, // INS_fstle_s
        8436, 7, // INS_fstle_d
        8444, 7, // INS_fmadd_s
        8452, 7, // INS_fmadd_d
        8460, 7, // INS_fmsub_s
        8468, 7, // INS_fmsub_d
        8476, 8, // INS_fnmadd_s
        8485, 8, // INS_fnmadd_d
        8494, 8, // INS_fnmsub_s
        8503, 8, // INS_fnmsub_d
        8512, 6, // INS_fabs_s
        8519, 6, // INS_fabs_d
        8526, 6, // INS_fneg_s
        8533, 6, // INS_fneg_d
        8540, 7, // INS_fsqrt_s
        8548, 7, // INS_fsqrt_d
        8556, 8, // INS_frsqrt_s
        8565, 8, // INS_frsqrt_d
        8574, 9, // INS_frsqrte_s
        8584, 9, // INS_frsqrte_d
        8594, 8, // INS_frecip_s
        8603, 8, // INS_frecip_d
        8612, 9, // INS_frecipe_s
        8622, 9, // INS_frecipe_d
        8632, 7, // INS_flogb_s
        8640, 7, // INS_flogb_d
        8648, 8, // INS_fclass_s
        8657, 8, // INS_fclass_d
        8666, 8, // INS_fcvt_s_d
        8675, 8, // INS_fcvt_d_s
        8684, 9, // INS_ffint_s_w
        8694, 9, // INS_ffint_s_l
        8704, 9, // INS_ffint_d_w
        8714, 9, // INS_ffint_d_l
        8724, 9, // INS_ftint_w_s
        8734, 9, // INS_ftint_w_d
        8744, 9, // INS_ftint_l_s
        8754, 9, // INS_ftint_l_d
        8764, 11, // INS_ftintrm_w_s
        8776, 11, // INS_ftintrm_w_d
        8788, 11, // INS_ftintrm_l_s
        8800, 11, // INS_ftintrm_l_d
        8812, 11, // INS_ftintrp_w_s
        8824, 11, // INS_ftintrp_w_d
        8836, 11, // INS_ftintrp_l_s
        8848, 11, // INS_ftintrp_l_d
        8860, 11, // INS_ftintrz_w_s
        8872, 11, // INS_ftintrz_w_d
        8884, 11, // INS_ftintrz_l_s
        8896, 11, // INS_ftintrz_l_d
        8908, 12, // INS_ftintrne_w_s
        8921, 12, // INS_ftintrne_w_d
        8934, 12, // INS_ftintrne_l_s
        8947, 12, // INS_ftintrne_l_d
        8960, 7, // INS_frint_s
        8968, 7, // INS_frint_d
        8976, 6, // INS_fmov_s
        8983, 6, // INS_fmov_d
        8990, 10, // INS_movgr2fr_w
        9001, 10, // INS_movgr2fr_d
        9012, 11, // INS_movgr2frh_w
        9024, 10, // INS_movgr2fcsr
        9035, 10, // INS_movfcsr2gr
        9046, 8, // INS_movfr2cf
        9055, 8, // INS_movcf2fr
        9064, 8, // INS_movgr2cf
        9073, 8, // INS_movcf2gr
        9082, 10, // INS_fcmp_caf_s
        9093, 10, // INS_fcmp_cun_s
        9104, 10, // INS_fcmp_ceq_s
        9115, 11, // INS_fcmp_cueq_s
        9127, 10, // INS_fcmp_clt_s
        9138, 11, // INS_fcmp_cult_s
        9150, 10, // INS_fcmp_cle_s
        9161, 11, // INS_fcmp_cule_s
        9173, 10, // INS_fcmp_cne_s
        9184, 10, // INS_fcmp_cor_s
        9195, 11, // INS_fcmp_cune_s
        9207, 10, // INS_fcmp_saf_d
        9218, 10, // INS_fcmp_sun_d
        9229, 10, // INS_fcmp_seq_d
        9240, 11, // INS_fcmp_sueq_d
        9252, 10, // INS_fcmp_slt_d
        9263, 11, // INS_fcmp_sult_d
        9275, 10, // INS_fcmp_sle_d
        9286, 11, // INS_fcmp_sule_d
        9298, 10, // INS_fcmp_sne_d
        9309, 10, // INS_fcmp_sor_d
        9320, 11, // INS_fcmp_sune_d
        9332, 10, // INS_fcmp_caf_d
        9343, 10, // INS_fcmp_cun_d
        9354, 10, // INS_fcmp_ceq_d
        9365, 11, // INS_fcmp_cueq_d
        9377, 10, // INS_fcmp_clt_d
        9388, 11, // INS_fcmp_cult_d
        9400, 10, // INS_fcmp_cle_d
        9411, 11, // INS_fcmp_cule_d
        9423, 10, // INS_fcmp_cne_d
        9434, 10, // INS_fcmp_cor_d
        9445, 11, // INS_fcmp_cune_d
        9457, 10, // INS_fcmp_saf_s
        9468, 10, // INS_fcmp_sun_s
        9479, 10, // INS_fcmp_seq_s
        9490, 11, // INS_fcmp_sueq_s
        9502, 10, // INS_fcmp_slt_s
        9513, 11, // INS_fcmp_sult_s
        9525, 10, // INS_fcmp_sle_s
        9536, 11, // INS_fcmp_sule_s
        9548, 10, // INS_fcmp_sne_s
        9559, 10, // INS_fcmp_sor_s
        9570, 11, // INS_fcmp_sune_s
        9582, 5, // INS_fld_s
        9588, 5, // INS_fld_d
        9594, 5, // INS_fst_s
        9600, 5, // INS_fst_d
        9606, 8, // INS_vfmadd_s
        9615, 8, // INS_vfmadd_d
        9624, 8, // INS_vfmsub_s
        9633, 8, // INS_vfmsub_d
        9642, 9, // INS_vfnmadd_s
        9652, 9, // INS_vfnmadd_d
        9662, 9, // INS_vfnmsub_s
        9672, 9, // INS_vfnmsub_d
        9682, 9, // INS_vbitsel_v
        9692, 7, // INS_vshuf_b
        9700, 4, // INS_vldx
        9705, 4, // INS_vstx
        9710, 9, // INS_vreplve_b
        9720, 9, // INS_vreplve_h
        9730, 9, // INS_vreplve_w
        9740, 9, // INS_vreplve_d
        9750, 11, // INS_vfcmp_caf_s
        9762, 11, // INS_vfcmp_cun_s
        9774, 11, // INS_vfcmp_ceq_s
        9786, 12, // INS_vfcmp_cueq_s
        9799, 11, // INS_vfcmp_clt_s
        9811, 12, // INS_vfcmp_cult_s
        9824, 11, // INS_vfcmp_cle_s
        9836, 12, // INS_vfcmp_cule_s
        9849, 11, // INS_vfcmp_cne_s
        9861, 11, // INS_vfcmp_cor_s
        9873, 12, // INS_vfcmp_cune_s
        9886, 11, // INS_vfcmp_saf_s
        9898, 11, // INS_vfcmp_sun_s
        9910, 11, // INS_vfcmp_seq_s
        9922, 12, // INS_vfcmp_sueq_s
        9935, 11, // INS_vfcmp_slt_s
        9947, 12, // INS_vfcmp_sult_s
        9960, 11, // INS_vfcmp_sle_s
        9972, 12, // INS_vfcmp_sule_s
        9985, 11, // INS_vfcmp_sne_s
        9997, 11, // INS_vfcmp_sor_s
        10009, 12, // INS_vfcmp_sune_s
        10022, 11, // INS_vfcmp_caf_d
        10034, 11, // INS_vfcmp_cun_d
        10046, 11, // INS_vfcmp_ceq_d
        10058, 12, // INS_vfcmp_cueq_d
        10071, 11, // INS_vfcmp_clt_d
        10083, 12, // INS_vfcmp_cult_d
        10096, 11, // INS_vfcmp_cle_d
        10108, 12, // INS_vfcmp_cule_d
        10121, 11, // INS_vfcmp_cne_d
        10133, 11, // INS_vfcmp_cor_d
        10145, 12, // INS_vfcmp_cune_d
        10158, 11, // INS_vfcmp_saf_d
        10170, 11, // INS_vfcmp_sun_d
        10182, 11, // INS_vfcmp_seq_d
        10194, 12, // INS_vfcmp_sueq_d
        10207, 11, // INS_vfcmp_slt_d
        10219, 12, // INS_vfcmp_sult_d
        10232, 11, // INS_vfcmp_sle_d
        10244, 12, // INS_vfcmp_sule_d
        10257, 11, // INS_vfcmp_sne_d
        10269, 11, // INS_vfcmp_sor_d
        10281, 12, // INS_vfcmp_sune_d
        10294, 6, // INS_vseq_b
        10301, 6, // INS_vseq_h
        10308, 6, // INS_vseq_w
        10315, 6, // INS_vseq_d
        10322, 6, // INS_vsle_b
        10329, 6, // INS_vsle_h
        10336, 6, // INS_vsle_w
        10343, 6, // INS_vsle_d
        10350, 7, // INS_vsle_bu
        10358, 7, // INS_vsle_hu
        10366, 7, // INS_vsle_wu
        10374, 7, // INS_vsle_du
        10382, 6, // INS_vslt_b
        10389, 6, // INS_vslt_h
        10396, 6, // INS_vslt_w
        10403, 6, // INS_vslt_d
        10410, 7, // INS_vslt_bu
        10418, 7, // INS_vslt_hu
        10426, 7, // INS_vslt_wu
        10434, 7, // INS_vslt_du
        10442, 6, // INS_vadd_b
        10449, 6, // INS_vadd_h
        10456, 6, // INS_vadd_w
        10463, 6, // INS_vadd_d
        10470, 6, // INS_vsub_b
        10477, 6, // INS_vsub_h
        10484, 6, // INS_vsub_w
        10491, 6, // INS_vsub_d
        10498, 11, // INS_vaddwev_h_b
        10510, 11, // INS_vaddwev_w_h
        10522, 11, // INS_vaddwev_d_w
        10534, 11, // INS_vaddwev_q_d
        10546, 11, // INS_vsubwev_h_b
        10558, 11, // INS_vsubwev_w_h
        10570, 11, // INS_vsubwev_d_w
        10582, 11, // INS_vsubwev_q_d
        10594, 11, // INS_vaddwod_h_b
        10606, 11, // INS_vaddwod_w_h
        10618, 11, // INS_vaddwod_d_w
        10630, 11, // INS_vaddwod_q_d
        10642, 11, // INS_vsubwod_h_b
        10654, 11, // INS_vsubwod_w_h
        10666, 11, // INS_vsubwod_d_w
        10678, 11, // INS_vsubwod_q_d
        10690, 12, // INS_vaddwev_h_bu
        10703, 12, // INS_vaddwev_w_hu
        10716, 12, // INS_vaddwev_d_wu
        10729, 12, // INS_vaddwev_q_du
        10742, 12, // INS_vsubwev_h_bu
        10755, 12, // INS_vsubwev_w_hu
        10768, 12, // INS_vsubwev_d_wu
        10781, 12, // INS_vsubwev_q_du
        10794, 12, // INS_vaddwod_h_bu
        10807, 12, // INS_vaddwod_w_hu
        10820, 12, // INS_vaddwod_d_wu
        10833, 12, // INS_vaddwod_q_du
        10846, 12, // INS_vsubwod_h_bu
        10859, 12, // INS_vsubwod_w_hu
        10872, 12, // INS_vsubwod_d_wu
        10885, 12, // INS_vsubwod_q_du
        10898, 14, // INS_vaddwev_h_bu_b
        10913, 14, // INS_vaddwev_w_hu_h
        10928, 14, // INS_vaddwev_d_wu_w
        10943, 14, // INS_vaddwev_q_du_d
        10958, 14, // INS_vaddwod_h_bu_b
        10973, 14, // INS_vaddwod_w_hu_h
        10988, 14, // INS_vaddwod_d_wu_w
        11003, 14, // INS_vaddwod_q_du_d
        11018, 7, // INS_vsadd_b
        11026, 7, // INS_vsadd_h
        11034, 7, // INS_vsadd_w
        11042, 7, // INS_vsadd_d
        11050, 7, // INS_vssub_b
        11058, 7, // INS_vssub_h
        11066, 7, // INS_vssub_w
        11074, 7, // INS_vssub_d
        11082, 8, // INS_vsadd_bu
        11091, 8, // INS_vsadd_hu
        11100, 8, // INS_vsadd_wu
        11109, 8, // INS_vsadd_du
        11118, 8, // INS_vssub_bu
        11127, 8, // INS_vssub_hu
        11136, 8, // INS_vssub_wu
        11145, 8, // INS_vssub_du
        11154, 10, // INS_vhaddw_h_b
        11165, 10, // INS_vhaddw_w_h
        11176, 10, // INS_vhaddw_d_w
        11187, 10, // INS_vhaddw_q_d
        11198, 10, // INS_vhsubw_h_b
        11209, 10, // INS_vhsubw_w_h
        11220, 10, // INS_vhsubw_d_w
        11231, 10, // INS_vhsubw_q_d
        11242, 12, // INS_vhaddw_hu_bu
        11255, 12, // INS_vhaddw_wu_hu
        11268, 12, // INS_vhaddw_du_wu
        11281, 12, // INS_vhaddw_qu_du
        11294, 12, // INS_vhsubw_hu_bu
        11307, 12, // INS_vhsubw_wu_hu
        11320, 12, // INS_vhsubw_du_wu
        11333, 12, // INS_vhsubw_qu_du
        11346, 7, // INS_vadda_b
        11354, 7, // INS_vadda_h
        11362, 7, // INS_vadda_w
        11370, 7, // INS_vadda_d
        11378, 7, // INS_vabsd_b
        11386, 7, // INS_vabsd_h
        11394, 7, // INS_vabsd_w
        11402, 7, // INS_vabsd_d
        11410, 8, // INS_vabsd_bu
        11419, 8, // INS_vabsd_hu
        11428, 8, // INS_vabsd_wu
        11437, 8, // INS_vabsd_du
        11446, 6, // INS_vavg_b
        11453, 6, // INS_vavg_h
        11460, 6, // INS_vavg_w
        11467, 6, // INS_vavg_d
        11474, 7, // INS_vavg_bu
        11482, 7, // INS_vavg_hu
        11490, 7, // INS_vavg_wu
        11498, 7, // INS_vavg_du
        11506, 7, // INS_vavgr_b
        11514, 7, // INS_vavgr_h
        11522, 7, // INS_vavgr_w
        11530, 7, // INS_vavgr_d
        11538, 8, // INS_vavgr_bu
        11547, 8, // INS_vavgr_hu
        11556, 8, // INS_vavgr_wu
        11565, 8, // INS_vavgr_du
        11574, 6, // INS_vmax_b
        11581, 6, // INS_vmax_h
        11588, 6, // INS_vmax_w
        11595, 6, // INS_vmax_d
        11602, 6, // INS_vmin_b
        11609, 6, // INS_vmin_h
        11616, 6, // INS_vmin_w
        11623, 6, // INS_vmin_d
        11630, 7, // INS_vmax_bu
        11638, 7, // INS_vmax_hu
        11646, 7, // INS_vmax_wu
        11654, 7, // INS_vmax_du
        11662, 7, // INS_vmin_bu
        11670, 7, // INS_vmin_hu
        11678, 7, // INS_vmin_wu
        11686, 7, // INS_vmin_du
        11694, 6, // INS_vmul_b
        11701, 6, // INS_vmul_h
        11708, 6, // INS_vmul_w
        11715, 6, // INS_vmul_d
        11722, 6, // INS_vmuh_b
        11729, 6, // INS_vmuh_h
        11736, 6, // INS_vmuh_w
        11743, 6, // INS_vmuh_d
        11750, 7, // INS_vmuh_bu
        11758, 7, // INS_vmuh_hu
        11766, 7, // INS_vmuh_wu
        11774, 7, // INS_vmuh_du
        11782, 11, // INS_vmulwev_h_b
        11794, 11, // INS_vmulwev_w_h
        11806, 11, // INS_vmulwev_d_w
        11818, 11, // INS_vmulwev_q_d
        11830, 11, // INS_vmulwod_h_b
        11842, 11, // INS_vmulwod_w_h
        11854, 11, // INS_vmulwod_d_w
        11866, 11, // INS_vmulwod_q_d
        11878, 12, // INS_vmulwev_h_bu
        11891, 12, // INS_vmulwev_w_hu
        11904, 12, // INS_vmulwev_d_wu
        11917, 12, // INS_vmulwev_q_du
        11930, 12, // INS_vmulwod_h_bu
        11943, 12, // INS_vmulwod_w_hu
        11956, 12, // INS_vmulwod_d_wu
        11969, 12, // INS_vmulwod_q_du
        11982, 14, // INS_vmulwev_h_bu_b
        11997, 14, // INS_vmulwev_w_hu_h
        12012, 14, // INS_vmulwev_d_wu_w
        12027, 14, // INS_vmulwev_q_du_d
        12042, 14, // INS_vmulwod_h_bu_b
        12057, 14, // INS_vmulwod_w_hu_h
        12072, 14, // INS_vmulwod_d_wu_w
        12087, 14, // INS_vmulwod_q_du_d
        12102, 7, // INS_vmadd_b
        12110, 7, // INS_vmadd_h
        12118, 7, // INS_vmadd_w
        12126, 7, // INS_vmadd_d
        12134, 7, // INS_vmsub_b
        12142, 7, // INS_vmsub_h
        12150, 7, // INS_vmsub_w
        12158, 7, // INS_vmsub_d
        12166, 12, // INS_vmaddwev_h_b
        12179, 12, // INS_vmaddwev_w_h
        12192, 12, // INS_vmaddwev_d_w
        12205, 12, // INS_vmaddwev_q_d
        12218, 12, // INS_vmaddwod_h_b
        12231, 12, // INS_vmaddwod_w_h
        12244, 12, // INS_vmaddwod_d_w
        12257, 12, // INS_vmaddwod_q_d
        12270, 13, // INS_vmaddwev_h_bu
        12284, 13, // INS_vmaddwev_w_hu
        12298, 13, // INS_vmaddwev_d_wu
        12312, 13, // INS_vmaddwev_q_du
        12326, 13, // INS_vmaddwod_h_bu
        12340, 13, // INS_vmaddwod_w_hu
        12354, 13, // INS_vmaddwod_d_wu
        12368, 13, // INS_vmaddwod_q_du
        12382, 15, // INS_vmaddwev_h_bu_b
        12398, 15, // INS_vmaddwev_w_hu_h
        12414, 15, // INS_vmaddwev_d_wu_w
        12430, 15, // INS_vmaddwev_q_du_d
        12446, 15, // INS_vmaddwod_h_bu_b
        12462, 15, // INS_vmaddwod_w_hu_h
        12478, 15, // INS_vmaddwod_d_wu_w
        12494, 15, // INS_vmaddwod_q_du_d
        12510, 6, // INS_vdiv_b
        12517, 6, // INS_vdiv_h
        12524, 6, // INS_vdiv_w
        12531, 6, // INS_vdiv_d
        12538, 6, // INS_vmod_b
        12545, 6, // INS_vmod_h
        12552, 6, // INS_vmod_w
        12559, 6, // INS_vmod_d
        12566, 7, // INS_vdiv_bu
        12574, 7, // INS_vdiv_hu
        12582, 7, // INS_vdiv_wu
        12590, 7, // INS_vdiv_du
        12598, 7, // INS_vmod_bu
        12606, 7, // INS_vmod_hu
        12614, 7, // INS_vmod_wu
        12622, 7, // INS_vmod_du
        12630, 6, // INS_vsll_b
        12637, 6, // INS_vsll_h
        12644, 6, // INS_vsll_w
        12651, 6, // INS_vsll_d
        12658, 6, // INS_vsrl_b
        12665, 6, // INS_vsrl_h
        12672, 6, // INS_vsrl_w
        12679, 6, // INS_vsrl_d
        12686, 6, // INS_vsra_b
        12693, 6, // INS_vsra_h
        12700, 6, // INS_vsra_w
        12707, 6, // INS_vsra_d
        12714, 7, // INS_vrotr_b
        12722, 7, // INS_vrotr_h
        12730, 7, // INS_vrotr_w
        12738, 7, // INS_vrotr_d
        12746, 7, // INS_vsrlr_b
        12754, 7, // INS_vsrlr_h
        12762, 7, // INS_vsrlr_w
        12770, 7, // INS_vsrlr_d
        12778, 7, // INS_vsrar_b
        12786, 7, // INS_vsrar_h
        12794, 7, // INS_vsrar_w
        12802, 7, // INS_vsrar_d
        12810, 9, // INS_vsrln_b_h
        12820, 9, // INS_vsrln_h_w
        12830, 9, // INS_vsrln_w_d
        12840, 9, // INS_vsran_b_h
        12850, 9, // INS_vsran_h_w
        12860, 9, // INS_vsran_w_d
        12870, 10, // INS_vsrlrn_b_h
        12881, 10, // INS_vsrlrn_h_w
        12892, 10, // INS_vsrlrn_w_d
        12903, 10, // INS_vsrarn_b_h
        12914, 10, // INS_vsrarn_h_w
        12925, 10, // INS_vsrarn_w_d
        12936, 10, // INS_vssrln_b_h
        12947, 10, // INS_vssrln_h_w
        12958, 10, // INS_vssrln_w_d
        12969, 10, // INS_vssran_b_h
        12980, 10, // INS_vssran_h_w
        12991, 10, // INS_vssran_w_d
        13002, 11, // INS_vssrlrn_b_h
        13014, 11, // INS_vssrlrn_h_w
        13026, 11, // INS_vssrlrn_w_d
        13038, 11, // INS_vssrarn_b_h
        13050, 11, // INS_vssrarn_h_w
        13062, 11, // INS_vssrarn_w_d
        13074, 11, // INS_vssrln_bu_h
        13086, 11, // INS_vssrln_hu_w
        13098, 11, // INS_vssrln_wu_d
        13110, 11, // INS_vssran_bu_h
        13122, 11, // INS_vssran_hu_w
        13134, 11, // INS_vssran_wu_d
        13146, 12, // INS_vssrlrn_bu_h
        13159, 12, // INS_vssrlrn_hu_w
        13172, 12, // INS_vssrlrn_wu_d
        13185, 12, // INS_vssrarn_bu_h
        13198, 12, // INS_vssrarn_hu_w
        13211, 12, // INS_vssrarn_wu_d
        13224, 9, // INS_vbitclr_b
        13234, 9, // INS_vbitclr_h
        13244, 9, // INS_vbitclr_w
        13254, 9, // INS_vbitclr_d
        13264, 9, // INS_vbitset_b
        13274, 9, // INS_vbitset_h
        13284, 9, // INS_vbitset_w
        13294, 9, // INS_vbitset_d
        13304, 9, // INS_vbitrev_b
        13314, 9, // INS_vbitrev_h
        13324, 9, // INS_vbitrev_w
        13334, 9, // INS_vbitrev_d
        13344, 9, // INS_vpackev_b
        13354, 9, // INS_vpackev_h
        13364, 9, // INS_vpackev_w
        13374, 9, // INS_vpackev_d
        13384, 9, // INS_vpackod_b
        13394, 9, // INS_vpackod_h
        13404, 9, // INS_vpackod_w
        13414, 9, // INS_vpackod_d
        13424, 7, // INS_vilvl_b
        13432, 7, // INS_vilvl_h
        13440, 7, // INS_vilvl_w
        13448, 7, // INS_vilvl_d
        13456, 7, // INS_vilvh_b
        13464, 7, // INS_vilvh_h
        13472, 7, // INS_vilvh_w
        13480, 7, // INS_vilvh_d
        13488, 9, // INS_vpickev_b
        13498, 9, // INS_vpickev_h
        13508, 9, // INS_vpickev_w
        13518, 9, // INS_vpickev_d
        13528, 9, // INS_vpickod_b
        13538, 9, // INS_vpickod_h
        13548, 9, // INS_vpickod_w
        13558, 9, // INS_vpickod_d
        13568, 6, // INS_vand_v
        13575, 5, // INS_vor_v
        13581, 6, // INS_vxor_v
        13588, 6, // INS_vnor_v
        13595, 7, // INS_vandn_v
        13603, 6, // INS_vorn_v
        13610, 8, // INS_vfrstp_b
        13619, 8, // INS_vfrstp_h
        13628, 6, // INS_vadd_q
        13635, 6, // INS_vsub_q
        13642, 10, // INS_vsigncov_b
        13653, 10, // INS_vsigncov_h
        13664, 10, // INS_vsigncov_w
        13675, 10, // INS_vsigncov_d
        13686, 7, // INS_vfadd_s
        13694, 7, // INS_vfadd_d
        13702, 7, // INS_vfsub_s
        13710, 7, // INS_vfsub_d
        13718, 7, // INS_vfmul_s
        13726, 7, // INS_vfmul_d
        13734, 7, // INS_vfdiv_s
        13742, 7, // INS_vfdiv_d
        13750, 7, // INS_vfmax_s
        13758, 7, // INS_vfmax_d
        13766, 7, // INS_vfmin_s
        13774, 7, // INS_vfmin_d
        13782, 8, // INS_vfmaxa_s
        13791, 8, // INS_vfmaxa_d
        13800, 8, // INS_vfmina_s
        13809, 8, // INS_vfmina_d
        13818, 9, // INS_vfcvt_h_s
        13828, 9, // INS_vfcvt_s_d
        13838, 10, // INS_vffint_s_l
        13849, 10, // INS_vftint_w_d
        13860, 12, // INS_vftintrm_w_d
        13873, 12, // INS_vftintrp_w_d
        13886, 12, // INS_vftintrz_w_d
        13899, 13, // INS_vftintrne_w_d
        13913, 7, // INS_vshuf_h
        13921, 7, // INS_vshuf_w
        13929, 7, // INS_vshuf_d
        13937, 12, // INS_vreplgr2vr_b
        13950, 12, // INS_vreplgr2vr_h
        13963, 12, // INS_vreplgr2vr_w
        13976, 12, // INS_vreplgr2vr_d
        13989, 6, // INS_vclo_b
        13996, 6, // INS_vclo_h
        14003, 6, // INS_vclo_w
        14010, 6, // INS_vclo_d
        14017, 6, // INS_vclz_b
        14024, 6, // INS_vclz_h
        14031, 6, // INS_vclz_w
        14038, 6, // INS_vclz_d
        14045, 7, // INS_vpcnt_b
        14053, 7, // INS_vpcnt_h
        14061, 7, // INS_vpcnt_w
        14069, 7, // INS_vpcnt_d
        14077, 6, // INS_vneg_b
        14084, 6, // INS_vneg_h
        14091, 6, // INS_vneg_w
        14098, 6, // INS_vneg_d
        14105, 9, // INS_vmskltz_b
        14115, 9, // INS_vmskltz_h
        14125, 9, // INS_vmskltz_w
        14135, 9, // INS_vmskltz_d
        14145, 9, // INS_vmskgez_b
        14155, 8, // INS_vmsknz_b
        14164, 8, // INS_vflogb_s
        14173, 8, // INS_vflogb_d
        14182, 9, // INS_vfclass_s
        14192, 9, // INS_vfclass_d
        14202, 8, // INS_vfsqrt_s
        14211, 8, // INS_vfsqrt_d
        14220, 9, // INS_vfrecip_s
        14230, 9, // INS_vfrecip_d
        14240, 9, // INS_vfrsqrt_s
        14250, 9, // INS_vfrsqrt_d
        14260, 10, // INS_vfrecipe_s
        14271, 10, // INS_vfrecipe_d
        14282, 10, // INS_vfrsqrte_s
        14293, 10, // INS_vfrsqrte_d
        14304, 8, // INS_vfrint_s
        14313, 8, // INS_vfrint_d
        14322, 10, // INS_vfrintrm_s
        14333, 10, // INS_vfrintrm_d
        14344, 10, // INS_vfrintrp_s
        14355, 10, // INS_vfrintrp_d
        14366, 10, // INS_vfrintrz_s
        14377, 10, // INS_vfrintrz_d
        14388, 11, // INS_vfrintrne_s
        14400, 11, // INS_vfrintrne_d
        14412, 10, // INS_vfcvtl_s_h
        14423, 10, // INS_vfcvth_s_h
        14434, 10, // INS_vfcvtl_d_s
        14445, 10, // INS_vfcvth_d_s
        14456, 10, // INS_vffint_s_w
        14467, 11, // INS_vffint_s_wu
        14479, 10, // INS_vffint_d_l
        14490, 11, // INS_vffint_d_lu
        14502, 11, // INS_vffintl_d_w
        14514, 11, // INS_vffinth_d_w
        14526, 10, // INS_vftint_w_s
        14537, 10, // INS_vftint_l_d
        14548, 12, // INS_vftintrm_w_s
        14561, 12, // INS_vftintrm_l_d
        14574, 12, // INS_vftintrp_w_s
        14587, 12, // INS_vftintrp_l_d
        14600, 12, // INS_vftintrz_w_s
        14613, 12, // INS_vftintrz_l_d
        14626, 13, // INS_vftintrne_w_s
        14640, 13, // INS_vftintrne_l_d
        14654, 11, // INS_vftint_wu_s
        14666, 11, // INS_vftint_lu_d
        14678, 13, // INS_vftintrz_wu_s
        14692, 13, // INS_vftintrz_lu_d
        14706, 11, // INS_vftintl_l_s
        14718, 11, // INS_vftinth_l_s
        14730, 13, // INS_vftintrml_l_s
        14744, 13, // INS_vftintrmh_l_s
        14758, 13, // INS_vftintrpl_l_s
        14772, 13, // INS_vftintrph_l_s
        14786, 13, // INS_vftintrzl_l_s
        14800, 13, // INS_vftintrzh_l_s
        14814, 14, // INS_vftintrnel_l_s
        14829, 14, // INS_vftintrneh_l_s
        14844, 9, // INS_vexth_h_b
        14854, 9, // INS_vexth_w_h
        14864, 9, // INS_vexth_d_w
        14874, 9, // INS_vexth_q_d
        14884, 11, // INS_vexth_hu_bu
        14896, 11, // INS_vexth_wu_hu
        14908, 11, // INS_vexth_du_wu
        14920, 11, // INS_vexth_qu_du
        14932, 9, // INS_vextl_q_d
        14942, 11, // INS_vextl_qu_du
        14954, 4, // INS_vldi
        14959, 9, // INS_vseteqz_v
        14969, 9, // INS_vsetnez_v
        14979, 12, // INS_vsetanyeqz_b
        14992, 12, // INS_vsetanyeqz_h
        15005, 12, // INS_vsetanyeqz_w
        15018, 12, // INS_vsetanyeqz_d
        15031, 12, // INS_vsetallnez_b
        15044, 12, // INS_vsetallnez_h
        15057, 12, // INS_vsetallnez_w
        15070, 12, // INS_vsetallnez_d
        15083, 8, // INS_vstelm_b
        15092, 8, // INS_vstelm_h
        15101, 8, // INS_vstelm_w
        15110, 8, // INS_vstelm_d
        15119, 7, // INS_vseqi_b
        15127, 7, // INS_vseqi_h
        15135, 7, // INS_vseqi_w
        15143, 7, // INS_vseqi_d
        15151, 7, // INS_vslei_b
        15159, 7, // INS_vslei_h
        15167, 7, // INS_vslei_w
        15175, 7, // INS_vslei_d
        15183, 7, // INS_vslti_b
        15191, 7, // INS_vslti_h
        15199, 7, // INS_vslti_w
        15207, 7, // INS_vslti_d
        15215, 7, // INS_vmaxi_b
        15223, 7, // INS_vmaxi_h
        15231, 7, // INS_vmaxi_w
        15239, 7, // INS_vmaxi_d
        15247, 7, // INS_vmini_b
        15255, 7, // INS_vmini_h
        15263, 7, // INS_vmini_w
        15271, 7, // INS_vmini_d
        15279, 9, // INS_vldrepl_d
        15289, 9, // INS_vldrepl_w
        15299, 9, // INS_vldrepl_h
        15309, 3, // INS_vld
        15313, 9, // INS_vldrepl_b
        15323, 3, // INS_vst
        15327, 11, // INS_vinsgr2vr_d
        15339, 10, // INS_vreplvei_d
        15350, 12, // INS_vpickve2gr_d
        15363, 13, // INS_vpickve2gr_du
        15377, 12, // INS_vpickve2gr_w
        15390, 13, // INS_vpickve2gr_wu
        15404, 11, // INS_vinsgr2vr_w
        15416, 10, // INS_vreplvei_w
        15427, 7, // INS_vslli_b
        15435, 7, // INS_vsrli_b
        15443, 7, // INS_vsrai_b
        15451, 8, // INS_vsrlri_b
        15460, 8, // INS_vsrari_b
        15469, 8, // INS_vrotri_b
        15478, 11, // INS_vsllwil_h_b
        15490, 13, // INS_vsllwil_hu_bu
        15504, 10, // INS_vbitclri_b
        15515, 10, // INS_vbitseti_b
        15526, 10, // INS_vbitrevi_b
        15537, 6, // INS_vsat_b
        15544, 7, // INS_vsat_bu
        15552, 10, // INS_vreplvei_h
        15563, 11, // INS_vinsgr2vr_h
        15575, 12, // INS_vpickve2gr_h
        15588, 13, // INS_vpickve2gr_hu
        15602, 12, // INS_vpickve2gr_b
        15615, 13, // INS_vpickve2gr_bu
        15629, 7, // INS_vslli_h
        15637, 7, // INS_vsrli_h
        15645, 7, // INS_vsrai_h
        15653, 8, // INS_vsrlri_h
        15662, 8, // INS_vsrari_h
        15671, 8, // INS_vrotri_h
        15680, 11, // INS_vsllwil_w_h
        15692, 13, // INS_vsllwil_wu_hu
        15706, 10, // INS_vsrlni_b_h
        15717, 11, // INS_vsrlrni_b_h
        15729, 11, // INS_vssrlni_b_h
        15741, 12, // INS_vssrlni_bu_h
        15754, 12, // INS_vssrlrni_b_h
        15767, 13, // INS_vssrlrni_bu_h
        15781, 10, // INS_vsrani_b_h
        15792, 11, // INS_vsrarni_b_h
        15804, 11, // INS_vssrani_b_h
        15816, 12, // INS_vssrani_bu_h
        15829, 12, // INS_vssrarni_b_h
        15842, 13, // INS_vssrarni_bu_h
        15856, 10, // INS_vbitclri_h
        15867, 10, // INS_vbitseti_h
        15878, 10, // INS_vbitrevi_h
        15889, 6, // INS_vsat_h
        15896, 7, // INS_vsat_hu
        15904, 11, // INS_vinsgr2vr_b
        15916, 10, // INS_vreplvei_b
        15927, 8, // INS_vslei_bu
        15936, 8, // INS_vslei_hu
        15945, 8, // INS_vslei_wu
        15954, 8, // INS_vslei_du
        15963, 8, // INS_vslti_bu
        15972, 8, // INS_vslti_hu
        15981, 8, // INS_vslti_wu
        15990, 8, // INS_vslti_du
        15999, 8, // INS_vaddi_bu
        16008, 8, // INS_vaddi_hu
        16017, 8, // INS_vaddi_wu
        16026, 8, // INS_vaddi_du
        16035, 8, // INS_vsubi_bu
        16044, 8, // INS_vsubi_hu
        16053, 8, // INS_vsubi_wu
        16062, 8, // INS_vsubi_du
        16071, 7, // INS_vslli_w
        16079, 7, // INS_vsrli_w
        16087, 7, // INS_vsrai_w
        16095, 7, // INS_vbsll_v
        16103, 7, // INS_vbsrl_v
        16111, 8, // INS_vsrlri_w
        16120, 8, // INS_vsrari_w
        16129, 8, // INS_vrotri_w
        16138, 11, // INS_vsllwil_d_w
        16150, 13, // INS_vsllwil_du_wu
        16164, 10, // INS_vsrlni_h_w
        16175, 11, // INS_vsrlrni_h_w
        16187, 11, // INS_vssrlni_h_w
        16199, 12, // INS_vssrlni_hu_w
        16212, 12, // INS_vssrlrni_h_w
        16225, 13, // INS_vssrlrni_hu_w
        16239, 10, // INS_vsrani_h_w
        16250, 11, // INS_vsrarni_h_w
        16262, 11, // INS_vssrani_h_w
        16274, 12, // INS_vssrani_hu_w
        16287, 12, // INS_vssrarni_h_w
        16300, 13, // INS_vssrarni_hu_w
        16314, 10, // INS_vbitclri_w
        16325, 10, // INS_vbitseti_w
        16336, 10, // INS_vbitrevi_w
        16347, 8, // INS_vmaxi_bu
        16356, 8, // INS_vmaxi_hu
        16365, 8, // INS_vmaxi_wu
        16374, 8, // INS_vmaxi_du
        16383, 8, // INS_vmini_bu
        16392, 8, // INS_vmini_hu
        16401, 8, // INS_vmini_wu
        16410, 8, // INS_vmini_du
        16419, 9, // INS_vfrstpi_b
        16429, 9, // INS_vfrstpi_h
        16439, 6, // INS_vsat_w
        16446, 7, // INS_vsat_wu
        16454, 7, // INS_vslli_d
        16462, 7, // INS_vsrli_d
        16470, 7, // INS_vsrai_d
        16478, 8, // INS_vrotri_d
        16487, 8, // INS_vsrlri_d
        16496, 8, // INS_vsrari_d
        16505, 10, // INS_vsrlni_w_d
        16516, 11, // INS_vsrlrni_w_d
        16528, 11, // INS_vssrlni_w_d
        16540, 12, // INS_vssrlni_wu_d
        16553, 12, // INS_vssrlrni_w_d
        16566, 13, // INS_vssrlrni_wu_d
        16580, 10, // INS_vsrani_w_d
        16591, 11, // INS_vsrarni_w_d
        16603, 11, // INS_vssrani_w_d
        16615, 12, // INS_vssrani_wu_d
        16628, 12, // INS_vssrarni_w_d
        16641, 13, // INS_vssrarni_wu_d
        16655, 10, // INS_vbitclri_d
        16666, 10, // INS_vbitseti_d
        16677, 10, // INS_vbitrevi_d
        16688, 6, // INS_vsat_d
        16695, 7, // INS_vsat_du
        16703, 10, // INS_vsrlni_d_q
        16714, 11, // INS_vsrlrni_d_q
        16726, 11, // INS_vssrlni_d_q
        16738, 12, // INS_vssrlni_du_q
        16751, 12, // INS_vssrlrni_d_q
        16764, 13, // INS_vssrlrni_du_q
        16778, 10, // INS_vsrani_d_q
        16789, 11, // INS_vsrarni_d_q
        16801, 11, // INS_vssrani_d_q
        16813, 12, // INS_vssrani_du_q
        16826, 12, // INS_vssrarni_d_q
        16839, 13, // INS_vssrarni_du_q
        16853, 10, // INS_vextrins_d
        16864, 10, // INS_vextrins_w
        16875, 10, // INS_vextrins_h
        16886, 10, // INS_vextrins_b
        16897, 9, // INS_vshuf4i_b
        16907, 9, // INS_vshuf4i_h
        16917, 9, // INS_vshuf4i_w
        16927, 9, // INS_vshuf4i_d
        16937, 10, // INS_vbitseli_b
        16948, 7, // INS_vandi_b
        16956, 6, // INS_vori_b
        16963, 7, // INS_vxori_b
        16971, 7, // INS_vnori_b
        16979, 8, // INS_vpermi_w
        16988, 9, // INS_xvfmadd_s
        16998, 9, // INS_xvfmadd_d
        17008, 9, // INS_xvfmsub_s
        17018, 9, // INS_xvfmsub_d
        17028, 10, // INS_xvfnmadd_s
        17039, 10, // INS_xvfnmadd_d
        17050, 10, // INS_xvfnmsub_s
        17061, 10, // INS_xvfnmsub_d
        17072, 10, // INS_xvbitsel_v
        17083, 8, // INS_xvshuf_b
        17092, 5, // INS_xvldx
        17098, 5, // INS_xvstx
        17104, 10, // INS_xvreplve_b
        17115, 10, // INS_xvreplve_h
        17126, 10, // INS_xvreplve_w
        17137, 10, // INS_xvreplve_d
        17148, 12, // INS_xvfcmp_caf_s
        17161, 12, // INS_xvfcmp_cun_s
        17174, 12, // INS_xvfcmp_ceq_s
        17187, 13, // INS_xvfcmp_cueq_s
        17201, 12, // INS_xvfcmp_clt_s
        17214, 13, // INS_xvfcmp_cult_s
        17228, 12, // INS_xvfcmp_cle_s
        17241, 13, // INS_xvfcmp_cule_s
        17255, 12, // INS_xvfcmp_cne_s
        17268, 12, // INS_xvfcmp_cor_s
        17281, 13, // INS_xvfcmp_cune_s
        17295, 12, // INS_xvfcmp_saf_s
        17308, 12, // INS_xvfcmp_sun_s
        17321, 12, // INS_xvfcmp_seq_s
        17334, 13, // INS_xvfcmp_sueq_s
        17348, 12, // INS_xvfcmp_slt_s
        17361, 13, // INS_xvfcmp_sult_s
        17375, 12, // INS_xvfcmp_sle_s
        17388, 13, // INS_xvfcmp_sule_s
        17402, 12, // INS_xvfcmp_sne_s
        17415, 12, // INS_xvfcmp_sor_s
        17428, 13, // INS_xvfcmp_sune_s
        17442, 12, // INS_xvfcmp_caf_d
        17455, 12, // INS_xvfcmp_cun_d
        17468, 12, // INS_xvfcmp_ceq_d
        17481, 13, // INS_xvfcmp_cueq_d
        17495, 12, // INS_xvfcmp_clt_d
        17508, 13, // INS_xvfcmp_cult_d
        17522, 12, // INS_xvfcmp_cle_d
        17535, 13, // INS_xvfcmp_cule_d
        17549, 12, // INS_xvfcmp_cne_d
        17562, 12, // INS_xvfcmp_cor_d
        17575, 13, // INS_xvfcmp_cune_d
        17589, 12, // INS_xvfcmp_saf_d
        17602, 12, // INS_xvfcmp_sun_d
        17615, 12, // INS_xvfcmp_seq_d
        17628, 13, // INS_xvfcmp_sueq_d
        17642, 12, // INS_xvfcmp_slt_d
        17655, 13, // INS_xvfcmp_sult_d
        17669, 12, // INS_xvfcmp_sle_d
        17682, 13, // INS_xvfcmp_sule_d
        17696, 12, // INS_xvfcmp_sne_d
        17709, 12, // INS_xvfcmp_sor_d
        17722, 13, // INS_xvfcmp_sune_d
        17736, 7, // INS_xvseq_b
        17744, 7, // INS_xvseq_h
        17752, 7, // INS_xvseq_w
        17760, 7, // INS_xvseq_d
        17768, 7, // INS_xvsle_b
        17776, 7, // INS_xvsle_h
        17784, 7, // INS_xvsle_w
        17792, 7, // INS_xvsle_d
        17800, 8, // INS_xvsle_bu
        17809, 8, // INS_xvsle_hu
        17818, 8, // INS_xvsle_wu
        17827, 8, // INS_xvsle_du
        17836, 7, // INS_xvslt_b
        17844, 7, // INS_xvslt_h
        17852, 7, // INS_xvslt_w
        17860, 7, // INS_xvslt_d
        17868, 8, // INS_xvslt_bu
        17877, 8, // INS_xvslt_hu
        17886, 8, // INS_xvslt_wu
        17895, 8, // INS_xvslt_du
        17904, 7, // INS_xvadd_b
        17912, 7, // INS_xvadd_h
        17920, 7, // INS_xvadd_w
        17928, 7, // INS_xvadd_d
        17936, 7, // INS_xvsub_b
        17944, 7, // INS_xvsub_h
        17952, 7, // INS_xvsub_w
        17960, 7, // INS_xvsub_d
        17968, 12, // INS_xvaddwev_h_b
        17981, 12, // INS_xvaddwev_w_h
        17994, 12, // INS_xvaddwev_d_w
        18007, 12, // INS_xvaddwev_q_d
        18020, 12, // INS_xvsubwev_h_b
        18033, 12, // INS_xvsubwev_w_h
        18046, 12, // INS_xvsubwev_d_w
        18059, 12, // INS_xvsubwev_q_d
        18072, 12, // INS_xvaddwod_h_b
        18085, 12, // INS_xvaddwod_w_h
        18098, 12, // INS_xvaddwod_d_w
        18111, 12, // INS_xvaddwod_q_d
        18124, 12, // INS_xvsubwod_h_b
        18137, 12, // INS_xvsubwod_w_h
        18150, 12, // INS_xvsubwod_d_w
        18163, 12, // INS_xvsubwod_q_d
        18176, 13, // INS_xvaddwev_h_bu
        18190, 13, // INS_xvaddwev_w_hu
        18204, 13, // INS_xvaddwev_d_wu
        18218, 13, // INS_xvaddwev_q_du
        18232, 13, // INS_xvsubwev_h_bu
        18246, 13, // INS_xvsubwev_w_hu
        18260, 13, // INS_xvsubwev_d_wu
        18274, 13, // INS_xvsubwev_q_du
        18288, 13, // INS_xvaddwod_h_bu
        18302, 13, // INS_xvaddwod_w_hu
        18316, 13, // INS_xvaddwod_d_wu
        18330, 13, // INS_xvaddwod_q_du
        18344, 13, // INS_xvsubwod_h_bu
        18358, 13, // INS_xvsubwod_w_hu
        18372, 13, // INS_xvsubwod_d_wu
        18386, 13, // INS_xvsubwod_q_du
        18400, 15, // INS_xvaddwev_h_bu_b
        18416, 15, // INS_xvaddwev_w_hu_h
        18432, 15, // INS_xvaddwev_d_wu_w
        18448, 15, // INS_xvaddwev_q_du_d
        18464, 15, // INS_xvaddwod_h_bu_b
        18480, 15, // INS_xvaddwod_w_hu_h
        18496, 15, // INS_xvaddwod_d_wu_w
        18512, 15, // INS_xvaddwod_q_du_d
        18528, 8, // INS_xvsadd_b
        18537, 8, // INS_xvsadd_h
        18546, 8, // INS_xvsadd_w
        18555, 8, // INS_xvsadd_d
        18564, 8, // INS_xvssub_b
        18573, 8, // INS_xvssub_h
        18582, 8, // INS_xvssub_w
        18591, 8, // INS_xvssub_d
        18600, 9, // INS_xvsadd_bu
        18610, 9, // INS_xvsadd_hu
        18620, 9, // INS_xvsadd_wu
        18630, 9, // INS_xvsadd_du
        18640, 9, // INS_xvssub_bu
        18650, 9, // INS_xvssub_hu
        18660, 9, // INS_xvssub_wu
        18670, 9, // INS_xvssub_du
        18680, 11, // INS_xvhaddw_h_b
        18692, 11, // INS_xvhaddw_w_h
        18704, 11, // INS_xvhaddw_d_w
        18716, 11, // INS_xvhaddw_q_d
        18728, 11, // INS_xvhsubw_h_b
        18740, 11, // INS_xvhsubw_w_h
        18752, 11, // INS_xvhsubw_d_w
        18764, 11, // INS_xvhsubw_q_d
        18776, 13, // INS_xvhaddw_hu_bu
        18790, 13, // INS_xvhaddw_wu_hu
        18804, 13, // INS_xvhaddw_du_wu
        18818, 13, // INS_xvhaddw_qu_du
        18832, 13, // INS_xvhsubw_hu_bu
        18846, 13, // INS_xvhsubw_wu_hu
        18860, 13, // INS_xvhsubw_du_wu
        18874, 13, // INS_xvhsubw_qu_du
        18888, 8, // INS_xvadda_b
        18897, 8, // INS_xvadda_h
        18906, 8, // INS_xvadda_w
        18915, 8, // INS_xvadda_d
        18924, 8, // INS_xvabsd_b
        18933, 8, // INS_xvabsd_h
        18942, 8, // INS_xvabsd_w
        18951, 8, // INS_xvabsd_d
        18960, 9, // INS_xvabsd_bu
        18970, 9, // INS_xvabsd_hu
        18980, 9, // INS_xvabsd_wu
        18990, 9, // INS_xvabsd_du
        19000, 7, // INS_xvavg_b
        19008, 7, // INS_xvavg_h
        19016, 7, // INS_xvavg_w
        19024, 7, // INS_xvavg_d
        19032, 8, // INS_xvavg_bu
        19041, 8, // INS_xvavg_hu
        19050, 8, // INS_xvavg_wu
        19059, 8, // INS_xvavg_du
        19068, 8, // INS_xvavgr_b
        19077, 8, // INS_xvavgr_h
        19086, 8, // INS_xvavgr_w
        19095, 8, // INS_xvavgr_d
        19104, 9, // INS_xvavgr_bu
        19114, 9, // INS_xvavgr_hu
        19124, 9, // INS_xvavgr_wu
        19134, 9, // INS_xvavgr_du
        19144, 7, // INS_xvmax_b
        19152, 7, // INS_xvmax_h
        19160, 7, // INS_xvmax_w
        19168, 7, // INS_xvmax_d
        19176, 7, // INS_xvmin_b
        19184, 7, // INS_xvmin_h
        19192, 7, // INS_xvmin_w
        19200, 7, // INS_xvmin_d
        19208, 8, // INS_xvmax_bu
        19217, 8, // INS_xvmax_hu
        19226, 8, // INS_xvmax_wu
        19235, 8, // INS_xvmax_du
        19244, 8, // INS_xvmin_bu
        19253, 8, // INS_xvmin_hu
        19262, 8, // INS_xvmin_wu
        19271, 8, // INS_xvmin_du
        19280, 7, // INS_xvmul_b
        19288, 7, // INS_xvmul_h
        19296, 7, // INS_xvmul_w
        19304, 7, // INS_xvmul_d
        19312, 7, // INS_xvmuh_b
        19320, 7, // INS_xvmuh_h
        19328, 7, // INS_xvmuh_w
        19336, 7, // INS_xvmuh_d
        19344, 8, // INS_xvmuh_bu
        19353, 8, // INS_xvmuh_hu
        19362, 8, // INS_xvmuh_wu
        19371, 8, // INS_xvmuh_du
        19380, 12, // INS_xvmulwev_h_b
        19393, 12, // INS_xvmulwev_w_h
        19406, 12, // INS_xvmulwev_d_w
        19419, 12, // INS_xvmulwev_q_d
        19432, 12, // INS_xvmulwod_h_b
        19445, 12, // INS_xvmulwod_w_h
        19458, 12, // INS_xvmulwod_d_w
        19471, 12, // INS_xvmulwod_q_d
        19484, 13, // INS_xvmulwev_h_bu
        19498, 13, // INS_xvmulwev_w_hu
        19512, 13, // INS_xvmulwev_d_wu
        19526, 13, // INS_xvmulwev_q_du
        19540, 13, // INS_xvmulwod_h_bu
        19554, 13, // INS_xvmulwod_w_hu
        19568, 13, // INS_xvmulwod_d_wu
        19582, 13, // INS_xvmulwod_q_du
        19596, 15, // INS_xvmulwev_h_bu_b
        19612, 15, // INS_xvmulwev_w_hu_h
        19628, 15, // INS_xvmulwev_d_wu_w
        19644, 15, // INS_xvmulwev_q_du_d
        19660, 15, // INS_xvmulwod_h_bu_b
        19676, 15, // INS_xvmulwod_w_hu_h
        19692, 15, // INS_xvmulwod_d_wu_w
        19708, 15, // INS_xvmulwod_q_du_d
        19724, 8, // INS_xvmadd_b
        19733, 8, // INS_xvmadd_h
        19742, 8, // INS_xvmadd_w
        19751, 8, // INS_xvmadd_d
        19760, 8, // INS_xvmsub_b
        19769, 8, // INS_xvmsub_h
        19778, 8, // INS_xvmsub_w
        19787, 8, // INS_xvmsub_d
        19796, 13, // INS_xvmaddwev_h_b
        19810, 13, // INS_xvmaddwev_w_h
        19824, 13, // INS_xvmaddwev_d_w
        19838, 13, // INS_xvmaddwev_q_d
        19852, 13, // INS_xvmaddwod_h_b
        19866, 13, // INS_xvmaddwod_w_h
        19880, 13, // INS_xvmaddwod_d_w
        19894, 13, // INS_xvmaddwod_q_d
        19908, 14, // INS_xvmaddwev_h_bu
        19923, 14, // INS_xvmaddwev_w_hu
        19938, 14, // INS_xvmaddwev_d_wu
        19953, 14, // INS_xvmaddwev_q_du
        19968, 14, // INS_xvmaddwod_h_bu
        19983, 14, // INS_xvmaddwod_w_hu
        19998, 14, // INS_xvmaddwod_d_wu
        20013, 14, // INS_xvmaddwod_q_du
        20028, 16, // INS_xvmaddwev_h_bu_b
        20045, 16, // INS_xvmaddwev_w_hu_h
        20062, 16, // INS_xvmaddwev_d_wu_w
        20079, 16, // INS_xvmaddwev_q_du_d
        20096, 16, // INS_xvmaddwod_h_bu_b
        20113, 16, // INS_xvmaddwod_w_hu_h
        20130, 16, // INS_xvmaddwod_d_wu_w
        20147, 16, // INS_xvmaddwod_q_du_d
        20164, 7, // INS_xvdiv_b
        20172, 7, // INS_xvdiv_h
        20180, 7, // INS_xvdiv_w
        20188, 7, // INS_xvdiv_d
        20196, 7, // INS_xvmod_b
        20204, 7, // INS_xvmod_h
        20212, 7, // INS_xvmod_w
        20220, 7, // INS_xvmod_d
        20228, 8, // INS_xvdiv_bu
        20237, 8, // INS_xvdiv_hu
        20246, 8, // INS_xvdiv_wu
        20255, 8, // INS_xvdiv_du
        20264, 8, // INS_xvmod_bu
        20273, 8, // INS_xvmod_hu
        20282, 8, // INS_xvmod_wu
        20291, 8, // INS_xvmod_du
        20300, 7, // INS_xvsll_b
        20308, 7, // INS_xvsll_h
        20316, 7, // INS_xvsll_w
        20324, 7, // INS_xvsll_d
        20332, 7, // INS_xvsrl_b
        20340, 7, // INS_xvsrl_h
        20348, 7, // INS_xvsrl_w
        20356, 7, // INS_xvsrl_d
        20364, 7, // INS_xvsra_b
        20372, 7, // INS_xvsra_h
        20380, 7, // INS_xvsra_w
        20388, 7, // INS_xvsra_d
        20396, 8, // INS_xvrotr_b
        20405, 8, // INS_xvrotr_h
        20414, 8, // INS_xvrotr_w
        20423, 8, // INS_xvrotr_d
        20432, 8, // INS_xvsrlr_b
        20441, 8, // INS_xvsrlr_h
        20450, 8, // INS_xvsrlr_w
        20459, 8, // INS_xvsrlr_d
        20468, 8, // INS_xvsrar_b
        20477, 8, // INS_xvsrar_h
        20486, 8, // INS_xvsrar_w
        20495, 8, // INS_xvsrar_d
        20504, 10, // INS_xvsrln_b_h
        20515, 10, // INS_xvsrln_h_w
        20526, 10, // INS_xvsrln_w_d
        20537, 10, // INS_xvsran_b_h
        20548, 10, // INS_xvsran_h_w
        20559, 10, // INS_xvsran_w_d
        20570, 11, // INS_xvsrlrn_b_h
        20582, 11, // INS_xvsrlrn_h_w
        20594, 11, // INS_xvsrlrn_w_d
        20606, 11, // INS_xvsrarn_b_h
        20618, 11, // INS_xvsrarn_h_w
        20630, 11, // INS_xvsrarn_w_d
        20642, 11, // INS_xvssrln_b_h
        20654, 11, // INS_xvssrln_h_w
        20666, 11, // INS_xvssrln_w_d
        20678, 11, // INS_xvssran_b_h
        20690, 11, // INS_xvssran_h_w
        20702, 11, // INS_xvssran_w_d
        20714, 12, // INS_xvssrlrn_b_h
        20727, 12, // INS_xvssrlrn_h_w
        20740, 12, // INS_xvssrlrn_w_d
        20753, 12, // INS_xvssrarn_b_h
        20766, 12, // INS_xvssrarn_h_w
        20779, 12, // INS_xvssrarn_w_d
        20792, 12, // INS_xvssrln_bu_h
        20805, 12, // INS_xvssrln_hu_w
        20818, 12, // INS_xvssrln_wu_d
        20831, 12, // INS_xvssran_bu_h
        20844, 12, // INS_xvssran_hu_w
        20857, 12, // INS_xvssran_wu_d
        20870, 13, // INS_xvssrlrn_bu_h
        20884, 13, // INS_xvssrlrn_hu_w
        20898, 13, // INS_xvssrlrn_wu_d
        20912, 13, // INS_xvssrarn_bu_h
        20926, 13, // INS_xvssrarn_hu_w
        20940, 13, // INS_xvssrarn_wu_d
        20954, 10, // INS_xvbitclr_b
        20965, 10, // INS_xvbitclr_h
        20976, 10, // INS_xvbitclr_w
        20987, 10, // INS_xvbitclr_d
        20998, 10, // INS_xvbitset_b
        21009, 10, // INS_xvbitset_h
        21020, 10, // INS_xvbitset_w
        21031, 10, // INS_xvbitset_d
        21042, 10, // INS_xvbitrev_b
        21053, 10, // INS_xvbitrev_h
        21064, 10, // INS_xvbitrev_w
        21075, 10, // INS_xvbitrev_d
        21086, 10, // INS_xvpackev_b
        21097, 10, // INS_xvpackev_h
        21108, 10, // INS_xvpackev_w
        21119, 10, // INS_xvpackev_d
        21130, 10, // INS_xvpackod_b
        21141, 10, // INS_xvpackod_h
        21152, 10, // INS_xvpackod_w
        21163, 10, // INS_xvpackod_d
        21174, 8, // INS_xvilvl_b
        21183, 8, // INS_xvilvl_h
        21192, 8, // INS_xvilvl_w
        21201, 8, // INS_xvilvl_d
        21210, 8, // INS_xvilvh_b
        21219, 8, // INS_xvilvh_h
        21228, 8, // INS_xvilvh_w
        21237, 8, // INS_xvilvh_d
        21246, 10, // INS_xvpickev_b
        21257, 10, // INS_xvpickev_h
        21268, 10, // INS_xvpickev_w
        21279, 10, // INS_xvpickev_d
        21290, 10, // INS_xvpickod_b
        21301, 10, // INS_xvpickod_h
        21312, 10, // INS_xvpickod_w
        21323, 10, // INS_xvpickod_d
        21334, 7, // INS_xvand_v
        21342, 6, // INS_xvor_v
        21349, 7, // INS_xvxor_v
        21357, 7, // INS_xvnor_v
        21365, 8, // INS_xvandn_v
        21374, 7, // INS_xvorn_v
        21382, 9, // INS_xvfrstp_b
        21392, 9, // INS_xvfrstp_h
        21402, 7, // INS_xvadd_q
        21410, 7, // INS_xvsub_q
        21418, 11, // INS_xvsigncov_b
        21430, 11, // INS_xvsigncov_h
        21442, 11, // INS_xvsigncov_w
        21454, 11, // INS_xvsigncov_d
        21466, 8, // INS_xvfadd_s
        21475, 8, // INS_xvfadd_d
        21484, 8, // INS_xvfsub_s
        21493, 8, // INS_xvfsub_d
        21502, 8, // INS_xvfmul_s
        21511, 8, // INS_xvfmul_d
        21520, 8, // INS_xvfdiv_s
        21529, 8, // INS_xvfdiv_d
        21538, 8, // INS_xvfmax_s
        21547, 8, // INS_xvfmax_d
        21556, 8, // INS_xvfmin_s
        21565, 8, // INS_xvfmin_d
        21574, 9, // INS_xvfmaxa_s
        21584, 9, // INS_xvfmaxa_d
        21594, 9, // INS_xvfmina_s
        21604, 9, // INS_xvfmina_d
        21614, 10, // INS_xvfcvt_h_s
        21625, 10, // INS_xvfcvt_s_d
        21636, 11, // INS_xvffint_s_l
        21648, 11, // INS_xvftint_w_d
        21660, 13, // INS_xvftintrm_w_d
        21674, 13, // INS_xvftintrp_w_d
        21688, 13, // INS_xvftintrz_w_d
        21702, 14, // INS_xvftintrne_w_d
        21717, 8, // INS_xvshuf_h
        21726, 8, // INS_xvshuf_w
        21735, 8, // INS_xvshuf_d
        21744, 8, // INS_xvperm_w
        21753, 13, // INS_xvreplgr2vr_b
        21767, 13, // INS_xvreplgr2vr_h
        21781, 13, // INS_xvreplgr2vr_w
        21795, 13, // INS_xvreplgr2vr_d
        21809, 7, // INS_xvclo_b
        21817, 7, // INS_xvclo_h
        21825, 7, // INS_xvclo_w
        21833, 7, // INS_xvclo_d
        21841, 7, // INS_xvclz_b
        21849, 7, // INS_xvclz_h
        21857, 7, // INS_xvclz_w
        21865, 7, // INS_xvclz_d
        21873, 8, // INS_xvpcnt_b
        21882, 8, // INS_xvpcnt_h
        21891, 8, // INS_xvpcnt_w
        21900, 8, // INS_xvpcnt_d
        21909, 7, // INS_xvneg_b
        21917, 7, // INS_xvneg_h
        21925, 7, // INS_xvneg_w
        21933, 7, // INS_xvneg_d
        21941, 10, // INS_xvmskltz_b
        21952, 10, // INS_xvmskltz_h
        21963, 10, // INS_xvmskltz_w
        21974, 10, // INS_xvmskltz_d
        21985, 10, // INS_xvmskgez_b
        21996, 9, // INS_xvmsknz_b
        22006, 9, // INS_xvflogb_s
        22016, 9, // INS_xvflogb_d
        22026, 10, // INS_xvfclass_s
        22037, 10, // INS_xvfclass_d
        22048, 9, // INS_xvfsqrt_s
        22058, 9, // INS_xvfsqrt_d
        22068, 10, // INS_xvfrecip_s
        22079, 10, // INS_xvfrecip_d
        22090, 10, // INS_xvfrsqrt_s
        22101, 10, // INS_xvfrsqrt_d
        22112, 11, // INS_xvfrecipe_s
        22124, 11, // INS_xvfrecipe_d
        22136, 11, // INS_xvfrsqrte_s
        22148, 11, // INS_xvfrsqrte_d
        22160, 9, // INS_xvfrint_s
        22170, 9, // INS_xvfrint_d
        22180, 11, // INS_xvfrintrm_s
        22192, 11, // INS_xvfrintrm_d
        22204, 11, // INS_xvfrintrp_s
        22216, 11, // INS_xvfrintrp_d
        22228, 11, // INS_xvfrintrz_s
        22240, 11, // INS_xvfrintrz_d
        22252, 12, // INS_xvfrintrne_s
        22265, 12, // INS_xvfrintrne_d
        22278, 11, // INS_xvfcvtl_s_h
        22290, 11, // INS_xvfcvth_s_h
        22302, 11, // INS_xvfcvtl_d_s
        22314, 11, // INS_xvfcvth_d_s
        22326, 11, // INS_xvffint_s_w
        22338, 12, // INS_xvffint_s_wu
        22351, 11, // INS_xvffint_d_l
        22363, 12, // INS_xvffint_d_lu
        22376, 12, // INS_xvffintl_d_w
        22389, 12, // INS_xvffinth_d_w
        22402, 11, // INS_xvftint_w_s
        22414, 11, // INS_xvftint_l_d
        22426, 13, // INS_xvftintrm_w_s
        22440, 13, // INS_xvftintrm_l_d
        22454, 13, // INS_xvftintrp_w_s
        22468, 13, // INS_xvftintrp_l_d
        22482, 13, // INS_xvftintrz_w_s
        22496, 13, // INS_xvftintrz_l_d
        22510, 14, // INS_xvftintrne_w_s
        22525, 14, // INS_xvftintrne_l_d
        22540, 12, // INS_xvftint_wu_s
        22553, 12, // INS_xvftint_lu_d
        22566, 14, // INS_xvftintrz_wu_s
        22581, 14, // INS_xvftintrz_lu_d
        22596, 12, // INS_xvftintl_l_s
        22609, 12, // INS_xvftinth_l_s
        22622, 14, // INS_xvftintrml_l_s
        22637, 14, // INS_xvftintrmh_l_s
        22652, 14, // INS_xvftintrpl_l_s
        22667, 14, // INS_xvftintrph_l_s
        22682, 14, // INS_xvftintrzl_l_s
        22697, 14, // INS_xvftintrzh_l_s
        22712, 15, // INS_xvftintrnel_l_s
        22728, 15, // INS_xvftintrneh_l_s
        22744, 10, // INS_xvexth_h_b
        22755, 10, // INS_xvexth_w_h
        22766, 10, // INS_xvexth_d_w
        22777, 10, // INS_xvexth_q_d
        22788, 12, // INS_xvexth_hu_bu
        22801, 12, // INS_xvexth_wu_hu
        22814, 12, // INS_xvexth_du_wu
        22827, 12, // INS_xvexth_qu_du
        22840, 11, // INS_vext2xv_h_b
        22852, 11, // INS_vext2xv_w_b
        22864, 11, // INS_vext2xv_d_b
        22876, 11, // INS_vext2xv_w_h
        22888, 11, // INS_vext2xv_d_h
        22900, 11, // INS_vext2xv_d_w
        22912, 13, // INS_vext2xv_hu_bu
        22926, 13, // INS_vext2xv_wu_bu
        22940, 13, // INS_vext2xv_du_bu
        22954, 13, // INS_vext2xv_wu_hu
        22968, 13, // INS_vext2xv_du_hu
        22982, 13, // INS_vext2xv_du_wu
        22996, 11, // INS_xvreplve0_b
        23008, 11, // INS_xvreplve0_h
        23020, 11, // INS_xvreplve0_w
        23032, 11, // INS_xvreplve0_d
        23044, 11, // INS_xvreplve0_q
        23056, 10, // INS_xvextl_q_d
        23067, 12, // INS_xvextl_qu_du
        23080, 9, // INS_xvstelm_b
        23090, 9, // INS_xvstelm_h
        23100, 9, // INS_xvstelm_w
        23110, 9, // INS_xvstelm_d
        23120, 8, // INS_xvseqi_b
        23129, 8, // INS_xvseqi_h
        23138, 8, // INS_xvseqi_w
        23147, 8, // INS_xvseqi_d
        23156, 8, // INS_xvslei_b
        23165, 8, // INS_xvslei_h
        23174, 8, // INS_xvslei_w
        23183, 8, // INS_xvslei_d
        23192, 8, // INS_xvslti_b
        23201, 8, // INS_xvslti_h
        23210, 8, // INS_xvslti_w
        23219, 8, // INS_xvslti_d
        23228, 8, // INS_xvmaxi_b
        23237, 8, // INS_xvmaxi_h
        23246, 8, // INS_xvmaxi_w
        23255, 8, // INS_xvmaxi_d
        23264, 8, // INS_xvmini_b
        23273, 8, // INS_xvmini_h
        23282, 8, // INS_xvmini_w
        23291, 8, // INS_xvmini_d
        23300, 10, // INS_xvldrepl_d
        23311, 10, // INS_xvldrepl_w
        23322, 10, // INS_xvldrepl_h
        23333, 4, // INS_xvld
        23338, 10, // INS_xvldrepl_b
        23349, 4, // INS_xvst
        23354, 14, // INS_xvrepl128vei_d
        23369, 10, // INS_xvinsve0_d
        23380, 14, // INS_xvrepl128vei_w
        23395, 10, // INS_xvpickve_d
        23406, 12, // INS_xvinsgr2vr_d
        23419, 13, // INS_xvpickve2gr_d
        23433, 14, // INS_xvpickve2gr_du
        23448, 13, // INS_xvpickve2gr_w
        23462, 14, // INS_xvpickve2gr_wu
        23477, 8, // INS_xvslli_b
        23486, 8, // INS_xvsrli_b
        23495, 8, // INS_xvsrai_b
        23504, 9, // INS_xvrotri_b
        23514, 9, // INS_xvsrlri_b
        23524, 9, // INS_xvsrari_b
        23534, 12, // INS_xvsllwil_h_b
        23547, 14, // INS_xvsllwil_hu_bu
        23562, 11, // INS_xvsrlni_b_h
        23574, 12, // INS_xvsrlrni_b_h
        23587, 12, // INS_xvssrlni_b_h
        23600, 13, // INS_xvssrlni_bu_h
        23614, 13, // INS_xvssrlrni_b_h
        23628, 14, // INS_xvssrlrni_bu_h
        23643, 11, // INS_xvsrani_b_h
        23655, 12, // INS_xvsrarni_b_h
        23668, 12, // INS_xvssrani_b_h
        23681, 13, // INS_xvssrani_bu_h
        23695, 13, // INS_xvssrarni_b_h
        23709, 14, // INS_xvssrarni_bu_h
        23724, 10, // INS_xvinsve0_w
        23735, 14, // INS_xvrepl128vei_h
        23750, 10, // INS_xvpickve_w
        23761, 11, // INS_xvbitclri_b
        23773, 11, // INS_xvbitseti_b
        23785, 11, // INS_xvbitrevi_b
        23797, 12, // INS_xvinsgr2vr_w
        23810, 7, // INS_xvsat_b
        23818, 8, // INS_xvsat_bu
        23827, 8, // INS_xvslli_h
        23836, 8, // INS_xvsrli_h
        23845, 8, // INS_xvsrai_h
        23854, 9, // INS_xvrotri_h
        23864, 14, // INS_xvrepl128vei_b
        23879, 9, // INS_xvsrlri_h
        23889, 9, // INS_xvsrari_h
        23899, 12, // INS_xvsllwil_w_h
        23912, 14, // INS_xvsllwil_wu_hu
        23927, 11, // INS_xvbitclri_h
        23939, 11, // INS_xvbitseti_h
        23951, 11, // INS_xvbitrevi_h
        23963, 7, // INS_xvsat_h
        23971, 8, // INS_xvsat_hu
        23980, 9, // INS_xvslei_bu
        23990, 9, // INS_xvslei_hu
        24000, 9, // INS_xvslei_wu
        24010, 9, // INS_xvslei_du
        24020, 9, // INS_xvslti_bu
        24030, 9, // INS_xvslti_hu
        24040, 9, // INS_xvslti_wu
        24050, 9, // INS_xvslti_du
        24060, 9, // INS_xvaddi_bu
        24070, 9, // INS_xvaddi_hu
        24080, 9, // INS_xvaddi_wu
        24090, 9, // INS_xvaddi_du
        24100, 9, // INS_xvsubi_bu
        24110, 9, // INS_xvsubi_hu
        24120, 9, // INS_xvsubi_wu
        24130, 9, // INS_xvsubi_du
        24140, 9, // INS_xvmaxi_bu
        24150, 9, // INS_xvmaxi_hu
        24160, 9, // INS_xvmaxi_wu
        24170, 9, // INS_xvmaxi_du
        24180, 9, // INS_xvmini_bu
        24190, 9, // INS_xvmini_hu
        24200, 9, // INS_xvmini_wu
        24210, 9, // INS_xvmini_du
        24220, 10, // INS_xvfrstpi_b
        24231, 10, // INS_xvfrstpi_h
        24242, 8, // INS_xvslli_w
        24251, 8, // INS_xvsrli_w
        24260, 8, // INS_xvsrai_w
        24269, 8, // INS_xvbsll_v
        24278, 8, // INS_xvbsrl_v
        24287, 9, // INS_xvsrlri_w
        24297, 9, // INS_xvsrari_w
        24307, 9, // INS_xvrotri_w
        24317, 12, // INS_xvsllwil_d_w
        24330, 14, // INS_xvsllwil_du_wu
        24345, 11, // INS_xvsrlni_h_w
        24357, 12, // INS_xvsrlrni_h_w
        24370, 12, // INS_xvssrlni_h_w
        24383, 13, // INS_xvssrlni_hu_w
        24397, 13, // INS_xvssrlrni_h_w
        24411, 14, // INS_xvssrlrni_hu_w
        24426, 11, // INS_xvsrani_h_w
        24438, 12, // INS_xvsrarni_h_w
        24451, 12, // INS_xvssrani_h_w
        24464, 13, // INS_xvssrani_hu_w
        24478, 13, // INS_xvssrarni_h_w
        24492, 14, // INS_xvssrarni_hu_w
        24507, 11, // INS_xvbitclri_w
        24519, 11, // INS_xvbitseti_w
        24531, 11, // INS_xvbitrevi_w
        24543, 7, // INS_xvsat_w
        24551, 8, // INS_xvsat_wu
        24560, 8, // INS_xvslli_d
        24569, 8, // INS_xvsrli_d
        24578, 8, // INS_xvsrai_d
        24587, 9, // INS_xvrotri_d
        24597, 9, // INS_xvsrlri_d
        24607, 9, // INS_xvsrari_d
        24617, 11, // INS_xvsrlni_w_d
        24629, 12, // INS_xvsrlrni_w_d
        24642, 12, // INS_xvssrlni_w_d
        24655, 13, // INS_xvssrlni_wu_d
        24669, 13, // INS_xvssrlrni_w_d
        24683, 14, // INS_xvssrlrni_wu_d
        24698, 11, // INS_xvsrani_w_d
        24710, 12, // INS_xvsrarni_w_d
        24723, 12, // INS_xvssrani_w_d
        24736, 13, // INS_xvssrani_wu_d
        24750, 13, // INS_xvssrarni_w_d
        24764, 14, // INS_xvssrarni_wu_d
        24779, 11, // INS_xvbitclri_d
        24791, 11, // INS_xvbitseti_d
        24803, 11, // INS_xvbitrevi_d
        24815, 7, // INS_xvsat_d
        24823, 8, // INS_xvsat_du
        24832, 11, // INS_xvsrlni_d_q
        24844, 12, // INS_xvsrlrni_d_q
        24857, 12, // INS_xvssrlni_d_q
        24870, 13, // INS_xvssrlni_du_q
        24884, 13, // INS_xvssrlrni_d_q
        24898, 14, // INS_xvssrlrni_du_q
        24913, 11, // INS_xvsrani_d_q
        24925, 12, // INS_xvsrarni_d_q
        24938, 12, // INS_xvssrani_d_q
        24951, 13, // INS_xvssrani_du_q
        24965, 13, // INS_xvssrarni_d_q
        24979, 14, // INS_xvssrarni_du_q
        24994, 11, // INS_xvextrins_d
        25006, 11, // INS_xvextrins_w
        25018, 11, // INS_xvextrins_h
        25030, 11, // INS_xvextrins_b
        25042, 10, // INS_xvshuf4i_b
        25053, 10, // INS_xvshuf4i_h
        25064, 10, // INS_xvshuf4i_w
        25075, 10, // INS_xvshuf4i_d
        25086, 11, // INS_xvbitseli_b
        25098, 8, // INS_xvandi_b
        25107, 7, // INS_xvori_b
        25115, 8, // INS_xvxori_b
        25124, 8, // INS_xvnori_b
        25133, 9, // INS_xvpermi_w
        25143, 9, // INS_xvpermi_d
        25153, 9, // INS_xvpermi_q
        25163, 5, // INS_xvldi
        25169, 10, // INS_xvseteqz_v
        25180, 10, // INS_xvsetnez_v
        25191, 13, // INS_xvsetanyeqz_b
        25205, 13, // INS_xvsetanyeqz_h
        25219, 13, // INS_xvsetanyeqz_w
        25233, 13, // INS_xvsetanyeqz_d
        25247, 13, // INS_xvsetallnez_b
        25261, 13, // INS_xvsetallnez_h
        25275, 13, // INS_xvsetallnez_w
        25289, 13, // INS_xvsetallnez_d
    ];

    private static ReadOnlySpan<int> s_instructionNameRangesLoongArch64NoSimd => [
        0, 7, // INS_invalid
        349, 3, // INS_nop
        6531, 5, // INS_bceqz
        6537, 5, // INS_bcnez
        162, 3, // INS_beq
        166, 3, // INS_bne
        206, 3, // INS_blt
        202, 3, // INS_bge
        6543, 4, // INS_bltu
        6548, 4, // INS_bgeu
        6553, 4, // INS_beqz
        6558, 4, // INS_bnez
        160, 1, // INS_b
        274, 2, // INS_bl
        56, 3, // INS_mov
        6563, 4, // INS_dneg
        822, 3, // INS_neg
        2869, 3, // INS_not
        6568, 5, // INS_add_w
        6574, 5, // INS_add_d
        6580, 5, // INS_sub_w
        6586, 5, // INS_sub_d
        103, 3, // INS_and
        6592, 2, // INS_or
        4850, 3, // INS_nor
        6595, 3, // INS_xor
        6599, 4, // INS_andn
        115, 3, // INS_orn
        6604, 5, // INS_mul_w
        6610, 5, // INS_mul_d
        6616, 6, // INS_mulh_w
        6623, 7, // INS_mulh_wu
        6631, 6, // INS_mulh_d
        6638, 7, // INS_mulh_du
        6646, 8, // INS_mulw_d_w
        6655, 9, // INS_mulw_d_wu
        6665, 5, // INS_div_w
        6671, 5, // INS_mod_w
        6677, 6, // INS_div_wu
        6684, 6, // INS_mod_wu
        6691, 5, // INS_div_d
        6697, 5, // INS_mod_d
        6703, 6, // INS_div_du
        6710, 6, // INS_mod_du
        6717, 5, // INS_sll_w
        6723, 5, // INS_srl_w
        6729, 5, // INS_sra_w
        6735, 6, // INS_rotr_w
        6742, 5, // INS_sll_d
        6748, 5, // INS_srl_d
        6754, 5, // INS_sra_d
        6760, 6, // INS_rotr_d
        6767, 7, // INS_maskeqz
        6775, 7, // INS_masknez
        6783, 3, // INS_slt
        6787, 4, // INS_sltu
        6792, 9, // INS_crc_w_b_w
        6802, 9, // INS_crc_w_h_w
        6812, 9, // INS_crc_w_w_w
        6822, 9, // INS_crc_w_d_w
        6832, 10, // INS_crcc_w_b_w
        6843, 10, // INS_crcc_w_h_w
        6854, 10, // INS_crcc_w_w_w
        6865, 10, // INS_crcc_w_d_w
        6876, 7, // INS_amcas_b
        6884, 7, // INS_amcas_h
        6892, 7, // INS_amcas_w
        6900, 7, // INS_amcas_d
        6908, 8, // INS_amswap_b
        6917, 8, // INS_amswap_h
        6926, 8, // INS_amswap_w
        6935, 8, // INS_amswap_d
        6944, 7, // INS_amadd_b
        6952, 7, // INS_amadd_h
        6960, 7, // INS_amadd_w
        6968, 7, // INS_amadd_d
        6976, 7, // INS_amand_w
        6984, 7, // INS_amand_d
        6992, 6, // INS_amor_w
        6999, 6, // INS_amor_d
        7006, 7, // INS_amxor_w
        7014, 7, // INS_amxor_d
        7022, 7, // INS_ammax_w
        7030, 7, // INS_ammax_d
        7038, 7, // INS_ammin_w
        7046, 7, // INS_ammin_d
        7054, 8, // INS_ammax_wu
        7063, 8, // INS_ammax_du
        7072, 8, // INS_ammin_wu
        7081, 8, // INS_ammin_du
        7090, 10, // INS_amcas_db_b
        7101, 10, // INS_amcas_db_h
        7112, 10, // INS_amcas_db_w
        7123, 10, // INS_amcas_db_d
        7134, 11, // INS_amswap_db_b
        7146, 11, // INS_amswap_db_h
        7158, 11, // INS_amswap_db_w
        7170, 11, // INS_amswap_db_d
        7182, 10, // INS_amadd_db_b
        7193, 10, // INS_amadd_db_h
        7204, 10, // INS_amadd_db_w
        7215, 10, // INS_amadd_db_d
        7226, 10, // INS_amand_db_w
        7237, 10, // INS_amand_db_d
        7248, 9, // INS_amor_db_w
        7258, 9, // INS_amor_db_d
        7268, 10, // INS_amxor_db_w
        7279, 10, // INS_amxor_db_d
        7290, 10, // INS_ammax_db_w
        7301, 10, // INS_ammax_db_d
        7312, 10, // INS_ammin_db_w
        7323, 10, // INS_ammin_db_d
        7334, 11, // INS_ammax_db_wu
        7346, 11, // INS_ammax_db_du
        7358, 11, // INS_ammin_db_wu
        7370, 11, // INS_ammin_db_du
        7382, 6, // INS_alsl_w
        7389, 7, // INS_alsl_wu
        7397, 6, // INS_alsl_d
        7404, 10, // INS_bytepick_w
        7415, 10, // INS_bytepick_d
        7426, 4, // INS_fsel
        7431, 7, // INS_lu12i_w
        7439, 7, // INS_lu32i_d
        7447, 6, // INS_pcaddi
        7454, 9, // INS_pcaddu12i
        7464, 9, // INS_pcalau12i
        7474, 9, // INS_pcaddu18i
        7484, 7, // INS_ext_w_b
        7492, 7, // INS_ext_w_h
        7500, 5, // INS_clo_w
        7506, 5, // INS_clz_w
        7512, 5, // INS_cto_w
        7518, 5, // INS_ctz_w
        7524, 5, // INS_clo_d
        7530, 5, // INS_clz_d
        7536, 5, // INS_cto_d
        7542, 5, // INS_ctz_d
        7548, 7, // INS_revb_2h
        7556, 7, // INS_revb_4h
        7564, 7, // INS_revb_2w
        7572, 6, // INS_revb_d
        7579, 7, // INS_revh_2w
        7587, 6, // INS_revh_d
        7594, 9, // INS_bitrev_4b
        7604, 9, // INS_bitrev_8b
        7614, 8, // INS_bitrev_w
        7623, 8, // INS_bitrev_d
        7632, 9, // INS_rdtimel_w
        7642, 9, // INS_rdtimeh_w
        7652, 8, // INS_rdtime_d
        7661, 6, // INS_cpucfg
        7668, 10, // INS_movfr2gr_s
        7679, 10, // INS_movfr2gr_d
        7690, 11, // INS_movfrh2gr_s
        7702, 9, // INS_bstrins_w
        7712, 9, // INS_bstrins_d
        7722, 10, // INS_bstrpick_w
        7733, 10, // INS_bstrpick_d
        7744, 4, // INS_ld_b
        7749, 4, // INS_ld_h
        7754, 4, // INS_ld_w
        7759, 4, // INS_ld_d
        7764, 5, // INS_ld_bu
        7770, 5, // INS_ld_hu
        7776, 5, // INS_ld_wu
        7782, 7, // INS_ldptr_w
        7790, 7, // INS_ldptr_d
        7798, 4, // INS_ll_w
        7803, 4, // INS_ll_d
        7808, 5, // INS_ldx_b
        7814, 5, // INS_ldx_h
        7820, 5, // INS_ldx_w
        7826, 5, // INS_ldx_d
        7832, 6, // INS_ldx_bu
        7839, 6, // INS_ldx_hu
        7846, 6, // INS_ldx_wu
        7853, 6, // INS_ldgt_b
        7860, 6, // INS_ldgt_h
        7867, 6, // INS_ldgt_w
        7874, 6, // INS_ldgt_d
        7881, 6, // INS_ldle_b
        7888, 6, // INS_ldle_h
        7895, 6, // INS_ldle_w
        7902, 6, // INS_ldle_d
        7909, 6, // INS_addi_w
        7916, 6, // INS_addi_d
        7923, 7, // INS_lu52i_d
        7931, 4, // INS_slti
        7936, 5, // INS_sltui
        7942, 4, // INS_andi
        7947, 3, // INS_ori
        7951, 4, // INS_xori
        7956, 6, // INS_slli_w
        7963, 6, // INS_srli_w
        7970, 6, // INS_srai_w
        7977, 7, // INS_rotri_w
        7985, 6, // INS_slli_d
        7992, 6, // INS_srli_d
        7999, 6, // INS_srai_d
        8006, 7, // INS_rotri_d
        8014, 9, // INS_addu16i_d
        8024, 4, // INS_jirl
        8029, 4, // INS_st_b
        8034, 4, // INS_st_h
        8039, 4, // INS_st_w
        8044, 4, // INS_st_d
        8049, 7, // INS_stptr_w
        8057, 7, // INS_stptr_d
        8065, 4, // INS_sc_w
        8070, 4, // INS_sc_d
        8075, 5, // INS_stx_b
        8081, 5, // INS_stx_h
        8087, 5, // INS_stx_w
        8093, 5, // INS_stx_d
        8099, 6, // INS_stgt_b
        8106, 6, // INS_stgt_h
        8113, 6, // INS_stgt_w
        8120, 6, // INS_stgt_d
        8127, 6, // INS_stle_b
        8134, 6, // INS_stle_h
        8141, 6, // INS_stle_w
        8148, 6, // INS_stle_d
        8155, 4, // INS_dbar
        8160, 4, // INS_ibar
        8165, 7, // INS_syscall
        8173, 5, // INS_break
        8179, 5, // INS_preld
        8185, 6, // INS_preldx
        8192, 6, // INS_fadd_s
        8199, 6, // INS_fadd_d
        8206, 6, // INS_fsub_s
        8213, 6, // INS_fsub_d
        8220, 6, // INS_fmul_s
        8227, 6, // INS_fmul_d
        8234, 6, // INS_fdiv_s
        8241, 6, // INS_fdiv_d
        8248, 6, // INS_fmax_s
        8255, 6, // INS_fmax_d
        8262, 6, // INS_fmin_s
        8269, 6, // INS_fmin_d
        8276, 7, // INS_fmaxa_s
        8284, 7, // INS_fmaxa_d
        8292, 7, // INS_fmina_s
        8300, 7, // INS_fmina_d
        8308, 9, // INS_fscaleb_s
        8318, 9, // INS_fscaleb_d
        8328, 11, // INS_fcopysign_s
        8340, 11, // INS_fcopysign_d
        8352, 6, // INS_fldx_s
        8359, 6, // INS_fldx_d
        8366, 6, // INS_fstx_s
        8373, 6, // INS_fstx_d
        8380, 7, // INS_fldgt_s
        8388, 7, // INS_fldgt_d
        8396, 7, // INS_fldle_s
        8404, 7, // INS_fldle_d
        8412, 7, // INS_fstgt_s
        8420, 7, // INS_fstgt_d
        8428, 7, // INS_fstle_s
        8436, 7, // INS_fstle_d
        8444, 7, // INS_fmadd_s
        8452, 7, // INS_fmadd_d
        8460, 7, // INS_fmsub_s
        8468, 7, // INS_fmsub_d
        8476, 8, // INS_fnmadd_s
        8485, 8, // INS_fnmadd_d
        8494, 8, // INS_fnmsub_s
        8503, 8, // INS_fnmsub_d
        8512, 6, // INS_fabs_s
        8519, 6, // INS_fabs_d
        8526, 6, // INS_fneg_s
        8533, 6, // INS_fneg_d
        8540, 7, // INS_fsqrt_s
        8548, 7, // INS_fsqrt_d
        8556, 8, // INS_frsqrt_s
        8565, 8, // INS_frsqrt_d
        8574, 9, // INS_frsqrte_s
        8584, 9, // INS_frsqrte_d
        8594, 8, // INS_frecip_s
        8603, 8, // INS_frecip_d
        8612, 9, // INS_frecipe_s
        8622, 9, // INS_frecipe_d
        8632, 7, // INS_flogb_s
        8640, 7, // INS_flogb_d
        8648, 8, // INS_fclass_s
        8657, 8, // INS_fclass_d
        8666, 8, // INS_fcvt_s_d
        8675, 8, // INS_fcvt_d_s
        8684, 9, // INS_ffint_s_w
        8694, 9, // INS_ffint_s_l
        8704, 9, // INS_ffint_d_w
        8714, 9, // INS_ffint_d_l
        8724, 9, // INS_ftint_w_s
        8734, 9, // INS_ftint_w_d
        8744, 9, // INS_ftint_l_s
        8754, 9, // INS_ftint_l_d
        8764, 11, // INS_ftintrm_w_s
        8776, 11, // INS_ftintrm_w_d
        8788, 11, // INS_ftintrm_l_s
        8800, 11, // INS_ftintrm_l_d
        8812, 11, // INS_ftintrp_w_s
        8824, 11, // INS_ftintrp_w_d
        8836, 11, // INS_ftintrp_l_s
        8848, 11, // INS_ftintrp_l_d
        8860, 11, // INS_ftintrz_w_s
        8872, 11, // INS_ftintrz_w_d
        8884, 11, // INS_ftintrz_l_s
        8896, 11, // INS_ftintrz_l_d
        8908, 12, // INS_ftintrne_w_s
        8921, 12, // INS_ftintrne_w_d
        8934, 12, // INS_ftintrne_l_s
        8947, 12, // INS_ftintrne_l_d
        8960, 7, // INS_frint_s
        8968, 7, // INS_frint_d
        8976, 6, // INS_fmov_s
        8983, 6, // INS_fmov_d
        8990, 10, // INS_movgr2fr_w
        9001, 10, // INS_movgr2fr_d
        9012, 11, // INS_movgr2frh_w
        9024, 10, // INS_movgr2fcsr
        9035, 10, // INS_movfcsr2gr
        9046, 8, // INS_movfr2cf
        9055, 8, // INS_movcf2fr
        9064, 8, // INS_movgr2cf
        9073, 8, // INS_movcf2gr
        9082, 10, // INS_fcmp_caf_s
        9093, 10, // INS_fcmp_cun_s
        9104, 10, // INS_fcmp_ceq_s
        9115, 11, // INS_fcmp_cueq_s
        9127, 10, // INS_fcmp_clt_s
        9138, 11, // INS_fcmp_cult_s
        9150, 10, // INS_fcmp_cle_s
        9161, 11, // INS_fcmp_cule_s
        9173, 10, // INS_fcmp_cne_s
        9184, 10, // INS_fcmp_cor_s
        9195, 11, // INS_fcmp_cune_s
        9207, 10, // INS_fcmp_saf_d
        9218, 10, // INS_fcmp_sun_d
        9229, 10, // INS_fcmp_seq_d
        9240, 11, // INS_fcmp_sueq_d
        9252, 10, // INS_fcmp_slt_d
        9263, 11, // INS_fcmp_sult_d
        9275, 10, // INS_fcmp_sle_d
        9286, 11, // INS_fcmp_sule_d
        9298, 10, // INS_fcmp_sne_d
        9309, 10, // INS_fcmp_sor_d
        9320, 11, // INS_fcmp_sune_d
        9332, 10, // INS_fcmp_caf_d
        9343, 10, // INS_fcmp_cun_d
        9354, 10, // INS_fcmp_ceq_d
        9365, 11, // INS_fcmp_cueq_d
        9377, 10, // INS_fcmp_clt_d
        9388, 11, // INS_fcmp_cult_d
        9400, 10, // INS_fcmp_cle_d
        9411, 11, // INS_fcmp_cule_d
        9423, 10, // INS_fcmp_cne_d
        9434, 10, // INS_fcmp_cor_d
        9445, 11, // INS_fcmp_cune_d
        9457, 10, // INS_fcmp_saf_s
        9468, 10, // INS_fcmp_sun_s
        9479, 10, // INS_fcmp_seq_s
        9490, 11, // INS_fcmp_sueq_s
        9502, 10, // INS_fcmp_slt_s
        9513, 11, // INS_fcmp_sult_s
        9525, 10, // INS_fcmp_sle_s
        9536, 11, // INS_fcmp_sule_s
        9548, 10, // INS_fcmp_sne_s
        9559, 10, // INS_fcmp_sor_s
        9570, 11, // INS_fcmp_sune_s
        9582, 5, // INS_fld_s
        9588, 5, // INS_fld_d
        9594, 5, // INS_fst_s
        9600, 5, // INS_fst_d
    ];

    private static ReadOnlySpan<int> s_instructionNameRangesRiscv64 => [
        0, 7, // INS_invalid
        349, 3, // INS_nop
        25303, 2, // INS_mov
        25306, 6, // INS_sext_w
        2869, 3, // INS_not
        25313, 3, // INS_lui
        25317, 5, // INS_auipc
        25323, 4, // INS_addi
        7931, 4, // INS_slti
        25328, 5, // INS_sltiu
        7951, 4, // INS_xori
        7947, 3, // INS_ori
        7942, 4, // INS_andi
        25334, 4, // INS_slli
        25339, 4, // INS_srli
        25344, 4, // INS_srai
        8, 3, // INS_add
        12, 3, // INS_sub
        25349, 3, // INS_sll
        6783, 3, // INS_slt
        6787, 4, // INS_sltu
        6595, 3, // INS_xor
        25353, 3, // INS_srl
        25357, 3, // INS_sra
        6592, 2, // INS_or
        103, 3, // INS_and
        25361, 5, // INS_fence
        25367, 7, // INS_fence_i
        25375, 5, // INS_csrrw
        25381, 5, // INS_csrrs
        25387, 5, // INS_csrrc
        25393, 6, // INS_csrrwi
        25400, 6, // INS_csrrsi
        25407, 6, // INS_csrrci
        25414, 5, // INS_ecall
        25420, 6, // INS_ebreak
        25427, 2, // INS_lb
        25430, 2, // INS_lh
        25433, 2, // INS_lw
        25436, 3, // INS_lbu
        25440, 3, // INS_lhu
        25444, 2, // INS_sb
        25447, 2, // INS_sh
        25450, 2, // INS_sw
        25453, 3, // INS_jal
        25457, 1, // INS_j
        6553, 4, // INS_beqz
        6558, 4, // INS_bnez
        25459, 4, // INS_jalr
        162, 3, // INS_beq
        166, 3, // INS_bne
        206, 3, // INS_blt
        202, 3, // INS_bge
        6543, 4, // INS_bltu
        6548, 4, // INS_bgeu
        25464, 5, // INS_addiw
        25470, 5, // INS_slliw
        25476, 5, // INS_srliw
        25482, 5, // INS_sraiw
        261, 4, // INS_addw
        424, 4, // INS_subw
        25488, 4, // INS_sllw
        25493, 4, // INS_srlw
        25498, 4, // INS_sraw
        25503, 3, // INS_lwu
        25507, 2, // INS_ld
        25510, 2, // INS_sd
        253, 3, // INS_mul
        25513, 4, // INS_mulh
        25518, 6, // INS_mulhsu
        25525, 5, // INS_mulhu
        25531, 3, // INS_div
        25535, 4, // INS_divu
        25540, 3, // INS_rem
        25544, 4, // INS_remu
        25549, 4, // INS_mulw
        25554, 4, // INS_divw
        25559, 5, // INS_divuw
        25565, 4, // INS_remw
        25570, 5, // INS_remuw
        8444, 7, // INS_fmadd_s
        8460, 7, // INS_fmsub_s
        8494, 8, // INS_fnmsub_s
        8476, 8, // INS_fnmadd_s
        8192, 6, // INS_fadd_s
        8206, 6, // INS_fsub_s
        8220, 6, // INS_fmul_s
        8234, 6, // INS_fdiv_s
        8540, 7, // INS_fsqrt_s
        25576, 7, // INS_fsgnj_s
        25584, 8, // INS_fsgnjn_s
        25593, 8, // INS_fsgnjx_s
        8262, 6, // INS_fmin_s
        8248, 6, // INS_fmax_s
        25602, 8, // INS_fcvt_w_s
        25611, 9, // INS_fcvt_wu_s
        25621, 7, // INS_fmv_x_w
        25629, 5, // INS_feq_s
        25635, 5, // INS_flt_s
        25641, 5, // INS_fle_s
        8648, 8, // INS_fclass_s
        25647, 8, // INS_fcvt_s_w
        25656, 9, // INS_fcvt_s_wu
        25666, 7, // INS_fmv_w_x
        8452, 7, // INS_fmadd_d
        8468, 7, // INS_fmsub_d
        8503, 8, // INS_fnmsub_d
        8485, 8, // INS_fnmadd_d
        8199, 6, // INS_fadd_d
        8213, 6, // INS_fsub_d
        8227, 6, // INS_fmul_d
        8241, 6, // INS_fdiv_d
        8548, 7, // INS_fsqrt_d
        25674, 7, // INS_fsgnj_d
        25682, 8, // INS_fsgnjn_d
        25691, 8, // INS_fsgnjx_d
        8269, 6, // INS_fmin_d
        8255, 6, // INS_fmax_d
        8666, 8, // INS_fcvt_s_d
        8675, 8, // INS_fcvt_d_s
        25700, 5, // INS_feq_d
        25706, 5, // INS_flt_d
        25712, 5, // INS_fle_d
        8657, 8, // INS_fclass_d
        25718, 8, // INS_fcvt_w_d
        25727, 9, // INS_fcvt_wu_d
        25737, 8, // INS_fcvt_d_w
        25746, 9, // INS_fcvt_d_wu
        25756, 3, // INS_flw
        25760, 3, // INS_fsw
        25764, 3, // INS_fld
        25768, 3, // INS_fsd
        25772, 8, // INS_fcvt_l_s
        25781, 9, // INS_fcvt_lu_s
        25791, 8, // INS_fcvt_s_l
        25800, 9, // INS_fcvt_s_lu
        25810, 8, // INS_fcvt_l_d
        25819, 9, // INS_fcvt_lu_d
        25829, 7, // INS_fmv_x_d
        25837, 8, // INS_fcvt_d_l
        25846, 9, // INS_fcvt_d_lu
        25856, 7, // INS_fmv_d_x
        25864, 4, // INS_lr_w
        25869, 4, // INS_lr_d
        8065, 4, // INS_sc_w
        8070, 4, // INS_sc_d
        25874, 9, // INS_amoswap_w
        25884, 9, // INS_amoswap_d
        25894, 8, // INS_amoadd_w
        25903, 8, // INS_amoadd_d
        25912, 8, // INS_amoxor_w
        25921, 8, // INS_amoxor_d
        25930, 8, // INS_amoand_w
        25939, 8, // INS_amoand_d
        25948, 7, // INS_amoor_w
        25956, 7, // INS_amoor_d
        25964, 8, // INS_amomin_w
        25973, 8, // INS_amomin_d
        25982, 8, // INS_amomax_w
        25991, 8, // INS_amomax_d
        26000, 9, // INS_amominu_w
        26010, 9, // INS_amominu_d
        26020, 9, // INS_amomaxu_w
        26030, 9, // INS_amomaxu_d
        291, 3, // INS_clz
        26040, 4, // INS_clzw
        2865, 3, // INS_ctz
        26045, 4, // INS_ctzw
        26050, 4, // INS_cpop
        26055, 5, // INS_cpopw
        26061, 6, // INS_sext_b
        26068, 6, // INS_sext_h
        26075, 6, // INS_zext_h
        26082, 4, // INS_rev8
        26087, 3, // INS_rol
        26091, 4, // INS_rolw
        76, 3, // INS_ror
        26096, 4, // INS_rorw
        26101, 4, // INS_xnor
        115, 3, // INS_orn
        6599, 4, // INS_andn
        26106, 3, // INS_min
        26110, 4, // INS_minu
        26115, 3, // INS_max
        26119, 4, // INS_maxu
        26124, 4, // INS_rori
        26129, 5, // INS_roriw
        26135, 6, // INS_sh1add
        26142, 6, // INS_sh2add
        26149, 6, // INS_sh3add
        26156, 6, // INS_add_uw
        26163, 9, // INS_sh1add_uw
        26173, 9, // INS_sh2add_uw
        26183, 9, // INS_sh3add_uw
        26193, 7, // INS_slli_uw
        26201, 4, // INS_bset
        26206, 4, // INS_bclr
        5768, 4, // INS_bext
        26211, 4, // INS_binv
        26216, 5, // INS_bseti
        26222, 5, // INS_bclri
        26228, 5, // INS_bexti
        26234, 5, // INS_binvi
        26240, 9, // INS_czero_eqz
        26250, 9, // INS_czero_nez
        26260, 4, // INS_c_mv
        26265, 5, // INS_c_add
        26271, 5, // INS_c_and
        26277, 4, // INS_c_or
        26282, 5, // INS_c_xor
        26288, 5, // INS_c_sub
        26294, 6, // INS_c_addw
        26301, 6, // INS_c_subw
    ];

    private static ReadOnlySpan<int> s_instructionNameRangesWasm => [
        0, 7, // INS_invalid
        26308, 11, // INS_unreachable
        26320, 5, // INS_label
        26326, 9, // INS_catch_ref
        26336, 9, // INS_local_cnt
        26346, 5, // INS_local_decl
        26352, 9, // INS_code_size
        349, 3, // INS_nop
        26362, 5, // INS_block
        26368, 4, // INS_loop
        26373, 2, // INS_if
        26376, 4, // INS_else
        26381, 9, // INS_throw_ref
        26391, 3, // INS_end
        2202, 2, // INS_br
        26395, 5, // INS_br_if
        26401, 8, // INS_br_table
        26410, 6, // INS_return
        26417, 4, // INS_call
        26422, 13, // INS_call_indirect
        26436, 11, // INS_return_call
        26448, 20, // INS_return_call_indirect
        26469, 12, // INS_call_funclet
        26482, 4, // INS_drop
        26487, 9, // INS_try_table
        26497, 9, // INS_local_get
        26507, 9, // INS_local_set
        26517, 9, // INS_local_tee
        26527, 10, // INS_global_get
        26538, 10, // INS_global_set
        26549, 8, // INS_i32_load
        26558, 8, // INS_i64_load
        26567, 8, // INS_f32_load
        26576, 8, // INS_f64_load
        26585, 11, // INS_i32_load8_s
        26597, 11, // INS_i32_load8_u
        26609, 12, // INS_i32_load16_s
        26622, 12, // INS_i32_load16_u
        26635, 11, // INS_i64_load8_s
        26647, 11, // INS_i64_load8_u
        26659, 12, // INS_i64_load16_s
        26672, 12, // INS_i64_load16_u
        26685, 12, // INS_i64_load32_s
        26698, 12, // INS_i64_load32_u
        26711, 9, // INS_i32_store
        26721, 9, // INS_i64_store
        26731, 9, // INS_f32_store
        26741, 9, // INS_f64_store
        26751, 10, // INS_i32_store8
        26762, 11, // INS_i32_store16
        26774, 9, // INS_i32_const
        26784, 17, // INS_i32_const_address
        26802, 17, // INS_i32_const_funcptr
        26820, 20, // INS_i32_const_funcletptr
        26841, 23, // INS_i32_const_frameidentity
        26865, 18, // INS_i32_const_dataoffs
        26884, 9, // INS_i64_const
        26894, 9, // INS_f32_const
        26904, 9, // INS_f64_const
        26914, 7, // INS_i32_eqz
        26922, 6, // INS_i32_eq
        26929, 6, // INS_i32_ne
        26936, 8, // INS_i32_lt_s
        26945, 8, // INS_i32_lt_u
        26954, 8, // INS_i32_gt_s
        26963, 8, // INS_i32_gt_u
        26972, 8, // INS_i32_le_s
        26981, 8, // INS_i32_le_u
        26990, 8, // INS_i32_ge_s
        26999, 8, // INS_i32_ge_u
        27008, 7, // INS_i64_eqz
        27016, 6, // INS_i64_eq
        27023, 6, // INS_i64_ne
        27030, 8, // INS_i64_lt_s
        27039, 8, // INS_i64_lt_u
        27048, 8, // INS_i64_gt_s
        27057, 8, // INS_i64_gt_u
        27066, 8, // INS_i64_le_s
        27075, 8, // INS_i64_le_u
        27084, 8, // INS_i64_ge_s
        27093, 8, // INS_i64_ge_u
        27102, 6, // INS_f32_eq
        27109, 6, // INS_f32_ne
        27116, 6, // INS_f32_lt
        27123, 6, // INS_f32_gt
        27130, 6, // INS_f32_le
        27137, 6, // INS_f32_ge
        27144, 6, // INS_f64_eq
        27151, 6, // INS_f64_ne
        27158, 6, // INS_f64_lt
        27165, 6, // INS_f64_gt
        27172, 6, // INS_f64_le
        27179, 6, // INS_f64_ge
        27186, 7, // INS_i32_clz
        27194, 7, // INS_i32_ctz
        27202, 10, // INS_i32_popcnt
        27213, 7, // INS_i32_add
        27221, 7, // INS_i32_sub
        27229, 7, // INS_i32_mul
        27237, 9, // INS_i32_div_s
        27247, 9, // INS_i32_div_u
        27257, 9, // INS_i32_rem_s
        27267, 9, // INS_i32_rem_u
        27277, 7, // INS_i32_and
        27285, 6, // INS_i32_or
        27292, 7, // INS_i32_xor
        27300, 7, // INS_i32_shl
        27308, 9, // INS_i32_shr_s
        27318, 9, // INS_i32_shr_u
        27328, 8, // INS_i32_rotl
        27337, 8, // INS_i32_rotr
        27346, 7, // INS_i64_clz
        27354, 7, // INS_i64_ctz
        27362, 10, // INS_i64_popcnt
        27373, 7, // INS_i64_add
        27381, 7, // INS_i64_sub
        27389, 7, // INS_i64_mul
        27397, 9, // INS_i64_div_s
        27407, 9, // INS_i64_div_u
        27417, 9, // INS_i64_rem_s
        27427, 9, // INS_i64_rem_u
        27437, 7, // INS_i64_and
        27445, 6, // INS_i64_or
        27452, 7, // INS_i64_xor
        27460, 7, // INS_i64_shl
        27468, 9, // INS_i64_shr_s
        27478, 9, // INS_i64_shr_u
        27488, 8, // INS_i64_rotl
        27497, 8, // INS_i64_rotr
        27506, 7, // INS_f32_abs
        27514, 7, // INS_f32_neg
        27522, 8, // INS_f32_ceil
        27531, 9, // INS_f32_floor
        27541, 9, // INS_f32_trunc
        27551, 11, // INS_f32_nearest
        27563, 8, // INS_f32_sqrt
        27572, 7, // INS_f32_add
        27580, 7, // INS_f32_sub
        27588, 7, // INS_f32_mul
        27596, 7, // INS_f32_div
        27604, 7, // INS_f32_min
        27612, 7, // INS_f32_max
        27620, 12, // INS_f32_copysign
        27633, 7, // INS_f64_abs
        27641, 7, // INS_f64_neg
        27649, 8, // INS_f64_ceil
        27658, 9, // INS_f64_floor
        27668, 9, // INS_f64_trunc
        27678, 11, // INS_f64_nearest
        27690, 8, // INS_f64_sqrt
        27699, 7, // INS_f64_add
        27707, 7, // INS_f64_sub
        27715, 7, // INS_f64_mul
        27723, 7, // INS_f64_div
        27731, 7, // INS_f64_min
        27739, 7, // INS_f64_max
        27747, 12, // INS_f64_copysign
        27760, 12, // INS_i32_wrap_i64
        27773, 15, // INS_i32_trunc_s_f32
        27789, 15, // INS_i32_trunc_u_f32
        27805, 15, // INS_i32_trunc_s_f64
        27821, 15, // INS_i32_trunc_u_f64
        27837, 16, // INS_i64_extend_s_i32
        27854, 16, // INS_i64_extend_u_i32
        27871, 15, // INS_i64_trunc_s_f32
        27887, 15, // INS_i64_trunc_u_f32
        27903, 15, // INS_i64_trunc_s_f64
        27919, 15, // INS_i64_trunc_u_f64
        27935, 17, // INS_f32_convert_s_i32
        27953, 17, // INS_f32_convert_u_i32
        27971, 17, // INS_f32_convert_s_i64
        27989, 17, // INS_f32_convert_u_i64
        28007, 14, // INS_f32_demote_f64
        28022, 17, // INS_f64_convert_s_i32
        28040, 17, // INS_f64_convert_u_i32
        28058, 17, // INS_f64_convert_s_i64
        28076, 17, // INS_f64_convert_u_i64
        28094, 15, // INS_f64_promote_f32
        28110, 19, // INS_i32_reinterpret_f32
        28130, 19, // INS_i64_reinterpret_f64
        28150, 19, // INS_f32_reinterpret_i32
        28170, 19, // INS_f64_reinterpret_i64
        28190, 13, // INS_i32_extend8_s
        28204, 14, // INS_i32_extend16_s
        28219, 13, // INS_i64_extend8_s
        28233, 14, // INS_i64_extend16_s
        28248, 14, // INS_i64_extend32_s
        28263, 19, // INS_i32_trunc_sat_f32_s
        28283, 19, // INS_i32_trunc_sat_f32_u
        28303, 19, // INS_i32_trunc_sat_f64_s
        28323, 19, // INS_i32_trunc_sat_f64_u
        28343, 19, // INS_i64_trunc_sat_f32_s
        28363, 19, // INS_i64_trunc_sat_f32_u
        28383, 19, // INS_i64_trunc_sat_f64_s
        28403, 19, // INS_i64_trunc_sat_f64_u
        28423, 11, // INS_memory_copy
        28435, 11, // INS_memory_fill
        28447, 9, // INS_v128_load
        28457, 14, // INS_v128_load8x8_s
        28472, 14, // INS_v128_load8x8_u
        28487, 15, // INS_v128_load16x4_s
        28503, 15, // INS_v128_load16x4_u
        28519, 15, // INS_v128_load32x2_s
        28535, 15, // INS_v128_load32x2_u
        28551, 16, // INS_v128_load8_splat
        28568, 17, // INS_v128_load16_splat
        28586, 17, // INS_v128_load32_splat
        28604, 17, // INS_v128_load64_splat
        28622, 10, // INS_v128_store
        28633, 10, // INS_v128_const
        28644, 13, // INS_i8x16_shuffle
        28658, 13, // INS_i8x16_swizzle
        28672, 11, // INS_i8x16_splat
        28684, 11, // INS_i16x8_splat
        28696, 11, // INS_i32x4_splat
        28708, 11, // INS_i64x2_splat
        28720, 11, // INS_f32x4_splat
        28732, 11, // INS_f64x2_splat
        28744, 20, // INS_i8x16_extract_lane_s
        28765, 20, // INS_i8x16_extract_lane_u
        28786, 18, // INS_i8x16_replace_lane
        28805, 20, // INS_i16x8_extract_lane_s
        28826, 20, // INS_i16x8_extract_lane_u
        28847, 18, // INS_i16x8_replace_lane
        28866, 18, // INS_i32x4_extract_lane
        28885, 18, // INS_i32x4_replace_lane
        28904, 18, // INS_i64x2_extract_lane
        28923, 18, // INS_i64x2_replace_lane
        28942, 18, // INS_f32x4_extract_lane
        28961, 18, // INS_f32x4_replace_lane
        28980, 18, // INS_f64x2_extract_lane
        28999, 18, // INS_f64x2_replace_lane
        29018, 8, // INS_i8x16_eq
        29027, 8, // INS_i8x16_ne
        29036, 10, // INS_i8x16_lt_s
        29047, 10, // INS_i8x16_lt_u
        29058, 10, // INS_i8x16_gt_s
        29069, 10, // INS_i8x16_gt_u
        29080, 10, // INS_i8x16_le_s
        29091, 10, // INS_i8x16_le_u
        29102, 10, // INS_i8x16_ge_s
        29113, 10, // INS_i8x16_ge_u
        29124, 8, // INS_i16x8_eq
        29133, 8, // INS_i16x8_ne
        29142, 10, // INS_i16x8_lt_s
        29153, 10, // INS_i16x8_lt_u
        29164, 10, // INS_i16x8_gt_s
        29175, 10, // INS_i16x8_gt_u
        29186, 10, // INS_i16x8_le_s
        29197, 10, // INS_i16x8_le_u
        29208, 10, // INS_i16x8_ge_s
        29219, 10, // INS_i16x8_ge_u
        29230, 8, // INS_i32x4_eq
        29239, 8, // INS_i32x4_ne
        29248, 10, // INS_i32x4_lt_s
        29259, 10, // INS_i32x4_lt_u
        29270, 10, // INS_i32x4_gt_s
        29281, 10, // INS_i32x4_gt_u
        29292, 10, // INS_i32x4_le_s
        29303, 10, // INS_i32x4_le_u
        29314, 10, // INS_i32x4_ge_s
        29325, 10, // INS_i32x4_ge_u
        29336, 8, // INS_i64x2_eq
        29345, 8, // INS_i64x2_ne
        29354, 10, // INS_i64x2_lt_s
        29365, 10, // INS_i64x2_gt_s
        29376, 10, // INS_i64x2_le_s
        29387, 10, // INS_i64x2_ge_s
        29398, 8, // INS_f32x4_eq
        29407, 8, // INS_f32x4_ne
        29416, 8, // INS_f32x4_lt
        29425, 8, // INS_f32x4_gt
        29434, 8, // INS_f32x4_le
        29443, 8, // INS_f32x4_ge
        29452, 8, // INS_f64x2_eq
        29461, 8, // INS_f64x2_ne
        29470, 8, // INS_f64x2_lt
        29479, 8, // INS_f64x2_gt
        29488, 8, // INS_f64x2_le
        29497, 8, // INS_f64x2_ge
        29506, 8, // INS_v128_not
        29515, 8, // INS_v128_and
        29524, 11, // INS_v128_andnot
        29536, 7, // INS_v128_or
        29544, 8, // INS_v128_xor
        29553, 14, // INS_v128_bitselect
        29568, 13, // INS_v128_any_true
        29582, 15, // INS_v128_load8_lane
        29598, 16, // INS_v128_load16_lane
        29615, 16, // INS_v128_load32_lane
        29632, 16, // INS_v128_load64_lane
        29649, 16, // INS_v128_store8_lane
        29666, 17, // INS_v128_store16_lane
        29684, 17, // INS_v128_store32_lane
        29702, 17, // INS_v128_store64_lane
        29720, 16, // INS_v128_load32_zero
        29737, 16, // INS_v128_load64_zero
        29754, 23, // INS_f32x4_demote_f64x2_zero
        29778, 23, // INS_f64x2_promote_low_f32x4
        29802, 9, // INS_i8x16_abs
        29812, 9, // INS_i8x16_neg
        29822, 12, // INS_i8x16_popcnt
        29835, 14, // INS_i8x16_all_true
        29850, 13, // INS_i8x16_bitmask
        29864, 20, // INS_i8x16_narrow_i16x8_s
        29885, 20, // INS_i8x16_narrow_i16x8_u
        29906, 10, // INS_f32x4_ceil
        29917, 11, // INS_f32x4_floor
        29929, 11, // INS_f32x4_trunc
        29941, 13, // INS_f32x4_nearest
        29955, 9, // INS_i8x16_shl
        29965, 11, // INS_i8x16_shr_s
        29977, 11, // INS_i8x16_shr_u
        29989, 9, // INS_i8x16_add
        29999, 15, // INS_i8x16_add_sat_s
        30015, 15, // INS_i8x16_add_sat_u
        30031, 9, // INS_i8x16_sub
        30041, 15, // INS_i8x16_sub_sat_s
        30057, 15, // INS_i8x16_sub_sat_u
        30073, 10, // INS_f64x2_ceil
        30084, 11, // INS_f64x2_floor
        30096, 11, // INS_i8x16_min_s
        30108, 11, // INS_i8x16_min_u
        30120, 11, // INS_i8x16_max_s
        30132, 11, // INS_i8x16_max_u
        30144, 11, // INS_f64x2_trunc
        30156, 12, // INS_i8x16_avgr_u
        30169, 29, // INS_i16x8_extadd_pairwise_s_i8x16
        30199, 29, // INS_i16x8_extadd_pairwise_u_i8x16
        30229, 29, // INS_i32x4_extadd_pairwise_s_i16x8
        30259, 29, // INS_i32x4_extadd_pairwise_u_i16x8
        30289, 9, // INS_i16x8_abs
        30299, 9, // INS_i16x8_neg
        30309, 19, // INS_i16x8_q15mulr_sat_s
        30329, 14, // INS_i16x8_all_true
        30344, 13, // INS_i16x8_bitmask
        30358, 20, // INS_i16x8_narrow_i32x4_s
        30379, 20, // INS_i16x8_narrow_i32x4_u
        30400, 24, // INS_i16x8_extend_low_s_i8x16
        30425, 25, // INS_i16x8_extend_high_s_i8x16
        30451, 24, // INS_i16x8_extend_low_u_i8x16
        30476, 25, // INS_i16x8_extend_high_u_i8x16
        30502, 9, // INS_i16x8_shl
        30512, 11, // INS_i16x8_shr_s
        30524, 11, // INS_i16x8_shr_u
        30536, 9, // INS_i16x8_add
        30546, 15, // INS_i16x8_add_sat_s
        30562, 15, // INS_i16x8_add_sat_u
        30578, 9, // INS_i16x8_sub
        30588, 15, // INS_i16x8_sub_sat_s
        30604, 15, // INS_i16x8_sub_sat_u
        30620, 13, // INS_f64x2_nearest
        30634, 9, // INS_i16x8_mul
        30644, 11, // INS_i16x8_min_s
        30656, 11, // INS_i16x8_min_u
        30668, 11, // INS_i16x8_max_s
        30680, 11, // INS_i16x8_max_u
        30692, 12, // INS_i16x8_avgr_u
        30705, 24, // INS_i16x8_extmul_low_s_i8x16
        30730, 25, // INS_i16x8_extmul_high_s_i8x16
        30756, 24, // INS_i16x8_extmul_low_u_i8x16
        30781, 25, // INS_i16x8_extmul_high_u_i8x16
        30807, 9, // INS_i32x4_abs
        30817, 9, // INS_i32x4_neg
        30827, 14, // INS_i32x4_all_true
        30842, 13, // INS_i32x4_bitmask
        30856, 24, // INS_i32x4_extend_low_s_i16x8
        30881, 25, // INS_i32x4_extend_high_s_i16x8
        30907, 24, // INS_i32x4_extend_low_u_i16x8
        30932, 25, // INS_i32x4_extend_high_u_i16x8
        30958, 9, // INS_i32x4_shl
        30968, 11, // INS_i32x4_shr_s
        30980, 11, // INS_i32x4_shr_u
        30992, 9, // INS_i32x4_add
        31002, 9, // INS_i32x4_sub
        31012, 9, // INS_i32x4_mul
        31022, 11, // INS_i32x4_min_s
        31034, 11, // INS_i32x4_min_u
        31046, 11, // INS_i32x4_max_s
        31058, 11, // INS_i32x4_max_u
        31070, 17, // INS_i32x4_dot_i16x8_s
        31088, 24, // INS_i32x4_extmul_low_s_i16x8
        31113, 25, // INS_i32x4_extmul_high_s_i16x8
        31139, 24, // INS_i32x4_extmul_low_u_i16x8
        31164, 25, // INS_i32x4_extmul_high_u_i16x8
        31190, 9, // INS_i64x2_abs
        31200, 9, // INS_i64x2_neg
        31210, 14, // INS_i64x2_all_true
        31225, 13, // INS_i64x2_bitmask
        31239, 24, // INS_i64x2_extend_low_s_i32x4
        31264, 25, // INS_i64x2_extend_high_s_i32x4
        31290, 24, // INS_i64x2_extend_low_u_i32x4
        31315, 25, // INS_i64x2_extend_high_u_i32x4
        31341, 9, // INS_i64x2_shl
        31351, 11, // INS_i64x2_shr_s
        31363, 11, // INS_i64x2_shr_u
        31375, 9, // INS_i64x2_add
        31385, 9, // INS_i64x2_sub
        31395, 9, // INS_i64x2_mul
        31405, 24, // INS_i64x2_extmul_low_s_i32x4
        31430, 25, // INS_i64x2_extmul_high_s_i32x4
        31456, 24, // INS_i64x2_extmul_low_u_i32x4
        31481, 25, // INS_i64x2_extmul_high_u_i32x4
        31507, 9, // INS_f32x4_abs
        31517, 9, // INS_f32x4_neg
        31527, 10, // INS_f32x4_sqrt
        31538, 9, // INS_f32x4_add
        31548, 9, // INS_f32x4_sub
        31558, 9, // INS_f32x4_mul
        31568, 9, // INS_f32x4_div
        31578, 9, // INS_f32x4_min
        31588, 9, // INS_f32x4_max
        31598, 10, // INS_f32x4_pmin
        31609, 10, // INS_f32x4_pmax
        31620, 9, // INS_f64x2_abs
        31630, 9, // INS_f64x2_neg
        31640, 10, // INS_f64x2_sqrt
        31651, 9, // INS_f64x2_add
        31661, 9, // INS_f64x2_sub
        31671, 9, // INS_f64x2_mul
        31681, 9, // INS_f64x2_div
        31691, 9, // INS_f64x2_min
        31701, 9, // INS_f64x2_max
        31711, 10, // INS_f64x2_pmin
        31722, 10, // INS_f64x2_pmax
        31733, 23, // INS_i32x4_trunc_sat_s_f32x4
        31757, 23, // INS_i32x4_trunc_sat_u_f32x4
        31781, 21, // INS_f32x4_convert_s_i32x4
        31803, 21, // INS_f32x4_convert_u_i32x4
        31825, 28, // INS_i32x4_trunc_sat_s_f64x2_zero
        31854, 28, // INS_i32x4_trunc_sat_u_f64x2_zero
        31883, 25, // INS_f64x2_convert_low_s_i32x4
        31909, 25, // INS_f64x2_convert_low_u_i32x4
    ];
}