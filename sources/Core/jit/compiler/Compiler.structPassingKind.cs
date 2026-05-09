// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public const structPassingKind SPK_Unknown = structPassingKind.SPK_Unknown;
    public const structPassingKind SPK_PrimitiveType = structPassingKind.SPK_PrimitiveType;
    public const structPassingKind SPK_EnclosingType = structPassingKind.SPK_EnclosingType;
    public const structPassingKind SPK_ByValue = structPassingKind.SPK_ByValue;
    public const structPassingKind SPK_ByValueAsHfa = structPassingKind.SPK_ByValueAsHfa;
    public const structPassingKind SPK_ByReference = structPassingKind.SPK_ByReference;

    public enum structPassingKind
    {
        /// <summary>Invalid value, never returned</summary>
        SPK_Unknown,

        /// <summary>The struct is passed/returned using a primitive type.</summary>
        SPK_PrimitiveType,

        /// <summary>Like SPK_Primitive type, but used for return types that require a primitive type temp that is larger than the struct size.</summary>
        /// <remarks>Currently used for structs of size 3, 5, 6, or 7 bytes.</remarks>
        SPK_EnclosingType,

        /// <summary>The struct is passed/returned by value (using the ABI rules).</summary>
        /// <remarks>
        ///   <para>For ARM64 and UNIX_X64 in multiple registers. (when all of the parameters registers are used, then the stack will be used)</para>
        ///   <para>For X86 passed on the stack, for ARM32 passed in registers or the stack or split between registers and the stack.</para>
        /// </remarks>
        SPK_ByValue,

        /// <summary>The struct is passed/returned as an HFA in multiple registers.</summary>
        SPK_ByValueAsHfa,

        /// <summary>The struct is passed/returned by reference to a copy/buffer.</summary>
        SPK_ByReference,
    }
}
