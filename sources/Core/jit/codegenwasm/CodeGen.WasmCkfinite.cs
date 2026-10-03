// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_WASM
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private void genCkfinite(GenTree treeNode)
    {
        assert(treeNode.Oper is GT_CKFINITE);

        var tree = treeNode.AsOp();
        var op1 = tree.Op1;
        var targetType = treeNode.Type;
        assert(varTypeIsFloating(targetType));

        genConsumeOperands(tree);

        // The producer tees this multiply-used operand into a temporary local. Reload it for the check,
        // leaving the original value on the Wasm stack to become the result of GT_CKFINITE.
        var op1Reg = GetMultiUseOperandReg(op1);
        var emit = GetEmitter();

        // !(|x| < +Inf) is true for NaN and either infinity, and false for every finite value.
        // Wasm comparisons involving NaN return false. The f32 constant emitter consumes the
        // double-width +Inf bit pattern and truncates it to float.
        const long infBits = 0x7FF0000000000000L;
        if (targetType is TYP_FLOAT)
        {
            emit.emitIns_I(
                INS_local_get, EA_4BYTE, unchecked((nint)regNumberExtensions.WasmRegToIndex(op1Reg)));
            emit.emitIns(INS_f32_abs);
            emit.emitIns_I(INS_f32_const, EA_4BYTE, unchecked((nint)infBits));
            emit.emitIns(INS_f32_lt);
        }
        else
        {
            assert(targetType is TYP_DOUBLE);
            emit.emitIns_I(
                INS_local_get, EA_8BYTE, unchecked((nint)regNumberExtensions.WasmRegToIndex(op1Reg)));
            emit.emitIns(INS_f64_abs);
            emit.emitIns_I(INS_f64_const, EA_8BYTE, unchecked((nint)infBits));
            emit.emitIns(INS_f64_lt);
        }

        emit.emitIns(INS_i32_eqz);

        // Throw on a non-finite value; the original operand remains on the stack on fall-through.
        genJumpToThrowHlpBlk(SCK_ARITH_EXCPN);

        WasmProduceReg(treeNode);
    }
}
#endif
