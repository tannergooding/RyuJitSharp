// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    private static bool emitIGisInProlog(insGroup? ig)
        => ig is not null && (ig.igFlags & InsGroupFlags.Prolog) != 0;

    private static bool emitIGisInEpilog(insGroup? ig)
        => ig is not null && (ig.igFlags & InsGroupFlags.Epilog) != 0;

    private static bool emitIGisInFuncletProlog(insGroup? ig)
        => ig is not null && (ig.igFlags & InsGroupFlags.FuncletProlog) != 0;

    private static bool emitIGisInFuncletEpilog(insGroup? ig)
        => ig is not null && (ig.igFlags & InsGroupFlags.FuncletEpilog) != 0;

    public bool emitGeneratingPrologOrFuncletProlog()
        => emitIGisInProlog(emitCurIG) || emitIGisInFuncletProlog(emitCurIG);

    public bool emitGeneratingEpilogOrFuncletEpilog()
        => emitIGisInEpilog(emitCurIG) || emitIGisInFuncletEpilog(emitCurIG);
}
