// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
using System.Numerics;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForIndexAddr(GenTreeIndexAddr tree)
    {
        var baseAddress = tree.Arr;
        var index = tree.Index;

        _ = genConsumeReg(baseAddress);
        _ = genConsumeReg(index);

        // INDEX_ADDR uses the base through the final instruction, so restore its GC state after consumption.
        _gcInfo.gcMarkRegPtrVal(baseAddress.RegNum, baseAddress.Type);
        assert(!varTypeIsGC(index.Type));
        assert(index.IsUsedFromReg);

        var tempReg = InternalRegisters.GetSingle(tree);

        if (tree.IsBoundsChecked)
        {
            Emitter.emitIns_R_R_I(INS_lw, EA_4BYTE, tempReg, baseAddress.RegNum, tree.LenOffset);
            genJumpToThrowHlpBlk_la(SCK_RNGCHK_FAIL, INS_bgeu, index.RegNum, null, tempReg);
        }

        var attr = tree.Type.EmitActualSize;
        var elementSize = unchecked((uint)tree.ElemSize);
        if (uint.IsPow2(elementSize))
        {
            var scale = BitOperations.TrailingZeroCount(elementSize);
            if (elementSize <= 64)
            {
                var shxaddIns = getShxaddVariant(scale, index.Type.EmitSize == EA_4BYTE);
                if (_compiler.compOpportunisticallyDependsOn(InstructionSet_Zba) && (shxaddIns != INS_none))
                {
                    Emitter.emitIns_R_R_R(
                        shxaddIns, attr, tree.RegNum, index.RegNum, baseAddress.RegNum);
                }
                else
                {
                    genScaledAdd(attr, tree.RegNum, baseAddress.RegNum, index.RegNum, scale, tempReg);
                }
            }
            else
            {
                _ = Emitter.emitLoadImmediate(true, EA_PTRSIZE, tempReg, scale);
                var shiftIns = attr == EA_4BYTE ? INS_sllw : INS_sll;
                var addIns = attr == EA_4BYTE ? INS_addw : INS_add;
                Emitter.emitIns_R_R_R(shiftIns, attr, tempReg, index.RegNum, tempReg);
                Emitter.emitIns_R_R_R(addIns, attr, tree.RegNum, tempReg, baseAddress.RegNum);
            }
        }
        else
        {
            instGen_Set_Reg_To_Imm(EA_4BYTE, tempReg, unchecked((nint)elementSize));

            var multiplyIns = attr == EA_4BYTE ? INS_mulw : INS_mul;
            var addIns = attr == EA_4BYTE ? INS_addw : INS_add;
            Emitter.emitIns_R_R_R(multiplyIns, EA_PTRSIZE, tempReg, index.RegNum, tempReg);
            Emitter.emitIns_R_R_R(addIns, attr, tree.RegNum, tempReg, baseAddress.RegNum);
        }

        Emitter.emitIns_R_R_I(INS_addi, attr, tree.RegNum, tree.RegNum, tree.ElemOffset);

        _gcInfo.gcMarkRegSetNpt(baseAddress.RegMask);
        genProduceReg(tree);
    }
}
#endif
