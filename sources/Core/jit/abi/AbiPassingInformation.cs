// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics.CodeAnalysis;

namespace RyuJitSharp;

public struct AbiPassingInformation
{
    private readonly AbiPassingSegment[] _segments;
    private AbiPassingSegment _singleSegment;
    private readonly bool _passedByRef;

    /// <summary>The number of segments used to pass the value.</summary>
    /// <remarks>
    ///   <list type="bullet">
    ///     <item>On SysV x64, structs can be passed in two registers, resulting in two register segments</item>
    ///     <item>On arm64/arm32, HFAs can be passed in up to four registers, giving four register segments</item>
    ///     <item>On arm32, structs can be split out over register and stack, giving multiple register segments and a struct segment.</item>
    ///     <item>On Windows x64, all parameters always fit into one stack slot or register, and thus always have NumSegments is 1</item>
    ///     <item>On loongarch64/riscv64, structs can be passed in two registers or can be split out over register and stack, giving multiple register segments and a struct segment.</item>
    ///   </list>
    /// </remarks>
    public readonly int NumSegments => (_segments is not null) ? _segments.Length : 1;

    /// <summary>Check if the argument is passed by (implicit) reference.</summary>
    /// <remarks>If true, a single pointer-sized segment is expected.</remarks>
    public readonly bool IsPassedByReference => _passedByRef;

    [UnscopedRef]
    public Span<AbiPassingSegment> Segments
    {
        get
        {
            var result = new Span<AbiPassingSegment>(ref _singleSegment);

            if (_segments is not null)
            {
                result = _segments;
            }
            return result;
        }
    }
}
