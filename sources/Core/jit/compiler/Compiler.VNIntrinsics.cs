// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Compiler
{
    private static bool IsBitCountingIntrinsic(NamedIntrinsic intrinsic) => intrinsic is
        NI_PRIMITIVE_LeadingZeroCount or NI_PRIMITIVE_TrailingZeroCount or NI_PRIMITIVE_PopCount;

    public unsafe void fgValueNumberIntrinsic(GenTree tree)
    {
        assert(tree.Oper is GT_INTRINSIC);
        assert(vnStore is not null);
        var intrinsic = tree.AsIntrinsic();
        vnStore.VNPUnpackExc(intrinsic.Op1._vnPair, out var arg0, out var arg0Exceptions);
        var arg1 = default(ValueNumPair);
        var arg1Exceptions = ValueNumStore.VNPForEmptyExcSet();
        if (intrinsic.Op2 is not null)
        {
            vnStore.VNPUnpackExc(intrinsic.Op2._vnPair, out arg1, out arg1Exceptions);
        }

        var id = intrinsic.IntrinsicName;
        if (IsMathIntrinsic(id) || IsBitCountingIntrinsic(id))
        {
            if (intrinsic.Op2 is null)
            {
                tree._vnPair = vnStore.VNPWithExc(
                    vnStore.EvalMathFuncUnary(tree.Type, id, arg0), arg0Exceptions);
            }
            else
            {
                var value = vnStore.EvalMathFuncBinary(tree.Type, id, arg0, arg1);
                var exceptions = vnStore.VNPExcSetUnion(arg0Exceptions, arg1Exceptions);
                tree._vnPair = vnStore.VNPWithExc(value, exceptions);
            }
        }
        else if (id is NI_PRIMITIVE_SaturateToInt8 or NI_PRIMITIVE_SaturateToInt16 or
            NI_PRIMITIVE_SaturateToUInt8 or NI_PRIMITIVE_SaturateToUInt16)
        {
            assert(intrinsic.Op2 is null);
            var func = id switch {
                NI_PRIMITIVE_SaturateToInt8 => VNF_SaturateToInt8,
                NI_PRIMITIVE_SaturateToInt16 => VNF_SaturateToInt16,
                NI_PRIMITIVE_SaturateToUInt8 => VNF_SaturateToUInt8,
                NI_PRIMITIVE_SaturateToUInt16 => VNF_SaturateToUInt16,
                _ => throw new InvalidOperationException($"Unexpected saturating intrinsic: {id}"),
            };
            tree._vnPair = vnStore.VNPWithExc(
                vnStore.VNPairForFunc(tree.Type, func, arg0), arg0Exceptions);
        }
        else if (id is NI_PRIMITIVE_Log2)
        {
            tree._vnPair = vnStore.VNPUniqueWithExc(tree.Type, arg0Exceptions);
        }
        else
        {
            assert(id is NI_System_Object_GetType);
            var classHandle = gtGetClassHandle(intrinsic.Op1, out var isExact, out var isNonNull);
            if ((classHandle != NO_CLASS_HANDLE) && isExact)
            {
                var typeObject = info.compCompHnd->getRuntimeTypePointer(classHandle);
                if (typeObject != null)
                {
                    var handle = vnStore.VNForHandle((nint)typeObject, GTF_ICON_OBJ_HDL);
                    var exceptions = arg0Exceptions;
                    if (!isNonNull)
                    {
                        // An exact object type does not prove the receiver is non-null.
                        exceptions = vnStore.VNPExcSetUnion(exceptions,
                            fgValueNumberIndirNullCheckExceptions(intrinsic.Op1));
                    }
                    tree._vnPair = vnStore.VNPWithExc(new(handle, handle), exceptions);
                    return;
                }
            }

            tree._vnPair = vnStore.VNPWithExc(
                vnStore.VNPairForFunc(tree.Type, VNF_ObjGetType, arg0), arg0Exceptions);
        }
    }

    private static unsafe CORINFO_CLASS_HANDLE EncodeElemType(var_types elemType,
        CORINFO_CLASS_HANDLE elemStructType)
    {
        if (elemStructType != NO_CLASS_HANDLE)
        {
            assert(varTypeIsStruct(elemType) || elemType is TYP_REF or TYP_BYREF ||
                varTypeIsIntegral(elemType));
            assert((((nuint)elemStructType) & 1) == 0);
            return elemStructType;
        }

        assert(elemType is not TYP_STRUCT);
        return (CORINFO_CLASS_HANDLE)((((nuint)(int)varTypeToSigned(elemType)) << 1) | 1);
    }

    public unsafe void fgValueNumberArrIndexAddr(GenTreeArrAddr arrAddr)
    {
        assert(vnStore is not null);
        var indexVN = ValueNumStore.NoVN;
        arrAddr.ParseArrayAddress(this, out var array, ref indexVN);

        if (array is null)
        {
            JITDUMP("    *** ARR_ADDR -- an unparsable array expression, assigning a new, unique VN\n");
            arrAddr._vnPair = vnStore.VNPUniqueWithExc(TYP_BYREF,
                vnStore.VNPExceptionSet(arrAddr.Addr._vnPair));
            return;
        }

        var elemType = arrAddr.ElemType;
        var elemClass = arrAddr.ElemClassHandle;
        var encodedElemType = EncodeElemType(elemType, elemClass);
        var elemTypeVN = vnStore.VNForHandle((nint)encodedElemType, GTF_ICON_CLASS_HDL);
        JITDUMP($"    VNForHandle(arrElemType: {(elemType is TYP_STRUCT ? eeGetClassName(elemClass) : elemType.Name)}) is {elemTypeVN:x}\n");

        var arrayVN = vnStore.VNNormalValue(array._vnPair.Liberal);
        indexVN = vnStore.VNNormalValue(indexVN);
        var offsetVN = vnStore.VNForIntPtrCon(0);
        var addressVN = vnStore.VNForFunc(TYP_BYREF, VNF_PtrToArrElem,
            elemTypeVN, arrayVN, indexVN, offsetVN);
        arrAddr._vnPair = vnStore.VNPWithExc(new(addressVN, addressVN),
            vnStore.VNPExceptionSet(arrAddr.Addr._vnPair));
    }

#if FEATURE_HW_INTRINSICS
    public void fgValueNumberHWIntrinsic(GenTreeHWIntrinsic tree)
    {
#if !TARGET_XARCH
        throw new NotImplementedException("Hardware intrinsic value numbering is not ported for this target.");
#else
        assert(vnStore is not null);
        var id = tree.HWIntrinsicId;
        var isMemoryLoad = tree.IsMemoryLoad(out var address);
        var isMemoryStore = !isMemoryLoad && tree.IsMemoryStore(out address);

        if (isMemoryStore)
        {
            fgMutateGcHeap(tree, "HWIntrinsic - MemoryStore");
        }
        else if (HWIntrinsicInfo.HasSpecialSideEffect_Barrier(id))
        {
            fgMutateGcHeap(tree, "HWIntrinsic - Barrier");
        }

        var exceptions = ValueNumStore.VNPForEmptyExcSet();
        var normal = default(ValueNumPair);
        var count = tree.Operands.Length;
        if ((count > 3) || ((JitConfig.JitDisableSimdVN & 2) == 2) ||
            HWIntrinsicInfo.HasSpecialSideEffect(id))
        {
            normal = vnStore.VNPairForExpr(compCurBB, tree.Type);
            foreach (var operand in tree.Operands)
            {
                exceptions = vnStore.VNPUnionExcSet(operand._vnPair, exceptions);
            }
        }
        else
        {
            var func = GetVNFuncForNode(tree);
            var typeVN = vnStore.VNForSimdType(tree.SimdSize, tree.SimdBaseType);
            var typePair = new ValueNumPair(typeVN, typeVN);
#if DEBUG
            if (verbose)
            {
                jitprintf("    simdTypeVN is ");
                vnStore.vnDump(this, typeVN);
                jitprintf("\n");
            }
#endif

            if (count == 0)
            {
                normal = vnStore.VNPairForFunc(tree.Type, func, typePair);
                assert(ValueNumStore.VNFuncArity(func) == 1);
            }
            else
            {
                GetOperandVNs(tree.GetOp(1), out var op1, out var op1Exceptions);
                if (count == 1)
                {
                    normal = new(
                        vnStore.EvalHWIntrinsicFunUnary(tree, func, op1.Liberal, typePair.Liberal),
                        vnStore.EvalHWIntrinsicFunUnary(tree, func, op1.Conservative, typePair.Conservative));
                    exceptions = op1Exceptions;
                }
                else
                {
                    GetOperandVNs(tree.GetOp(2), out var op2, out var op2Exceptions);
                    if (count == 2)
                    {
                        normal = new(
                            vnStore.EvalHWIntrinsicFunBinary(tree, func, op1.Liberal, op2.Liberal, typePair.Liberal),
                            vnStore.EvalHWIntrinsicFunBinary(tree, func, op1.Conservative, op2.Conservative,
                                typePair.Conservative));
                        exceptions = vnStore.VNPExcSetUnion(op1Exceptions, op2Exceptions);
                    }
                    else
                    {
                        assert(count == 3);
                        GetOperandVNs(tree.GetOp(3), out var op3, out var op3Exceptions);
                        normal = new(
                            vnStore.EvalHWIntrinsicFunTernary(tree, func, op1.Liberal, op2.Liberal,
                                op3.Liberal, typePair.Liberal),
                            vnStore.EvalHWIntrinsicFunTernary(tree, func, op1.Conservative, op2.Conservative,
                                op3.Conservative, typePair.Conservative));
                        exceptions = vnStore.VNPExcSetUnion(op1Exceptions, op2Exceptions);
                        exceptions = vnStore.VNPExcSetUnion(exceptions, op3Exceptions);
                    }
                }
            }
        }

        tree._vnPair = tree.IsConvertMaskToVector
            ? vnStore.VNPUniqueWithExc(tree.Type, exceptions)
            : vnStore.VNPWithExc(normal, exceptions);

        if (isMemoryLoad || isMemoryStore)
        {
            switch (id)
            {
                case NI_X86Base_MaskMove:
                case NI_AVX_MaskStore:
                case NI_AVX2_MaskStore:
                case NI_AVX_MaskLoad:
                case NI_AVX2_MaskLoad:
                case NI_AVX2_GatherVector128:
                case NI_AVX2_GatherVector256:
                case NI_AVX2_GatherMaskVector128:
                case NI_AVX2_GatherMaskVector256:
                {
                    var uniqueAddress = vnStore.VNPairForExpr(compCurBB, TYP_BYREF);
                    var nullCheck = vnStore.VNPairForFunc(TYP_REF, VNF_NullPtrExc, uniqueAddress);
                    var nullSet = vnStore.VNPExcSetSingleton(nullCheck);
                    tree._vnPair = vnStore.VNPWithExc(tree._vnPair, nullSet);
                    break;
                }

                default:
                {
                    if (address is null)
                    {
                        throw new InvalidOperationException("Memory intrinsic has no address operand.");
                    }
                    fgValueNumberAddExceptionSetForIndirection(tree, address);
                    break;
                }
            }
        }

        void GetOperandVNs(GenTree operand, out ValueNumPair value, out ValueNumPair operandExceptions)
        {
            vnStore.VNPUnpackExc(operand._vnPair, out value, out operandExceptions);
            if (operand == address)
            {
                // Number the effective byref load rather than the address operand itself.
                var loadType = operand.Type;
                value.Liberal = fgValueNumberByrefExposedLoad(loadType, value.Liberal);
                value.Conservative = vnStore.VNForExpr(compCurBB, loadType);
            }
        }
#endif
    }
#endif

    public void fgValueNumberCastTree(GenTree tree)
    {
        assert(tree.Oper is GT_CAST);
        assert(vnStore is not null);
        var cast = tree.AsCast();
        assert(cast.CastType.ActualType == tree.Type.ActualType);
        tree._vnPair = vnStore.VNPairForCast(cast.CastOp._vnPair,
            cast.CastType, cast.CastOp.Type, cast.IsUnsigned, tree.HasOverflowCheckEx);
    }

    public void fgValueNumberBitCast(GenTree tree)
    {
        assert(tree.Oper is GT_BITCAST);
        assert(vnStore is not null);
        var source = tree.AsUnOp().Op1._vnPair;
        var castToType = tree.Type;
        vnStore.VNPUnpackExc(source, out var normal, out var exceptions);
        var result = vnStore.VNPairForBitCast(normal, castToType, ValueSize.FromJitType(castToType));
        tree._vnPair = vnStore.VNPWithExc(result, exceptions);
    }
}
