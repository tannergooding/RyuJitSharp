// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_WASM
using static RyuJitSharp.CodeGen.GenIntCastDesc.CheckKind;
using static RyuJitSharp.CodeGen.GenIntCastDesc.ExtendKind;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genIntToIntCast(GenTreeCast cast)
    {
        var desc = new GenIntCastDesc(cast);
        if (desc.Check != CHECK_NONE)
        {
            var castValue = cast.CastOp;
            var castReg = GetMultiUseOperandReg(castValue);
            genIntCastOverflowCheck(cast, in desc, castReg);
        }

        var toType = genActualType(cast.CastType);
        var fromType = genActualType(cast.CastOp);
        var extendSize = desc.ExtendSrcSize;
        var ins = INS_none;
        assert((fromType == TYP_INT) || (fromType == TYP_LONG));

        genConsumeOperands(cast);

        // Load containment is not yet represented by GenIntCastDesc for Wasm.
        switch (desc.Extend)
        {
            case COPY:
            {
                if ((toType == TYP_INT) && (fromType == TYP_LONG))
                {
                    ins = INS_i32_wrap_i64;
                }
                else
                {
                    assert(toType == fromType);
                }
                break;
            }

            case ZERO_EXTEND_SMALL_INT:
            {
                var andAmount = extendSize == 1u ? 255 : 65535;
                if (fromType == TYP_LONG)
                {
                    GetEmitter().emitIns(INS_i32_wrap_i64);
                }

                GetEmitter().emitIns_I(INS_i32_const, EA_4BYTE, andAmount);
                ins = INS_i32_and;
                break;
            }

            case SIGN_EXTEND_SMALL_INT:
            {
                if (fromType == TYP_LONG)
                {
                    GetEmitter().emitIns(INS_i32_wrap_i64);
                }

                ins = extendSize == 1u ? INS_i32_extend8_s : INS_i32_extend16_s;
                break;
            }

            case ZERO_EXTEND_INT:
            {
                ins = INS_i64_extend_u_i32;
                break;
            }

            case SIGN_EXTEND_INT:
            {
                ins = INS_i64_extend_s_i32;
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        if (ins != INS_none)
        {
            GetEmitter().emitIns(ins);
        }

        WasmProduceReg(cast);
    }

    public void genIntCastOverflowCheck(GenTreeCast cast, in GenIntCastDesc desc, regNumber reg)
    {
        var is64BitSrc = desc.CheckSrcSize == 8u;
        var srcSize = is64BitSrc ? EA_8BYTE : EA_4BYTE;

        genEmitLocalGet(reg, is64BitSrc ? WasmValueType.I64 : WasmValueType.I32);

        switch (desc.Check)
        {
            case CHECK_POSITIVE:
            {
                GetEmitter().emitIns_I(is64BitSrc ? INS_i64_const : INS_i32_const, srcSize, 0);
                GetEmitter().emitIns(is64BitSrc ? INS_i64_lt_s : INS_i32_lt_s);
                genJumpToThrowHlpBlk(SCK_OVERFLOW);
                break;
            }

            case CHECK_UINT_RANGE:
            {
                assert(is64BitSrc);
                GetEmitter().emitIns_I(INS_i64_const, srcSize, unchecked((nint)uint.MaxValue));
                GetEmitter().emitIns(INS_i64_gt_u);
                genJumpToThrowHlpBlk(SCK_OVERFLOW);
                break;
            }

            case CHECK_POSITIVE_INT_RANGE:
            {
                GetEmitter().emitIns_I(INS_i64_const, srcSize, int.MaxValue);
                GetEmitter().emitIns(INS_i64_gt_u);
                genJumpToThrowHlpBlk(SCK_OVERFLOW);
                break;
            }

            case CHECK_INT_RANGE:
            {
                GetEmitter().emitIns(INS_i64_extend32_s);
                genEmitLocalGet(reg, is64BitSrc ? WasmValueType.I64 : WasmValueType.I32);
                GetEmitter().emitIns(INS_i64_ne);
                genJumpToThrowHlpBlk(SCK_OVERFLOW);
                break;
            }

            case CHECK_SMALL_INT_RANGE:
            {
                var castMaxValue = desc.CheckSmallIntMax;
                var castMinValue = desc.CheckSmallIntMin;

                if (castMinValue == 0)
                {
                    GetEmitter().emitIns_I(is64BitSrc ? INS_i64_const : INS_i32_const, srcSize, castMaxValue);
                    GetEmitter().emitIns(is64BitSrc ? INS_i64_gt_u : INS_i32_gt_u);
                }
                else
                {
                    assert(!cast.IsUnsigned);
                    GetEmitter().emitIns_I(is64BitSrc ? INS_i64_const : INS_i32_const, srcSize, castMaxValue);
                    GetEmitter().emitIns(is64BitSrc ? INS_i64_gt_s : INS_i32_gt_s);
                    genEmitLocalGet(reg, is64BitSrc ? WasmValueType.I64 : WasmValueType.I32);
                    GetEmitter().emitIns_I(is64BitSrc ? INS_i64_const : INS_i32_const, srcSize, castMinValue);
                    GetEmitter().emitIns(is64BitSrc ? INS_i64_lt_s : INS_i32_lt_s);
                    GetEmitter().emitIns(INS_i32_or);
                }

                genJumpToThrowHlpBlk(SCK_OVERFLOW);
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
    }

    public void genJumpToThrowHlpBlk(SpecialCodeKind codeKind)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Wasm throw-helper block generation is not ported.");
    }
}
#endif
