// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genZeroInitFrameUsingBlockInit(int untrLclHi, int untrLclLo, regNumber initReg,
        ref bool initRegZeroed)
    {
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());
        assert(UseBlockInit);
        assert(untrLclHi > untrLclLo);

        var bytesToWrite = untrLclHi - untrLclLo;
        const regNumber zeroSimdReg = REG_V16;
        var simdRegZeroed = false;
        var simdRegPairSizeBytes = 2 * FP_REGSIZE_BYTES;
        var addrReg = REG_R9;

        if (addrReg == initReg)
        {
            initRegZeroed = false;
        }

        var addrOffset = 0;
        var frameReg = IsFramePointerUsed ? REG_FPBASE : REG_SPBASE;
        // The address register plus this offset always identifies the next bytes to initialize.
        const int bytesUseZeroingLoop = 192;

        if (bytesToWrite >= bytesUseZeroingLoop)
        {
            // The loop writes 64 bytes per iteration; smaller regions are faster unrolled.
            const int bytesUseDataCacheZeroInstruction = 256;

            Emitter.emitIns_R_I(INS_movi, EA_16BYTE, zeroSimdReg, 0, INS_OPTS_16B);
            simdRegZeroed = true;

            if ((bytesToWrite >= bytesUseDataCacheZeroInstruction) &&
                _compiler.compOpportunisticallyDependsOn(InstructionSet_Dczva))
            {
                _ = genInstrWithConstant(INS_add, EA_PTRSIZE, addrReg, frameReg, untrLclLo + 64, addrReg);
                addrOffset = -64;

                // Keep DC ZVA within the requested region by explicitly storing its first and last 64 bytes.
                const regNumber endAddrReg = REG_R10;

                if (endAddrReg == initReg)
                {
                    initRegZeroed = false;
                }

                _ = genInstrWithConstant(INS_add, EA_PTRSIZE, endAddrReg, frameReg, untrLclHi - 64, endAddrReg);

                Emitter.emitIns_R_R_R_I(INS_stp, EA_16BYTE, zeroSimdReg, zeroSimdReg, addrReg, addrOffset);
                addrOffset += simdRegPairSizeBytes;

                Emitter.emitIns_R_R_R_I(INS_stp, EA_16BYTE, zeroSimdReg, zeroSimdReg, addrReg, addrOffset);
                addrOffset += simdRegPairSizeBytes;

                assert(addrOffset == 0);

                Emitter.emitIns_R_R_I_I(INS_bfm, EA_PTRSIZE, addrReg, REG_ZR, 0, 5);

                var loopHead = genCreateTempLabel();
                genDefineInlineTempLabel(loopHead);
                Emitter.emitIns_R(INS_dczva, EA_PTRSIZE, addrReg);
                Emitter.emitIns_R_R_I(INS_add, EA_PTRSIZE, addrReg, addrReg, 64);
                Emitter.emitIns_R_R(INS_cmp, EA_PTRSIZE, addrReg, endAddrReg);
                Emitter.emitIns_ShortJ(INS_blo, loopHead);

                addrReg = endAddrReg;
                bytesToWrite = 64;
            }
            else
            {
                _ = genInstrWithConstant(INS_add, EA_PTRSIZE, addrReg, frameReg, untrLclLo - 32, addrReg);
                addrOffset = 32;

                const regNumber countReg = REG_R10;

                if (countReg == initReg)
                {
                    initRegZeroed = false;
                }

                instGen_Set_Reg_To_Imm(EA_PTRSIZE, countReg, bytesToWrite - 64);

                var loopHead = genCreateTempLabel();
                genDefineInlineTempLabel(loopHead);
                Emitter.emitIns_R_R_R_I(INS_stp, EA_16BYTE, zeroSimdReg, zeroSimdReg, addrReg, 32);
                Emitter.emitIns_R_R_R_I(INS_stp, EA_16BYTE, zeroSimdReg, zeroSimdReg, addrReg, 64,
                    INS_OPTS_PRE_INDEX);

                Emitter.emitIns_R_R_I(INS_subs, EA_PTRSIZE, countReg, countReg, 64);
                Emitter.emitIns_ShortJ(INS_bge, loopHead);

                bytesToWrite %= 64;
            }
        }
        else
        {
            _ = genInstrWithConstant(INS_add, EA_PTRSIZE, addrReg, frameReg, untrLclLo, addrReg);
        }

        if (bytesToWrite >= simdRegPairSizeBytes)
        {
            if (!simdRegZeroed)
            {
                Emitter.emitIns_R_I(INS_movi, EA_16BYTE, zeroSimdReg, 0, INS_OPTS_16B);
            }

            for (; bytesToWrite >= simdRegPairSizeBytes; bytesToWrite -= simdRegPairSizeBytes)
            {
                Emitter.emitIns_R_R_R_I(INS_stp, EA_16BYTE, zeroSimdReg, zeroSimdReg, addrReg, addrOffset);
                addrOffset += simdRegPairSizeBytes;
            }
        }

        var regPairSizeBytes = 2 * REGSIZE_BYTES;

        if (bytesToWrite >= regPairSizeBytes)
        {
            Emitter.emitIns_R_R_R_I(INS_stp, EA_PTRSIZE, REG_ZR, REG_ZR, addrReg, addrOffset);
            addrOffset += regPairSizeBytes;
            bytesToWrite -= regPairSizeBytes;
        }

        if (bytesToWrite >= REGSIZE_BYTES)
        {
            Emitter.emitIns_R_R_I(INS_str, EA_PTRSIZE, REG_ZR, addrReg, addrOffset);
            addrOffset += REGSIZE_BYTES;
            bytesToWrite -= REGSIZE_BYTES;
        }

        if (bytesToWrite == sizeof(int))
        {
            Emitter.emitIns_R_R_I(INS_str, EA_4BYTE, REG_ZR, addrReg, addrOffset);
            bytesToWrite = 0;
        }

        assert(bytesToWrite == 0);
    }
}
#endif
