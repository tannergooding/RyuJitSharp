// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public const BoxRemovalOptions BR_REMOVE_AND_NARROW = BoxRemovalOptions.BR_REMOVE_AND_NARROW;
    public const BoxRemovalOptions BR_REMOVE_AND_NARROW_WANT_TYPE_HANDLE = BoxRemovalOptions.BR_REMOVE_AND_NARROW_WANT_TYPE_HANDLE;
    public const BoxRemovalOptions BR_REMOVE_BUT_NOT_NARROW = BoxRemovalOptions.BR_REMOVE_BUT_NOT_NARROW;
    public const BoxRemovalOptions BR_DONT_REMOVE = BoxRemovalOptions.BR_DONT_REMOVE;
    public const BoxRemovalOptions BR_DONT_REMOVE_WANT_TYPE_HANDLE = BoxRemovalOptions.BR_DONT_REMOVE_WANT_TYPE_HANDLE;

    public enum BoxRemovalOptions
    {
        /// <summary>remove effects, minimize remaining work, return possibly narrowed source tree</summary>
        BR_REMOVE_AND_NARROW,

        /// <summary>remove effects and minimize remaining work, return type handle tree</summary>
        BR_REMOVE_AND_NARROW_WANT_TYPE_HANDLE,

        /// <summary>remove effects, return original source tree</summary>
        BR_REMOVE_BUT_NOT_NARROW,

        /// <summary>check if removal is possible, return copy source tree</summary>
        BR_DONT_REMOVE,

        /// <summary>check if removal is possible, return type handle tree</summary>
        BR_DONT_REMOVE_WANT_TYPE_HANDLE,
    }
}
