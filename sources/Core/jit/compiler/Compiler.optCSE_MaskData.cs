// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public struct optCSE_MaskData
    {
        public unsafe EXPSET_TP CSE_defMask;
        public unsafe EXPSET_TP CSE_useMask;
    }
}
