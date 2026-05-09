// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct PgoInfo
{
    /// <summary>pgo schema for method</summary>
    public unsafe ICorJitInfo.PgoInstrumentationSchema* PgoSchema;

    /// <summary>pgo data for the method</summary>
    public unsafe byte* PgoData;

    /// <summary>count of schema elements</summary>
    public int PgoSchemaCount;

    public unsafe PgoInfo(Compiler compiler)
    {
        PgoSchema = compiler.fgPgoSchema;
        PgoSchemaCount = compiler.fgPgoSchemaCount;
        PgoData = compiler.fgPgoData;
    }

    public PgoInfo(InlineContext inlineContext)
    {
        this = inlineContext.PgoInfo;
    }
}
