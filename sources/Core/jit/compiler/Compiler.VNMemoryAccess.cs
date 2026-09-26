// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.

using System;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public partial class Compiler
{
    private readonly struct VNLocalStoreVisitor : ILocalDefVisitor
    {
        private readonly Compiler _compiler;
        private readonly GenTree _store;
        private readonly ValueNumPair _value;
        private readonly bool _normalize;

        public VNLocalStoreVisitor(Compiler compiler, GenTree store, ValueNumPair value, bool normalize)
        {
            _compiler = compiler;
            _store = store;
            _value = value;
            _normalize = normalize;
        }

        public GenTree.VisitResult Visit<TDef>(TDef def) where TDef : struct, ILocalDef
        {
            _compiler.fgValueNumberLocalStore(_store, def, _value, _normalize);
            return GenTree.VisitResult.Continue;
        }
    }

    public void fgValueNumberStore(GenTree store)
    {
        assert(store.Oper.IsStore);
        ArgumentNullException.ThrowIfNull(vnStore);

        var data = store.Data;
        vnStore.VNPUnpackExc(data._vnPair, out var value, out var valueExcSet);
        assert(value.BothDefined());

        if (data.Type != store.Type)
        {
            if (store.IsInitBlkOp)
            {
                var initVN = data.IsIntegralConst(0) ? vnStore.VNForZeroObj(store.GetLayout(this))
                    : vnStore.VNForExpr(compCurBB, TYP_STRUCT);
                value.SetBoth(initVN);
            }
            else if (varTypeIsGC(data.Type))
            {
                value.SetBoth(vnStore.VNForExpr(compCurBB, store.Type));
            }
            else
            {
                value = vnStore.VNPairForCast(value, store.Type, data.Type);
            }
        }

        switch (store.Oper)
        {
            case GT_STORE_LCL_VAR:
            case GT_STORE_LCL_FLD:
            {
                var visitor = new VNLocalStoreVisitor(this, store, value, store.Oper is GT_STORE_LCL_FLD);
                _ = store.VisitLogicalLocalDefs(this, ref visitor);
                break;
            }

            case GT_STOREIND:
            case GT_STORE_BLK:
            {
                if (store.AsIndir().IsVolatile)
                {
                    fgMutateGcHeap(store, "GTF_IND_VOLATILE - store");
                }

                var addr = store.AsIndir().Addr;
                VNFuncApp funcApp = default;
                var addrIsVNFunc = vnStore.GetVNFunc(vnStore.VNNormalValue(addr._vnPair.Liberal), ref funcApp);
                var storeSize = store.AsIndir().ValueSize;

                if (addrIsVNFunc && funcApp.FuncIs(VNF_PtrToStatic))
                {
                    var fieldSeq = vnStore.FieldSeqVNToFieldSeq(funcApp.GetArg(1));
                    assert(fieldSeq is not null);
                    fgValueNumberFieldStore(store, null, fieldSeq, vnStore.ConstantValue<nint>(funcApp.GetArg(2)),
                        storeSize, value.Liberal);
                }
                else if (addrIsVNFunc && funcApp.FuncIs(VNF_PtrToArrElem))
                {
                    fgValueNumberArrayElemStore(store, funcApp, storeSize, value.Liberal);
                }
                else if (addr.IsFieldAddr(this, out var baseAddr, out var fieldSeq, out var offset))
                {
                    assert(fieldSeq is not null);
                    fgValueNumberFieldStore(store, baseAddr, fieldSeq, offset, storeSize, value.Liberal);
                }
                else
                {
                    assert(!store.HasAnyLocalDefs(this));
                    fgMutateGcHeap(store, "assign-of-IND");
                }
                break;
            }

            default:
                throw new InvalidOperationException($"Unexpected store operator {store.Oper}");
        }

        var storeExcSet = store.Oper.IsIndir
            ? vnStore.VNPUnionExcSet(store.AsIndir().Addr._vnPair, valueExcSet) : valueExcSet;
        store._vnPair = vnStore.VNPWithExc(ValueNumStore.VNPForVoid(), storeExcSet);
    }

    private static unsafe var_types DecodeElemType(CORINFO_CLASS_HANDLE clsHnd)
    {
        var value = (nuint)clsHnd;
        return (value & 1) != 0 ? (var_types)(value >> 1) : TYP_STRUCT;
    }

    public unsafe void fgValueNumberArrayElemLoad(GenTree loadTree, VNFuncApp addrFunc)
    {
        assert(loadTree.Oper.IsIndir && addrFunc.FuncIs(VNF_PtrToArrElem));
        ArgumentNullException.ThrowIfNull(vnStore);

        var elemTypeEq = (CORINFO_CLASS_HANDLE)vnStore.ConstantValue<nint>(addrFunc.GetArg(0));
        var arrVN = addrFunc.GetArg(1);
        var inxVN = addrFunc.GetArg(2);
        var offset = vnStore.ConstantValue<nint>(addrFunc.GetArg(3));
        assert(arrVN == vnStore.VNNormalValue(arrVN));
        assert(inxVN == vnStore.VNNormalValue(inxVN));

        var elemType = DecodeElemType(elemTypeEq);
        var elemTypeEqVN = vnStore.VNForHandle((nint)elemTypeEq, GTF_ICON_CLASS_HDL);
#if DEBUG
        JITDUMP($"  Array element load: elemTypeEq is ${elemTypeEqVN:x} for " +
            $"{(elemType is TYP_STRUCT ? eeGetClassName(elemTypeEq) : elemType.Name)}[]\n");
#endif
        var atType = vnStore.VNForMapSelect(VNK_Liberal, TYP_MEM, fgCurMemoryVN[(int)GcHeap], elemTypeEqVN);
#if DEBUG
        JITDUMP($"  GcHeap[elemTypeEq: ${elemTypeEqVN:x}] is ${atType:x}\n");
#endif
        var atArray = vnStore.VNForMapSelect(VNK_Liberal, TYP_MEM, atType, arrVN);
#if DEBUG
        JITDUMP($"  GcHeap[elemTypeEq][array: ${arrVN:x}] is ${atArray:x}\n");
#endif
        var wholeElem = vnStore.VNForMapSelect(VNK_Liberal, elemType, atArray, inxVN);
#if DEBUG
        JITDUMP($"  GcHeap[elemTypeEq][array][index: ${inxVN:x}] is ${wholeElem:x}\n");
#endif
        var elemSize = elemType is TYP_STRUCT ? new ValueSize(info.compCompHnd->getClassSize(elemTypeEq))
            : ValueSize.FromJitType(elemType);
        var loadType = loadTree.Type;
        var loadSize = loadTree.AsIndir().ValueSize;
        var loadVN = vnStore.VNForLoad(VNK_Liberal, wholeElem, elemSize, loadType, offset, loadSize);
        loadTree._vnPair.Liberal = loadVN;

        VNFuncApp arrFn = default;
        loadTree._vnPair.Conservative = vnStore.IsVNNewLocalArr(arrVN, ref arrFn) ? loadVN
            : vnStore.VNForExpr(compCurBB, loadType);
    }

    public unsafe void fgValueNumberArrayElemStore(GenTree storeNode, VNFuncApp addrFunc, ValueSize storeSize, ValueNum value)
    {
        assert(addrFunc.FuncIs(VNF_PtrToArrElem));
        ArgumentNullException.ThrowIfNull(vnStore);

        var elemTypeEq = (CORINFO_CLASS_HANDLE)vnStore.ConstantValue<nint>(addrFunc.GetArg(0));
        var arrVN = addrFunc.GetArg(1);
        var inxVN = addrFunc.GetArg(2);
        var offset = vnStore.ConstantValue<nint>(addrFunc.GetArg(3));
        var elemType = DecodeElemType(elemTypeEq);
        var elemTypeEqVN = vnStore.VNForHandle((nint)elemTypeEq, GTF_ICON_CLASS_HDL);
#if DEBUG
        JITDUMP($"  Array element store: elemTypeEq is ${elemTypeEqVN:x} for " +
            $"{(elemType is TYP_STRUCT ? eeGetClassName(elemTypeEq) : elemType.Name)}[]\n");
#endif
        var atType = vnStore.VNForMapSelect(VNK_Liberal, TYP_MEM, fgCurMemoryVN[(int)GcHeap], elemTypeEqVN);
#if DEBUG
        JITDUMP($"  GcHeap[elemTypeEq: ${elemTypeEqVN:x}] is ${atType:x}\n");
#endif
        var atArray = vnStore.VNForMapSelect(VNK_Liberal, TYP_MEM, atType, arrVN);
#if DEBUG
        JITDUMP($"  GcHeap[elemTypeEq][array: ${arrVN:x}] is ${atArray:x}\n");
#endif
        var elemSize = elemType is TYP_STRUCT ? new ValueSize(info.compCompHnd->getClassSize(elemTypeEq))
            : ValueSize.FromJitType(elemType);

        ValueNum newWholeElem;
        if (ValueNumStore.LoadStoreIsEntire(elemSize, offset, storeSize))
        {
            newWholeElem = value;
        }
        else
        {
            var oldWholeElem = vnStore.VNForMapSelect(VNK_Liberal, elemType, atArray, inxVN);
#if DEBUG
            JITDUMP($"  GcHeap[elemTypeEq][array][index: ${inxVN:x}] is ${oldWholeElem:x}\n");
#endif
            newWholeElem = vnStore.VNForStore(oldWholeElem, elemSize, offset, storeSize, value);
        }

        if (newWholeElem != ValueNumStore.NoVN)
        {
#if DEBUG
            JITDUMP($"  GcHeap[elemTypeEq][array][index: ${inxVN:x}] = ${newWholeElem:x}:\n");
#endif
            var newAtArray = vnStore.VNForMapStore(atArray, inxVN, newWholeElem);
#if DEBUG
            JITDUMP($"  GcHeap[elemTypeEq][array: ${arrVN:x}] = ${newAtArray:x}:\n");
#endif
            var newAtType = vnStore.VNForMapStore(atType, arrVN, newAtArray);
#if DEBUG
            JITDUMP($"  GcHeap[elemTypeEq: ${elemTypeEqVN:x}] = ${newAtType:x}:\n");
#endif
            var newHeap = vnStore.VNForMapStore(fgCurMemoryVN[(int)GcHeap], elemTypeEqVN, newAtType);
            recordGcHeapStore(storeNode, newHeap, "array element store");
        }
        else
        {
            fgMutateGcHeap(storeNode, "out-of-bounds array element store");
        }
    }

    public unsafe void fgValueNumberFieldLoad(GenTree loadTree, GenTree? baseAddr, FieldSeq fieldSeq, nint offset)
    {
        ArgumentNullException.ThrowIfNull(vnStore);

        var fieldSelector = vnStore.VNForFieldSelector(fieldSeq.FieldHandle, out var fieldType, out var fieldSize);
        ValueNum fieldMap;
        ValueNum fieldValueSelector;
        if (baseAddr is not null)
        {
            fieldMap = vnStore.VNForMapSelect(VNK_Liberal, TYP_MEM, fgCurMemoryVN[(int)GcHeap], fieldSelector);
            fieldValueSelector = vnStore.VNNormalValue(baseAddr._vnPair.Liberal);
        }
        else
        {
            fieldMap = fgCurMemoryVN[(int)GcHeap];
            fieldValueSelector = fieldSelector;
        }

        var fieldValue = vnStore.VNForMapSelect(VNK_Liberal, fieldType, fieldMap, fieldValueSelector);
        var loadType = loadTree.Type;
        var loadSize = loadTree.Oper.IsBlk ? new ValueSize(loadTree.AsBlk().Size)
            : ValueSize.FromJitType(loadType);
        loadTree._vnPair.Liberal = vnStore.VNForLoad(VNK_Liberal, fieldValue, fieldSize, loadType, offset, loadSize);
        loadTree._vnPair.Conservative = vnStore.VNForExpr(compCurBB, loadType);
    }

    public unsafe void fgValueNumberFieldStore(GenTree storeNode, GenTree? baseAddr, FieldSeq fieldSeq, nint offset,
        ValueSize storeSize, ValueNum value)
    {
        ArgumentNullException.ThrowIfNull(vnStore);

        var fieldSelector = vnStore.VNForFieldSelector(fieldSeq.FieldHandle, out var fieldType, out var fieldSize);
        ValueNum fieldMap;
        ValueNum fieldValueSelector;
        if (baseAddr is not null)
        {
            fieldMap = vnStore.VNForMapSelect(VNK_Liberal, TYP_MEM, fgCurMemoryVN[(int)GcHeap], fieldSelector);
            fieldValueSelector = vnStore.VNNormalValue(baseAddr._vnPair.Liberal);
        }
        else
        {
            fieldMap = fgCurMemoryVN[(int)GcHeap];
            fieldValueSelector = fieldSelector;
        }

        ValueNum newFieldValue;
        if (ValueNumStore.LoadStoreIsEntire(fieldSize, offset, storeSize))
        {
            newFieldValue = value;
        }
        else
        {
            var oldFieldValue = vnStore.VNForMapSelect(VNK_Liberal, fieldType, fieldMap, fieldValueSelector);
            newFieldValue = vnStore.VNForStore(oldFieldValue, fieldSize, offset, storeSize, value);
        }

        if (newFieldValue != ValueNumStore.NoVN)
        {
            var newFieldMap = vnStore.VNForMapStore(fieldMap, fieldValueSelector, newFieldValue);
            var newHeap = baseAddr is not null
                ? vnStore.VNForMapStore(fgCurMemoryVN[(int)GcHeap], fieldSelector, newFieldMap) : newFieldMap;
            recordGcHeapStore(storeNode, newHeap, "StoreField");
        }
        else
        {
            fgMutateGcHeap(storeNode, "out-of-bounds store to a field");
        }
    }

    public void fgValueNumberLocalStore<TDef>(GenTree storeNode, in TDef def, ValueNumPair value,
        bool normalize) where TDef : struct, ILocalDef
    {
        assert(vnStore is not null);
        ArgumentNullException.ThrowIfNull(vnStore);
        assert(!GetMemorySsaMap(GcHeap).ContainsKey(storeNode));

        var defNode = def.DefNode;
        var defLclNum = def.LclNum;
        ref var defVarDsc = ref lvaGetDesc(defLclNum);
        var defValue = value;
        if (def.HasMultiDefIndex())
        {
            var defValueType = def.IsEntire(this) ? defVarDsc.Type : TYP_STRUCT;
            defValue = vnStore.VNPairForLoad(value, def.GetStoreSize(this),
                defValueType, def.GetValueOffset(this), def.GetSize(this));
        }

        var defSsaNum = def.GetSsaNum(this);
        if (defSsaNum != SsaConfig.RESERVED_SSA_NUM)
        {
            var lclSize = defVarDsc.lvValueSize;
            ValueNumPair newLclValue;
            if (def.IsEntire(this))
            {
                newLclValue = defValue;
            }
            else
            {
                var defSize = def.GetSize(this);
                if (defSize.IsUnknown)
                {
#if DEBUG
                    JITDUMP($"Tree [{storeNode.TreeId:D6}] performs store to variable-sized local\n");
#endif
                    newLclValue = vnStore.VNPairForExpr(compCurBB, defVarDsc.Type);
                }
                else
                {
                    assert((defNode.Flags & GTF_VAR_USEASG) != 0);
                    var oldDefSsaNum = defVarDsc.GetPerSsaData(defSsaNum).UseDefSsaNum;
                    var oldLclValue = defVarDsc.GetPerSsaData(oldDefSsaNum)._vnPair;
                    assert(oldLclValue.BothDefined() && defValue.BothDefined());
                    newLclValue = vnStore.VNPairForStore(oldLclValue, lclSize, def.GetOffset(this),
                        defSize, defValue);
                }
            }

            assert(newLclValue.BothDefined());
            if (normalize)
            {
                newLclValue = vnStore.VNPairForLoadStoreBitCast(newLclValue, defVarDsc.Type, lclSize);
                assert(vnStore.TypeOfVN(newLclValue.Liberal).ActualType == defVarDsc.Type.ActualType);
            }

            defVarDsc.GetPerSsaData(defSsaNum)._vnPair = newLclValue;
#if DEBUG
            JITDUMP($"Tree [{storeNode.TreeId:D6}] assigned VN to local var V{defLclNum:D2}/{defSsaNum}: ");
            if (verbose)
            {
                vnpPrint(newLclValue, 1);
            }
            JITDUMP("\n");
#endif
        }
        else if (defVarDsc.IsAddressExposed)
        {
            var heapVN = vnStore.VNForExpr(compCurBB, TYP_HEAP);
            recordAddressExposedLocalStore(storeNode, heapVN, "local assign");
        }
        else
        {
#if DEBUG
            JITDUMP($"Tree [{storeNode.TreeId:D6}] assigns to non-address-taken local V{defLclNum:D2}; " +
                "excluded from SSA, so value not tracked\n");
#endif
        }
    }

    public void fgValueNumberSsaVarDef(GenTreeLclVarCommon lcl)
    {
        assert(lcl.Oper is GT_LCL_VAR && lcl.HasSsaName);
        assert(vnStore is not null);

        ref var varDsc = ref lvaGetDesc(lcl.LclNum);
        var wholeLclVarVNP = varDsc.GetPerSsaData(lcl.SsaNum)._vnPair;
        assert(wholeLclVarVNP.BothDefined());

        if (varDsc.Type.ActualType != lcl.Type.ActualType)
        {
            if (varDsc.Type.Size != lcl.Type.Size)
            {
                assert(varDsc.Type is TYP_LONG && lcl.Type is TYP_INT);
                lcl._vnPair = vnStore.VNPairForCast(wholeLclVarVNP, lcl.Type, varDsc.Type);
            }
            else
            {
                assert((varDsc.Type is TYP_I_IMPL && lcl.Type is TYP_BYREF) ||
                       (varDsc.Type is TYP_BYREF && lcl.Type is TYP_I_IMPL));
                lcl._vnPair = wholeLclVarVNP;
            }
        }
        else
        {
            lcl._vnPair = wholeLclVarVNP;
        }
    }

    public ValueNum fgValueNumberByrefExposedLoad(var_types type, ValueNum pointerVN)
    {
        assert(vnStore is not null);

        if (type is TYP_STRUCT)
        {
            return vnStore.VNForExpr(compCurBB, TYP_STRUCT);
        }

        var memoryVN = fgCurMemoryVN[(int)ByrefExposed];
        var typeVN = vnStore.VNForIntCon((int)type);
        return vnStore.VNForFunc(type, VNF_ByrefExposedLoad, typeVN,
            vnStore.VNNormalValue(pointerVN), memoryVN);
    }

    public static bool fgGetStaticFieldSeqAndAddress(ValueNumStore store, GenTree tree,
        out nint byteOffset, out FieldSeq? fieldSeq)
    {
        var app = new VNFuncApp();
        if (store.GetVNFunc(tree._vnPair.Liberal, ref app) && app.FuncIs(VNF_PtrToStatic))
        {
            var sequence = store.FieldSeqVNToFieldSeq(app.GetArg(1));
            if (sequence is not null && sequence.Kind is FieldSeq.FieldKind.SimpleStatic)
            {
                byteOffset = store.ConstantValue<nint>(app.GetArg(2));
                fieldSeq = sequence;
                return true;
            }
        }

        if (tree.Oper is GT_ADD && tree.AsOp().Op2.Oper.IsCnsIntOrI &&
            !tree.AsOp().Op2.IsIconHandle())
        {
            var constant = tree.AsOp().Op2.AsIntCon();
            if (constant.FieldSeq is FieldSeq sequence &&
                sequence.Kind is FieldSeq.FieldKind.SimpleStatic)
            {
                byteOffset = unchecked(constant.IconValue - sequence.Offset);
                fieldSeq = sequence;
                return true;
            }
        }

        nint value = 0;
        while (tree.Oper is GT_ADD)
        {
            var op1 = tree.AsOp().Op1;
            var op2 = tree.AsOp().Op2;
            var op1VN = op1._vnPair.Liberal;
            var op2VN = op2._vnPair.Liberal;

            if (op1._vnPair.BothEqual() && store.IsVNConstant(op1VN) &&
                !store.IsVNHandle(op1VN) && varTypeIsIntegral(store.TypeOfVN(op1VN)))
            {
                value = unchecked(value + store.CoercedConstantValue<nint>(op1VN));
                tree = op2;
            }
            else if (op2._vnPair.BothEqual() && store.IsVNConstant(op2VN) &&
                     !store.IsVNHandle(op2VN) && varTypeIsIntegral(store.TypeOfVN(op2VN)))
            {
                value = unchecked(value + store.CoercedConstantValue<nint>(op2VN));
                tree = op1;
            }
            else
            {
                byteOffset = 0;
                fieldSeq = null;
                return false;
            }
        }

        var treeVN = tree._vnPair.Liberal;
        if (tree._vnPair.BothEqual() && store.IsVNConstant(treeVN))
        {
            var sequence = store.GetFieldSeqFromAddress(treeVN);
            if (sequence is not null)
            {
                assert(sequence.Kind is FieldSeq.FieldKind.SimpleStaticKnownAddress);
                fieldSeq = sequence;
                byteOffset = unchecked(store.CoercedConstantValue<nint>(treeVN) - sequence.Offset + value);
                return true;
            }
        }

        byteOffset = 0;
        fieldSeq = null;
        return false;
    }

    public unsafe bool GetObjectHandleAndOffset(GenTree tree, out nint byteOffset,
        out CORINFO_OBJECT_HANDLE obj)
    {
        byteOffset = 0;
        obj = NO_OBJECT_HANDLE;
        if (!tree._vnPair.BothEqual())
        {
            return false;
        }

        var treeVN = tree._vnPair.Liberal;
        if (treeVN == ValueNumStore.NoVN)
        {
            return false;
        }

        assert(vnStore is not null);
        vnStore.PeelOffsets(ref treeVN, out var offset);
        if (vnStore.IsVNObjHandle(treeVN))
        {
            obj = (CORINFO_OBJECT_HANDLE)vnStore.ConstantValue<nint>(treeVN);
            byteOffset = unchecked((nint)offset);
            return true;
        }

        return false;
    }

    public unsafe bool GetImmutableDataFromAddress(GenTree address, int size, out byte[]? value)
    {
        assert(vnStore is not null);
        value = null;

        if (GetObjectHandleAndOffset(address, out var byteOffset, out var obj) &&
            unchecked((nuint)byteOffset) <= int.MaxValue)
        {
            assert(obj != NO_OBJECT_HANDLE);
            if (!info.compCompHnd->isObjectImmutable(obj))
            {
                return false;
            }

            value = new byte[size];
            fixed (byte* buffer = value)
            {
                return info.compCompHnd->getObjectContent(obj, buffer, size, (int)byteOffset);
            }
        }

        if (fgGetStaticFieldSeqAndAddress(vnStore, address, out byteOffset, out var fieldSeq) &&
            unchecked((nuint)byteOffset) <= int.MaxValue)
        {
            var field = fieldSeq!.FieldHandle;
            if (field == NO_FIELD_HANDLE)
            {
                return false;
            }

            value = new byte[size];
            fixed (byte* buffer = value)
            {
                return info.compCompHnd->getStaticFieldContent(field, buffer, size, (int)byteOffset);
            }
        }

        return false;
    }

    public unsafe bool fgValueNumberConstLoad(GenTreeIndir tree)
    {
        if (!tree._vnPair.BothEqual())
        {
            return false;
        }

        assert(vnStore is not null);
        var size = tree.Type.Size;
        var maxElementSize = sizeof(simd64_t);
        Span<byte> buffer = stackalloc byte[maxElementSize];

        if (tree.Type is not (TYP_BYREF or TYP_STRUCT) &&
            fgGetStaticFieldSeqAndAddress(vnStore, tree.Addr, out var byteOffset, out var fieldSeq))
        {
            var field = fieldSeq!.FieldHandle;
            if ((field != NO_FIELD_HANDLE) && (size > 0) && (size <= maxElementSize) &&
                (unchecked((nuint)byteOffset) < int.MaxValue))
            {
                buffer.Clear();
                fixed (byte* content = buffer)
                {
                    if (info.compCompHnd->getStaticFieldContent(field, content, size, (int)byteOffset))
                    {
                        tree._vnPair.SetBoth(vnStore.VNForGenericCon(tree.Type, buffer[..size]));
                        return true;
                    }
                }
            }
        }
        else if (tree.Type is not (TYP_REF or TYP_BYREF or TYP_STRUCT) &&
                 GetObjectHandleAndOffset(tree.Addr, out byteOffset, out var obj))
        {
            assert(obj != NO_OBJECT_HANDLE);
            if ((size > 0) && (size <= maxElementSize) &&
                (unchecked((nuint)byteOffset) < int.MaxValue))
            {
                buffer.Clear();
                fixed (byte* content = buffer)
                {
                    if (info.compCompHnd->getObjectContent(obj, content, size, (int)byteOffset))
                    {
                        if ((size == TARGET_POINTER_SIZE) && (byteOffset == 0))
                        {
                            var rawHandle = (CORINFO_CLASS_HANDLE)MemoryMarshal.Read<nint>(buffer);
                            void* pEmbedClsHnd;
                            var embedClsHnd = info.compCompHnd->embedClassHandle(rawHandle, &pEmbedClsHnd);
                            if (pEmbedClsHnd == null)
                            {
                                tree._vnPair.SetBoth(vnStore.VNForHandle((nint)embedClsHnd, GTF_ICON_CLASS_HDL));
                                return true;
                            }
                        }
                        else
                        {
                            var vn = vnStore.VNForGenericCon(tree.Type, buffer[..size]);
                            assert(!vnStore.IsVNObjHandle(vn));
                            tree._vnPair.SetBoth(vn);
                            return true;
                        }
                    }
                }
            }
        }

        if (tree.Oper is not GT_IND || tree.Type is not TYP_USHORT)
        {
            return false;
        }

        var addressVN = tree.Addr._vnPair.Liberal;
        var app = new VNFuncApp();
        if (!vnStore.GetVNFunc(addressVN, ref app))
        {
            return false;
        }

        var objHandle = NO_OBJECT_HANDLE;
        var index = nuint.MaxValue;
        if (app.FuncIs(VNF_PtrToArrElem))
        {
            var arrayVN = app.GetArg(1);
            var indexVN = app.GetArg(2);
            var offset = vnStore.ConstantValue<nint>(app.GetArg(3));
            if (vnStore.IsVNObjHandle(arrayVN) && (offset == 0) && vnStore.IsVNConstant(indexVN))
            {
                objHandle = (CORINFO_OBJECT_HANDLE)vnStore.ConstantValue<nint>(arrayVN);
                index = vnStore.CoercedConstantValue<nuint>(indexVN);
            }
        }
        else if (app.FuncIs(VNF_ADD))
        {
            vnStore.PeelOffsets(ref addressVN, out var dataOffset);
            if (vnStore.IsVNObjHandle(addressVN) &&
                (dataOffset >= OFFSETOF__CORINFO_String__chars) && ((dataOffset % 2) == 0))
            {
                objHandle = (CORINFO_OBJECT_HANDLE)vnStore.ConstantValue<nint>(addressVN);
                index = unchecked((nuint)((dataOffset - OFFSETOF__CORINFO_String__chars) / 2));
            }
        }

        var character = (ushort)0;
        if ((index < int.MaxValue) && (objHandle != NO_OBJECT_HANDLE) &&
            info.compCompHnd->getStringChar(objHandle, (int)index, &character))
        {
#if DEBUG
            JITDUMP($"Folding \"cns_str\"[{index}] into {character}");
#endif
            tree._vnPair.SetBoth(vnStore.VNForIntCon(character));
            return true;
        }

        return false;
    }
}
