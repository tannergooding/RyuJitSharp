// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public const lvaPromotionType PROMOTION_TYPE_NONE = lvaPromotionType.PROMOTION_TYPE_NONE;

    public const lvaPromotionType PROMOTION_TYPE_INDEPENDENT = lvaPromotionType.PROMOTION_TYPE_INDEPENDENT;

    public const lvaPromotionType PROMOTION_TYPE_DEPENDENT = lvaPromotionType.PROMOTION_TYPE_DEPENDENT;

    public enum lvaPromotionType
    {
        /// <summary>The struct local is not promoted</summary>
        PROMOTION_TYPE_NONE,

        /// <summary>The struct local is promoted, and its field locals are independent of its parent struct local.</summary>
        PROMOTION_TYPE_INDEPENDENT,

        /// <summary>The struct local is promoted, but its field locals depend on its parent struct local.</summary>
        PROMOTION_TYPE_DEPENDENT

    }
}
