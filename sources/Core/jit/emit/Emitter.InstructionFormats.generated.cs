// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public enum insFormat : uint
    {
        IF_NONE = 0,
        IF_XARCH_LABEL = 1,
        IF_XARCH_RWR_LABEL = 2,
        IF_XARCH_SWR_LABEL = 3,
        IF_XARCH_METHOD = 4,
        IF_XARCH_METHPTR = 5,
        IF_XARCH_CNS = 6,
        IF_XARCH_RRD = 7,
        IF_XARCH_RWR = 8,
        IF_XARCH_RRW = 9,
        IF_XARCH_RRD_CNS = 10,
        IF_XARCH_RWR_CNS = 11,
        IF_XARCH_RRW_CNS = 12,
        IF_XARCH_RRW_SHF = 13,
        IF_XARCH_RRD_RRD = 14,
        IF_XARCH_RWR_RRD = 15,
        IF_XARCH_RRW_RRD = 16,
        IF_XARCH_RRW_RRW = 17,
        IF_XARCH_RRD_RRD_CNS = 18,
        IF_XARCH_RWR_RRD_CNS = 19,
        IF_XARCH_RRW_RRD_CNS = 20,
        IF_XARCH_RWR_RRD_SHF = 21,
        IF_XARCH_RRD_RRD_RRD = 22,
        IF_XARCH_RWR_RRD_RRD = 23,
        IF_XARCH_RRW_RRD_RRD = 24,
        IF_XARCH_RWR_RWR_RRD = 25,
        IF_XARCH_RWR_RRD_RRD_CNS = 26,
        IF_XARCH_RWR_RRD_RRD_RRD = 27,
        IF_XARCH_MRD = 28,
        IF_XARCH_MWR = 29,
        IF_XARCH_MRW = 30,
        IF_XARCH_MRD_CNS = 31,
        IF_XARCH_MWR_CNS = 32,
        IF_XARCH_MRW_CNS = 33,
        IF_XARCH_MRW_SHF = 34,
        IF_XARCH_MRD_RRD = 35,
        IF_XARCH_MWR_RRD = 36,
        IF_XARCH_MRW_RRD = 37,
        IF_XARCH_MRW_RRW = 38,
        IF_XARCH_MRD_RRD_CNS = 39,
        IF_XARCH_MWR_RRD_CNS = 40,
        IF_XARCH_MRW_RRD_CNS = 41,
        IF_XARCH_MWR_RRD_RRD = 42,
        IF_XARCH_RRD_MRD = 43,
        IF_XARCH_RWR_MRD = 44,
        IF_XARCH_RRW_MRD = 45,
        IF_XARCH_RRD_MRD_CNS = 46,
        IF_XARCH_RWR_MRD_CNS = 47,
        IF_XARCH_RRW_MRD_CNS = 48,
        IF_XARCH_RRD_MRD_RRD = 49,
        IF_XARCH_RWR_MRD_RRD = 50,
        IF_XARCH_RRW_MRD_RRD = 51,
        IF_XARCH_RRD_RRD_MRD = 52,
        IF_XARCH_RWR_RRD_MRD = 53,
        IF_XARCH_RRW_RRD_MRD = 54,
        IF_XARCH_RWR_RWR_MRD = 55,
        IF_XARCH_RWR_RRD_MRD_CNS = 56,
        IF_XARCH_RWR_RRD_MRD_RRD = 57,
        IF_XARCH_MRD_OFF = 58,
        IF_XARCH_RWR_MRD_OFF = 59,
        IF_XARCH_SRD = 60,
        IF_XARCH_SWR = 61,
        IF_XARCH_SRW = 62,
        IF_XARCH_SRD_CNS = 63,
        IF_XARCH_SWR_CNS = 64,
        IF_XARCH_SRW_CNS = 65,
        IF_XARCH_SRW_SHF = 66,
        IF_XARCH_SRD_RRD = 67,
        IF_XARCH_SWR_RRD = 68,
        IF_XARCH_SRW_RRD = 69,
        IF_XARCH_SRW_RRW = 70,
        IF_XARCH_SRD_RRD_CNS = 71,
        IF_XARCH_SWR_RRD_CNS = 72,
        IF_XARCH_SRW_RRD_CNS = 73,
        IF_XARCH_SWR_RRD_RRD = 74,
        IF_XARCH_RRD_SRD = 75,
        IF_XARCH_RWR_SRD = 76,
        IF_XARCH_RRW_SRD = 77,
        IF_XARCH_RRD_SRD_CNS = 78,
        IF_XARCH_RWR_SRD_CNS = 79,
        IF_XARCH_RRW_SRD_CNS = 80,
        IF_XARCH_RRD_SRD_RRD = 81,
        IF_XARCH_RWR_SRD_RRD = 82,
        IF_XARCH_RRW_SRD_RRD = 83,
        IF_XARCH_RRD_RRD_SRD = 84,
        IF_XARCH_RWR_RRD_SRD = 85,
        IF_XARCH_RRW_RRD_SRD = 86,
        IF_XARCH_RWR_RWR_SRD = 87,
        IF_XARCH_RWR_RRD_SRD_CNS = 88,
        IF_XARCH_RWR_RRD_SRD_RRD = 89,
        IF_XARCH_ARD = 90,
        IF_XARCH_AWR = 91,
        IF_XARCH_ARW = 92,
        IF_XARCH_ARD_CNS = 93,
        IF_XARCH_AWR_CNS = 94,
        IF_XARCH_ARW_CNS = 95,
        IF_XARCH_ARW_SHF = 96,
        IF_XARCH_ARD_RRD = 97,
        IF_XARCH_AWR_RRD = 98,
        IF_XARCH_ARW_RRD = 99,
        IF_XARCH_ARW_RRW = 100,
        IF_XARCH_ARD_RRD_CNS = 101,
        IF_XARCH_AWR_RRD_CNS = 102,
        IF_XARCH_ARW_RRD_CNS = 103,
        IF_XARCH_AWR_RRD_RRD = 104,
        IF_XARCH_RRD_ARD = 105,
        IF_XARCH_RWR_ARD = 106,
        IF_XARCH_RRW_ARD = 107,
        IF_XARCH_RRD_ARD_CNS = 108,
        IF_XARCH_RWR_ARD_CNS = 109,
        IF_XARCH_RRW_ARD_CNS = 110,
        IF_XARCH_RRD_ARD_RRD = 111,
        IF_XARCH_RWR_ARD_RRD = 112,
        IF_XARCH_RRW_ARD_RRD = 113,
        IF_XARCH_RRD_RRD_ARD = 114,
        IF_XARCH_RWR_RRD_ARD = 115,
        IF_XARCH_RRW_RRD_ARD = 116,
        IF_XARCH_RWR_RWR_ARD = 117,
        IF_XARCH_RWR_RRD_ARD_CNS = 118,
        IF_XARCH_RWR_RRD_ARD_RRD = 119,
        IF_XARCH_COUNT = 120,
        IF_ARM32_LABEL = 1,
        IF_ARM32_LARGEJMP = 2,
        IF_ARM32_EN9 = 3,
        IF_ARM32_EN8 = 4,
        IF_ARM32_EN6A = 5,
        IF_ARM32_EN6B = 6,
        IF_ARM32_EN5A = 7,
        IF_ARM32_EN5B = 8,
        IF_ARM32_EN4A = 9,
        IF_ARM32_EN4B = 10,
        IF_ARM32_EN4C = 11,
        IF_ARM32_EN3A = 12,
        IF_ARM32_EN3B = 13,
        IF_ARM32_EN3C = 14,
        IF_ARM32_EN3D = 15,
        IF_ARM32_EN3E = 16,
        IF_ARM32_EN2A = 17,
        IF_ARM32_EN2B = 18,
        IF_ARM32_EN2C = 19,
        IF_ARM32_EN2D = 20,
        IF_ARM32_EN2E = 21,
        IF_ARM32_EN2F = 22,
        IF_ARM32_EN2G = 23,
        IF_ARM32_T1_A = 24,
        IF_ARM32_T1_B = 25,
        IF_ARM32_T1_C = 26,
        IF_ARM32_T1_D0 = 27,
        IF_ARM32_T1_D1 = 28,
        IF_ARM32_T1_D2 = 29,
        IF_ARM32_T1_E = 30,
        IF_ARM32_T1_F = 31,
        IF_ARM32_T1_G = 32,
        IF_ARM32_T1_H = 33,
        IF_ARM32_T1_I = 34,
        IF_ARM32_T1_J0 = 35,
        IF_ARM32_T1_J1 = 36,
        IF_ARM32_T1_J2 = 37,
        IF_ARM32_T1_J3 = 38,
        IF_ARM32_T1_K = 39,
        IF_ARM32_T1_L0 = 40,
        IF_ARM32_T1_L1 = 41,
        IF_ARM32_T1_M = 42,
        IF_ARM32_T2_A = 43,
        IF_ARM32_T2_B = 44,
        IF_ARM32_T2_C0 = 45,
        IF_ARM32_T2_C1 = 46,
        IF_ARM32_T2_C2 = 47,
        IF_ARM32_T2_C3 = 48,
        IF_ARM32_T2_C4 = 49,
        IF_ARM32_T2_C5 = 50,
        IF_ARM32_T2_C6 = 51,
        IF_ARM32_T2_C7 = 52,
        IF_ARM32_T2_C8 = 53,
        IF_ARM32_T2_C9 = 54,
        IF_ARM32_T2_C10 = 55,
        IF_ARM32_T2_D0 = 56,
        IF_ARM32_T2_D1 = 57,
        IF_ARM32_T2_E0 = 58,
        IF_ARM32_T2_E1 = 59,
        IF_ARM32_T2_E2 = 60,
        IF_ARM32_T2_F1 = 61,
        IF_ARM32_T2_F2 = 62,
        IF_ARM32_T2_G0 = 63,
        IF_ARM32_T2_G1 = 64,
        IF_ARM32_T2_H0 = 65,
        IF_ARM32_T2_H1 = 66,
        IF_ARM32_T2_H2 = 67,
        IF_ARM32_T2_I0 = 68,
        IF_ARM32_T2_I1 = 69,
        IF_ARM32_T2_J1 = 70,
        IF_ARM32_T2_J2 = 71,
        IF_ARM32_T2_J3 = 72,
        IF_ARM32_T2_K1 = 73,
        IF_ARM32_T2_K2 = 74,
        IF_ARM32_T2_K3 = 75,
        IF_ARM32_T2_K4 = 76,
        IF_ARM32_T2_L0 = 77,
        IF_ARM32_T2_L1 = 78,
        IF_ARM32_T2_L2 = 79,
        IF_ARM32_T2_M0 = 80,
        IF_ARM32_T2_M1 = 81,
        IF_ARM32_T2_N = 82,
        IF_ARM32_T2_N1 = 83,
        IF_ARM32_T2_N2 = 84,
        IF_ARM32_T2_N3 = 85,
        IF_ARM32_T2_VLDST = 86,
        IF_ARM32_T2_VFP2 = 87,
        IF_ARM32_T2_VFP3 = 88,
        IF_ARM32_T2_VMOVS = 89,
        IF_ARM32_T2_VMOVD = 90,
        IF_ARM32_INVALID = 91,
        IF_ARM32_ARM_INVALID = 92,
        IF_ARM32_COUNT = 93,
        IF_ARM64_LABEL = 1,
        IF_ARM64_LARGEJMP = 2,
        IF_ARM64_LARGEADR = 3,
        IF_ARM64_LARGELDC = 4,
        IF_ARM64_EN9 = 5,
        IF_ARM64_EN6A = 6,
        IF_ARM64_EN6B = 7,
        IF_ARM64_EN5A = 8,
        IF_ARM64_EN5B = 9,
        IF_ARM64_EN5C = 10,
        IF_ARM64_EN4A = 11,
        IF_ARM64_EN4B = 12,
        IF_ARM64_EN4C = 13,
        IF_ARM64_EN4D = 14,
        IF_ARM64_EN4E = 15,
        IF_ARM64_EN4F = 16,
        IF_ARM64_EN4G = 17,
        IF_ARM64_EN4H = 18,
        IF_ARM64_EN4I = 19,
        IF_ARM64_EN4J = 20,
        IF_ARM64_EN4K = 21,
        IF_ARM64_EN3A = 22,
        IF_ARM64_EN3B = 23,
        IF_ARM64_EN3C = 24,
        IF_ARM64_EN3D = 25,
        IF_ARM64_EN3E = 26,
        IF_ARM64_EN3F = 27,
        IF_ARM64_EN3G = 28,
        IF_ARM64_EN3H = 29,
        IF_ARM64_EN3I = 30,
        IF_ARM64_EN3J = 31,
        IF_ARM64_EN2A = 32,
        IF_ARM64_EN2B = 33,
        IF_ARM64_EN2C = 34,
        IF_ARM64_EN2D = 35,
        IF_ARM64_EN2E = 36,
        IF_ARM64_EN2F = 37,
        IF_ARM64_EN2G = 38,
        IF_ARM64_EN2H = 39,
        IF_ARM64_EN2I = 40,
        IF_ARM64_EN2J = 41,
        IF_ARM64_EN2K = 42,
        IF_ARM64_EN2L = 43,
        IF_ARM64_EN2M = 44,
        IF_ARM64_EN2N = 45,
        IF_ARM64_EN2O = 46,
        IF_ARM64_EN2P = 47,
        IF_ARM64_EN2Q = 48,
        IF_ARM64_BI_0A = 49,
        IF_ARM64_BI_0B = 50,
        IF_ARM64_BI_0C = 51,
        IF_ARM64_BI_1A = 52,
        IF_ARM64_BI_1B = 53,
        IF_ARM64_BR_0A = 54,
        IF_ARM64_BR_1A = 55,
        IF_ARM64_BR_1B = 56,
        IF_ARM64_LS_1A = 57,
        IF_ARM64_LS_2A = 58,
        IF_ARM64_LS_2B = 59,
        IF_ARM64_LS_2C = 60,
        IF_ARM64_LS_2D = 61,
        IF_ARM64_LS_2E = 62,
        IF_ARM64_LS_2F = 63,
        IF_ARM64_LS_2G = 64,
        IF_ARM64_LS_3A = 65,
        IF_ARM64_LS_3B = 66,
        IF_ARM64_LS_3C = 67,
        IF_ARM64_LS_3D = 68,
        IF_ARM64_LS_3E = 69,
        IF_ARM64_LS_3F = 70,
        IF_ARM64_LS_3G = 71,
        IF_ARM64_DI_1A = 72,
        IF_ARM64_DI_1B = 73,
        IF_ARM64_DI_1C = 74,
        IF_ARM64_DI_1D = 75,
        IF_ARM64_DI_1E = 76,
        IF_ARM64_DI_1F = 77,
        IF_ARM64_DI_2A = 78,
        IF_ARM64_DI_2B = 79,
        IF_ARM64_DI_2C = 80,
        IF_ARM64_DI_2D = 81,
        IF_ARM64_DR_1D = 82,
        IF_ARM64_DR_2A = 83,
        IF_ARM64_DR_2B = 84,
        IF_ARM64_DR_2C = 85,
        IF_ARM64_DR_2D = 86,
        IF_ARM64_DR_2E = 87,
        IF_ARM64_DR_2F = 88,
        IF_ARM64_DR_2G = 89,
        IF_ARM64_DR_2H = 90,
        IF_ARM64_DR_2I = 91,
        IF_ARM64_DR_3A = 92,
        IF_ARM64_DR_3B = 93,
        IF_ARM64_DR_3C = 94,
        IF_ARM64_DR_3D = 95,
        IF_ARM64_DR_3E = 96,
        IF_ARM64_DR_4A = 97,
        IF_ARM64_DV_1A = 98,
        IF_ARM64_DV_1B = 99,
        IF_ARM64_DV_1C = 100,
        IF_ARM64_DV_2A = 101,
        IF_ARM64_DV_2B = 102,
        IF_ARM64_DV_2C = 103,
        IF_ARM64_DV_2D = 104,
        IF_ARM64_DV_2E = 105,
        IF_ARM64_DV_2F = 106,
        IF_ARM64_DV_2G = 107,
        IF_ARM64_DV_2H = 108,
        IF_ARM64_DV_2I = 109,
        IF_ARM64_DV_2J = 110,
        IF_ARM64_DV_2K = 111,
        IF_ARM64_DV_2L = 112,
        IF_ARM64_DV_2M = 113,
        IF_ARM64_DV_2N = 114,
        IF_ARM64_DV_2O = 115,
        IF_ARM64_DV_2P = 116,
        IF_ARM64_DV_2Q = 117,
        IF_ARM64_DV_2R = 118,
        IF_ARM64_DV_2S = 119,
        IF_ARM64_DV_2T = 120,
        IF_ARM64_DV_2U = 121,
        IF_ARM64_DV_2V = 122,
        IF_ARM64_DV_3A = 123,
        IF_ARM64_DV_3AI = 124,
        IF_ARM64_DV_3B = 125,
        IF_ARM64_DV_3BI = 126,
        IF_ARM64_DV_3C = 127,
        IF_ARM64_DV_3D = 128,
        IF_ARM64_DV_3DI = 129,
        IF_ARM64_DV_3E = 130,
        IF_ARM64_DV_3EI = 131,
        IF_ARM64_DV_3F = 132,
        IF_ARM64_DV_3G = 133,
        IF_ARM64_DV_3H = 134,
        IF_ARM64_DV_3I = 135,
        IF_ARM64_DV_4A = 136,
        IF_ARM64_DV_4B = 137,
        IF_ARM64_SN_0A = 138,
        IF_ARM64_SI_0A = 139,
        IF_ARM64_SI_0B = 140,
        IF_ARM64_PC_0A = 141,
        IF_ARM64_PC_1A = 142,
        IF_ARM64_PC_2A = 143,
        IF_ARM64_SR_1A = 144,
        IF_ARM64_INVALID = 145,
        IF_ARM64_SVE_13A = 146,
        IF_ARM64_SVE_11A = 147,
        IF_ARM64_SVE_9A = 148,
        IF_ARM64_SVE_9B = 149,
        IF_ARM64_SVE_9C = 150,
        IF_ARM64_SVE_9D = 151,
        IF_ARM64_SVE_9E = 152,
        IF_ARM64_SVE_9F = 153,
        IF_ARM64_SVE_8A = 154,
        IF_ARM64_SVE_8B = 155,
        IF_ARM64_SVE_8C = 156,
        IF_ARM64_SVE_7A = 157,
        IF_ARM64_SVE_6A = 158,
        IF_ARM64_SVE_6B = 159,
        IF_ARM64_SVE_6C = 160,
        IF_ARM64_SVE_6D = 161,
        IF_ARM64_SVE_6E = 162,
        IF_ARM64_SVE_6F = 163,
        IF_ARM64_SVE_6G = 164,
        IF_ARM64_SVE_5A = 165,
        IF_ARM64_SVE_5B = 166,
        IF_ARM64_SVE_5C = 167,
        IF_ARM64_SVE_5D = 168,
        IF_ARM64_SVE_5E = 169,
        IF_ARM64_SVE_4A = 170,
        IF_ARM64_SVE_4B = 171,
        IF_ARM64_SVE_4C = 172,
        IF_ARM64_SVE_4D = 173,
        IF_ARM64_SVE_4E = 174,
        IF_ARM64_SVE_4F = 175,
        IF_ARM64_SVE_4G = 176,
        IF_ARM64_SVE_4H = 177,
        IF_ARM64_SVE_4I = 178,
        IF_ARM64_SVE_4J = 179,
        IF_ARM64_SVE_4K = 180,
        IF_ARM64_SVE_4L = 181,
        IF_ARM64_SVE_3A = 182,
        IF_ARM64_SVE_3B = 183,
        IF_ARM64_SVE_3C = 184,
        IF_ARM64_SVE_3D = 185,
        IF_ARM64_SVE_3E = 186,
        IF_ARM64_SVE_3F = 187,
        IF_ARM64_SVE_3G = 188,
        IF_ARM64_SVE_3H = 189,
        IF_ARM64_SVE_3I = 190,
        IF_ARM64_SVE_3J = 191,
        IF_ARM64_SVE_3K = 192,
        IF_ARM64_SVE_3L = 193,
        IF_ARM64_SVE_3M = 194,
        IF_ARM64_SVE_3N = 195,
        IF_ARM64_SVE_3O = 196,
        IF_ARM64_SVE_3P = 197,
        IF_ARM64_SVE_3Q = 198,
        IF_ARM64_SVE_3R = 199,
        IF_ARM64_SVE_3S = 200,
        IF_ARM64_SVE_3T = 201,
        IF_ARM64_SVE_3U = 202,
        IF_ARM64_SVE_3V = 203,
        IF_ARM64_SVE_2AA = 204,
        IF_ARM64_SVE_2AB = 205,
        IF_ARM64_SVE_2AC = 206,
        IF_ARM64_SVE_2AD = 207,
        IF_ARM64_SVE_2AE = 208,
        IF_ARM64_SVE_2AF = 209,
        IF_ARM64_SVE_2AG = 210,
        IF_ARM64_SVE_2AH = 211,
        IF_ARM64_SVE_2AI = 212,
        IF_ARM64_SVE_2AJ = 213,
        IF_ARM64_SVE_2AK = 214,
        IF_ARM64_SVE_2AL = 215,
        IF_ARM64_SVE_2AM = 216,
        IF_ARM64_SVE_2AN = 217,
        IF_ARM64_SVE_2AO = 218,
        IF_ARM64_SVE_2AP = 219,
        IF_ARM64_SVE_2AQ = 220,
        IF_ARM64_SVE_2AR = 221,
        IF_ARM64_SVE_2AS = 222,
        IF_ARM64_SVE_2AT = 223,
        IF_ARM64_SVE_2AU = 224,
        IF_ARM64_SVE_2AV = 225,
        IF_ARM64_SVE_2AW = 226,
        IF_ARM64_SVE_2AX = 227,
        IF_ARM64_SVE_2AY = 228,
        IF_ARM64_SVE_2AZ = 229,
        IF_ARM64_SVE_2BA = 230,
        IF_ARM64_SVE_2BB = 231,
        IF_ARM64_SVE_2BC = 232,
        IF_ARM64_SVE_2BD = 233,
        IF_ARM64_SVE_2BE = 234,
        IF_ARM64_SVE_2BF = 235,
        IF_ARM64_SVE_2BG = 236,
        IF_ARM64_SVE_2BH = 237,
        IF_ARM64_SVE_2BI = 238,
        IF_ARM64_SVE_2BJ = 239,
        IF_ARM64_SVE_2BK = 240,
        IF_ARM64_SVE_2BL = 241,
        IF_ARM64_SVE_2BM = 242,
        IF_ARM64_SVE_2BN = 243,
        IF_ARM64_SVE_2BO = 244,
        IF_ARM64_SVE_2BP = 245,
        IF_ARM64_SVE_2BQ = 246,
        IF_ARM64_SVE_2BR = 247,
        IF_ARM64_SVE_2BS = 248,
        IF_ARM64_SVE_AA_3A = 249,
        IF_ARM64_SVE_AB_3B = 250,
        IF_ARM64_SVE_AC_3A = 251,
        IF_ARM64_SVE_AF_3A = 252,
        IF_ARM64_SVE_AG_3A = 253,
        IF_ARM64_SVE_AH_3A = 254,
        IF_ARM64_SVE_AI_3A = 255,
        IF_ARM64_SVE_AJ_3A = 256,
        IF_ARM64_SVE_AK_3A = 257,
        IF_ARM64_SVE_AL_3A = 258,
        IF_ARM64_SVE_AM_2A = 259,
        IF_ARM64_SVE_AO_3A = 260,
        IF_ARM64_SVE_AP_3A = 261,
        IF_ARM64_SVE_AQ_3A = 262,
        IF_ARM64_SVE_AR_4A = 263,
        IF_ARM64_SVE_AS_4A = 264,
        IF_ARM64_SVE_AT_3A = 265,
        IF_ARM64_SVE_AT_3B = 266,
        IF_ARM64_SVE_AU_3A = 267,
        IF_ARM64_SVE_AV_3A = 268,
        IF_ARM64_SVE_AW_2A = 269,
        IF_ARM64_SVE_AX_1A = 270,
        IF_ARM64_SVE_AY_2A = 271,
        IF_ARM64_SVE_AZ_2A = 272,
        IF_ARM64_SVE_BA_3A = 273,
        IF_ARM64_SVE_BB_2A = 274,
        IF_ARM64_SVE_BC_1A = 275,
        IF_ARM64_SVE_BD_3B = 276,
        IF_ARM64_SVE_BF_2A = 277,
        IF_ARM64_SVE_BG_3A = 278,
        IF_ARM64_SVE_BH_3A = 279,
        IF_ARM64_SVE_BH_3B = 280,
        IF_ARM64_SVE_BH_3B_A = 281,
        IF_ARM64_SVE_BI_2A = 282,
        IF_ARM64_SVE_BJ_2A = 283,
        IF_ARM64_SVE_BL_1A = 284,
        IF_ARM64_SVE_BM_1A = 285,
        IF_ARM64_SVE_BN_1A = 286,
        IF_ARM64_SVE_BO_1A = 287,
        IF_ARM64_SVE_BO_1A_A = 288,
        IF_ARM64_SVE_BP_1A = 289,
        IF_ARM64_SVE_BQ_2A = 290,
        IF_ARM64_SVE_BQ_2B = 291,
        IF_ARM64_SVE_BR_3B = 292,
        IF_ARM64_SVE_BS_1A = 293,
        IF_ARM64_SVE_BT_1A = 294,
        IF_ARM64_SVE_BU_2A = 295,
        IF_ARM64_SVE_BV_2A = 296,
        IF_ARM64_SVE_BV_2A_A = 297,
        IF_ARM64_SVE_BV_2B = 298,
        IF_ARM64_SVE_BV_2A_J = 299,
        IF_ARM64_SVE_BW_2A = 300,
        IF_ARM64_SVE_BX_2A = 301,
        IF_ARM64_SVE_BY_2A = 302,
        IF_ARM64_SVE_BZ_3A = 303,
        IF_ARM64_SVE_BZ_3A_A = 304,
        IF_ARM64_SVE_CB_2A = 305,
        IF_ARM64_SVE_CC_2A = 306,
        IF_ARM64_SVE_CD_2A = 307,
        IF_ARM64_SVE_CE_2A = 308,
        IF_ARM64_SVE_CE_2B = 309,
        IF_ARM64_SVE_CE_2C = 310,
        IF_ARM64_SVE_CE_2D = 311,
        IF_ARM64_SVE_CF_2A = 312,
        IF_ARM64_SVE_CF_2B = 313,
        IF_ARM64_SVE_CF_2C = 314,
        IF_ARM64_SVE_CF_2D = 315,
        IF_ARM64_SVE_CG_2A = 316,
        IF_ARM64_SVE_CH_2A = 317,
        IF_ARM64_SVE_CI_3A = 318,
        IF_ARM64_SVE_CJ_2A = 319,
        IF_ARM64_SVE_CK_2A = 320,
        IF_ARM64_SVE_CL_3A = 321,
        IF_ARM64_SVE_CM_3A = 322,
        IF_ARM64_SVE_CN_3A = 323,
        IF_ARM64_SVE_CO_3A = 324,
        IF_ARM64_SVE_CP_3A = 325,
        IF_ARM64_SVE_CQ_3A = 326,
        IF_ARM64_SVE_CR_3A = 327,
        IF_ARM64_SVE_CS_3A = 328,
        IF_ARM64_SVE_CT_3A = 329,
        IF_ARM64_SVE_CU_3A = 330,
        IF_ARM64_SVE_CV_3A = 331,
        IF_ARM64_SVE_CV_3B = 332,
        IF_ARM64_SVE_CW_4A = 333,
        IF_ARM64_SVE_CX_4A = 334,
        IF_ARM64_SVE_CX_4A_A = 335,
        IF_ARM64_SVE_CY_3A = 336,
        IF_ARM64_SVE_CY_3B = 337,
        IF_ARM64_SVE_CZ_4A = 338,
        IF_ARM64_SVE_CZ_4A_K = 339,
        IF_ARM64_SVE_CZ_4A_L = 340,
        IF_ARM64_SVE_CZ_4A_A = 341,
        IF_ARM64_SVE_DA_4A = 342,
        IF_ARM64_SVE_DB_3A = 343,
        IF_ARM64_SVE_DB_3B = 344,
        IF_ARM64_SVE_DC_3A = 345,
        IF_ARM64_SVE_DD_2A = 346,
        IF_ARM64_SVE_DE_1A = 347,
        IF_ARM64_SVE_DF_2A = 348,
        IF_ARM64_SVE_DG_2A = 349,
        IF_ARM64_SVE_DH_1A = 350,
        IF_ARM64_SVE_DI_2A = 351,
        IF_ARM64_SVE_DJ_1A = 352,
        IF_ARM64_SVE_DK_3A = 353,
        IF_ARM64_SVE_DL_2A = 354,
        IF_ARM64_SVE_DM_2A = 355,
        IF_ARM64_SVE_DN_2A = 356,
        IF_ARM64_SVE_DO_2A = 357,
        IF_ARM64_SVE_DO_2A_A = 358,
        IF_ARM64_SVE_DP_2A = 359,
        IF_ARM64_SVE_DQ_0A = 360,
        IF_ARM64_SVE_DR_1A = 361,
        IF_ARM64_SVE_DS_2A = 362,
        IF_ARM64_SVE_DT_3A = 363,
        IF_ARM64_SVE_DU_3A = 364,
        IF_ARM64_SVE_DV_4A = 365,
        IF_ARM64_SVE_DW_2A = 366,
        IF_ARM64_SVE_DW_2B = 367,
        IF_ARM64_SVE_DX_3A = 368,
        IF_ARM64_SVE_DY_3A = 369,
        IF_ARM64_SVE_DZ_1A = 370,
        IF_ARM64_SVE_EA_1A = 371,
        IF_ARM64_SVE_EB_1A = 372,
        IF_ARM64_SVE_EB_1B = 373,
        IF_ARM64_SVE_EC_1A = 374,
        IF_ARM64_SVE_ED_1A = 375,
        IF_ARM64_SVE_EE_1A = 376,
        IF_ARM64_SVE_EF_3A = 377,
        IF_ARM64_SVE_EG_3A = 378,
        IF_ARM64_SVE_EH_3A = 379,
        IF_ARM64_SVE_EI_3A = 380,
        IF_ARM64_SVE_EJ_3A = 381,
        IF_ARM64_SVE_EK_3A = 382,
        IF_ARM64_SVE_EL_3A = 383,
        IF_ARM64_SVE_EM_3A = 384,
        IF_ARM64_SVE_EQ_3A = 385,
        IF_ARM64_SVE_ES_3A = 386,
        IF_ARM64_SVE_EW_3A = 387,
        IF_ARM64_SVE_EW_3B = 388,
        IF_ARM64_SVE_EX_3A = 389,
        IF_ARM64_SVE_EY_3A = 390,
        IF_ARM64_SVE_EY_3B = 391,
        IF_ARM64_SVE_EZ_3A = 392,
        IF_ARM64_SVE_FA_3A = 393,
        IF_ARM64_SVE_FA_3B = 394,
        IF_ARM64_SVE_FB_3A = 395,
        IF_ARM64_SVE_FB_3B = 396,
        IF_ARM64_SVE_FC_3A = 397,
        IF_ARM64_SVE_FC_3B = 398,
        IF_ARM64_SVE_FD_3A = 399,
        IF_ARM64_SVE_FD_3B = 400,
        IF_ARM64_SVE_FD_3C = 401,
        IF_ARM64_SVE_FE_3A = 402,
        IF_ARM64_SVE_FE_3B = 403,
        IF_ARM64_SVE_FF_3A = 404,
        IF_ARM64_SVE_FF_3B = 405,
        IF_ARM64_SVE_FF_3C = 406,
        IF_ARM64_SVE_FG_3A = 407,
        IF_ARM64_SVE_FG_3B = 408,
        IF_ARM64_SVE_FH_3A = 409,
        IF_ARM64_SVE_FH_3B = 410,
        IF_ARM64_SVE_FI_3A = 411,
        IF_ARM64_SVE_FI_3B = 412,
        IF_ARM64_SVE_FI_3C = 413,
        IF_ARM64_SVE_FJ_3A = 414,
        IF_ARM64_SVE_FJ_3B = 415,
        IF_ARM64_SVE_FK_3A = 416,
        IF_ARM64_SVE_FK_3B = 417,
        IF_ARM64_SVE_FK_3C = 418,
        IF_ARM64_SVE_FL_3A = 419,
        IF_ARM64_SVE_FM_3A = 420,
        IF_ARM64_SVE_FN_3B = 421,
        IF_ARM64_SVE_FO_3A = 422,
        IF_ARM64_SVE_FR_2A = 423,
        IF_ARM64_SVE_FT_2A = 424,
        IF_ARM64_SVE_FU_2A = 425,
        IF_ARM64_SVE_FV_2A = 426,
        IF_ARM64_SVE_FW_3A = 427,
        IF_ARM64_SVE_FY_3A = 428,
        IF_ARM64_SVE_FZ_2A = 429,
        IF_ARM64_SVE_GA_2A = 430,
        IF_ARM64_SVE_GB_2A = 431,
        IF_ARM64_SVE_GC_3A = 432,
        IF_ARM64_SVE_GD_2A = 433,
        IF_ARM64_SVE_GE_4A = 434,
        IF_ARM64_SVE_GF_3A = 435,
        IF_ARM64_SVE_GG_3A = 436,
        IF_ARM64_SVE_GG_3B = 437,
        IF_ARM64_SVE_GH_3A = 438,
        IF_ARM64_SVE_GH_3B = 439,
        IF_ARM64_SVE_GH_3B_B = 440,
        IF_ARM64_SVE_GI_4A = 441,
        IF_ARM64_SVE_GJ_3A = 442,
        IF_ARM64_SVE_GK_2A = 443,
        IF_ARM64_SVE_GL_1A = 444,
        IF_ARM64_SVE_GM_3A = 445,
        IF_ARM64_SVE_GN_3A = 446,
        IF_ARM64_SVE_GO_3A = 447,
        IF_ARM64_SVE_GP_3A = 448,
        IF_ARM64_SVE_GQ_3A = 449,
        IF_ARM64_SVE_GR_3A = 450,
        IF_ARM64_SVE_GS_3A = 451,
        IF_ARM64_SVE_GT_4A = 452,
        IF_ARM64_SVE_GU_3A = 453,
        IF_ARM64_SVE_GU_3B = 454,
        IF_ARM64_SVE_GU_3C = 455,
        IF_ARM64_SVE_GV_3A = 456,
        IF_ARM64_SVE_GW_3B = 457,
        IF_ARM64_SVE_GX_3A = 458,
        IF_ARM64_SVE_GX_3B = 459,
        IF_ARM64_SVE_GX_3C = 460,
        IF_ARM64_SVE_GY_3A = 461,
        IF_ARM64_SVE_GY_3B = 462,
        IF_ARM64_SVE_GY_3B_D = 463,
        IF_ARM64_SVE_GZ_3A = 464,
        IF_ARM64_SVE_HA_3A = 465,
        IF_ARM64_SVE_HA_3A_E = 466,
        IF_ARM64_SVE_HA_3A_F = 467,
        IF_ARM64_SVE_HB_3A = 468,
        IF_ARM64_SVE_HC_3A = 469,
        IF_ARM64_SVE_HD_3A = 470,
        IF_ARM64_SVE_HD_3A_A = 471,
        IF_ARM64_SVE_HE_3A = 472,
        IF_ARM64_SVE_HF_2A = 473,
        IF_ARM64_SVE_HG_2A = 474,
        IF_ARM64_SVE_HH_2A = 475,
        IF_ARM64_SVE_HI_3A = 476,
        IF_ARM64_SVE_HJ_3A = 477,
        IF_ARM64_SVE_HK_3B = 478,
        IF_ARM64_SVE_HL_3A = 479,
        IF_ARM64_SVE_HL_3B = 480,
        IF_ARM64_SVE_HM_2A = 481,
        IF_ARM64_SVE_HN_2A = 482,
        IF_ARM64_SVE_HO_3A = 483,
        IF_ARM64_SVE_HO_3B = 484,
        IF_ARM64_SVE_HO_3C = 485,
        IF_ARM64_SVE_HP_3A = 486,
        IF_ARM64_SVE_HP_3B = 487,
        IF_ARM64_SVE_HQ_3A = 488,
        IF_ARM64_SVE_HR_3A = 489,
        IF_ARM64_SVE_HS_3A = 490,
        IF_ARM64_SVE_HT_4A = 491,
        IF_ARM64_SVE_HU_4A = 492,
        IF_ARM64_SVE_HU_4B = 493,
        IF_ARM64_SVE_HV_4A = 494,
        IF_ARM64_SVE_HW_4A = 495,
        IF_ARM64_SVE_HW_4A_A = 496,
        IF_ARM64_SVE_HW_4B = 497,
        IF_ARM64_SVE_HW_4A_B = 498,
        IF_ARM64_SVE_HW_4A_C = 499,
        IF_ARM64_SVE_HW_4B_D = 500,
        IF_ARM64_SVE_HX_3A = 501,
        IF_ARM64_SVE_HX_3A_B = 502,
        IF_ARM64_SVE_HX_3A_E = 503,
        IF_ARM64_SVE_HY_3A = 504,
        IF_ARM64_SVE_HY_3A_A = 505,
        IF_ARM64_SVE_HY_3B = 506,
        IF_ARM64_SVE_HZ_2A = 507,
        IF_ARM64_SVE_HZ_2A_B = 508,
        IF_ARM64_SVE_IA_2A = 509,
        IF_ARM64_SVE_IB_3A = 510,
        IF_ARM64_SVE_IC_3A = 511,
        IF_ARM64_SVE_IC_3A_A = 512,
        IF_ARM64_SVE_IC_3A_B = 513,
        IF_ARM64_SVE_IC_3A_C = 514,
        IF_ARM64_SVE_ID_2A = 515,
        IF_ARM64_SVE_IE_2A = 516,
        IF_ARM64_SVE_IF_4A = 517,
        IF_ARM64_SVE_IF_4A_A = 518,
        IF_ARM64_SVE_IG_4A = 519,
        IF_ARM64_SVE_IG_4A_C = 520,
        IF_ARM64_SVE_IG_4A_D = 521,
        IF_ARM64_SVE_IG_4A_E = 522,
        IF_ARM64_SVE_IG_4A_F = 523,
        IF_ARM64_SVE_IG_4A_G = 524,
        IF_ARM64_SVE_IH_3A = 525,
        IF_ARM64_SVE_IH_3A_F = 526,
        IF_ARM64_SVE_IH_3A_G = 527,
        IF_ARM64_SVE_IH_3A_A = 528,
        IF_ARM64_SVE_II_4A = 529,
        IF_ARM64_SVE_II_4A_H = 530,
        IF_ARM64_SVE_II_4A_I = 531,
        IF_ARM64_SVE_II_4A_B = 532,
        IF_ARM64_SVE_IJ_3A = 533,
        IF_ARM64_SVE_IJ_3A_C = 534,
        IF_ARM64_SVE_IJ_3A_D = 535,
        IF_ARM64_SVE_IJ_3A_E = 536,
        IF_ARM64_SVE_IJ_3A_F = 537,
        IF_ARM64_SVE_IJ_3A_G = 538,
        IF_ARM64_SVE_IK_4A = 539,
        IF_ARM64_SVE_IK_4A_F = 540,
        IF_ARM64_SVE_IK_4A_G = 541,
        IF_ARM64_SVE_IK_4A_H = 542,
        IF_ARM64_SVE_IK_4A_I = 543,
        IF_ARM64_SVE_IK_4A_E = 544,
        IF_ARM64_SVE_IL_3A = 545,
        IF_ARM64_SVE_IL_3A_A = 546,
        IF_ARM64_SVE_IL_3A_B = 547,
        IF_ARM64_SVE_IL_3A_C = 548,
        IF_ARM64_SVE_IM_3A = 549,
        IF_ARM64_SVE_IN_4A = 550,
        IF_ARM64_SVE_IO_3A = 551,
        IF_ARM64_SVE_IP_4A = 552,
        IF_ARM64_SVE_IQ_3A = 553,
        IF_ARM64_SVE_IR_4A = 554,
        IF_ARM64_SVE_IS_3A = 555,
        IF_ARM64_SVE_IT_4A = 556,
        IF_ARM64_SVE_IU_4A = 557,
        IF_ARM64_SVE_IU_4A_A = 558,
        IF_ARM64_SVE_IU_4B = 559,
        IF_ARM64_SVE_IU_4B_B = 560,
        IF_ARM64_SVE_IU_4A_C = 561,
        IF_ARM64_SVE_IU_4B_D = 562,
        IF_ARM64_SVE_IV_3A = 563,
        IF_ARM64_SVE_IW_4A = 564,
        IF_ARM64_SVE_IX_4A = 565,
        IF_ARM64_SVE_IY_4A = 566,
        IF_ARM64_SVE_IZ_4A = 567,
        IF_ARM64_SVE_IZ_4A_A = 568,
        IF_ARM64_SVE_JA_4A = 569,
        IF_ARM64_SVE_JB_4A = 570,
        IF_ARM64_SVE_JC_4A = 571,
        IF_ARM64_SVE_JD_4A = 572,
        IF_ARM64_SVE_JD_4B = 573,
        IF_ARM64_SVE_JD_4C = 574,
        IF_ARM64_SVE_JD_4C_A = 575,
        IF_ARM64_SVE_JE_3A = 576,
        IF_ARM64_SVE_JF_4A = 577,
        IF_ARM64_SVE_JG_2A = 578,
        IF_ARM64_SVE_JH_2A = 579,
        IF_ARM64_SVE_JI_3A = 580,
        IF_ARM64_SVE_JI_3A_A = 581,
        IF_ARM64_SVE_JJ_4A = 582,
        IF_ARM64_SVE_JJ_4A_B = 583,
        IF_ARM64_SVE_JJ_4A_C = 584,
        IF_ARM64_SVE_JJ_4A_D = 585,
        IF_ARM64_SVE_JJ_4B = 586,
        IF_ARM64_SVE_JJ_4B_E = 587,
        IF_ARM64_SVE_JJ_4B_C = 588,
        IF_ARM64_SVE_JK_4A = 589,
        IF_ARM64_SVE_JK_4A_B = 590,
        IF_ARM64_SVE_JK_4B = 591,
        IF_ARM64_SVE_JL_3A = 592,
        IF_ARM64_SVE_JM_3A = 593,
        IF_ARM64_SVE_JN_3A = 594,
        IF_ARM64_SVE_JN_3B = 595,
        IF_ARM64_SVE_JN_3C = 596,
        IF_ARM64_SVE_JN_3C_D = 597,
        IF_ARM64_SVE_JO_3A = 598,
        IF_ARM64_COUNT = 599,
        IF_WASM_OPCODE = 1,
        IF_WASM_BLOCK = 2,
        IF_WASM_RAW_ULEB128 = 3,
        IF_WASM_CODE_SIZE = 4,
        IF_WASM_ULEB128 = 5,
        IF_WASM_FUNCIDX = 6,
        IF_WASM_GLOBALIDX = 7,
        IF_WASM_SLEB128 = 8,
        IF_WASM_MEMADDR = 9,
        IF_WASM_FUNCPTR = 10,
        IF_WASM_FUNCLETPTR = 11,
        IF_WASM_FUNCLETIDX = 12,
        IF_WASM_FRAMEIDENTITY = 13,
        IF_WASM_DATAOFFS = 14,
        IF_WASM_F32 = 15,
        IF_WASM_F64 = 16,
        IF_WASM_MEMARG = 17,
        IF_WASM_LOCAL_DECL = 18,
        IF_WASM_CALL_INDIRECT = 19,
        IF_WASM_MEMIDX_MEMIDX = 20,
        IF_WASM_TRY_TABLE = 21,
        IF_WASM_CATCH_DECL = 22,
        IF_WASM_V128 = 23,
        IF_WASM_LANE = 24,
        IF_WASM_MEMARG_LANE = 25,
        IF_WASM_COUNT = 26,
        IF_LOONGARCH64_COUNT = 1,
        IF_RISCV64_COUNT = 1,
    }

    internal static ReadOnlySpan<byte> emitFmtToOps
    {
        get
        {
            if (Configuration.TARGET_XARCH)
            {
                return s_emitFmtToOpsXarch;
            }

            if (Configuration.TARGET_ARM)
            {
                return s_emitFmtToOpsArm32;
            }

            if (Configuration.TARGET_ARM64)
            {
                return s_emitFmtToOpsArm64;
            }

            if (Configuration.TARGET_WASM)
            {
                return s_emitFmtToOpsWasm;
            }

            if (Configuration.TARGET_LOONGARCH64)
            {
                return s_emitFmtToOpsLoongArch64;
            }

            if (Configuration.TARGET_RISCV64)
            {
                return s_emitFmtToOpsRiscV64;
            }

            FatalJitException.ThrowFatalJitException(
                CORJIT_SKIPPED,
                FatalJitErrorCode.UnsupportedInstructionFormatTarget);
            return default;
        }
    }

    internal static ReadOnlySpan<string> emitFmtNames
    {
        get
        {
            if (Configuration.TARGET_XARCH)
            {
                return EmitFmtNamesXarch.Values;
            }

            if (Configuration.TARGET_ARM)
            {
                return EmitFmtNamesArm32.Values;
            }

            if (Configuration.TARGET_ARM64)
            {
                return EmitFmtNamesArm64.Values;
            }

            if (Configuration.TARGET_WASM)
            {
                return EmitFmtNamesWasm.Values;
            }

            if (Configuration.TARGET_LOONGARCH64)
            {
                return EmitFmtNamesLoongArch64.Values;
            }

            if (Configuration.TARGET_RISCV64)
            {
                return EmitFmtNamesRiscV64.Values;
            }

            FatalJitException.ThrowFatalJitException(
                CORJIT_SKIPPED,
                FatalJitErrorCode.UnsupportedInstructionFormatTarget);
            return default;
        }
    }

    private static ReadOnlySpan<byte> s_emitFmtToOpsXarch => [
        (byte)ID_OP_NONE, // IF_NONE
        (byte)ID_OP_JMP, // IF_LABEL
        (byte)ID_OP_JMP, // IF_RWR_LABEL
        (byte)ID_OP_LBL, // IF_SWR_LABEL
        (byte)ID_OP_CALL, // IF_METHOD
        (byte)ID_OP_CALL, // IF_METHPTR
        (byte)ID_OP_SCNS, // IF_CNS
        (byte)ID_OP_NONE, // IF_RRD
        (byte)ID_OP_NONE, // IF_RWR
        (byte)ID_OP_NONE, // IF_RRW
        (byte)ID_OP_SCNS, // IF_RRD_CNS
        (byte)ID_OP_SCNS, // IF_RWR_CNS
        (byte)ID_OP_SCNS, // IF_RRW_CNS
        (byte)ID_OP_SCNS, // IF_RRW_SHF
        (byte)ID_OP_NONE, // IF_RRD_RRD
        (byte)ID_OP_NONE, // IF_RWR_RRD
        (byte)ID_OP_NONE, // IF_RRW_RRD
        (byte)ID_OP_NONE, // IF_RRW_RRW
        (byte)ID_OP_SCNS, // IF_RRD_RRD_CNS
        (byte)ID_OP_SCNS, // IF_RWR_RRD_CNS
        (byte)ID_OP_SCNS, // IF_RRW_RRD_CNS
        (byte)ID_OP_SCNS, // IF_RWR_RRD_SHF
        (byte)ID_OP_NONE, // IF_RRD_RRD_RRD
        (byte)ID_OP_NONE, // IF_RWR_RRD_RRD
        (byte)ID_OP_NONE, // IF_RRW_RRD_RRD
        (byte)ID_OP_NONE, // IF_RWR_RWR_RRD
        (byte)ID_OP_SCNS, // IF_RWR_RRD_RRD_CNS
        (byte)ID_OP_SCNS, // IF_RWR_RRD_RRD_RRD
        (byte)ID_OP_SPEC, // IF_MRD
        (byte)ID_OP_DSP, // IF_MWR
        (byte)ID_OP_DSP, // IF_MRW
        (byte)ID_OP_DSP_CNS, // IF_MRD_CNS
        (byte)ID_OP_DSP_CNS, // IF_MWR_CNS
        (byte)ID_OP_DSP_CNS, // IF_MRW_CNS
        (byte)ID_OP_DSP_CNS, // IF_MRW_SHF
        (byte)ID_OP_DSP, // IF_MRD_RRD
        (byte)ID_OP_DSP, // IF_MWR_RRD
        (byte)ID_OP_DSP, // IF_MRW_RRD
        (byte)ID_OP_DSP, // IF_MRW_RRW
        (byte)ID_OP_DSP_CNS, // IF_MRD_RRD_CNS
        (byte)ID_OP_DSP_CNS, // IF_MWR_RRD_CNS
        (byte)ID_OP_DSP_CNS, // IF_MRW_RRD_CNS
        (byte)ID_OP_DSP, // IF_MWR_RRD_RRD
        (byte)ID_OP_DSP, // IF_RRD_MRD
        (byte)ID_OP_DSP, // IF_RWR_MRD
        (byte)ID_OP_DSP, // IF_RRW_MRD
        (byte)ID_OP_DSP_CNS, // IF_RRD_MRD_CNS
        (byte)ID_OP_DSP_CNS, // IF_RWR_MRD_CNS
        (byte)ID_OP_DSP_CNS, // IF_RRW_MRD_CNS
        (byte)ID_OP_DSP, // IF_RRD_MRD_RRD
        (byte)ID_OP_DSP, // IF_RWR_MRD_RRD
        (byte)ID_OP_DSP, // IF_RRW_MRD_RRD
        (byte)ID_OP_DSP, // IF_RRD_RRD_MRD
        (byte)ID_OP_DSP, // IF_RWR_RRD_MRD
        (byte)ID_OP_DSP, // IF_RRW_RRD_MRD
        (byte)ID_OP_DSP, // IF_RWR_RWR_MRD
        (byte)ID_OP_DSP_CNS, // IF_RWR_RRD_MRD_CNS
        (byte)ID_OP_DSP_CNS, // IF_RWR_RRD_MRD_RRD
        (byte)ID_OP_DSP, // IF_MRD_OFF
        (byte)ID_OP_DSP, // IF_RWR_MRD_OFF
        (byte)ID_OP_SPEC, // IF_SRD
        (byte)ID_OP_NONE, // IF_SWR
        (byte)ID_OP_NONE, // IF_SRW
        (byte)ID_OP_CNS, // IF_SRD_CNS
        (byte)ID_OP_CNS, // IF_SWR_CNS
        (byte)ID_OP_CNS, // IF_SRW_CNS
        (byte)ID_OP_CNS, // IF_SRW_SHF
        (byte)ID_OP_NONE, // IF_SRD_RRD
        (byte)ID_OP_NONE, // IF_SWR_RRD
        (byte)ID_OP_NONE, // IF_SRW_RRD
        (byte)ID_OP_NONE, // IF_SRW_RRW
        (byte)ID_OP_CNS, // IF_SRD_RRD_CNS
        (byte)ID_OP_CNS, // IF_SWR_RRD_CNS
        (byte)ID_OP_CNS, // IF_SRW_RRD_CNS
        (byte)ID_OP_NONE, // IF_SWR_RRD_RRD
        (byte)ID_OP_NONE, // IF_RRD_SRD
        (byte)ID_OP_NONE, // IF_RWR_SRD
        (byte)ID_OP_NONE, // IF_RRW_SRD
        (byte)ID_OP_CNS, // IF_RRD_SRD_CNS
        (byte)ID_OP_CNS, // IF_RWR_SRD_CNS
        (byte)ID_OP_CNS, // IF_RRW_SRD_CNS
        (byte)ID_OP_NONE, // IF_RRD_SRD_RRD
        (byte)ID_OP_NONE, // IF_RWR_SRD_RRD
        (byte)ID_OP_NONE, // IF_RRW_SRD_RRD
        (byte)ID_OP_NONE, // IF_RRD_RRD_SRD
        (byte)ID_OP_NONE, // IF_RWR_RRD_SRD
        (byte)ID_OP_NONE, // IF_RRW_RRD_SRD
        (byte)ID_OP_NONE, // IF_RWR_RWR_SRD
        (byte)ID_OP_CNS, // IF_RWR_RRD_SRD_CNS
        (byte)ID_OP_CNS, // IF_RWR_RRD_SRD_RRD
        (byte)ID_OP_SPEC, // IF_ARD
        (byte)ID_OP_AMD, // IF_AWR
        (byte)ID_OP_AMD, // IF_ARW
        (byte)ID_OP_AMD_CNS, // IF_ARD_CNS
        (byte)ID_OP_AMD_CNS, // IF_AWR_CNS
        (byte)ID_OP_AMD_CNS, // IF_ARW_CNS
        (byte)ID_OP_AMD_CNS, // IF_ARW_SHF
        (byte)ID_OP_AMD, // IF_ARD_RRD
        (byte)ID_OP_AMD, // IF_AWR_RRD
        (byte)ID_OP_AMD, // IF_ARW_RRD
        (byte)ID_OP_AMD, // IF_ARW_RRW
        (byte)ID_OP_AMD_CNS, // IF_ARD_RRD_CNS
        (byte)ID_OP_AMD_CNS, // IF_AWR_RRD_CNS
        (byte)ID_OP_AMD_CNS, // IF_ARW_RRD_CNS
        (byte)ID_OP_AMD_CNS, // IF_AWR_RRD_RRD
        (byte)ID_OP_AMD, // IF_RRD_ARD
        (byte)ID_OP_AMD, // IF_RWR_ARD
        (byte)ID_OP_AMD, // IF_RRW_ARD
        (byte)ID_OP_AMD_CNS, // IF_RRD_ARD_CNS
        (byte)ID_OP_AMD_CNS, // IF_RWR_ARD_CNS
        (byte)ID_OP_AMD_CNS, // IF_RRW_ARD_CNS
        (byte)ID_OP_AMD, // IF_RRD_ARD_RRD
        (byte)ID_OP_AMD, // IF_RWR_ARD_RRD
        (byte)ID_OP_AMD, // IF_RRW_ARD_RRD
        (byte)ID_OP_AMD, // IF_RRD_RRD_ARD
        (byte)ID_OP_AMD, // IF_RWR_RRD_ARD
        (byte)ID_OP_AMD, // IF_RRW_RRD_ARD
        (byte)ID_OP_AMD, // IF_RWR_RWR_ARD
        (byte)ID_OP_AMD_CNS, // IF_RWR_RRD_ARD_CNS
        (byte)ID_OP_AMD_CNS, // IF_RWR_RRD_ARD_RRD
    ];

    private static ReadOnlySpan<byte> s_emitFmtToOpsArm32 => [
        (byte)ID_OP_NONE, // IF_NONE
        (byte)ID_OP_JMP, // IF_LABEL
        (byte)ID_OP_JMP, // IF_LARGEJMP
        (byte)ID_OP_NONE, // IF_EN9
        (byte)ID_OP_NONE, // IF_EN8
        (byte)ID_OP_NONE, // IF_EN6A
        (byte)ID_OP_NONE, // IF_EN6B
        (byte)ID_OP_NONE, // IF_EN5A
        (byte)ID_OP_NONE, // IF_EN5B
        (byte)ID_OP_NONE, // IF_EN4A
        (byte)ID_OP_NONE, // IF_EN4B
        (byte)ID_OP_NONE, // IF_EN4C
        (byte)ID_OP_NONE, // IF_EN3A
        (byte)ID_OP_NONE, // IF_EN3B
        (byte)ID_OP_NONE, // IF_EN3C
        (byte)ID_OP_NONE, // IF_EN3D
        (byte)ID_OP_NONE, // IF_EN3E
        (byte)ID_OP_NONE, // IF_EN2A
        (byte)ID_OP_NONE, // IF_EN2B
        (byte)ID_OP_NONE, // IF_EN2C
        (byte)ID_OP_NONE, // IF_EN2D
        (byte)ID_OP_NONE, // IF_EN2E
        (byte)ID_OP_NONE, // IF_EN2F
        (byte)ID_OP_NONE, // IF_EN2G
        (byte)ID_OP_NONE, // IF_T1_A
        (byte)ID_OP_NONE, // IF_T1_B
        (byte)ID_OP_NONE, // IF_T1_C
        (byte)ID_OP_NONE, // IF_T1_D0
        (byte)ID_OP_SPEC, // IF_T1_D1
        (byte)ID_OP_SPEC, // IF_T1_D2
        (byte)ID_OP_NONE, // IF_T1_E
        (byte)ID_OP_NONE, // IF_T1_F
        (byte)ID_OP_NONE, // IF_T1_G
        (byte)ID_OP_NONE, // IF_T1_H
        (byte)ID_OP_JMP, // IF_T1_I
        (byte)ID_OP_NONE, // IF_T1_J0
        (byte)ID_OP_NONE, // IF_T1_J1
        (byte)ID_OP_NONE, // IF_T1_J2
        (byte)ID_OP_LBL, // IF_T1_J3
        (byte)ID_OP_JMP, // IF_T1_K
        (byte)ID_OP_NONE, // IF_T1_L0
        (byte)ID_OP_NONE, // IF_T1_L1
        (byte)ID_OP_JMP, // IF_T1_M
        (byte)ID_OP_NONE, // IF_T2_A
        (byte)ID_OP_NONE, // IF_T2_B
        (byte)ID_OP_NONE, // IF_T2_C0
        (byte)ID_OP_NONE, // IF_T2_C1
        (byte)ID_OP_NONE, // IF_T2_C2
        (byte)ID_OP_NONE, // IF_T2_C3
        (byte)ID_OP_NONE, // IF_T2_C4
        (byte)ID_OP_NONE, // IF_T2_C5
        (byte)ID_OP_NONE, // IF_T2_C6
        (byte)ID_OP_NONE, // IF_T2_C7
        (byte)ID_OP_NONE, // IF_T2_C8
        (byte)ID_OP_NONE, // IF_T2_C9
        (byte)ID_OP_NONE, // IF_T2_C10
        (byte)ID_OP_NONE, // IF_T2_D0
        (byte)ID_OP_NONE, // IF_T2_D1
        (byte)ID_OP_NONE, // IF_T2_E0
        (byte)ID_OP_NONE, // IF_T2_E1
        (byte)ID_OP_NONE, // IF_T2_E2
        (byte)ID_OP_NONE, // IF_T2_F1
        (byte)ID_OP_NONE, // IF_T2_F2
        (byte)ID_OP_NONE, // IF_T2_G0
        (byte)ID_OP_NONE, // IF_T2_G1
        (byte)ID_OP_NONE, // IF_T2_H0
        (byte)ID_OP_NONE, // IF_T2_H1
        (byte)ID_OP_NONE, // IF_T2_H2
        (byte)ID_OP_NONE, // IF_T2_I0
        (byte)ID_OP_NONE, // IF_T2_I1
        (byte)ID_OP_JMP, // IF_T2_J1
        (byte)ID_OP_JMP, // IF_T2_J2
        (byte)ID_OP_CALL, // IF_T2_J3
        (byte)ID_OP_NONE, // IF_T2_K1
        (byte)ID_OP_NONE, // IF_T2_K2
        (byte)ID_OP_NONE, // IF_T2_K3
        (byte)ID_OP_NONE, // IF_T2_K4
        (byte)ID_OP_NONE, // IF_T2_L0
        (byte)ID_OP_NONE, // IF_T2_L1
        (byte)ID_OP_NONE, // IF_T2_L2
        (byte)ID_OP_NONE, // IF_T2_M0
        (byte)ID_OP_LBL, // IF_T2_M1
        (byte)ID_OP_NONE, // IF_T2_N
        (byte)ID_OP_JMP, // IF_T2_N1
        (byte)ID_OP_NONE, // IF_T2_N2
        (byte)ID_OP_NONE, // IF_T2_N3
        (byte)ID_OP_NONE, // IF_T2_VLDST
        (byte)ID_OP_NONE, // IF_T2_VFP2
        (byte)ID_OP_NONE, // IF_T2_VFP3
        (byte)ID_OP_NONE, // IF_T2_VMOVS
        (byte)ID_OP_NONE, // IF_T2_VMOVD
        (byte)ID_OP_NONE, // IF_INVALID
        (byte)ID_OP_NONE, // IF_ARM_INVALID
    ];

    private static ReadOnlySpan<byte> s_emitFmtToOpsArm64 => [
        (byte)ID_OP_NONE, // IF_NONE
        (byte)ID_OP_JMP, // IF_LABEL
        (byte)ID_OP_JMP, // IF_LARGEJMP
        (byte)ID_OP_JMP, // IF_LARGEADR
        (byte)ID_OP_JMP, // IF_LARGELDC
        (byte)ID_OP_NONE, // IF_EN9
        (byte)ID_OP_NONE, // IF_EN6A
        (byte)ID_OP_NONE, // IF_EN6B
        (byte)ID_OP_NONE, // IF_EN5A
        (byte)ID_OP_NONE, // IF_EN5B
        (byte)ID_OP_NONE, // IF_EN5C
        (byte)ID_OP_NONE, // IF_EN4A
        (byte)ID_OP_NONE, // IF_EN4B
        (byte)ID_OP_NONE, // IF_EN4C
        (byte)ID_OP_NONE, // IF_EN4D
        (byte)ID_OP_NONE, // IF_EN4E
        (byte)ID_OP_NONE, // IF_EN4F
        (byte)ID_OP_NONE, // IF_EN4G
        (byte)ID_OP_NONE, // IF_EN4H
        (byte)ID_OP_NONE, // IF_EN4I
        (byte)ID_OP_NONE, // IF_EN4J
        (byte)ID_OP_NONE, // IF_EN4K
        (byte)ID_OP_NONE, // IF_EN3A
        (byte)ID_OP_NONE, // IF_EN3B
        (byte)ID_OP_NONE, // IF_EN3C
        (byte)ID_OP_NONE, // IF_EN3D
        (byte)ID_OP_NONE, // IF_EN3E
        (byte)ID_OP_NONE, // IF_EN3F
        (byte)ID_OP_NONE, // IF_EN3G
        (byte)ID_OP_NONE, // IF_EN3H
        (byte)ID_OP_NONE, // IF_EN3I
        (byte)ID_OP_NONE, // IF_EN3J
        (byte)ID_OP_NONE, // IF_EN2A
        (byte)ID_OP_NONE, // IF_EN2B
        (byte)ID_OP_NONE, // IF_EN2C
        (byte)ID_OP_NONE, // IF_EN2D
        (byte)ID_OP_NONE, // IF_EN2E
        (byte)ID_OP_NONE, // IF_EN2F
        (byte)ID_OP_NONE, // IF_EN2G
        (byte)ID_OP_NONE, // IF_EN2H
        (byte)ID_OP_NONE, // IF_EN2I
        (byte)ID_OP_NONE, // IF_EN2J
        (byte)ID_OP_NONE, // IF_EN2K
        (byte)ID_OP_NONE, // IF_EN2L
        (byte)ID_OP_NONE, // IF_EN2M
        (byte)ID_OP_NONE, // IF_EN2N
        (byte)ID_OP_NONE, // IF_EN2O
        (byte)ID_OP_NONE, // IF_EN2P
        (byte)ID_OP_NONE, // IF_EN2Q
        (byte)ID_OP_JMP, // IF_BI_0A
        (byte)ID_OP_JMP, // IF_BI_0B
        (byte)ID_OP_CALL, // IF_BI_0C
        (byte)ID_OP_JMP, // IF_BI_1A
        (byte)ID_OP_JMP, // IF_BI_1B
        (byte)ID_OP_CALL, // IF_BR_0A
        (byte)ID_OP_CALL, // IF_BR_1A
        (byte)ID_OP_CALL, // IF_BR_1B
        (byte)ID_OP_JMP, // IF_LS_1A
        (byte)ID_OP_NONE, // IF_LS_2A
        (byte)ID_OP_NONE, // IF_LS_2B
        (byte)ID_OP_NONE, // IF_LS_2C
        (byte)ID_OP_NONE, // IF_LS_2D
        (byte)ID_OP_NONE, // IF_LS_2E
        (byte)ID_OP_NONE, // IF_LS_2F
        (byte)ID_OP_NONE, // IF_LS_2G
        (byte)ID_OP_NONE, // IF_LS_3A
        (byte)ID_OP_NONE, // IF_LS_3B
        (byte)ID_OP_NONE, // IF_LS_3C
        (byte)ID_OP_NONE, // IF_LS_3D
        (byte)ID_OP_NONE, // IF_LS_3E
        (byte)ID_OP_NONE, // IF_LS_3F
        (byte)ID_OP_NONE, // IF_LS_3G
        (byte)ID_OP_NONE, // IF_DI_1A
        (byte)ID_OP_NONE, // IF_DI_1B
        (byte)ID_OP_NONE, // IF_DI_1C
        (byte)ID_OP_NONE, // IF_DI_1D
        (byte)ID_OP_JMP, // IF_DI_1E
        (byte)ID_OP_NONE, // IF_DI_1F
        (byte)ID_OP_NONE, // IF_DI_2A
        (byte)ID_OP_NONE, // IF_DI_2B
        (byte)ID_OP_NONE, // IF_DI_2C
        (byte)ID_OP_NONE, // IF_DI_2D
        (byte)ID_OP_NONE, // IF_DR_1D
        (byte)ID_OP_NONE, // IF_DR_2A
        (byte)ID_OP_NONE, // IF_DR_2B
        (byte)ID_OP_NONE, // IF_DR_2C
        (byte)ID_OP_NONE, // IF_DR_2D
        (byte)ID_OP_NONE, // IF_DR_2E
        (byte)ID_OP_NONE, // IF_DR_2F
        (byte)ID_OP_NONE, // IF_DR_2G
        (byte)ID_OP_NONE, // IF_DR_2H
        (byte)ID_OP_NONE, // IF_DR_2I
        (byte)ID_OP_NONE, // IF_DR_3A
        (byte)ID_OP_NONE, // IF_DR_3B
        (byte)ID_OP_NONE, // IF_DR_3C
        (byte)ID_OP_NONE, // IF_DR_3D
        (byte)ID_OP_NONE, // IF_DR_3E
        (byte)ID_OP_NONE, // IF_DR_4A
        (byte)ID_OP_NONE, // IF_DV_1A
        (byte)ID_OP_NONE, // IF_DV_1B
        (byte)ID_OP_NONE, // IF_DV_1C
        (byte)ID_OP_NONE, // IF_DV_2A
        (byte)ID_OP_NONE, // IF_DV_2B
        (byte)ID_OP_NONE, // IF_DV_2C
        (byte)ID_OP_NONE, // IF_DV_2D
        (byte)ID_OP_NONE, // IF_DV_2E
        (byte)ID_OP_NONE, // IF_DV_2F
        (byte)ID_OP_NONE, // IF_DV_2G
        (byte)ID_OP_NONE, // IF_DV_2H
        (byte)ID_OP_NONE, // IF_DV_2I
        (byte)ID_OP_NONE, // IF_DV_2J
        (byte)ID_OP_NONE, // IF_DV_2K
        (byte)ID_OP_NONE, // IF_DV_2L
        (byte)ID_OP_NONE, // IF_DV_2M
        (byte)ID_OP_NONE, // IF_DV_2N
        (byte)ID_OP_NONE, // IF_DV_2O
        (byte)ID_OP_NONE, // IF_DV_2P
        (byte)ID_OP_NONE, // IF_DV_2Q
        (byte)ID_OP_NONE, // IF_DV_2R
        (byte)ID_OP_NONE, // IF_DV_2S
        (byte)ID_OP_NONE, // IF_DV_2T
        (byte)ID_OP_NONE, // IF_DV_2U
        (byte)ID_OP_NONE, // IF_DV_2V
        (byte)ID_OP_NONE, // IF_DV_3A
        (byte)ID_OP_NONE, // IF_DV_3AI
        (byte)ID_OP_NONE, // IF_DV_3B
        (byte)ID_OP_NONE, // IF_DV_3BI
        (byte)ID_OP_NONE, // IF_DV_3C
        (byte)ID_OP_NONE, // IF_DV_3D
        (byte)ID_OP_NONE, // IF_DV_3DI
        (byte)ID_OP_NONE, // IF_DV_3E
        (byte)ID_OP_NONE, // IF_DV_3EI
        (byte)ID_OP_NONE, // IF_DV_3F
        (byte)ID_OP_NONE, // IF_DV_3G
        (byte)ID_OP_NONE, // IF_DV_3H
        (byte)ID_OP_NONE, // IF_DV_3I
        (byte)ID_OP_NONE, // IF_DV_4A
        (byte)ID_OP_NONE, // IF_DV_4B
        (byte)ID_OP_NONE, // IF_SN_0A
        (byte)ID_OP_NONE, // IF_SI_0A
        (byte)ID_OP_NONE, // IF_SI_0B
        (byte)ID_OP_NONE, // IF_PC_0A
        (byte)ID_OP_NONE, // IF_PC_1A
        (byte)ID_OP_NONE, // IF_PC_2A
        (byte)ID_OP_NONE, // IF_SR_1A
        (byte)ID_OP_NONE, // IF_INVALID
        (byte)ID_OP_NONE, // IF_SVE_13A
        (byte)ID_OP_NONE, // IF_SVE_11A
        (byte)ID_OP_NONE, // IF_SVE_9A
        (byte)ID_OP_NONE, // IF_SVE_9B
        (byte)ID_OP_NONE, // IF_SVE_9C
        (byte)ID_OP_NONE, // IF_SVE_9D
        (byte)ID_OP_NONE, // IF_SVE_9E
        (byte)ID_OP_NONE, // IF_SVE_9F
        (byte)ID_OP_NONE, // IF_SVE_8A
        (byte)ID_OP_NONE, // IF_SVE_8B
        (byte)ID_OP_NONE, // IF_SVE_8C
        (byte)ID_OP_NONE, // IF_SVE_7A
        (byte)ID_OP_NONE, // IF_SVE_6A
        (byte)ID_OP_NONE, // IF_SVE_6B
        (byte)ID_OP_NONE, // IF_SVE_6C
        (byte)ID_OP_NONE, // IF_SVE_6D
        (byte)ID_OP_NONE, // IF_SVE_6E
        (byte)ID_OP_NONE, // IF_SVE_6F
        (byte)ID_OP_NONE, // IF_SVE_6G
        (byte)ID_OP_NONE, // IF_SVE_5A
        (byte)ID_OP_NONE, // IF_SVE_5B
        (byte)ID_OP_NONE, // IF_SVE_5C
        (byte)ID_OP_NONE, // IF_SVE_5D
        (byte)ID_OP_NONE, // IF_SVE_5E
        (byte)ID_OP_NONE, // IF_SVE_4A
        (byte)ID_OP_NONE, // IF_SVE_4B
        (byte)ID_OP_NONE, // IF_SVE_4C
        (byte)ID_OP_NONE, // IF_SVE_4D
        (byte)ID_OP_NONE, // IF_SVE_4E
        (byte)ID_OP_NONE, // IF_SVE_4F
        (byte)ID_OP_NONE, // IF_SVE_4G
        (byte)ID_OP_NONE, // IF_SVE_4H
        (byte)ID_OP_NONE, // IF_SVE_4I
        (byte)ID_OP_NONE, // IF_SVE_4J
        (byte)ID_OP_NONE, // IF_SVE_4K
        (byte)ID_OP_NONE, // IF_SVE_4L
        (byte)ID_OP_NONE, // IF_SVE_3A
        (byte)ID_OP_NONE, // IF_SVE_3B
        (byte)ID_OP_NONE, // IF_SVE_3C
        (byte)ID_OP_NONE, // IF_SVE_3D
        (byte)ID_OP_NONE, // IF_SVE_3E
        (byte)ID_OP_NONE, // IF_SVE_3F
        (byte)ID_OP_NONE, // IF_SVE_3G
        (byte)ID_OP_NONE, // IF_SVE_3H
        (byte)ID_OP_NONE, // IF_SVE_3I
        (byte)ID_OP_NONE, // IF_SVE_3J
        (byte)ID_OP_NONE, // IF_SVE_3K
        (byte)ID_OP_NONE, // IF_SVE_3L
        (byte)ID_OP_NONE, // IF_SVE_3M
        (byte)ID_OP_NONE, // IF_SVE_3N
        (byte)ID_OP_NONE, // IF_SVE_3O
        (byte)ID_OP_NONE, // IF_SVE_3P
        (byte)ID_OP_NONE, // IF_SVE_3Q
        (byte)ID_OP_NONE, // IF_SVE_3R
        (byte)ID_OP_NONE, // IF_SVE_3S
        (byte)ID_OP_NONE, // IF_SVE_3T
        (byte)ID_OP_NONE, // IF_SVE_3U
        (byte)ID_OP_NONE, // IF_SVE_3V
        (byte)ID_OP_NONE, // IF_SVE_2AA
        (byte)ID_OP_NONE, // IF_SVE_2AB
        (byte)ID_OP_NONE, // IF_SVE_2AC
        (byte)ID_OP_NONE, // IF_SVE_2AD
        (byte)ID_OP_NONE, // IF_SVE_2AE
        (byte)ID_OP_NONE, // IF_SVE_2AF
        (byte)ID_OP_NONE, // IF_SVE_2AG
        (byte)ID_OP_NONE, // IF_SVE_2AH
        (byte)ID_OP_NONE, // IF_SVE_2AI
        (byte)ID_OP_NONE, // IF_SVE_2AJ
        (byte)ID_OP_NONE, // IF_SVE_2AK
        (byte)ID_OP_NONE, // IF_SVE_2AL
        (byte)ID_OP_NONE, // IF_SVE_2AM
        (byte)ID_OP_NONE, // IF_SVE_2AN
        (byte)ID_OP_NONE, // IF_SVE_2AO
        (byte)ID_OP_NONE, // IF_SVE_2AP
        (byte)ID_OP_NONE, // IF_SVE_2AQ
        (byte)ID_OP_NONE, // IF_SVE_2AR
        (byte)ID_OP_NONE, // IF_SVE_2AS
        (byte)ID_OP_NONE, // IF_SVE_2AT
        (byte)ID_OP_NONE, // IF_SVE_2AU
        (byte)ID_OP_NONE, // IF_SVE_2AV
        (byte)ID_OP_NONE, // IF_SVE_2AW
        (byte)ID_OP_NONE, // IF_SVE_2AX
        (byte)ID_OP_NONE, // IF_SVE_2AY
        (byte)ID_OP_NONE, // IF_SVE_2AZ
        (byte)ID_OP_NONE, // IF_SVE_2BA
        (byte)ID_OP_NONE, // IF_SVE_2BB
        (byte)ID_OP_NONE, // IF_SVE_2BC
        (byte)ID_OP_NONE, // IF_SVE_2BD
        (byte)ID_OP_NONE, // IF_SVE_2BE
        (byte)ID_OP_NONE, // IF_SVE_2BF
        (byte)ID_OP_NONE, // IF_SVE_2BG
        (byte)ID_OP_NONE, // IF_SVE_2BH
        (byte)ID_OP_NONE, // IF_SVE_2BI
        (byte)ID_OP_NONE, // IF_SVE_2BJ
        (byte)ID_OP_NONE, // IF_SVE_2BK
        (byte)ID_OP_NONE, // IF_SVE_2BL
        (byte)ID_OP_NONE, // IF_SVE_2BM
        (byte)ID_OP_NONE, // IF_SVE_2BN
        (byte)ID_OP_NONE, // IF_SVE_2BO
        (byte)ID_OP_NONE, // IF_SVE_2BP
        (byte)ID_OP_NONE, // IF_SVE_2BQ
        (byte)ID_OP_NONE, // IF_SVE_2BR
        (byte)ID_OP_NONE, // IF_SVE_2BS
        (byte)ID_OP_NONE, // IF_SVE_AA_3A
        (byte)ID_OP_NONE, // IF_SVE_AB_3B
        (byte)ID_OP_NONE, // IF_SVE_AC_3A
        (byte)ID_OP_NONE, // IF_SVE_AF_3A
        (byte)ID_OP_NONE, // IF_SVE_AG_3A
        (byte)ID_OP_NONE, // IF_SVE_AH_3A
        (byte)ID_OP_NONE, // IF_SVE_AI_3A
        (byte)ID_OP_NONE, // IF_SVE_AJ_3A
        (byte)ID_OP_NONE, // IF_SVE_AK_3A
        (byte)ID_OP_NONE, // IF_SVE_AL_3A
        (byte)ID_OP_NONE, // IF_SVE_AM_2A
        (byte)ID_OP_NONE, // IF_SVE_AO_3A
        (byte)ID_OP_NONE, // IF_SVE_AP_3A
        (byte)ID_OP_NONE, // IF_SVE_AQ_3A
        (byte)ID_OP_NONE, // IF_SVE_AR_4A
        (byte)ID_OP_NONE, // IF_SVE_AS_4A
        (byte)ID_OP_NONE, // IF_SVE_AT_3A
        (byte)ID_OP_NONE, // IF_SVE_AT_3B
        (byte)ID_OP_NONE, // IF_SVE_AU_3A
        (byte)ID_OP_NONE, // IF_SVE_AV_3A
        (byte)ID_OP_NONE, // IF_SVE_AW_2A
        (byte)ID_OP_NONE, // IF_SVE_AX_1A
        (byte)ID_OP_NONE, // IF_SVE_AY_2A
        (byte)ID_OP_NONE, // IF_SVE_AZ_2A
        (byte)ID_OP_NONE, // IF_SVE_BA_3A
        (byte)ID_OP_NONE, // IF_SVE_BB_2A
        (byte)ID_OP_NONE, // IF_SVE_BC_1A
        (byte)ID_OP_NONE, // IF_SVE_BD_3B
        (byte)ID_OP_NONE, // IF_SVE_BF_2A
        (byte)ID_OP_NONE, // IF_SVE_BG_3A
        (byte)ID_OP_NONE, // IF_SVE_BH_3A
        (byte)ID_OP_NONE, // IF_SVE_BH_3B
        (byte)ID_OP_NONE, // IF_SVE_BH_3B_A
        (byte)ID_OP_NONE, // IF_SVE_BI_2A
        (byte)ID_OP_NONE, // IF_SVE_BJ_2A
        (byte)ID_OP_NONE, // IF_SVE_BL_1A
        (byte)ID_OP_NONE, // IF_SVE_BM_1A
        (byte)ID_OP_NONE, // IF_SVE_BN_1A
        (byte)ID_OP_NONE, // IF_SVE_BO_1A
        (byte)ID_OP_NONE, // IF_SVE_BO_1A_A
        (byte)ID_OP_NONE, // IF_SVE_BP_1A
        (byte)ID_OP_NONE, // IF_SVE_BQ_2A
        (byte)ID_OP_NONE, // IF_SVE_BQ_2B
        (byte)ID_OP_NONE, // IF_SVE_BR_3B
        (byte)ID_OP_NONE, // IF_SVE_BS_1A
        (byte)ID_OP_NONE, // IF_SVE_BT_1A
        (byte)ID_OP_NONE, // IF_SVE_BU_2A
        (byte)ID_OP_NONE, // IF_SVE_BV_2A
        (byte)ID_OP_NONE, // IF_SVE_BV_2A_A
        (byte)ID_OP_NONE, // IF_SVE_BV_2B
        (byte)ID_OP_NONE, // IF_SVE_BV_2A_J
        (byte)ID_OP_NONE, // IF_SVE_BW_2A
        (byte)ID_OP_NONE, // IF_SVE_BX_2A
        (byte)ID_OP_NONE, // IF_SVE_BY_2A
        (byte)ID_OP_NONE, // IF_SVE_BZ_3A
        (byte)ID_OP_NONE, // IF_SVE_BZ_3A_A
        (byte)ID_OP_NONE, // IF_SVE_CB_2A
        (byte)ID_OP_NONE, // IF_SVE_CC_2A
        (byte)ID_OP_NONE, // IF_SVE_CD_2A
        (byte)ID_OP_NONE, // IF_SVE_CE_2A
        (byte)ID_OP_NONE, // IF_SVE_CE_2B
        (byte)ID_OP_NONE, // IF_SVE_CE_2C
        (byte)ID_OP_NONE, // IF_SVE_CE_2D
        (byte)ID_OP_NONE, // IF_SVE_CF_2A
        (byte)ID_OP_NONE, // IF_SVE_CF_2B
        (byte)ID_OP_NONE, // IF_SVE_CF_2C
        (byte)ID_OP_NONE, // IF_SVE_CF_2D
        (byte)ID_OP_NONE, // IF_SVE_CG_2A
        (byte)ID_OP_NONE, // IF_SVE_CH_2A
        (byte)ID_OP_NONE, // IF_SVE_CI_3A
        (byte)ID_OP_NONE, // IF_SVE_CJ_2A
        (byte)ID_OP_NONE, // IF_SVE_CK_2A
        (byte)ID_OP_NONE, // IF_SVE_CL_3A
        (byte)ID_OP_NONE, // IF_SVE_CM_3A
        (byte)ID_OP_NONE, // IF_SVE_CN_3A
        (byte)ID_OP_NONE, // IF_SVE_CO_3A
        (byte)ID_OP_NONE, // IF_SVE_CP_3A
        (byte)ID_OP_NONE, // IF_SVE_CQ_3A
        (byte)ID_OP_NONE, // IF_SVE_CR_3A
        (byte)ID_OP_NONE, // IF_SVE_CS_3A
        (byte)ID_OP_NONE, // IF_SVE_CT_3A
        (byte)ID_OP_NONE, // IF_SVE_CU_3A
        (byte)ID_OP_NONE, // IF_SVE_CV_3A
        (byte)ID_OP_NONE, // IF_SVE_CV_3B
        (byte)ID_OP_NONE, // IF_SVE_CW_4A
        (byte)ID_OP_NONE, // IF_SVE_CX_4A
        (byte)ID_OP_NONE, // IF_SVE_CX_4A_A
        (byte)ID_OP_NONE, // IF_SVE_CY_3A
        (byte)ID_OP_NONE, // IF_SVE_CY_3B
        (byte)ID_OP_NONE, // IF_SVE_CZ_4A
        (byte)ID_OP_NONE, // IF_SVE_CZ_4A_K
        (byte)ID_OP_NONE, // IF_SVE_CZ_4A_L
        (byte)ID_OP_NONE, // IF_SVE_CZ_4A_A
        (byte)ID_OP_NONE, // IF_SVE_DA_4A
        (byte)ID_OP_NONE, // IF_SVE_DB_3A
        (byte)ID_OP_NONE, // IF_SVE_DB_3B
        (byte)ID_OP_NONE, // IF_SVE_DC_3A
        (byte)ID_OP_NONE, // IF_SVE_DD_2A
        (byte)ID_OP_NONE, // IF_SVE_DE_1A
        (byte)ID_OP_NONE, // IF_SVE_DF_2A
        (byte)ID_OP_NONE, // IF_SVE_DG_2A
        (byte)ID_OP_NONE, // IF_SVE_DH_1A
        (byte)ID_OP_NONE, // IF_SVE_DI_2A
        (byte)ID_OP_NONE, // IF_SVE_DJ_1A
        (byte)ID_OP_NONE, // IF_SVE_DK_3A
        (byte)ID_OP_NONE, // IF_SVE_DL_2A
        (byte)ID_OP_NONE, // IF_SVE_DM_2A
        (byte)ID_OP_NONE, // IF_SVE_DN_2A
        (byte)ID_OP_NONE, // IF_SVE_DO_2A
        (byte)ID_OP_NONE, // IF_SVE_DO_2A_A
        (byte)ID_OP_NONE, // IF_SVE_DP_2A
        (byte)ID_OP_NONE, // IF_SVE_DQ_0A
        (byte)ID_OP_NONE, // IF_SVE_DR_1A
        (byte)ID_OP_NONE, // IF_SVE_DS_2A
        (byte)ID_OP_NONE, // IF_SVE_DT_3A
        (byte)ID_OP_NONE, // IF_SVE_DU_3A
        (byte)ID_OP_NONE, // IF_SVE_DV_4A
        (byte)ID_OP_NONE, // IF_SVE_DW_2A
        (byte)ID_OP_NONE, // IF_SVE_DW_2B
        (byte)ID_OP_NONE, // IF_SVE_DX_3A
        (byte)ID_OP_NONE, // IF_SVE_DY_3A
        (byte)ID_OP_NONE, // IF_SVE_DZ_1A
        (byte)ID_OP_NONE, // IF_SVE_EA_1A
        (byte)ID_OP_NONE, // IF_SVE_EB_1A
        (byte)ID_OP_NONE, // IF_SVE_EB_1B
        (byte)ID_OP_NONE, // IF_SVE_EC_1A
        (byte)ID_OP_NONE, // IF_SVE_ED_1A
        (byte)ID_OP_NONE, // IF_SVE_EE_1A
        (byte)ID_OP_NONE, // IF_SVE_EF_3A
        (byte)ID_OP_NONE, // IF_SVE_EG_3A
        (byte)ID_OP_NONE, // IF_SVE_EH_3A
        (byte)ID_OP_NONE, // IF_SVE_EI_3A
        (byte)ID_OP_NONE, // IF_SVE_EJ_3A
        (byte)ID_OP_NONE, // IF_SVE_EK_3A
        (byte)ID_OP_NONE, // IF_SVE_EL_3A
        (byte)ID_OP_NONE, // IF_SVE_EM_3A
        (byte)ID_OP_NONE, // IF_SVE_EQ_3A
        (byte)ID_OP_NONE, // IF_SVE_ES_3A
        (byte)ID_OP_NONE, // IF_SVE_EW_3A
        (byte)ID_OP_NONE, // IF_SVE_EW_3B
        (byte)ID_OP_NONE, // IF_SVE_EX_3A
        (byte)ID_OP_NONE, // IF_SVE_EY_3A
        (byte)ID_OP_NONE, // IF_SVE_EY_3B
        (byte)ID_OP_NONE, // IF_SVE_EZ_3A
        (byte)ID_OP_NONE, // IF_SVE_FA_3A
        (byte)ID_OP_NONE, // IF_SVE_FA_3B
        (byte)ID_OP_NONE, // IF_SVE_FB_3A
        (byte)ID_OP_NONE, // IF_SVE_FB_3B
        (byte)ID_OP_NONE, // IF_SVE_FC_3A
        (byte)ID_OP_NONE, // IF_SVE_FC_3B
        (byte)ID_OP_NONE, // IF_SVE_FD_3A
        (byte)ID_OP_NONE, // IF_SVE_FD_3B
        (byte)ID_OP_NONE, // IF_SVE_FD_3C
        (byte)ID_OP_NONE, // IF_SVE_FE_3A
        (byte)ID_OP_NONE, // IF_SVE_FE_3B
        (byte)ID_OP_NONE, // IF_SVE_FF_3A
        (byte)ID_OP_NONE, // IF_SVE_FF_3B
        (byte)ID_OP_NONE, // IF_SVE_FF_3C
        (byte)ID_OP_NONE, // IF_SVE_FG_3A
        (byte)ID_OP_NONE, // IF_SVE_FG_3B
        (byte)ID_OP_NONE, // IF_SVE_FH_3A
        (byte)ID_OP_NONE, // IF_SVE_FH_3B
        (byte)ID_OP_NONE, // IF_SVE_FI_3A
        (byte)ID_OP_NONE, // IF_SVE_FI_3B
        (byte)ID_OP_NONE, // IF_SVE_FI_3C
        (byte)ID_OP_NONE, // IF_SVE_FJ_3A
        (byte)ID_OP_NONE, // IF_SVE_FJ_3B
        (byte)ID_OP_NONE, // IF_SVE_FK_3A
        (byte)ID_OP_NONE, // IF_SVE_FK_3B
        (byte)ID_OP_NONE, // IF_SVE_FK_3C
        (byte)ID_OP_NONE, // IF_SVE_FL_3A
        (byte)ID_OP_NONE, // IF_SVE_FM_3A
        (byte)ID_OP_NONE, // IF_SVE_FN_3B
        (byte)ID_OP_NONE, // IF_SVE_FO_3A
        (byte)ID_OP_NONE, // IF_SVE_FR_2A
        (byte)ID_OP_NONE, // IF_SVE_FT_2A
        (byte)ID_OP_NONE, // IF_SVE_FU_2A
        (byte)ID_OP_NONE, // IF_SVE_FV_2A
        (byte)ID_OP_NONE, // IF_SVE_FW_3A
        (byte)ID_OP_NONE, // IF_SVE_FY_3A
        (byte)ID_OP_NONE, // IF_SVE_FZ_2A
        (byte)ID_OP_NONE, // IF_SVE_GA_2A
        (byte)ID_OP_NONE, // IF_SVE_GB_2A
        (byte)ID_OP_NONE, // IF_SVE_GC_3A
        (byte)ID_OP_NONE, // IF_SVE_GD_2A
        (byte)ID_OP_NONE, // IF_SVE_GE_4A
        (byte)ID_OP_NONE, // IF_SVE_GF_3A
        (byte)ID_OP_NONE, // IF_SVE_GG_3A
        (byte)ID_OP_NONE, // IF_SVE_GG_3B
        (byte)ID_OP_NONE, // IF_SVE_GH_3A
        (byte)ID_OP_NONE, // IF_SVE_GH_3B
        (byte)ID_OP_NONE, // IF_SVE_GH_3B_B
        (byte)ID_OP_NONE, // IF_SVE_GI_4A
        (byte)ID_OP_NONE, // IF_SVE_GJ_3A
        (byte)ID_OP_NONE, // IF_SVE_GK_2A
        (byte)ID_OP_NONE, // IF_SVE_GL_1A
        (byte)ID_OP_NONE, // IF_SVE_GM_3A
        (byte)ID_OP_NONE, // IF_SVE_GN_3A
        (byte)ID_OP_NONE, // IF_SVE_GO_3A
        (byte)ID_OP_NONE, // IF_SVE_GP_3A
        (byte)ID_OP_NONE, // IF_SVE_GQ_3A
        (byte)ID_OP_NONE, // IF_SVE_GR_3A
        (byte)ID_OP_NONE, // IF_SVE_GS_3A
        (byte)ID_OP_NONE, // IF_SVE_GT_4A
        (byte)ID_OP_NONE, // IF_SVE_GU_3A
        (byte)ID_OP_NONE, // IF_SVE_GU_3B
        (byte)ID_OP_NONE, // IF_SVE_GU_3C
        (byte)ID_OP_NONE, // IF_SVE_GV_3A
        (byte)ID_OP_NONE, // IF_SVE_GW_3B
        (byte)ID_OP_NONE, // IF_SVE_GX_3A
        (byte)ID_OP_NONE, // IF_SVE_GX_3B
        (byte)ID_OP_NONE, // IF_SVE_GX_3C
        (byte)ID_OP_NONE, // IF_SVE_GY_3A
        (byte)ID_OP_NONE, // IF_SVE_GY_3B
        (byte)ID_OP_NONE, // IF_SVE_GY_3B_D
        (byte)ID_OP_NONE, // IF_SVE_GZ_3A
        (byte)ID_OP_NONE, // IF_SVE_HA_3A
        (byte)ID_OP_NONE, // IF_SVE_HA_3A_E
        (byte)ID_OP_NONE, // IF_SVE_HA_3A_F
        (byte)ID_OP_NONE, // IF_SVE_HB_3A
        (byte)ID_OP_NONE, // IF_SVE_HC_3A
        (byte)ID_OP_NONE, // IF_SVE_HD_3A
        (byte)ID_OP_NONE, // IF_SVE_HD_3A_A
        (byte)ID_OP_NONE, // IF_SVE_HE_3A
        (byte)ID_OP_NONE, // IF_SVE_HF_2A
        (byte)ID_OP_NONE, // IF_SVE_HG_2A
        (byte)ID_OP_NONE, // IF_SVE_HH_2A
        (byte)ID_OP_NONE, // IF_SVE_HI_3A
        (byte)ID_OP_NONE, // IF_SVE_HJ_3A
        (byte)ID_OP_NONE, // IF_SVE_HK_3B
        (byte)ID_OP_NONE, // IF_SVE_HL_3A
        (byte)ID_OP_NONE, // IF_SVE_HL_3B
        (byte)ID_OP_NONE, // IF_SVE_HM_2A
        (byte)ID_OP_NONE, // IF_SVE_HN_2A
        (byte)ID_OP_NONE, // IF_SVE_HO_3A
        (byte)ID_OP_NONE, // IF_SVE_HO_3B
        (byte)ID_OP_NONE, // IF_SVE_HO_3C
        (byte)ID_OP_NONE, // IF_SVE_HP_3A
        (byte)ID_OP_NONE, // IF_SVE_HP_3B
        (byte)ID_OP_NONE, // IF_SVE_HQ_3A
        (byte)ID_OP_NONE, // IF_SVE_HR_3A
        (byte)ID_OP_NONE, // IF_SVE_HS_3A
        (byte)ID_OP_NONE, // IF_SVE_HT_4A
        (byte)ID_OP_NONE, // IF_SVE_HU_4A
        (byte)ID_OP_NONE, // IF_SVE_HU_4B
        (byte)ID_OP_NONE, // IF_SVE_HV_4A
        (byte)ID_OP_NONE, // IF_SVE_HW_4A
        (byte)ID_OP_NONE, // IF_SVE_HW_4A_A
        (byte)ID_OP_NONE, // IF_SVE_HW_4B
        (byte)ID_OP_NONE, // IF_SVE_HW_4A_B
        (byte)ID_OP_NONE, // IF_SVE_HW_4A_C
        (byte)ID_OP_NONE, // IF_SVE_HW_4B_D
        (byte)ID_OP_NONE, // IF_SVE_HX_3A
        (byte)ID_OP_NONE, // IF_SVE_HX_3A_B
        (byte)ID_OP_NONE, // IF_SVE_HX_3A_E
        (byte)ID_OP_NONE, // IF_SVE_HY_3A
        (byte)ID_OP_NONE, // IF_SVE_HY_3A_A
        (byte)ID_OP_NONE, // IF_SVE_HY_3B
        (byte)ID_OP_NONE, // IF_SVE_HZ_2A
        (byte)ID_OP_NONE, // IF_SVE_HZ_2A_B
        (byte)ID_OP_NONE, // IF_SVE_IA_2A
        (byte)ID_OP_NONE, // IF_SVE_IB_3A
        (byte)ID_OP_NONE, // IF_SVE_IC_3A
        (byte)ID_OP_NONE, // IF_SVE_IC_3A_A
        (byte)ID_OP_NONE, // IF_SVE_IC_3A_B
        (byte)ID_OP_NONE, // IF_SVE_IC_3A_C
        (byte)ID_OP_NONE, // IF_SVE_ID_2A
        (byte)ID_OP_NONE, // IF_SVE_IE_2A
        (byte)ID_OP_NONE, // IF_SVE_IF_4A
        (byte)ID_OP_NONE, // IF_SVE_IF_4A_A
        (byte)ID_OP_NONE, // IF_SVE_IG_4A
        (byte)ID_OP_NONE, // IF_SVE_IG_4A_C
        (byte)ID_OP_NONE, // IF_SVE_IG_4A_D
        (byte)ID_OP_NONE, // IF_SVE_IG_4A_E
        (byte)ID_OP_NONE, // IF_SVE_IG_4A_F
        (byte)ID_OP_NONE, // IF_SVE_IG_4A_G
        (byte)ID_OP_NONE, // IF_SVE_IH_3A
        (byte)ID_OP_NONE, // IF_SVE_IH_3A_F
        (byte)ID_OP_NONE, // IF_SVE_IH_3A_G
        (byte)ID_OP_NONE, // IF_SVE_IH_3A_A
        (byte)ID_OP_NONE, // IF_SVE_II_4A
        (byte)ID_OP_NONE, // IF_SVE_II_4A_H
        (byte)ID_OP_NONE, // IF_SVE_II_4A_I
        (byte)ID_OP_NONE, // IF_SVE_II_4A_B
        (byte)ID_OP_NONE, // IF_SVE_IJ_3A
        (byte)ID_OP_NONE, // IF_SVE_IJ_3A_C
        (byte)ID_OP_NONE, // IF_SVE_IJ_3A_D
        (byte)ID_OP_NONE, // IF_SVE_IJ_3A_E
        (byte)ID_OP_NONE, // IF_SVE_IJ_3A_F
        (byte)ID_OP_NONE, // IF_SVE_IJ_3A_G
        (byte)ID_OP_NONE, // IF_SVE_IK_4A
        (byte)ID_OP_NONE, // IF_SVE_IK_4A_F
        (byte)ID_OP_NONE, // IF_SVE_IK_4A_G
        (byte)ID_OP_NONE, // IF_SVE_IK_4A_H
        (byte)ID_OP_NONE, // IF_SVE_IK_4A_I
        (byte)ID_OP_NONE, // IF_SVE_IK_4A_E
        (byte)ID_OP_NONE, // IF_SVE_IL_3A
        (byte)ID_OP_NONE, // IF_SVE_IL_3A_A
        (byte)ID_OP_NONE, // IF_SVE_IL_3A_B
        (byte)ID_OP_NONE, // IF_SVE_IL_3A_C
        (byte)ID_OP_NONE, // IF_SVE_IM_3A
        (byte)ID_OP_NONE, // IF_SVE_IN_4A
        (byte)ID_OP_NONE, // IF_SVE_IO_3A
        (byte)ID_OP_NONE, // IF_SVE_IP_4A
        (byte)ID_OP_NONE, // IF_SVE_IQ_3A
        (byte)ID_OP_NONE, // IF_SVE_IR_4A
        (byte)ID_OP_NONE, // IF_SVE_IS_3A
        (byte)ID_OP_NONE, // IF_SVE_IT_4A
        (byte)ID_OP_NONE, // IF_SVE_IU_4A
        (byte)ID_OP_NONE, // IF_SVE_IU_4A_A
        (byte)ID_OP_NONE, // IF_SVE_IU_4B
        (byte)ID_OP_NONE, // IF_SVE_IU_4B_B
        (byte)ID_OP_NONE, // IF_SVE_IU_4A_C
        (byte)ID_OP_NONE, // IF_SVE_IU_4B_D
        (byte)ID_OP_NONE, // IF_SVE_IV_3A
        (byte)ID_OP_NONE, // IF_SVE_IW_4A
        (byte)ID_OP_NONE, // IF_SVE_IX_4A
        (byte)ID_OP_NONE, // IF_SVE_IY_4A
        (byte)ID_OP_NONE, // IF_SVE_IZ_4A
        (byte)ID_OP_NONE, // IF_SVE_IZ_4A_A
        (byte)ID_OP_NONE, // IF_SVE_JA_4A
        (byte)ID_OP_NONE, // IF_SVE_JB_4A
        (byte)ID_OP_NONE, // IF_SVE_JC_4A
        (byte)ID_OP_NONE, // IF_SVE_JD_4A
        (byte)ID_OP_NONE, // IF_SVE_JD_4B
        (byte)ID_OP_NONE, // IF_SVE_JD_4C
        (byte)ID_OP_NONE, // IF_SVE_JD_4C_A
        (byte)ID_OP_NONE, // IF_SVE_JE_3A
        (byte)ID_OP_NONE, // IF_SVE_JF_4A
        (byte)ID_OP_NONE, // IF_SVE_JG_2A
        (byte)ID_OP_NONE, // IF_SVE_JH_2A
        (byte)ID_OP_NONE, // IF_SVE_JI_3A
        (byte)ID_OP_NONE, // IF_SVE_JI_3A_A
        (byte)ID_OP_NONE, // IF_SVE_JJ_4A
        (byte)ID_OP_NONE, // IF_SVE_JJ_4A_B
        (byte)ID_OP_NONE, // IF_SVE_JJ_4A_C
        (byte)ID_OP_NONE, // IF_SVE_JJ_4A_D
        (byte)ID_OP_NONE, // IF_SVE_JJ_4B
        (byte)ID_OP_NONE, // IF_SVE_JJ_4B_E
        (byte)ID_OP_NONE, // IF_SVE_JJ_4B_C
        (byte)ID_OP_NONE, // IF_SVE_JK_4A
        (byte)ID_OP_NONE, // IF_SVE_JK_4A_B
        (byte)ID_OP_NONE, // IF_SVE_JK_4B
        (byte)ID_OP_NONE, // IF_SVE_JL_3A
        (byte)ID_OP_NONE, // IF_SVE_JM_3A
        (byte)ID_OP_NONE, // IF_SVE_JN_3A
        (byte)ID_OP_NONE, // IF_SVE_JN_3B
        (byte)ID_OP_NONE, // IF_SVE_JN_3C
        (byte)ID_OP_NONE, // IF_SVE_JN_3C_D
        (byte)ID_OP_NONE, // IF_SVE_JO_3A
    ];

    private static ReadOnlySpan<byte> s_emitFmtToOpsWasm => [
        (byte)ID_OP_NONE, // IF_NONE
        (byte)ID_OP_NONE, // IF_OPCODE
        (byte)ID_OP_NONE, // IF_BLOCK
        (byte)ID_OP_NONE, // IF_RAW_ULEB128
        (byte)ID_OP_NONE, // IF_CODE_SIZE
        (byte)ID_OP_NONE, // IF_ULEB128
        (byte)ID_OP_NONE, // IF_FUNCIDX
        (byte)ID_OP_NONE, // IF_GLOBALIDX
        (byte)ID_OP_NONE, // IF_SLEB128
        (byte)ID_OP_NONE, // IF_MEMADDR
        (byte)ID_OP_NONE, // IF_FUNCPTR
        (byte)ID_OP_NONE, // IF_FUNCLETPTR
        (byte)ID_OP_NONE, // IF_FUNCLETIDX
        (byte)ID_OP_NONE, // IF_FRAMEIDENTITY
        (byte)ID_OP_NONE, // IF_DATAOFFS
        (byte)ID_OP_NONE, // IF_F32
        (byte)ID_OP_NONE, // IF_F64
        (byte)ID_OP_NONE, // IF_MEMARG
        (byte)ID_OP_NONE, // IF_LOCAL_DECL
        (byte)ID_OP_NONE, // IF_CALL_INDIRECT
        (byte)ID_OP_NONE, // IF_MEMIDX_MEMIDX
        (byte)ID_OP_NONE, // IF_TRY_TABLE
        (byte)ID_OP_NONE, // IF_CATCH_DECL
        (byte)ID_OP_NONE, // IF_V128
        (byte)ID_OP_NONE, // IF_LANE
        (byte)ID_OP_NONE, // IF_MEMARG_LANE
    ];

    private static ReadOnlySpan<byte> s_emitFmtToOpsLoongArch64 => [
        (byte)ID_OP_NONE, // IF_NONE
    ];

    private static ReadOnlySpan<byte> s_emitFmtToOpsRiscV64 => [
        (byte)ID_OP_NONE, // IF_NONE
    ];

    private static class EmitFmtNamesXarch
    {
        internal static readonly string[] Values = [
            "IF_NONE",
            "IF_LABEL",
            "IF_RWR_LABEL",
            "IF_SWR_LABEL",
            "IF_METHOD",
            "IF_METHPTR",
            "IF_CNS",
            "IF_RRD",
            "IF_RWR",
            "IF_RRW",
            "IF_RRD_CNS",
            "IF_RWR_CNS",
            "IF_RRW_CNS",
            "IF_RRW_SHF",
            "IF_RRD_RRD",
            "IF_RWR_RRD",
            "IF_RRW_RRD",
            "IF_RRW_RRW",
            "IF_RRD_RRD_CNS",
            "IF_RWR_RRD_CNS",
            "IF_RRW_RRD_CNS",
            "IF_RWR_RRD_SHF",
            "IF_RRD_RRD_RRD",
            "IF_RWR_RRD_RRD",
            "IF_RRW_RRD_RRD",
            "IF_RWR_RWR_RRD",
            "IF_RWR_RRD_RRD_CNS",
            "IF_RWR_RRD_RRD_RRD",
            "IF_MRD",
            "IF_MWR",
            "IF_MRW",
            "IF_MRD_CNS",
            "IF_MWR_CNS",
            "IF_MRW_CNS",
            "IF_MRW_SHF",
            "IF_MRD_RRD",
            "IF_MWR_RRD",
            "IF_MRW_RRD",
            "IF_MRW_RRW",
            "IF_MRD_RRD_CNS",
            "IF_MWR_RRD_CNS",
            "IF_MRW_RRD_CNS",
            "IF_MWR_RRD_RRD",
            "IF_RRD_MRD",
            "IF_RWR_MRD",
            "IF_RRW_MRD",
            "IF_RRD_MRD_CNS",
            "IF_RWR_MRD_CNS",
            "IF_RRW_MRD_CNS",
            "IF_RRD_MRD_RRD",
            "IF_RWR_MRD_RRD",
            "IF_RRW_MRD_RRD",
            "IF_RRD_RRD_MRD",
            "IF_RWR_RRD_MRD",
            "IF_RRW_RRD_MRD",
            "IF_RWR_RWR_MRD",
            "IF_RWR_RRD_MRD_CNS",
            "IF_RWR_RRD_MRD_RRD",
            "IF_MRD_OFF",
            "IF_RWR_MRD_OFF",
            "IF_SRD",
            "IF_SWR",
            "IF_SRW",
            "IF_SRD_CNS",
            "IF_SWR_CNS",
            "IF_SRW_CNS",
            "IF_SRW_SHF",
            "IF_SRD_RRD",
            "IF_SWR_RRD",
            "IF_SRW_RRD",
            "IF_SRW_RRW",
            "IF_SRD_RRD_CNS",
            "IF_SWR_RRD_CNS",
            "IF_SRW_RRD_CNS",
            "IF_SWR_RRD_RRD",
            "IF_RRD_SRD",
            "IF_RWR_SRD",
            "IF_RRW_SRD",
            "IF_RRD_SRD_CNS",
            "IF_RWR_SRD_CNS",
            "IF_RRW_SRD_CNS",
            "IF_RRD_SRD_RRD",
            "IF_RWR_SRD_RRD",
            "IF_RRW_SRD_RRD",
            "IF_RRD_RRD_SRD",
            "IF_RWR_RRD_SRD",
            "IF_RRW_RRD_SRD",
            "IF_RWR_RWR_SRD",
            "IF_RWR_RRD_SRD_CNS",
            "IF_RWR_RRD_SRD_RRD",
            "IF_ARD",
            "IF_AWR",
            "IF_ARW",
            "IF_ARD_CNS",
            "IF_AWR_CNS",
            "IF_ARW_CNS",
            "IF_ARW_SHF",
            "IF_ARD_RRD",
            "IF_AWR_RRD",
            "IF_ARW_RRD",
            "IF_ARW_RRW",
            "IF_ARD_RRD_CNS",
            "IF_AWR_RRD_CNS",
            "IF_ARW_RRD_CNS",
            "IF_AWR_RRD_RRD",
            "IF_RRD_ARD",
            "IF_RWR_ARD",
            "IF_RRW_ARD",
            "IF_RRD_ARD_CNS",
            "IF_RWR_ARD_CNS",
            "IF_RRW_ARD_CNS",
            "IF_RRD_ARD_RRD",
            "IF_RWR_ARD_RRD",
            "IF_RRW_ARD_RRD",
            "IF_RRD_RRD_ARD",
            "IF_RWR_RRD_ARD",
            "IF_RRW_RRD_ARD",
            "IF_RWR_RWR_ARD",
            "IF_RWR_RRD_ARD_CNS",
            "IF_RWR_RRD_ARD_RRD",
        ];
    }

    private static class EmitFmtNamesArm32
    {
        internal static readonly string[] Values = [
            "IF_NONE",
            "IF_LABEL",
            "IF_LARGEJMP",
            "IF_EN9",
            "IF_EN8",
            "IF_EN6A",
            "IF_EN6B",
            "IF_EN5A",
            "IF_EN5B",
            "IF_EN4A",
            "IF_EN4B",
            "IF_EN4C",
            "IF_EN3A",
            "IF_EN3B",
            "IF_EN3C",
            "IF_EN3D",
            "IF_EN3E",
            "IF_EN2A",
            "IF_EN2B",
            "IF_EN2C",
            "IF_EN2D",
            "IF_EN2E",
            "IF_EN2F",
            "IF_EN2G",
            "IF_T1_A",
            "IF_T1_B",
            "IF_T1_C",
            "IF_T1_D0",
            "IF_T1_D1",
            "IF_T1_D2",
            "IF_T1_E",
            "IF_T1_F",
            "IF_T1_G",
            "IF_T1_H",
            "IF_T1_I",
            "IF_T1_J0",
            "IF_T1_J1",
            "IF_T1_J2",
            "IF_T1_J3",
            "IF_T1_K",
            "IF_T1_L0",
            "IF_T1_L1",
            "IF_T1_M",
            "IF_T2_A",
            "IF_T2_B",
            "IF_T2_C0",
            "IF_T2_C1",
            "IF_T2_C2",
            "IF_T2_C3",
            "IF_T2_C4",
            "IF_T2_C5",
            "IF_T2_C6",
            "IF_T2_C7",
            "IF_T2_C8",
            "IF_T2_C9",
            "IF_T2_C10",
            "IF_T2_D0",
            "IF_T2_D1",
            "IF_T2_E0",
            "IF_T2_E1",
            "IF_T2_E2",
            "IF_T2_F1",
            "IF_T2_F2",
            "IF_T2_G0",
            "IF_T2_G1",
            "IF_T2_H0",
            "IF_T2_H1",
            "IF_T2_H2",
            "IF_T2_I0",
            "IF_T2_I1",
            "IF_T2_J1",
            "IF_T2_J2",
            "IF_T2_J3",
            "IF_T2_K1",
            "IF_T2_K2",
            "IF_T2_K3",
            "IF_T2_K4",
            "IF_T2_L0",
            "IF_T2_L1",
            "IF_T2_L2",
            "IF_T2_M0",
            "IF_T2_M1",
            "IF_T2_N",
            "IF_T2_N1",
            "IF_T2_N2",
            "IF_T2_N3",
            "IF_T2_VLDST",
            "IF_T2_VFP2",
            "IF_T2_VFP3",
            "IF_T2_VMOVS",
            "IF_T2_VMOVD",
            "IF_INVALID",
            "IF_ARM_INVALID",
        ];
    }

    private static class EmitFmtNamesArm64
    {
        internal static readonly string[] Values = [
            "IF_NONE",
            "IF_LABEL",
            "IF_LARGEJMP",
            "IF_LARGEADR",
            "IF_LARGELDC",
            "IF_EN9",
            "IF_EN6A",
            "IF_EN6B",
            "IF_EN5A",
            "IF_EN5B",
            "IF_EN5C",
            "IF_EN4A",
            "IF_EN4B",
            "IF_EN4C",
            "IF_EN4D",
            "IF_EN4E",
            "IF_EN4F",
            "IF_EN4G",
            "IF_EN4H",
            "IF_EN4I",
            "IF_EN4J",
            "IF_EN4K",
            "IF_EN3A",
            "IF_EN3B",
            "IF_EN3C",
            "IF_EN3D",
            "IF_EN3E",
            "IF_EN3F",
            "IF_EN3G",
            "IF_EN3H",
            "IF_EN3I",
            "IF_EN3J",
            "IF_EN2A",
            "IF_EN2B",
            "IF_EN2C",
            "IF_EN2D",
            "IF_EN2E",
            "IF_EN2F",
            "IF_EN2G",
            "IF_EN2H",
            "IF_EN2I",
            "IF_EN2J",
            "IF_EN2K",
            "IF_EN2L",
            "IF_EN2M",
            "IF_EN2N",
            "IF_EN2O",
            "IF_EN2P",
            "IF_EN2Q",
            "IF_BI_0A",
            "IF_BI_0B",
            "IF_BI_0C",
            "IF_BI_1A",
            "IF_BI_1B",
            "IF_BR_0A",
            "IF_BR_1A",
            "IF_BR_1B",
            "IF_LS_1A",
            "IF_LS_2A",
            "IF_LS_2B",
            "IF_LS_2C",
            "IF_LS_2D",
            "IF_LS_2E",
            "IF_LS_2F",
            "IF_LS_2G",
            "IF_LS_3A",
            "IF_LS_3B",
            "IF_LS_3C",
            "IF_LS_3D",
            "IF_LS_3E",
            "IF_LS_3F",
            "IF_LS_3G",
            "IF_DI_1A",
            "IF_DI_1B",
            "IF_DI_1C",
            "IF_DI_1D",
            "IF_DI_1E",
            "IF_DI_1F",
            "IF_DI_2A",
            "IF_DI_2B",
            "IF_DI_2C",
            "IF_DI_2D",
            "IF_DR_1D",
            "IF_DR_2A",
            "IF_DR_2B",
            "IF_DR_2C",
            "IF_DR_2D",
            "IF_DR_2E",
            "IF_DR_2F",
            "IF_DR_2G",
            "IF_DR_2H",
            "IF_DR_2I",
            "IF_DR_3A",
            "IF_DR_3B",
            "IF_DR_3C",
            "IF_DR_3D",
            "IF_DR_3E",
            "IF_DR_4A",
            "IF_DV_1A",
            "IF_DV_1B",
            "IF_DV_1C",
            "IF_DV_2A",
            "IF_DV_2B",
            "IF_DV_2C",
            "IF_DV_2D",
            "IF_DV_2E",
            "IF_DV_2F",
            "IF_DV_2G",
            "IF_DV_2H",
            "IF_DV_2I",
            "IF_DV_2J",
            "IF_DV_2K",
            "IF_DV_2L",
            "IF_DV_2M",
            "IF_DV_2N",
            "IF_DV_2O",
            "IF_DV_2P",
            "IF_DV_2Q",
            "IF_DV_2R",
            "IF_DV_2S",
            "IF_DV_2T",
            "IF_DV_2U",
            "IF_DV_2V",
            "IF_DV_3A",
            "IF_DV_3AI",
            "IF_DV_3B",
            "IF_DV_3BI",
            "IF_DV_3C",
            "IF_DV_3D",
            "IF_DV_3DI",
            "IF_DV_3E",
            "IF_DV_3EI",
            "IF_DV_3F",
            "IF_DV_3G",
            "IF_DV_3H",
            "IF_DV_3I",
            "IF_DV_4A",
            "IF_DV_4B",
            "IF_SN_0A",
            "IF_SI_0A",
            "IF_SI_0B",
            "IF_PC_0A",
            "IF_PC_1A",
            "IF_PC_2A",
            "IF_SR_1A",
            "IF_INVALID",
            "IF_SVE_13A",
            "IF_SVE_11A",
            "IF_SVE_9A",
            "IF_SVE_9B",
            "IF_SVE_9C",
            "IF_SVE_9D",
            "IF_SVE_9E",
            "IF_SVE_9F",
            "IF_SVE_8A",
            "IF_SVE_8B",
            "IF_SVE_8C",
            "IF_SVE_7A",
            "IF_SVE_6A",
            "IF_SVE_6B",
            "IF_SVE_6C",
            "IF_SVE_6D",
            "IF_SVE_6E",
            "IF_SVE_6F",
            "IF_SVE_6G",
            "IF_SVE_5A",
            "IF_SVE_5B",
            "IF_SVE_5C",
            "IF_SVE_5D",
            "IF_SVE_5E",
            "IF_SVE_4A",
            "IF_SVE_4B",
            "IF_SVE_4C",
            "IF_SVE_4D",
            "IF_SVE_4E",
            "IF_SVE_4F",
            "IF_SVE_4G",
            "IF_SVE_4H",
            "IF_SVE_4I",
            "IF_SVE_4J",
            "IF_SVE_4K",
            "IF_SVE_4L",
            "IF_SVE_3A",
            "IF_SVE_3B",
            "IF_SVE_3C",
            "IF_SVE_3D",
            "IF_SVE_3E",
            "IF_SVE_3F",
            "IF_SVE_3G",
            "IF_SVE_3H",
            "IF_SVE_3I",
            "IF_SVE_3J",
            "IF_SVE_3K",
            "IF_SVE_3L",
            "IF_SVE_3M",
            "IF_SVE_3N",
            "IF_SVE_3O",
            "IF_SVE_3P",
            "IF_SVE_3Q",
            "IF_SVE_3R",
            "IF_SVE_3S",
            "IF_SVE_3T",
            "IF_SVE_3U",
            "IF_SVE_3V",
            "IF_SVE_2AA",
            "IF_SVE_2AB",
            "IF_SVE_2AC",
            "IF_SVE_2AD",
            "IF_SVE_2AE",
            "IF_SVE_2AF",
            "IF_SVE_2AG",
            "IF_SVE_2AH",
            "IF_SVE_2AI",
            "IF_SVE_2AJ",
            "IF_SVE_2AK",
            "IF_SVE_2AL",
            "IF_SVE_2AM",
            "IF_SVE_2AN",
            "IF_SVE_2AO",
            "IF_SVE_2AP",
            "IF_SVE_2AQ",
            "IF_SVE_2AR",
            "IF_SVE_2AS",
            "IF_SVE_2AT",
            "IF_SVE_2AU",
            "IF_SVE_2AV",
            "IF_SVE_2AW",
            "IF_SVE_2AX",
            "IF_SVE_2AY",
            "IF_SVE_2AZ",
            "IF_SVE_2BA",
            "IF_SVE_2BB",
            "IF_SVE_2BC",
            "IF_SVE_2BD",
            "IF_SVE_2BE",
            "IF_SVE_2BF",
            "IF_SVE_2BG",
            "IF_SVE_2BH",
            "IF_SVE_2BI",
            "IF_SVE_2BJ",
            "IF_SVE_2BK",
            "IF_SVE_2BL",
            "IF_SVE_2BM",
            "IF_SVE_2BN",
            "IF_SVE_2BO",
            "IF_SVE_2BP",
            "IF_SVE_2BQ",
            "IF_SVE_2BR",
            "IF_SVE_2BS",
            "IF_SVE_AA_3A",
            "IF_SVE_AB_3B",
            "IF_SVE_AC_3A",
            "IF_SVE_AF_3A",
            "IF_SVE_AG_3A",
            "IF_SVE_AH_3A",
            "IF_SVE_AI_3A",
            "IF_SVE_AJ_3A",
            "IF_SVE_AK_3A",
            "IF_SVE_AL_3A",
            "IF_SVE_AM_2A",
            "IF_SVE_AO_3A",
            "IF_SVE_AP_3A",
            "IF_SVE_AQ_3A",
            "IF_SVE_AR_4A",
            "IF_SVE_AS_4A",
            "IF_SVE_AT_3A",
            "IF_SVE_AT_3B",
            "IF_SVE_AU_3A",
            "IF_SVE_AV_3A",
            "IF_SVE_AW_2A",
            "IF_SVE_AX_1A",
            "IF_SVE_AY_2A",
            "IF_SVE_AZ_2A",
            "IF_SVE_BA_3A",
            "IF_SVE_BB_2A",
            "IF_SVE_BC_1A",
            "IF_SVE_BD_3B",
            "IF_SVE_BF_2A",
            "IF_SVE_BG_3A",
            "IF_SVE_BH_3A",
            "IF_SVE_BH_3B",
            "IF_SVE_BH_3B_A",
            "IF_SVE_BI_2A",
            "IF_SVE_BJ_2A",
            "IF_SVE_BL_1A",
            "IF_SVE_BM_1A",
            "IF_SVE_BN_1A",
            "IF_SVE_BO_1A",
            "IF_SVE_BO_1A_A",
            "IF_SVE_BP_1A",
            "IF_SVE_BQ_2A",
            "IF_SVE_BQ_2B",
            "IF_SVE_BR_3B",
            "IF_SVE_BS_1A",
            "IF_SVE_BT_1A",
            "IF_SVE_BU_2A",
            "IF_SVE_BV_2A",
            "IF_SVE_BV_2A_A",
            "IF_SVE_BV_2B",
            "IF_SVE_BV_2A_J",
            "IF_SVE_BW_2A",
            "IF_SVE_BX_2A",
            "IF_SVE_BY_2A",
            "IF_SVE_BZ_3A",
            "IF_SVE_BZ_3A_A",
            "IF_SVE_CB_2A",
            "IF_SVE_CC_2A",
            "IF_SVE_CD_2A",
            "IF_SVE_CE_2A",
            "IF_SVE_CE_2B",
            "IF_SVE_CE_2C",
            "IF_SVE_CE_2D",
            "IF_SVE_CF_2A",
            "IF_SVE_CF_2B",
            "IF_SVE_CF_2C",
            "IF_SVE_CF_2D",
            "IF_SVE_CG_2A",
            "IF_SVE_CH_2A",
            "IF_SVE_CI_3A",
            "IF_SVE_CJ_2A",
            "IF_SVE_CK_2A",
            "IF_SVE_CL_3A",
            "IF_SVE_CM_3A",
            "IF_SVE_CN_3A",
            "IF_SVE_CO_3A",
            "IF_SVE_CP_3A",
            "IF_SVE_CQ_3A",
            "IF_SVE_CR_3A",
            "IF_SVE_CS_3A",
            "IF_SVE_CT_3A",
            "IF_SVE_CU_3A",
            "IF_SVE_CV_3A",
            "IF_SVE_CV_3B",
            "IF_SVE_CW_4A",
            "IF_SVE_CX_4A",
            "IF_SVE_CX_4A_A",
            "IF_SVE_CY_3A",
            "IF_SVE_CY_3B",
            "IF_SVE_CZ_4A",
            "IF_SVE_CZ_4A_K",
            "IF_SVE_CZ_4A_L",
            "IF_SVE_CZ_4A_A",
            "IF_SVE_DA_4A",
            "IF_SVE_DB_3A",
            "IF_SVE_DB_3B",
            "IF_SVE_DC_3A",
            "IF_SVE_DD_2A",
            "IF_SVE_DE_1A",
            "IF_SVE_DF_2A",
            "IF_SVE_DG_2A",
            "IF_SVE_DH_1A",
            "IF_SVE_DI_2A",
            "IF_SVE_DJ_1A",
            "IF_SVE_DK_3A",
            "IF_SVE_DL_2A",
            "IF_SVE_DM_2A",
            "IF_SVE_DN_2A",
            "IF_SVE_DO_2A",
            "IF_SVE_DO_2A_A",
            "IF_SVE_DP_2A",
            "IF_SVE_DQ_0A",
            "IF_SVE_DR_1A",
            "IF_SVE_DS_2A",
            "IF_SVE_DT_3A",
            "IF_SVE_DU_3A",
            "IF_SVE_DV_4A",
            "IF_SVE_DW_2A",
            "IF_SVE_DW_2B",
            "IF_SVE_DX_3A",
            "IF_SVE_DY_3A",
            "IF_SVE_DZ_1A",
            "IF_SVE_EA_1A",
            "IF_SVE_EB_1A",
            "IF_SVE_EB_1B",
            "IF_SVE_EC_1A",
            "IF_SVE_ED_1A",
            "IF_SVE_EE_1A",
            "IF_SVE_EF_3A",
            "IF_SVE_EG_3A",
            "IF_SVE_EH_3A",
            "IF_SVE_EI_3A",
            "IF_SVE_EJ_3A",
            "IF_SVE_EK_3A",
            "IF_SVE_EL_3A",
            "IF_SVE_EM_3A",
            "IF_SVE_EQ_3A",
            "IF_SVE_ES_3A",
            "IF_SVE_EW_3A",
            "IF_SVE_EW_3B",
            "IF_SVE_EX_3A",
            "IF_SVE_EY_3A",
            "IF_SVE_EY_3B",
            "IF_SVE_EZ_3A",
            "IF_SVE_FA_3A",
            "IF_SVE_FA_3B",
            "IF_SVE_FB_3A",
            "IF_SVE_FB_3B",
            "IF_SVE_FC_3A",
            "IF_SVE_FC_3B",
            "IF_SVE_FD_3A",
            "IF_SVE_FD_3B",
            "IF_SVE_FD_3C",
            "IF_SVE_FE_3A",
            "IF_SVE_FE_3B",
            "IF_SVE_FF_3A",
            "IF_SVE_FF_3B",
            "IF_SVE_FF_3C",
            "IF_SVE_FG_3A",
            "IF_SVE_FG_3B",
            "IF_SVE_FH_3A",
            "IF_SVE_FH_3B",
            "IF_SVE_FI_3A",
            "IF_SVE_FI_3B",
            "IF_SVE_FI_3C",
            "IF_SVE_FJ_3A",
            "IF_SVE_FJ_3B",
            "IF_SVE_FK_3A",
            "IF_SVE_FK_3B",
            "IF_SVE_FK_3C",
            "IF_SVE_FL_3A",
            "IF_SVE_FM_3A",
            "IF_SVE_FN_3B",
            "IF_SVE_FO_3A",
            "IF_SVE_FR_2A",
            "IF_SVE_FT_2A",
            "IF_SVE_FU_2A",
            "IF_SVE_FV_2A",
            "IF_SVE_FW_3A",
            "IF_SVE_FY_3A",
            "IF_SVE_FZ_2A",
            "IF_SVE_GA_2A",
            "IF_SVE_GB_2A",
            "IF_SVE_GC_3A",
            "IF_SVE_GD_2A",
            "IF_SVE_GE_4A",
            "IF_SVE_GF_3A",
            "IF_SVE_GG_3A",
            "IF_SVE_GG_3B",
            "IF_SVE_GH_3A",
            "IF_SVE_GH_3B",
            "IF_SVE_GH_3B_B",
            "IF_SVE_GI_4A",
            "IF_SVE_GJ_3A",
            "IF_SVE_GK_2A",
            "IF_SVE_GL_1A",
            "IF_SVE_GM_3A",
            "IF_SVE_GN_3A",
            "IF_SVE_GO_3A",
            "IF_SVE_GP_3A",
            "IF_SVE_GQ_3A",
            "IF_SVE_GR_3A",
            "IF_SVE_GS_3A",
            "IF_SVE_GT_4A",
            "IF_SVE_GU_3A",
            "IF_SVE_GU_3B",
            "IF_SVE_GU_3C",
            "IF_SVE_GV_3A",
            "IF_SVE_GW_3B",
            "IF_SVE_GX_3A",
            "IF_SVE_GX_3B",
            "IF_SVE_GX_3C",
            "IF_SVE_GY_3A",
            "IF_SVE_GY_3B",
            "IF_SVE_GY_3B_D",
            "IF_SVE_GZ_3A",
            "IF_SVE_HA_3A",
            "IF_SVE_HA_3A_E",
            "IF_SVE_HA_3A_F",
            "IF_SVE_HB_3A",
            "IF_SVE_HC_3A",
            "IF_SVE_HD_3A",
            "IF_SVE_HD_3A_A",
            "IF_SVE_HE_3A",
            "IF_SVE_HF_2A",
            "IF_SVE_HG_2A",
            "IF_SVE_HH_2A",
            "IF_SVE_HI_3A",
            "IF_SVE_HJ_3A",
            "IF_SVE_HK_3B",
            "IF_SVE_HL_3A",
            "IF_SVE_HL_3B",
            "IF_SVE_HM_2A",
            "IF_SVE_HN_2A",
            "IF_SVE_HO_3A",
            "IF_SVE_HO_3B",
            "IF_SVE_HO_3C",
            "IF_SVE_HP_3A",
            "IF_SVE_HP_3B",
            "IF_SVE_HQ_3A",
            "IF_SVE_HR_3A",
            "IF_SVE_HS_3A",
            "IF_SVE_HT_4A",
            "IF_SVE_HU_4A",
            "IF_SVE_HU_4B",
            "IF_SVE_HV_4A",
            "IF_SVE_HW_4A",
            "IF_SVE_HW_4A_A",
            "IF_SVE_HW_4B",
            "IF_SVE_HW_4A_B",
            "IF_SVE_HW_4A_C",
            "IF_SVE_HW_4B_D",
            "IF_SVE_HX_3A",
            "IF_SVE_HX_3A_B",
            "IF_SVE_HX_3A_E",
            "IF_SVE_HY_3A",
            "IF_SVE_HY_3A_A",
            "IF_SVE_HY_3B",
            "IF_SVE_HZ_2A",
            "IF_SVE_HZ_2A_B",
            "IF_SVE_IA_2A",
            "IF_SVE_IB_3A",
            "IF_SVE_IC_3A",
            "IF_SVE_IC_3A_A",
            "IF_SVE_IC_3A_B",
            "IF_SVE_IC_3A_C",
            "IF_SVE_ID_2A",
            "IF_SVE_IE_2A",
            "IF_SVE_IF_4A",
            "IF_SVE_IF_4A_A",
            "IF_SVE_IG_4A",
            "IF_SVE_IG_4A_C",
            "IF_SVE_IG_4A_D",
            "IF_SVE_IG_4A_E",
            "IF_SVE_IG_4A_F",
            "IF_SVE_IG_4A_G",
            "IF_SVE_IH_3A",
            "IF_SVE_IH_3A_F",
            "IF_SVE_IH_3A_G",
            "IF_SVE_IH_3A_A",
            "IF_SVE_II_4A",
            "IF_SVE_II_4A_H",
            "IF_SVE_II_4A_I",
            "IF_SVE_II_4A_B",
            "IF_SVE_IJ_3A",
            "IF_SVE_IJ_3A_C",
            "IF_SVE_IJ_3A_D",
            "IF_SVE_IJ_3A_E",
            "IF_SVE_IJ_3A_F",
            "IF_SVE_IJ_3A_G",
            "IF_SVE_IK_4A",
            "IF_SVE_IK_4A_F",
            "IF_SVE_IK_4A_G",
            "IF_SVE_IK_4A_H",
            "IF_SVE_IK_4A_I",
            "IF_SVE_IK_4A_E",
            "IF_SVE_IL_3A",
            "IF_SVE_IL_3A_A",
            "IF_SVE_IL_3A_B",
            "IF_SVE_IL_3A_C",
            "IF_SVE_IM_3A",
            "IF_SVE_IN_4A",
            "IF_SVE_IO_3A",
            "IF_SVE_IP_4A",
            "IF_SVE_IQ_3A",
            "IF_SVE_IR_4A",
            "IF_SVE_IS_3A",
            "IF_SVE_IT_4A",
            "IF_SVE_IU_4A",
            "IF_SVE_IU_4A_A",
            "IF_SVE_IU_4B",
            "IF_SVE_IU_4B_B",
            "IF_SVE_IU_4A_C",
            "IF_SVE_IU_4B_D",
            "IF_SVE_IV_3A",
            "IF_SVE_IW_4A",
            "IF_SVE_IX_4A",
            "IF_SVE_IY_4A",
            "IF_SVE_IZ_4A",
            "IF_SVE_IZ_4A_A",
            "IF_SVE_JA_4A",
            "IF_SVE_JB_4A",
            "IF_SVE_JC_4A",
            "IF_SVE_JD_4A",
            "IF_SVE_JD_4B",
            "IF_SVE_JD_4C",
            "IF_SVE_JD_4C_A",
            "IF_SVE_JE_3A",
            "IF_SVE_JF_4A",
            "IF_SVE_JG_2A",
            "IF_SVE_JH_2A",
            "IF_SVE_JI_3A",
            "IF_SVE_JI_3A_A",
            "IF_SVE_JJ_4A",
            "IF_SVE_JJ_4A_B",
            "IF_SVE_JJ_4A_C",
            "IF_SVE_JJ_4A_D",
            "IF_SVE_JJ_4B",
            "IF_SVE_JJ_4B_E",
            "IF_SVE_JJ_4B_C",
            "IF_SVE_JK_4A",
            "IF_SVE_JK_4A_B",
            "IF_SVE_JK_4B",
            "IF_SVE_JL_3A",
            "IF_SVE_JM_3A",
            "IF_SVE_JN_3A",
            "IF_SVE_JN_3B",
            "IF_SVE_JN_3C",
            "IF_SVE_JN_3C_D",
            "IF_SVE_JO_3A",
        ];
    }

    private static class EmitFmtNamesWasm
    {
        internal static readonly string[] Values = [
            "IF_NONE",
            "IF_OPCODE",
            "IF_BLOCK",
            "IF_RAW_ULEB128",
            "IF_CODE_SIZE",
            "IF_ULEB128",
            "IF_FUNCIDX",
            "IF_GLOBALIDX",
            "IF_SLEB128",
            "IF_MEMADDR",
            "IF_FUNCPTR",
            "IF_FUNCLETPTR",
            "IF_FUNCLETIDX",
            "IF_FRAMEIDENTITY",
            "IF_DATAOFFS",
            "IF_F32",
            "IF_F64",
            "IF_MEMARG",
            "IF_LOCAL_DECL",
            "IF_CALL_INDIRECT",
            "IF_MEMIDX_MEMIDX",
            "IF_TRY_TABLE",
            "IF_CATCH_DECL",
            "IF_V128",
            "IF_LANE",
            "IF_MEMARG_LANE",
        ];
    }

    private static class EmitFmtNamesLoongArch64
    {
        internal static readonly string[] Values = [
            "IF_NONE",
        ];
    }

    private static class EmitFmtNamesRiscV64
    {
        internal static readonly string[] Values = [
            "IF_NONE",
        ];
    }

    private static ReadOnlySpan<IS_INFO> emitFmtToSchedInfo => [
        IS_NONE, // IF_NONE
        IS_NONE, // IF_LABEL
        IS_R1_WR, // IF_RWR_LABEL
        IS_SF_WR, // IF_SWR_LABEL
        IS_NONE, // IF_METHOD
        IS_NONE, // IF_METHPTR
        IS_NONE, // IF_CNS
        IS_R1_RD, // IF_RRD
        IS_R1_WR, // IF_RWR
        IS_R1_RW, // IF_RRW
        IS_R1_RD, // IF_RRD_CNS
        IS_R1_WR, // IF_RWR_CNS
        IS_R1_RW, // IF_RRW_CNS
        IS_R1_RW, // IF_RRW_SHF
        IS_R1_RD | IS_R2_RD, // IF_RRD_RRD
        IS_R1_WR | IS_R2_RD, // IF_RWR_RRD
        IS_R1_RW | IS_R2_RD, // IF_RRW_RRD
        IS_R1_RW | IS_R2_RW, // IF_RRW_RRW
        IS_R1_RD | IS_R2_RD, // IF_RRD_RRD_CNS
        IS_R1_WR | IS_R2_RD, // IF_RWR_RRD_CNS
        IS_R1_RW | IS_R2_RD, // IF_RRW_RRD_CNS
        IS_R1_WR | IS_R2_RD, // IF_RWR_RRD_SHF
        IS_R1_RD | IS_R2_RD | IS_R3_RD, // IF_RRD_RRD_RRD
        IS_R1_WR | IS_R2_RD | IS_R3_RD, // IF_RWR_RRD_RRD
        IS_R1_RW | IS_R2_RD | IS_R3_RD, // IF_RRW_RRD_RRD
        IS_R1_WR | IS_R2_WR | IS_R3_RD, // IF_RWR_RWR_RRD
        IS_R1_WR | IS_R2_RD | IS_R3_RD, // IF_RWR_RRD_RRD_CNS
        IS_R1_WR | IS_R2_RD | IS_R3_RD | IS_R4_RD, // IF_RWR_RRD_RRD_RRD
        IS_GM_RD, // IF_MRD
        IS_GM_WR, // IF_MWR
        IS_GM_RW, // IF_MRW
        IS_GM_RD, // IF_MRD_CNS
        IS_GM_WR, // IF_MWR_CNS
        IS_GM_RW, // IF_MRW_CNS
        IS_GM_RW, // IF_MRW_SHF
        IS_GM_RD | IS_R1_RD, // IF_MRD_RRD
        IS_GM_WR | IS_R1_RD, // IF_MWR_RRD
        IS_GM_RW | IS_R1_RD, // IF_MRW_RRD
        IS_GM_RW | IS_R1_RW, // IF_MRW_RRW
        IS_GM_RD | IS_R1_RD, // IF_MRD_RRD_CNS
        IS_GM_WR | IS_R1_RD, // IF_MWR_RRD_CNS
        IS_GM_RW | IS_R1_RD, // IF_MRW_RRD_CNS
        IS_GM_WR | IS_R1_RD | IS_R2_RD, // IF_MWR_RRD_RRD
        IS_R1_RD | IS_GM_RD, // IF_RRD_MRD
        IS_R1_WR | IS_GM_RD, // IF_RWR_MRD
        IS_R1_RW | IS_GM_RD, // IF_RRW_MRD
        IS_R1_RD | IS_GM_RD, // IF_RRD_MRD_CNS
        IS_R1_WR | IS_GM_RD, // IF_RWR_MRD_CNS
        IS_R1_RW | IS_GM_RD, // IF_RRW_MRD_CNS
        IS_R1_RD | IS_GM_RD | IS_R2_RD, // IF_RRD_MRD_RRD
        IS_R1_WR | IS_GM_RD | IS_R2_RD, // IF_RWR_MRD_RRD
        IS_R1_RW | IS_GM_RD | IS_R2_RD, // IF_RRW_MRD_RRD
        IS_R1_RD | IS_R2_RD | IS_GM_RD, // IF_RRD_RRD_MRD
        IS_R1_WR | IS_R2_RD | IS_GM_RD, // IF_RWR_RRD_MRD
        IS_R1_RW | IS_R2_RD | IS_GM_RD, // IF_RRW_RRD_MRD
        IS_R1_WR | IS_R2_WR | IS_GM_RD, // IF_RWR_RWR_MRD
        IS_R1_WR | IS_R2_RD | IS_GM_RD, // IF_RWR_RRD_MRD_CNS
        IS_R1_WR | IS_R2_RD | IS_GM_RD | IS_R3_RD, // IF_RWR_RRD_MRD_RRD
        IS_GM_RD, // IF_MRD_OFF
        IS_R1_WR | IS_GM_RD, // IF_RWR_MRD_OFF
        IS_SF_RD, // IF_SRD
        IS_SF_WR, // IF_SWR
        IS_SF_RW, // IF_SRW
        IS_SF_RD, // IF_SRD_CNS
        IS_SF_WR, // IF_SWR_CNS
        IS_SF_RW, // IF_SRW_CNS
        IS_SF_RW, // IF_SRW_SHF
        IS_SF_RD | IS_R1_RD, // IF_SRD_RRD
        IS_SF_WR | IS_R1_RD, // IF_SWR_RRD
        IS_SF_RW | IS_R1_RD, // IF_SRW_RRD
        IS_SF_RW | IS_R1_RW, // IF_SRW_RRW
        IS_SF_RD | IS_R1_RD, // IF_SRD_RRD_CNS
        IS_SF_WR | IS_R1_RD, // IF_SWR_RRD_CNS
        IS_SF_RW | IS_R1_RD, // IF_SRW_RRD_CNS
        IS_SF_WR | IS_R1_RD | IS_R2_RD, // IF_SWR_RRD_RRD
        IS_R1_RD | IS_SF_RD, // IF_RRD_SRD
        IS_R1_WR | IS_SF_RD, // IF_RWR_SRD
        IS_R1_RW | IS_SF_RD, // IF_RRW_SRD
        IS_R1_RD | IS_SF_RD, // IF_RRD_SRD_CNS
        IS_R1_WR | IS_SF_RD, // IF_RWR_SRD_CNS
        IS_R1_RW | IS_SF_RD, // IF_RRW_SRD_CNS
        IS_R1_RD | IS_SF_RD | IS_R2_RD, // IF_RRD_SRD_RRD
        IS_R1_WR | IS_SF_RD | IS_R2_RD, // IF_RWR_SRD_RRD
        IS_R1_RW | IS_SF_RD | IS_R2_RD, // IF_RRW_SRD_RRD
        IS_R1_RD | IS_R2_RD | IS_SF_RD, // IF_RRD_RRD_SRD
        IS_R1_WR | IS_R2_RD | IS_SF_RD, // IF_RWR_RRD_SRD
        IS_R1_RW | IS_R2_RD | IS_SF_RD, // IF_RRW_RRD_SRD
        IS_R1_WR | IS_R2_WR | IS_SF_RD, // IF_RWR_RWR_SRD
        IS_R1_WR | IS_R2_RD | IS_SF_RD, // IF_RWR_RRD_SRD_CNS
        IS_R1_WR | IS_R2_RD | IS_SF_RD | IS_R3_RD, // IF_RWR_RRD_SRD_RRD
        IS_AM_RD, // IF_ARD
        IS_AM_WR, // IF_AWR
        IS_AM_RW, // IF_ARW
        IS_AM_RD, // IF_ARD_CNS
        IS_AM_WR, // IF_AWR_CNS
        IS_AM_RW, // IF_ARW_CNS
        IS_AM_RW, // IF_ARW_SHF
        IS_AM_RD | IS_R1_RD, // IF_ARD_RRD
        IS_AM_WR | IS_R1_RD, // IF_AWR_RRD
        IS_AM_RW | IS_R1_RD, // IF_ARW_RRD
        IS_AM_RW | IS_R1_RW, // IF_ARW_RRW
        IS_AM_RD | IS_R1_RD, // IF_ARD_RRD_CNS
        IS_AM_WR | IS_R1_RD, // IF_AWR_RRD_CNS
        IS_AM_RW | IS_R1_RD, // IF_ARW_RRD_CNS
        IS_AM_WR | IS_R1_RD | IS_R2_RD, // IF_AWR_RRD_RRD
        IS_R1_RD | IS_AM_RD, // IF_RRD_ARD
        IS_R1_WR | IS_AM_RD, // IF_RWR_ARD
        IS_R1_RW | IS_AM_RD, // IF_RRW_ARD
        IS_R1_RD | IS_AM_RD, // IF_RRD_ARD_CNS
        IS_R1_WR | IS_AM_RD, // IF_RWR_ARD_CNS
        IS_R1_RW | IS_AM_RD, // IF_RRW_ARD_CNS
        IS_R1_RD | IS_AM_RD | IS_R2_RD, // IF_RRD_ARD_RRD
        IS_R1_WR | IS_AM_RD | IS_R2_RD, // IF_RWR_ARD_RRD
        IS_R1_RW | IS_AM_RD | IS_R2_RD, // IF_RRW_ARD_RRD
        IS_R1_RD | IS_R2_RD | IS_AM_RD, // IF_RRD_RRD_ARD
        IS_R1_WR | IS_R2_RD | IS_AM_RD, // IF_RWR_RRD_ARD
        IS_R1_RW | IS_R2_RD | IS_AM_RD, // IF_RRW_RRD_ARD
        IS_R1_WR | IS_R2_WR | IS_AM_RD, // IF_RWR_RWR_ARD
        IS_R1_WR | IS_R2_RD | IS_AM_RD, // IF_RWR_RRD_ARD_CNS
        IS_R1_WR | IS_R2_RD | IS_AM_RD | IS_R3_RD, // IF_RWR_RRD_ARD_RRD
    ];

    private static ReadOnlySpan<byte> s_arm64InstructionLoadStoreFlags => [
        0, // invalid
        0, // mov
        0, // add
        0, // sub
        1, // ld1
        1, // ld2
        1, // ld3
        1, // ld4
        1, // st1
        1, // st2
        1, // st3
        1, // st4
        1, // ldr
        1, // ldrsw
        0, // fmov
        0, // orr
        1, // ldrb
        1, // ldrh
        1, // ldrsb
        1, // ldrsh
        1, // str
        1, // strb
        1, // strh
        0, // adds
        0, // subs
        0, // cmp
        0, // cmn
        0, // fmul
        0, // fmulx
        0, // and
        0, // eor
        0, // bic
        0, // neg
        0, // cmeq
        0, // cmge
        0, // cmgt
        0, // fcmeq
        0, // fcmge
        0, // fcmgt
        0, // sqshl
        0, // uqshl
        0, // sqdmlal
        0, // sqdmlsl
        0, // sqdmulh
        0, // sqdmull
        0, // sqrdmlah
        0, // sqrdmlsh
        0, // sqrdmulh
        0, // ands
        0, // tst
        0, // orn
        0, // dup
        0, // fmla
        0, // fmls
        0, // fcvtas
        0, // fcvtau
        0, // fcvtms
        0, // fcvtmu
        0, // fcvtns
        0, // fcvtnu
        0, // fcvtps
        0, // fcvtpu
        0, // fcvtzs
        0, // fcvtzu
        0, // scvtf
        0, // ucvtf
        0, // mul
        0, // smull
        0, // umull
        0, // mvn
        1, // ld1_2regs
        1, // ld1_3regs
        1, // ld1_4regs
        1, // st1_2regs
        1, // st1_3regs
        1, // st1_4regs
        1, // ld1r
        1, // ld2r
        1, // ld3r
        1, // ld4r
        0, // negs
        0, // bics
        0, // eon
        0, // lsl
        0, // lsr
        0, // asr
        0, // ror
        1, // ldp
        1, // ldpsw
        1, // stp
        1, // ldnp
        1, // stnp
        0, // ccmp
        0, // ccmn
        0, // ins
        0, // fadd
        0, // fsub
        0, // fdiv
        0, // fmax
        0, // fmaxnm
        0, // fmin
        0, // fminnm
        0, // fabd
        0, // facge
        0, // facgt
        0, // frecps
        0, // frsqrts
        0, // fcmp
        0, // fcmpe
        0, // fabs
        0, // fcmle
        0, // fcmlt
        0, // fcvtxn
        0, // fneg
        0, // frecpe
        0, // frintn
        0, // frintp
        0, // frintm
        0, // frintz
        0, // frinta
        0, // frintx
        0, // frinti
        0, // frsqrte
        0, // fsqrt
        0, // abs
        0, // cmle
        0, // cmlt
        0, // sqabs
        0, // sqneg
        0, // sqxtn
        0, // sqxtun
        0, // suqadd
        0, // usqadd
        0, // uqxtn
        0, // cls
        0, // clz
        0, // rbit
        0, // cnt
        0, // rev16
        0, // rev32
        0, // mla
        0, // mls
        0, // smlal
        0, // smlal2
        0, // smlsl
        0, // smlsl2
        0, // smull2
        0, // sqdmlal2
        0, // sqdmlsl2
        0, // sqdmull2
        0, // sdot
        0, // udot
        0, // umlal
        0, // umlal2
        0, // umlsl
        0, // umlsl2
        0, // umull2
        0, // sshr
        0, // ssra
        0, // srshr
        0, // srsra
        0, // shl
        0, // ushr
        0, // usra
        0, // urshr
        0, // ursra
        0, // sri
        0, // sli
        0, // sqshlu
        0, // sqrshrn
        0, // sqrshrun
        0, // sqshrn
        0, // sqshrun
        0, // uqrshrn
        0, // uqshrn
        0, // cmhi
        0, // cmhs
        0, // cmtst
        0, // sqadd
        0, // sqrshl
        0, // sqsub
        0, // srshl
        0, // sshl
        0, // uqadd
        0, // uqrshl
        0, // uqsub
        0, // urshl
        0, // ushl
        0, // faddp
        0, // fmaxnmp
        0, // fmaxp
        0, // fminnmp
        0, // fminp
        0, // addp
        1, // ldar
        1, // ldarb
        1, // ldarh
        1, // ldapr
        1, // ldaprb
        1, // ldaprh
        1, // ldxr
        1, // ldxrb
        1, // ldxrh
        1, // ldaxr
        1, // ldaxrb
        1, // ldaxrh
        1, // ldur
        1, // ldurb
        1, // ldurh
        1, // ldursb
        1, // ldursh
        1, // ldursw
        1, // ldapur
        1, // ldapurb
        1, // ldapurh
        1, // stlr
        1, // stlrb
        1, // stlrh
        1, // stxr
        1, // stxrb
        1, // stxrh
        1, // stlxr
        1, // stlxrb
        1, // stlxrh
        1, // stur
        1, // sturb
        1, // sturh
        1, // stlur
        1, // stlurb
        1, // stlurh
        1, // casb
        1, // casab
        1, // casalb
        1, // caslb
        1, // cash
        1, // casah
        1, // casalh
        1, // caslh
        1, // cas
        1, // casa
        1, // casal
        1, // casl
        1, // ldaddb
        1, // ldaddab
        1, // ldaddalb
        1, // ldaddlb
        1, // ldaddh
        1, // ldaddah
        1, // ldaddalh
        1, // ldaddlh
        1, // ldadd
        1, // ldadda
        1, // ldaddal
        1, // ldclral
        1, // ldsetal
        1, // ldaddl
        1, // staddb
        1, // staddlb
        1, // staddh
        1, // staddlh
        1, // stadd
        1, // staddl
        1, // swpb
        1, // swpab
        1, // swpalb
        1, // swplb
        1, // swph
        1, // swpah
        1, // swpalh
        1, // swplh
        1, // swp
        1, // swpa
        1, // swpal
        1, // swpl
        0, // adr
        0, // adrp
        0, // b
        0, // b_tail
        0, // bl_local
        0, // bl
        0, // br
        0, // br_tail
        0, // blr
        0, // ret
        0, // retaa
        0, // retab
        0, // beq
        0, // bne
        0, // bhs
        0, // blo
        0, // bmi
        0, // bpl
        0, // bvs
        0, // bvc
        0, // bhi
        0, // bls
        0, // bge
        0, // blt
        0, // bgt
        0, // ble
        0, // cbz
        0, // cbnz
        0, // tbz
        0, // tbnz
        0, // movk
        0, // movn
        0, // movz
        0, // csel
        0, // csinc
        0, // csinv
        0, // csneg
        0, // cinc
        0, // cinv
        0, // cneg
        0, // cset
        0, // csetm
        0, // aese
        0, // aesd
        0, // aesmc
        0, // aesimc
        0, // rev
        0, // rev64
        0, // adc
        0, // adcs
        0, // sbc
        0, // sbcs
        0, // udiv
        0, // sdiv
        0, // mneg
        0, // madd
        0, // msub
        0, // smaddl
        0, // smnegl
        0, // smsubl
        0, // smulh
        0, // umaddl
        0, // umnegl
        0, // umsubl
        0, // umulh
        0, // extr
        0, // lslv
        0, // lsrv
        0, // asrv
        0, // rorv
        0, // crc32b
        0, // crc32h
        0, // crc32w
        0, // crc32x
        0, // crc32cb
        0, // crc32ch
        0, // crc32cw
        0, // crc32cx
        0, // sha1c
        0, // sha1m
        0, // sha1p
        0, // sha1h
        0, // sha1su0
        0, // sha1su1
        0, // sha256h
        0, // sha256h2
        0, // sha256su0
        0, // sha256su1
        0, // ext
        0, // sbfm
        0, // bfm
        0, // ubfm
        0, // sbfiz
        0, // bfi
        0, // ubfiz
        0, // sbfx
        0, // bfxil
        0, // ubfx
        0, // sxtb
        0, // sxth
        0, // sxtw
        0, // uxtb
        0, // uxth
        0, // autia1716
        0, // autiasp
        0, // autib1716
        0, // autibsp
        0, // autibz
        0, // autiaz
        0, // pacia1716
        0, // paciasp
        0, // pacib1716
        0, // pacibsp
        0, // pacibz
        0, // paciaz
        0, // xpaclri
        0, // autiza
        0, // autizb
        0, // paciza
        0, // pacizb
        0, // xpacd
        0, // xpaci
        0, // autia
        0, // autib
        0, // pacia
        0, // pacib
        0, // nop
        0, // yield
        0, // brk
        0, // dsb
        0, // dmb
        0, // isb
        0, // dczva
        0, // mrs_tpid0
        0, // umov
        0, // smov
        0, // movi
        0, // mvni
        0, // urecpe
        0, // ursqrte
        0, // bsl
        0, // bit
        0, // bif
        0, // addv
        0, // ctz
        0, // not
        0, // saddlv
        0, // smaxv
        0, // sminv
        0, // uaddlv
        0, // umaxv
        0, // uminv
        0, // fmaxnmv
        0, // fmaxv
        0, // fminnmv
        0, // fminv
        0, // uzp1
        0, // uzp2
        0, // zip1
        0, // zip2
        0, // trn1
        0, // trn2
        0, // sqxtn2
        0, // sqxtun2
        0, // uqxtn2
        0, // xtn
        0, // xtn2
        0, // fnmul
        0, // fmadd
        0, // fmsub
        0, // fnmadd
        0, // fnmsub
        0, // fcvt
        0, // pmul
        0, // saba
        0, // sabd
        0, // smax
        0, // smaxp
        0, // smin
        0, // sminp
        0, // uaba
        0, // uabd
        0, // umax
        0, // umaxp
        0, // umin
        0, // uminp
        0, // fcvtl
        0, // fcvtl2
        0, // fcvtn
        0, // fcvtn2
        0, // fcvtxn2
        0, // frecpx
        0, // addhn
        0, // addhn2
        0, // pmull
        0, // pmull2
        0, // raddhn
        0, // raddhn2
        0, // rsubhn
        0, // rsubhn2
        0, // sabal
        0, // sabal2
        0, // sabdl
        0, // sabdl2
        0, // sadalp
        0, // saddl
        0, // saddl2
        0, // saddlp
        0, // saddw
        0, // saddw2
        0, // shadd
        0, // shsub
        0, // srhadd
        0, // ssubl
        0, // ssubl2
        0, // ssubw
        0, // ssubw2
        0, // subhn
        0, // subhn2
        0, // uabal
        0, // uabal2
        0, // uabdl
        0, // uabdl2
        0, // uadalp
        0, // uaddl
        0, // uaddl2
        0, // uaddlp
        0, // uaddw
        0, // uaddw2
        0, // uhadd
        0, // uhsub
        0, // urhadd
        0, // usubl
        0, // usubl2
        0, // usubw
        0, // usubw2
        0, // shll
        0, // shll2
        0, // sshll
        0, // sshll2
        0, // ushll
        0, // ushll2
        0, // shrn
        0, // shrn2
        0, // rshrn
        0, // rshrn2
        0, // sqrshrn2
        0, // sqrshrun2
        0, // sqshrn2
        0, // sqshrun2
        0, // uqrshrn2
        0, // uqshrn2
        0, // sxtl
        0, // sxtl2
        0, // uxtl
        0, // uxtl2
        0, // tbl
        0, // tbl_2regs
        0, // tbl_3regs
        0, // tbl_4regs
        0, // tbx
        0, // tbx_2regs
        0, // tbx_3regs
        0, // tbx_4regs
        0, // align
        0, // eor3
        0, // bcax
        0, // sm3ss1
        0, // sha512h
        0, // sha512h2
        0, // sha512su1
        0, // rax1
        0, // sm3partw1
        0, // sm3partw2
        0, // sm4ekey
        0, // xar
        0, // sha512su0
        0, // sm4e
        0, // invalid
        0, // mov
        1, // st1w
        1, // ld1sh
        1, // ld1h
        1, // ld1w
        1, // ld1d
        1, // st1h
        1, // st1d
        0, // pmov
        1, // ldff1sh
        1, // ldff1w
        1, // ldff1h
        1, // ld1sw
        0, // mul
        0, // fdot
        1, // ld1sb
        1, // ld1b
        0, // prfb
        0, // prfd
        0, // prfh
        0, // prfw
        1, // ldff1d
        1, // ldff1sw
        1, // st1b
        0, // asr
        0, // lsl
        0, // lsr
        0, // fmul
        0, // sdot
        0, // udot
        1, // ldff1sb
        1, // ldff1b
        0, // and
        0, // bic
        0, // eor
        0, // orr
        0, // fmov
        0, // sqdmulh
        0, // sqrdmulh
        0, // sqrdmlah
        0, // sqrdmlsh
        0, // mla
        0, // mls
        0, // fmlalb
        0, // fmlalt
        0, // index
        0, // cpy
        1, // ldnt1b
        1, // ldnt1h
        1, // ldnt1w
        1, // stnt1b
        1, // stnt1h
        1, // stnt1w
        0, // add
        0, // sub
        0, // adr
        0, // dup
        0, // trn1
        0, // trn2
        0, // uzp1
        0, // uzp2
        0, // zip1
        0, // zip2
        0, // sqadd
        0, // sqsub
        0, // uqadd
        0, // uqsub
        0, // fmla
        0, // fmls
        0, // luti4
        0, // fadd
        0, // fsub
        0, // clasta
        0, // clastb
        0, // cmpeq
        0, // cmpge
        0, // cmpgt
        0, // cmple
        0, // cmplt
        0, // cmpne
        0, // cmphi
        0, // cmphs
        0, // cmplo
        0, // cmpls
        0, // whilege
        0, // whilegt
        0, // whilehi
        0, // whilehs
        0, // whilele
        0, // whilelo
        0, // whilels
        0, // whilelt
        0, // cdot
        0, // cmla
        0, // sqrdcmlah
        0, // smlalb
        0, // smlalt
        0, // smlslb
        0, // smlslt
        0, // umlalb
        0, // umlalt
        0, // umlslb
        0, // umlslt
        0, // sqdmlalb
        0, // sqdmlalt
        0, // sqdmlslb
        0, // sqdmlslt
        0, // smullb
        0, // smullt
        0, // umullb
        0, // umullt
        0, // sqdmullb
        0, // sqdmullt
        0, // bfmul
        1, // ldnt1d
        1, // stnt1d
        1, // ldr
        1, // str
        0, // smax
        0, // smin
        0, // umax
        0, // umin
        0, // addpt
        0, // subpt
        0, // rev
        0, // smulh
        0, // umulh
        0, // orn
        0, // ext
        0, // sqshl
        0, // uqshl
        0, // fcmeq
        0, // fcmge
        0, // fcmgt
        0, // fcmle
        0, // fcmlt
        0, // fcmne
        0, // tbl
        0, // luti2
        0, // fmax
        0, // fmaxnm
        0, // fmin
        0, // fminnm
        0, // fsubr
        0, // usdot
        0, // fcmla
        0, // bfdot
        0, // fmlallbb
        0, // fmlallbt
        0, // fmlalltb
        0, // fmlalltt
        0, // not
        0, // subr
        0, // movprfx
        0, // decd
        0, // dech
        0, // decw
        0, // incd
        0, // inch
        0, // incw
        0, // sqdecd
        0, // sqdech
        0, // sqdecw
        0, // sqincd
        0, // sqinch
        0, // sqincw
        0, // uqdecd
        0, // uqdech
        0, // uqdecw
        0, // uqincd
        0, // uqinch
        0, // uqincw
        0, // insr
        0, // lasta
        0, // lastb
        0, // splice
        0, // sel
        0, // movs
        0, // ptrue
        0, // rdffr
        0, // cntp
        0, // decp
        0, // incp
        0, // sqdecp
        0, // sqincp
        0, // uqdecp
        0, // uqincp
        0, // pext
        0, // pmullb
        0, // pmullt
        0, // fcvtnt
        0, // bfmla
        0, // bfmls
        0, // bfmlalb
        0, // bfmlalt
        0, // bfmlslb
        0, // bfmlslt
        0, // fmlslb
        0, // fmlslt
        0, // bfadd
        0, // bfsub
        1, // ldnt1sb
        1, // ldnt1sh
        1, // ld1rob
        1, // ld1rod
        1, // ld1roh
        1, // ld1row
        1, // ld1rqb
        1, // ld1rqd
        1, // ld1rqh
        1, // ld1rqw
        1, // ld2q
        1, // ld3q
        1, // ld4q
        1, // ld2b
        1, // ld2d
        1, // ld2h
        1, // ld2w
        1, // ld3b
        1, // ld3d
        1, // ld3h
        1, // ld3w
        1, // ld4b
        1, // ld4d
        1, // ld4h
        1, // ld4w
        1, // st2b
        1, // st2d
        1, // st2h
        1, // st2w
        1, // st3b
        1, // st3d
        1, // st3h
        1, // st3w
        1, // st4b
        1, // st4d
        1, // st4h
        1, // st4w
        1, // st2q
        1, // st3q
        1, // st4q
        0, // abs
        0, // neg
        0, // sxtb
        0, // sxth
        0, // sxtw
        0, // uxtb
        0, // uxth
        0, // uxtw
        0, // ands
        0, // bics
        0, // eors
        0, // nand
        0, // nands
        0, // nor
        0, // nors
        0, // nots
        0, // orns
        0, // orrs
        0, // rbit
        0, // revb
        0, // revh
        0, // revw
        0, // cls
        0, // clz
        0, // cnot
        0, // cnt
        0, // fabs
        0, // fneg
        0, // sdiv
        0, // sdivr
        0, // udiv
        0, // udivr
        0, // eon
        0, // smaxv
        0, // sminv
        0, // umaxv
        0, // uminv
        0, // faddv
        0, // fmaxnmv
        0, // fmaxv
        0, // fminnmv
        0, // fminv
        0, // addp
        0, // smaxp
        0, // sminp
        0, // umaxp
        0, // uminp
        0, // faddp
        0, // fmaxnmp
        0, // fmaxp
        0, // fminnmp
        0, // fminp
        0, // srsra
        0, // ssra
        0, // ursra
        0, // usra
        0, // asrd
        0, // sqshlu
        0, // srshr
        0, // urshr
        0, // sqrshrn
        0, // sqrshrun
        0, // uqrshrn
        0, // sli
        0, // sri
        0, // sqrshl
        0, // sqrshlr
        0, // sqshlr
        0, // srshl
        0, // srshlr
        0, // uqrshl
        0, // uqrshlr
        0, // uqshlr
        0, // urshl
        0, // urshlr
        0, // fabd
        0, // famax
        0, // famin
        0, // fdiv
        0, // fdivr
        0, // fmulx
        0, // fscale
        0, // frecps
        0, // frsqrts
        0, // ftsmul
        0, // facge
        0, // facgt
        0, // facle
        0, // faclt
        0, // fcmuo
        0, // sqsubr
        0, // suqadd
        0, // uqsubr
        0, // usqadd
        0, // sqabs
        0, // sqneg
        0, // urecpe
        0, // ursqrte
        0, // frecpe
        0, // frsqrte
        0, // frecpx
        0, // fsqrt
        0, // tbx
        0, // shadd
        0, // shsub
        0, // shsubr
        0, // srhadd
        0, // uhadd
        0, // uhsub
        0, // uhsubr
        0, // urhadd
        0, // sabd
        0, // uabd
        0, // saba
        0, // uaba
        0, // pmul
        0, // bcax
        0, // bsl
        0, // bsl1n
        0, // bsl2n
        0, // eor3
        0, // nbsl
        0, // bfcvtn
        0, // fcvtn
        0, // fcvtnb
        0, // fcadd
        0, // smmla
        0, // ummla
        0, // usmmla
        0, // bfmmla
        0, // sadalp
        0, // uadalp
        0, // frinta
        0, // frinti
        0, // frintm
        0, // frintn
        0, // frintp
        0, // frintx
        0, // frintz
        0, // sudot
        0, // aesd
        0, // aese
        0, // sm4e
        0, // aesimc
        0, // aesmc
        0, // rax1
        0, // sm4ekey
        0, // xar
        0, // andv
        0, // eorv
        0, // orv
        0, // andqv
        0, // eorqv
        0, // orqv
        0, // saddv
        0, // uaddv
        0, // addqv
        0, // smaxqv
        0, // sminqv
        0, // umaxqv
        0, // uminqv
        0, // asrr
        0, // lslr
        0, // lsrr
        0, // mad
        0, // msb
        0, // addpl
        0, // addvl
        0, // rdvl
        0, // fexpa
        0, // ftssel
        0, // cntb
        0, // cntd
        0, // cnth
        0, // cntw
        0, // decb
        0, // incb
        0, // sqdecb
        0, // sqincb
        0, // uqdecb
        0, // uqincb
        0, // dupm
        0, // fcpy
        0, // dupq
        0, // extq
        0, // tbxq
        0, // sunpkhi
        0, // sunpklo
        0, // uunpkhi
        0, // uunpklo
        0, // punpkhi
        0, // punpklo
        0, // compact
        0, // revd
        0, // brkpa
        0, // brkpas
        0, // brkpb
        0, // brkpbs
        0, // brka
        0, // brkb
        0, // brkas
        0, // brkbs
        0, // brkn
        0, // brkns
        0, // pfirst
        0, // ptrues
        0, // pnext
        0, // rdffrs
        0, // ptest
        0, // pfalse
        0, // setffr
        0, // wrffr
        0, // ctermeq
        0, // ctermne
        0, // whilerw
        0, // whilewr
        0, // psel
        0, // fdup
        0, // sqdmlalbt
        0, // sqdmlslbt
        0, // sclamp
        0, // uclamp
        0, // mlapt
        0, // madpt
        0, // tblq
        0, // uzpq1
        0, // uzpq2
        0, // zipq1
        0, // zipq2
        0, // sabdlb
        0, // sabdlt
        0, // saddlb
        0, // saddlt
        0, // ssublb
        0, // ssublt
        0, // uabdlb
        0, // uabdlt
        0, // uaddlb
        0, // uaddlt
        0, // usublb
        0, // usublt
        0, // saddwb
        0, // saddwt
        0, // ssubwb
        0, // ssubwt
        0, // uaddwb
        0, // uaddwt
        0, // usubwb
        0, // usubwt
        0, // eorbt
        0, // eortb
        0, // bdep
        0, // bext
        0, // bgrp
        0, // sshllb
        0, // sshllt
        0, // ushllb
        0, // ushllt
        0, // saddlbt
        0, // ssublbt
        0, // ssubltb
        0, // cadd
        0, // sqcadd
        0, // sabalb
        0, // sabalt
        0, // uabalb
        0, // uabalt
        0, // adclb
        0, // adclt
        0, // sbclb
        0, // sbclt
        0, // sqcvtn
        0, // sqcvtun
        0, // uqcvtn
        0, // rshrnb
        0, // rshrnt
        0, // shrnb
        0, // shrnt
        0, // sqrshrnb
        0, // sqrshrnt
        0, // sqrshrunb
        0, // sqrshrunt
        0, // sqshrnb
        0, // sqshrnt
        0, // sqshrunb
        0, // sqshrunt
        0, // uqrshrnb
        0, // uqrshrnt
        0, // uqshrnb
        0, // uqshrnt
        0, // addhnb
        0, // addhnt
        0, // raddhnb
        0, // raddhnt
        0, // rsubhnb
        0, // rsubhnt
        0, // subhnb
        0, // subhnt
        0, // sqxtnb
        0, // sqxtnt
        0, // sqxtunb
        0, // sqxtunt
        0, // uqxtnb
        0, // uqxtnt
        0, // match
        0, // nmatch
        0, // histseg
        0, // histcnt
        0, // bfcvtnt
        0, // fcvtlt
        0, // fcvtxnt
        0, // faddqv
        0, // fmaxnmqv
        0, // fmaxqv
        0, // fminnmqv
        0, // fminqv
        0, // fclamp
        0, // bfclamp
        0, // fmmla
        0, // bf1cvt
        0, // bf1cvtlt
        0, // bf2cvt
        0, // bf2cvtlt
        0, // f1cvt
        0, // f1cvtlt
        0, // f2cvt
        0, // f2cvtlt
        0, // fadda
        0, // bfmax
        0, // bfmaxnm
        0, // bfmin
        0, // bfminnm
        0, // ftmad
        0, // bfcvt
        0, // fcvt
        0, // fcvtx
        0, // flogb
        0, // fcvtzs
        0, // fcvtzu
        0, // scvtf
        0, // ucvtf
        0, // fnmla
        0, // fnmls
        0, // fmad
        0, // fmsb
        0, // fnmad
        0, // fnmsb
        1, // ld1rb
        1, // ld1rd
        1, // ld1rsw
        1, // ld1rh
        1, // ld1rsb
        1, // ld1rsh
        1, // ld1rw
        1, // ldnf1b
        1, // ldnf1d
        1, // ldnf1sw
        1, // ldnf1h
        1, // ldnf1sb
        1, // ldnf1sh
        1, // ldnf1w
        1, // ld1q
        1, // ldnt1sw
        1, // st1q
        0, // INS_lea is zero-initialized in native instInfo[INS_count].
    ];

    private static ReadOnlySpan<insFormat> s_armInstructionFormats => [
        insFormat.IF_NONE, // invalid
        insFormat.IF_ARM32_EN9, // add
        insFormat.IF_ARM32_EN9, // sub
        insFormat.IF_ARM32_EN8, // ldr
        insFormat.IF_ARM32_EN6B, // str
        insFormat.IF_ARM32_EN6A, // ldrb
        insFormat.IF_ARM32_EN6B, // strb
        insFormat.IF_ARM32_EN6A, // ldrh
        insFormat.IF_ARM32_EN6B, // strh
        insFormat.IF_ARM32_EN6A, // ldrsb
        insFormat.IF_ARM32_EN6A, // ldrsh
        insFormat.IF_ARM32_EN5A, // mov
        insFormat.IF_ARM32_EN5B, // cmp
        insFormat.IF_ARM32_EN4A, // lsl
        insFormat.IF_ARM32_EN4A, // lsr
        insFormat.IF_ARM32_EN4A, // asr
        insFormat.IF_ARM32_EN4A, // ror
        insFormat.IF_ARM32_EN4B, // pld
        insFormat.IF_ARM32_EN4B, // pldw
        insFormat.IF_ARM32_EN4C, // movt
        insFormat.IF_ARM32_EN4C, // movw
        insFormat.IF_ARM32_EN3A, // and
        insFormat.IF_ARM32_EN3A, // eor
        insFormat.IF_ARM32_EN3A, // orr
        insFormat.IF_ARM32_EN3A, // orn
        insFormat.IF_ARM32_EN3A, // bic
        insFormat.IF_ARM32_EN3A, // adc
        insFormat.IF_ARM32_EN3A, // sbc
        insFormat.IF_ARM32_EN3A, // rsb
        insFormat.IF_ARM32_EN3B, // tst
        insFormat.IF_ARM32_EN3B, // teq
        insFormat.IF_ARM32_EN3B, // cmn
        insFormat.IF_ARM32_EN3C, // mvn
        insFormat.IF_ARM32_EN3D, // push
        insFormat.IF_ARM32_EN3D, // pop
        insFormat.IF_ARM32_EN3E, // b
        insFormat.IF_ARM32_EN2A, // beq
        insFormat.IF_ARM32_EN2A, // bne
        insFormat.IF_ARM32_EN2A, // bhs
        insFormat.IF_ARM32_EN2A, // blo
        insFormat.IF_ARM32_EN2A, // bmi
        insFormat.IF_ARM32_EN2A, // bpl
        insFormat.IF_ARM32_EN2A, // bvs
        insFormat.IF_ARM32_EN2A, // bvc
        insFormat.IF_ARM32_EN2A, // bhi
        insFormat.IF_ARM32_EN2A, // bls
        insFormat.IF_ARM32_EN2A, // bge
        insFormat.IF_ARM32_EN2A, // blt
        insFormat.IF_ARM32_EN2A, // bgt
        insFormat.IF_ARM32_EN2A, // ble
        insFormat.IF_ARM32_EN2B, // bx
        insFormat.IF_ARM32_EN2C, // blx
        insFormat.IF_ARM32_EN2D, // ldm
        insFormat.IF_ARM32_EN2D, // stm
        insFormat.IF_ARM32_EN2E, // sxtb
        insFormat.IF_ARM32_EN2E, // sxth
        insFormat.IF_ARM32_EN2E, // uxtb
        insFormat.IF_ARM32_EN2E, // uxth
        insFormat.IF_ARM32_EN2F, // mul
        insFormat.IF_ARM32_EN2G, // adr
        insFormat.IF_ARM32_T2_M0, // addw
        insFormat.IF_ARM32_T2_D1, // bfc
        insFormat.IF_ARM32_T2_D0, // bfi
        insFormat.IF_ARM32_T2_J3, // bl
        insFormat.IF_ARM32_T1_A, // bkpt
        insFormat.IF_ARM32_T1_I, // cbnz
        insFormat.IF_ARM32_T1_I, // cbz
        insFormat.IF_ARM32_T2_C10, // clz
        insFormat.IF_ARM32_T2_B, // dmb
        insFormat.IF_ARM32_T2_B, // ism
        insFormat.IF_ARM32_T2_I0, // ldmdb
        insFormat.IF_ARM32_T2_G0, // ldrd
        insFormat.IF_ARM32_T2_H1, // ldrex
        insFormat.IF_ARM32_T2_E1, // ldrexb
        insFormat.IF_ARM32_T2_G1, // ldrexd
        insFormat.IF_ARM32_T2_E1, // ldrexh
        insFormat.IF_ARM32_T2_F2, // mla
        insFormat.IF_ARM32_T2_F2, // mls
        insFormat.IF_ARM32_T1_A, // nop
        insFormat.IF_ARM32_T2_A, // nopw
        insFormat.IF_ARM32_T2_D0, // sbfx
        insFormat.IF_ARM32_T2_C5, // sdiv
        insFormat.IF_ARM32_T2_D0, // ssat
        insFormat.IF_ARM32_T2_F1, // smlal
        insFormat.IF_ARM32_T2_F1, // smull
        insFormat.IF_ARM32_EN2D, // stmdb
        insFormat.IF_ARM32_T2_G0, // strd
        insFormat.IF_ARM32_T2_H1, // strex
        insFormat.IF_ARM32_T2_E1, // strexb
        insFormat.IF_ARM32_T2_G1, // strexd
        insFormat.IF_ARM32_T2_E1, // strexh
        insFormat.IF_ARM32_T2_M0, // subw
        insFormat.IF_ARM32_T2_C9, // tbb
        insFormat.IF_ARM32_T2_C9, // tbh
        insFormat.IF_ARM32_T2_D0, // ubfx
        insFormat.IF_ARM32_T2_C5, // udiv
        insFormat.IF_ARM32_T2_F1, // umlal
        insFormat.IF_ARM32_T2_F1, // umull
        insFormat.IF_ARM32_T2_D0, // usat
        insFormat.IF_ARM32_T2_VLDST, // vstr
        insFormat.IF_ARM32_T2_VLDST, // vldr
        insFormat.IF_ARM32_T2_VLDST, // vstm
        insFormat.IF_ARM32_T2_VLDST, // vldm
        insFormat.IF_ARM32_T2_VLDST, // vpush
        insFormat.IF_ARM32_T2_VLDST, // vpop
        insFormat.IF_ARM32_T2_E2, // vmrs
        insFormat.IF_ARM32_T2_VFP3, // vadd
        insFormat.IF_ARM32_T2_VFP3, // vsub
        insFormat.IF_ARM32_T2_VFP3, // vmul
        insFormat.IF_ARM32_T2_VFP3, // vdiv
        insFormat.IF_ARM32_T2_VFP2, // vmov
        insFormat.IF_ARM32_T2_VFP2, // vabs
        insFormat.IF_ARM32_T2_VFP2, // vsqrt
        insFormat.IF_ARM32_T2_VFP2, // vneg
        insFormat.IF_ARM32_T2_VFP2, // vcmp
        insFormat.IF_ARM32_T2_VFP2, // vcmp0
        insFormat.IF_ARM32_T2_VFP2, // vcvt_d2i
        insFormat.IF_ARM32_T2_VFP2, // vcvt_f2i
        insFormat.IF_ARM32_T2_VFP2, // vcvt_d2u
        insFormat.IF_ARM32_T2_VFP2, // vcvt_f2u
        insFormat.IF_ARM32_T2_VFP2, // vcvt_i2f
        insFormat.IF_ARM32_T2_VFP2, // vcvt_i2d
        insFormat.IF_ARM32_T2_VFP2, // vcvt_u2f
        insFormat.IF_ARM32_T2_VFP2, // vcvt_u2d
        insFormat.IF_ARM32_T2_VFP2, // vcvt_d2f
        insFormat.IF_ARM32_T2_VFP2, // vcvt_f2d
        insFormat.IF_ARM32_T2_VMOVD, // vmov_i2d
        insFormat.IF_ARM32_T2_VMOVD, // vmov_d2i
        insFormat.IF_ARM32_T2_VMOVS, // vmov_i2f
        insFormat.IF_ARM32_T2_VMOVS, // vmov_f2i
    ];

    private static ReadOnlySpan<byte> s_armInstructionLoadStoreFlags => [
        0, // invalid
        0, // add
        0, // sub
        2, // ldr
        4, // str
        2, // ldrb
        4, // strb
        2, // ldrh
        4, // strh
        2, // ldrsb
        2, // ldrsh
        0, // mov
        0, // cmp
        0, // lsl
        0, // lsr
        0, // asr
        0, // ror
        2, // pld
        2, // pldw
        0, // movt
        0, // movw
        0, // and
        0, // eor
        0, // orr
        0, // orn
        0, // bic
        0, // adc
        0, // sbc
        0, // rsb
        0, // tst
        0, // teq
        0, // cmn
        0, // mvn
        0, // push
        0, // pop
        0, // b
        0, // beq
        0, // bne
        0, // bhs
        0, // blo
        0, // bmi
        0, // bpl
        0, // bvs
        0, // bvc
        0, // bhi
        0, // bls
        0, // bge
        0, // blt
        0, // bgt
        0, // ble
        0, // bx
        0, // blx
        2, // ldm
        4, // stm
        0, // sxtb
        0, // sxth
        0, // uxtb
        0, // uxth
        0, // mul
        0, // adr
        0, // addw
        0, // bfc
        0, // bfi
        0, // bl
        0, // bkpt
        0, // cbnz
        0, // cbz
        0, // clz
        0, // dmb
        0, // ism
        2, // ldmdb
        2, // ldrd
        2, // ldrex
        2, // ldrexb
        2, // ldrexd
        2, // ldrexh
        0, // mla
        0, // mls
        0, // nop
        0, // nopw
        0, // sbfx
        0, // sdiv
        0, // ssat
        0, // smlal
        0, // smull
        4, // stmdb
        4, // strd
        4, // strex
        4, // strexb
        4, // strexd
        4, // strexh
        0, // subw
        0, // tbb
        0, // tbh
        0, // ubfx
        0, // udiv
        0, // umlal
        0, // umull
        0, // usat
        4, // vstr
        2, // vldr
        4, // vstm
        2, // vldm
        4, // vpush
        2, // vpop
        0, // vmrs
        0, // vadd
        0, // vsub
        0, // vmul
        0, // vdiv
        0, // vmov
        0, // vabs
        0, // vsqrt
        0, // vneg
        0, // vcmp
        0, // vcmp0
        0, // vcvt_d2i
        0, // vcvt_f2i
        0, // vcvt_d2u
        0, // vcvt_f2u
        0, // vcvt_i2f
        0, // vcvt_i2d
        0, // vcvt_u2f
        0, // vcvt_u2d
        0, // vcvt_d2f
        0, // vcvt_f2d
        0, // vmov_i2d
        0, // vmov_d2i
        0, // vmov_i2f
        0, // vmov_f2i
    ];

    private static ReadOnlySpan<byte> s_armInstructionCompareFlags => [
        0, // invalid
        0, // add
        0, // sub
        0, // ldr
        0, // str
        0, // ldrb
        0, // strb
        0, // ldrh
        0, // strh
        0, // ldrsb
        0, // ldrsh
        0, // mov
        8, // cmp
        0, // lsl
        0, // lsr
        0, // asr
        0, // ror
        0, // pld
        0, // pldw
        0, // movt
        0, // movw
        0, // and
        0, // eor
        0, // orr
        0, // orn
        0, // bic
        0, // adc
        0, // sbc
        0, // rsb
        8, // tst
        8, // teq
        8, // cmn
        0, // mvn
        0, // push
        0, // pop
        0, // b
        0, // beq
        0, // bne
        0, // bhs
        0, // blo
        0, // bmi
        0, // bpl
        0, // bvs
        0, // bvc
        0, // bhi
        0, // bls
        0, // bge
        0, // blt
        0, // bgt
        0, // ble
        0, // bx
        0, // blx
        0, // ldm
        0, // stm
        0, // sxtb
        0, // sxth
        0, // uxtb
        0, // uxth
        0, // mul
        0, // adr
        0, // addw
        0, // bfc
        0, // bfi
        0, // bl
        0, // bkpt
        0, // cbnz
        0, // cbz
        0, // clz
        0, // dmb
        0, // ism
        0, // ldmdb
        0, // ldrd
        0, // ldrex
        0, // ldrexb
        0, // ldrexd
        0, // ldrexh
        0, // mla
        0, // mls
        0, // nop
        0, // nopw
        0, // sbfx
        0, // sdiv
        0, // ssat
        0, // smlal
        0, // smull
        0, // stmdb
        0, // strd
        0, // strex
        0, // strexb
        0, // strexd
        0, // strexh
        0, // subw
        0, // tbb
        0, // tbh
        0, // ubfx
        0, // udiv
        0, // umlal
        0, // umull
        0, // usat
        0, // vstr
        0, // vldr
        0, // vstm
        0, // vldm
        0, // vpush
        0, // vpop
        0, // vmrs
        0, // vadd
        0, // vsub
        0, // vmul
        0, // vdiv
        0, // vmov
        0, // vabs
        0, // vsqrt
        0, // vneg
        8, // vcmp
        8, // vcmp0
        0, // vcvt_d2i
        0, // vcvt_f2i
        0, // vcvt_d2u
        0, // vcvt_f2u
        0, // vcvt_i2f
        0, // vcvt_i2d
        0, // vcvt_u2f
        0, // vcvt_u2d
        0, // vcvt_d2f
        0, // vcvt_f2d
        0, // vmov_i2d
        0, // vmov_d2i
        0, // vmov_i2f
        0, // vmov_f2i
    ];

    private static ReadOnlySpan<byte> s_x86InstructionNddFlags => [
        0, // INS_invalid
        0, // INS_push
        0, // INS_pop
        0, // INS_push_hide
        0, // INS_pop_hide
        1, // INS_push2
        1, // INS_pop2
        1, // INS_inc
        0, // INS_inc_l
        1, // INS_dec
        0, // INS_dec_l
        0, // INS_bswap
        1, // INS_add
        1, // INS_or
        0, // INS_adc
        0, // INS_sbb
        1, // INS_and
        1, // INS_sub
        0, // INS_sub_hide
        1, // INS_xor
        0, // INS_cmp
        0, // INS_test
        0, // INS_mov
        0, // INS_lea
        0, // INS_bt
        0, // INS_bts
        0, // INS_btr
        0, // INS_btc
        0, // INS_bsr
        0, // INS_bsf
        0, // INS_movsx
        0, // INS_movzx
        1, // INS_cmovo
        1, // INS_cmovno
        1, // INS_cmovb
        1, // INS_cmovae
        1, // INS_cmove
        1, // INS_cmovne
        1, // INS_cmovbe
        1, // INS_cmova
        1, // INS_cmovs
        1, // INS_cmovns
        1, // INS_cmovp
        1, // INS_cmovnp
        1, // INS_cmovl
        1, // INS_cmovge
        1, // INS_cmovle
        1, // INS_cmovg
        0, // INS_xchg
        1, // INS_imul
        0, // INS_imul_AX
        0, // INS_imul_CX
        0, // INS_imul_DX
        0, // INS_imul_BX
        0, // INS_imul_SP
        0, // INS_imul_BP
        0, // INS_imul_SI
        0, // INS_imul_DI
        0, // INS_addpd
        0, // INS_addps
        0, // INS_addsd
        0, // INS_addss
        0, // INS_addsubpd
        0, // INS_addsubps
        0, // INS_andnpd
        0, // INS_andnps
        0, // INS_andpd
        0, // INS_andps
        0, // INS_blendpd
        0, // INS_blendps
        0, // INS_blendvpd
        0, // INS_blendvps
        0, // INS_cmppd
        0, // INS_cmpps
        0, // INS_cmpsd
        0, // INS_cmpss
        0, // INS_comisd
        0, // INS_comiss
        0, // INS_cvtdq2pd
        0, // INS_cvtdq2ps
        0, // INS_cvtpd2dq
        0, // INS_cvtpd2ps
        0, // INS_cvtps2dq
        0, // INS_cvtps2pd
        0, // INS_cvtsd2si32
        0, // INS_cvtsd2si64
        0, // INS_cvtsd2ss
        0, // INS_cvtsi2sd32
        0, // INS_cvtsi2sd64
        0, // INS_cvtsi2ss32
        0, // INS_cvtsi2ss64
        0, // INS_cvtss2sd
        0, // INS_cvtss2si32
        0, // INS_cvtss2si64
        0, // INS_cvttpd2dq
        0, // INS_cvttps2dq
        0, // INS_cvttsd2si32
        0, // INS_cvttsd2si64
        0, // INS_cvttss2si32
        0, // INS_cvttss2si64
        0, // INS_divpd
        0, // INS_divps
        0, // INS_divsd
        0, // INS_divss
        0, // INS_dppd
        0, // INS_dpps
        0, // INS_extractps
        0, // INS_haddpd
        0, // INS_haddps
        0, // INS_hsubpd
        0, // INS_hsubps
        0, // INS_insertps
        0, // INS_lddqu
        0, // INS_lfence
        0, // INS_maskmovdqu
        0, // INS_maxpd
        0, // INS_maxps
        0, // INS_maxsd
        0, // INS_maxss
        0, // INS_mfence
        0, // INS_minpd
        0, // INS_minps
        0, // INS_minsd
        0, // INS_minss
        0, // INS_movapd
        0, // INS_movaps
        0, // INS_movd32
        0, // INS_movd64
        0, // INS_movddup
        0, // INS_movdqa32
        0, // INS_movdqu32
        0, // INS_movhlps
        0, // INS_movhpd
        0, // INS_movhps
        0, // INS_movlhps
        0, // INS_movlpd
        0, // INS_movlps
        0, // INS_movmskpd
        0, // INS_movmskps
        0, // INS_movntdq
        0, // INS_movntdqa
        0, // INS_movnti32
        0, // INS_movnti64
        0, // INS_movntpd
        0, // INS_movntps
        0, // INS_movq
        0, // INS_movsd_simd
        0, // INS_movshdup
        0, // INS_movsldup
        0, // INS_movss
        0, // INS_movupd
        0, // INS_movups
        0, // INS_mpsadbw
        0, // INS_mulpd
        0, // INS_mulps
        0, // INS_mulsd
        0, // INS_mulss
        0, // INS_orpd
        0, // INS_orps
        0, // INS_pabsb
        0, // INS_pabsd
        0, // INS_pabsw
        0, // INS_packssdw
        0, // INS_packsswb
        0, // INS_packusdw
        0, // INS_packuswb
        0, // INS_paddb
        0, // INS_paddd
        0, // INS_paddq
        0, // INS_paddsb
        0, // INS_paddsw
        0, // INS_paddusb
        0, // INS_paddusw
        0, // INS_paddw
        0, // INS_palignr
        0, // INS_pandd
        0, // INS_pandnd
        0, // INS_pavgb
        0, // INS_pavgw
        0, // INS_pblendvb
        0, // INS_pblendw
        0, // INS_pcmpeqb
        0, // INS_pcmpeqd
        0, // INS_pcmpeqq
        0, // INS_pcmpeqw
        0, // INS_pcmpgtb
        0, // INS_pcmpgtd
        0, // INS_pcmpgtq
        0, // INS_pcmpgtw
        0, // INS_pextrb
        0, // INS_pextrd
        0, // INS_pextrq
        0, // INS_phaddd
        0, // INS_pextrw
        0, // INS_phaddsw
        0, // INS_phaddw
        0, // INS_phminposuw
        0, // INS_phsubd
        0, // INS_phsubsw
        0, // INS_phsubw
        0, // INS_pinsrb
        0, // INS_pinsrd
        0, // INS_pinsrq
        0, // INS_pinsrw
        0, // INS_pmaddubsw
        0, // INS_pmaddwd
        0, // INS_pmaxsb
        0, // INS_pmaxsd
        0, // INS_pmaxsw
        0, // INS_pmaxub
        0, // INS_pmaxud
        0, // INS_pmaxuw
        0, // INS_pminsb
        0, // INS_pminsd
        0, // INS_pminsw
        0, // INS_pminub
        0, // INS_pminud
        0, // INS_pminuw
        0, // INS_pmovmskb
        0, // INS_pmovsxbd
        0, // INS_pmovsxbq
        0, // INS_pmovsxbw
        0, // INS_pmovsxdq
        0, // INS_pmovsxwd
        0, // INS_pmovsxwq
        0, // INS_pmovzxbd
        0, // INS_pmovzxbq
        0, // INS_pmovzxbw
        0, // INS_pmovzxdq
        0, // INS_pmovzxwd
        0, // INS_pmovzxwq
        0, // INS_pmuldq
        0, // INS_pmulhrsw
        0, // INS_pmulhuw
        0, // INS_pmulhw
        0, // INS_pmulld
        0, // INS_pmuludq
        0, // INS_pmullw
        0, // INS_pord
        0, // INS_prefetchnta
        0, // INS_prefetcht0
        0, // INS_prefetcht1
        0, // INS_prefetcht2
        0, // INS_psadbw
        0, // INS_pshufb
        0, // INS_pshufd
        0, // INS_pshufhw
        0, // INS_pshuflw
        0, // INS_psignb
        0, // INS_psignd
        0, // INS_psignw
        0, // INS_pslld
        0, // INS_pslldq
        0, // INS_psllq
        0, // INS_psllw
        0, // INS_psrad
        0, // INS_psraw
        0, // INS_psrld
        0, // INS_psrldq
        0, // INS_psrlq
        0, // INS_psrlw
        0, // INS_psubb
        0, // INS_psubd
        0, // INS_psubq
        0, // INS_psubsb
        0, // INS_psubsw
        0, // INS_psubusb
        0, // INS_psubusw
        0, // INS_psubw
        0, // INS_ptest
        0, // INS_punpckhbw
        0, // INS_punpckhdq
        0, // INS_punpckhqdq
        0, // INS_punpckhwd
        0, // INS_punpcklbw
        0, // INS_punpckldq
        0, // INS_punpcklqdq
        0, // INS_punpcklwd
        0, // INS_pxord
        0, // INS_rcpps
        0, // INS_rcpss
        0, // INS_roundpd
        0, // INS_roundps
        0, // INS_roundsd
        0, // INS_roundss
        0, // INS_rsqrtps
        0, // INS_rsqrtss
        0, // INS_sfence
        0, // INS_shufpd
        0, // INS_shufps
        0, // INS_sqrtpd
        0, // INS_sqrtps
        0, // INS_sqrtsd
        0, // INS_sqrtss
        0, // INS_subpd
        0, // INS_subps
        0, // INS_subsd
        0, // INS_subss
        0, // INS_ucomisd
        0, // INS_ucomiss
        0, // INS_unpckhpd
        0, // INS_unpckhps
        0, // INS_unpcklpd
        0, // INS_unpcklps
        0, // INS_xorpd
        0, // INS_xorps
        0, // INS_aesdec
        0, // INS_aesdeclast
        0, // INS_aesenc
        0, // INS_aesenclast
        0, // INS_aesimc
        0, // INS_aeskeygenassist
        0, // INS_pclmulqdq
        0, // INS_sha1msg1
        0, // INS_sha1msg2
        0, // INS_sha1nexte
        0, // INS_sha1rnds4
        0, // INS_sha256msg1
        0, // INS_sha256msg2
        0, // INS_sha256rnds2
        0, // INS_gf2p8affineinvqb
        0, // INS_gf2p8affineqb
        0, // INS_gf2p8mulb
        0, // INS_vblendvpd
        0, // INS_vblendvps
        0, // INS_vbroadcastf32x4
        0, // INS_vbroadcastsd
        0, // INS_vbroadcastss
        0, // INS_vextractf32x4
        0, // INS_vinsertf32x4
        0, // INS_vmaskmovpd
        0, // INS_vmaskmovps
        0, // INS_vpblendvb
        0, // INS_vperm2f128
        0, // INS_vpermilpd
        0, // INS_vpermilpdvar
        0, // INS_vpermilps
        0, // INS_vpermilpsvar
        0, // INS_vtestpd
        0, // INS_vtestps
        0, // INS_vzeroupper
        0, // INS_vbroadcasti32x4
        0, // INS_vcvtph2ps
        0, // INS_vcvtps2ph
        0, // INS_vextracti32x4
        0, // INS_vgatherdpd
        0, // INS_vgatherdps
        0, // INS_vgatherqpd
        0, // INS_vgatherqps
        0, // INS_vinserti32x4
        0, // INS_vpblendd
        0, // INS_vpbroadcastb
        0, // INS_vpbroadcastd
        0, // INS_vpbroadcastq
        0, // INS_vpbroadcastw
        0, // INS_vperm2i128
        0, // INS_vpermd
        0, // INS_vpermpd
        0, // INS_vpermps
        0, // INS_vpermq
        0, // INS_vpgatherdd
        0, // INS_vpgatherdq
        0, // INS_vpgatherqd
        0, // INS_vpgatherqq
        0, // INS_vpmaskmovd
        0, // INS_vpmaskmovq
        0, // INS_vpsllvd
        0, // INS_vpsllvq
        0, // INS_vpsravd
        0, // INS_vpsrlvd
        0, // INS_vpsrlvq
        0, // INS_vfmadd132pd
        0, // INS_vfmadd213pd
        0, // INS_vfmadd231pd
        0, // INS_vfmadd132ps
        0, // INS_vfmadd213ps
        0, // INS_vfmadd231ps
        0, // INS_vfmadd132sd
        0, // INS_vfmadd213sd
        0, // INS_vfmadd231sd
        0, // INS_vfmadd132ss
        0, // INS_vfmadd213ss
        0, // INS_vfmadd231ss
        0, // INS_vfmaddsub132pd
        0, // INS_vfmaddsub213pd
        0, // INS_vfmaddsub231pd
        0, // INS_vfmaddsub132ps
        0, // INS_vfmaddsub213ps
        0, // INS_vfmaddsub231ps
        0, // INS_vfmsubadd132pd
        0, // INS_vfmsubadd213pd
        0, // INS_vfmsubadd231pd
        0, // INS_vfmsubadd132ps
        0, // INS_vfmsubadd213ps
        0, // INS_vfmsubadd231ps
        0, // INS_vfmsub132pd
        0, // INS_vfmsub213pd
        0, // INS_vfmsub231pd
        0, // INS_vfmsub132ps
        0, // INS_vfmsub213ps
        0, // INS_vfmsub231ps
        0, // INS_vfmsub132sd
        0, // INS_vfmsub213sd
        0, // INS_vfmsub231sd
        0, // INS_vfmsub132ss
        0, // INS_vfmsub213ss
        0, // INS_vfmsub231ss
        0, // INS_vfnmadd132pd
        0, // INS_vfnmadd213pd
        0, // INS_vfnmadd231pd
        0, // INS_vfnmadd132ps
        0, // INS_vfnmadd213ps
        0, // INS_vfnmadd231ps
        0, // INS_vfnmadd132sd
        0, // INS_vfnmadd213sd
        0, // INS_vfnmadd231sd
        0, // INS_vfnmadd132ss
        0, // INS_vfnmadd213ss
        0, // INS_vfnmadd231ss
        0, // INS_vfnmsub132pd
        0, // INS_vfnmsub213pd
        0, // INS_vfnmsub231pd
        0, // INS_vfnmsub132ps
        0, // INS_vfnmsub213ps
        0, // INS_vfnmsub231ps
        0, // INS_vfnmsub132sd
        0, // INS_vfnmsub213sd
        0, // INS_vfnmsub231sd
        0, // INS_vfnmsub132ss
        0, // INS_vfnmsub213ss
        0, // INS_vfnmsub231ss
        0, // INS_andn
        0, // INS_bextr
        0, // INS_blsi
        0, // INS_blsmsk
        0, // INS_blsr
        0, // INS_bzhi
        0, // INS_mulx
        0, // INS_pdep
        0, // INS_pext
        0, // INS_rorx
        0, // INS_sarx
        0, // INS_shlx
        0, // INS_shrx
        0, // INS_vpdpbusd
        0, // INS_vpdpbusds
        0, // INS_vpdpwssd
        0, // INS_vpdpwssds
        0, // INS_vpdpwsud
        0, // INS_vpdpwsuds
        0, // INS_vpdpwusd
        0, // INS_vpdpwusds
        0, // INS_vpdpwuud
        0, // INS_vpdpwuuds
        0, // INS_vpdpbssd
        0, // INS_vpdpbssds
        0, // INS_vpdpbsud
        0, // INS_vpdpbsuds
        0, // INS_vpdpbuud
        0, // INS_vpdpbuuds
        0, // INS_vbmacor16x16x16
        0, // INS_vbmacxor16x16x16
        0, // INS_vbitrev
        0, // INS_vpmadd52huq
        0, // INS_vpmadd52luq
        0, // INS_kaddb
        0, // INS_kaddd
        0, // INS_kaddq
        0, // INS_kaddw
        0, // INS_kandb
        0, // INS_kandd
        0, // INS_kandnb
        0, // INS_kandnd
        0, // INS_kandnq
        0, // INS_kandnw
        0, // INS_kandq
        0, // INS_kandw
        0, // INS_kmovb_gpr
        0, // INS_kmovb_msk
        0, // INS_kmovd_gpr
        0, // INS_kmovd_msk
        0, // INS_kmovq_gpr
        0, // INS_kmovq_msk
        0, // INS_kmovw_gpr
        0, // INS_kmovw_msk
        0, // INS_knotb
        0, // INS_knotd
        0, // INS_knotq
        0, // INS_knotw
        0, // INS_korb
        0, // INS_kord
        0, // INS_korq
        0, // INS_kortestb
        0, // INS_kortestd
        0, // INS_kortestq
        0, // INS_kortestw
        0, // INS_korw
        0, // INS_kshiftlb
        0, // INS_kshiftld
        0, // INS_kshiftlq
        0, // INS_kshiftlw
        0, // INS_kshiftrb
        0, // INS_kshiftrd
        0, // INS_kshiftrq
        0, // INS_kshiftrw
        0, // INS_ktestb
        0, // INS_ktestd
        0, // INS_ktestq
        0, // INS_ktestw
        0, // INS_kunpckbw
        0, // INS_kunpckdq
        0, // INS_kunpckwd
        0, // INS_kxnorb
        0, // INS_kxnord
        0, // INS_kxnorq
        0, // INS_kxnorw
        0, // INS_kxorb
        0, // INS_kxord
        0, // INS_kxorq
        0, // INS_kxorw
        0, // INS_valignd
        0, // INS_valignq
        0, // INS_vblendmpd
        0, // INS_vblendmps
        0, // INS_vbroadcastf32x2
        0, // INS_vbroadcastf32x8
        0, // INS_vbroadcastf64x2
        0, // INS_vbroadcastf64x4
        0, // INS_vbroadcasti32x2
        0, // INS_vbroadcasti32x8
        0, // INS_vbroadcasti64x2
        0, // INS_vbroadcasti64x4
        0, // INS_vcmppd
        0, // INS_vcmpps
        0, // INS_vcmpsd
        0, // INS_vcmpss
        0, // INS_vcompresspd
        0, // INS_vcompressps
        0, // INS_vcvtpd2qq
        0, // INS_vcvtpd2udq
        0, // INS_vcvtpd2uqq
        0, // INS_vcvtps2qq
        0, // INS_vcvtps2udq
        0, // INS_vcvtps2uqq
        0, // INS_vcvtqq2pd
        0, // INS_vcvtqq2ps
        0, // INS_vcvtsd2usi32
        0, // INS_vcvtsd2usi64
        0, // INS_vcvtss2usi32
        0, // INS_vcvtss2usi64
        0, // INS_vcvttpd2qq
        0, // INS_vcvttpd2udq
        0, // INS_vcvttpd2uqq
        0, // INS_vcvttps2qq
        0, // INS_vcvttps2udq
        0, // INS_vcvttps2uqq
        0, // INS_vcvttsd2usi32
        0, // INS_vcvttsd2usi64
        0, // INS_vcvttss2usi32
        0, // INS_vcvttss2usi64
        0, // INS_vcvtudq2pd
        0, // INS_vcvtudq2ps
        0, // INS_vcvtuqq2pd
        0, // INS_vcvtuqq2ps
        0, // INS_vcvtusi2sd32
        0, // INS_vcvtusi2sd64
        0, // INS_vcvtusi2ss32
        0, // INS_vcvtusi2ss64
        0, // INS_vdbpsadbw
        0, // INS_vexpandpd
        0, // INS_vexpandps
        0, // INS_vextractf32x8
        0, // INS_vextractf64x2
        0, // INS_vextractf64x4
        0, // INS_vextracti32x8
        0, // INS_vextracti64x2
        0, // INS_vextracti64x4
        0, // INS_vfixupimmpd
        0, // INS_vfixupimmps
        0, // INS_vfixupimmsd
        0, // INS_vfixupimmss
        0, // INS_vfpclasspd
        0, // INS_vfpclassps
        0, // INS_vfpclasssd
        0, // INS_vfpclassss
        0, // INS_vgatherdpd_msk
        0, // INS_vgatherdps_msk
        0, // INS_vgatherqpd_msk
        0, // INS_vgatherqps_msk
        0, // INS_vgetexppd
        0, // INS_vgetexpps
        0, // INS_vgetexpsd
        0, // INS_vgetexpss
        0, // INS_vgetmantpd
        0, // INS_vgetmantps
        0, // INS_vgetmantsd
        0, // INS_vgetmantss
        0, // INS_vinsertf32x8
        0, // INS_vinsertf64x2
        0, // INS_vinsertf64x4
        0, // INS_vinserti32x8
        0, // INS_vinserti64x2
        0, // INS_vinserti64x4
        0, // INS_vmovdqa64
        0, // INS_vmovdqu16
        0, // INS_vmovdqu64
        0, // INS_vmovdqu8
        0, // INS_vpabsq
        0, // INS_vpandnq
        0, // INS_vpandq
        0, // INS_vpblendmb
        0, // INS_vpblendmd
        0, // INS_vpblendmq
        0, // INS_vpblendmw
        0, // INS_vpbroadcastb_gpr
        0, // INS_vpbroadcastd_gpr
        0, // INS_vpbroadcastq_gpr
        0, // INS_vpbroadcastw_gpr
        0, // INS_vpcmpb
        0, // INS_vpcmpd
        0, // INS_vpcmpeqb
        0, // INS_vpcmpeqd
        0, // INS_vpcmpeqq
        0, // INS_vpcmpeqw
        0, // INS_vpcmpgtb
        0, // INS_vpcmpgtd
        0, // INS_vpcmpgtq
        0, // INS_vpcmpgtw
        0, // INS_vpcmpq
        0, // INS_vpcmpub
        0, // INS_vpcmpud
        0, // INS_vpcmpuq
        0, // INS_vpcmpuw
        0, // INS_vpcmpw
        0, // INS_vpcompressd
        0, // INS_vpcompressq
        0, // INS_vpconflictd
        0, // INS_vpconflictq
        0, // INS_vpermi2d
        0, // INS_vpermi2pd
        0, // INS_vpermi2ps
        0, // INS_vpermi2q
        0, // INS_vpermi2w
        0, // INS_vpermpd_reg
        0, // INS_vpermq_reg
        0, // INS_vpermt2d
        0, // INS_vpermt2pd
        0, // INS_vpermt2ps
        0, // INS_vpermt2q
        0, // INS_vpermt2w
        0, // INS_vpermw
        0, // INS_vpexpandd
        0, // INS_vpexpandq
        0, // INS_vpgatherdd_msk
        0, // INS_vpgatherdq_msk
        0, // INS_vpgatherqd_msk
        0, // INS_vpgatherqq_msk
        0, // INS_vplzcntd
        0, // INS_vplzcntq
        0, // INS_vpmaxsq
        0, // INS_vpmaxuq
        0, // INS_vpminsq
        0, // INS_vpminuq
        0, // INS_vpmovb2m
        0, // INS_vpmovd2m
        0, // INS_vpmovdb
        0, // INS_vpmovdw
        0, // INS_vpmovm2b
        0, // INS_vpmovm2d
        0, // INS_vpmovm2q
        0, // INS_vpmovm2w
        0, // INS_vpmovq2m
        0, // INS_vpmovqb
        0, // INS_vpmovqd
        0, // INS_vpmovqw
        0, // INS_vpmovsdb
        0, // INS_vpmovsdw
        0, // INS_vpmovsqb
        0, // INS_vpmovsqd
        0, // INS_vpmovsqw
        0, // INS_vpmovswb
        0, // INS_vpmovusdb
        0, // INS_vpmovusdw
        0, // INS_vpmovusqb
        0, // INS_vpmovusqd
        0, // INS_vpmovusqw
        0, // INS_vpmovuswb
        0, // INS_vpmovw2m
        0, // INS_vpmovwb
        0, // INS_vpmullq
        0, // INS_vporq
        0, // INS_vprold
        0, // INS_vprolq
        0, // INS_vprolvd
        0, // INS_vprolvq
        0, // INS_vprord
        0, // INS_vprorq
        0, // INS_vprorvd
        0, // INS_vprorvq
        0, // INS_vpscatterdd_msk
        0, // INS_vpscatterdq_msk
        0, // INS_vpscatterqd_msk
        0, // INS_vpscatterqq_msk
        0, // INS_vpsllvw
        0, // INS_vpsraq
        0, // INS_vpsravq
        0, // INS_vpsravw
        0, // INS_vpsrlvw
        0, // INS_vpternlogd
        0, // INS_vpternlogq
        0, // INS_vptestmb
        0, // INS_vptestmd
        0, // INS_vptestmq
        0, // INS_vptestmw
        0, // INS_vptestnmb
        0, // INS_vptestnmd
        0, // INS_vptestnmq
        0, // INS_vptestnmw
        0, // INS_vpxorq
        0, // INS_vrangepd
        0, // INS_vrangeps
        0, // INS_vrangesd
        0, // INS_vrangess
        0, // INS_vrcp14pd
        0, // INS_vrcp14ps
        0, // INS_vrcp14sd
        0, // INS_vrcp14ss
        0, // INS_vreducepd
        0, // INS_vreduceps
        0, // INS_vreducesd
        0, // INS_vreducess
        0, // INS_vrndscalepd
        0, // INS_vrndscaleps
        0, // INS_vrndscalesd
        0, // INS_vrndscaless
        0, // INS_vrsqrt14pd
        0, // INS_vrsqrt14ps
        0, // INS_vrsqrt14sd
        0, // INS_vrsqrt14ss
        0, // INS_vscalefpd
        0, // INS_vscalefps
        0, // INS_vscalefsd
        0, // INS_vscalefss
        0, // INS_vscatterdpd_msk
        0, // INS_vscatterdps_msk
        0, // INS_vscatterqpd_msk
        0, // INS_vscatterqps_msk
        0, // INS_vshuff32x4
        0, // INS_vshuff64x2
        0, // INS_vshufi32x4
        0, // INS_vshufi64x2
        0, // INS_vpermb
        0, // INS_vpermi2b
        0, // INS_vpermt2b
        0, // INS_vpmultishiftqb
        0, // INS_vpcompressb
        0, // INS_vpcompressw
        0, // INS_vpexpandb
        0, // INS_vpexpandw
        0, // INS_vpopcntb
        0, // INS_vpopcntd
        0, // INS_vpopcntq
        0, // INS_vpopcntw
        0, // INS_vpshldd
        0, // INS_vpshldq
        0, // INS_vpshldvd
        0, // INS_vpshldvq
        0, // INS_vpshldvw
        0, // INS_vpshldw
        0, // INS_vpshrdd
        0, // INS_vpshrdq
        0, // INS_vpshrdvd
        0, // INS_vpshrdvq
        0, // INS_vpshrdvw
        0, // INS_vpshrdw
        0, // INS_vpshufbitqmb
        0, // INS_vaddph
        0, // INS_vaddsh
        0, // INS_vcmpph
        0, // INS_vcmpsh
        0, // INS_vcomish
        0, // INS_vcvtdq2ph
        0, // INS_vcvtne2ps2bf16
        0, // INS_vcvtneps2bf16
        0, // INS_vcvtpd2ph
        0, // INS_vcvtph2dq
        0, // INS_vcvtph2pd
        0, // INS_vcvtph2psx
        0, // INS_vcvtph2qq
        0, // INS_vcvtph2udq
        0, // INS_vcvtph2uqq
        0, // INS_vcvtph2uw
        0, // INS_vcvtph2w
        0, // INS_vcvtps2phx
        0, // INS_vcvtqq2ph
        0, // INS_vcvtsd2sh
        0, // INS_vcvtsh2sd
        0, // INS_vcvtsh2si32
        0, // INS_vcvtsh2si64
        0, // INS_vcvtsh2ss
        0, // INS_vcvtsh2usi32
        0, // INS_vcvtsh2usi64
        0, // INS_vcvtsi2sh32
        0, // INS_vcvtsi2sh64
        0, // INS_vcvtss2sh
        0, // INS_vcvttph2dq
        0, // INS_vcvttph2qq
        0, // INS_vcvttph2udq
        0, // INS_vcvttph2uqq
        0, // INS_vcvttph2uw
        0, // INS_vcvttph2w
        0, // INS_vcvttsh2si32
        0, // INS_vcvttsh2si64
        0, // INS_vcvttsh2usi32
        0, // INS_vcvttsh2usi64
        0, // INS_vcvtudq2ph
        0, // INS_vcvtuqq2ph
        0, // INS_vcvtusi2sh32
        0, // INS_vcvtusi2sh64
        0, // INS_vcvtuw2ph
        0, // INS_vcvtw2ph
        0, // INS_vdivph
        0, // INS_vdivsh
        0, // INS_vdpbf16ps
        0, // INS_vfcmaddcph
        0, // INS_vfcmaddcsh
        0, // INS_vfcmulcph
        0, // INS_vfcmulcsh
        0, // INS_vfmulcph
        0, // INS_vfmulcsh
        0, // INS_vfmadd132ph
        0, // INS_vfmadd213ph
        0, // INS_vfmadd231ph
        0, // INS_vfmadd132sh
        0, // INS_vfmadd213sh
        0, // INS_vfmadd231sh
        0, // INS_vfmaddcph
        0, // INS_vfmaddcsh
        0, // INS_vfmaddsub132ph
        0, // INS_vfmaddsub213ph
        0, // INS_vfmaddsub231ph
        0, // INS_vfmsub132ph
        0, // INS_vfmsub213ph
        0, // INS_vfmsub231ph
        0, // INS_vfmsub132sh
        0, // INS_vfmsub213sh
        0, // INS_vfmsub231sh
        0, // INS_vfmsubadd132ph
        0, // INS_vfmsubadd213ph
        0, // INS_vfmsubadd231ph
        0, // INS_vfnmadd132ph
        0, // INS_vfnmadd213ph
        0, // INS_vfnmadd231ph
        0, // INS_vfnmadd132sh
        0, // INS_vfnmadd213sh
        0, // INS_vfnmadd231sh
        0, // INS_vfnmsub132ph
        0, // INS_vfnmsub213ph
        0, // INS_vfnmsub231ph
        0, // INS_vfnmsub132sh
        0, // INS_vfnmsub213sh
        0, // INS_vfnmsub231sh
        0, // INS_vfpclassph
        0, // INS_vfpclasssh
        0, // INS_vgetexpph
        0, // INS_vgetexpsh
        0, // INS_vgetmantph
        0, // INS_vgetmantsh
        0, // INS_vmaxph
        0, // INS_vmaxsh
        0, // INS_vminsh
        0, // INS_vminph
        0, // INS_vmovsh
        0, // INS_vmovw
        0, // INS_vmulph
        0, // INS_vmulsh
        0, // INS_vrcpph
        0, // INS_vrcpsh
        0, // INS_vreduceph
        0, // INS_vreducesh
        0, // INS_vrndscaleph
        0, // INS_vrndscalesh
        0, // INS_vrsqrtph
        0, // INS_vrsqrtsh
        0, // INS_vscalefph
        0, // INS_vscalefsh
        0, // INS_vsqrtph
        0, // INS_vsqrtsh
        0, // INS_vsubph
        0, // INS_vsubsh
        0, // INS_vucomish
        0, // INS_vp2intersectd
        0, // INS_vp2intersectq
        0, // INS_vcomxsd
        0, // INS_vcomxss
        0, // INS_vcvtps2ibs
        0, // INS_vcvtps2iubs
        0, // INS_vcvttpd2dqs
        0, // INS_vcvttpd2qqs
        0, // INS_vcvttpd2udqs
        0, // INS_vcvttpd2uqqs
        0, // INS_vcvttps2dqs
        0, // INS_vcvttps2ibs
        0, // INS_vcvttps2iubs
        0, // INS_vcvttps2qqs
        0, // INS_vcvttps2udqs
        0, // INS_vcvttps2uqqs
        0, // INS_vcvttsd2sis32
        0, // INS_vcvttsd2sis64
        0, // INS_vcvttsd2usis32
        0, // INS_vcvttsd2usis64
        0, // INS_vcvttss2sis32
        0, // INS_vcvttss2sis64
        0, // INS_vcvttss2usis32
        0, // INS_vcvttss2usis64
        0, // INS_vminmaxpd
        0, // INS_vminmaxps
        0, // INS_vminmaxsd
        0, // INS_vminmaxss
        0, // INS_vmovd_simd
        0, // INS_vmovw_simd
        0, // INS_vmpsadbw
        0, // INS_vucomxsd
        0, // INS_vucomxss
        0, // INS_crc32
        0, // INS_tzcnt
        0, // INS_lzcnt
        0, // INS_movbe
        0, // INS_popcnt
        0, // INS_tpause
        0, // INS_umonitor
        0, // INS_umwait
        1, // INS_neg
        1, // INS_not
        1, // INS_rol
        1, // INS_rol_1
        1, // INS_rol_N
        1, // INS_ror
        1, // INS_ror_1
        1, // INS_ror_N
        1, // INS_rcl
        1, // INS_rcl_1
        1, // INS_rcl_N
        1, // INS_rcr
        1, // INS_rcr_1
        1, // INS_rcr_N
        1, // INS_shl
        1, // INS_shl_1
        1, // INS_shl_N
        1, // INS_shr
        1, // INS_shr_1
        1, // INS_shr_N
        1, // INS_sar
        1, // INS_sar_1
        1, // INS_sar_N
        0, // INS_ret
        0, // INS_loop
        0, // INS_call
        0, // INS_r_movsb
        0, // INS_r_movsd
        0, // INS_movsb
        0, // INS_movsd
        0, // INS_r_stosb
        0, // INS_r_stosd
        0, // INS_stosb
        0, // INS_stosd
        0, // INS_int3
        0, // INS_nop
        0, // INS_pause
        0, // INS_lock
        0, // INS_leave
        0, // INS_serialize
        0, // INS_cwde
        0, // INS_cdq
        0, // INS_idiv
        0, // INS_imulEAX
        0, // INS_div
        0, // INS_mulEAX
        0, // INS_sahf
        0, // INS_xadd
        0, // INS_cmpxchg
        0, // INS_shld
        0, // INS_shrd
        0, // INS_fld
        0, // INS_fstp
        0, // INS_seto
        0, // INS_setno
        0, // INS_setb
        0, // INS_setae
        0, // INS_sete
        0, // INS_setne
        0, // INS_setbe
        0, // INS_seta
        0, // INS_sets
        0, // INS_setns
        0, // INS_setp
        0, // INS_setnp
        0, // INS_setl
        0, // INS_setge
        0, // INS_setle
        0, // INS_setg
        0, // INS_tail_i_jmp
        0, // INS_i_jmp
        0, // INS_jmp
        0, // INS_jo
        0, // INS_jno
        0, // INS_jb
        0, // INS_jae
        0, // INS_je
        0, // INS_jne
        0, // INS_jbe
        0, // INS_ja
        0, // INS_js
        0, // INS_jns
        0, // INS_jp
        0, // INS_jnp
        0, // INS_jl
        0, // INS_jge
        0, // INS_jle
        0, // INS_jg
        0, // INS_l_jmp
        0, // INS_l_jo
        0, // INS_l_jno
        0, // INS_l_jb
        0, // INS_l_jae
        0, // INS_l_je
        0, // INS_l_jne
        0, // INS_l_jbe
        0, // INS_l_ja
        0, // INS_l_js
        0, // INS_l_jns
        0, // INS_l_jp
        0, // INS_l_jnp
        0, // INS_l_jl
        0, // INS_l_jge
        0, // INS_l_jle
        0, // INS_l_jg
        0, // INS_align
        0, // INS_data16
    ];

    private static ReadOnlySpan<byte> s_amd64InstructionNddFlags => [
        0, // INS_invalid
        0, // INS_push
        0, // INS_pop
        0, // INS_push_hide
        0, // INS_pop_hide
        1, // INS_push2
        1, // INS_pop2
        1, // INS_inc
        0, // INS_inc_l
        1, // INS_dec
        0, // INS_dec_l
        0, // INS_bswap
        1, // INS_add
        1, // INS_or
        0, // INS_adc
        0, // INS_sbb
        1, // INS_and
        1, // INS_sub
        0, // INS_sub_hide
        1, // INS_xor
        0, // INS_cmp
        0, // INS_test
        0, // INS_mov
        0, // INS_lea
        0, // INS_bt
        0, // INS_bts
        0, // INS_btr
        0, // INS_btc
        0, // INS_bsr
        0, // INS_bsf
        0, // INS_movsx
        0, // INS_movsxd
        0, // INS_movzx
        1, // INS_cmovo
        1, // INS_cmovno
        1, // INS_cmovb
        1, // INS_cmovae
        1, // INS_cmove
        1, // INS_cmovne
        1, // INS_cmovbe
        1, // INS_cmova
        1, // INS_cmovs
        1, // INS_cmovns
        1, // INS_cmovp
        1, // INS_cmovnp
        1, // INS_cmovl
        1, // INS_cmovge
        1, // INS_cmovle
        1, // INS_cmovg
        0, // INS_xchg
        1, // INS_imul
        0, // INS_imul_AX
        0, // INS_imul_CX
        0, // INS_imul_DX
        0, // INS_imul_BX
        0, // INS_imul_SP
        0, // INS_imul_BP
        0, // INS_imul_SI
        0, // INS_imul_DI
        0, // INS_imul_08
        0, // INS_imul_09
        0, // INS_imul_10
        0, // INS_imul_11
        0, // INS_imul_12
        0, // INS_imul_13
        0, // INS_imul_14
        0, // INS_imul_15
        0, // INS_imul_16
        0, // INS_imul_17
        0, // INS_imul_18
        0, // INS_imul_19
        0, // INS_imul_20
        0, // INS_imul_21
        0, // INS_imul_22
        0, // INS_imul_23
        0, // INS_imul_24
        0, // INS_imul_25
        0, // INS_imul_26
        0, // INS_imul_27
        0, // INS_imul_28
        0, // INS_imul_29
        0, // INS_imul_30
        0, // INS_imul_31
        0, // INS_addpd
        0, // INS_addps
        0, // INS_addsd
        0, // INS_addss
        0, // INS_addsubpd
        0, // INS_addsubps
        0, // INS_andnpd
        0, // INS_andnps
        0, // INS_andpd
        0, // INS_andps
        0, // INS_blendpd
        0, // INS_blendps
        0, // INS_blendvpd
        0, // INS_blendvps
        0, // INS_cmppd
        0, // INS_cmpps
        0, // INS_cmpsd
        0, // INS_cmpss
        0, // INS_comisd
        0, // INS_comiss
        0, // INS_cvtdq2pd
        0, // INS_cvtdq2ps
        0, // INS_cvtpd2dq
        0, // INS_cvtpd2ps
        0, // INS_cvtps2dq
        0, // INS_cvtps2pd
        0, // INS_cvtsd2si32
        0, // INS_cvtsd2si64
        0, // INS_cvtsd2ss
        0, // INS_cvtsi2sd32
        0, // INS_cvtsi2sd64
        0, // INS_cvtsi2ss32
        0, // INS_cvtsi2ss64
        0, // INS_cvtss2sd
        0, // INS_cvtss2si32
        0, // INS_cvtss2si64
        0, // INS_cvttpd2dq
        0, // INS_cvttps2dq
        0, // INS_cvttsd2si32
        0, // INS_cvttsd2si64
        0, // INS_cvttss2si32
        0, // INS_cvttss2si64
        0, // INS_divpd
        0, // INS_divps
        0, // INS_divsd
        0, // INS_divss
        0, // INS_dppd
        0, // INS_dpps
        0, // INS_extractps
        0, // INS_haddpd
        0, // INS_haddps
        0, // INS_hsubpd
        0, // INS_hsubps
        0, // INS_insertps
        0, // INS_lddqu
        0, // INS_lfence
        0, // INS_maskmovdqu
        0, // INS_maxpd
        0, // INS_maxps
        0, // INS_maxsd
        0, // INS_maxss
        0, // INS_mfence
        0, // INS_minpd
        0, // INS_minps
        0, // INS_minsd
        0, // INS_minss
        0, // INS_movapd
        0, // INS_movaps
        0, // INS_movd32
        0, // INS_movd64
        0, // INS_movddup
        0, // INS_movdqa32
        0, // INS_movdqu32
        0, // INS_movhlps
        0, // INS_movhpd
        0, // INS_movhps
        0, // INS_movlhps
        0, // INS_movlpd
        0, // INS_movlps
        0, // INS_movmskpd
        0, // INS_movmskps
        0, // INS_movntdq
        0, // INS_movntdqa
        0, // INS_movnti32
        0, // INS_movnti64
        0, // INS_movntpd
        0, // INS_movntps
        0, // INS_movq
        0, // INS_movsd_simd
        0, // INS_movshdup
        0, // INS_movsldup
        0, // INS_movss
        0, // INS_movupd
        0, // INS_movups
        0, // INS_mpsadbw
        0, // INS_mulpd
        0, // INS_mulps
        0, // INS_mulsd
        0, // INS_mulss
        0, // INS_orpd
        0, // INS_orps
        0, // INS_pabsb
        0, // INS_pabsd
        0, // INS_pabsw
        0, // INS_packssdw
        0, // INS_packsswb
        0, // INS_packusdw
        0, // INS_packuswb
        0, // INS_paddb
        0, // INS_paddd
        0, // INS_paddq
        0, // INS_paddsb
        0, // INS_paddsw
        0, // INS_paddusb
        0, // INS_paddusw
        0, // INS_paddw
        0, // INS_palignr
        0, // INS_pandd
        0, // INS_pandnd
        0, // INS_pavgb
        0, // INS_pavgw
        0, // INS_pblendvb
        0, // INS_pblendw
        0, // INS_pcmpeqb
        0, // INS_pcmpeqd
        0, // INS_pcmpeqq
        0, // INS_pcmpeqw
        0, // INS_pcmpgtb
        0, // INS_pcmpgtd
        0, // INS_pcmpgtq
        0, // INS_pcmpgtw
        0, // INS_pextrb
        0, // INS_pextrd
        0, // INS_pextrq
        0, // INS_phaddd
        0, // INS_pextrw
        0, // INS_phaddsw
        0, // INS_phaddw
        0, // INS_phminposuw
        0, // INS_phsubd
        0, // INS_phsubsw
        0, // INS_phsubw
        0, // INS_pinsrb
        0, // INS_pinsrd
        0, // INS_pinsrq
        0, // INS_pinsrw
        0, // INS_pmaddubsw
        0, // INS_pmaddwd
        0, // INS_pmaxsb
        0, // INS_pmaxsd
        0, // INS_pmaxsw
        0, // INS_pmaxub
        0, // INS_pmaxud
        0, // INS_pmaxuw
        0, // INS_pminsb
        0, // INS_pminsd
        0, // INS_pminsw
        0, // INS_pminub
        0, // INS_pminud
        0, // INS_pminuw
        0, // INS_pmovmskb
        0, // INS_pmovsxbd
        0, // INS_pmovsxbq
        0, // INS_pmovsxbw
        0, // INS_pmovsxdq
        0, // INS_pmovsxwd
        0, // INS_pmovsxwq
        0, // INS_pmovzxbd
        0, // INS_pmovzxbq
        0, // INS_pmovzxbw
        0, // INS_pmovzxdq
        0, // INS_pmovzxwd
        0, // INS_pmovzxwq
        0, // INS_pmuldq
        0, // INS_pmulhrsw
        0, // INS_pmulhuw
        0, // INS_pmulhw
        0, // INS_pmulld
        0, // INS_pmuludq
        0, // INS_pmullw
        0, // INS_pord
        0, // INS_prefetchnta
        0, // INS_prefetcht0
        0, // INS_prefetcht1
        0, // INS_prefetcht2
        0, // INS_psadbw
        0, // INS_pshufb
        0, // INS_pshufd
        0, // INS_pshufhw
        0, // INS_pshuflw
        0, // INS_psignb
        0, // INS_psignd
        0, // INS_psignw
        0, // INS_pslld
        0, // INS_pslldq
        0, // INS_psllq
        0, // INS_psllw
        0, // INS_psrad
        0, // INS_psraw
        0, // INS_psrld
        0, // INS_psrldq
        0, // INS_psrlq
        0, // INS_psrlw
        0, // INS_psubb
        0, // INS_psubd
        0, // INS_psubq
        0, // INS_psubsb
        0, // INS_psubsw
        0, // INS_psubusb
        0, // INS_psubusw
        0, // INS_psubw
        0, // INS_ptest
        0, // INS_punpckhbw
        0, // INS_punpckhdq
        0, // INS_punpckhqdq
        0, // INS_punpckhwd
        0, // INS_punpcklbw
        0, // INS_punpckldq
        0, // INS_punpcklqdq
        0, // INS_punpcklwd
        0, // INS_pxord
        0, // INS_rcpps
        0, // INS_rcpss
        0, // INS_roundpd
        0, // INS_roundps
        0, // INS_roundsd
        0, // INS_roundss
        0, // INS_rsqrtps
        0, // INS_rsqrtss
        0, // INS_sfence
        0, // INS_shufpd
        0, // INS_shufps
        0, // INS_sqrtpd
        0, // INS_sqrtps
        0, // INS_sqrtsd
        0, // INS_sqrtss
        0, // INS_subpd
        0, // INS_subps
        0, // INS_subsd
        0, // INS_subss
        0, // INS_ucomisd
        0, // INS_ucomiss
        0, // INS_unpckhpd
        0, // INS_unpckhps
        0, // INS_unpcklpd
        0, // INS_unpcklps
        0, // INS_xorpd
        0, // INS_xorps
        0, // INS_aesdec
        0, // INS_aesdeclast
        0, // INS_aesenc
        0, // INS_aesenclast
        0, // INS_aesimc
        0, // INS_aeskeygenassist
        0, // INS_pclmulqdq
        0, // INS_sha1msg1
        0, // INS_sha1msg2
        0, // INS_sha1nexte
        0, // INS_sha1rnds4
        0, // INS_sha256msg1
        0, // INS_sha256msg2
        0, // INS_sha256rnds2
        0, // INS_gf2p8affineinvqb
        0, // INS_gf2p8affineqb
        0, // INS_gf2p8mulb
        0, // INS_vblendvpd
        0, // INS_vblendvps
        0, // INS_vbroadcastf32x4
        0, // INS_vbroadcastsd
        0, // INS_vbroadcastss
        0, // INS_vextractf32x4
        0, // INS_vinsertf32x4
        0, // INS_vmaskmovpd
        0, // INS_vmaskmovps
        0, // INS_vpblendvb
        0, // INS_vperm2f128
        0, // INS_vpermilpd
        0, // INS_vpermilpdvar
        0, // INS_vpermilps
        0, // INS_vpermilpsvar
        0, // INS_vtestpd
        0, // INS_vtestps
        0, // INS_vzeroupper
        0, // INS_vbroadcasti32x4
        0, // INS_vcvtph2ps
        0, // INS_vcvtps2ph
        0, // INS_vextracti32x4
        0, // INS_vgatherdpd
        0, // INS_vgatherdps
        0, // INS_vgatherqpd
        0, // INS_vgatherqps
        0, // INS_vinserti32x4
        0, // INS_vpblendd
        0, // INS_vpbroadcastb
        0, // INS_vpbroadcastd
        0, // INS_vpbroadcastq
        0, // INS_vpbroadcastw
        0, // INS_vperm2i128
        0, // INS_vpermd
        0, // INS_vpermpd
        0, // INS_vpermps
        0, // INS_vpermq
        0, // INS_vpgatherdd
        0, // INS_vpgatherdq
        0, // INS_vpgatherqd
        0, // INS_vpgatherqq
        0, // INS_vpmaskmovd
        0, // INS_vpmaskmovq
        0, // INS_vpsllvd
        0, // INS_vpsllvq
        0, // INS_vpsravd
        0, // INS_vpsrlvd
        0, // INS_vpsrlvq
        0, // INS_vfmadd132pd
        0, // INS_vfmadd213pd
        0, // INS_vfmadd231pd
        0, // INS_vfmadd132ps
        0, // INS_vfmadd213ps
        0, // INS_vfmadd231ps
        0, // INS_vfmadd132sd
        0, // INS_vfmadd213sd
        0, // INS_vfmadd231sd
        0, // INS_vfmadd132ss
        0, // INS_vfmadd213ss
        0, // INS_vfmadd231ss
        0, // INS_vfmaddsub132pd
        0, // INS_vfmaddsub213pd
        0, // INS_vfmaddsub231pd
        0, // INS_vfmaddsub132ps
        0, // INS_vfmaddsub213ps
        0, // INS_vfmaddsub231ps
        0, // INS_vfmsubadd132pd
        0, // INS_vfmsubadd213pd
        0, // INS_vfmsubadd231pd
        0, // INS_vfmsubadd132ps
        0, // INS_vfmsubadd213ps
        0, // INS_vfmsubadd231ps
        0, // INS_vfmsub132pd
        0, // INS_vfmsub213pd
        0, // INS_vfmsub231pd
        0, // INS_vfmsub132ps
        0, // INS_vfmsub213ps
        0, // INS_vfmsub231ps
        0, // INS_vfmsub132sd
        0, // INS_vfmsub213sd
        0, // INS_vfmsub231sd
        0, // INS_vfmsub132ss
        0, // INS_vfmsub213ss
        0, // INS_vfmsub231ss
        0, // INS_vfnmadd132pd
        0, // INS_vfnmadd213pd
        0, // INS_vfnmadd231pd
        0, // INS_vfnmadd132ps
        0, // INS_vfnmadd213ps
        0, // INS_vfnmadd231ps
        0, // INS_vfnmadd132sd
        0, // INS_vfnmadd213sd
        0, // INS_vfnmadd231sd
        0, // INS_vfnmadd132ss
        0, // INS_vfnmadd213ss
        0, // INS_vfnmadd231ss
        0, // INS_vfnmsub132pd
        0, // INS_vfnmsub213pd
        0, // INS_vfnmsub231pd
        0, // INS_vfnmsub132ps
        0, // INS_vfnmsub213ps
        0, // INS_vfnmsub231ps
        0, // INS_vfnmsub132sd
        0, // INS_vfnmsub213sd
        0, // INS_vfnmsub231sd
        0, // INS_vfnmsub132ss
        0, // INS_vfnmsub213ss
        0, // INS_vfnmsub231ss
        0, // INS_andn
        0, // INS_bextr
        0, // INS_blsi
        0, // INS_blsmsk
        0, // INS_blsr
        0, // INS_bzhi
        0, // INS_mulx
        0, // INS_pdep
        0, // INS_pext
        0, // INS_rorx
        0, // INS_sarx
        0, // INS_shlx
        0, // INS_shrx
        0, // INS_vpdpbusd
        0, // INS_vpdpbusds
        0, // INS_vpdpwssd
        0, // INS_vpdpwssds
        0, // INS_vpdpwsud
        0, // INS_vpdpwsuds
        0, // INS_vpdpwusd
        0, // INS_vpdpwusds
        0, // INS_vpdpwuud
        0, // INS_vpdpwuuds
        0, // INS_vpdpbssd
        0, // INS_vpdpbssds
        0, // INS_vpdpbsud
        0, // INS_vpdpbsuds
        0, // INS_vpdpbuud
        0, // INS_vpdpbuuds
        0, // INS_vbmacor16x16x16
        0, // INS_vbmacxor16x16x16
        0, // INS_vbitrev
        0, // INS_vpmadd52huq
        0, // INS_vpmadd52luq
        0, // INS_kaddb
        0, // INS_kaddd
        0, // INS_kaddq
        0, // INS_kaddw
        0, // INS_kandb
        0, // INS_kandd
        0, // INS_kandnb
        0, // INS_kandnd
        0, // INS_kandnq
        0, // INS_kandnw
        0, // INS_kandq
        0, // INS_kandw
        0, // INS_kmovb_gpr
        0, // INS_kmovb_msk
        0, // INS_kmovd_gpr
        0, // INS_kmovd_msk
        0, // INS_kmovq_gpr
        0, // INS_kmovq_msk
        0, // INS_kmovw_gpr
        0, // INS_kmovw_msk
        0, // INS_knotb
        0, // INS_knotd
        0, // INS_knotq
        0, // INS_knotw
        0, // INS_korb
        0, // INS_kord
        0, // INS_korq
        0, // INS_kortestb
        0, // INS_kortestd
        0, // INS_kortestq
        0, // INS_kortestw
        0, // INS_korw
        0, // INS_kshiftlb
        0, // INS_kshiftld
        0, // INS_kshiftlq
        0, // INS_kshiftlw
        0, // INS_kshiftrb
        0, // INS_kshiftrd
        0, // INS_kshiftrq
        0, // INS_kshiftrw
        0, // INS_ktestb
        0, // INS_ktestd
        0, // INS_ktestq
        0, // INS_ktestw
        0, // INS_kunpckbw
        0, // INS_kunpckdq
        0, // INS_kunpckwd
        0, // INS_kxnorb
        0, // INS_kxnord
        0, // INS_kxnorq
        0, // INS_kxnorw
        0, // INS_kxorb
        0, // INS_kxord
        0, // INS_kxorq
        0, // INS_kxorw
        0, // INS_valignd
        0, // INS_valignq
        0, // INS_vblendmpd
        0, // INS_vblendmps
        0, // INS_vbroadcastf32x2
        0, // INS_vbroadcastf32x8
        0, // INS_vbroadcastf64x2
        0, // INS_vbroadcastf64x4
        0, // INS_vbroadcasti32x2
        0, // INS_vbroadcasti32x8
        0, // INS_vbroadcasti64x2
        0, // INS_vbroadcasti64x4
        0, // INS_vcmppd
        0, // INS_vcmpps
        0, // INS_vcmpsd
        0, // INS_vcmpss
        0, // INS_vcompresspd
        0, // INS_vcompressps
        0, // INS_vcvtpd2qq
        0, // INS_vcvtpd2udq
        0, // INS_vcvtpd2uqq
        0, // INS_vcvtps2qq
        0, // INS_vcvtps2udq
        0, // INS_vcvtps2uqq
        0, // INS_vcvtqq2pd
        0, // INS_vcvtqq2ps
        0, // INS_vcvtsd2usi32
        0, // INS_vcvtsd2usi64
        0, // INS_vcvtss2usi32
        0, // INS_vcvtss2usi64
        0, // INS_vcvttpd2qq
        0, // INS_vcvttpd2udq
        0, // INS_vcvttpd2uqq
        0, // INS_vcvttps2qq
        0, // INS_vcvttps2udq
        0, // INS_vcvttps2uqq
        0, // INS_vcvttsd2usi32
        0, // INS_vcvttsd2usi64
        0, // INS_vcvttss2usi32
        0, // INS_vcvttss2usi64
        0, // INS_vcvtudq2pd
        0, // INS_vcvtudq2ps
        0, // INS_vcvtuqq2pd
        0, // INS_vcvtuqq2ps
        0, // INS_vcvtusi2sd32
        0, // INS_vcvtusi2sd64
        0, // INS_vcvtusi2ss32
        0, // INS_vcvtusi2ss64
        0, // INS_vdbpsadbw
        0, // INS_vexpandpd
        0, // INS_vexpandps
        0, // INS_vextractf32x8
        0, // INS_vextractf64x2
        0, // INS_vextractf64x4
        0, // INS_vextracti32x8
        0, // INS_vextracti64x2
        0, // INS_vextracti64x4
        0, // INS_vfixupimmpd
        0, // INS_vfixupimmps
        0, // INS_vfixupimmsd
        0, // INS_vfixupimmss
        0, // INS_vfpclasspd
        0, // INS_vfpclassps
        0, // INS_vfpclasssd
        0, // INS_vfpclassss
        0, // INS_vgatherdpd_msk
        0, // INS_vgatherdps_msk
        0, // INS_vgatherqpd_msk
        0, // INS_vgatherqps_msk
        0, // INS_vgetexppd
        0, // INS_vgetexpps
        0, // INS_vgetexpsd
        0, // INS_vgetexpss
        0, // INS_vgetmantpd
        0, // INS_vgetmantps
        0, // INS_vgetmantsd
        0, // INS_vgetmantss
        0, // INS_vinsertf32x8
        0, // INS_vinsertf64x2
        0, // INS_vinsertf64x4
        0, // INS_vinserti32x8
        0, // INS_vinserti64x2
        0, // INS_vinserti64x4
        0, // INS_vmovdqa64
        0, // INS_vmovdqu16
        0, // INS_vmovdqu64
        0, // INS_vmovdqu8
        0, // INS_vpabsq
        0, // INS_vpandnq
        0, // INS_vpandq
        0, // INS_vpblendmb
        0, // INS_vpblendmd
        0, // INS_vpblendmq
        0, // INS_vpblendmw
        0, // INS_vpbroadcastb_gpr
        0, // INS_vpbroadcastd_gpr
        0, // INS_vpbroadcastq_gpr
        0, // INS_vpbroadcastw_gpr
        0, // INS_vpcmpb
        0, // INS_vpcmpd
        0, // INS_vpcmpeqb
        0, // INS_vpcmpeqd
        0, // INS_vpcmpeqq
        0, // INS_vpcmpeqw
        0, // INS_vpcmpgtb
        0, // INS_vpcmpgtd
        0, // INS_vpcmpgtq
        0, // INS_vpcmpgtw
        0, // INS_vpcmpq
        0, // INS_vpcmpub
        0, // INS_vpcmpud
        0, // INS_vpcmpuq
        0, // INS_vpcmpuw
        0, // INS_vpcmpw
        0, // INS_vpcompressd
        0, // INS_vpcompressq
        0, // INS_vpconflictd
        0, // INS_vpconflictq
        0, // INS_vpermi2d
        0, // INS_vpermi2pd
        0, // INS_vpermi2ps
        0, // INS_vpermi2q
        0, // INS_vpermi2w
        0, // INS_vpermpd_reg
        0, // INS_vpermq_reg
        0, // INS_vpermt2d
        0, // INS_vpermt2pd
        0, // INS_vpermt2ps
        0, // INS_vpermt2q
        0, // INS_vpermt2w
        0, // INS_vpermw
        0, // INS_vpexpandd
        0, // INS_vpexpandq
        0, // INS_vpgatherdd_msk
        0, // INS_vpgatherdq_msk
        0, // INS_vpgatherqd_msk
        0, // INS_vpgatherqq_msk
        0, // INS_vplzcntd
        0, // INS_vplzcntq
        0, // INS_vpmaxsq
        0, // INS_vpmaxuq
        0, // INS_vpminsq
        0, // INS_vpminuq
        0, // INS_vpmovb2m
        0, // INS_vpmovd2m
        0, // INS_vpmovdb
        0, // INS_vpmovdw
        0, // INS_vpmovm2b
        0, // INS_vpmovm2d
        0, // INS_vpmovm2q
        0, // INS_vpmovm2w
        0, // INS_vpmovq2m
        0, // INS_vpmovqb
        0, // INS_vpmovqd
        0, // INS_vpmovqw
        0, // INS_vpmovsdb
        0, // INS_vpmovsdw
        0, // INS_vpmovsqb
        0, // INS_vpmovsqd
        0, // INS_vpmovsqw
        0, // INS_vpmovswb
        0, // INS_vpmovusdb
        0, // INS_vpmovusdw
        0, // INS_vpmovusqb
        0, // INS_vpmovusqd
        0, // INS_vpmovusqw
        0, // INS_vpmovuswb
        0, // INS_vpmovw2m
        0, // INS_vpmovwb
        0, // INS_vpmullq
        0, // INS_vporq
        0, // INS_vprold
        0, // INS_vprolq
        0, // INS_vprolvd
        0, // INS_vprolvq
        0, // INS_vprord
        0, // INS_vprorq
        0, // INS_vprorvd
        0, // INS_vprorvq
        0, // INS_vpscatterdd_msk
        0, // INS_vpscatterdq_msk
        0, // INS_vpscatterqd_msk
        0, // INS_vpscatterqq_msk
        0, // INS_vpsllvw
        0, // INS_vpsraq
        0, // INS_vpsravq
        0, // INS_vpsravw
        0, // INS_vpsrlvw
        0, // INS_vpternlogd
        0, // INS_vpternlogq
        0, // INS_vptestmb
        0, // INS_vptestmd
        0, // INS_vptestmq
        0, // INS_vptestmw
        0, // INS_vptestnmb
        0, // INS_vptestnmd
        0, // INS_vptestnmq
        0, // INS_vptestnmw
        0, // INS_vpxorq
        0, // INS_vrangepd
        0, // INS_vrangeps
        0, // INS_vrangesd
        0, // INS_vrangess
        0, // INS_vrcp14pd
        0, // INS_vrcp14ps
        0, // INS_vrcp14sd
        0, // INS_vrcp14ss
        0, // INS_vreducepd
        0, // INS_vreduceps
        0, // INS_vreducesd
        0, // INS_vreducess
        0, // INS_vrndscalepd
        0, // INS_vrndscaleps
        0, // INS_vrndscalesd
        0, // INS_vrndscaless
        0, // INS_vrsqrt14pd
        0, // INS_vrsqrt14ps
        0, // INS_vrsqrt14sd
        0, // INS_vrsqrt14ss
        0, // INS_vscalefpd
        0, // INS_vscalefps
        0, // INS_vscalefsd
        0, // INS_vscalefss
        0, // INS_vscatterdpd_msk
        0, // INS_vscatterdps_msk
        0, // INS_vscatterqpd_msk
        0, // INS_vscatterqps_msk
        0, // INS_vshuff32x4
        0, // INS_vshuff64x2
        0, // INS_vshufi32x4
        0, // INS_vshufi64x2
        0, // INS_vpermb
        0, // INS_vpermi2b
        0, // INS_vpermt2b
        0, // INS_vpmultishiftqb
        0, // INS_vpcompressb
        0, // INS_vpcompressw
        0, // INS_vpexpandb
        0, // INS_vpexpandw
        0, // INS_vpopcntb
        0, // INS_vpopcntd
        0, // INS_vpopcntq
        0, // INS_vpopcntw
        0, // INS_vpshldd
        0, // INS_vpshldq
        0, // INS_vpshldvd
        0, // INS_vpshldvq
        0, // INS_vpshldvw
        0, // INS_vpshldw
        0, // INS_vpshrdd
        0, // INS_vpshrdq
        0, // INS_vpshrdvd
        0, // INS_vpshrdvq
        0, // INS_vpshrdvw
        0, // INS_vpshrdw
        0, // INS_vpshufbitqmb
        0, // INS_vaddph
        0, // INS_vaddsh
        0, // INS_vcmpph
        0, // INS_vcmpsh
        0, // INS_vcomish
        0, // INS_vcvtdq2ph
        0, // INS_vcvtne2ps2bf16
        0, // INS_vcvtneps2bf16
        0, // INS_vcvtpd2ph
        0, // INS_vcvtph2dq
        0, // INS_vcvtph2pd
        0, // INS_vcvtph2psx
        0, // INS_vcvtph2qq
        0, // INS_vcvtph2udq
        0, // INS_vcvtph2uqq
        0, // INS_vcvtph2uw
        0, // INS_vcvtph2w
        0, // INS_vcvtps2phx
        0, // INS_vcvtqq2ph
        0, // INS_vcvtsd2sh
        0, // INS_vcvtsh2sd
        0, // INS_vcvtsh2si32
        0, // INS_vcvtsh2si64
        0, // INS_vcvtsh2ss
        0, // INS_vcvtsh2usi32
        0, // INS_vcvtsh2usi64
        0, // INS_vcvtsi2sh32
        0, // INS_vcvtsi2sh64
        0, // INS_vcvtss2sh
        0, // INS_vcvttph2dq
        0, // INS_vcvttph2qq
        0, // INS_vcvttph2udq
        0, // INS_vcvttph2uqq
        0, // INS_vcvttph2uw
        0, // INS_vcvttph2w
        0, // INS_vcvttsh2si32
        0, // INS_vcvttsh2si64
        0, // INS_vcvttsh2usi32
        0, // INS_vcvttsh2usi64
        0, // INS_vcvtudq2ph
        0, // INS_vcvtuqq2ph
        0, // INS_vcvtusi2sh32
        0, // INS_vcvtusi2sh64
        0, // INS_vcvtuw2ph
        0, // INS_vcvtw2ph
        0, // INS_vdivph
        0, // INS_vdivsh
        0, // INS_vdpbf16ps
        0, // INS_vfcmaddcph
        0, // INS_vfcmaddcsh
        0, // INS_vfcmulcph
        0, // INS_vfcmulcsh
        0, // INS_vfmulcph
        0, // INS_vfmulcsh
        0, // INS_vfmadd132ph
        0, // INS_vfmadd213ph
        0, // INS_vfmadd231ph
        0, // INS_vfmadd132sh
        0, // INS_vfmadd213sh
        0, // INS_vfmadd231sh
        0, // INS_vfmaddcph
        0, // INS_vfmaddcsh
        0, // INS_vfmaddsub132ph
        0, // INS_vfmaddsub213ph
        0, // INS_vfmaddsub231ph
        0, // INS_vfmsub132ph
        0, // INS_vfmsub213ph
        0, // INS_vfmsub231ph
        0, // INS_vfmsub132sh
        0, // INS_vfmsub213sh
        0, // INS_vfmsub231sh
        0, // INS_vfmsubadd132ph
        0, // INS_vfmsubadd213ph
        0, // INS_vfmsubadd231ph
        0, // INS_vfnmadd132ph
        0, // INS_vfnmadd213ph
        0, // INS_vfnmadd231ph
        0, // INS_vfnmadd132sh
        0, // INS_vfnmadd213sh
        0, // INS_vfnmadd231sh
        0, // INS_vfnmsub132ph
        0, // INS_vfnmsub213ph
        0, // INS_vfnmsub231ph
        0, // INS_vfnmsub132sh
        0, // INS_vfnmsub213sh
        0, // INS_vfnmsub231sh
        0, // INS_vfpclassph
        0, // INS_vfpclasssh
        0, // INS_vgetexpph
        0, // INS_vgetexpsh
        0, // INS_vgetmantph
        0, // INS_vgetmantsh
        0, // INS_vmaxph
        0, // INS_vmaxsh
        0, // INS_vminsh
        0, // INS_vminph
        0, // INS_vmovsh
        0, // INS_vmovw
        0, // INS_vmulph
        0, // INS_vmulsh
        0, // INS_vrcpph
        0, // INS_vrcpsh
        0, // INS_vreduceph
        0, // INS_vreducesh
        0, // INS_vrndscaleph
        0, // INS_vrndscalesh
        0, // INS_vrsqrtph
        0, // INS_vrsqrtsh
        0, // INS_vscalefph
        0, // INS_vscalefsh
        0, // INS_vsqrtph
        0, // INS_vsqrtsh
        0, // INS_vsubph
        0, // INS_vsubsh
        0, // INS_vucomish
        0, // INS_vp2intersectd
        0, // INS_vp2intersectq
        0, // INS_vcomxsd
        0, // INS_vcomxss
        0, // INS_vcvtps2ibs
        0, // INS_vcvtps2iubs
        0, // INS_vcvttpd2dqs
        0, // INS_vcvttpd2qqs
        0, // INS_vcvttpd2udqs
        0, // INS_vcvttpd2uqqs
        0, // INS_vcvttps2dqs
        0, // INS_vcvttps2ibs
        0, // INS_vcvttps2iubs
        0, // INS_vcvttps2qqs
        0, // INS_vcvttps2udqs
        0, // INS_vcvttps2uqqs
        0, // INS_vcvttsd2sis32
        0, // INS_vcvttsd2sis64
        0, // INS_vcvttsd2usis32
        0, // INS_vcvttsd2usis64
        0, // INS_vcvttss2sis32
        0, // INS_vcvttss2sis64
        0, // INS_vcvttss2usis32
        0, // INS_vcvttss2usis64
        0, // INS_vminmaxpd
        0, // INS_vminmaxps
        0, // INS_vminmaxsd
        0, // INS_vminmaxss
        0, // INS_vmovd_simd
        0, // INS_vmovw_simd
        0, // INS_vmpsadbw
        0, // INS_vucomxsd
        0, // INS_vucomxss
        0, // INS_ccmpo
        0, // INS_ccmpno
        0, // INS_ccmpb
        0, // INS_ccmpae
        0, // INS_ccmpe
        0, // INS_ccmpne
        0, // INS_ccmpbe
        0, // INS_ccmpa
        0, // INS_ccmps
        0, // INS_ccmpns
        0, // INS_ccmpt
        0, // INS_ccmpf
        0, // INS_ccmpl
        0, // INS_ccmpge
        0, // INS_ccmple
        0, // INS_ccmpg
        1, // INS_cfcmovo
        1, // INS_cfcmovno
        1, // INS_cfcmovb
        1, // INS_cfcmovae
        1, // INS_cfcmove
        1, // INS_cfcmovne
        1, // INS_cfcmovbe
        1, // INS_cfcmova
        1, // INS_cfcmovs
        1, // INS_cfcmovns
        1, // INS_cfcmovp
        1, // INS_cfcmovnp
        1, // INS_cfcmovl
        1, // INS_cfcmovge
        1, // INS_cfcmovle
        1, // INS_cfcmovg
        0, // INS_ctesto
        0, // INS_ctestno
        0, // INS_ctestb
        0, // INS_ctestae
        0, // INS_cteste
        0, // INS_ctestne
        0, // INS_ctestbe
        0, // INS_ctesta
        0, // INS_ctests
        0, // INS_ctestns
        0, // INS_ctestt
        0, // INS_ctestf
        0, // INS_ctestl
        0, // INS_ctestge
        0, // INS_ctestle
        0, // INS_ctestg
        0, // INS_crc32_apx
        0, // INS_movbe_apx
        0, // INS_seto_apx
        0, // INS_setno_apx
        0, // INS_setb_apx
        0, // INS_setae_apx
        0, // INS_sete_apx
        0, // INS_setne_apx
        0, // INS_setbe_apx
        0, // INS_seta_apx
        0, // INS_sets_apx
        0, // INS_setns_apx
        0, // INS_setp_apx
        0, // INS_setnp_apx
        0, // INS_setl_apx
        0, // INS_setge_apx
        0, // INS_setle_apx
        0, // INS_setg_apx
        0, // INS_crc32
        0, // INS_tzcnt
        0, // INS_tzcnt_apx
        0, // INS_lzcnt
        0, // INS_lzcnt_apx
        0, // INS_movbe
        0, // INS_popcnt
        0, // INS_popcnt_apx
        0, // INS_tpause
        0, // INS_umonitor
        0, // INS_umwait
        1, // INS_neg
        1, // INS_not
        1, // INS_rol
        1, // INS_rol_1
        1, // INS_rol_N
        1, // INS_ror
        1, // INS_ror_1
        1, // INS_ror_N
        1, // INS_rcl
        1, // INS_rcl_1
        1, // INS_rcl_N
        1, // INS_rcr
        1, // INS_rcr_1
        1, // INS_rcr_N
        1, // INS_shl
        1, // INS_shl_1
        1, // INS_shl_N
        1, // INS_shr
        1, // INS_shr_1
        1, // INS_shr_N
        1, // INS_sar
        1, // INS_sar_1
        1, // INS_sar_N
        0, // INS_ret
        0, // INS_loop
        0, // INS_call
        0, // INS_r_movsb
        0, // INS_r_movsd
        0, // INS_r_movsq
        0, // INS_movsb
        0, // INS_movsd
        0, // INS_movsq
        0, // INS_r_stosb
        0, // INS_r_stosd
        0, // INS_r_stosq
        0, // INS_stosb
        0, // INS_stosd
        0, // INS_stosq
        0, // INS_int3
        0, // INS_nop
        0, // INS_pause
        0, // INS_lock
        0, // INS_leave
        0, // INS_serialize
        0, // INS_cwde
        0, // INS_cdq
        0, // INS_idiv
        0, // INS_imulEAX
        0, // INS_div
        0, // INS_mulEAX
        0, // INS_sahf
        0, // INS_xadd
        0, // INS_cmpxchg
        0, // INS_shld
        0, // INS_shrd
        0, // INS_seto
        0, // INS_setno
        0, // INS_setb
        0, // INS_setae
        0, // INS_sete
        0, // INS_setne
        0, // INS_setbe
        0, // INS_seta
        0, // INS_sets
        0, // INS_setns
        0, // INS_setp
        0, // INS_setnp
        0, // INS_setl
        0, // INS_setge
        0, // INS_setle
        0, // INS_setg
        0, // INS_tail_i_jmp
        0, // INS_i_jmp
        0, // INS_jmp
        0, // INS_jo
        0, // INS_jno
        0, // INS_jb
        0, // INS_jae
        0, // INS_je
        0, // INS_jne
        0, // INS_jbe
        0, // INS_ja
        0, // INS_js
        0, // INS_jns
        0, // INS_jp
        0, // INS_jnp
        0, // INS_jl
        0, // INS_jge
        0, // INS_jle
        0, // INS_jg
        0, // INS_l_jmp
        0, // INS_l_jo
        0, // INS_l_jno
        0, // INS_l_jb
        0, // INS_l_jae
        0, // INS_l_je
        0, // INS_l_jne
        0, // INS_l_jbe
        0, // INS_l_ja
        0, // INS_l_js
        0, // INS_l_jns
        0, // INS_l_jp
        0, // INS_l_jnp
        0, // INS_l_jl
        0, // INS_l_jge
        0, // INS_l_jle
        0, // INS_l_jg
        0, // INS_align
        0, // INS_data16
    ];
}