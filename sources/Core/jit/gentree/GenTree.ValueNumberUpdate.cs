// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
namespace RyuJitSharp;

public partial class GenTree
{
    public const ValueNumberUpdate CLEAR_VN = ValueNumberUpdate.CLEAR_VN;
    public const ValueNumberUpdate PRESERVE_VN = ValueNumberUpdate.PRESERVE_VN;

    public enum ValueNumberUpdate
    {
        /// <summary>Clear value number</summary>
        CLEAR_VN,

        /// <summary>Preserve value number</summary>
        PRESERVE_VN,
    }
}
#endif
