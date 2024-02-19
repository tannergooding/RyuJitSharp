// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct ICorDebugInfo
{
    // VLT_FIXED_VA -- fixed argument of a varargs function.
    //
    // The argument location depends on the size of the variable
    // arguments (...). Inspecting the VARARGS_HANDLE indicates the
    // location of the first arg. This argument can then be accessed
    // relative to the position of the first arg
    public struct vlFixedVarArg
    {
        public uint vlfvOffset;
    }
}
