// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public const TypeProducerKind TPK_Unknown = TypeProducerKind.TPK_Unknown;
    public const TypeProducerKind TPK_Handle = TypeProducerKind.TPK_Handle;
    public const TypeProducerKind TPK_GetType = TypeProducerKind.TPK_GetType;
    public const TypeProducerKind TPK_Null = TypeProducerKind.TPK_Null;
    public const TypeProducerKind TPK_Other = TypeProducerKind.TPK_Other;

    public enum TypeProducerKind
    {
        /// <summary>May not be a RuntimeType</summary>
        TPK_Unknown = 0,

        /// <summary>RuntimeType via handle</summary>
        TPK_Handle = 1,

        /// <summary>RuntimeType via Object.get_Type()</summary>
        TPK_GetType = 2,

        /// <summary>Tree value is null</summary>
        TPK_Null = 3,

        /// <summary>RuntimeType via other means</summary>
        TPK_Other = 4
    }
}
