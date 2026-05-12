// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct ValueSize
{
    private enum Kind
    {
        /// <summary>Value has size known at compile time</summary>
        Exact,  

        /// <summary>Value represents the platform vector length (Vector&lt;T&gt;/TYP_Simd)</summary>
        Vector, 

        /// <summary>Value represents the platform mask length (TYP_MASK)</summary>
        Mask,   

        /// <summary>Value represents some compile-time unknown size that is not equivalent to any other value.</summary>
        Unknown,
    }
}
