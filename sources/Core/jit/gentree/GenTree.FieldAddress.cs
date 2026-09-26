// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class GenTree
{
    public unsafe bool IsFieldAddr(Compiler compiler, out GenTree? baseAddr, out FieldSeq? fieldSeq, out nint offset)
    {
        assert(Type is TYP_I_IMPL or TYP_BYREF or TYP_REF);

        baseAddr = null;
        fieldSeq = null;
        offset = 0;

        GenTree? candidateBase = null;
        FieldSeq? candidateSeq;
        nint candidateOffset;
        if (Oper is GT_ADD)
        {
            if (!AsOp().Op2.Oper.IsCnsIntOrI)
            {
                return false;
            }

            candidateBase = AsOp().Op1;
            var constant = AsOp().Op2.AsIntCon();
            candidateSeq = constant.FieldSeq;
            candidateOffset = constant.IconValue;
            if ((candidateSeq is not null) && (candidateSeq.Kind is FieldSeq.FieldKind.SimpleStaticKnownAddress))
            {
                return false;
            }
        }
        else if (Oper.IsCnsIntOrI && AsIntCon().IsIconHandle(GTF_ICON_STATIC_HDL))
        {
            var constant = AsIntCon();
            candidateSeq = constant.FieldSeq;
            candidateOffset = constant.IconValue;
            assert((candidateSeq is null) || (candidateSeq.Kind is FieldSeq.FieldKind.SimpleStaticKnownAddress));
        }
        else
        {
            return false;
        }

        if (candidateSeq is null)
        {
            return false;
        }

        candidateOffset = unchecked(candidateOffset - candidateSeq.Offset);
        if (candidateSeq.IsStaticField)
        {
            if (candidateSeq.IsSharedStaticField)
            {
                baseAddr = candidateBase;
            }

            fieldSeq = candidateSeq;
            offset = candidateOffset;
            return true;
        }

        if (candidateBase is not null && candidateBase.Type is TYP_REF)
        {
            assert(!compiler.eeIsValueClass(compiler.info.compCompHnd->getFieldClass(candidateSeq.FieldHandle)));
            baseAddr = candidateBase;
            fieldSeq = candidateSeq;
            offset = candidateOffset;
            return true;
        }

        return false;
    }
}
