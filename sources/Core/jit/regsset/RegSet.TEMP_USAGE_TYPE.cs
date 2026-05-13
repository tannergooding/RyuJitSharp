// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct RegSet
{
    public const TEMP_USAGE_TYPE TEMP_USAGE_FREE = TEMP_USAGE_TYPE.TEMP_USAGE_FREE;

    public const TEMP_USAGE_TYPE TEMP_USAGE_USED = TEMP_USAGE_TYPE.TEMP_USAGE_USED;

    public enum TEMP_USAGE_TYPE
    {
        TEMP_USAGE_FREE,

        TEMP_USAGE_USED,
    }
}
