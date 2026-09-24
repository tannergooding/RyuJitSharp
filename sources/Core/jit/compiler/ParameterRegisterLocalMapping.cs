// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct ParameterRegisterLocalMapping
{
    public AbiPassingSegment RegisterSegment;
    public int LclNum;
    public int Offset;

    public ParameterRegisterLocalMapping(AbiPassingSegment segment, int lclNum, int offset)
    {
        RegisterSegment = segment;
        LclNum = lclNum;
        Offset = offset;
    }
}
