// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Globals
{
    /// <summary>default value for JitConfig.JitMaxLocalsToTrack</summary>
    public const uint lclMAX_ALLSET_TRACKED = 0x400;
}
