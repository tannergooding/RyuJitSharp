// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

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

                // TODO-RISCV64-CQ: Currently we cannot do this for all handles because of
                // https://github.com/dotnet/runtime/issues/60712
                if (constant.ImmedValNeedsReloc(_compiler))
                {
                    attr = EA_SET_FLG(attr, EA_CNS_RELOC_FLG);
                }

                if (targetType is TYP_BYREF)
                {
                    attr = EA_SET_FLG(attr, EA_BYREF_FLG);
                }

                instGen_Set_Reg_To_Imm(attr, targetReg, value, INS_FLAGS_DONT_CARE
#if DEBUG
                    , unchecked((nuint)constant.TargetHandle), constant.Flags
#endif
                    );
                _regSet.verifyRegUsed(targetReg);
                break;
            }

            case GT_CNS_DBL:
            {
                var emit = Emitter;
                var size = tree.Type.EmitActualSize;
                var value = tree.AsDblCon().DconVal;

                assert(Emitter.isFloatReg(targetReg));

                if (Emitter.isSingleInstructionFpImm(value, size, out var bits))
                {
                    var tempReg = REG_ZERO;
                    if (bits != 0)
                    {
                        tempReg = InternalRegisters.GetSingle(tree);
                        if (Emitter.isValidSimm12(unchecked((nint)bits)))
                        {
                            emit.emitIns_R_R_I(INS_addi, size, tempReg, REG_ZERO, unchecked((nint)bits));
                        }
                        else
                        {
                            var upperBits = bits >> 12;
                            assert((upperBits << 12) == bits);
                            emit.emitIns_R_I(INS_lui, size, tempReg, unchecked((nint)upperBits));
                        }
                    }

                    var ins = size is EA_4BYTE ? INS_fmv_w_x : INS_fmv_d_x;
                    emit.emitIns_R_R(ins, size, targetReg, tempReg);
                    break;
                }

                var handle = emit.emitFltOrDblConst(value, size);
                var loadIns = size is EA_4BYTE ? INS_flw : INS_fld;
                emit.emitIns_R_C(loadIns, size, targetReg, REG_NA, handle);
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
