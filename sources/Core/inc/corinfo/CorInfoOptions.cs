// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CorInfoOptions;
using System;

namespace RyuJitSharp;

/// <summary>These are returned from <c>getMethodOptions</c>.</summary>
[Flags]
public enum CorInfoOptions
{
    /// <summary>Zero initialize all variables.</summary>
    CORINFO_OPT_INIT_LOCALS = 0x00000010,

    /// <summary>Is this shared generic code that access the generic context from the this pointer? If so, then if the method has SEH then the 'this' pointer must always be reported and kept alive.</summary>
    CORINFO_GENERICS_CTXT_FROM_THIS = 0x00000020,

    /// <summary>Is this shared generic code that access the generic context from the ParamTypeArg(that is a MethodDesc)? If so, then if the method has SEH then the <c>ParamTypeArg</c> must always be reported and kept alive.</summary>
    /// <remarks>Same as <see cref="CORINFO_CALLCONV_PARAMTYPE" />.</remarks>
    CORINFO_GENERICS_CTXT_FROM_METHODDESC = 0x00000040,

    /// <summary>Is this shared generic code that access the generic context from the ParamTypeArg(that is a MethodTable)? If so, then if the method has SEH then the 'ParamTypeArg' must always be reported and kept alive.</summary>
    /// <remarks>Same as <see cref="CORINFO_CALLCONV_PARAMTYPE" />.</remarks>
    CORINFO_GENERICS_CTXT_FROM_METHODTABLE = 0x00000080,

    CORINFO_GENERICS_CTXT_MASK = CORINFO_GENERICS_CTXT_FROM_THIS | CORINFO_GENERICS_CTXT_FROM_METHODDESC | CORINFO_GENERICS_CTXT_FROM_METHODTABLE,

    /// <summary>Keep the generics context alive throughout the method even if there is no explicit use, and report its location to the CLR.</summary>
    CORINFO_GENERICS_CTXT_KEEP_ALIVE = 0x00000100,
}
