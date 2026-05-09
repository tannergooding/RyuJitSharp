// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CorInfoFlag;
using System;

namespace RyuJitSharp;

/// <summary>These are the attribute flags for fields and methods (<c>getMethodAttribs</c>).</summary>
[Flags]
public enum CorInfoFlag
{
    // CORINFO_FLG_UNUSED = 1 << 0,
    // CORINFO_FLG_UNUSED = 1 << 1,
    // CORINFO_FLG_UNUSED = 1 << 2,

    CORINFO_FLG_STATIC = 1 << 3,

    CORINFO_FLG_FINAL = 1 << 4,

    CORINFO_FLG_SYNCH = 1 << 5,

    CORINFO_FLG_VIRTUAL = 1 << 6,

    // CORINFO_FLG_UNUSED = 1 << 7,
    // CORINFO_FLG_UNUSED = 1 << 8,

    /// <summary>This type is marked by [Intrinsic].</summary>
    CORINFO_FLG_INTRINSIC_TYPE = 1 << 9,

    CORINFO_FLG_ABSTRACT = 1 << 10,

    /// <summary>Member was added by Edit and Continue.</summary>
    CORINFO_FLG_EnC = 1 << 11,

    //
    // These are internal flags that can only be on methods
    //

    /// <summary>The method should be inlined if possible.</summary>
    CORINFO_FLG_FORCEINLINE = 1 << 16,

    /// <summary>The code for this method is shared between different generic instantiations (also set on classes/types).</summary>
    CORINFO_FLG_SHAREDINST = 1 << 17,

    /// <summary>Delegate.</summary>
    CORINFO_FLG_DELEGATE_INVOKE = 1 << 18,

    /// <summary>A P/Invoke call.</summary>
    CORINFO_FLG_PINVOKE = 1 << 19,

    // CORINFO_FLG_UNUSED = 1 << 20,

    /// <summary>This method is FCALL that has no GC check.</summary>
    /// <remarks>Don't put alone in loops</remarks>
    CORINFO_FLG_NOGCCHECK = 1 << 21,

    /// <summary>This method MAY have an intrinsic ID.</summary>
    CORINFO_FLG_INTRINSIC = 1 << 22,

    /// <summary>This method is an instance or type initializer.</summary>
    CORINFO_FLG_CONSTRUCTOR = 1 << 23,

    /// <summary>The method may contain hot code and should be aggressively optimized if possible.</summary>
    CORINFO_FLG_AGGRESSIVE_OPT = 1 << 24,

    /// <summary>Indicates that tier 0 JIT should not be used for a method that contains a loop.</summary>
    CORINFO_FLG_DISABLE_TIER0_FOR_LOOPS = 1 << 25,

    // CORINFO_FLG_UNUSED = 1 << 26,
    // CORINFO_FLG_UNUSED = 1 << 27,

    /// <summary>The method should not be inlined.</summary>
    CORINFO_FLG_DONT_INLINE = 1 << 28,

    /// <summary>The method should not be inlined, nor should its callers.</summary>
    /// <remarks>It cannot be tail called.</remarks>
    CORINFO_FLG_DONT_INLINE_CALLER = 1 << 29,

    // CORINFO_FLG_UNUSED = 1 << 30,

    //
    // These are internal flags that can only be on Classes
    //

    /// <summary>Is the class a value class.</summary>
    /// <remarks>This flag is defined in the Methods section, but is also valid on classes.</remarks>
    CORINFO_FLG_VALUECLASS = 1 << 16,

    // /// <summary>This class is satisfies <c>TypeHandle.IsCanonicalSubtype</c>.</summary>
    // CORINFO_FLG_SHAREDINST = 1 << 17,

    /// <summary>The object size varies depending of constructor args.</summary>
    CORINFO_FLG_VAROBJSIZE = 1 << 18,

    /// <summary>Class is an array class (initialized differently).</summary>
    CORINFO_FLG_ARRAY = 1 << 19,

    /// <summary>Struct or class has fields that overlap (aka union).</summary>
    CORINFO_FLG_OVERLAPPING_FIELDS = 1 << 20,

    /// <summary>It is an interface.</summary>
    CORINFO_FLG_INTERFACE = 1 << 21,

    /// <summary>// Does the class contain a gc ptr?</summary>
    CORINFO_FLG_CONTAINS_GC_PTR = 1 << 24,

    /// <summary>Is this a subclass of delegate or multicast delegate?</summary>
    CORINFO_FLG_DELEGATE = 1 << 25,

    /// <summary>Struct fields may be accessed via indexing (used for inline arrays).</summary>
    CORINFO_FLG_INDEXABLE_FIELDS = 1 << 26,

    /// <summary>It is byref-like value type.</summary>
    CORINFO_FLG_BYREF_LIKE = 1 << 27,

    // CORINFO_FLG_UNUSED = 1 << 28,

    /// <summary>Additional flexibility for when to run .cctor (see code:#ClassConstructionFlags).</summary>
    CORINFO_FLG_BEFOREFIELDINIT = 1 << 29,

    /// <summary>This is really a handle for a variable type.</summary>
    CORINFO_FLG_GENERIC_TYPE_VARIABLE = 1 << 30,

    /// <summary>Unsafe (C++'s /GS) value type.</summary>
    CORINFO_FLG_UNSAFE_VALUECLASS = 1 << 31,
}
