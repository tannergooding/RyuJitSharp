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
    private bool _passedByRef;

    /// <summary>The number of segments used to pass the value.</summary>
    /// <remarks>
    ///   <list type="bullet">
    ///     <item>On SysV x64, structs can be passed in two registers, resulting in two register segments</item>
    ///     <item>On arm64/arm32, HFAs can be passed in up to four registers, giving four register segments</item>
    ///     <item>On arm32, structs can be split out over register and stack, giving multiple register segments and a struct segment.</item>
    ///     <item>On Windows x64, all parameters always fit into one stack slot or register, and thus always have NumSegments == 1</item>
    ///     <item>On loongarch64/riscv64, structs can be passed in two registers or can be split out over register and stack, giving multiple register segments and a struct segment.</item>
    ///   </list>
    /// </remarks>
    public readonly int NumSegments => (_segments is not null) ? _segments.Length : 1;

    public bool HasExactlyOneStackSegment => (NumSegments == 1) && Segments[0].IsPassedOnStack;

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

    /// <summary>Create AbiPassingInformation from a single segment.</summary>
    /// <param name="comp">Compiler instance</param>
    /// <param name="passedByRef">If true, the argument is passed by reference and the segment is for its pointer.</param>
    /// <param name="segment">The single segment that represents the passing information</param>
    /// <returns>An instance of AbiPassingInformation.</returns>
    public static AbiPassingInformation FromSegment(Compiler comp, bool passedByRef, in AbiPassingSegment segment)
    {
        var info = new AbiPassingInformation {
            _passedByRef = passedByRef,
            _singleSegment = segment,
        };

#if DEBUG
        if (passedByRef)
        {
            assert(segment.Size == TARGET_POINTER_SIZE);
            assert(!segment.IsPassedInRegister || (segment.GetRegisterType() == TYP_I_IMPL));
        }
#endif

        return info;
    }

#if DEBUG
    public void Dump()
    {
        if (NumSegments != 1)
        {
            jitprintf($"{NumSegments} segments\n");
        }

        for (var i = 0; i < NumSegments; i++)
        {
            if (NumSegments > 1)
            {
                jitprintf($"  [{i}] ");
            }

            ref readonly var seg = ref Segments[i];
            seg.Dump();
            jitprintf($"{(IsPassedByReference ? " (implicit by-ref)" : "")}\n");
        }
    }
#endif
}
