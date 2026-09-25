// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct PatchpointInfo
{
    public int AsyncThreadOffset
    {
        readonly get
        {
            return _asyncThreadObjectOffset;
        }

        set
        {
            _asyncThreadObjectOffset = value;
        }
    }

    public readonly bool HasAsyncThreadOffset => _asyncThreadObjectOffset is not -1;
}
