// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed class GenTreeStoreInd : GenTreeIndir
{
    private RmwStatus _rmwStatus;

    public GenTreeStoreInd(var_types type, GenTree addr, GenTree data)
        : base(GT_STOREIND, type, addr, data)
    {
    }

    internal GenTreeStoreInd(var_types type, GenTree addr, GenTree data, GenTree source, NodeThreading threading)
        : base(GT_STOREIND, type, addr, data, source, threading)
    {
        if (!Configuration.CPU_LOAD_STORE_ARCH && (source is GenTreeStoreInd store))
        {
            _rmwStatus = store._rmwStatus;
        }
    }

    public bool IsRMWMemoryOp =>
        !Configuration.CPU_LOAD_STORE_ARCH && (_rmwStatus is STOREIND_RMW_DST_IS_OP1 or STOREIND_RMW_DST_IS_OP2);

    public bool IsRMWDstOp1 => !Configuration.CPU_LOAD_STORE_ARCH && (_rmwStatus is STOREIND_RMW_DST_IS_OP1);

    public bool IsRMWDstOp2 => !Configuration.CPU_LOAD_STORE_ARCH && (_rmwStatus is STOREIND_RMW_DST_IS_OP2);

    public RmwStatus RmwStatus
    {
        get
        {
            return Configuration.CPU_LOAD_STORE_ARCH ? STOREIND_RMW_STATUS_UNKNOWN : _rmwStatus;
        }

        set
        {
            if (Configuration.CPU_LOAD_STORE_ARCH && (value is not STOREIND_RMW_STATUS_UNKNOWN))
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.RMWStatusIsNotSupportedOnThisTarget);
            }

            _rmwStatus = value;
        }
    }
}
