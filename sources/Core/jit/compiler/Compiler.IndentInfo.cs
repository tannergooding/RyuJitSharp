// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public const IndentInfo IINone = IndentInfo.IINone;
    public const IndentInfo IIArc = IndentInfo.IIArc;
    public const IndentInfo IIArcTop = IndentInfo.IIArcTop;
    public const IndentInfo IIArcBottom = IndentInfo.IIArcBottom;
    public const IndentInfo IIEmbedded = IndentInfo.IIEmbedded;
    public const IndentInfo IIError = IndentInfo.IIError;
    public const IndentInfo IndentInfoCount = IndentInfo.IndentInfoCount;

    public enum IndentInfo
    {
        IINone,
        IIArc,
        IIArcTop,
        IIArcBottom,
        IIEmbedded,
        IIError,
        IndentInfoCount,
    }
}
