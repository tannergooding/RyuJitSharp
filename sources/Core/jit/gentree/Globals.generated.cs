// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class Globals
{
    public const GenTreeFlags GTF_ICON_SCOPE_HDL = (GenTreeFlags)((int)(HandleKindIndex.GTF_ICON_SCOPE_HDL + 1) << HANDLE_KIND_INDEX_SHIFT);
    public const GenTreeFlags GTF_ICON_CLASS_HDL = (GenTreeFlags)((int)(HandleKindIndex.GTF_ICON_CLASS_HDL + 1) << HANDLE_KIND_INDEX_SHIFT);
    public const GenTreeFlags GTF_ICON_METHOD_HDL = (GenTreeFlags)((int)(HandleKindIndex.GTF_ICON_METHOD_HDL + 1) << HANDLE_KIND_INDEX_SHIFT);
    public const GenTreeFlags GTF_ICON_FIELD_HDL = (GenTreeFlags)((int)(HandleKindIndex.GTF_ICON_FIELD_HDL + 1) << HANDLE_KIND_INDEX_SHIFT);
    public const GenTreeFlags GTF_ICON_STATIC_HDL = (GenTreeFlags)((int)(HandleKindIndex.GTF_ICON_STATIC_HDL + 1) << HANDLE_KIND_INDEX_SHIFT);
    public const GenTreeFlags GTF_ICON_STR_HDL = (GenTreeFlags)((int)(HandleKindIndex.GTF_ICON_STR_HDL + 1) << HANDLE_KIND_INDEX_SHIFT);
    public const GenTreeFlags GTF_ICON_OBJ_HDL = (GenTreeFlags)((int)(HandleKindIndex.GTF_ICON_OBJ_HDL + 1) << HANDLE_KIND_INDEX_SHIFT);
    public const GenTreeFlags GTF_ICON_CONST_PTR = (GenTreeFlags)((int)(HandleKindIndex.GTF_ICON_CONST_PTR + 1) << HANDLE_KIND_INDEX_SHIFT);
    public const GenTreeFlags GTF_ICON_GLOBAL_PTR = (GenTreeFlags)((int)(HandleKindIndex.GTF_ICON_GLOBAL_PTR + 1) << HANDLE_KIND_INDEX_SHIFT);
    public const GenTreeFlags GTF_ICON_VARG_HDL = (GenTreeFlags)((int)(HandleKindIndex.GTF_ICON_VARG_HDL + 1) << HANDLE_KIND_INDEX_SHIFT);
    public const GenTreeFlags GTF_ICON_PINVKI_HDL = (GenTreeFlags)((int)(HandleKindIndex.GTF_ICON_PINVKI_HDL + 1) << HANDLE_KIND_INDEX_SHIFT);
    public const GenTreeFlags GTF_ICON_TOKEN_HDL = (GenTreeFlags)((int)(HandleKindIndex.GTF_ICON_TOKEN_HDL + 1) << HANDLE_KIND_INDEX_SHIFT);
    public const GenTreeFlags GTF_ICON_TLS_HDL = (GenTreeFlags)((int)(HandleKindIndex.GTF_ICON_TLS_HDL + 1) << HANDLE_KIND_INDEX_SHIFT);
    public const GenTreeFlags GTF_ICON_FTN_ADDR = (GenTreeFlags)((int)(HandleKindIndex.GTF_ICON_FTN_ADDR + 1) << HANDLE_KIND_INDEX_SHIFT);
    public const GenTreeFlags GTF_ICON_CIDMID_HDL = (GenTreeFlags)((int)(HandleKindIndex.GTF_ICON_CIDMID_HDL + 1) << HANDLE_KIND_INDEX_SHIFT);
    public const GenTreeFlags GTF_ICON_BBC_PTR = (GenTreeFlags)((int)(HandleKindIndex.GTF_ICON_BBC_PTR + 1) << HANDLE_KIND_INDEX_SHIFT);
    public const GenTreeFlags GTF_ICON_STATIC_BOX_PTR = (GenTreeFlags)((int)(HandleKindIndex.GTF_ICON_STATIC_BOX_PTR + 1) << HANDLE_KIND_INDEX_SHIFT);
    public const GenTreeFlags GTF_ICON_FIELD_SEQ = (GenTreeFlags)((int)(HandleKindIndex.GTF_ICON_FIELD_SEQ + 1) << HANDLE_KIND_INDEX_SHIFT);
    public const GenTreeFlags GTF_ICON_STATIC_ADDR_PTR = (GenTreeFlags)((int)(HandleKindIndex.GTF_ICON_STATIC_ADDR_PTR + 1) << HANDLE_KIND_INDEX_SHIFT);
    public const GenTreeFlags GTF_ICON_SECREL_OFFSET = (GenTreeFlags)((int)(HandleKindIndex.GTF_ICON_SECREL_OFFSET + 1) << HANDLE_KIND_INDEX_SHIFT);
    public const GenTreeFlags GTF_ICON_TLSGD_OFFSET = (GenTreeFlags)((int)(HandleKindIndex.GTF_ICON_TLSGD_OFFSET + 1) << HANDLE_KIND_INDEX_SHIFT);
}