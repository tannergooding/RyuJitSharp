// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Globals
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CORINFO_InstructionSetFlags EnsureInstructionSetFlagsAreValid(CORINFO_InstructionSetFlags input)
    {
        CORINFO_InstructionSetFlags oldFlags;
        CORINFO_InstructionSetFlags resultFlags = input;

        do
        {
            oldFlags = resultFlags;

            if (TARGET_ARM64)
            {
                if (resultFlags.HasInstructionSet(InstructionSet_ARMARCH_ArmBase) && !resultFlags.HasInstructionSet(InstructionSet_ARMARCH_ArmBase_Arm64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_ARMARCH_ArmBase);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_ARMARCH_ArmBase_Arm64) && !resultFlags.HasInstructionSet(InstructionSet_ARMARCH_ArmBase))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_ARMARCH_ArmBase_Arm64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_ARMARCH_AdvSimd) && !resultFlags.HasInstructionSet(InstructionSet_ARMARCH_AdvSimd_Arm64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_ARMARCH_AdvSimd);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_ARMARCH_AdvSimd_Arm64) && !resultFlags.HasInstructionSet(InstructionSet_ARMARCH_AdvSimd))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_ARMARCH_AdvSimd_Arm64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Aes) && !resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Aes_Arm64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_ARMARCH_Aes);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Aes_Arm64) && !resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Aes))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_ARMARCH_Aes_Arm64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Crc32) && !resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Crc32_Arm64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_ARMARCH_Crc32);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Crc32_Arm64) && !resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Crc32))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_ARMARCH_Crc32_Arm64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Dp) && !resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Dp_Arm64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_ARMARCH_Dp);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Dp_Arm64) && !resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Dp))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_ARMARCH_Dp_Arm64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Rdm) && !resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Rdm_Arm64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_ARMARCH_Rdm);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Rdm_Arm64) && !resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Rdm))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_ARMARCH_Rdm_Arm64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Sha1) && !resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Sha1_Arm64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_ARMARCH_Sha1);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Sha1_Arm64) && !resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Sha1))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_ARMARCH_Sha1_Arm64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Sha256) && !resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Sha256_Arm64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_ARMARCH_Sha256);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Sha256_Arm64) && !resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Sha256))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_ARMARCH_Sha256_Arm64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Sve) && !resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Sve_Arm64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_ARMARCH_Sve);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Sve_Arm64) && !resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Sve))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_ARMARCH_Sve_Arm64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_ARMARCH_AdvSimd) && !resultFlags.HasInstructionSet(InstructionSet_ARMARCH_ArmBase))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_ARMARCH_AdvSimd);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Aes) && !resultFlags.HasInstructionSet(InstructionSet_ARMARCH_ArmBase))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_ARMARCH_Aes);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Crc32) && !resultFlags.HasInstructionSet(InstructionSet_ARMARCH_ArmBase))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_ARMARCH_Crc32);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Dp) && !resultFlags.HasInstructionSet(InstructionSet_ARMARCH_AdvSimd))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_ARMARCH_Dp);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Rdm) && !resultFlags.HasInstructionSet(InstructionSet_ARMARCH_AdvSimd))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_ARMARCH_Rdm);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Sha1) && !resultFlags.HasInstructionSet(InstructionSet_ARMARCH_ArmBase))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_ARMARCH_Sha1);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Sha256) && !resultFlags.HasInstructionSet(InstructionSet_ARMARCH_ArmBase))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_ARMARCH_Sha256);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Vector64) && !resultFlags.HasInstructionSet(InstructionSet_ARMARCH_AdvSimd))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_ARMARCH_Vector64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Vector128) && !resultFlags.HasInstructionSet(InstructionSet_ARMARCH_AdvSimd))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_ARMARCH_Vector128);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_ARMARCH_VectorT128) && !resultFlags.HasInstructionSet(InstructionSet_ARMARCH_AdvSimd))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_ARMARCH_VectorT128);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_ARMARCH_Sve) && !resultFlags.HasInstructionSet(InstructionSet_ARMARCH_AdvSimd))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_ARMARCH_Sve);
                }
            }

            if (TARGET_X64)
            {
                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_X86Base) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_X86Base_X64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_X86Base);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_X86Base_X64) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_X86Base))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_X86Base_X64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_SSE) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_SSE_X64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_SSE);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_SSE_X64) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_SSE))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_SSE_X64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_SSE2) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_SSE2_X64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_SSE2);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_SSE2_X64) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_SSE2))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_SSE2_X64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_SSE3) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_SSE3_X64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_SSE3);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_SSE3_X64) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_SSE3))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_SSE3_X64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_SSSE3) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_SSSE3_X64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_SSSE3);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_SSSE3_X64) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_SSSE3))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_SSSE3_X64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_SSE41) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_SSE41_X64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_SSE41);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_SSE41_X64) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_SSE41))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_SSE41_X64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_SSE42) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_SSE42_X64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_SSE42);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_SSE42_X64) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_SSE42))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_SSE42_X64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX_X64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX_X64) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX_X64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX2) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX2_X64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX2);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX2_X64) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX2))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX2_X64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AES) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AES_X64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AES);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AES_X64) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AES))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AES_X64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_BMI1) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_BMI1_X64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_BMI1);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_BMI1_X64) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_BMI1))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_BMI1_X64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_BMI2) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_BMI2_X64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_BMI2);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_BMI2_X64) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_BMI2))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_BMI2_X64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_FMA) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_FMA_X64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_FMA);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_FMA_X64) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_FMA))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_FMA_X64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_LZCNT) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_LZCNT_X64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_LZCNT);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_LZCNT_X64) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_LZCNT))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_LZCNT_X64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_PCLMULQDQ) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_PCLMULQDQ_X64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_PCLMULQDQ);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_PCLMULQDQ_X64) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_PCLMULQDQ))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_PCLMULQDQ_X64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_POPCNT) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_POPCNT_X64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_POPCNT);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_POPCNT_X64) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_POPCNT))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_POPCNT_X64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVXVNNI) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVXVNNI_X64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVXVNNI);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVXVNNI_X64) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVXVNNI))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVXVNNI_X64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_MOVBE) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_MOVBE_X64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_MOVBE);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_MOVBE_X64) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_MOVBE))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_MOVBE_X64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_X86Serialize) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_X86Serialize_X64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_X86Serialize);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_X86Serialize_X64) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_X86Serialize))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_X86Serialize_X64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512F) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512F_X64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512F);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512F_X64) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512F))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512F_X64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512F_VL) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512F_VL_X64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512F_VL);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512F_VL_X64) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512F_VL))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512F_VL_X64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512BW) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512BW_X64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512BW);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512BW_X64) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512BW))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512BW_X64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512BW_VL) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512BW_VL_X64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512BW_VL);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512BW_VL_X64) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512BW_VL))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512BW_VL_X64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512CD) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512CD_X64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512CD);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512CD_X64) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512CD))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512CD_X64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512CD_VL) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512CD_VL_X64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512CD_VL);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512CD_VL_X64) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512CD_VL))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512CD_VL_X64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512DQ) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512DQ_X64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512DQ);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512DQ_X64) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512DQ))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512DQ_X64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512DQ_VL) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512DQ_VL_X64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512DQ_VL);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512DQ_VL_X64) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512DQ_VL))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512DQ_VL_X64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512VBMI) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512VBMI_X64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512VBMI);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512VBMI_X64) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512VBMI))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512VBMI_X64);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512VBMI_VL) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512VBMI_VL_X64))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512VBMI_VL);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512VBMI_VL_X64) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512VBMI_VL))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512VBMI_VL_X64);
                }
            }

            if (TARGET_XARCH)
            {
                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_SSE) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_X86Base))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_SSE);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_SSE2) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_SSE))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_SSE2);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_SSE3) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_SSE2))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_SSE3);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_SSSE3) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_SSE3))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_SSSE3);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_SSE41) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_SSSE3))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_SSE41);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_SSE42) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_SSE41))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_SSE42);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_SSE42))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX2) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX2);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AES) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_SSE2))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AES);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_BMI1) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_BMI1);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_BMI2) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_BMI2);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_FMA) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_FMA);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_LZCNT) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_X86Base))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_LZCNT);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_PCLMULQDQ) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_SSE2))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_PCLMULQDQ);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_POPCNT) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_SSE42))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_POPCNT);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_Vector128) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_SSE))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_Vector128);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_Vector256) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_Vector256);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_Vector512) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512F))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_Vector512);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVXVNNI) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX2))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVXVNNI);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_MOVBE) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_SSE42))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_MOVBE);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_X86Serialize) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_X86Base))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_X86Serialize);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512F) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX2))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512F);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512F) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_FMA))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512F);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512F_VL) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512F))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512F_VL);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512CD) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512F))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512CD);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512CD_VL) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512CD))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512CD_VL);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512CD_VL) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512F_VL))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512CD_VL);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512BW) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512F))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512BW);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512BW_VL) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512BW))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512BW_VL);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512BW_VL) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512F_VL))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512BW_VL);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512DQ) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512F))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512DQ);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512DQ_VL) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512DQ))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512DQ_VL);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512DQ_VL) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512F_VL))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512DQ_VL);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512VBMI) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512BW))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512VBMI);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512VBMI_VL) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512VBMI))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512VBMI_VL);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512VBMI_VL) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512BW_VL))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512VBMI_VL);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_VectorT128) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_SSE2))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_VectorT128);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_VectorT256) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX2))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_VectorT256);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_VectorT512) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512F))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_VectorT512);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512F) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512BW_VL))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512F);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512F) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512CD_VL))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512F);
                }

                if (resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512F) && !resultFlags.HasInstructionSet(InstructionSet_XARCH_AVX512DQ_VL))
                {
                    resultFlags.RemoveInstructionSet(InstructionSet_XARCH_AVX512F);
                }
            }
        }
        while (!oldFlags.Equals(resultFlags));

        return resultFlags;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string InstructionSetToString(CORINFO_InstructionSet instructionSet)
    {
        if (TARGET_ARM64)
        {
            return instructionSet switch {
                InstructionSet_ARMARCH_ArmBase => "ArmBase",
                InstructionSet_ARMARCH_ArmBase_Arm64 => "ArmBase_Arm64",
                InstructionSet_ARMARCH_AdvSimd => "AdvSimd",
                InstructionSet_ARMARCH_AdvSimd_Arm64 => "AdvSimd_Arm64",
                InstructionSet_ARMARCH_Aes => "Aes",
                InstructionSet_ARMARCH_Aes_Arm64 => "Aes_Arm64",
                InstructionSet_ARMARCH_Crc32 => "Crc32",
                InstructionSet_ARMARCH_Crc32_Arm64 => "Crc32_Arm64",
                InstructionSet_ARMARCH_Dp => "Dp",
                InstructionSet_ARMARCH_Dp_Arm64 => "Dp_Arm64",
                InstructionSet_ARMARCH_Rdm => "Rdm",
                InstructionSet_ARMARCH_Rdm_Arm64 => "Rdm_Arm64",
                InstructionSet_ARMARCH_Sha1 => "Sha1",
                InstructionSet_ARMARCH_Sha1_Arm64 => "Sha1_Arm64",
                InstructionSet_ARMARCH_Sha256 => "Sha256",
                InstructionSet_ARMARCH_Sha256_Arm64 => "Sha256_Arm64",
                InstructionSet_ARMARCH_Atomics => "Atomics",
                InstructionSet_ARMARCH_Vector64 => "Vector64",
                InstructionSet_ARMARCH_Vector128 => "Vector128",
                InstructionSet_ARMARCH_Dczva => "Dczva",
                InstructionSet_ARMARCH_Rcpc => "Rcpc",
                InstructionSet_ARMARCH_VectorT128 => "VectorT128",
                InstructionSet_ARMARCH_Rcpc2 => "Rcpc2",
                InstructionSet_ARMARCH_Sve => "Sve",
                InstructionSet_ARMARCH_Sve_Arm64 => "Sve_Arm64",
                _ => "UnknownInstructionSet",
            };
        }

        if (TARGET_X64)
        {
            return instructionSet switch {
                InstructionSet_XARCH_X86Base => "X86Base",
                InstructionSet_XARCH_X86Base_X64 => "X86Base_X64",
                InstructionSet_XARCH_SSE => "SSE",
                InstructionSet_XARCH_SSE_X64 => "SSE_X64",
                InstructionSet_XARCH_SSE2 => "SSE2",
                InstructionSet_XARCH_SSE2_X64 => "SSE2_X64",
                InstructionSet_XARCH_SSE3 => "SSE3",
                InstructionSet_XARCH_SSE3_X64 => "SSE3_X64",
                InstructionSet_XARCH_SSSE3 => "SSSE3",
                InstructionSet_XARCH_SSSE3_X64 => "SSSE3_X64",
                InstructionSet_XARCH_SSE41 => "SSE41",
                InstructionSet_XARCH_SSE41_X64 => "SSE41_X64",
                InstructionSet_XARCH_SSE42 => "SSE42",
                InstructionSet_XARCH_SSE42_X64 => "SSE42_X64",
                InstructionSet_XARCH_AVX => "AVX",
                InstructionSet_XARCH_AVX_X64 => "AVX_X64",
                InstructionSet_XARCH_AVX2 => "AVX2",
                InstructionSet_XARCH_AVX2_X64 => "AVX2_X64",
                InstructionSet_XARCH_AES => "AES",
                InstructionSet_XARCH_AES_X64 => "AES_X64",
                InstructionSet_XARCH_BMI1 => "BMI1",
                InstructionSet_XARCH_BMI1_X64 => "BMI1_X64",
                InstructionSet_XARCH_BMI2 => "BMI2",
                InstructionSet_XARCH_BMI2_X64 => "BMI2_X64",
                InstructionSet_XARCH_FMA => "FMA",
                InstructionSet_XARCH_FMA_X64 => "FMA_X64",
                InstructionSet_XARCH_LZCNT => "LZCNT",
                InstructionSet_XARCH_LZCNT_X64 => "LZCNT_X64",
                InstructionSet_XARCH_PCLMULQDQ => "PCLMULQDQ",
                InstructionSet_XARCH_PCLMULQDQ_X64 => "PCLMULQDQ_X64",
                InstructionSet_XARCH_POPCNT => "POPCNT",
                InstructionSet_XARCH_POPCNT_X64 => "POPCNT_X64",
                InstructionSet_XARCH_Vector128 => "Vector128",
                InstructionSet_XARCH_Vector256 => "Vector256",
                InstructionSet_XARCH_Vector512 => "Vector512",
                InstructionSet_XARCH_AVXVNNI => "AVXVNNI",
                InstructionSet_XARCH_AVXVNNI_X64 => "AVXVNNI_X64",
                InstructionSet_XARCH_MOVBE => "MOVBE",
                InstructionSet_XARCH_MOVBE_X64 => "MOVBE_X64",
                InstructionSet_XARCH_X86Serialize => "X86Serialize",
                InstructionSet_XARCH_X86Serialize_X64 => "X86Serialize_X64",
                InstructionSet_XARCH_AVX512F => "AVX512F",
                InstructionSet_XARCH_AVX512F_X64 => "AVX512F_X64",
                InstructionSet_XARCH_AVX512F_VL => "AVX512F_VL",
                InstructionSet_XARCH_AVX512F_VL_X64 => "AVX512F_VL_X64",
                InstructionSet_XARCH_AVX512BW => "AVX512BW",
                InstructionSet_XARCH_AVX512BW_X64 => "AVX512BW_X64",
                InstructionSet_XARCH_AVX512BW_VL => "AVX512BW_VL",
                InstructionSet_XARCH_AVX512BW_VL_X64 => "AVX512BW_VL_X64",
                InstructionSet_XARCH_AVX512CD => "AVX512CD",
                InstructionSet_XARCH_AVX512CD_X64 => "AVX512CD_X64",
                InstructionSet_XARCH_AVX512CD_VL => "AVX512CD_VL",
                InstructionSet_XARCH_AVX512CD_VL_X64 => "AVX512CD_VL_X64",
                InstructionSet_XARCH_AVX512DQ => "AVX512DQ",
                InstructionSet_XARCH_AVX512DQ_X64 => "AVX512DQ_X64",
                InstructionSet_XARCH_AVX512DQ_VL => "AVX512DQ_VL",
                InstructionSet_XARCH_AVX512DQ_VL_X64 => "AVX512DQ_VL_X64",
                InstructionSet_XARCH_AVX512VBMI => "AVX512VBMI",
                InstructionSet_XARCH_AVX512VBMI_X64 => "AVX512VBMI_X64",
                InstructionSet_XARCH_AVX512VBMI_VL => "AVX512VBMI_VL",
                InstructionSet_XARCH_AVX512VBMI_VL_X64 => "AVX512VBMI_VL_X64",
                InstructionSet_XARCH_VectorT128 => "VectorT128",
                InstructionSet_XARCH_VectorT256 => "VectorT256",
                InstructionSet_XARCH_VectorT512 => "VectorT512",
                _ => "UnknownInstructionSet",
            };
        }

        if (TARGET_X86)
        {
            return instructionSet switch {
                InstructionSet_XARCH_X86Base => "X86Base",
                InstructionSet_XARCH_SSE => "SSE",
                InstructionSet_XARCH_SSE2 => "SSE2",
                InstructionSet_XARCH_SSE3 => "SSE3",
                InstructionSet_XARCH_SSSE3 => "SSSE3",
                InstructionSet_XARCH_SSE41 => "SSE41",
                InstructionSet_XARCH_SSE42 => "SSE42",
                InstructionSet_XARCH_AVX => "AVX",
                InstructionSet_XARCH_AVX2 => "AVX2",
                InstructionSet_XARCH_AES => "AES",
                InstructionSet_XARCH_BMI1 => "BMI1",
                InstructionSet_XARCH_BMI2 => "BMI2",
                InstructionSet_XARCH_FMA => "FMA",
                InstructionSet_XARCH_LZCNT => "LZCNT",
                InstructionSet_XARCH_PCLMULQDQ => "PCLMULQDQ",
                InstructionSet_XARCH_POPCNT => "POPCNT",
                InstructionSet_XARCH_Vector128 => "Vector128",
                InstructionSet_XARCH_Vector256 => "Vector256",
                InstructionSet_XARCH_Vector512 => "Vector512",
                InstructionSet_XARCH_AVXVNNI => "AVXVNNI",
                InstructionSet_XARCH_MOVBE => "MOVBE",
                InstructionSet_XARCH_X86Serialize => "X86Serialize",
                InstructionSet_XARCH_AVX512F => "AVX512F",
                InstructionSet_XARCH_AVX512F_VL => "AVX512F_VL",
                InstructionSet_XARCH_AVX512BW => "AVX512BW",
                InstructionSet_XARCH_AVX512BW_VL => "AVX512BW_VL",
                InstructionSet_XARCH_AVX512CD => "AVX512CD",
                InstructionSet_XARCH_AVX512CD_VL => "AVX512CD_VL",
                InstructionSet_XARCH_AVX512DQ => "AVX512DQ",
                InstructionSet_XARCH_AVX512DQ_VL => "AVX512DQ_VL",
                InstructionSet_XARCH_AVX512VBMI => "AVX512VBMI",
                InstructionSet_XARCH_AVX512VBMI_VL => "AVX512VBMI_VL",
                InstructionSet_XARCH_VectorT128 => "VectorT128",
                InstructionSet_XARCH_VectorT256 => "VectorT256",
                InstructionSet_XARCH_VectorT512 => "VectorT512",
                _ => "UnknownInstructionSet",
            };
        }

        return "UnknownInstructionSet";
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CORINFO_InstructionSet InstructionSetFromR2RInstructionSet(ReadyToRunInstructionSet r2rSet)
    {
        if (TARGET_ARM64)
        {
            return r2rSet switch {
                READYTORUN_INSTRUCTION_ArmBase => InstructionSet_ARMARCH_ArmBase,
                READYTORUN_INSTRUCTION_AdvSimd => InstructionSet_ARMARCH_AdvSimd,
                READYTORUN_INSTRUCTION_Aes => InstructionSet_ARMARCH_Aes,
                READYTORUN_INSTRUCTION_Crc32 => InstructionSet_ARMARCH_Crc32,
                READYTORUN_INSTRUCTION_Dp => InstructionSet_ARMARCH_Dp,
                READYTORUN_INSTRUCTION_Rdm => InstructionSet_ARMARCH_Rdm,
                READYTORUN_INSTRUCTION_Sha1 => InstructionSet_ARMARCH_Sha1,
                READYTORUN_INSTRUCTION_Sha256 => InstructionSet_ARMARCH_Sha256,
                READYTORUN_INSTRUCTION_Atomics => InstructionSet_ARMARCH_Atomics,
                READYTORUN_INSTRUCTION_Rcpc => InstructionSet_ARMARCH_Rcpc,
                READYTORUN_INSTRUCTION_VectorT128 => InstructionSet_ARMARCH_VectorT128,
                READYTORUN_INSTRUCTION_Rcpc2 => InstructionSet_ARMARCH_Rcpc2,
                READYTORUN_INSTRUCTION_Sve => InstructionSet_ARMARCH_Sve,
                _ => InstructionSet_ILLEGAL,
            };
        }

        if (TARGET_XARCH)
        {
            return r2rSet switch {
                READYTORUN_INSTRUCTION_X86Base => InstructionSet_XARCH_X86Base,
                READYTORUN_INSTRUCTION_Sse => InstructionSet_XARCH_SSE,
                READYTORUN_INSTRUCTION_Sse2 => InstructionSet_XARCH_SSE2,
                READYTORUN_INSTRUCTION_Sse3 => InstructionSet_XARCH_SSE3,
                READYTORUN_INSTRUCTION_Ssse3 => InstructionSet_XARCH_SSSE3,
                READYTORUN_INSTRUCTION_Sse41 => InstructionSet_XARCH_SSE41,
                READYTORUN_INSTRUCTION_Sse42 => InstructionSet_XARCH_SSE42,
                READYTORUN_INSTRUCTION_Avx => InstructionSet_XARCH_AVX,
                READYTORUN_INSTRUCTION_Avx2 => InstructionSet_XARCH_AVX2,
                READYTORUN_INSTRUCTION_Aes => InstructionSet_XARCH_AES,
                READYTORUN_INSTRUCTION_Bmi1 => InstructionSet_XARCH_BMI1,
                READYTORUN_INSTRUCTION_Bmi2 => InstructionSet_XARCH_BMI2,
                READYTORUN_INSTRUCTION_Fma => InstructionSet_XARCH_FMA,
                READYTORUN_INSTRUCTION_Lzcnt => InstructionSet_XARCH_LZCNT,
                READYTORUN_INSTRUCTION_Pclmulqdq => InstructionSet_XARCH_PCLMULQDQ,
                READYTORUN_INSTRUCTION_Popcnt => InstructionSet_XARCH_POPCNT,
                READYTORUN_INSTRUCTION_AvxVnni => InstructionSet_XARCH_AVXVNNI,
                READYTORUN_INSTRUCTION_Movbe => InstructionSet_XARCH_MOVBE,
                READYTORUN_INSTRUCTION_X86Serialize => InstructionSet_XARCH_X86Serialize,
                READYTORUN_INSTRUCTION_Avx512F => InstructionSet_XARCH_AVX512F,
                READYTORUN_INSTRUCTION_Avx512F_VL => InstructionSet_XARCH_AVX512F_VL,
                READYTORUN_INSTRUCTION_Avx512BW => InstructionSet_XARCH_AVX512BW,
                READYTORUN_INSTRUCTION_Avx512BW_VL => InstructionSet_XARCH_AVX512BW_VL,
                READYTORUN_INSTRUCTION_Avx512CD => InstructionSet_XARCH_AVX512CD,
                READYTORUN_INSTRUCTION_Avx512CD_VL => InstructionSet_XARCH_AVX512CD_VL,
                READYTORUN_INSTRUCTION_Avx512DQ => InstructionSet_XARCH_AVX512DQ,
                READYTORUN_INSTRUCTION_Avx512DQ_VL => InstructionSet_XARCH_AVX512DQ_VL,
                READYTORUN_INSTRUCTION_Avx512Vbmi => InstructionSet_XARCH_AVX512VBMI,
                READYTORUN_INSTRUCTION_Avx512Vbmi_VL => InstructionSet_XARCH_AVX512VBMI_VL,
                READYTORUN_INSTRUCTION_VectorT128 => InstructionSet_XARCH_VectorT128,
                READYTORUN_INSTRUCTION_VectorT256 => InstructionSet_XARCH_VectorT256,
                READYTORUN_INSTRUCTION_VectorT512 => InstructionSet_XARCH_VectorT512,
                _ => InstructionSet_ILLEGAL,
            };
        }

        return InstructionSet_ILLEGAL;
    }
}
