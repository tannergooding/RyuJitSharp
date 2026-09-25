// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class Globals
{
    public static unsafe bool jitStaticFldIsGlobAddr(CORINFO_FIELD_HANDLE fldHnd)
    {
        return (fldHnd == FLD_GLOBAL_DS) || (fldHnd == FLD_GLOBAL_FS) || (fldHnd == FLD_GLOBAL_GS);
    }
}
