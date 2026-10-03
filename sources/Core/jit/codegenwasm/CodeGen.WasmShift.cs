// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_WASM
using System.Diagnostics;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForShift(GenTree tree)
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

        var ins = (treeNode.Oper, treeNode.Type) switch
        {
            (GT_LSH, TYP_INT) => INS_i32_shl,
            (GT_LSH, TYP_LONG) => INS_i64_shl,
            (GT_RSH, TYP_INT) => INS_i32_shr_s,
            (GT_RSH, TYP_LONG) => INS_i64_shr_s,
            (GT_RSZ, TYP_INT) => INS_i32_shr_u,
            (GT_RSZ, TYP_LONG) => INS_i64_shr_u,
            (GT_ROL, TYP_INT) => INS_i32_rotl,
            (GT_ROL, TYP_LONG) => INS_i64_rotl,
            (GT_ROR, TYP_INT) => INS_i32_rotr,
            (GT_ROR, TYP_LONG) => INS_i64_rotr,
            _ => throw new UnreachableException(),
        };

        GetEmitter().emitIns(ins);
        WasmProduceReg(treeNode);
    }
}
#endif
