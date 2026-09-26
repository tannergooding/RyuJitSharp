// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class ValueNumStore
{
    public ValueNum ExtendPtrVN(GenTree opA, GenTree opB)
    {
        if (opB.Oper is GT_CNS_INT)
        {
            return ExtendPtrVN(opA, opB.AsIntCon().FieldSeq, opB.AsIntCon().IconValue);
        }

        return NoVN;
    }

    public ValueNum ExtendPtrVN(GenTree opA, FieldSeq? fieldSeq, nint offset)
    {
        var result = NoVN;
        var valueWithExceptions = opA._vnPair.Liberal;
        assert(VNIsValid(valueWithExceptions));
        VNUnpackExc(valueWithExceptions, out var value, out var exceptions);
        assert(VNIsValid(value) && VNIsValid(exceptions));
        VNFuncApp app = default;
        if (!GetVNFunc(value, ref app))
        {
            return result;
        }

        if (app.Func is VNF_PtrToStatic)
        {
            fieldSeq = _compiler.FieldSeqStore.Append(FieldSeqVNToFieldSeq(app.GetArg(1)), fieldSeq);
            result = VNForFunc(TYP_BYREF, VNF_PtrToStatic, app.GetArg(0), VNForFieldSeq(fieldSeq),
                VNForIntPtrCon(unchecked(ConstantValue<nint>(app.GetArg(2)) + offset)));
        }
        else if (app.Func is VNF_PtrToArrElem)
        {
            result = VNForFunc(TYP_BYREF, VNF_PtrToArrElem, app.GetArg(0), app.GetArg(1), app.GetArg(2),
                VNForIntPtrCon(unchecked(ConstantValue<nint>(app.GetArg(3)) + offset)));
        }

        if (result != NoVN)
        {
            result = VNWithExc(result, exceptions);
        }

        return result;
    }
}
