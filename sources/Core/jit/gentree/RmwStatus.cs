// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.RmwStatus;

namespace RyuJitSharp;

/// <summary>Read-modify-write status of a RMW memory op rooted at a storeInd</summary>
public enum RmwStatus
{
    /// <summary>RMW status of storeInd unknown</summary>
    /// <remarks>Default status unless modified by IsRMWMemOpRootedAtStoreInd</remarks>
    STOREIND_RMW_STATUS_UNKNOWN,

    // One of these denote storeind is a RMW memory operation.

    /// <summary>StoreInd is known to be a RMW memory op and dst candidate is op1</summary>
    STOREIND_RMW_DST_IS_OP1,

    /// <summary>StoreInd is known to be a RMW memory op and dst candidate is op2</summary>
    STOREIND_RMW_DST_IS_OP2,

    // One of these denote the reason for storeind is marked as non-RMW operation

    /// <summary>Addr mode is not yet supported for RMW memory</summary>
    STOREIND_RMW_UNSUPPORTED_ADDR,

    /// <summary>Operation is not supported for RMW memory</summary>
    STOREIND_RMW_UNSUPPORTED_OPER,

    /// <summary>Type is not supported for RMW memory</summary>
    STOREIND_RMW_UNSUPPORTED_TYPE,

    /// <summary>Indir to read value is not equivalent to indir that writes the value</summary>
    STOREIND_RMW_INDIR_UNEQUAL,
}
