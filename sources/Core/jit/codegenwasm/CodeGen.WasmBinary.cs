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
        assert(treeNode.HasOverflowCheck);
        assert(varTypeIsIntegral(treeNode.Type));

        // TODO-WASM-CQ: consider using helper calls for all these cases.
        genConsumeOperands(treeNode);

        var is64BitOp = treeNode.Type is TYP_LONG;
        var op1Reg = GetMultiUseOperandReg(treeNode.Op1);
        var op2Reg = GetMultiUseOperandReg(treeNode.Op2);

        switch (treeNode.Oper)
        {
            case GT_ADD:
            {
                assert(GetWasmInternalRegisterCount(treeNode) == 1u);
                var resultReg = ExtractWasmInternalRegister(treeNode);
                assert(regNumberExtensions.WasmRegToType(resultReg) ==
                    regNumberExtensions.TypeToWasmValueType(treeNode.Type));

                GetEmitter().emitIns(is64BitOp ? INS_i64_add : INS_i32_add);

                if (treeNode.IsUnsigned)
                {
                    // Unsigned addition overflows when the result is less than op1.
                    GetEmitter().emitIns_I(
                        INS_local_tee, treeNode.Type.EmitActualSize,
                        unchecked((nint)regNumberExtensions.WasmRegToIndex(resultReg)));
                    genEmitLocalGet(op1Reg, treeNode.Type);
                    GetEmitter().emitIns(is64BitOp ? INS_i64_lt_u : INS_i32_lt_u);
                    genJumpToThrowHlpBlk(SCK_OVERFLOW);
                }
                else
                {
                    GetEmitter().emitIns_I(
                        INS_local_set, treeNode.Type.EmitActualSize,
                        unchecked((nint)regNumberExtensions.WasmRegToIndex(resultReg)));
                    // Equal operand signs produce a non-negative XOR; only these cases can overflow.
                    genEmitLocalGet(op1Reg, treeNode.Type);
                    genEmitLocalGet(op2Reg, treeNode.Type);
                    GetEmitter().emitIns(is64BitOp ? INS_i64_xor : INS_i32_xor);

                    // Overflow occurs when equal-sign operands produce a result with the opposite sign.
                    GetEmitter().emitIns_I(
                        is64BitOp ? INS_i64_const : INS_i32_const,
                        treeNode.Type.EmitActualSize, 0);
                    GetEmitter().emitIns(is64BitOp ? INS_i64_ge_s : INS_i32_ge_s);
                    // TODO-WASM-CQ: consider a branchless alternative here (and for subtraction).
                    genEmitIf();
                    {
                        GetEmitter().emitIns_I(
                            INS_local_get, treeNode.Type.EmitActualSize,
                            unchecked((nint)regNumberExtensions.WasmRegToIndex(resultReg)));
                        genEmitLocalGet(op1Reg, treeNode.Type);
                        GetEmitter().emitIns(is64BitOp ? INS_i64_xor : INS_i32_xor);
                        GetEmitter().emitIns_I(
                            is64BitOp ? INS_i64_const : INS_i32_const,
                            treeNode.Type.EmitActualSize, 0);
                        GetEmitter().emitIns(is64BitOp ? INS_i64_lt_s : INS_i32_lt_s);
                        genJumpToThrowHlpBlk(SCK_OVERFLOW);
                    }
                    genEmitEndIf();
                }

                GetEmitter().emitIns_I(
                    INS_local_get, treeNode.Type.EmitActualSize,
                    unchecked((nint)regNumberExtensions.WasmRegToIndex(resultReg)));
                break;
            }

            case GT_SUB:
            {
                assert(GetWasmInternalRegisterCount(treeNode) == 1u);
                var resultReg = ExtractWasmInternalRegister(treeNode);
                assert(regNumberExtensions.WasmRegToType(resultReg) ==
                    regNumberExtensions.TypeToWasmValueType(treeNode.Type));

                GetEmitter().emitIns(is64BitOp ? INS_i64_sub : INS_i32_sub);
                GetEmitter().emitIns_I(
                    INS_local_set, treeNode.Type.EmitActualSize,
                    unchecked((nint)regNumberExtensions.WasmRegToIndex(resultReg)));

                if (treeNode.IsUnsigned)
                {
                    // Unsigned subtraction overflows when op1 is less than op2.
                    genEmitLocalGet(op1Reg, treeNode.Type);
                    genEmitLocalGet(op2Reg, treeNode.Type);
                    GetEmitter().emitIns(is64BitOp ? INS_i64_lt_u : INS_i32_lt_u);
                    genJumpToThrowHlpBlk(SCK_OVERFLOW);
                }
                else
                {
                    // Different-sign operands can overflow if the difference's sign differs from op1.
                    genEmitLocalGet(op1Reg, treeNode.Type);
                    genEmitLocalGet(op2Reg, treeNode.Type);
                    GetEmitter().emitIns(is64BitOp ? INS_i64_xor : INS_i32_xor);
                    GetEmitter().emitIns_I(
                        is64BitOp ? INS_i64_const : INS_i32_const,
                        treeNode.Type.EmitActualSize, 0);
                    GetEmitter().emitIns(is64BitOp ? INS_i64_lt_s : INS_i32_lt_s);
                    // Overflow occurs when differing-sign operands produce a result with op1's opposite sign.
                    genEmitIf();
                    {
                        GetEmitter().emitIns_I(
                            INS_local_get, treeNode.Type.EmitActualSize,
                            unchecked((nint)regNumberExtensions.WasmRegToIndex(resultReg)));
                        genEmitLocalGet(op1Reg, treeNode.Type);
                        GetEmitter().emitIns(is64BitOp ? INS_i64_xor : INS_i32_xor);
                        GetEmitter().emitIns_I(
                            is64BitOp ? INS_i64_const : INS_i32_const,
                            treeNode.Type.EmitActualSize, 0);
                        GetEmitter().emitIns(is64BitOp ? INS_i64_lt_s : INS_i32_lt_s);
                        genJumpToThrowHlpBlk(SCK_OVERFLOW);
                    }
                    genEmitEndIf();
                }

                GetEmitter().emitIns_I(
                    INS_local_get, treeNode.Type.EmitActualSize,
                    unchecked((nint)regNumberExtensions.WasmRegToIndex(resultReg)));
                break;
            }

            case GT_MUL:
            {
                assert(!is64BitOp,
                    conditionExpression: "64-bit multiply with overflow should have been transformed into a helper call by morph");

                assert(GetWasmInternalRegisterCount(treeNode) == 1u);
                var wideReg = ExtractWasmInternalRegister(treeNode);
                assert(regNumberExtensions.WasmRegToType(wideReg) is WasmValueType.I64);

                var isUnsigned = treeNode.IsUnsigned;
                // Operands are I32 values on the stack; widen both before multiplying to retain overflow bits.
                GetEmitter().emitIns(INS_drop);
                GetEmitter().emitIns(isUnsigned ? INS_i64_extend_u_i32 : INS_i64_extend_s_i32);
                genEmitLocalGet(op2Reg, treeNode.Type);
                GetEmitter().emitIns(isUnsigned ? INS_i64_extend_u_i32 : INS_i64_extend_s_i32);
                GetEmitter().emitIns(INS_i64_mul);

                // Keep the full product for range checking, then return its low I32 when it fits.
                GetEmitter().emitIns_I(
                    INS_local_tee, EA_8BYTE,
                    unchecked((nint)regNumberExtensions.WasmRegToIndex(wideReg)));

                if (isUnsigned)
                {
                    // Unsigned multiplication overflows when the product exceeds UINT32_MAX.
                    GetEmitter().emitIns_I(INS_i64_const, EA_8BYTE, unchecked((nint)uint.MaxValue));
                    GetEmitter().emitIns(INS_i64_gt_u);
                    genJumpToThrowHlpBlk(SCK_OVERFLOW);
                }
                else
                {
                    // Signed multiplication overflows when sign-extending the low I32 changes the product.
                    GetEmitter().emitIns(INS_i64_extend32_s);
                    GetEmitter().emitIns_I(
                        INS_local_get, EA_8BYTE,
                        unchecked((nint)regNumberExtensions.WasmRegToIndex(wideReg)));
                    GetEmitter().emitIns(INS_i64_ne);
                    genJumpToThrowHlpBlk(SCK_OVERFLOW);
                }

                GetEmitter().emitIns_I(
                    INS_local_get, EA_8BYTE,
                    unchecked((nint)regNumberExtensions.WasmRegToIndex(wideReg)));
                GetEmitter().emitIns(INS_i32_wrap_i64);
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        WasmProduceReg(treeNode);
    }

    private uint GetWasmInternalRegisterCount(GenTree tree)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Wasm internal-register tracking is not ported.");
    }

    private regNumber ExtractWasmInternalRegister(GenTree tree)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Wasm internal-register extraction is not ported.");
    }
}
#endif
