// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;

namespace RyuJitSharp;

public partial class Compiler
{
    public int fgGetFieldMorphingTemp(GenTreeFieldAddr field)
    {
        assert(field.IsInstance);
        int localNumber;
        if (field.IsOffsetKnown && (field.FldOffset == 0))
        {
            // Reusing a zero-offset temp can put a use before its defining
            // store, in a shape that the subsequent morphing cannot handle.
            localNumber = lvaGrabTemp(true, "Zero offset field obj");
        }
        else
        {
            var type = field.FldObj.Type.ActualType;
            localNumber = fgBigOffsetMorphingTemps[(int)type];
            if (localNumber == BAD_VAR_NUM)
            {
                localNumber = lvaGrabTemp(false, "Field obj");
                fgBigOffsetMorphingTemps[(int)type] = localNumber;
            }
            else
            {
                noway_assert(lvaGetDesc(localNumber).Type == type);
            }
        }
        assert(localNumber != BAD_VAR_NUM);
        return localNumber;
    }

    public unsafe GenTree fgMorphExpandInstanceField(GenTree tree)
    {
        assert((tree.Oper is GT_FIELD_ADDR) && tree.AsFieldAddr().IsInstance);
        var field = tree.AsFieldAddr();
        var obj = field.FldObj;
        var fieldHandle = field.FldHnd;
        var fieldOffset = unchecked((uint)field.FldOffset);
        noway_assert(varTypeIsI(obj.Type.ActualType));

        var objectType = obj.Type;
        GenTree address;
        GenTree? comma = null;
        // Field marking may delegate the null check to a consuming indirection.
        // Otherwise replace the field's value dependency with COMMA(NULLCHECK, ...).
        var explicitNullCheck = fgAddrCouldBeNull(obj) && ((tree.Flags & GTF_FLD_TGT_NONFAULTING) == 0);
        if (explicitNullCheck)
        {
            JITDUMP("Before explicit null check morphing:\n");
            DISPTREE(tree);

            GenTree? store = null;
            int localNumber;
            if ((obj.Oper is not GT_LCL_VAR) || lvaIsLocalImplicitlyAccessedByRef(obj.AsLclVar().LclNum))
            {
                localNumber = fgGetFieldMorphingTemp(field);
                store = gtNewTempStore(localNumber, obj);
            }
            else
            {
                localNumber = obj.AsLclVarCommon().LclNum;
            }
            var local = gtNewLclvNode(objectType, localNumber);
            var nullCheck = gtNewNullCheck(local);
            nullCheck.HasOrderingSideEffect = true;
            comma = store is not null ? gtNewBinaryNode(GT_COMMA, TYP_VOID, store, nullCheck) : nullCheck;
            address = gtNewLclvNode(objectType, localNumber);
        }
        else
        {
            address = obj;
        }

#if FEATURE_READYTORUN
        if (field.FieldLookup.addr is not null)
        {
            if (field.FieldLookup.accessType is not IAT_PVALUE)
            {
                throw new UnreachableException("unexpected accessType for R2R field access");
            }
            var offset = gtNewIndOfIconHandleNode(TYP_I_IMPL, (nint)field.FieldLookup.addr, GTF_ICON_CONST_PTR);
#if DEBUG
            offset.Addr.AsIntCon().TargetHandle = (nint)fieldHandle;
#endif
            address = gtNewBinaryNode(GT_ADD, objectType is TYP_I_IMPL ? TYP_I_IMPL : TYP_BYREF, address, offset);
            if (explicitNullCheck && (address.Type is TYP_BYREF))
            {
                address.HasOrderingSideEffect = true;
            }
        }
#endif
        FieldSeq? fields = null;
        if ((objectType is TYP_REF) && !field.MayOverlap)
        {
            fields = FieldSeqStore.Create(fieldHandle, unchecked((nint)fieldOffset), FieldSeq.FieldKind.Instance);
        }
        if (fieldOffset != 0)
        {
            address = gtNewBinaryNode(GT_ADD, objectType is TYP_I_IMPL ? TYP_I_IMPL : TYP_BYREF,
                address, gtNewIconNode(unchecked((nint)fieldOffset), fields));
            // Do not form a potentially out-of-object byref before its null check.
            if (explicitNullCheck && (address.Type is TYP_BYREF))
            {
                address.HasOrderingSideEffect = true;
            }
            if (address.AsOp().Op1.Oper.IsConst && address.AsOp().Op2.Oper.IsConst)
            {
                address = gtFoldExprConst(address);
            }
        }
        if (explicitNullCheck)
        {
            assert(comma is not null);
            address = gtNewBinaryNode(GT_COMMA, address.Type, comma, address);
            JITDUMP("After adding explicit null check:\n");
            DISPTREE(address);
        }
        return address;
    }

    public unsafe GenTreeOp fgMorphExpandTlsFieldAddr(GenTree tree)
    {
        assert((tree.Oper is GT_FIELD_ADDR) && tree.AsFieldAddr().IsTlsStatic);
        var field = tree.AsFieldAddr();
        var fieldHandle = field.FldHnd;
        void** idAddress = null;
        var id = unchecked((uint)info.compCompHnd->getFieldThreadLocalStoreID(fieldHandle, (void**)&idAddress));
        GenTree? dllReference = null;
#pragma warning disable CA1508 // The EE writes idAddress through the double pointer passed to getFieldThreadLocalStoreID.
        if (idAddress is null)
        {
            if (id != 0)
            {
                dllReference = gtNewIconNode(TYP_I_IMPL, unchecked((nint)(id * 4)));
            }
        }
        else
        {
            dllReference = gtNewIndOfIconHandleNode(TYP_I_IMPL, (nint)idAddress, GTF_ICON_CONST_PTR);
            dllReference = gtNewBinaryNode(GT_MUL, TYP_I_IMPL, dllReference, gtNewIconNode(TYP_I_IMPL, 4));
        }
#pragma warning restore CA1508

        // Native x86 TLS slots pointer at fs:[0x2C]; GTF_ICON_TLS_HDL
        // communicates the segment-relative access to code generation.
        GenTree tlsReference = gtNewIconHandleNode(0x2C, GTF_ICON_TLS_HDL);
        tlsReference = gtNewIndir(TYP_I_IMPL, tlsReference, GTF_IND_NONFAULTING | GTF_IND_INVARIANT);
        if (dllReference is not null)
        {
            tlsReference = gtNewBinaryNode(GT_ADD, TYP_I_IMPL, tlsReference, dllReference);
        }
        tlsReference = gtNewIndir(TYP_I_IMPL, tlsReference);

        assert(!field.MayOverlap);
        var fields = FieldSeqStore.Create(fieldHandle, field.FldOffset, FieldSeq.FieldKind.SimpleStatic);
        var offset = gtNewIconNode(field.FldOffset, fields);

        // Match ChangeOper's flags and VN clearing. Recursive morphing will
        // propagate the new children's effects afterwards.
        return new GenTreeOp(GT_ADD, tree.Type, tlsReference, offset, tree, NodeThreading.None) {
            Flags = tree.Flags & GTF_COMMON_MASK,
            _vnPair = new ValueNumPair(),
        };
    }
}
