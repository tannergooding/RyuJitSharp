// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

/// <summary>Indicates the details of the runtime lookup operation to be performed.</summary>
public unsafe struct CORINFO_RUNTIME_LOOKUP
{
    /// <summary>This is signature you must pass back to the runtime lookup helper.</summary>
    public void* signature;

    /// <summary>Here is the helper you must call. It is one of CORINFO_HELP_RUNTIMEHANDLE_* helpers.</summary>
    public CorInfoHelpFunc helper;

    /// <summary>Number of indirections to get there.</summary>
    /// <remarks>
    ///   <para><see cref="CORINFO_USEHELPER" /> = don't know how to get it, so use helper function at run-time instead</para>
    ///   <para><see cref="CORINFO_USENULL" /> = the context should be null because the callee doesn't actually use it</para>
    ///   <para>0 = use the this pointer itself (e.g. token is C&lt;!0&gt; inside code in sealed class C) or method desc itself (e.g. token is method void M.mymeth&lt;!!0&gt;() inside code in M.mymeth)</para>
    ///   <para>Otherwise, follow each byte-offset stored in the "offsets[]" array (may be negative)</para>
    /// </remarks>
    public ushort indirections;

    /// <summary>If set, test for null and branch to helper if null.</summary>
    public bool testForNull;

    public ushort sizeOffset;

    private _offsets_e__FixedBuffer _offsets;

    [UnscopedRef]
    public Span<nuint> offsets => _offsets;

    /// <summary>If set, first offset is indirect.</summary>
    /// <remarks>
    ///   <para><c>false</c> means that value stored at first offset (offsets[0]) from pointer is next pointer, to which the next offset (offsets[1]) is added and so on.</para>
    ///   <para><c>true</c> means that value stored at first offset (offsets[0]) from pointer is offset1, and the next pointer is stored at pointer+offsets[0]+offset1.</para>
    /// </remarks>
    public bool indirectFirstOffset;

    /// <summary>If set, second offset is indirect.</summary>
    /// <remarks>
    ///   <para><c>false</c> means that value stored at second offset (offsets[1]) from pointer is next pointer, to which the next offset (offsets[2]) is added and so on.</para>
    ///   <para><c>true</c> means that value stored at second offset (offsets[1]) from pointer is offset2, and the next pointer is stored at pointer+offsets[1]+offset2.</para>
    /// </remarks>
    public bool indirectSecondOffset;

    [InlineArray(CORINFO_MAXINDIRECTIONS)]
    private struct _offsets_e__FixedBuffer
    {
        public nuint e0;
    }
}
