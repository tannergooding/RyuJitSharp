// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Emitter
{
    private const int SMALL_CNS_TSZ = 256;

    private static uint emitTotalIDescCnsCnt;
    private static uint emitTotalIDescDspCnt;
    private static uint emitTotalIDescCnsDspCnt;
    private static uint emitTotalIDescAmdCnt;
    private static uint emitTotalIDescCnsAmdCnt;

    private static readonly uint[]? emitSmallCns =
        Configuration.EMITTER_STATS ? new uint[SMALL_CNS_TSZ] : null;
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

        var smallConstantCounts = emitSmallCns!;
        emitSmallCnsCnt = unchecked(emitSmallCnsCnt + 1);
        smallConstantCounts[index] = unchecked(smallConstantCounts[index] + 1);

        TrackCns(value);
    }

    private void TrackLargeCns(nint value)
    {
        emitLargeCnsCnt = unchecked(emitLargeCnsCnt + 1);

        TrackCns(value);
    }

    private instrDescCns emitAllocInstrCns(emitAttr attr)
    {
        if (Configuration.EMITTER_STATS)
        {
            emitTotalIDescCnsCnt = unchecked(emitTotalIDescCnsCnt + 1);
        }

        return emitAllocAnyInstr<instrDescCns>(unchecked((nuint)ConstantDescriptorSizes.Constant), attr);
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
        if (Configuration.EMITTER_STATS)
        {
            emitTotalIDescDspCnt = unchecked(emitTotalIDescDspCnt + 1);
        }

        return emitAllocAnyInstr<instrDescDsp>(unchecked((nuint)ConstantDescriptorSizes.Displacement), attr);
    }

    private instrDescCnsDsp emitAllocInstrCnsDsp(emitAttr attr)
    {
        if (Configuration.EMITTER_STATS)
        {
            emitTotalIDescCnsDspCnt = unchecked(emitTotalIDescCnsDspCnt + 1);
        }

        return emitAllocAnyInstr<instrDescCnsDsp>(unchecked((nuint)ConstantDescriptorSizes.ConstantDisplacement), attr);
    }

    private instrDescAmd emitAllocInstrAmd(emitAttr attr)
    {
        RequireXarchAddressModeDescriptor();

        if (Configuration.EMITTER_STATS)
        {
            emitTotalIDescAmdCnt = unchecked(emitTotalIDescAmdCnt + 1);
        }

        return emitAllocAnyInstr<instrDescAmd>(unchecked((nuint)ConstantDescriptorSizes.AddressMode), attr);
    }

    private instrDescCnsAmd emitAllocInstrCnsAmd(emitAttr attr)
    {
        RequireXarchAddressModeDescriptor();

        if (Configuration.EMITTER_STATS)
        {
            emitTotalIDescCnsAmdCnt = unchecked(emitTotalIDescCnsAmdCnt + 1);
        }

        return emitAllocAnyInstr<instrDescCnsAmd>(unchecked((nuint)ConstantDescriptorSizes.ConstantAddressMode), attr);
    }

    private static void RequireXarchAddressModeDescriptor()
    {
        if (!Configuration.TARGET_XARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "Address-mode descriptor allocation requires xarch.");
        }
    }

    private instrDesc emitNewInstrCns(emitAttr attr, nint cns)
    {
        if (instrDesc.fitsInSmallCns(cns))
        {
            var id = emitAllocInstr(attr);
            id.idSmallCns(cns);

            if (Configuration.EMITTER_STATS)
            {
                TrackSmallCns(cns);
            }

            return id;
        }
        else
        {
            var id = emitAllocInstrCns(attr, unchecked((nuint)cns));

            if (Configuration.EMITTER_STATS)
            {
                TrackLargeCns(cns);
            }

            return id;
        }
    }

    private instrDesc emitNewInstrSC(emitAttr attr, nint cns)
    {
        if (instrDesc.fitsInSmallCns(cns))
        {
            var id = emitNewInstrSmall(attr);
            id.idSmallCns(cns);

            if (Configuration.EMITTER_STATS)
            {
                TrackSmallCns(cns);
            }

            return id;
        }
        else
        {
            var id = emitAllocInstrCns(attr, unchecked((nuint)cns));

            if (Configuration.EMITTER_STATS)
            {
                TrackLargeCns(cns);
            }

            return id;
        }
    }

    private instrDesc emitNewInstrDsp(emitAttr attr, nint dsp)
    {
        if (dsp == 0)
        {
            var id = emitAllocInstr(attr);

            if (Configuration.EMITTER_STATS)
            {
                emitSmallDspCnt = unchecked(emitSmallDspCnt + 1);
            }

            return id;
        }
        else
        {
            var id = emitAllocInstrDsp(attr);
            id.idSetIsLargeDsp();
            id.iddDspVal = dsp;

            if (Configuration.EMITTER_STATS)
            {
                emitLargeDspCnt = unchecked(emitLargeDspCnt + 1);
            }

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

                if (Configuration.EMITTER_STATS)
                {
                    TrackSmallCns(cns);
                    emitSmallDspCnt = unchecked(emitSmallDspCnt + 1);
                }

                return id;
            }
            else
            {
                var id = emitAllocInstrCns(size, unchecked((nuint)cns));

                if (Configuration.EMITTER_STATS)
                {
                    TrackLargeCns(cns);
                    emitSmallDspCnt = unchecked(emitSmallDspCnt + 1);
                }

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

                if (Configuration.EMITTER_STATS)
                {
                    TrackSmallCns(cns);
                    emitLargeDspCnt = unchecked(emitLargeDspCnt + 1);
                }

                return id;
            }
            else
            {
                var id = emitAllocInstrCnsDsp(size);
                id.idSetIsLargeCns();
                id.iddcCnsVal = cns;
                id.idSetIsLargeDsp();
                id.iddcDspVal = dsp;

                if (Configuration.EMITTER_STATS)
                {
                    TrackLargeCns(cns);
                    emitLargeDspCnt = unchecked(emitLargeDspCnt + 1);
                }

                return id;
            }
        }
    }
}
