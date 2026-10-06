// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM || TARGET_ARM64
using System.Numerics;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.insFlags;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;
using static RyuJitSharp.genTreeOps;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genLeaInstruction(GenTreeAddrMode lea)
    {
        genConsumeOperands(lea);
        var size = lea.Type.EmitSize;
        var offset = lea.Offset;

        // ARM address modes can require multiple instructions to form a GT_LEA result.
        // TODO-ARM64-CQ: Lowering should make GT_LEA correspond to one ARM64 address-forming instruction.
        if (lea.HasBaseAddress && lea.HasIndex)
        {
            var baseAddress = lea.BaseAddress;
            var index = lea.Index;

            assert(uint.IsPow2(lea.Scale));
            var scale = BitOperations.TrailingZeroCount((uint)lea.Scale);
            assert(scale <= 4);

            if (offset != 0)
            {
                var tempReg = _internalRegisters.GetSingle(lea);
                var useLargeOffsetSequence = Interruptible && (size == EA_BYREF);

                // A fully interruptible GC byref cannot temporarily point outside its object.
                if (!useLargeOffsetSequence && Emitter.emitIns_valid_imm_for_add(offset))
                {
                    genScaledAdd(size, tempReg, baseAddress.RegNum, index.RegNum, scale);
#if TARGET_ARM
                    Emitter.emitIns_R_R_I(
                        INS_add, size, lea.RegNum, tempReg, offset, INS_FLAGS_DONT_CARE, INS_OPTS_NONE);
#else
                    Emitter.emitIns_R_R_I(INS_add, size, lea.RegNum, tempReg, offset, INS_OPTS_NONE);
#endif
                }
                else
                {
                    noway_assert(tempReg != index.RegNum);
                    noway_assert(tempReg != baseAddress.RegNum);

                    instGen_Set_Reg_To_Imm(EA_PTRSIZE, tempReg, offset);
                    genScaledAdd(EA_PTRSIZE, tempReg, tempReg, index.RegNum, scale);
                    Emitter.emitIns_R_R_R(INS_add, size, lea.RegNum, baseAddress.RegNum, tempReg);
                }
            }
            else
            {
#if TARGET_ARM64
                if (index.IsContained)
                {
                    if (index.Oper is GT_BFIZ)
                    {
                        assert(scale == 0);
                        scale = (int)index.AsOp().Op2.AsIntConCommon().IconValue;
                        index = index.AsOp().Op1.AsCast().Op1;
                    }
                    else if (index.Oper is GT_CAST)
                    {
                        index = index.AsCast().Op1;
                    }
                    else
                    {
                        unreached();
                    }
                }
#endif

                genScaledAdd(size, lea.RegNum, baseAddress.RegNum, index.RegNum, scale);
            }
        }
        else if (lea.HasBaseAddress)
        {
            var baseAddress = lea.BaseAddress;

            if (Emitter.emitIns_valid_imm_for_add(offset))
            {
                if (offset != 0)
                {
#if TARGET_ARM
                    Emitter.emitIns_R_R_I(
                        INS_add, size, lea.RegNum, baseAddress.RegNum, offset, INS_FLAGS_DONT_CARE, INS_OPTS_NONE);
#else
                    Emitter.emitIns_R_R_I(
                        INS_add, size, lea.RegNum, baseAddress.RegNum, offset, INS_OPTS_NONE);
#endif
                }
                else
                {
                    Emitter.emitIns_Mov(INS_mov, size, lea.RegNum, baseAddress.RegNum, canSkip: true);
                }
            }
            else
            {
                var tempReg = _internalRegisters.GetSingle(lea);
                instGen_Set_Reg_To_Imm(EA_PTRSIZE, tempReg, offset);
                Emitter.emitIns_R_R_R(INS_add, size, lea.RegNum, baseAddress.RegNum, tempReg);
            }
        }
        else if (lea.HasIndex)
        {
            // Keep baseless address-tree optimization disabled until lowering accounts for ARM64 addressing limits.
            assert(false, "We shouldn't see a baseless address computation during CodeGen for ARM64");
        }

        genProduceReg(lea);
    }
}
#endif
