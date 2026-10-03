// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_WASM
using System;
using System.Diagnostics;
#if FEATURE_SIMD
using System.Runtime.InteropServices;
#endif

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForConstant(GenTree treeNode)
    {
        instruction ins;
        long bits = 0;
        var type = treeNode.Type is TYP_REF or TYP_BYREF ? TYP_I_IMPL : treeNode.Type;

        if (type is TYP_INT or TYP_LONG)
        {
            var icon = treeNode.AsIntConCommon();
            if (icon.IsIconHandle())
            {
                // Wasm has no absolute-address literals; handles are materialized as relocated module-base-relative
                // constants. Real AOT compilation always enables relocation; only cross-VM SuperPMI replay may not.
                assert(icon.ImmedValNeedsReloc(_compiler) || _compiler.RunningSuperPmiReplay);
                GetEmitter().emitAddressConstant(unchecked((nint)icon.IntegralValue));
                WasmProduceReg(treeNode);
                return;
            }

            bits = icon.IntegralValue;
        }

        switch (type)
        {
            case TYP_INT:
            {
                ins = INS_i32_const;
                assert(Globals.FitsIn(TYP_INT, bits) || Globals.FitsIn(TYP_UINT, bits));
                // Wasm i32.const accepts any 32-bit pattern. Truncate through uint before using the signed value
                // for canonical SLEB128 encoding.
                bits = unchecked((int)(uint)bits);
                break;
            }

            case TYP_LONG:
            {
                ins = INS_i64_const;
                break;
            }

            case TYP_FLOAT:
            {
                ins = INS_f32_const;
                var value = treeNode.AsDblCon().DconVal;
                bits = BitConverter.DoubleToInt64Bits(value);
                break;
            }

            case TYP_DOUBLE:
            {
                ins = INS_f64_const;
                var value = treeNode.AsDblCon().DconVal;
                bits = BitConverter.DoubleToInt64Bits(value);
                break;
            }

            default:
            {
                throw new UnreachableException();
            }
        }

        // The instruction format determines how the immediate bits are emitted.
        GetEmitter().emitIns_I(ins, treeNode.Type.EmitSize, unchecked((nint)bits));
        WasmProduceReg(treeNode);
    }

#if FEATURE_SIMD
    public void genCodeForVectorConstant(GenTree treeNode)
    {
        assert(treeNode.Oper is GT_CNS_VEC);
        var vecCon = treeNode.AsVecCon();
        ref var simdValue = ref vecCon.SimdVal;

        Span<byte> bytes = stackalloc byte[16];
        bytes.Clear();
        MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref simdValue, 1))[..treeNode.Type.Size]
            .CopyTo(bytes);

        // v128.const has one byte-array encoding; consumers reinterpret the payload for each vector operation.
        GetEmitter().emitIns_V128Imm(INS_v128_const, bytes);
        WasmProduceReg(treeNode);
    }
#endif
}
#endif
