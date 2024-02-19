// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct ICorJitInfo
{
    public struct PgoInstrumentationSchema
    {
        public nuint Offset;

        public PgoInstrumentationKind InstrumentationKind;

        public int ILOffset;

        public int Count;

        public int Other;
    }
}
