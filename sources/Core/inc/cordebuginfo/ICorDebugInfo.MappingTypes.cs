// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct ICorDebugInfo
{
    public enum MappingTypes
    {
        NO_MAPPING = -1,

        PROLOG = -2,

        EPILOG = -3,

        // Sentinel value. This should be set to the largest magnitude value in the enum
        // so that the compression routines know the enum's range.
        MAX_MAPPING_VALUE = -3
    }
}
