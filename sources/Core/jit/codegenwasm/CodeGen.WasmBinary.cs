// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_WASM
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForBinary(GenTreeOp treeNode)
    {
        if (treeNode.HasOverflowCheckEx)
        {
            genCodeForBinaryOverflow(treeNode);
            return;
        }

        genConsumeOperands(treeNode);

        var ins = INS_none;
        switch ((treeNode.Oper, treeNode.Type))
        {
            case (GT_ADD, TYP_INT):
            {
                ins = INS_i32_add;
                break;
            }

            case (GT_ADD, TYP_LONG):
            {
                ins = INS_i64_add;
                break;
            }

            case (GT_ADD, TYP_FLOAT):
            {
                ins = INS_f32_add;
                break;
            }

            case (GT_ADD, TYP_DOUBLE):
            {
                ins = INS_f64_add;
                break;
            }

            case (GT_SUB, TYP_INT):
            {
                ins = INS_i32_sub;
                break;
            }

            case (GT_SUB, TYP_LONG):
            {
                ins = INS_i64_sub;
                break;
            }

            case (GT_SUB, TYP_FLOAT):
            {
                ins = INS_f32_sub;
                break;
            }

            case (GT_SUB, TYP_DOUBLE):
            {
                ins = INS_f64_sub;
                break;
            }

            case (GT_MUL, TYP_INT):
            {
                ins = INS_i32_mul;
                break;
            }

            case (GT_MUL, TYP_LONG):
            {
                ins = INS_i64_mul;
                break;
            }

            case (GT_MUL, TYP_FLOAT):
            {
                ins = INS_f32_mul;
                break;
            }

            case (GT_MUL, TYP_DOUBLE):
            {
                ins = INS_f64_mul;
                break;
            }

            case (GT_AND, TYP_INT):
            {
                ins = INS_i32_and;
                break;
            }

            case (GT_AND, TYP_LONG):
            {
                ins = INS_i64_and;
                break;
            }

            case (GT_OR, TYP_INT):
            {
                ins = INS_i32_or;
                break;
            }

            case (GT_OR, TYP_LONG):
            {
                ins = INS_i64_or;
                break;
            }

            case (GT_XOR, TYP_INT):
            {
                ins = INS_i32_xor;
                break;
            }

            case (GT_XOR, TYP_LONG):
            {
                ins = INS_i64_xor;
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        GetEmitter().emitIns(ins);
        WasmProduceReg(treeNode);
    }

    public void genCodeForBinaryOverflow(GenTreeOp treeNode)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Wasm overflow-checked binary code generation is not ported.");
    }
}
#endif
