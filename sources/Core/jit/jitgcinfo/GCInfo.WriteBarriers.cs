// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct GCInfo
{
    public readonly bool gcIsWriteBarrierStoreIndNode(GenTreeStoreInd store)
        => gcIsWriteBarrierCandidate(store) is not WriteBarrierForm.WBF_NoBarrier;

    public readonly WriteBarrierForm gcIsWriteBarrierCandidate(GenTreeStoreInd store)
    {
        if (store.Type is not TYP_REF)
        {
            return WriteBarrierForm.WBF_NoBarrier;
        }

        var value = store.Data.SkipCopyOrReload;
        if (value.IsIntegralConst(0) || (value.Oper.IsCnsIntOrI && value.AsIntCon().IsIconHandle(GTF_ICON_OBJ_HDL)))
        {
            return WriteBarrierForm.WBF_NoBarrier;
        }
        if ((store.Flags & GTF_IND_TGT_NOT_HEAP) != 0)
        {
            return WriteBarrierForm.WBF_NoBarrier;
        }
        if ((store.Flags & GTF_IND_TGT_HEAP) != 0)
        {
            return WriteBarrierForm.WBF_BarrierUnchecked;
        }

        var form = gcWriteBarrierFormFromTargetAddress(store.Addr);
        return form is WriteBarrierForm.WBF_BarrierUnknown ? WriteBarrierForm.WBF_BarrierChecked : form;
    }

    public readonly WriteBarrierForm gcWriteBarrierFormFromTargetAddress(GenTree address)
    {
        if (address.Oper is GT_LCL_ADDR)
        {
            return WriteBarrierForm.WBF_NoBarrier;
        }
        if (address.Type is TYP_I_IMPL)
        {
            return WriteBarrierForm.WBF_BarrierUnknown;
        }

        assert(address.Type is TYP_BYREF);
        var simplified = true;
        while (simplified)
        {
            simplified = false;
            address = address.SkipCopyOrReload;
            while (address.Oper is GT_ADD or GT_LEA)
            {
                if (address.Oper is GT_ADD)
                {
                    var first = address.AsOp().Op1;
                    var second = address.AsOp().Op2;
                    if (first.Type is TYP_BYREF or TYP_REF)
                    {
                        assert(((second.Type is not TYP_BYREF) || (second.Oper is GT_CNS_INT)) && (second.Type is not TYP_REF));
                        address = first;
                        simplified = true;
                    }
                    else if (second.Type is TYP_BYREF or TYP_REF)
                    {
                        address = second;
                        simplified = true;
                    }
                    else
                    {
                        return WriteBarrierForm.WBF_BarrierUnknown;
                    }
                }
                else
                {
                    var baseAddress = address.AsAddrMode().BaseAddress;
                    assert(baseAddress is not null);
                    address = baseAddress;
                    if (address.Type is TYP_BYREF or TYP_REF)
                    {
                        simplified = true;
                    }
                    else
                    {
                        return WriteBarrierForm.WBF_BarrierUnknown;
                    }
                }
            }
        }

        return address.Type is TYP_REF ? WriteBarrierForm.WBF_BarrierUnchecked : WriteBarrierForm.WBF_BarrierUnknown;
    }
}
