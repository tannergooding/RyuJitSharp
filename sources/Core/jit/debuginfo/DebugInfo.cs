// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace RyuJitSharp;

public readonly struct DebugInfo
{
    private readonly InlineContext? _inlineContext;
    private readonly ILLocation _location;

    public DebugInfo(InlineContext? inlineContext, ILLocation location)
    {
        _inlineContext = inlineContext;
        _location = location;
    }

    public InlineContext? InlineContext => _inlineContext;

    /// <summary>Check if this debug info has both a valid inline context and valid location.</summary>
    [MemberNotNullWhen(true, nameof(_inlineContext), nameof(InlineContext))]
    public bool IsValid
        => (_inlineContext is not null) && _location.IsValid;

    public ILLocation Location => _location;

    /// <summary>Get debug info for the parent statement that inlined the statement for this debug info.</summary>
    /// <param name="parent">Debug info for the location that inlined this statement.</param>
    /// <returns>True if the current debug info is valid and has a parent; otherwise false. On false return, the 'parent' parameter is unaffected.</returns>
    public bool GetParent(out DebugInfo parent)
    {
        if ((_inlineContext is null) || _inlineContext.IsRoot)
        {
            parent = default;
            return false;
        }
        parent = new DebugInfo(_inlineContext.Parent, _inlineContext.Location);
        return true;
    }

#if DEBUG
    // Dump textual representation of this DebugInfo to jitstdout.
    public void Dump(bool recurse)
    {
        // The DebugInfo is printed in the format
        //
        //     INL02 @ 0xabc[EC]
        //
        // Before '@' is the ordinal of the inline context, then comes the IL
        // offset, and then comes the IL location flags (stack Empty, isCall).
        //
        // If 'recurse' is specified then dump the full DebugInfo path to the
        // root in the format
        //
        //     INL02 @ 0xabc[EC] <- INL01 @ 0x123[EC] <- ... <- INLRT @ 0x456[EC]
        //
        // with the left most entry being the inner most inlined statement.

        var context = InlineContext;

        if (context is not null)
        {
            if (context.IsRoot)
            {
                jitprintf("INLRT @ ");
            }
            else if (context.Ordinal != 0)
            {
                jitprintf($"{FMT_INL_CTX(context.Ordinal)} @ ");
            }
        }

        Location.Dump();

        if (recurse && GetParent(out var par))
        {
            jitprintf(" <- ");
            par.Dump(recurse);
        }
    }
#endif

    /// <summary>Validate this DebugInfo instance.</summary>
    /// <remarks>This validates that if there is DebugInfo, then it looks sane by checking that the IL location correctly points to the beginning of an IL instruction.</remarks>
    [Conditional("DEBUG")]
    public void Validate()
    {
#if DEBUG
        var di = this;

        do
        {
            if (!di.IsValid)
            {
                continue;
            }

            var isValidOffs = di.Location.Offset < di.InlineContext.ILSize;

            if (isValidOffs)
            {
                var isValidStart = di.InlineContext.ILInstsSet[di.Location.Offset];
                assert(isValidStart, "Detected invalid debug info: IL offset does not refer to the start of an IL instruction");
            }
            else
            {
                NO_WAY("Detected invalid debug info: IL offset is out of range");
            }
        }
        while (di.GetParent(out di));
#endif
    }
}
