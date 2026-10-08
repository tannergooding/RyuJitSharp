// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if MEASURE_CLRAPI_CALLS
namespace RyuJitSharp;

/// <summary>Managed timing forwarding surface, not an unmanaged EE vtable.</summary>
public sealed unsafe partial class WrapICorJitInfo : ICorJitInfo.Interface
{
    private readonly Compiler wrapComp;
    private readonly ICorJitInfo* wrapHnd;

    public WrapICorJitInfo(Compiler compiler, ICorJitInfo* jitInfo)
    {
        wrapComp = compiler;
        wrapHnd = jitInfo;
    }
}
#endif
