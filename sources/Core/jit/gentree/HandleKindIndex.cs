// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public enum HandleKindIndex : uint
{
    GTF_ICON_SCOPE_HDL,
    GTF_ICON_CLASS_HDL,
    GTF_ICON_METHOD_HDL,
    GTF_ICON_FIELD_HDL,
    GTF_ICON_STATIC_HDL,
    GTF_ICON_STR_HDL,
    GTF_ICON_OBJ_HDL,
    GTF_ICON_CONST_PTR,
    GTF_ICON_GLOBAL_PTR,
    GTF_ICON_VARG_HDL,
    GTF_ICON_PINVKI_HDL,
    GTF_ICON_TOKEN_HDL,
    GTF_ICON_TLS_HDL,
    GTF_ICON_FTN_ADDR,
    GTF_ICON_CIDMID_HDL,
    GTF_ICON_BBC_PTR,
    GTF_ICON_STATIC_BOX_PTR,
    GTF_ICON_FIELD_SEQ,
    GTF_ICON_STATIC_ADDR_PTR,
    GTF_ICON_SECREL_OFFSET,
    GTF_ICON_TLSGD_OFFSET,
    COUNT,
}
