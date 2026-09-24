// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.FrameType;

namespace RyuJitSharp;

public enum FrameType
{
    FT_NOT_SET,
    FT_ESP_FRAME,
    FT_EBP_FRAME,
#if DOUBLE_ALIGN
    FT_DOUBLE_ALIGN_FRAME,
#endif
}

#if DOUBLE_ALIGN
public enum CanDoubleAlign
{
    CANT_DOUBLE_ALIGN,
    CAN_DOUBLE_ALIGN,
    MUST_DOUBLE_ALIGN,
    COUNT_DOUBLE_ALIGN,

    DEFAULT_DOUBLE_ALIGN = CAN_DOUBLE_ALIGN,
}
#endif
