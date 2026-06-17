// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class GenTree
{
    internal static ReadOnlySpan<HandleKindFlag> s_handleKindFlags => [
        HKF_INVARIANT, // GTF_ICON_SCOPE_HDL
        HKF_INVARIANT, // GTF_ICON_CLASS_HDL
        HKF_INVARIANT, // GTF_ICON_METHOD_HDL
        HKF_INVARIANT, // GTF_ICON_FIELD_HDL
        0, // GTF_ICON_STATIC_HDL
        HKF_INVARIANT | HKF_NONNULL, // GTF_ICON_STR_HDL
        0, // GTF_ICON_OBJ_HDL
        HKF_INVARIANT, // GTF_ICON_CONST_PTR
        0, // GTF_ICON_GLOBAL_PTR
        HKF_INVARIANT, // GTF_ICON_VARG_HDL
        0, // GTF_ICON_PINVKI_HDL
        HKF_INVARIANT, // GTF_ICON_TOKEN_HDL
        HKF_INVARIANT, // GTF_ICON_TLS_HDL
        0, // GTF_ICON_FTN_ADDR
        HKF_INVARIANT, // GTF_ICON_CIDMID_HDL
        0, // GTF_ICON_BBC_PTR
        0, // GTF_ICON_STATIC_BOX_PTR
        0, // GTF_ICON_FIELD_SEQ
        HKF_INVARIANT | HKF_NONNULL, // GTF_ICON_STATIC_ADDR_PTR
        HKF_INVARIANT, // GTF_ICON_SECREL_OFFSET
        HKF_INVARIANT, // GTF_ICON_TLSGD_OFFSET
    ];
}