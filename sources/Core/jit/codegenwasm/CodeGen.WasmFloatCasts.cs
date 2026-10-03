// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_WASM
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genFloatToIntCast(GenTreeCast tree)
    {
        assert(!tree.HasOverflowCheck);

        var toType = tree.Type;
        var fromType = tree.CastOp.Type;
        var isUnsigned = varTypeIsUnsigned(tree.CastType);
        var ins = INS_none;
        assert(varTypeIsFloating(fromType) && ((toType == TYP_INT) || (toType == TYP_LONG)));

        genConsumeOperands(tree);

        switch ((fromType, toType))
        {
            case (TYP_FLOAT, TYP_INT):
            {
                ins = isUnsigned ? INS_i32_trunc_sat_f32_u : INS_i32_trunc_sat_f32_s;
                break;
            }

            case (TYP_DOUBLE, TYP_INT):
            {
                ins = isUnsigned ? INS_i32_trunc_sat_f64_u : INS_i32_trunc_sat_f64_s;
                break;
            }

            case (TYP_FLOAT, TYP_LONG):
            {
                ins = isUnsigned ? INS_i64_trunc_sat_f32_u : INS_i64_trunc_sat_f32_s;
                break;
            }

            case (TYP_DOUBLE, TYP_LONG):
            {
                ins = isUnsigned ? INS_i64_trunc_sat_f64_u : INS_i64_trunc_sat_f64_s;
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        // Wasm trunc_sat instructions clamp out-of-range values; overflow checks are handled during import.
        GetEmitter().emitIns(ins);
        WasmProduceReg(tree);
    }
}
#endif
