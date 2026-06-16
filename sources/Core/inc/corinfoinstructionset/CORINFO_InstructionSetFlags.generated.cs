// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct CORINFO_InstructionSetFlags
{
    public void Set64BitInstructionSetVariants()
    {
#if TARGET_AMD64
        if (HasInstructionSet(InstructionSet_X86Base))                    
        {
            AddInstructionSet(InstructionSet_X86Base_X64);
        }

        if (HasInstructionSet(InstructionSet_AVX))                    
        {
            AddInstructionSet(InstructionSet_AVX_X64);
        }

        if (HasInstructionSet(InstructionSet_AVX2))                    
        {
            AddInstructionSet(InstructionSet_AVX2_X64);
        }

        if (HasInstructionSet(InstructionSet_AVX512))                    
        {
            AddInstructionSet(InstructionSet_AVX512_X64);
        }

        if (HasInstructionSet(InstructionSet_AVX512v2))                    
        {
            AddInstructionSet(InstructionSet_AVX512v2_X64);
        }

        if (HasInstructionSet(InstructionSet_AVX512v3))                    
        {
            AddInstructionSet(InstructionSet_AVX512v3_X64);
        }

        if (HasInstructionSet(InstructionSet_AVX10v1))                    
        {
            AddInstructionSet(InstructionSet_AVX10v1_X64);
        }

        if (HasInstructionSet(InstructionSet_AVX10v2))                    
        {
            AddInstructionSet(InstructionSet_AVX10v2_X64);
        }

        if (HasInstructionSet(InstructionSet_AES))                    
        {
            AddInstructionSet(InstructionSet_AES_X64);
        }

        if (HasInstructionSet(InstructionSet_AVX512VP2INTERSECT))                    
        {
            AddInstructionSet(InstructionSet_AVX512VP2INTERSECT_X64);
        }

        if (HasInstructionSet(InstructionSet_AVXIFMA))                    
        {
            AddInstructionSet(InstructionSet_AVXIFMA_X64);
        }

        if (HasInstructionSet(InstructionSet_AVXVNNI))                    
        {
            AddInstructionSet(InstructionSet_AVXVNNI_X64);
        }

        if (HasInstructionSet(InstructionSet_GFNI))                    
        {
            AddInstructionSet(InstructionSet_GFNI_X64);
        }

        if (HasInstructionSet(InstructionSet_SHA))                    
        {
            AddInstructionSet(InstructionSet_SHA_X64);
        }

        if (HasInstructionSet(InstructionSet_WAITPKG))                    
        {
            AddInstructionSet(InstructionSet_WAITPKG_X64);
        }

        if (HasInstructionSet(InstructionSet_X86Serialize))                    
        {
            AddInstructionSet(InstructionSet_X86Serialize_X64);
        }
#elif TARGET_ARM64
        if (HasInstructionSet(InstructionSet_ArmBase))                    
        {
            AddInstructionSet(InstructionSet_ArmBase_Arm64);
        }

        if (HasInstructionSet(InstructionSet_AdvSimd))                    
        {
            AddInstructionSet(InstructionSet_AdvSimd_Arm64);
        }

        if (HasInstructionSet(InstructionSet_Aes))                    
        {
            AddInstructionSet(InstructionSet_Aes_Arm64);
        }

        if (HasInstructionSet(InstructionSet_Crc32))                    
        {
            AddInstructionSet(InstructionSet_Crc32_Arm64);
        }

        if (HasInstructionSet(InstructionSet_Dp))                    
        {
            AddInstructionSet(InstructionSet_Dp_Arm64);
        }

        if (HasInstructionSet(InstructionSet_Rdm))                    
        {
            AddInstructionSet(InstructionSet_Rdm_Arm64);
        }

        if (HasInstructionSet(InstructionSet_Sha1))                    
        {
            AddInstructionSet(InstructionSet_Sha1_Arm64);
        }

        if (HasInstructionSet(InstructionSet_Sha256))                    
        {
            AddInstructionSet(InstructionSet_Sha256_Arm64);
        }

        if (HasInstructionSet(InstructionSet_Sve))                    
        {
            AddInstructionSet(InstructionSet_Sve_Arm64);
        }

        if (HasInstructionSet(InstructionSet_Sve2))                    
        {
            AddInstructionSet(InstructionSet_Sve2_Arm64);
        }

        if (HasInstructionSet(InstructionSet_Sha3))                    
        {
            AddInstructionSet(InstructionSet_Sha3_Arm64);
        }

        if (HasInstructionSet(InstructionSet_Sm4))                    
        {
            AddInstructionSet(InstructionSet_Sm4_Arm64);
        }

        if (HasInstructionSet(InstructionSet_SveAes))                    
        {
            AddInstructionSet(InstructionSet_SveAes_Arm64);
        }

        if (HasInstructionSet(InstructionSet_SveSha3))                    
        {
            AddInstructionSet(InstructionSet_SveSha3_Arm64);
        }

        if (HasInstructionSet(InstructionSet_SveSm4))                    
        {
            AddInstructionSet(InstructionSet_SveSm4_Arm64);
        }
#endif
    }
}