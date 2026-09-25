// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_SIMD
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public partial class Emitter
{
    public unsafe CORINFO_FIELD_HANDLE emitSimd8Const(simd8_t constValue)
    {
        var cnsAlign = 8u;
#if TARGET_XARCH
        assert(_compiler is not null);
        if (_compiler.compCodeOpt == Compiler.SMALL_CODE)
        {
            cnsAlign = dataSection.MIN_DATA_ALIGN;
        }
#endif
        var bytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(in constValue, 1));
        var cnum = emitDataConst(bytes, cnsAlign, TYP_SIMD8);

        return Compiler.eeFindJitDataOffs(cnum);
    }

    public unsafe CORINFO_FIELD_HANDLE emitSimd16Const(simd16_t constValue)
    {
        var cnsAlign = 16u;
#if TARGET_XARCH
        assert(_compiler is not null);
        if (_compiler.compCodeOpt == Compiler.SMALL_CODE)
        {
            cnsAlign = dataSection.MIN_DATA_ALIGN;
        }
#endif
        var bytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(in constValue, 1));
        var cnum = emitDataConst(bytes, cnsAlign, TYP_SIMD16);

        return Compiler.eeFindJitDataOffs(cnum);
    }

#if TARGET_XARCH
    public unsafe CORINFO_FIELD_HANDLE emitSimdConst(in simd_t constValue, emitAttr attr)
    {
        assert(_compiler is not null);
        var cnsSize = (int)EA_SIZE(attr);
        var cnsAlign = (uint)cnsSize;
        var dataType = cnsSize >= 8 ? Compiler.GetSimdTypeForSize(cnsSize) : TYP_FLOAT;

        if (_compiler.compCodeOpt == Compiler.SMALL_CODE)
        {
            cnsAlign = dataSection.MIN_DATA_ALIGN;
        }

        var bytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(in constValue, 1));
        var cnum = emitDataConst(bytes[..cnsSize], cnsAlign, dataType);

        return Compiler.eeFindJitDataOffs(cnum);
    }

    public unsafe void emitSimdConstCompressedLoad(in simd_t constValue, emitAttr attr, regNumber targetReg)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "SIMD constant-load recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        assert(_compiler is not null);
        assert((EA_SIZE(attr) >= EA_8BYTE) && (EA_SIZE(attr) <= EA_64BYTE));

        var cnsSize = (int)EA_SIZE(attr);
        var dataSize = cnsSize;
        var ins = cnsSize == 8 ? INS_movsd_simd : INS_movups;

        // Prefer repeated-prefix broadcasts to loads that merely zero the upper lanes.
        if ((dataSize == 64) && (constValue.v256[1] == constValue.v256[0]))
        {
            assert(_compiler.compIsaSupportedDebugOnly(InstructionSet_AVX512));
            dataSize = 32;
            ins = INS_vbroadcastf32x8;
        }

        if ((dataSize == 32) && (constValue.v128[1] == constValue.v128[0]))
        {
            assert(_compiler.compIsaSupportedDebugOnly(InstructionSet_AVX));
            dataSize = 16;
            ins = INS_vbroadcastf32x4;
        }

        if ((dataSize == 16) && (constValue.u64[1] == constValue.u64[0]))
        {
            dataSize = 8;
            ins = cnsSize == 16 ? INS_movddup : INS_vbroadcastsd;
        }

        // A scalar broadcast fills a whole register and cannot preserve SIMD8's upper zeroes.
        if ((dataSize == 8) && (cnsSize >= 16) && (constValue.u32[1] == constValue.u32[0]))
        {
            if (_compiler.compOpportunisticallyDependsOn(InstructionSet_AVX))
            {
                dataSize = 4;
                ins = INS_vbroadcastss;
            }
        }

        if (dataSize < cnsSize)
        {
            var handle = emitSimdConst(in constValue, (emitAttr)dataSize);
            emitIns_R_C(ins, attr, targetReg, handle, 0);
            return;
        }

        simd32_t zeroValue = default;
        if ((dataSize == 64) && (constValue.v256[1] == zeroValue))
        {
            dataSize = 32;
        }

        if ((dataSize == 32) && (constValue.v128[1] == zeroValue.v128[0]))
        {
            dataSize = 16;
        }

        if ((dataSize == 16) && (constValue.u64[1] == 0))
        {
            dataSize = 8;
            ins = INS_movsd_simd;
        }

        if ((dataSize == 8) && (constValue.u32[1] == 0))
        {
            dataSize = 4;
            ins = INS_movss;
        }

        // Memory loads zero-extend, so only this path uses the reduced instruction width.
        attr = (emitAttr)dataSize;
        var cnum = emitSimdConst(in constValue, attr);
        emitIns_R_C(ins, attr, targetReg, cnum, 0);
#endif
    }
#endif

#if FEATURE_MASKED_HW_INTRINSICS
    public unsafe CORINFO_FIELD_HANDLE emitSimdMaskConst(simdmask_t constValue)
    {
        var cnsAlign = 8u;
#if TARGET_XARCH
        assert(_compiler is not null);
        if (_compiler.compCodeOpt == Compiler.SMALL_CODE)
        {
            cnsAlign = dataSection.MIN_DATA_ALIGN;
        }
#endif
        var bytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(in constValue, 1));
        var cnum = emitDataConst(bytes, cnsAlign, TYP_MASK);

        return Compiler.eeFindJitDataOffs(cnum);
    }
#endif
}
#endif
