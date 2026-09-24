// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Compiler
{
    public void fgValueNumberTreeConst(GenTree tree)
    {
        assert(tree.Oper.IsConst);
        assert(vnStore is not null);
        var type = tree.Type;
        switch (type)
        {
            case TYP_LONG:
            case TYP_ULONG:
            case TYP_INT:
            case TYP_UINT:
            case TYP_USHORT:
            case TYP_SHORT:
            case TYP_BYTE:
            case TYP_UBYTE:
            {
                if (tree.IsIconHandle())
                {
                    var constant = tree.AsIntCon();
                    var flags = constant.IconHandleFlag;
                    tree._vnPair.SetBoth(vnStore.VNForHandle(constant.IconValue, flags));
                    if ((flags == GTF_ICON_CLASS_HDL) && (constant.CompileTimeHandle != 0))
                    {
                        // Re-flagged constants can have unknown compile-time handles; do not poison a valid mapping.
                        vnStore.AddToEmbeddedHandleMap(constant.IconValue, constant.CompileTimeHandle);
                    }
                }
                else if (type is TYP_LONG or TYP_ULONG)
                {
                    tree._vnPair.SetBoth(vnStore.VNForLongCon(tree.AsIntConCommon().IntegralValue));
                }
                else
                {
                    tree._vnPair.SetBoth(vnStore.VNForIntCon(unchecked((int)tree.AsIntConCommon().IconValue)));
                }

                if (tree.Oper.IsCnsIntOrI)
                {
                    fgValueNumberRegisterConstFieldSeq(tree.AsIntCon());
                }

                break;
            }

#if FEATURE_SIMD
            case TYP_SIMD8:
            case TYP_SIMD12:
            case TYP_SIMD16:
#if TARGET_XARCH
            case TYP_SIMD32:
            case TYP_SIMD64:
#endif
            {
                tree._vnPair.SetBoth(vnStore.VNForGenericCon(type, tree.AsVecCon().SimdVal.AsSpan<byte>()[..type.Size]));
                break;
            }

#if TARGET_ARM64
            case TYP_SIMD:
            {
                throw new NotImplementedException("Scalable VN constant storage is not yet ported.");
            }
#endif
#if FEATURE_MASKED_HW_INTRINSICS
            case TYP_MASK:
            {
                tree._vnPair.SetBoth(vnStore.VNForSimdMaskCon(tree.AsMskCon().SimdMaskVal));
                break;
            }
#endif
#endif
            case TYP_FLOAT:
            {
                tree._vnPair.SetBoth(vnStore.VNForFloatCon((float)tree.AsDblCon().DconVal));
                break;
            }

            case TYP_DOUBLE:
            {
                tree._vnPair.SetBoth(vnStore.VNForDoubleCon(tree.AsDblCon().DconVal));
                break;
            }

            case TYP_REF:
            {
                if (tree.AsIntConCommon().IconValue == 0)
                {
                    tree._vnPair.SetBoth(ValueNumStore.VNForNull());
                }
                else
                {
                    tree._vnPair.SetBoth(vnStore.VNForHandle(tree.AsIntConCommon().IconValue, tree.AsIntCon().IconHandleFlag));
                    fgValueNumberRegisterConstFieldSeq(tree.AsIntCon());
                }

                break;
            }

            case TYP_BYREF:
            {
                if (tree.AsIntConCommon().IconValue == 0)
                {
                    tree._vnPair.SetBoth(ValueNumStore.VNForNull());
                }
                else
                {
                    assert(tree.Oper.IsCnsIntOrI);
                    if (tree.IsIconHandle())
                    {
                        tree._vnPair.SetBoth(vnStore.VNForHandle(tree.AsIntConCommon().IconValue, tree.AsIntCon().IconHandleFlag));
                        fgValueNumberRegisterConstFieldSeq(tree.AsIntCon());
                    }
                    else
                    {
                        tree._vnPair.SetBoth(vnStore.VNForByrefCon(unchecked((nuint)tree.AsIntConCommon().IconValue)));
                    }
                }

                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
    }

    public void fgValueNumberRegisterConstFieldSeq(GenTreeIntCon tree)
    {
        if ((tree.FieldSeq is FieldSeq fieldSeq) && (fieldSeq.Kind == FieldSeq.FieldKind.SimpleStaticKnownAddress))
        {
            assert(vnStore is not null);
            vnStore.AddToFieldAddressToFieldSeqMap(tree._vnPair.Liberal, fieldSeq);
        }
    }
}
