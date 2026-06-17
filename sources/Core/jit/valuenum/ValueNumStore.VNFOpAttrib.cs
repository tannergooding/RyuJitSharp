// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class ValueNumStore
{
    private const VNFOpAttrib VNFOA_IllegalGenTreeOp = VNFOpAttrib.VNFOA_IllegalGenTreeOp;
    private const VNFOpAttrib VNFOA_Commutative = VNFOpAttrib.VNFOA_Commutative;
    private const VNFOpAttrib VNFOA_Arity1 = VNFOpAttrib.VNFOA_Arity1;
    private const VNFOpAttrib VNFOA_Arity2 = VNFOpAttrib.VNFOA_Arity2;
    private const VNFOpAttrib VNFOA_Arity4 = VNFOpAttrib.VNFOA_Arity4;
    private const VNFOpAttrib VNFOA_KnownNonNull = VNFOpAttrib.VNFOA_KnownNonNull;

    [Flags]
    internal enum VNFOpAttrib : byte
    {
        /// <summary>corresponds to a genTreeOps value that is not a legal VN func.</summary>
        VNFOA_IllegalGenTreeOp = 1 << VNFOA_IllegalGenTreeOpShift,

        /// <summary>1 iff the function is commutative.</summary>
        VNFOA_Commutative = 1 << VNFOA_CommutativeShift,

        /// <summary>Bits 2,3,4 encode the arity.</summary>
        VNFOA_Arity1 = 1 << VNFOA_ArityShift,

        /// <summary>Bits 2,3,4 encode the arity.</summary>
        VNFOA_Arity2 = 1 << (VNFOA_ArityShift + 1),

        /// <summary>Bits 2,3,4 encode the arity.</summary>
        VNFOA_Arity4 = 1 << (VNFOA_ArityShift + 2),

        /// <summary>1 iff the result is known to be non-null.</summary>
        VNFOA_KnownNonNull = 1 << VNFOA_KnownNonNullShift,
    }
}
