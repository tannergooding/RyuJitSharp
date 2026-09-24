// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Compiler
{
#if DEBUG
    private static uint genTreeHashAdd(uint old, uint add) => unchecked((old + (old / 2)) ^ add);

    public unsafe uint gtHashValue(GenTree tree)
    {
        // Native hashes a layout's address. Use stable reference identity rather
        // than a movable managed address; the hash is only used to detect changes.
        static uint LayoutHash(ClassLayout? layout)
        {
            return layout is null ? 0 : unchecked((uint)RuntimeHelpers.GetHashCode(layout));
        }

        static nuint HashBits(ulong bits)
        {
            return IntPtr.Size == 8 ? unchecked((nuint)bits) : genTreeHashAdd((uint)(bits >> 32), unchecked((uint)bits));
        }

        unchecked
        {
            uint hash = 0;
            while (true)
            {
                var oper = tree.Oper;
                hash = genTreeHashAdd(hash, (uint)oper);
                if (oper.IsLeaf)
                {
                    nuint add = 0;
                    switch (oper)
                    {
                        case GT_LCL_VAR:
                        {
                            add = (nuint)tree.AsLclVar().LclNum;
                            break;
                        }

                        case GT_LCL_FLD:
                        {
                            var field = tree.AsLclFld();
                            hash = genTreeHashAdd(hash, (uint)field.LclNum);
                            hash = genTreeHashAdd(hash, LayoutHash(field.Layout));
                            add = field.LclOffs;
                            break;
                        }

                        case GT_CNS_INT:
                        {
                            add = (nuint)tree.AsIntCon().IconValue;
                            break;
                        }

                        case GT_CNS_LNG:
                        {
                            add = HashBits((ulong)tree.AsLngCon().LconValue);
                            break;
                        }

                        case GT_CNS_DBL:
                        {
                            add = HashBits((ulong)BitConverter.DoubleToInt64Bits(tree.AsDblCon().DconVal));
                            break;
                        }

                        case GT_CNS_STR:
                        {
                            add = (nuint)tree.AsStrCon().SconCpx;
                            break;
                        }

#if FEATURE_SIMD
                        case GT_CNS_VEC:
                        {
#if TARGET_ARM64
                            if (tree.Type is TYP_SIMD)
                            {
                                throw new NotImplementedException("Scalable-vector constant hashing requires scalable storage.");
                            }
#endif
                            var vector = tree.AsVecCon();
                            for (var index = tree.Type.Size / sizeof(uint); index > 0; index--)
                            {
                                add = genTreeHashAdd((uint)add, vector.SimdVal.u32[index - 1]);
                            }
                            break;
                        }
#endif
#if FEATURE_MASKED_HW_INTRINSICS
                        case GT_CNS_MSK:
                        {
                            var mask = tree.AsMskCon();
                            add = genTreeHashAdd(0, mask.SimdMaskVal.u32[1]);
                            add = genTreeHashAdd((uint)add, mask.SimdMaskVal.u32[0]);
                            break;
                        }
#endif
                        case GT_JMP:
                        {
                            add = (nuint)tree.AsVal().Val1;
                            break;
                        }
                    }

                    var value = IntPtr.Size == 8 ? genTreeHashAdd((uint)((ulong)add >> 32), (uint)add) : (uint)add;
                    return genTreeHashAdd(hash, value);
                }

                if (oper.IsUnary || oper.IsBinary)
                {
                    if (oper.IsExOp)
                    {
                        switch (oper)
                        {
                            case GT_STORE_LCL_VAR:
                            {
                                hash = genTreeHashAdd(hash, (uint)tree.AsLclVar().LclNum);
                                break;
                            }

                            case GT_STORE_LCL_FLD:
                            {
                                var field = tree.AsLclFld();
                                hash = genTreeHashAdd(hash, (uint)field.LclNum);
                                hash = genTreeHashAdd(hash, field.LclOffs);
                                hash = genTreeHashAdd(hash, LayoutHash(field.Layout));
                                break;
                            }

                            case GT_STOREIND:
                            {
                                hash = genTreeHashAdd(hash, (uint)tree.AsStoreInd().RmwStatus);
                                break;
                            }

                            case GT_ARR_LENGTH:
                            {
                                hash += (uint)tree.AsArrLen().ArrLenOffset;
                                break;
                            }

                            case GT_MDARR_LENGTH:
                            case GT_MDARR_LOWER_BOUND:
                            {
                                hash += (uint)tree.AsMDArr().Dim;
                                hash += (uint)tree.AsMDArr().Rank;
                                break;
                            }

                            case GT_CAST:
                            {
                                hash ^= (uint)tree.AsCast().CastType;
                                break;
                            }

                            case GT_ALLOCOBJ:
                            {
                                hash = genTreeHashAdd(hash, (uint)(nuint)tree.AsAllocObj().ClsHnd);
                                hash = genTreeHashAdd(hash, (uint)tree.AsAllocObj().NewHelper);
                                break;
                            }

                            case GT_RUNTIMELOOKUP:
                            {
                                hash = genTreeHashAdd(hash, (uint)(nuint)tree.AsRuntimeLookup().Handle);
                                break;
                            }

                            case GT_BLK:
                            case GT_STORE_BLK:
                            {
                                hash = genTreeHashAdd(hash, LayoutHash(tree.AsBlk().Layout));
                                break;
                            }

                            case GT_FIELD_ADDR:
                            {
                                hash = genTreeHashAdd(hash, (uint)(nuint)tree.AsFieldAddr().FldHnd);
                                break;
                            }

                            case GT_BOX:
                            case GT_QMARK:
                            {
                                break;
                            }

                            case GT_ARR_ADDR:
                            {
                                var address = tree.AsArrAddr();
                                hash = genTreeHashAdd(hash, (uint)address.ElemType);
                                hash = genTreeHashAdd(hash, (uint)(nuint)address.ElemClassHandle);
                                hash = genTreeHashAdd(hash, address.FirstElemOffset);
                                break;
                            }

                            case GT_INTRINSIC:
                            {
                                hash += (uint)tree.AsIntrinsic().IntrinsicName;
                                break;
                            }

                            case GT_LEA:
                            {
                                var address = tree.AsAddrMode();
                                hash += (uint)(address.Offset << 3) + address.Scale;
                                break;
                            }

                            case GT_BOUNDS_CHECK:
                            {
                                hash = genTreeHashAdd(hash, (uint)tree.AsBoundsChk().ThrowKind);
                                break;
                            }

                            case GT_INDEX_ADDR:
                            {
                                var address = tree.AsIndexAddr();
                                hash = genTreeHashAdd(hash, (uint)address.ElemSize);
                                hash = genTreeHashAdd(hash, (uint)address.ElemType);
                                hash = genTreeHashAdd(hash, (uint)(nuint)address.StructElemClass);
                                hash = genTreeHashAdd(hash, (uint)address.LenOffset);
                                hash = genTreeHashAdd(hash, (uint)address.ElemOffset);
                                hash = genTreeHashAdd(hash, (uint)(tree.Flags & (GTF_INX_RNGCHK | GTF_INX_ADDR_NONNULL)));
                                break;
                            }

#if FEATURE_HW_INTRINSICS
                            case GT_HWINTRINSIC:
                            {
                                var intrinsic = tree.AsHWIntrinsic();
                                hash += (uint)intrinsic.HWIntrinsicId;
                                hash += (uint)intrinsic.SimdBaseType;
                                hash += (uint)intrinsic.SimdSize;
                                hash += (uint)intrinsic.AuxiliaryType;
                                hash += (uint)intrinsic.GetRegByIndex(1);
                                break;
                            }
#endif
                            default:
                            {
                                assert(false, "unexpected ExOp operator");
                                break;
                            }
                        }
                    }

                    var first = tree.AsUnOp().Op1;
                    if (oper.IsBinary && (tree.AsOp().Op2 is GenTree second))
                    {
                        hash = genTreeHashAdd(hash, gtHashValue(first));
                        tree = second;
                    }
                    else if (first is not null)
                    {
                        tree = first;
                    }
                    else
                    {
                        return hash;
                    }
                    continue;
                }

                switch (oper)
                {
                    case GT_ARR_ELEM:
                    {
                        var array = tree.AsArrElem();
                        hash = genTreeHashAdd(hash, gtHashValue(array.ArrObj));
                        hash = genTreeHashAdd(hash, (uint)array.ArrRank);
                        hash = genTreeHashAdd(hash, (uint)array.ArrElemSize);
                        foreach (var index in array.ArrInds)
                        {
                            hash = genTreeHashAdd(hash, gtHashValue(index));
                        }
                        break;
                    }

                    case GT_CALL:
                    {
                        var call = tree.AsCall();
                        foreach (var argument in call.Args.Args)
                        {
                            if (argument.EarlyNode is GenTree early)
                            {
                                hash = genTreeHashAdd(hash, gtHashValue(early));
                            }
                            if (argument.LateNode is GenTree late)
                            {
                                hash = genTreeHashAdd(hash, gtHashValue(late));
                            }
                        }
                        if (call._callType is CT_INDIRECT)
                        {
                            assert(call.ControlExpr is not null);
                            hash = genTreeHashAdd(hash, gtHashValue(call.ControlExpr));
                        }
                        else
                        {
                            hash = genTreeHashAdd(hash, (uint)(nuint)call._callMethHnd);
                        }
                        break;
                    }

#if FEATURE_HW_INTRINSICS
                    case GT_HWINTRINSIC:
                    {
                        foreach (var operand in tree.AsHWIntrinsic().Operands)
                        {
                            hash = genTreeHashAdd(hash, gtHashValue(operand));
                        }
                        break;
                    }
#endif
                    case GT_PHI:
                    {
                        foreach (var use in tree.AsPhi().Uses)
                        {
                            hash = genTreeHashAdd(hash, gtHashValue(use.Node));
                        }
                        break;
                    }

                    case GT_FIELD_LIST:
                    {
                        foreach (var use in tree.AsFieldList().Uses)
                        {
                            hash = genTreeHashAdd(hash, gtHashValue(use.Node));
                        }
                        break;
                    }

                    case GT_CMPXCHG:
                    {
                        var exchange = tree.AsCmpXchg();
                        hash = genTreeHashAdd(hash, gtHashValue(exchange.Addr));
                        hash = genTreeHashAdd(hash, gtHashValue(exchange.Data));
                        hash = genTreeHashAdd(hash, gtHashValue(exchange.Comparand));
                        break;
                    }

                    default:
                    {
                        gtDispTree(tree);
                        assert(false, "unexpected operator");
                        break;
                    }
                }
                return hash;
            }
        }
    }
#endif
}
