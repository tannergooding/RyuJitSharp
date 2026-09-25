// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
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

            return id;
        }
        else
        {
            var id = emitAllocInstrCns(attr, unchecked((nuint)cns));

            return id;
        }
    }

    private instrDesc emitNewInstrSC(emitAttr attr, nint cns)
    {
        if (instrDesc.fitsInSmallCns(cns))
        {
            var id = emitNewInstrSmall(attr);
            id.idSmallCns(cns);

            return id;
        }
        else
        {
            var id = emitAllocInstrCns(attr, unchecked((nuint)cns));

            return id;
        }
    }

    private instrDesc emitNewInstrDsp(emitAttr attr, nint dsp)
    {
        if (dsp == 0)
        {
            var id = emitAllocInstr(attr);

            return id;
        }
        else
        {
            var id = emitAllocInstrDsp(attr);
            id.idSetIsLargeDsp();
            id.iddDspVal = dsp;

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

                return id;
            }
            else
            {
                var id = emitAllocInstrCns(size, unchecked((nuint)cns));

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

                return id;
            }
            else
            {
                var id = emitAllocInstrCnsDsp(size);
                id.idSetIsLargeCns();
                id.iddcCnsVal = cns;
                id.idSetIsLargeDsp();
                id.iddcDspVal = dsp;

                return id;
            }
        }
    }
}
