// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class Globals
{
    public static bool isRegParamType(var_types type)
    {
#if TARGET_X86
        return (type <= TYP_INT) || (type == TYP_REF) || (type == TYP_BYREF);
#else
        return true;
#endif
    }

#if DEBUG
    public static string varTypeGCstring(var_types type)
    {
        return type switch {
            TYP_REF => "gcr",
            TYP_BYREF => "byr",
            _ => "non",
        };
    }
#endif
}
