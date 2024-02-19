// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CorJitResult;

namespace RyuJitSharp;

/// <summary>These are error codes returned by CompileMethod.</summary>
public enum CorJitResult
{
    // Note that I dont use FACILITY_NULL for the facility number, we may want to get a 'real' facility number
    CORJIT_OK = NO_ERROR,

    CORJIT_BADCODE = (SEVERITY_ERROR << 31) | (FACILITY_NULL << 16) | 1,

    CORJIT_OUTOFMEM = (SEVERITY_ERROR << 31) | (FACILITY_NULL << 16) | 2,

    CORJIT_INTERNALERROR = (SEVERITY_ERROR << 31) | (FACILITY_NULL << 16) | 3,

    CORJIT_SKIPPED = (SEVERITY_ERROR << 31) | (FACILITY_NULL << 16) | 4,

    CORJIT_RECOVERABLEERROR = (SEVERITY_ERROR << 31) | (FACILITY_NULL << 16) | 5,

    CORJIT_IMPLLIMITATION = (SEVERITY_ERROR << 31) | (FACILITY_NULL << 16) | 6,
}
