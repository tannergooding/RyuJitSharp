// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class ValueNumStore
{
    public ValueNum VNExcSetSingleton(ValueNum exception)
        => VNForFuncNoFolding(TYP_REF, VNF_ExcSetCons, exception, VNForEmptyExcSet());

    public ValueNumPair VNPExcSetSingleton(ValueNumPair exceptions)
        => new(VNExcSetSingleton(exceptions.Liberal), VNExcSetSingleton(exceptions.Conservative));

    public ValueNumPair VNPWithExc(ValueNumPair values, ValueNumPair exceptions)
        => new(VNWithExc(values.Liberal, exceptions.Liberal), VNWithExc(values.Conservative, exceptions.Conservative));

    public ValueNum VNExceptionSet(ValueNum vn)
    {
        var normal = NoVN;
        var exceptions = NoVN;
        return IsVNBinFunc(vn, VNF_ValWithExc, ref normal, ref exceptions) ? exceptions : VNForEmptyExcSet();
    }

    public ValueNumPair VNPExceptionSet(ValueNumPair values)
        => new(VNExceptionSet(values.Liberal), VNExceptionSet(values.Conservative));

    private bool VNCheckAscending(ValueNum item, ValueNum set)
    {
        if (set == VNForEmptyExcSet())
        {
            return true;
        }

        var app = new VNFuncApp();
        var found = GetVNFunc(set, ref app);
        assert(found && app.FuncIs(VNF_ExcSetCons));
        return unchecked((uint)item) < unchecked((uint)app.GetArg(0));
    }

    public ValueNum VNExcSetUnion(ValueNum xs0, ValueNum xs1)
    {
        if (xs0 == VNForEmptyExcSet())
        {
            return xs1;
        }

        if (xs1 == VNForEmptyExcSet())
        {
            return xs0;
        }

        var app0 = new VNFuncApp();
        var found0 = GetVNFunc(xs0, ref app0);
        assert(found0 && app0.FuncIs(VNF_ExcSetCons));
        var app1 = new VNFuncApp();
        var found1 = GetVNFunc(xs1, ref app1);
        assert(found1 && app1.FuncIs(VNF_ExcSetCons));

        var item0 = app0.GetArg(0);
        var item1 = app1.GetArg(0);
        if (unchecked((uint)item0) < unchecked((uint)item1))
        {
            assert(VNCheckAscending(item0, app0.GetArg(1)));
            return VNForFuncNoFolding(TYP_REF, VNF_ExcSetCons, item0, VNExcSetUnion(app0.GetArg(1), xs1));
        }

        if (item0 == item1)
        {
            assert(VNCheckAscending(item0, app0.GetArg(1)));
            assert(VNCheckAscending(item1, app1.GetArg(1)));
            return VNForFuncNoFolding(TYP_REF, VNF_ExcSetCons, item0, VNExcSetUnion(app0.GetArg(1), app1.GetArg(1)));
        }

        assert(VNCheckAscending(item1, app1.GetArg(1)));
        return VNForFuncNoFolding(TYP_REF, VNF_ExcSetCons, item1, VNExcSetUnion(xs0, app1.GetArg(1)));
    }

    public bool VNHasExc(ValueNum vn)
    {
        var app = new VNFuncApp();
        return GetVNFunc(vn, ref app) && app.FuncIs(VNF_ValWithExc);
    }

    public void VNUnpackExc(ValueNum vnWithExc, out ValueNum vn, out ValueNum exceptions)
    {
        assert(vnWithExc != NoVN);
        vn = vnWithExc;
        exceptions = VNForEmptyExcSet();
        _ = IsVNBinFunc(vnWithExc, VNF_ValWithExc, ref vn, ref exceptions);
    }

    public ValueNum VNWithExc(ValueNum vn, ValueNum exceptions)
    {
        if (exceptions == VNForEmptyExcSet())
        {
            return vn;
        }

        VNUnpackExc(vn, out var normal, out var existing);
        return VNForFuncNoFolding(TypeOfVN(normal), VNF_ValWithExc, normal, VNExcSetUnion(existing, exceptions));
    }
}
