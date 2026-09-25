// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
namespace RyuJitSharp;

public partial class Emitter
{
    private const int AM_DISP_BITS = 32 - (2 * (REGNUM_BITS + 1)) - 2;
    private const int AM_DISP_BIG_VAL = -(1 << (AM_DISP_BITS - 1));
    private const int AM_DISP_MIN = -((1 << (AM_DISP_BITS - 1)) - 1);
    private const int AM_DISP_MAX = (1 << (AM_DISP_BITS - 1)) - 1;

    public struct CnsVal
    {
        public nint cnsVal;
        public bool cnsReloc;
    }

    private instrDesc emitNewInstrAmd(emitAttr size, nint dsp)
    {
        if ((dsp < AM_DISP_MIN) || (dsp > AM_DISP_MAX))
        {
            var id = emitAllocInstrAmd(size);
            id.idSetIsLargeDsp();
#if DEBUG
            id.idAddr().iiaAddrMode.amDisp = AM_DISP_BIG_VAL;
#endif
            id.idaAmdVal = dsp;

            return id;
        }
        else
        {
            var id = emitAllocInstr(size);
            id.idAddr().iiaAddrMode.amDisp = (int)dsp;
            assert(id.idAddr().iiaAddrMode.amDisp == dsp);

            return id;
        }
    }

    private void emitSetAmdDisp(instrDescAmd id, nint dsp)
    {
        if ((dsp < AM_DISP_MIN) || (dsp > AM_DISP_MAX))
        {
            id.idSetIsLargeDsp();
#if DEBUG
            id.idAddr().iiaAddrMode.amDisp = AM_DISP_BIG_VAL;
#endif
            id.idaAmdVal = dsp;
        }
        else
        {
            id.idSetIsSmallDsp();
            id.idAddr().iiaAddrMode.amDisp = (int)dsp;
            assert(id.idAddr().iiaAddrMode.amDisp == dsp);
        }
    }

    private instrDesc emitNewInstrAmdCns(emitAttr size, nint dsp, int cns)
    {
        if ((dsp >= AM_DISP_MIN) && (dsp <= AM_DISP_MAX))
        {
            var id = emitNewInstrCns(size, cns);
            id.idAddr().iiaAddrMode.amDisp = (int)dsp;
            assert(id.idAddr().iiaAddrMode.amDisp == dsp);

            return id;
        }
        else
        {
            if (instrDesc.fitsInSmallCns(cns))
            {
                var id = emitAllocInstrAmd(size);
                id.idSetIsLargeDsp();
#if DEBUG
                id.idAddr().iiaAddrMode.amDisp = AM_DISP_BIG_VAL;
#endif
                id.idaAmdVal = dsp;
                id.idSmallCns(cns);

                return id;
            }
            else
            {
                var id = emitAllocInstrCnsAmd(size);
                id.idSetIsLargeCns();
                id.idacCnsVal = cns;
                id.idSetIsLargeDsp();
#if DEBUG
                id.idAddr().iiaAddrMode.amDisp = AM_DISP_BIG_VAL;
#endif
                id.idacAmdVal = dsp;

                return id;
            }
        }
    }

    private nint emitGetInsCns(instrDesc id)
    {
        return id.idIsLargeCns() ? ((instrDescCns)id).idcCnsVal : id.idSmallCns();
    }

    private nint emitGetInsDsp(instrDesc id)
    {
        if (id.idIsLargeDsp())
        {
            if (id.idIsLargeCns())
            {
                return ((instrDescCnsDsp)id).iddcDspVal;
            }

            return ((instrDescDsp)id).iddDspVal;
        }

        return 0;
    }

    private nint emitGetInsAmd(instrDesc id)
    {
        return id.idIsLargeDsp() ? ((instrDescAmd)id).idaAmdVal : id.idAddr().iiaAddrMode.amDisp;
    }

    private void emitGetInsCns(instrDesc id, ref CnsVal cv)
    {
        cv.cnsReloc = id.idIsCnsReloc();

        if (id.idIsLargeCns())
        {
            cv.cnsVal = ((instrDescCns)id).idcCnsVal;
        }
        else
        {
            cv.cnsVal = id.idSmallCns();
        }
    }

    private nint emitGetInsAmdCns(instrDesc id, ref CnsVal cv)
    {
        cv.cnsReloc = id.idIsCnsReloc();

        if (id.idIsLargeDsp())
        {
            if (id.idIsLargeCns())
            {
                cv.cnsVal = ((instrDescCnsAmd)id).idacCnsVal;

                return ((instrDescCnsAmd)id).idacAmdVal;
            }
            else
            {
                cv.cnsVal = id.idSmallCns();

                return ((instrDescAmd)id).idaAmdVal;
            }
        }
        else
        {
            if (id.idIsLargeCns())
            {
                cv.cnsVal = ((instrDescCns)id).idcCnsVal;
            }
            else
            {
                cv.cnsVal = id.idSmallCns();
            }

            return id.idAddr().iiaAddrMode.amDisp;
        }
    }

    private void emitGetInsDcmCns(instrDesc id, ref CnsVal cv)
    {
        cv.cnsReloc = id.idIsCnsReloc();

        if (id.idIsLargeCns())
        {
            if (id.idIsLargeDsp())
            {
                cv.cnsVal = ((instrDescCnsDsp)id).iddcCnsVal;
            }
            else
            {
                cv.cnsVal = ((instrDescCns)id).idcCnsVal;
            }
        }
        else
        {
            cv.cnsVal = id.idSmallCns();
        }
    }

    private nint emitGetInsAmdAny(instrDesc id)
    {
        if (id.idIsLargeDsp())
        {
            if (id.idIsLargeCns())
            {
                return ((instrDescCnsAmd)id).idacAmdVal;
            }

            return ((instrDescAmd)id).idaAmdVal;
        }

        return id.idAddr().iiaAddrMode.amDisp;
    }
}
#endif
