// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
namespace RyuJitSharp;

public partial class GenTree
{
    public const genRegTag GT_REGTAG_NONE = genRegTag.GT_REGTAG_NONE;
    public const genRegTag GT_REGTAG_REG = genRegTag.GT_REGTAG_REG;

    public enum genRegTag
    {
        /// <summary>Nothing has been assigned to _gtRegNum</summary>
        GT_REGTAG_NONE,

        /// <summary>_gtRegNum has been assigned</summary>
        GT_REGTAG_REG,
    }
}
#endif
