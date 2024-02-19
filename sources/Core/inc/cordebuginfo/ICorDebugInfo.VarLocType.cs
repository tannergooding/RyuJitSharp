// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct ICorDebugInfo
{
    /// <summary>Describes the location of a native variable.</summary>
    /// <remarks>Currently <see cref="VLT_REG_BYREF" /> and <see cref="VLT_STK_BYREF" /> are only used for value types on X64.</remarks>
    public enum VarLocType
    {
        /// <summary>Variable is in a register.</summary>
        VLT_REG,

        /// <summary>Address of the variable is in a register.</summary>
        VLT_REG_BYREF,

        /// <summary>Variable is in a fp register.</summary>
        VLT_REG_FP,

        /// <summary>Variable is on the stack (memory addressed relative to the frame-pointer).</summary>
        VLT_STK,

        /// <summary>Address of the variable is on the stack (memory addressed relative to the frame-pointer).</summary>
        VLT_STK_BYREF,

        /// <summary>Variable lives in two registers.</summary>
        VLT_REG_REG,

        /// <summary>Variable lives partly in a register and partly on the stack.</summary>
        VLT_REG_STK,

        /// <summary>Reverse of <see cref="VLT_REG_STK" />.</summary>
        VLT_STK_REG,

        /// <summary>Variable lives in two slots on the stack.</summary>
        VLT_STK2,

        /// <summary>Variable lives on the floating-point stack.</summary>
        VLT_FPSTK,

        /// <summary>Variable is a fixed argument in a varargs function (relative to VARARGS_HANDLE).</summary>
        VLT_FIXED_VA,

        VLT_COUNT,

        VLT_INVALID,
    }
}
