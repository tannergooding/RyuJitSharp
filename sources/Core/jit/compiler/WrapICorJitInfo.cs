// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if MEASURE_CLRAPI_CALLS
using System;
using System.Runtime.InteropServices;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

/// <summary>Per-compilation unmanaged proxy that times calls forwarded to the EE.</summary>
public sealed unsafe partial class WrapICorJitInfo : ICorJitInfo.Interface, IDisposable
{
    private readonly Compiler wrapComp;
    private readonly ICorJitInfo* wrapHnd;
    private readonly GCHandle _wrapperHandle;
    private ProxyContext* _proxyContext;

    public WrapICorJitInfo(Compiler compiler, ICorJitInfo* jitInfo)
    {
        wrapComp = compiler;
        wrapHnd = jitInfo;

        var proxyContext = (ProxyContext*)NativeMemory.Alloc((nuint)sizeof(ProxyContext));
        if (proxyContext == null)
        {
            throw new FatalJitException(CORJIT_OUTOFMEM, "Unable to allocate the ICorJitInfo timing proxy.");
        }

        var wrapperHandle = default(GCHandle);
        try
        {
            wrapperHandle = GCHandle.Alloc(this);
            proxyContext->JitInfo.lpVtbl = s_vtbl;
            proxyContext->WrapperHandle = GCHandle.ToIntPtr(wrapperHandle);
            _wrapperHandle = wrapperHandle;
            _proxyContext = proxyContext;
        }
        catch
        {
            if (wrapperHandle.IsAllocated)
            {
                wrapperHandle.Free();
            }

            NativeMemory.Free(proxyContext);
            throw;
        }
    }

    public ICorJitInfo* JitInfo
    {
        get
        {
            ObjectDisposedException.ThrowIf(_proxyContext == null, this);
            return &_proxyContext->JitInfo;
        }
    }

    public void Install()
    {
        ObjectDisposedException.ThrowIf(_proxyContext == null, this);

        if (wrapComp.info.compCompHnd != wrapHnd)
        {
            throw new FatalJitException(CORJIT_INTERNALERROR, "The compiler EE handle changed before timing proxy installation.");
        }

        wrapComp.info.compCompHnd = &_proxyContext->JitInfo;
    }

    public void Dispose()
    {
        var proxyContext = _proxyContext;
        if (proxyContext == null)
        {
            return;
        }

        if (wrapComp.info.compCompHnd == &proxyContext->JitInfo)
        {
            wrapComp.info.compCompHnd = wrapHnd;
        }

        _proxyContext = null;
        proxyContext->WrapperHandle = 0;
        NativeMemory.Free(proxyContext);
        _wrapperHandle.Free();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProxyContext
    {
        public ICorJitInfo JitInfo;
        public nint WrapperHandle;
    }
}
#endif
