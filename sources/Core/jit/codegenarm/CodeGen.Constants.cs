// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using System;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genSetRegToConst(regNumber targetReg, var_types targetType, GenTree tree)
    {
        switch (tree.Oper)
        {
            case GT_CNS_INT:
            {
                var constant = tree.AsIntCon();
                var value = constant.IconValue;
                var attr = targetType.EmitActualSize;
                if (constant.ImmedValNeedsReloc(_compiler))
                {
                    attr |= EA_CNS_RELOC_FLG;
                }

                if (targetType == TYP_BYREF)
                {
                    attr |= EA_BYREF_FLG;
                }

                instGen_Set_Reg_To_Imm(attr, targetReg, value);
                _regSet.verifyRegUsed(targetReg);
                break;
            }

            case GT_CNS_DBL:
            {
                var constant = tree.AsDblCon().DconVal;
                if (targetType == TYP_FLOAT)
                {
                    var tempReg = _internalRegisters.GetSingle(tree);
                    var value = BitConverter.SingleToInt32Bits(forceCastToFloat(constant));
                    instGen_Set_Reg_To_Imm(EA_4BYTE, tempReg, value);
                    _ = Emitter.emitIns_Mov(INS_vmov_i2f, EA_4BYTE, targetReg, tempReg, canSkip: false);
                }
                else
                {
                    assert(targetType == TYP_DOUBLE);
                    var tempReg1 = _internalRegisters.Extract(tree);
                    var tempReg2 = _internalRegisters.GetSingle(tree);
                    var bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(constant));
                    instGen_Set_Reg_To_Imm(EA_4BYTE, tempReg1, unchecked((int)(uint)bits));
                    instGen_Set_Reg_To_Imm(EA_4BYTE, tempReg2, unchecked((int)(uint)(bits >> 32)));
                    Emitter.emitIns_R_R_R(INS_vmov_i2d, EA_8BYTE, targetReg, tempReg1, tempReg2);
                }
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
    }
}
#endif
