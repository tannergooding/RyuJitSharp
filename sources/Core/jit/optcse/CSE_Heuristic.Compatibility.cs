// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public abstract partial class CSE_HeuristicCommon
{
    public bool IsCompatibleType(var_types cseLclVarTyp, var_types expTyp)
    {
        if (cseLclVarTyp == expTyp)
        {
            return true;
        }

        if ((cseLclVarTyp is TYP_BYREF) && (expTyp is TYP_I_IMPL))
        {
            return true;
        }

        if ((cseLclVarTyp is TYP_I_IMPL) && (expTyp is TYP_BYREF))
        {
            return true;
        }

        return false;
    }
}
