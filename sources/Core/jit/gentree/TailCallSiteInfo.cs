// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace RyuJitSharp;

public partial struct TailCallSiteInfo
{
    private Flags _flags;
    private CORINFO_SIG_INFO _sig;
    private CORINFO_RESOLVED_TOKEN _token;

    /// <summary>Is the tailcall a callvirt instruction?</summary>
    public readonly bool IsCallvirt => (_flags & Flags.IsCallVirt) is not 0;

    /// <summary>Is the tailcall a calli instruction?</summary>
    public readonly bool IsCalli => (_flags & Flags.IsCalli) is not 0;

    /// <summary>Get the token of the callee</summary>
    [UnscopedRef]
    public ref CORINFO_RESOLVED_TOKEN Token
    {
        get
        {
            assert(Debugger.IsAttached || !IsCalli);
            return ref _token;
        }
    }

    /// <summary>Get the signature of the callee</summary>
    [UnscopedRef]
    public ref CORINFO_SIG_INFO Sig => ref _sig;

    /// <summary>Mark the tailcall as a calli with the given signature</summary>
    /// <param name="sigInfo"></param>
    public void SetCalli(in CORINFO_SIG_INFO sigInfo)
    {
        _flags = (_flags & ~Flags.IsCallVirt) | Flags.IsCalli;
        _sig = sigInfo;
    }

    /// <summary>Mark the tailcall as a callvirt with the given signature and token</summary>
    /// <param name="sigInfo"></param>
    /// <param name="resolvedToken"></param>
    public void SetCallvirt(in CORINFO_SIG_INFO sigInfo, in CORINFO_RESOLVED_TOKEN resolvedToken)
    {
        _flags = (_flags & ~Flags.IsCalli) | Flags.IsCallVirt;
        _sig = sigInfo;
        _token = resolvedToken;
    }

    // Mark the tailcall as a call with the given signature and token
    public void SetCall(in CORINFO_SIG_INFO sigInfo, in CORINFO_RESOLVED_TOKEN resolvedToken)
    {
        _flags = 0;
        _sig = sigInfo;
        _token = resolvedToken;
    }
}
