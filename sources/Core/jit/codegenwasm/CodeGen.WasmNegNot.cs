// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_WASM
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private void genCodeForNegNot(GenTreeUnOp tree)
    {
        assert(tree.Oper is GT_NEG or GT_NOT);
        genConsumeOperands(tree);

        var ins = INS_none;
        switch ((tree.Oper, tree.Type))
        {
            case (GT_NOT, TYP_INT):
            {
                GetEmitter().emitIns_I(INS_i32_const, tree.Type.EmitSize, -1);
                ins = INS_i32_xor;
                break;
            }

            case (GT_NOT, TYP_LONG):
            {
                GetEmitter().emitIns_I(INS_i64_const, tree.Type.EmitSize, -1);
                ins = INS_i64_xor;
                break;
            }

            case (GT_NOT, TYP_FLOAT):
            case (GT_NOT, TYP_DOUBLE):
            {
                unreached();
                break;
            }

            case (GT_NEG, TYP_INT):
            case (GT_NEG, TYP_LONG):
            {
                // Integer negation is lowered to SUB because the value is already on the stack.
                unreached();
                break;
            }

            case (GT_NEG, TYP_FLOAT):
            {
                ins = INS_f32_neg;
                break;
            }

            case (GT_NEG, TYP_DOUBLE):
            {
                ins = INS_f64_neg;
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        GetEmitter().emitIns(ins);
        WasmProduceReg(tree);
    }
}
#endif
