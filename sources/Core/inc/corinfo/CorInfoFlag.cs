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
    // CORINFO_FLG_UNUSED = 0x00000001,
    // CORINFO_FLG_UNUSED = 0x00000002,
    // CORINFO_FLG_UNUSED = 0x00000004,

    CORINFO_FLG_STATIC = 0x00000008,

    CORINFO_FLG_FINAL = 0x00000010,

    CORINFO_FLG_SYNCH = 0x00000020,

    CORINFO_FLG_VIRTUAL = 0x00000040,

    // CORINFO_FLG_UNUSED = 0x00000080,
    // CORINFO_FLG_UNUSED = 0x00000100,

    /// <summary>This type is marked by [Intrinsic].</summary>
    CORINFO_FLG_INTRINSIC_TYPE = 0x00000200,

    CORINFO_FLG_ABSTRACT = 0x00000400,

    /// <summary>Member was added by Edit and Continue.</summary>
    CORINFO_FLG_EnC = 0x00000800,

    //
    // These are internal flags that can only be on methods
    //

    /// <summary>The method should be inlined if possible.</summary>
    CORINFO_FLG_FORCEINLINE = 0x00010000,

    /// <summary>The code for this method is shared between different generic instantiations (also set on classes/types).</summary>
    CORINFO_FLG_SHAREDINST = 0x00020000,

    /// <summary>Delegate.</summary>
    CORINFO_FLG_DELEGATE_INVOKE = 0x00040000,

    /// <summary>A P/Invoke call.</summary>
    CORINFO_FLG_PINVOKE = 0x00080000,

    // CORINFO_FLG_UNUSED = 0x00100000,

    /// <summary>This method is FCALL that has no GC check.</summary>
    /// <remarks>Don't put alone in loops</remarks>
    CORINFO_FLG_NOGCCHECK = 0x00200000,

    /// <summary>This method MAY have an intrinsic ID.</summary>
    CORINFO_FLG_INTRINSIC = 0x00400000,

    /// <summary>This method is an instance or type initializer.</summary>
    CORINFO_FLG_CONSTRUCTOR = 0x00800000,

    /// <summary>The method may contain hot code and should be aggressively optimized if possible.</summary>
    CORINFO_FLG_AGGRESSIVE_OPT = 0x01000000,

    /// <summary>Indicates that tier 0 JIT should not be used for a method that contains a loop.</summary>
    CORINFO_FLG_DISABLE_TIER0_FOR_LOOPS = 0x02000000,

    // CORINFO_FLG_UNUSED = 0x04000000,
    // CORINFO_FLG_UNUSED = 0x08000000,

    /// <summary>The method should not be inlined.</summary>
    CORINFO_FLG_DONT_INLINE = 0x10000000,

    /// <summary>The method should not be inlined, nor should its callers.</summary>
    /// <remarks>It cannot be tail called.</remarks>
    CORINFO_FLG_DONT_INLINE_CALLER = 0x20000000,

    // CORINFO_FLG_UNUSED = 0x40000000,

    //
    // These are internal flags that can only be on Classes
    //

    /// <summary>Is the class a value class.</summary>
    /// <remarks>This flag is defined in the Methods section, but is also valid on classes.</remarks>
    CORINFO_FLG_VALUECLASS = 0x00010000,

    // /// <summary>This class is satisfies <c>TypeHandle.IsCanonicalSubtype</c>.</summary>
    // CORINFO_FLG_SHAREDINST = 0x00020000,

    /// <summary>The object size varies depending of constructor args.</summary>
    CORINFO_FLG_VAROBJSIZE = 0x00040000,

    /// <summary>Class is an array class (initialized differently).</summary>
    CORINFO_FLG_ARRAY = 0x00080000,

    /// <summary>Struct or class has fields that overlap (aka union).</summary>
    CORINFO_FLG_OVERLAPPING_FIELDS = 0x00100000,

    /// <summary>It is an interface.</summary>
    CORINFO_FLG_INTERFACE = 0x00200000,

    /// <summary>// Does the class contain a gc ptr?</summary>
    CORINFO_FLG_CONTAINS_GC_PTR = 0x01000000,

    /// <summary>Is this a subclass of delegate or multicast delegate?</summary>
    CORINFO_FLG_DELEGATE = 0x02000000,

    /// <summary>Struct fields may be accessed via indexing (used for inline arrays).</summary>
    CORINFO_FLG_INDEXABLE_FIELDS = 0x04000000,

    /// <summary>It is byref-like value type.</summary>
    CORINFO_FLG_BYREF_LIKE = 0x08000000,

    // CORINFO_FLG_UNUSED = 0x10000000,

    /// <summary>Additional flexibility for when to run .cctor (see code:#ClassConstructionFlags).</summary>
    CORINFO_FLG_BEFOREFIELDINIT = 0x20000000,

    /// <summary>This is really a handle for a variable type.</summary>
    CORINFO_FLG_GENERIC_TYPE_VARIABLE = 0x40000000,

    /// <summary>Unsafe (C++'s /GS) value type.</summary>
    CORINFO_FLG_UNSAFE_VALUECLASS = unchecked((int)0x80000000),
}
