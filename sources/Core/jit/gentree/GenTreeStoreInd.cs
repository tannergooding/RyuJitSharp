// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class GenTreeStoreInd : GenTreeIndir
{
#if !CPU_LOAD_STORE_ARCH
    private RmwStatus _rmwStatus;
#endif

    public GenTreeStoreInd(var_types type, GenTree addr, GenTree data)
        : base(GT_STOREIND, type, addr, data)
    {
    }

    internal GenTreeStoreInd(var_types type, GenTree addr, GenTree data, GenTree source, NodeThreading threading)
        : base(GT_STOREIND, type, addr, data, source, threading)
    {
#if !CPU_LOAD_STORE_ARCH
        if (source is GenTreeStoreInd store)
        {
            _rmwStatus = store._rmwStatus;
        }
#endif
    }

#if CPU_LOAD_STORE_ARCH
    public RmwStatus RmwStatus => STOREIND_RMW_STATUS_UNKNOWN;
#else
    public bool IsRMWMemoryOp => _rmwStatus is STOREIND_RMW_DST_IS_OP1 or STOREIND_RMW_DST_IS_OP2;

    public bool IsRMWDstOp1 => _rmwStatus is STOREIND_RMW_DST_IS_OP1;

    public bool IsRMWDstOp2 => _rmwStatus is STOREIND_RMW_DST_IS_OP2;

    public RmwStatus RmwStatus
    {
        get
        {
            return _rmwStatus;
        }

        set
        {
            _rmwStatus = value;
        }
    }
#endif
}
