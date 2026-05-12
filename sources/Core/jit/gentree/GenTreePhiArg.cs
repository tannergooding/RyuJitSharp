// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class GenTreePhiArg : GenTreeLclVarCommon
{
    public GenTreePhiArg(var_types type, int lclNum)
        : base(GT_PHI_ARG, type, lclNum)
    {
    }
}
