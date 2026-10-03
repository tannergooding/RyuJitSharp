// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
#if EMITTER_STATS
    private const int SMALL_CNS_TSZ = 256;

    private static readonly uint[] emitSmallCns = new uint[SMALL_CNS_TSZ];
    private static uint emitSmallCnsCnt;
    private static uint emitLargeCnsCnt;
    private static uint emitInt8CnsCnt;
    private static uint emitInt16CnsCnt;
    private static uint emitInt32CnsCnt;
    private static uint emitNegCnsCnt;
    private static uint emitPow2CnsCnt;
    private static uint emitSmallDspCnt;
    private static uint emitLargeDspCnt;

    private void TrackCns(nint value)
    {
        if (value < 0)
        {
            emitNegCnsCnt = unchecked(emitNegCnsCnt + 1);

            if (value >= sbyte.MinValue)
            {
                emitInt8CnsCnt = unchecked(emitInt8CnsCnt + 1);
            }
            else if (value >= short.MinValue)
            {
                emitInt16CnsCnt = unchecked(emitInt16CnsCnt + 1);
            }
            else if (value >= int.MinValue)
            {
                emitInt32CnsCnt = unchecked(emitInt32CnsCnt + 1);
            }
        }
        else if (value <= sbyte.MaxValue)
        {
            emitInt8CnsCnt = unchecked(emitInt8CnsCnt + 1);
        }
        else if (value <= short.MaxValue)
        {
            emitInt16CnsCnt = unchecked(emitInt16CnsCnt + 1);
        }
        else if (value <= int.MaxValue)
        {
            emitInt32CnsCnt = unchecked(emitInt32CnsCnt + 1);
        }

        if ((value > 0) && ((value & (value - 1)) == 0))
        {
            emitPow2CnsCnt = unchecked(emitPow2CnsCnt + 1);
        }
    }

    private void TrackSmallCns(nint value)
    {
        assert(instrDesc.fitsInSmallCns(value));

        // Fold values outside [-128, 126] into the histogram's endpoint buckets.
        var index = 0;

        if (value >= ((SMALL_CNS_TSZ / 2) - 1))
        {
            index = SMALL_CNS_TSZ - 1;
        }
        else if (value >= (0 - SMALL_CNS_TSZ / 2))
        {
            index = (int)(value + (SMALL_CNS_TSZ / 2));
        }

        emitSmallCnsCnt = unchecked(emitSmallCnsCnt + 1);
        emitSmallCns[index] = unchecked(emitSmallCns[index] + 1);

        TrackCns(value);
    }

    private void TrackLargeCns(nint value)
    {
        emitLargeCnsCnt = unchecked(emitLargeCnsCnt + 1);

        TrackCns(value);
    }
#endif

    private instrDescCns emitAllocInstrCns(emitAttr attr)
    {
        return emitAllocAnyInstr<instrDescCns>(ConstantDescriptorSizes.Constant, attr);
    }

    private instrDescCns emitAllocInstrCns(emitAttr attr, nuint cns)
    {
        var result = emitAllocInstrCns(attr);
        result.idSetIsLargeCns();
        result.idcCnsVal = unchecked((nint)cns);

        return result;
    }

    private instrDescDsp emitAllocInstrDsp(emitAttr attr)
    {
        return emitAllocAnyInstr<instrDescDsp>(ConstantDescriptorSizes.Displacement, attr);
    }

    private instrDescCnsDsp emitAllocInstrCnsDsp(emitAttr attr)
    {
        return emitAllocAnyInstr<instrDescCnsDsp>(ConstantDescriptorSizes.ConstantDisplacement, attr);
    }

#if TARGET_XARCH
    private instrDescAmd emitAllocInstrAmd(emitAttr attr)
    {
        return emitAllocAnyInstr<instrDescAmd>(ConstantDescriptorSizes.AddressMode, attr);
    }

    private instrDescCnsAmd emitAllocInstrCnsAmd(emitAttr attr)
    {
        return emitAllocAnyInstr<instrDescCnsAmd>(ConstantDescriptorSizes.ConstantAddressMode, attr);
    }
#endif

    private instrDesc emitNewInstrCns(emitAttr attr, nint cns)
    {
        if (instrDesc.fitsInSmallCns(cns))
        {
            var id = emitAllocInstr(attr);
            id.idSmallCns(cns);

#if EMITTER_STATS
            TrackSmallCns(cns);
#endif

            return id;
        }
        else
        {
            var id = emitAllocInstrCns(attr, unchecked((nuint)cns));

#if EMITTER_STATS
            TrackLargeCns(cns);
#endif

            return id;
        }
    }

    private instrDesc emitNewInstrSC(emitAttr attr, nint cns)
    {
        if (instrDesc.fitsInSmallCns(cns))
        {
            var id = emitNewInstrSmall(attr);
            id.idSmallCns(cns);

#if EMITTER_STATS
            TrackSmallCns(cns);
#endif

            return id;
        }
        else
        {
            var id = emitAllocInstrCns(attr, unchecked((nuint)cns));

#if EMITTER_STATS
            TrackLargeCns(cns);
#endif

            return id;
        }
    }

    private instrDesc emitNewInstrDsp(emitAttr attr, nint dsp)
    {
        if (dsp == 0)
        {
            var id = emitAllocInstr(attr);

#if EMITTER_STATS
            emitSmallDspCnt = unchecked(emitSmallDspCnt + 1);
#endif

            return id;
        }
        else
        {
            var id = emitAllocInstrDsp(attr);
            id.idSetIsLargeDsp();
            id.iddDspVal = dsp;

#if EMITTER_STATS
            emitLargeDspCnt = unchecked(emitLargeDspCnt + 1);
#endif

            return id;
        }
    }

    private instrDesc emitNewInstrCnsDsp(emitAttr size, nint cns, int dsp)
    {
        if (dsp == 0)
        {
            if (instrDesc.fitsInSmallCns(cns))
            {
                var id = emitAllocInstr(size);
                id.idSmallCns(cns);

#if EMITTER_STATS
                TrackSmallCns(cns);
                emitSmallDspCnt = unchecked(emitSmallDspCnt + 1);
#endif

                return id;
            }
            else
            {
                var id = emitAllocInstrCns(size, unchecked((nuint)cns));

#if EMITTER_STATS
                TrackLargeCns(cns);
                emitSmallDspCnt = unchecked(emitSmallDspCnt + 1);
#endif

                return id;
            }
        }
        else
        {
            if (instrDesc.fitsInSmallCns(cns))
            {
                var id = emitAllocInstrDsp(size);
                id.idSetIsLargeDsp();
                id.iddDspVal = dsp;
                id.idSmallCns(cns);

#if EMITTER_STATS
                TrackSmallCns(cns);
                emitLargeDspCnt = unchecked(emitLargeDspCnt + 1);
#endif

                return id;
            }
            else
            {
                var id = emitAllocInstrCnsDsp(size);
                id.idSetIsLargeCns();
                id.iddcCnsVal = cns;
                id.idSetIsLargeDsp();
                id.iddcDspVal = dsp;

#if EMITTER_STATS
                TrackLargeCns(cns);
                emitLargeDspCnt = unchecked(emitLargeDspCnt + 1);
#endif

                return id;
            }
        }
    }
}
