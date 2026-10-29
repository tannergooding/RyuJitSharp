// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForShiftWasm(GenTree tree)
    {
        assert(tree.Oper.IsShiftOrRotate);

        var treeNode = tree.AsOp();
        genConsumeOperands(treeNode);

        // The IR shift amount is always int, but Wasm requires its width to match the shiftee's width.
        // Constant amounts are extended too; containing them could avoid the conversion.
        if (treeNode.Type is TYP_LONG)
        {
            assert(genActualType(treeNode.Op2.Type) is TYP_INT);
            GetEmitter().emitIns(INS_i64_extend_u_i32);
        }

        instruction ins;
        switch (treeNode.Oper, treeNode.Type)
        {
            case (GT_LSH, TYP_INT):
                ins = INS_i32_shl;
                break;
            case (GT_LSH, TYP_LONG):
                ins = INS_i64_shl;
                break;
            case (GT_RSH, TYP_INT):
                ins = INS_i32_shr_s;
                break;
            case (GT_RSH, TYP_LONG):
                ins = INS_i64_shr_s;
                break;
            case (GT_RSZ, TYP_INT):
                ins = INS_i32_shr_u;
                break;
            case (GT_RSZ, TYP_LONG):
                ins = INS_i64_shr_u;
                break;
            case (GT_ROL, TYP_INT):
                ins = INS_i32_rotl;
                break;
            case (GT_ROL, TYP_LONG):
                ins = INS_i64_rotl;
                break;
            case (GT_ROR, TYP_INT):
                ins = INS_i32_rotr;
                break;
            case (GT_ROR, TYP_LONG):
                ins = INS_i64_rotr;
                break;
            default:
                JitExceptionHelpers.ThrowUnreachableException();
                ins = default;
                break;
        }

        GetEmitter().emitIns(ins);
        WasmProduceReg(treeNode);
    }
}
