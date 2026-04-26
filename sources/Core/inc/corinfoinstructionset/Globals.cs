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
        var resultFlags = input;

        do
        {
            oldFlags = resultFlags;

#if TARGET_ARM64
            if (resultFlags.HasInstructionSet(InstructionSet_ArmBase) && !resultFlags.HasInstructionSet(InstructionSet_ArmBase_Arm64))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_ArmBase);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_ArmBase_Arm64) && !resultFlags.HasInstructionSet(InstructionSet_ArmBase))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_ArmBase_Arm64);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AdvSimd) && !resultFlags.HasInstructionSet(InstructionSet_AdvSimd_Arm64))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AdvSimd);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AdvSimd_Arm64) && !resultFlags.HasInstructionSet(InstructionSet_AdvSimd))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AdvSimd_Arm64);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Aes) && !resultFlags.HasInstructionSet(InstructionSet_Aes_Arm64))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Aes);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Aes_Arm64) && !resultFlags.HasInstructionSet(InstructionSet_Aes))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Aes_Arm64);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Crc32) && !resultFlags.HasInstructionSet(InstructionSet_Crc32_Arm64))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Crc32);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Crc32_Arm64) && !resultFlags.HasInstructionSet(InstructionSet_Crc32))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Crc32_Arm64);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Dp) && !resultFlags.HasInstructionSet(InstructionSet_Dp_Arm64))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Dp);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Dp_Arm64) && !resultFlags.HasInstructionSet(InstructionSet_Dp))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Dp_Arm64);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Rdm) && !resultFlags.HasInstructionSet(InstructionSet_Rdm_Arm64))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Rdm);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Rdm_Arm64) && !resultFlags.HasInstructionSet(InstructionSet_Rdm))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Rdm_Arm64);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Sha1) && !resultFlags.HasInstructionSet(InstructionSet_Sha1_Arm64))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Sha1);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Sha1_Arm64) && !resultFlags.HasInstructionSet(InstructionSet_Sha1))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Sha1_Arm64);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Sha256) && !resultFlags.HasInstructionSet(InstructionSet_Sha256_Arm64))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Sha256);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Sha256_Arm64) && !resultFlags.HasInstructionSet(InstructionSet_Sha256))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Sha256_Arm64);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Sve) && !resultFlags.HasInstructionSet(InstructionSet_Sve_Arm64))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Sve);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Sve_Arm64) && !resultFlags.HasInstructionSet(InstructionSet_Sve))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Sve_Arm64);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Sve2) && !resultFlags.HasInstructionSet(InstructionSet_Sve2_Arm64))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Sve2);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Sve2_Arm64) && !resultFlags.HasInstructionSet(InstructionSet_Sve2))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Sve2_Arm64);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Sha3) && !resultFlags.HasInstructionSet(InstructionSet_Sha3_Arm64))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Sha3);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Sha3_Arm64) && !resultFlags.HasInstructionSet(InstructionSet_Sha3))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Sha3_Arm64);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Sm4) && !resultFlags.HasInstructionSet(InstructionSet_Sm4_Arm64))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Sm4);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Sm4_Arm64) && !resultFlags.HasInstructionSet(InstructionSet_Sm4))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Sm4_Arm64);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_SveAes) && !resultFlags.HasInstructionSet(InstructionSet_SveAes_Arm64))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_SveAes);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_SveAes_Arm64) && !resultFlags.HasInstructionSet(InstructionSet_SveAes))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_SveAes_Arm64);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_SveSha3) && !resultFlags.HasInstructionSet(InstructionSet_SveSha3_Arm64))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_SveSha3);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_SveSha3_Arm64) && !resultFlags.HasInstructionSet(InstructionSet_SveSha3))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_SveSha3_Arm64);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_SveSm4) && !resultFlags.HasInstructionSet(InstructionSet_SveSm4_Arm64))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_SveSm4);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_SveSm4_Arm64) && !resultFlags.HasInstructionSet(InstructionSet_SveSm4))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_SveSm4_Arm64);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AdvSimd) && !resultFlags.HasInstructionSet(InstructionSet_ArmBase))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AdvSimd);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Aes) && !resultFlags.HasInstructionSet(InstructionSet_ArmBase))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Aes);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Crc32) && !resultFlags.HasInstructionSet(InstructionSet_ArmBase))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Crc32);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Dp) && !resultFlags.HasInstructionSet(InstructionSet_AdvSimd))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Dp);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Rdm) && !resultFlags.HasInstructionSet(InstructionSet_AdvSimd))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Rdm);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Sha1) && !resultFlags.HasInstructionSet(InstructionSet_ArmBase))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Sha1);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Sha256) && !resultFlags.HasInstructionSet(InstructionSet_ArmBase))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Sha256);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Vector64) && !resultFlags.HasInstructionSet(InstructionSet_AdvSimd))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Vector64);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Vector128) && !resultFlags.HasInstructionSet(InstructionSet_AdvSimd))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Vector128);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_VectorT) && !resultFlags.HasInstructionSet(InstructionSet_Sve))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_VectorT);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_VectorT128) && !resultFlags.HasInstructionSet(InstructionSet_AdvSimd))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_VectorT128);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Sve) && !resultFlags.HasInstructionSet(InstructionSet_AdvSimd))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Sve);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Sve2) && !resultFlags.HasInstructionSet(InstructionSet_Sve))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Sve2);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Sha3) && !resultFlags.HasInstructionSet(InstructionSet_ArmBase))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Sha3);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Sm4) && !resultFlags.HasInstructionSet(InstructionSet_ArmBase))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Sm4);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_SveAes) && !resultFlags.HasInstructionSet(InstructionSet_Sve))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_SveAes);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_SveAes) && !resultFlags.HasInstructionSet(InstructionSet_Aes))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_SveAes);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_SveSha3) && !resultFlags.HasInstructionSet(InstructionSet_Sve))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_SveSha3);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_SveSha3) && !resultFlags.HasInstructionSet(InstructionSet_Sha3))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_SveSha3);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_SveSm4) && !resultFlags.HasInstructionSet(InstructionSet_Sve))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_SveSm4);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_SveSm4) && !resultFlags.HasInstructionSet(InstructionSet_Sm4))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_SveSm4);
            }
#elif TARGET_RISCV64
            if (resultFlags.HasInstructionSet(InstructionSet_Zbb) && !resultFlags.HasInstructionSet(InstructionSet_RiscV64Base))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Zbb);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Zba) && !resultFlags.HasInstructionSet(InstructionSet_RiscV64Base))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Zba);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Zbs) && !resultFlags.HasInstructionSet(InstructionSet_RiscV64Base))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Zbs);
            }
#elif TARGET_AMD64
            if (resultFlags.HasInstructionSet(InstructionSet_X86Base) && !resultFlags.HasInstructionSet(InstructionSet_X86Base_X64))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_X86Base);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_X86Base_X64) && !resultFlags.HasInstructionSet(InstructionSet_X86Base))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_X86Base_X64);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVX) && !resultFlags.HasInstructionSet(InstructionSet_AVX_X64))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVX);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVX_X64) && !resultFlags.HasInstructionSet(InstructionSet_AVX))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVX_X64);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVX2) && !resultFlags.HasInstructionSet(InstructionSet_AVX2_X64))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVX2);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVX2_X64) && !resultFlags.HasInstructionSet(InstructionSet_AVX2))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVX2_X64);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVX512) && !resultFlags.HasInstructionSet(InstructionSet_AVX512_X64))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVX512);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVX512_X64) && !resultFlags.HasInstructionSet(InstructionSet_AVX512))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVX512_X64);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVX512v2) && !resultFlags.HasInstructionSet(InstructionSet_AVX512v2_X64))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVX512v2);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVX512v2_X64) && !resultFlags.HasInstructionSet(InstructionSet_AVX512v2))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVX512v2_X64);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVX512v3) && !resultFlags.HasInstructionSet(InstructionSet_AVX512v3_X64))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVX512v3);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVX512v3_X64) && !resultFlags.HasInstructionSet(InstructionSet_AVX512v3))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVX512v3_X64);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVX10v1) && !resultFlags.HasInstructionSet(InstructionSet_AVX10v1_X64))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVX10v1);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVX10v1_X64) && !resultFlags.HasInstructionSet(InstructionSet_AVX10v1))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVX10v1_X64);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVX10v2) && !resultFlags.HasInstructionSet(InstructionSet_AVX10v2_X64))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVX10v2);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVX10v2_X64) && !resultFlags.HasInstructionSet(InstructionSet_AVX10v2))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVX10v2_X64);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AES) && !resultFlags.HasInstructionSet(InstructionSet_AES_X64))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AES);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AES_X64) && !resultFlags.HasInstructionSet(InstructionSet_AES))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AES_X64);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVX512VP2INTERSECT) && !resultFlags.HasInstructionSet(InstructionSet_AVX512VP2INTERSECT_X64))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVX512VP2INTERSECT);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVX512VP2INTERSECT_X64) && !resultFlags.HasInstructionSet(InstructionSet_AVX512VP2INTERSECT))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVX512VP2INTERSECT_X64);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVXIFMA) && !resultFlags.HasInstructionSet(InstructionSet_AVXIFMA_X64))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVXIFMA);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVXIFMA_X64) && !resultFlags.HasInstructionSet(InstructionSet_AVXIFMA))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVXIFMA_X64);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVXVNNI) && !resultFlags.HasInstructionSet(InstructionSet_AVXVNNI_X64))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVXVNNI);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVXVNNI_X64) && !resultFlags.HasInstructionSet(InstructionSet_AVXVNNI))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVXVNNI_X64);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_GFNI) && !resultFlags.HasInstructionSet(InstructionSet_GFNI_X64))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_GFNI);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_GFNI_X64) && !resultFlags.HasInstructionSet(InstructionSet_GFNI))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_GFNI_X64);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_SHA) && !resultFlags.HasInstructionSet(InstructionSet_SHA_X64))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_SHA);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_SHA_X64) && !resultFlags.HasInstructionSet(InstructionSet_SHA))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_SHA_X64);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_WAITPKG) && !resultFlags.HasInstructionSet(InstructionSet_WAITPKG_X64))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_WAITPKG);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_WAITPKG_X64) && !resultFlags.HasInstructionSet(InstructionSet_WAITPKG))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_WAITPKG_X64);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_X86Serialize) && !resultFlags.HasInstructionSet(InstructionSet_X86Serialize_X64))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_X86Serialize);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_X86Serialize_X64) && !resultFlags.HasInstructionSet(InstructionSet_X86Serialize))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_X86Serialize_X64);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVX) && !resultFlags.HasInstructionSet(InstructionSet_X86Base))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVX);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVX2) && !resultFlags.HasInstructionSet(InstructionSet_AVX))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVX2);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVX512) && !resultFlags.HasInstructionSet(InstructionSet_AVX2))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVX512);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVX512v2) && !resultFlags.HasInstructionSet(InstructionSet_AVX512))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVX512v2);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVX512v3) && !resultFlags.HasInstructionSet(InstructionSet_AVX512v2))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVX512v3);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVX10v1) && !resultFlags.HasInstructionSet(InstructionSet_AVX512v3))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVX10v1);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVX10v2) && !resultFlags.HasInstructionSet(InstructionSet_AVX10v1))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVX10v2);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AES) && !resultFlags.HasInstructionSet(InstructionSet_X86Base))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AES);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AES_V256) && !resultFlags.HasInstructionSet(InstructionSet_AES))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AES_V256);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AES_V256) && !resultFlags.HasInstructionSet(InstructionSet_AVX))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AES_V256);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AES_V512) && !resultFlags.HasInstructionSet(InstructionSet_AES_V256))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AES_V512);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AES_V512) && !resultFlags.HasInstructionSet(InstructionSet_AVX512))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AES_V512);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVX512VP2INTERSECT) && !resultFlags.HasInstructionSet(InstructionSet_AVX512))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVX512VP2INTERSECT);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVXIFMA) && !resultFlags.HasInstructionSet(InstructionSet_AVX2))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVXIFMA);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVXVNNI) && !resultFlags.HasInstructionSet(InstructionSet_AVX2))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVXVNNI);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_GFNI) && !resultFlags.HasInstructionSet(InstructionSet_X86Base))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_GFNI);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_GFNI_V256) && !resultFlags.HasInstructionSet(InstructionSet_GFNI))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_GFNI_V256);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_GFNI_V256) && !resultFlags.HasInstructionSet(InstructionSet_AVX))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_GFNI_V256);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_GFNI_V512) && !resultFlags.HasInstructionSet(InstructionSet_GFNI_V256))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_GFNI_V512);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_GFNI_V512) && !resultFlags.HasInstructionSet(InstructionSet_AVX512))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_GFNI_V512);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_SHA) && !resultFlags.HasInstructionSet(InstructionSet_X86Base))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_SHA);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_WAITPKG) && !resultFlags.HasInstructionSet(InstructionSet_X86Base))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_WAITPKG);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_X86Serialize) && !resultFlags.HasInstructionSet(InstructionSet_X86Base))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_X86Serialize);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVXVNNIINT) && !resultFlags.HasInstructionSet(InstructionSet_AVX2))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVXVNNIINT);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVXVNNIINT_V512) && !resultFlags.HasInstructionSet(InstructionSet_AVX10v2))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVXVNNIINT_V512);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Vector128) && !resultFlags.HasInstructionSet(InstructionSet_X86Base))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Vector128);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Vector256) && !resultFlags.HasInstructionSet(InstructionSet_AVX))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Vector256);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Vector512) && !resultFlags.HasInstructionSet(InstructionSet_AVX512))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Vector512);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_VectorT128) && !resultFlags.HasInstructionSet(InstructionSet_X86Base))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_VectorT128);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_VectorT256) && !resultFlags.HasInstructionSet(InstructionSet_AVX2))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_VectorT256);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_VectorT512) && !resultFlags.HasInstructionSet(InstructionSet_AVX512))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_VectorT512);
            }
#elif TARGET_X86
            if (resultFlags.HasInstructionSet(InstructionSet_AVX) && !resultFlags.HasInstructionSet(InstructionSet_X86Base))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVX);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVX2) && !resultFlags.HasInstructionSet(InstructionSet_AVX))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVX2);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVX512) && !resultFlags.HasInstructionSet(InstructionSet_AVX2))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVX512);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVX512v2) && !resultFlags.HasInstructionSet(InstructionSet_AVX512))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVX512v2);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVX512v3) && !resultFlags.HasInstructionSet(InstructionSet_AVX512v2))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVX512v3);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVX10v1) && !resultFlags.HasInstructionSet(InstructionSet_AVX512v3))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVX10v1);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVX10v2) && !resultFlags.HasInstructionSet(InstructionSet_AVX10v1))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVX10v2);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AES) && !resultFlags.HasInstructionSet(InstructionSet_X86Base))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AES);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AES_V256) && !resultFlags.HasInstructionSet(InstructionSet_AES))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AES_V256);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AES_V256) && !resultFlags.HasInstructionSet(InstructionSet_AVX))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AES_V256);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AES_V512) && !resultFlags.HasInstructionSet(InstructionSet_AES_V256))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AES_V512);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AES_V512) && !resultFlags.HasInstructionSet(InstructionSet_AVX512))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AES_V512);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVX512VP2INTERSECT) && !resultFlags.HasInstructionSet(InstructionSet_AVX512))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVX512VP2INTERSECT);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVXIFMA) && !resultFlags.HasInstructionSet(InstructionSet_AVX2))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVXIFMA);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVXVNNI) && !resultFlags.HasInstructionSet(InstructionSet_AVX2))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVXVNNI);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_GFNI) && !resultFlags.HasInstructionSet(InstructionSet_X86Base))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_GFNI);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_GFNI_V256) && !resultFlags.HasInstructionSet(InstructionSet_GFNI))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_GFNI_V256);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_GFNI_V256) && !resultFlags.HasInstructionSet(InstructionSet_AVX))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_GFNI_V256);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_GFNI_V512) && !resultFlags.HasInstructionSet(InstructionSet_GFNI_V256))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_GFNI_V512);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_GFNI_V512) && !resultFlags.HasInstructionSet(InstructionSet_AVX512))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_GFNI_V512);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_SHA) && !resultFlags.HasInstructionSet(InstructionSet_X86Base))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_SHA);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_WAITPKG) && !resultFlags.HasInstructionSet(InstructionSet_X86Base))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_WAITPKG);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_X86Serialize) && !resultFlags.HasInstructionSet(InstructionSet_X86Base))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_X86Serialize);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVXVNNIINT) && !resultFlags.HasInstructionSet(InstructionSet_AVX2))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVXVNNIINT);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_AVXVNNIINT_V512) && !resultFlags.HasInstructionSet(InstructionSet_AVX10v2))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_AVXVNNIINT_V512);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Vector128) && !resultFlags.HasInstructionSet(InstructionSet_X86Base))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Vector128);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Vector256) && !resultFlags.HasInstructionSet(InstructionSet_AVX))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Vector256);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_Vector512) && !resultFlags.HasInstructionSet(InstructionSet_AVX512))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_Vector512);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_VectorT128) && !resultFlags.HasInstructionSet(InstructionSet_X86Base))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_VectorT128);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_VectorT256) && !resultFlags.HasInstructionSet(InstructionSet_AVX2))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_VectorT256);
            }

            if (resultFlags.HasInstructionSet(InstructionSet_VectorT512) && !resultFlags.HasInstructionSet(InstructionSet_AVX512))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_VectorT512);
            }
#endif // TARGET_X86
        }
        while (!oldFlags.Equals(resultFlags));

        return resultFlags;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string InstructionSetToString(CORINFO_InstructionSet instructionSet)
    {
        return instructionSet switch {
#if TARGET_ARM64
            InstructionSet_ArmBase => "ArmBase",
            InstructionSet_ArmBase_Arm64 => "ArmBase_Arm64",
            InstructionSet_AdvSimd => "AdvSimd",
            InstructionSet_AdvSimd_Arm64 => "AdvSimd_Arm64",
            InstructionSet_Aes => "Aes",
            InstructionSet_Aes_Arm64 => "Aes_Arm64",
            InstructionSet_Crc32 => "Crc32",
            InstructionSet_Crc32_Arm64 => "Crc32_Arm64",
            InstructionSet_Dp => "Dp",
            InstructionSet_Dp_Arm64 => "Dp_Arm64",
            InstructionSet_Rdm => "Rdm",
            InstructionSet_Rdm_Arm64 => "Rdm_Arm64",
            InstructionSet_Sha1 => "Sha1",
            InstructionSet_Sha1_Arm64 => "Sha1_Arm64",
            InstructionSet_Sha256 => "Sha256",
            InstructionSet_Sha256_Arm64 => "Sha256_Arm64",
            InstructionSet_Atomics => "Atomics",
            InstructionSet_Vector64 => "Vector64",
            InstructionSet_Vector128 => "Vector128",
            InstructionSet_VectorT => "VectorT",
            InstructionSet_Dczva => "Dczva",
            InstructionSet_Rcpc => "Rcpc",
            InstructionSet_VectorT128 => "VectorT128",
            InstructionSet_Rcpc2 => "Rcpc2",
            InstructionSet_Sve => "Sve",
            InstructionSet_Sve_Arm64 => "Sve_Arm64",
            InstructionSet_Sve2 => "Sve2",
            InstructionSet_Sve2_Arm64 => "Sve2_Arm64",
            InstructionSet_Sha3 => "Sha3",
            InstructionSet_Sha3_Arm64 => "Sha3_Arm64",
            InstructionSet_Sm4 => "Sm4",
            InstructionSet_Sm4_Arm64 => "Sm4_Arm64",
            InstructionSet_SveAes => "SveAes",
            InstructionSet_SveAes_Arm64 => "SveAes_Arm64",
            InstructionSet_SveSha3 => "SveSha3",
            InstructionSet_SveSha3_Arm64 => "SveSha3_Arm64",
            InstructionSet_SveSm4 => "SveSm4",
            InstructionSet_SveSm4_Arm64 => "SveSm4_Arm64",
#elif TARGET_RISCV64
            InstructionSet_RiscV64Base => "RiscV64Base",
            InstructionSet_Zba => "Zba",
            InstructionSet_Zbb => "Zbb",
            InstructionSet_Zbs => "Zbs",
#elif TARGET_AMD64
            InstructionSet_X86Base => "X86Base",
            InstructionSet_X86Base_X64 => "X86Base_X64",
            InstructionSet_AVX => "AVX",
            InstructionSet_AVX_X64 => "AVX_X64",
            InstructionSet_AVX2 => "AVX2",
            InstructionSet_AVX2_X64 => "AVX2_X64",
            InstructionSet_AVX512 => "AVX512",
            InstructionSet_AVX512_X64 => "AVX512_X64",
            InstructionSet_AVX512v2 => "AVX512v2",
            InstructionSet_AVX512v2_X64 => "AVX512v2_X64",
            InstructionSet_AVX512v3 => "AVX512v3",
            InstructionSet_AVX512v3_X64 => "AVX512v3_X64",
            InstructionSet_AVX10v1 => "AVX10v1",
            InstructionSet_AVX10v1_X64 => "AVX10v1_X64",
            InstructionSet_AVX10v2 => "AVX10v2",
            InstructionSet_AVX10v2_X64 => "AVX10v2_X64",
            InstructionSet_APX => "APX",
            InstructionSet_AES => "AES",
            InstructionSet_AES_X64 => "AES_X64",
            InstructionSet_AES_V256 => "AES_V256",
            InstructionSet_AES_V512 => "AES_V512",
            InstructionSet_AVX512VP2INTERSECT => "AVX512VP2INTERSECT",
            InstructionSet_AVX512VP2INTERSECT_X64 => "AVX512VP2INTERSECT_X64",
            InstructionSet_AVXIFMA => "AVXIFMA",
            InstructionSet_AVXIFMA_X64 => "AVXIFMA_X64",
            InstructionSet_AVXVNNI => "AVXVNNI",
            InstructionSet_AVXVNNI_X64 => "AVXVNNI_X64",
            InstructionSet_AVX512BMM => "AVX512BMM",
            InstructionSet_GFNI => "GFNI",
            InstructionSet_GFNI_X64 => "GFNI_X64",
            InstructionSet_GFNI_V256 => "GFNI_V256",
            InstructionSet_GFNI_V512 => "GFNI_V512",
            InstructionSet_SHA => "SHA",
            InstructionSet_SHA_X64 => "SHA_X64",
            InstructionSet_WAITPKG => "WAITPKG",
            InstructionSet_WAITPKG_X64 => "WAITPKG_X64",
            InstructionSet_X86Serialize => "X86Serialize",
            InstructionSet_X86Serialize_X64 => "X86Serialize_X64",
            InstructionSet_Vector128 => "Vector128",
            InstructionSet_Vector256 => "Vector256",
            InstructionSet_Vector512 => "Vector512",
            InstructionSet_VectorT128 => "VectorT128",
            InstructionSet_VectorT256 => "VectorT256",
            InstructionSet_VectorT512 => "VectorT512",
            InstructionSet_AVXVNNIINT => "AVXVNNIINT",
            InstructionSet_AVXVNNIINT_V512 => "AVXVNNIINT_V512",
#elif TARGET_X86
            InstructionSet_X86Base => "X86Base",
            InstructionSet_AVX => "AVX",
            InstructionSet_AVX2 => "AVX2",
            InstructionSet_AVX512 => "AVX512",
            InstructionSet_AVX512v2 => "AVX512v2",
            InstructionSet_AVX512v3 => "AVX512v3",
            InstructionSet_AVX10v1 => "AVX10v1",
            InstructionSet_AVX10v2 => "AVX10v2",
            InstructionSet_APX => "APX",
            InstructionSet_AES => "AES",
            InstructionSet_AES_V256 => "AES_V256",
            InstructionSet_AES_V512 => "AES_V512",
            InstructionSet_AVX512VP2INTERSECT => "AVX512VP2INTERSECT",
            InstructionSet_AVXIFMA => "AVXIFMA",
            InstructionSet_AVXVNNI => "AVXVNNI",
            InstructionSet_AVX512BMM => "AVX512BMM",
            InstructionSet_GFNI => "GFNI",
            InstructionSet_GFNI_V256 => "GFNI_V256",
            InstructionSet_GFNI_V512 => "GFNI_V512",
            InstructionSet_SHA => "SHA",
            InstructionSet_WAITPKG => "WAITPKG",
            InstructionSet_X86Serialize => "X86Serialize",
            InstructionSet_Vector128 => "Vector128",
            InstructionSet_Vector256 => "Vector256",
            InstructionSet_Vector512 => "Vector512",
            InstructionSet_VectorT128 => "VectorT128",
            InstructionSet_VectorT256 => "VectorT256",
            InstructionSet_VectorT512 => "VectorT512",
            InstructionSet_AVXVNNIINT => "AVXVNNIINT",
            InstructionSet_AVXVNNIINT_V512 => "AVXVNNIINT_V512",
#endif
            _ => "UnknownInstructionSet",
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CORINFO_InstructionSet InstructionSetFromR2RInstructionSet(ReadyToRunInstructionSet r2rSet)
    {
        return r2rSet switch {
#if TARGET_ARM64
            READYTORUN_INSTRUCTION_ArmBase => InstructionSet_ArmBase,
            READYTORUN_INSTRUCTION_AdvSimd => InstructionSet_AdvSimd,
            READYTORUN_INSTRUCTION_Aes => InstructionSet_Aes,
            READYTORUN_INSTRUCTION_Crc32 => InstructionSet_Crc32,
            READYTORUN_INSTRUCTION_Dp => InstructionSet_Dp,
            READYTORUN_INSTRUCTION_Rdm => InstructionSet_Rdm,
            READYTORUN_INSTRUCTION_Sha1 => InstructionSet_Sha1,
            READYTORUN_INSTRUCTION_Sha256 => InstructionSet_Sha256,
            READYTORUN_INSTRUCTION_Atomics => InstructionSet_Atomics,
            READYTORUN_INSTRUCTION_Rcpc => InstructionSet_Rcpc,
            READYTORUN_INSTRUCTION_VectorT128 => InstructionSet_VectorT128,
            READYTORUN_INSTRUCTION_Rcpc2 => InstructionSet_Rcpc2,
            READYTORUN_INSTRUCTION_Sve => InstructionSet_Sve,
            READYTORUN_INSTRUCTION_Sve2 => InstructionSet_Sve2,
            READYTORUN_INSTRUCTION_Sha3 => InstructionSet_Sha3,
            READYTORUN_INSTRUCTION_Sm4 => InstructionSet_Sm4,
            READYTORUN_INSTRUCTION_SveAes => InstructionSet_SveAes,
            READYTORUN_INSTRUCTION_SveSha3 => InstructionSet_SveSha3,
            READYTORUN_INSTRUCTION_SveSm4 => InstructionSet_SveSm4,
#elif TARGET_RISCV64               
            READYTORUN_INSTRUCTION_RiscV64Base => InstructionSet_RiscV64Base,
            READYTORUN_INSTRUCTION_Zba => InstructionSet_Zba,
            READYTORUN_INSTRUCTION_Zbb => InstructionSet_Zbb,
            READYTORUN_INSTRUCTION_Zbs => InstructionSet_Zbs,
#elif TARGET_AMD64             
            READYTORUN_INSTRUCTION_X86Base => InstructionSet_X86Base,
            READYTORUN_INSTRUCTION_Sse => InstructionSet_X86Base,
            READYTORUN_INSTRUCTION_Sse2 => InstructionSet_X86Base,
            READYTORUN_INSTRUCTION_Sse42 => InstructionSet_X86Base,
            READYTORUN_INSTRUCTION_Sse3 => InstructionSet_X86Base,
            READYTORUN_INSTRUCTION_Ssse3 => InstructionSet_X86Base,
            READYTORUN_INSTRUCTION_Sse41 => InstructionSet_X86Base,
            READYTORUN_INSTRUCTION_Popcnt => InstructionSet_X86Base,
            READYTORUN_INSTRUCTION_Avx => InstructionSet_AVX,
            READYTORUN_INSTRUCTION_Avx2 => InstructionSet_AVX2,
            READYTORUN_INSTRUCTION_Bmi1 => InstructionSet_AVX2,
            READYTORUN_INSTRUCTION_Bmi2 => InstructionSet_AVX2,
            READYTORUN_INSTRUCTION_F16C => InstructionSet_AVX2,
            READYTORUN_INSTRUCTION_Fma => InstructionSet_AVX2,
            READYTORUN_INSTRUCTION_Lzcnt => InstructionSet_AVX2,
            READYTORUN_INSTRUCTION_Movbe => InstructionSet_AVX2,
            READYTORUN_INSTRUCTION_Evex => InstructionSet_AVX512,
            READYTORUN_INSTRUCTION_Avx512F => InstructionSet_AVX512,
            READYTORUN_INSTRUCTION_Avx512F_VL => InstructionSet_AVX512,
            READYTORUN_INSTRUCTION_Avx512BW => InstructionSet_AVX512,
            READYTORUN_INSTRUCTION_Avx512BW_VL => InstructionSet_AVX512,
            READYTORUN_INSTRUCTION_Avx512CD => InstructionSet_AVX512,
            READYTORUN_INSTRUCTION_Avx512CD_VL => InstructionSet_AVX512,
            READYTORUN_INSTRUCTION_Avx512DQ => InstructionSet_AVX512,
            READYTORUN_INSTRUCTION_Avx512DQ_VL => InstructionSet_AVX512,
            READYTORUN_INSTRUCTION_Avx512Ifma => InstructionSet_AVX512v2,
            READYTORUN_INSTRUCTION_Avx512Vbmi => InstructionSet_AVX512v2,
            READYTORUN_INSTRUCTION_Avx512Vbmi_VL => InstructionSet_AVX512v2,
            READYTORUN_INSTRUCTION_Avx512Bitalg => InstructionSet_AVX512v3,
            READYTORUN_INSTRUCTION_Avx512Bitalg_VL => InstructionSet_AVX512v3,
            READYTORUN_INSTRUCTION_Avx512Vbmi2 => InstructionSet_AVX512v3,
            READYTORUN_INSTRUCTION_Avx512Vbmi2_VL => InstructionSet_AVX512v3,
            READYTORUN_INSTRUCTION_Avx512Vnni => InstructionSet_AVX512v3,
            READYTORUN_INSTRUCTION_Avx512Vpopcntdq => InstructionSet_AVX512v3,
            READYTORUN_INSTRUCTION_Avx512Vpopcntdq_VL => InstructionSet_AVX512v3,
            READYTORUN_INSTRUCTION_Avx512Bf16 => InstructionSet_AVX10v1,
            READYTORUN_INSTRUCTION_Avx512Bf16_VL => InstructionSet_AVX10v1,
            READYTORUN_INSTRUCTION_Avx512Fp16 => InstructionSet_AVX10v1,
            READYTORUN_INSTRUCTION_Avx512Fp16_VL => InstructionSet_AVX10v1,
            READYTORUN_INSTRUCTION_Avx10v1 => InstructionSet_AVX10v1,
            READYTORUN_INSTRUCTION_Avx10v1_V512 => InstructionSet_AVX10v1,
            READYTORUN_INSTRUCTION_Avx10v2 => InstructionSet_AVX10v2,
            READYTORUN_INSTRUCTION_Avx10v2_V512 => InstructionSet_AVX10v2,
            READYTORUN_INSTRUCTION_Apx => InstructionSet_APX,
            READYTORUN_INSTRUCTION_Aes => InstructionSet_AES,
            READYTORUN_INSTRUCTION_Aes_V256 => InstructionSet_AES_V256,
            READYTORUN_INSTRUCTION_Aes_V512 => InstructionSet_AES_V512,
            READYTORUN_INSTRUCTION_Pclmulqdq => InstructionSet_AES,
            READYTORUN_INSTRUCTION_Pclmulqdq_V256 => InstructionSet_AES_V256,
            READYTORUN_INSTRUCTION_Pclmulqdq_V512 => InstructionSet_AES_V512,
            READYTORUN_INSTRUCTION_Avx512Vp2intersect => InstructionSet_AVX512VP2INTERSECT,
            READYTORUN_INSTRUCTION_Avx512Vp2intersect_VL => InstructionSet_AVX512VP2INTERSECT,
            READYTORUN_INSTRUCTION_AvxIfma => InstructionSet_AVXIFMA,
            READYTORUN_INSTRUCTION_AvxVnni => InstructionSet_AVXVNNI,
            READYTORUN_INSTRUCTION_Avx512Bmm => InstructionSet_AVX512BMM,
            READYTORUN_INSTRUCTION_Gfni => InstructionSet_GFNI,
            READYTORUN_INSTRUCTION_Gfni_V256 => InstructionSet_GFNI_V256,
            READYTORUN_INSTRUCTION_Gfni_V512 => InstructionSet_GFNI_V512,
            READYTORUN_INSTRUCTION_Sha => InstructionSet_SHA,
            READYTORUN_INSTRUCTION_WaitPkg => InstructionSet_WAITPKG,
            READYTORUN_INSTRUCTION_X86Serialize => InstructionSet_X86Serialize,
            READYTORUN_INSTRUCTION_VectorT128 => InstructionSet_VectorT128,
            READYTORUN_INSTRUCTION_VectorT256 => InstructionSet_VectorT256,
            READYTORUN_INSTRUCTION_VectorT512 => InstructionSet_VectorT512,
            READYTORUN_INSTRUCTION_AvxVnniInt8 => InstructionSet_AVXVNNIINT,
            READYTORUN_INSTRUCTION_AvxVnniInt8_V512 => InstructionSet_AVXVNNIINT_V512,
            READYTORUN_INSTRUCTION_AvxVnniInt16 => InstructionSet_AVXVNNIINT,
            READYTORUN_INSTRUCTION_AvxVnniInt16_V512 => InstructionSet_AVXVNNIINT_V512,
#elif TARGET_X86
            READYTORUN_INSTRUCTION_X86Base => InstructionSet_X86Base,
            READYTORUN_INSTRUCTION_Sse => InstructionSet_X86Base,
            READYTORUN_INSTRUCTION_Sse2 => InstructionSet_X86Base,
            READYTORUN_INSTRUCTION_Sse42 => InstructionSet_X86Base,
            READYTORUN_INSTRUCTION_Sse3 => InstructionSet_X86Base,
            READYTORUN_INSTRUCTION_Ssse3 => InstructionSet_X86Base,
            READYTORUN_INSTRUCTION_Sse41 => InstructionSet_X86Base,
            READYTORUN_INSTRUCTION_Popcnt => InstructionSet_X86Base,
            READYTORUN_INSTRUCTION_Avx => InstructionSet_AVX,
            READYTORUN_INSTRUCTION_Avx2 => InstructionSet_AVX2,
            READYTORUN_INSTRUCTION_Bmi1 => InstructionSet_AVX2,
            READYTORUN_INSTRUCTION_Bmi2 => InstructionSet_AVX2,
            READYTORUN_INSTRUCTION_F16C => InstructionSet_AVX2,
            READYTORUN_INSTRUCTION_Fma => InstructionSet_AVX2,
            READYTORUN_INSTRUCTION_Lzcnt => InstructionSet_AVX2,
            READYTORUN_INSTRUCTION_Movbe => InstructionSet_AVX2,
            READYTORUN_INSTRUCTION_Evex => InstructionSet_AVX512,
            READYTORUN_INSTRUCTION_Avx512F => InstructionSet_AVX512,
            READYTORUN_INSTRUCTION_Avx512F_VL => InstructionSet_AVX512,
            READYTORUN_INSTRUCTION_Avx512BW => InstructionSet_AVX512,
            READYTORUN_INSTRUCTION_Avx512BW_VL => InstructionSet_AVX512,
            READYTORUN_INSTRUCTION_Avx512CD => InstructionSet_AVX512,
            READYTORUN_INSTRUCTION_Avx512CD_VL => InstructionSet_AVX512,
            READYTORUN_INSTRUCTION_Avx512DQ => InstructionSet_AVX512,
            READYTORUN_INSTRUCTION_Avx512DQ_VL => InstructionSet_AVX512,
            READYTORUN_INSTRUCTION_Avx512Ifma => InstructionSet_AVX512v2,
            READYTORUN_INSTRUCTION_Avx512Vbmi => InstructionSet_AVX512v2,
            READYTORUN_INSTRUCTION_Avx512Vbmi_VL => InstructionSet_AVX512v2,
            READYTORUN_INSTRUCTION_Avx512Bitalg => InstructionSet_AVX512v3,
            READYTORUN_INSTRUCTION_Avx512Bitalg_VL => InstructionSet_AVX512v3,
            READYTORUN_INSTRUCTION_Avx512Vbmi2 => InstructionSet_AVX512v3,
            READYTORUN_INSTRUCTION_Avx512Vbmi2_VL => InstructionSet_AVX512v3,
            READYTORUN_INSTRUCTION_Avx512Vnni => InstructionSet_AVX512v3,
            READYTORUN_INSTRUCTION_Avx512Vpopcntdq => InstructionSet_AVX512v3,
            READYTORUN_INSTRUCTION_Avx512Vpopcntdq_VL => InstructionSet_AVX512v3,
            READYTORUN_INSTRUCTION_Avx512Bf16 => InstructionSet_AVX10v1,
            READYTORUN_INSTRUCTION_Avx512Bf16_VL => InstructionSet_AVX10v1,
            READYTORUN_INSTRUCTION_Avx512Fp16 => InstructionSet_AVX10v1,
            READYTORUN_INSTRUCTION_Avx512Fp16_VL => InstructionSet_AVX10v1,
            READYTORUN_INSTRUCTION_Avx10v1 => InstructionSet_AVX10v1,
            READYTORUN_INSTRUCTION_Avx10v1_V512 => InstructionSet_AVX10v1,
            READYTORUN_INSTRUCTION_Avx10v2 => InstructionSet_AVX10v2,
            READYTORUN_INSTRUCTION_Avx10v2_V512 => InstructionSet_AVX10v2,
            READYTORUN_INSTRUCTION_Apx => InstructionSet_APX,
            READYTORUN_INSTRUCTION_Aes => InstructionSet_AES,
            READYTORUN_INSTRUCTION_Aes_V256 => InstructionSet_AES_V256,
            READYTORUN_INSTRUCTION_Aes_V512 => InstructionSet_AES_V512,
            READYTORUN_INSTRUCTION_Pclmulqdq => InstructionSet_AES,
            READYTORUN_INSTRUCTION_Pclmulqdq_V256 => InstructionSet_AES_V256,
            READYTORUN_INSTRUCTION_Pclmulqdq_V512 => InstructionSet_AES_V512,
            READYTORUN_INSTRUCTION_Avx512Vp2intersect => InstructionSet_AVX512VP2INTERSECT,
            READYTORUN_INSTRUCTION_Avx512Vp2intersect_VL => InstructionSet_AVX512VP2INTERSECT,
            READYTORUN_INSTRUCTION_AvxIfma => InstructionSet_AVXIFMA,
            READYTORUN_INSTRUCTION_AvxVnni => InstructionSet_AVXVNNI,
            READYTORUN_INSTRUCTION_Avx512Bmm => InstructionSet_AVX512BMM,
            READYTORUN_INSTRUCTION_Gfni => InstructionSet_GFNI,
            READYTORUN_INSTRUCTION_Gfni_V256 => InstructionSet_GFNI_V256,
            READYTORUN_INSTRUCTION_Gfni_V512 => InstructionSet_GFNI_V512,
            READYTORUN_INSTRUCTION_Sha => InstructionSet_SHA,
            READYTORUN_INSTRUCTION_WaitPkg => InstructionSet_WAITPKG,
            READYTORUN_INSTRUCTION_X86Serialize => InstructionSet_X86Serialize,
            READYTORUN_INSTRUCTION_VectorT128 => InstructionSet_VectorT128,
            READYTORUN_INSTRUCTION_VectorT256 => InstructionSet_VectorT256,
            READYTORUN_INSTRUCTION_VectorT512 => InstructionSet_VectorT512,
            READYTORUN_INSTRUCTION_AvxVnniInt8 => InstructionSet_AVXVNNIINT,
            READYTORUN_INSTRUCTION_AvxVnniInt8_V512 => InstructionSet_AVXVNNIINT_V512,
            READYTORUN_INSTRUCTION_AvxVnniInt16 => InstructionSet_AVXVNNIINT,
            READYTORUN_INSTRUCTION_AvxVnniInt16_V512 => InstructionSet_AVXVNNIINT_V512,
#endif
            _ => InstructionSet_ILLEGAL,
        };
    }
}
