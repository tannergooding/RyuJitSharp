// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    private void compInitVarTypeCalleeTrashRegMasks()
    {
        varTypeCalleeTrashRegMasks[(int)(TYP_UNDEF)] = SRBM_INT_CALLEE_TRASH;
        varTypeCalleeTrashRegMasks[(int)(TYP_VOID)] = SRBM_INT_CALLEE_TRASH;
        varTypeCalleeTrashRegMasks[(int)(TYP_BYTE)] = SRBM_INT_CALLEE_TRASH;
        varTypeCalleeTrashRegMasks[(int)(TYP_UBYTE)] = SRBM_INT_CALLEE_TRASH;
        varTypeCalleeTrashRegMasks[(int)(TYP_SHORT)] = SRBM_INT_CALLEE_TRASH;
        varTypeCalleeTrashRegMasks[(int)(TYP_USHORT)] = SRBM_INT_CALLEE_TRASH;
        varTypeCalleeTrashRegMasks[(int)(TYP_INT)] = SRBM_INT_CALLEE_TRASH;
        varTypeCalleeTrashRegMasks[(int)(TYP_UINT)] = SRBM_INT_CALLEE_TRASH;
        varTypeCalleeTrashRegMasks[(int)(TYP_LONG)] = SRBM_INT_CALLEE_TRASH;
        varTypeCalleeTrashRegMasks[(int)(TYP_ULONG)] = SRBM_INT_CALLEE_TRASH;
        varTypeCalleeTrashRegMasks[(int)(TYP_FLOAT)] = SRBM_FLT_CALLEE_TRASH;
        varTypeCalleeTrashRegMasks[(int)(TYP_DOUBLE)] = SRBM_FLT_CALLEE_TRASH;
        varTypeCalleeTrashRegMasks[(int)(TYP_REF)] = SRBM_INT_CALLEE_TRASH;
        varTypeCalleeTrashRegMasks[(int)(TYP_BYREF)] = SRBM_INT_CALLEE_TRASH;
        varTypeCalleeTrashRegMasks[(int)(TYP_STRUCT)] = SRBM_INT_CALLEE_TRASH;
#if FEATURE_SIMD
        varTypeCalleeTrashRegMasks[(int)(TYP_SIMD8)] = SRBM_FLT_CALLEE_TRASH;
        varTypeCalleeTrashRegMasks[(int)(TYP_SIMD12)] = SRBM_FLT_CALLEE_TRASH;
        varTypeCalleeTrashRegMasks[(int)(TYP_SIMD16)] = SRBM_FLT_CALLEE_TRASH;
#if TARGET_XARCH
        varTypeCalleeTrashRegMasks[(int)(TYP_SIMD32)] = SRBM_FLT_CALLEE_TRASH;
        varTypeCalleeTrashRegMasks[(int)(TYP_SIMD64)] = SRBM_FLT_CALLEE_TRASH;
#elif TARGET_ARM64
        varTypeCalleeTrashRegMasks[(int)(TYP_SIMD)] = SRBM_FLT_CALLEE_TRASH;
#endif
#if FEATURE_MASKED_HW_INTRINSICS
        varTypeCalleeTrashRegMasks[(int)(TYP_MASK)] = SRBM_MSK_CALLEE_TRASH;
#endif
#endif
        varTypeCalleeTrashRegMasks[(int)(TYP_UNKNOWN)] = SRBM_INT_CALLEE_TRASH;
    }

    private void gtDispIconHandleFlag(GenTreeIntCon intCon)
    {
        switch (intCon.IconHandleFlag)
        {
            case GTF_EMPTY:
            {
                break;
            }

            case GTF_ICON_SCOPE_HDL:
            {
                jitprintf($" {"scope"}");
                break;
            }

            case GTF_ICON_CLASS_HDL:
            {
                jitprintf($" {"class"}");
                break;
            }

            case GTF_ICON_METHOD_HDL:
            {
                jitprintf($" {"method"}");
                break;
            }

            case GTF_ICON_FIELD_HDL:
            {
                jitprintf($" {"field"}");
                break;
            }

            case GTF_ICON_STATIC_HDL:
            {
                jitprintf($" {"static"}");
                break;
            }

            case GTF_ICON_STR_HDL:
            {
                jitprintf($" {"string"}");
                break;
            }

            case GTF_ICON_OBJ_HDL:
            {
                jitprintf($" {"object"}");
                break;
            }

            case GTF_ICON_CONST_PTR:
            {
                jitprintf($" {"const ptr"}");
                break;
            }

            case GTF_ICON_GLOBAL_PTR:
            {
                jitprintf($" {"global ptr"}");
                break;
            }

            case GTF_ICON_VARG_HDL:
            {
                jitprintf($" {"vararg"}");
                break;
            }

            case GTF_ICON_PINVKI_HDL:
            {
                jitprintf($" {"pinvoke"}");
                break;
            }

            case GTF_ICON_TOKEN_HDL:
            {
                jitprintf($" {"token"}");
                break;
            }

            case GTF_ICON_TLS_HDL:
            {
                jitprintf($" {"tls"}");
                break;
            }

            case GTF_ICON_FTN_ADDR:
            {
                jitprintf($" {"ftn"}");
                break;
            }

            case GTF_ICON_CIDMID_HDL:
            {
                jitprintf($" {"cid/mid"}");
                break;
            }

            case GTF_ICON_BBC_PTR:
            {
                jitprintf($" {"bbc"}");
                break;
            }

            case GTF_ICON_STATIC_BOX_PTR:
            {
                jitprintf($" {"static box ptr"}");
                break;
            }

            case GTF_ICON_FIELD_SEQ:
            {
                jitprintf($" {"field seq"}");
                break;
            }

            case GTF_ICON_STATIC_ADDR_PTR:
            {
                jitprintf($" {"static base addr cell"}");
                break;
            }

            case GTF_ICON_SECREL_OFFSET:
            {
                jitprintf($" {"relative offset in section"}");
                break;
            }

            case GTF_ICON_TLSGD_OFFSET:
            {
                jitprintf($" {"tls global dynamic offset"}");
                break;
            }

            default:
            {
                jitprintf(" ILLEGAL");
                break;
            }
        }
    }
}