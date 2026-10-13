// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Numerics;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.Globals;
using static RyuJitSharp.insFlags;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.SpecialCodeKind;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForIndexAddrArmArch(GenTreeIndexAddr tree)
    {
        var baseAddress = tree.Arr;
        var index = tree.Index;

        _ = genConsumeReg(baseAddress);
        _ = genConsumeReg(index);

        _gcInfo.gcMarkRegPtrVal(baseAddress.RegNum, baseAddress.Type);
        assert(!varTypeIsGC(index.Type));
        assert(index.IsUsedFromReg);

        var tempReg = _internalRegisters.Extract(tree);
        var indexReg = index.RegNum;

        if (tree.IsBoundsChecked)
        {
            if (Configuration.TARGET_ARM)
            {
                Emitter.emitIns_R_R_I(
                    INS_ldr, EA_4BYTE, tempReg, baseAddress.RegNum, tree.LenOffset,
                    INS_FLAGS_DONT_CARE, INS_OPTS_NONE);
            }
            else
            {
                Emitter.emitIns_R_R_I(INS_ldr, EA_4BYTE, tempReg, baseAddress.RegNum, tree.LenOffset);
            }
            Emitter.emitIns_R_R(INS_cmp, index.Type.EmitActualSize, indexReg, tempReg);
            genJumpToThrowHlpBlk(EJ_hs, SCK_RNGCHK_FAIL);
        }

        var elementSize = unchecked((uint)tree.ElemSize);
        if (uint.IsPow2(elementSize) && (elementSize <= 32768))
        {
            var scale = BitOperations.TrailingZeroCount(elementSize);

            if (Configuration.TARGET_ARM64 && (index.Type != TYP_I_IMPL))
            {
                if (scale <= 4)
                {
                    Emitter.emitIns_R_R_R_I(
                        INS_add, tree.Type.EmitActualSize, tree.RegNum, baseAddress.RegNum, indexReg, scale,
                        INS_OPTS_UXTW);
                }
                else
                {
                    _ = Emitter.emitIns_Mov(INS_mov, EA_4BYTE, tempReg, indexReg, canSkip: false);
                    indexReg = tempReg;
                    genScaledAddArmArch(tree.Type.EmitActualSize, tree.RegNum, baseAddress.RegNum, indexReg, scale);
                }
            }
            else
            {
                genScaledAddArmArch(tree.Type.EmitActualSize, tree.RegNum, baseAddress.RegNum, indexReg, scale);
            }
        }
        else
        {
            if (Configuration.TARGET_ARM64 && (index.Type != TYP_I_IMPL))
            {
                var tempIndexReg = _internalRegisters.Extract(tree);
                _ = Emitter.emitIns_Mov(INS_mov, EA_4BYTE, tempIndexReg, indexReg, canSkip: false);
                indexReg = tempIndexReg;
            }

            instGen_Set_Reg_To_Imm(EA_4BYTE, tempReg, unchecked((nint)elementSize));
            var multiplyAddInstruction = Configuration.TARGET_ARM ? INS_mla : INS_madd;
            Emitter.emitIns_R_R_R_R(
                multiplyAddInstruction, tree.Type.EmitActualSize, tree.RegNum, indexReg, tempReg, baseAddress.RegNum);
        }

        if (Configuration.TARGET_ARM)
        {
            Emitter.emitIns_R_R_I(
                INS_add, tree.Type.EmitActualSize, tree.RegNum, tree.RegNum, tree.ElemOffset,
                INS_FLAGS_DONT_CARE, INS_OPTS_NONE);
        }
        else
        {
            Emitter.emitIns_R_R_I(
                INS_add, tree.Type.EmitActualSize, tree.RegNum, tree.RegNum, tree.ElemOffset, INS_OPTS_NONE);
        }

        _gcInfo.gcMarkRegSetNpt(baseAddress.RegMask);
        genProduceReg(tree);
    }

    private void genScaledAddArmArch(emitAttr attr, regNumber targetReg, regNumber baseReg, regNumber indexReg, int scale)
    {
        if (scale == 0)
        {
            Emitter.emitIns_R_R_R(INS_add, attr, targetReg, baseReg, indexReg);
        }
        else
        {
            if (Configuration.TARGET_ARM)
            {
                Emitter.emitIns_R_R_R_I(
                    INS_add, attr, targetReg, baseReg, indexReg, scale, INS_FLAGS_DONT_CARE, INS_OPTS_LSL);
            }
            else
            {
                Emitter.emitIns_R_R_R_I(INS_add, attr, targetReg, baseReg, indexReg, scale, INS_OPTS_LSL);
            }
        }
    }
}
