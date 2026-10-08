// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64 && FEATURE_SIMD
namespace RyuJitSharp;

public partial class Emitter
{
    public static bool isValidVectorIndex(emitAttr datasize, emitAttr elemsize, nint index)
    {
        assert(isValidVectorDatasize(datasize));
        assert(isValidVectorElemsize(elemsize));

        if (index < 0)
        {
            return false;
        }

        if (datasize == EA_8BYTE)
        {
            return elemsize switch
            {
                EA_1BYTE => index < 8,
                EA_2BYTE => index < 4,
                EA_4BYTE => index < 2,
                _ => InvalidVectorElementSize(),
            };
        }

        if (datasize == EA_16BYTE)
        {
            return elemsize switch
            {
                EA_1BYTE => index < 16,
                EA_2BYTE => index < 8,
                EA_4BYTE => index < 4,
                EA_8BYTE => index < 2,
                _ => InvalidVectorElementSize(),
            };
        }

        if (datasize == EA_32BYTE)
        {
            return elemsize switch
            {
                EA_1BYTE => index < 32,
                EA_2BYTE => index < 16,
                EA_4BYTE => index < 8,
                EA_8BYTE => index < 4,
                EA_16BYTE => index < 2,
                _ => InvalidVectorElementSize(),
            };
        }

        NO_WAY("unexpected vector data size");
        return false;
    }

    public void emitIns_S_R_SIMD12(regNumber reg, int varx, int offs)
    {
        assert(_compiler is not null);
        var frameOffset = _compiler.lvaFrameAddress(varx, out var fpBased);
        var offset = unchecked(frameOffset + offs + 8);

        emitIns_S_R(INS_fst_d, EA_8BYTE, reg, varx, offs);
        if (offset < 512)
        {
            assert(offset >= 0);
            var baseReg = fpBased ? REG_FPBASE : REG_SPBASE;
            emitIns_R_R_I_I(INS_vstelm_w, EA_4BYTE, reg, baseReg, offset >> 2, 2);
        }
        else
        {
            emitIns_R_R_I(INS_xvpickve_w, EA_4BYTE, REG_SCRATCH_FLT, reg, 2);
            emitIns_S_R(INS_fst_s, EA_4BYTE, REG_SCRATCH_FLT, varx, offs + 8);
        }
    }

    private static bool InvalidVectorElementSize()
    {
        NO_WAY("unexpected vector element size");
        return false;
    }
}
#endif
