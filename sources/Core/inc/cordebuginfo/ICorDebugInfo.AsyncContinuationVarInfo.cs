// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct ICorDebugInfo
{
    public struct AsyncContinuationVarInfo
    {
        /// <summary>IL number of variable (or one of the special IL numbers, like TYPECTXT_ILNUM)</summary>
        public int VarNumber;

        /// <summary>Offset in continuation object where this variable is stored</summary>
        public int Offset;
    }
}
