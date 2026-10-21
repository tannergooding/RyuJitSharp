// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

internal static class StateMachineTables
{
    internal static ReadOnlySpan<byte> s_smStateTermBits => [
        0xFC, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0x55, 0x56, 0xA8, 0x32, 0x03,
    ];

    // The least-significant bit in each byte corresponds to the lowest state ID in that byte.
    internal static bool IsTerminalState(SM_STATE_ID stateId)
    {
        var index = (int)stateId;
        if ((uint)index >= (uint)s_smStateLength.Length)
        {
            JitExceptionHelpers.ThrowIndexOutOfRangeException();
        }

        return (s_smStateTermBits[index >> 3] & (1 << (index & 7))) != 0;
    }

    internal static ReadOnlySpan<byte> s_smStateLength => [
        0,
        0,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        2,
        2,
        2,
        2,
        2,
        2,
        2,
        2,
        2,
        2,
        2,
        2,
        2,
        2,
        2,
        2,
        2,
        2,
        2,
        2,
        2,
        2,
        2,
        2,
        2,
        2,
        2,
        2,
        3,
        2,
        3,
        2,
        3,
        2,
        3,
        4,
        3,
        2,
        3,
        2,
        3,
        2,
        3,
        4,
        5,
        6,
        5,
        6,
        5,
        6,
        5,
        6,
        3,
        4,
        5,
        5,
        3,
        4,
        5,
        5,
    ];

    internal static ReadOnlySpan<SM_STATE_ID> s_smStateLongestTermState => [
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        181,
        3,
        4,
        5,
        6,
        16,
        19,
        188,
        189,
        11,
        12,
        13,
        14,
        35,
        35,
        35,
        35,
        36,
        36,
        36,
        36,
        95,
        95,
        95,
        95,
        96,
        96,
        3,
        3,
        3,
        3,
        3,
        3,
        3,
        3,
        3,
        3,
        3,
        3,
        3,
        3,
        3,
        3,
        3,
        3,
        3,
        3,
        3,
        3,
        3,
        3,
        3,
        191,
        191,
        191,
        191,
        195,
        195,
        195,
        195,
    ];

    internal static ReadOnlySpan<SM_STATE_ID> s_smStatePrevState => [
        0,
        0,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        1,
        181,
        3,
        4,
        5,
        6,
        16,
        19,
        188,
        189,
        11,
        12,
        13,
        14,
        35,
        35,
        35,
        35,
        36,
        36,
        36,
        36,
        95,
        95,
        95,
        95,
        96,
        96,
        3,
        217,
        3,
        219,
        3,
        221,
        3,
        223,
        224,
        223,
        3,
        227,
        3,
        229,
        3,
        231,
        232,
        233,
        234,
        233,
        236,
        233,
        238,
        233,
        240,
        191,
        242,
        243,
        243,
        195,
        246,
        247,
        247,
    ];

    internal static ReadOnlySpan<SM_OPCODE> s_smStateOpcode => [
        SM_NOSHOW, // state 0 [invalid]
        SM_NOSHOW, // state 1 [start]
        SM_NOSHOW, // state 2 [noshow]
        SM_LDARG_0, // state 3 [ldarg.0]
        SM_LDARG_1, // state 4 [ldarg.1]
        SM_LDARG_2, // state 5 [ldarg.2]
        SM_LDARG_3, // state 6 [ldarg.3]
        SM_LDLOC_0, // state 7 [ldloc.0]
        SM_LDLOC_1, // state 8 [ldloc.1]
        SM_LDLOC_2, // state 9 [ldloc.2]
        SM_LDLOC_3, // state 10 [ldloc.3]
        SM_STLOC_0, // state 11 [stloc.0]
        SM_STLOC_1, // state 12 [stloc.1]
        SM_STLOC_2, // state 13 [stloc.2]
        SM_STLOC_3, // state 14 [stloc.3]
        SM_LDARG_S, // state 15 [ldarg.s]
        SM_LDARGA_S, // state 16 [ldarga.s]
        SM_STARG_S, // state 17 [starg.s]
        SM_LDLOC_S, // state 18 [ldloc.s]
        SM_LDLOCA_S, // state 19 [ldloca.s]
        SM_STLOC_S, // state 20 [stloc.s]
        SM_LDNULL, // state 21 [ldnull]
        SM_LDC_I4_M1, // state 22 [ldc.i4.m1]
        SM_LDC_I4_0, // state 23 [ldc.i4.0]
        SM_LDC_I4_1, // state 24 [ldc.i4.1]
        SM_LDC_I4_2, // state 25 [ldc.i4.2]
        SM_LDC_I4_3, // state 26 [ldc.i4.3]
        SM_LDC_I4_4, // state 27 [ldc.i4.4]
        SM_LDC_I4_5, // state 28 [ldc.i4.5]
        SM_LDC_I4_6, // state 29 [ldc.i4.6]
        SM_LDC_I4_7, // state 30 [ldc.i4.7]
        SM_LDC_I4_8, // state 31 [ldc.i4.8]
        SM_LDC_I4_S, // state 32 [ldc.i4.s]
        SM_LDC_I4, // state 33 [ldc.i4]
        SM_LDC_I8, // state 34 [ldc.i8]
        SM_LDC_R4, // state 35 [ldc.r4]
        SM_LDC_R8, // state 36 [ldc.r8]
        SM_UNUSED, // state 37 [unused]
        SM_DUP, // state 38 [dup]
        SM_POP, // state 39 [pop]
        SM_CALL, // state 40 [call]
        SM_CALLI, // state 41 [calli]
        SM_RET, // state 42 [ret]
        SM_BR_S, // state 43 [br.s]
        SM_BRFALSE_S, // state 44 [brfalse.s]
        SM_BRTRUE_S, // state 45 [brtrue.s]
        SM_BEQ_S, // state 46 [beq.s]
        SM_BGE_S, // state 47 [bge.s]
        SM_BGT_S, // state 48 [bgt.s]
        SM_BLE_S, // state 49 [ble.s]
        SM_BLT_S, // state 50 [blt.s]
        SM_BNE_UN_S, // state 51 [bne.un.s]
        SM_BGE_UN_S, // state 52 [bge.un.s]
        SM_BGT_UN_S, // state 53 [bgt.un.s]
        SM_BLE_UN_S, // state 54 [ble.un.s]
        SM_BLT_UN_S, // state 55 [blt.un.s]
        SM_LONG_BRANCH, // state 56 [long.branch]
        SM_SWITCH, // state 57 [switch]
        SM_LDIND_I1, // state 58 [ldind.i1]
        SM_LDIND_U1, // state 59 [ldind.u1]
        SM_LDIND_I2, // state 60 [ldind.i2]
        SM_LDIND_U2, // state 61 [ldind.u2]
        SM_LDIND_I4, // state 62 [ldind.i4]
        SM_LDIND_U4, // state 63 [ldind.u4]
        SM_LDIND_I8, // state 64 [ldind.i8]
        SM_LDIND_I, // state 65 [ldind.i]
        SM_LDIND_R4, // state 66 [ldind.r4]
        SM_LDIND_R8, // state 67 [ldind.r8]
        SM_LDIND_REF, // state 68 [ldind.ref]
        SM_STIND_REF, // state 69 [stind.ref]
        SM_STIND_I1, // state 70 [stind.i1]
        SM_STIND_I2, // state 71 [stind.i2]
        SM_STIND_I4, // state 72 [stind.i4]
        SM_STIND_I8, // state 73 [stind.i8]
        SM_STIND_R4, // state 74 [stind.r4]
        SM_STIND_R8, // state 75 [stind.r8]
        SM_ADD, // state 76 [add]
        SM_SUB, // state 77 [sub]
        SM_MUL, // state 78 [mul]
        SM_DIV, // state 79 [div]
        SM_DIV_UN, // state 80 [div.un]
        SM_REM, // state 81 [rem]
        SM_REM_UN, // state 82 [rem.un]
        SM_AND, // state 83 [and]
        SM_OR, // state 84 [or]
        SM_XOR, // state 85 [xor]
        SM_SHL, // state 86 [shl]
        SM_SHR, // state 87 [shr]
        SM_SHR_UN, // state 88 [shr.un]
        SM_NEG, // state 89 [neg]
        SM_NOT, // state 90 [not]
        SM_CONV_I1, // state 91 [conv.i1]
        SM_CONV_I2, // state 92 [conv.i2]
        SM_CONV_I4, // state 93 [conv.i4]
        SM_CONV_I8, // state 94 [conv.i8]
        SM_CONV_R4, // state 95 [conv.r4]
        SM_CONV_R8, // state 96 [conv.r8]
        SM_CONV_U4, // state 97 [conv.u4]
        SM_CONV_U8, // state 98 [conv.u8]
        SM_CALLVIRT, // state 99 [callvirt]
        SM_CPOBJ, // state 100 [cpobj]
        SM_LDOBJ, // state 101 [ldobj]
        SM_LDSTR, // state 102 [ldstr]
        SM_NEWOBJ, // state 103 [newobj]
        SM_CASTCLASS, // state 104 [castclass]
        SM_ISINST, // state 105 [isinst]
        SM_CONV_R_UN, // state 106 [conv.r.un]
        SM_UNBOX, // state 107 [unbox]
        SM_THROW, // state 108 [throw]
        SM_LDFLD, // state 109 [ldfld]
        SM_LDFLDA, // state 110 [ldflda]
        SM_STFLD, // state 111 [stfld]
        SM_LDSFLD, // state 112 [ldsfld]
        SM_LDSFLDA, // state 113 [ldsflda]
        SM_STSFLD, // state 114 [stsfld]
        SM_STOBJ, // state 115 [stobj]
        SM_OVF_NOTYPE_UN, // state 116 [ovf.notype.un]
        SM_BOX, // state 117 [box]
        SM_NEWARR, // state 118 [newarr]
        SM_LDLEN, // state 119 [ldlen]
        SM_LDELEMA, // state 120 [ldelema]
        SM_LDELEM_I1, // state 121 [ldelem.i1]
        SM_LDELEM_U1, // state 122 [ldelem.u1]
        SM_LDELEM_I2, // state 123 [ldelem.i2]
        SM_LDELEM_U2, // state 124 [ldelem.u2]
        SM_LDELEM_I4, // state 125 [ldelem.i4]
        SM_LDELEM_U4, // state 126 [ldelem.u4]
        SM_LDELEM_I8, // state 127 [ldelem.i8]
        SM_LDELEM_I, // state 128 [ldelem.i]
        SM_LDELEM_R4, // state 129 [ldelem.r4]
        SM_LDELEM_R8, // state 130 [ldelem.r8]
        SM_LDELEM_REF, // state 131 [ldelem.ref]
        SM_STELEM_I, // state 132 [stelem.i]
        SM_STELEM_I1, // state 133 [stelem.i1]
        SM_STELEM_I2, // state 134 [stelem.i2]
        SM_STELEM_I4, // state 135 [stelem.i4]
        SM_STELEM_I8, // state 136 [stelem.i8]
        SM_STELEM_R4, // state 137 [stelem.r4]
        SM_STELEM_R8, // state 138 [stelem.r8]
        SM_STELEM_REF, // state 139 [stelem.ref]
        SM_LDELEM, // state 140 [ldelem]
        SM_STELEM, // state 141 [stelem]
        SM_UNBOX_ANY, // state 142 [unbox.any]
        SM_CONV_OVF_I1, // state 143 [conv.ovf.i1]
        SM_CONV_OVF_U1, // state 144 [conv.ovf.u1]
        SM_CONV_OVF_I2, // state 145 [conv.ovf.i2]
        SM_CONV_OVF_U2, // state 146 [conv.ovf.u2]
        SM_CONV_OVF_I4, // state 147 [conv.ovf.i4]
        SM_CONV_OVF_U4, // state 148 [conv.ovf.u4]
        SM_CONV_OVF_I8, // state 149 [conv.ovf.i8]
        SM_CONV_OVF_U8, // state 150 [conv.ovf.u8]
        SM_REFANYVAL, // state 151 [refanyval]
        SM_CKFINITE, // state 152 [ckfinite]
        SM_MKREFANY, // state 153 [mkrefany]
        SM_LDTOKEN, // state 154 [ldtoken]
        SM_CONV_U2, // state 155 [conv.u2]
        SM_CONV_U1, // state 156 [conv.u1]
        SM_CONV_I, // state 157 [conv.i]
        SM_CONV_OVF_I, // state 158 [conv.ovf.i]
        SM_CONV_OVF_U, // state 159 [conv.ovf.u]
        SM_ADD_OVF, // state 160 [add.ovf]
        SM_MUL_OVF, // state 161 [mul.ovf]
        SM_SUB_OVF, // state 162 [sub.ovf]
        SM_LEAVE_S, // state 163 [leave.s]
        SM_STIND_I, // state 164 [stind.i]
        SM_CONV_U, // state 165 [conv.u]
        SM_PREFIX_N, // state 166 [prefix.n]
        SM_ARGLIST, // state 167 [arglist]
        SM_CEQ, // state 168 [ceq]
        SM_CGT, // state 169 [cgt]
        SM_CGT_UN, // state 170 [cgt.un]
        SM_CLT, // state 171 [clt]
        SM_CLT_UN, // state 172 [clt.un]
        SM_LDFTN, // state 173 [ldftn]
        SM_LDVIRTFTN, // state 174 [ldvirtftn]
        SM_LONG_LOC_ARG, // state 175 [long.loc.arg]
        SM_LOCALLOC, // state 176 [localloc]
        SM_UNALIGNED, // state 177 [unaligned]
        SM_VOLATILE, // state 178 [volatile]
        SM_TAILCALL, // state 179 [tailcall]
        SM_INITOBJ, // state 180 [initobj]
        SM_CONSTRAINED, // state 181 [constrained]
        SM_CPBLK, // state 182 [cpblk]
        SM_INITBLK, // state 183 [initblk]
        SM_RETHROW, // state 184 [rethrow]
        SM_SIZEOF, // state 185 [sizeof]
        SM_REFANYTYPE, // state 186 [refanytype]
        SM_READONLY, // state 187 [readonly]
        SM_LDARGA_S_NORMED, // state 188 [ldarga.s.normed]
        SM_LDLOCA_S_NORMED, // state 189 [ldloca.s.normed]
        SM_CALLVIRT, // state 190 [constrained -> callvirt]
        SM_LDFLD, // state 191 [ldarg.0 -> ldfld]
        SM_LDFLD, // state 192 [ldarg.1 -> ldfld]
        SM_LDFLD, // state 193 [ldarg.2 -> ldfld]
        SM_LDFLD, // state 194 [ldarg.3 -> ldfld]
        SM_LDFLD, // state 195 [ldarga.s -> ldfld]
        SM_LDFLD, // state 196 [ldloca.s -> ldfld]
        SM_LDFLD, // state 197 [ldarga.s.normed -> ldfld]
        SM_LDFLD, // state 198 [ldloca.s.normed -> ldfld]
        SM_LDLOC_0, // state 199 [stloc.0 -> ldloc.0]
        SM_LDLOC_1, // state 200 [stloc.1 -> ldloc.1]
        SM_LDLOC_2, // state 201 [stloc.2 -> ldloc.2]
        SM_LDLOC_3, // state 202 [stloc.3 -> ldloc.3]
        SM_ADD, // state 203 [ldc.r4 -> add]
        SM_SUB, // state 204 [ldc.r4 -> sub]
        SM_MUL, // state 205 [ldc.r4 -> mul]
        SM_DIV, // state 206 [ldc.r4 -> div]
        SM_ADD, // state 207 [ldc.r8 -> add]
        SM_SUB, // state 208 [ldc.r8 -> sub]
        SM_MUL, // state 209 [ldc.r8 -> mul]
        SM_DIV, // state 210 [ldc.r8 -> div]
        SM_ADD, // state 211 [conv.r4 -> add]
        SM_SUB, // state 212 [conv.r4 -> sub]
        SM_MUL, // state 213 [conv.r4 -> mul]
        SM_DIV, // state 214 [conv.r4 -> div]
        SM_MUL, // state 215 [conv.r8 -> mul]
        SM_DIV, // state 216 [conv.r8 -> div]
        SM_LDC_I4_0, // state 217 [ldarg.0 -> ldc.i4.0]
        SM_STFLD, // state 218 [ldarg.0 -> ldc.i4.0 -> stfld]
        SM_LDC_R4, // state 219 [ldarg.0 -> ldc.r4]
        SM_STFLD, // state 220 [ldarg.0 -> ldc.r4 -> stfld]
        SM_LDC_R8, // state 221 [ldarg.0 -> ldc.r8]
        SM_STFLD, // state 222 [ldarg.0 -> ldc.r8 -> stfld]
        SM_LDARG_1, // state 223 [ldarg.0 -> ldarg.1]
        SM_LDFLD, // state 224 [ldarg.0 -> ldarg.1 -> ldfld]
        SM_STFLD, // state 225 [ldarg.0 -> ldarg.1 -> ldfld -> stfld]
        SM_STFLD, // state 226 [ldarg.0 -> ldarg.1 -> stfld]
        SM_LDARG_2, // state 227 [ldarg.0 -> ldarg.2]
        SM_STFLD, // state 228 [ldarg.0 -> ldarg.2 -> stfld]
        SM_LDARG_3, // state 229 [ldarg.0 -> ldarg.3]
        SM_STFLD, // state 230 [ldarg.0 -> ldarg.3 -> stfld]
        SM_DUP, // state 231 [ldarg.0 -> dup]
        SM_LDFLD, // state 232 [ldarg.0 -> dup -> ldfld]
        SM_LDARG_1, // state 233 [ldarg.0 -> dup -> ldfld -> ldarg.1]
        SM_ADD, // state 234 [ldarg.0 -> dup -> ldfld -> ldarg.1 -> add]
        SM_STFLD, // state 235 [ldarg.0 -> dup -> ldfld -> ldarg.1 -> add -> stfld]
        SM_SUB, // state 236 [ldarg.0 -> dup -> ldfld -> ldarg.1 -> sub]
        SM_STFLD, // state 237 [ldarg.0 -> dup -> ldfld -> ldarg.1 -> sub -> stfld]
        SM_MUL, // state 238 [ldarg.0 -> dup -> ldfld -> ldarg.1 -> mul]
        SM_STFLD, // state 239 [ldarg.0 -> dup -> ldfld -> ldarg.1 -> mul -> stfld]
        SM_DIV, // state 240 [ldarg.0 -> dup -> ldfld -> ldarg.1 -> div]
        SM_STFLD, // state 241 [ldarg.0 -> dup -> ldfld -> ldarg.1 -> div -> stfld]
        SM_LDARG_1, // state 242 [ldarg.0 -> ldfld -> ldarg.1]
        SM_LDFLD, // state 243 [ldarg.0 -> ldfld -> ldarg.1 -> ldfld]
        SM_ADD, // state 244 [ldarg.0 -> ldfld -> ldarg.1 -> ldfld -> add]
        SM_SUB, // state 245 [ldarg.0 -> ldfld -> ldarg.1 -> ldfld -> sub]
        SM_LDARGA_S, // state 246 [ldarga.s -> ldfld -> ldarga.s]
        SM_LDFLD, // state 247 [ldarga.s -> ldfld -> ldarga.s -> ldfld]
        SM_ADD, // state 248 [ldarga.s -> ldfld -> ldarga.s -> ldfld -> add]
        SM_SUB, // state 249 [ldarga.s -> ldfld -> ldarga.s -> ldfld -> sub]
    ];

    internal static ReadOnlySpan<short> s_smStateJumpTableByteOffset => [
        0,
        0,
        0,
        372,
        168,
        170,
        172,
        0,
        0,
        0,
        0,
        378,
        378,
        378,
        378,
        0,
        182,
        0,
        0,
        184,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        252,
        268,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        276,
        256,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        218,
        0,
        0,
        0,
        0,
        0,
        0,
        218,
        220,
        0,
        432,
        0,
        0,
        0,
        414,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        228,
        0,
        230,
        0,
        232,
        0,
        238,
        236,
        0,
        0,
        240,
        0,
        242,
        0,
        248,
        460,
        318,
        256,
        0,
        258,
        0,
        260,
        0,
        262,
        0,
        268,
        336,
        0,
        0,
        274,
        342,
        0,
        0,
    ];

    private static ReadOnlySpan<byte> JumpTableCellData => [
        1, 2, // cell# 0
        1, 3, // cell# 1
        1, 4, // cell# 2
        1, 5, // cell# 3
        1, 6, // cell# 4
        1, 7, // cell# 5
        1, 8, // cell# 6
        1, 9, // cell# 7
        1, 10, // cell# 8
        1, 11, // cell# 9
        1, 12, // cell# 10
        1, 13, // cell# 11
        1, 14, // cell# 12
        1, 15, // cell# 13
        1, 16, // cell# 14
        1, 17, // cell# 15
        1, 18, // cell# 16
        1, 19, // cell# 17
        1, 20, // cell# 18
        1, 21, // cell# 19
        1, 22, // cell# 20
        1, 23, // cell# 21
        1, 24, // cell# 22
        1, 25, // cell# 23
        1, 26, // cell# 24
        1, 27, // cell# 25
        1, 28, // cell# 26
        1, 29, // cell# 27
        1, 30, // cell# 28
        1, 31, // cell# 29
        1, 32, // cell# 30
        1, 33, // cell# 31
        1, 34, // cell# 32
        1, 35, // cell# 33
        1, 36, // cell# 34
        1, 37, // cell# 35
        1, 38, // cell# 36
        1, 39, // cell# 37
        1, 40, // cell# 38
        1, 41, // cell# 39
        1, 42, // cell# 40
        1, 43, // cell# 41
        1, 44, // cell# 42
        1, 45, // cell# 43
        1, 46, // cell# 44
        1, 47, // cell# 45
        1, 48, // cell# 46
        1, 49, // cell# 47
        1, 50, // cell# 48
        1, 51, // cell# 49
        1, 52, // cell# 50
        1, 53, // cell# 51
        1, 54, // cell# 52
        1, 55, // cell# 53
        1, 56, // cell# 54
        1, 57, // cell# 55
        1, 58, // cell# 56
        1, 59, // cell# 57
        1, 60, // cell# 58
        1, 61, // cell# 59
        1, 62, // cell# 60
        1, 63, // cell# 61
        1, 64, // cell# 62
        1, 65, // cell# 63
        1, 66, // cell# 64
        1, 67, // cell# 65
        1, 68, // cell# 66
        1, 69, // cell# 67
        1, 70, // cell# 68
        1, 71, // cell# 69
        1, 72, // cell# 70
        1, 73, // cell# 71
        1, 74, // cell# 72
        1, 75, // cell# 73
        1, 76, // cell# 74
        1, 77, // cell# 75
        1, 78, // cell# 76
        1, 79, // cell# 77
        1, 80, // cell# 78
        1, 81, // cell# 79
        1, 82, // cell# 80
        1, 83, // cell# 81
        1, 84, // cell# 82
        1, 85, // cell# 83
        1, 86, // cell# 84
        1, 87, // cell# 85
        1, 88, // cell# 86
        1, 89, // cell# 87
        1, 90, // cell# 88
        1, 91, // cell# 89
        1, 92, // cell# 90
        1, 93, // cell# 91
        1, 94, // cell# 92
        1, 95, // cell# 93
        1, 96, // cell# 94
        1, 97, // cell# 95
        1, 98, // cell# 96
        1, 99, // cell# 97
        1, 100, // cell# 98
        1, 101, // cell# 99
        1, 102, // cell# 100
        1, 103, // cell# 101
        1, 104, // cell# 102
        1, 105, // cell# 103
        1, 106, // cell# 104
        1, 107, // cell# 105
        1, 108, // cell# 106
        1, 109, // cell# 107
        1, 110, // cell# 108
        1, 111, // cell# 109
        1, 112, // cell# 110
        1, 113, // cell# 111
        1, 114, // cell# 112
        1, 115, // cell# 113
        1, 116, // cell# 114
        1, 117, // cell# 115
        1, 118, // cell# 116
        1, 119, // cell# 117
        1, 120, // cell# 118
        1, 121, // cell# 119
        1, 122, // cell# 120
        1, 123, // cell# 121
        1, 124, // cell# 122
        1, 125, // cell# 123
        1, 126, // cell# 124
        1, 127, // cell# 125
        1, 128, // cell# 126
        1, 129, // cell# 127
        1, 130, // cell# 128
        1, 131, // cell# 129
        1, 132, // cell# 130
        1, 133, // cell# 131
        1, 134, // cell# 132
        1, 135, // cell# 133
        1, 136, // cell# 134
        1, 137, // cell# 135
        1, 138, // cell# 136
        1, 139, // cell# 137
        1, 140, // cell# 138
        1, 141, // cell# 139
        1, 142, // cell# 140
        1, 143, // cell# 141
        1, 144, // cell# 142
        1, 145, // cell# 143
        1, 146, // cell# 144
        1, 147, // cell# 145
        1, 148, // cell# 146
        1, 149, // cell# 147
        1, 150, // cell# 148
        1, 151, // cell# 149
        1, 152, // cell# 150
        1, 153, // cell# 151
        1, 154, // cell# 152
        1, 155, // cell# 153
        1, 156, // cell# 154
        1, 157, // cell# 155
        1, 158, // cell# 156
        1, 159, // cell# 157
        1, 160, // cell# 158
        1, 161, // cell# 159
        1, 162, // cell# 160
        1, 163, // cell# 161
        1, 164, // cell# 162
        1, 165, // cell# 163
        1, 166, // cell# 164
        1, 167, // cell# 165
        1, 168, // cell# 166
        1, 169, // cell# 167
        1, 170, // cell# 168
        1, 171, // cell# 169
        1, 172, // cell# 170
        1, 173, // cell# 171
        1, 174, // cell# 172
        1, 175, // cell# 173
        1, 176, // cell# 174
        1, 177, // cell# 175
        1, 178, // cell# 176
        1, 179, // cell# 177
        1, 180, // cell# 178
        1, 181, // cell# 179
        1, 182, // cell# 180
        1, 183, // cell# 181
        1, 184, // cell# 182
        1, 185, // cell# 183
        1, 186, // cell# 184
        1, 187, // cell# 185
        1, 188, // cell# 186
        1, 189, // cell# 187
        3, 223, // cell# 188
        3, 227, // cell# 189
        3, 229, // cell# 190
        4, 192, // cell# 191
        5, 193, // cell# 192
        6, 194, // cell# 193
        11, 199, // cell# 194
        12, 200, // cell# 195
        13, 201, // cell# 196
        14, 202, // cell# 197
        16, 195, // cell# 198
        19, 196, // cell# 199
        35, 203, // cell# 200
        35, 204, // cell# 201
        35, 205, // cell# 202
        35, 206, // cell# 203
        96, 215, // cell# 204
        96, 216, // cell# 205
        181, 190, // cell# 206
        3, 217, // cell# 207
        36, 207, // cell# 208
        36, 208, // cell# 209
        36, 209, // cell# 210
        36, 210, // cell# 211
        95, 211, // cell# 212
        95, 212, // cell# 213
        95, 213, // cell# 214
        95, 214, // cell# 215
        188, 197, // cell# 216
        189, 198, // cell# 217
        191, 242, // cell# 218
        3, 219, // cell# 219
        3, 221, // cell# 220
        195, 246, // cell# 221
        3, 231, // cell# 222
        217, 218, // cell# 223
        219, 220, // cell# 224
        221, 222, // cell# 225
        223, 224, // cell# 226
        224, 225, // cell# 227
        223, 226, // cell# 228
        227, 228, // cell# 229
        229, 230, // cell# 230
        231, 232, // cell# 231
        232, 233, // cell# 232
        233, 234, // cell# 233
        233, 236, // cell# 234
        233, 238, // cell# 235
        233, 240, // cell# 236
        234, 235, // cell# 237
        236, 237, // cell# 238
        238, 239, // cell# 239
        240, 241, // cell# 240
        242, 243, // cell# 241
        243, 244, // cell# 242
        243, 245, // cell# 243
        246, 247, // cell# 244
        247, 248, // cell# 245
        247, 249, // cell# 246
        0, 0, // cell# 247
        0, 0, // cell# 248
        0, 0, // cell# 249
        0, 0, // cell# 250
        0, 0, // cell# 251
        0, 0, // cell# 252
        0, 0, // cell# 253
        0, 0, // cell# 254
        0, 0, // cell# 255
        0, 0, // cell# 256
        0, 0, // cell# 257
        0, 0, // cell# 258
        0, 0, // cell# 259
        0, 0, // cell# 260
        0, 0, // cell# 261
        0, 0, // cell# 262
        0, 0, // cell# 263
        0, 0, // cell# 264
        0, 0, // cell# 265
        0, 0, // cell# 266
        0, 0, // cell# 267
        0, 0, // cell# 268
        0, 0, // cell# 269
        0, 0, // cell# 270
        0, 0, // cell# 271
        0, 0, // cell# 272
        0, 0, // cell# 273
        0, 0, // cell# 274
        0, 0, // cell# 275
        0, 0, // cell# 276
        0, 0, // cell# 277
        0, 0, // cell# 278
        0, 0, // cell# 279
        0, 0, // cell# 280
        0, 0, // cell# 281
        0, 0, // cell# 282
        0, 0, // cell# 283
        0, 0, // cell# 284
        0, 0, // cell# 285
        0, 0, // cell# 286
        0, 0, // cell# 287
        0, 0, // cell# 288
        0, 0, // cell# 289
        0, 0, // cell# 290
        0, 0, // cell# 291
        0, 0, // cell# 292
        3, 191, // cell# 293
        0, 0, // cell# 294
        0, 0, // cell# 295
        0, 0, // cell# 296
        0, 0, // cell# 297
        0, 0, // cell# 298
        0, 0, // cell# 299
        0, 0, // cell# 300
        0, 0, // cell# 301
        0, 0, // cell# 302
        0, 0, // cell# 303
        0, 0, // cell# 304
        0, 0, // cell# 305
        0, 0, // cell# 306
        0, 0, // cell# 307
        0, 0, // cell# 308
        0, 0, // cell# 309
        0, 0, // cell# 310
        0, 0, // cell# 311
        0, 0, // cell# 312
        0, 0, // cell# 313
        0, 0, // cell# 314
        0, 0, // cell# 315
        0, 0, // cell# 316
        0, 0, // cell# 317
        0, 0, // cell# 318
        0, 0, // cell# 319
        0, 0, // cell# 320
        0, 0, // cell# 321
        0, 0, // cell# 322
        0, 0, // cell# 323
        0, 0, // cell# 324
        0, 0, // cell# 325
        0, 0, // cell# 326
        0, 0, // cell# 327
        0, 0, // cell# 328
        0, 0, // cell# 329
        0, 0, // cell# 330
        0, 0, // cell# 331
        0, 0, // cell# 332
        0, 0, // cell# 333
        0, 0, // cell# 334
        0, 0, // cell# 335
        0, 0, // cell# 336
        0, 0, // cell# 337
        0, 0, // cell# 338
        0, 0, // cell# 339
        0, 0, // cell# 340
        0, 0, // cell# 341
        0, 0, // cell# 342
        0, 0, // cell# 343
        0, 0, // cell# 344
        0, 0, // cell# 345
        0, 0, // cell# 346
        0, 0, // cell# 347
        0, 0, // cell# 348
        0, 0, // cell# 349
        0, 0, // cell# 350
        0, 0, // cell# 351
        0, 0, // cell# 352
        0, 0, // cell# 353
        0, 0, // cell# 354
        0, 0, // cell# 355
        0, 0, // cell# 356
        0, 0, // cell# 357
        0, 0, // cell# 358
        0, 0, // cell# 359
        0, 0, // cell# 360
        0, 0, // cell# 361
        0, 0, // cell# 362
        0, 0, // cell# 363
        0, 0, // cell# 364
        0, 0, // cell# 365
        0, 0, // cell# 366
        0, 0, // cell# 367
        0, 0, // cell# 368
        0, 0, // cell# 369
        0, 0, // cell# 370
        0, 0, // cell# 371
        0, 0, // cell# 372
        0, 0, // cell# 373
        0, 0, // cell# 374
        0, 0, // cell# 375
        0, 0, // cell# 376
        0, 0, // cell# 377
        0, 0, // cell# 378
        0, 0, // cell# 379
        0, 0, // cell# 380
        0, 0, // cell# 381
        0, 0, // cell# 382
        0, 0, // cell# 383
        0, 0, // cell# 384
        0, 0, // cell# 385
        0, 0, // cell# 386
        0, 0, // cell# 387
        0, 0, // cell# 388
        0, 0, // cell# 389
        0, 0, // cell# 390
        0, 0, // cell# 391
        0, 0, // cell# 392
        0, 0, // cell# 393
        0, 0, // cell# 394
        0, 0, // cell# 395
        0, 0, // cell# 396
        0, 0, // cell# 397
        0, 0, // cell# 398
        0, 0, // cell# 399
        0, 0, // cell# 400
        0, 0, // cell# 401
        0, 0, // cell# 402
        0, 0, // cell# 403
        0, 0, // cell# 404
        0, 0, // cell# 405
        0, 0, // cell# 406
        0, 0, // cell# 407
        0, 0, // cell# 408
        0, 0, // cell# 409
        0, 0, // cell# 410
        0, 0, // cell# 411
        0, 0, // cell# 412
        0, 0, // cell# 413
        0, 0, // cell# 414
        0, 0, // cell# 415
        0, 0, // cell# 416
        0, 0, // cell# 417
    ];

    internal static ReadOnlySpan<JumpTableCell> JumpTableCells =>
        MemoryMarshal.Cast<byte, JumpTableCell>(JumpTableCellData);
}

public static partial class Globals
{

    public static ReadOnlySpan<JumpTableCell> s_smJumpTableCells => StateMachineTables.JumpTableCells;
}
