// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class FieldSeq
{
    public enum FieldKind : byte
    {
        /// <summary>An instance field.</summary>
        Instance = 0,

        /// <summary>Simple static field - the handle represents a unique location.</summary>
        SimpleStatic = 1,

        /// <summary>Simple static field - the handle represents a known location.</summary>
        SimpleStaticKnownAddress = 2,

        /// <summary>Static field on a shared generic type: "Class&lt;__Canon&gt;.StaticField".</summary>
        SharedStatic = 3,
    }
}
