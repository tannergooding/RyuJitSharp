// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

public partial class Emitter
{
#if EMITTER_STATS
    private static uint emitTotalIDescLclVarPairCnt;
    private static uint emitTotalIDescLclVarPairCnsCnt;
#endif

    private instrDescLclVarPair emitAllocInstrLclVarPair(emitAttr attr)
    {
#if EMITTER_STATS
        emitTotalIDescLclVarPairCnt = unchecked(emitTotalIDescLclVarPairCnt + 1);
#endif
        var result = emitAllocAnyInstr<instrDescLclVarPair>(DescriptorSizes.LocalVarPair, attr);
        result.idSetIsLclVarPair();

        return result;
    }

    private instrDescLclVarPairCns emitAllocInstrLclVarPairCns(emitAttr attr, nint cns)
    {
#if EMITTER_STATS
        emitTotalIDescLclVarPairCnsCnt = unchecked(emitTotalIDescLclVarPairCnsCnt + 1);
#endif
        var result = emitAllocAnyInstr<instrDescLclVarPairCns>(DescriptorSizes.LocalVarPairConstant, attr);
        result.idSetIsLargeCns();
        result.idSetIsLclVarPair();
        result.idcCnsVal = cns;

        return result;
    }

    private instrDesc emitNewInstrLclVarPair(emitAttr attr, nint cns)
    {
#if EMITTER_STATS
        emitTotalIDescCnt = unchecked(emitTotalIDescCnt + 1);
        emitTotalIDescCnsCnt = unchecked(emitTotalIDescCnsCnt + 1);
#endif
        if (instrDesc.fitsInSmallCns(cns))
        {
            var id = emitAllocInstrLclVarPair(attr);
            id.idSmallCns(cns);

            return id;
        }
        else
        {
            var id = emitAllocInstrLclVarPairCns(attr, cns);

            return id;
        }
    }

    private static ref emitLclVarAddr emitGetLclVarPairLclVar2(instrDesc id)
    {
        assert(id.idIsLclVarPair());
        if (id.idIsLargeCns())
        {
            return ref ((instrDescLclVarPairCns)id).iiaLclVar2;
        }
        else
        {
            return ref ((instrDescLclVarPair)id).iiaLclVar2;
        }
    }

    public abstract partial class instrDesc
    {
        private bool _idLclVarPair;

        public bool idIsLclVarPair()
        {
            return _idLclVarPair;
        }

        public void idSetIsLclVarPair()
        {
            _idLclVarPair = true;
        }
    }

    protected sealed class instrDescLclVarPair : instrDesc
    {
        public emitLclVarAddr iiaLclVar2;

        public override int NativeLogicalSize => DescriptorSizes.LocalVarPair;
    }

    protected sealed class instrDescLclVarPairCns : instrDescCns
    {
        public emitLclVarAddr iiaLclVar2;

        public override int NativeLogicalSize => DescriptorSizes.LocalVarPairConstant;
    }
}
#endif
