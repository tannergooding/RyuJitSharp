// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_WASM
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForDivMod(GenTreeOp treeNode)
    {
        genConsumeOperands(treeNode);

        if (!varTypeIsFloating(treeNode.Type))
        {
            var exceptions = treeNode.Exceptions(_compiler);
            var is64BitOp = treeNode.Type is TYP_LONG;
            var size = is64BitOp ? EA_8BYTE : EA_4BYTE;
            var divisor = treeNode.Op2;
            var divisorReg = REG_NA;

            // AnyVal / 0 throws DivideByZeroException.
            if ((exceptions & ExceptionSetFlags.DivideByZeroException) is not ExceptionSetFlags.None)
            {
                divisorReg = GetMultiUseOperandReg(divisor);
                genEmitLocalGet(divisorReg, treeNode.Type);
                GetEmitter().emitIns(is64BitOp ? INS_i64_eqz : INS_i32_eqz);
                genJumpToThrowHlpBlk(SCK_DIV_BY_ZERO);
            }

            // MinInt / -1 throws ArithmeticException.
            if ((exceptions & ExceptionSetFlags.ArithmeticException) is not ExceptionSetFlags.None)
            {
                if (divisorReg is REG_NA)
                {
                    divisorReg = GetMultiUseOperandReg(divisor);
                }

                genEmitLocalGet(divisorReg, treeNode.Type);
                GetEmitter().emitIns_I(is64BitOp ? INS_i64_const : INS_i32_const, size, -1);
                GetEmitter().emitIns(is64BitOp ? INS_i64_eq : INS_i32_eq);

                var dividendReg = GetMultiUseOperandReg(treeNode.Op1);
                genEmitLocalGet(dividendReg, treeNode.Type);
                var minValue = is64BitOp ? long.MinValue : int.MinValue;
                GetEmitter().emitIns_I(
                    is64BitOp ? INS_i64_const : INS_i32_const,
                    size, unchecked((nint)minValue));
                GetEmitter().emitIns(is64BitOp ? INS_i64_eq : INS_i32_eq);

                // Wasm relational operations always produce i32 results.
                GetEmitter().emitIns(INS_i32_and);
                genJumpToThrowHlpBlk(SCK_ARITH_EXCPN);
            }
        }

        var ins = INS_none;
        switch ((treeNode.Oper, treeNode.Type))
        {
            case (GT_DIV, TYP_INT):
            {
                ins = INS_i32_div_s;
                break;
            }

            case (GT_DIV, TYP_LONG):
            {
                ins = INS_i64_div_s;
                break;
            }

            case (GT_DIV, TYP_FLOAT):
            {
                ins = INS_f32_div;
                break;
            }

            case (GT_DIV, TYP_DOUBLE):
            {
                ins = INS_f64_div;
                break;
            }

            case (GT_UDIV, TYP_INT):
            {
                ins = INS_i32_div_u;
                break;
            }

            case (GT_UDIV, TYP_LONG):
            {
                ins = INS_i64_div_u;
                break;
            }

            case (GT_MOD, TYP_INT):
            {
                ins = INS_i32_rem_s;
                break;
            }

            case (GT_MOD, TYP_LONG):
            {
                ins = INS_i64_rem_s;
                break;
            }

            case (GT_UMOD, TYP_INT):
            {
                ins = INS_i32_rem_u;
                break;
            }

            case (GT_UMOD, TYP_LONG):
            {
                ins = INS_i64_rem_u;
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
}
#endif
