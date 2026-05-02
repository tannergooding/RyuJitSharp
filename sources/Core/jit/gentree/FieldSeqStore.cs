// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

/// <summary>Canonicalizes field sequences.</summary>
public sealed class FieldSeqStore
{
    private Dictionary<Pointer<CORINFO_FIELD_STRUCT_>, FieldSeq> _map;

    public FieldSeqStore()
    {
        _map = [];
    }

    /// <summary>"Merge" two field sequences together.</summary>
    /// <param name="a">The field sequence</param>
    /// <param name="b">The second sequence.</param>
    /// <returns>The result of "merging" "a" and "b" (see remarks).</returns>
    /// <remarks>
    ///   <para>A field sequence only explicitly represents its "head", i. e. the static or class field with which it begins.</para>
    ///   <para>The struct fields that are part of it are "implicit" - represented in IR as offsets with "empty" sequences.</para>
    ///   <para>Thus when two sequences are merged, only one can be explicit:</para>
    ///   <list type="bullet">
    ///     <item>field seq + empty     =&gt; field seq</item>
    ///     <item>empty     + field seq =&gt; field seq</item>
    ///     <item>empty     + empty     =&gt; empty</item>
    ///     <item>field seq + field seq =&gt; illegal</item>
    ///   </list>
    /// </remarks>
    public FieldSeq? Append(FieldSeq? a, FieldSeq? b)
    {
        if (a is null)
        {
            return b;
        }

        if (b is null)
        {
            return a;
        }

        // In UB-like code (such as manual IL) we can see an addition of two static field addresses.
        // Treat that as cancelling out the sequence, since the result will point nowhere.
        //
        // It may be possible for the JIT to encounter other types of UB additions, such as due to
        // complex optimizations, inlining, etc. In release we'll still do the right thing by returning
        // null here, but the more conservative assert can help avoid JIT bugs

        assert(a.Kind is FieldSeq.FieldKind.SimpleStaticKnownAddress);
        assert(b.Kind is FieldSeq.FieldKind.SimpleStaticKnownAddress);

        return null;
    }

    /// <summary>Create or retrieve a field sequence for the given field handle.</summary>
    /// <param name="fieldHnd">The field handle</param>
    /// <param name="offset">The "offset" value for the field sequence</param>
    /// <param name="fieldKind">The field's kind</param>
    /// <returns>The canonical field sequence for the given field.</returns>
    /// <remarks>The field sequence instance contains some cached information relevant to its usage; thus for a given handle all callers of this method must pass the same set of arguments.</remarks>
    public unsafe FieldSeq Create(CORINFO_FIELD_HANDLE fieldHnd, nint offset, FieldSeq.FieldKind fieldKind)
    {
        ref var fieldSeq = ref CollectionsMarshal.GetValueRefOrAddDefault(_map, fieldHnd, out var exists);

        if (!exists)
        {
            fieldSeq = new FieldSeq(fieldHnd, offset, fieldKind);
        }

        assert(fieldSeq is not null);

        assert(fieldSeq.Offset == offset);
        assert(fieldSeq.Kind == fieldKind);

        return fieldSeq;
    }
}
