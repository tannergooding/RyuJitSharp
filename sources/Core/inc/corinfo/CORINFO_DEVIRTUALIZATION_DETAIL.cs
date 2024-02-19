// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CORINFO_DEVIRTUALIZATION_DETAIL;

namespace RyuJitSharp;

public enum CORINFO_DEVIRTUALIZATION_DETAIL
{
    /// <summary>No details available.</summary>
    CORINFO_DEVIRTUALIZATION_UNKNOWN,

    /// <summary>Devirtualization was successful.</summary>
    CORINFO_DEVIRTUALIZATION_SUCCESS,

    /// <summary>Object class was canonical.</summary>
    CORINFO_DEVIRTUALIZATION_FAILED_CANON,

    /// <summary>Object class was com.</summary>
    CORINFO_DEVIRTUALIZATION_FAILED_COM,

    /// <summary>Object class could not be cast to interface class.</summary>
    CORINFO_DEVIRTUALIZATION_FAILED_CAST,

    /// <summary>Interface method could not be found.</summary>
    CORINFO_DEVIRTUALIZATION_FAILED_LOOKUP,

    /// <summary>Interface method was default interface method.</summary>
    CORINFO_DEVIRTUALIZATION_FAILED_DIM,

    /// <summary>Object not subclass of base class.</summary>
    CORINFO_DEVIRTUALIZATION_FAILED_SUBCLASS,

    /// <summary>Virtual method installed via explicit override.</summary>
    CORINFO_DEVIRTUALIZATION_FAILED_SLOT,

    /// <summary>Devirtualization crossed version bubble.</summary>
    CORINFO_DEVIRTUALIZATION_FAILED_BUBBLE,

    /// <summary>Object has multiple implementations of interface class.</summary>
    CORINFO_DEVIRTUALIZATION_MULTIPLE_IMPL,

    /// <summary>Decl method is defined on class and decl method not in version bubble, and decl method not in closest to version bubble.</summary>
    CORINFO_DEVIRTUALIZATION_FAILED_BUBBLE_CLASS_DECL,

    /// <summary>Decl method is defined on interface and not in version bubble, and implementation type not entirely defined in bubble.</summary>
    CORINFO_DEVIRTUALIZATION_FAILED_BUBBLE_INTERFACE_DECL,

    /// <summary>Object class not defined within version bubble.</summary>
    CORINFO_DEVIRTUALIZATION_FAILED_BUBBLE_IMPL,

    /// <summary>Object class cannot be referenced from R2R code due to missing tokens.</summary>
    CORINFO_DEVIRTUALIZATION_FAILED_BUBBLE_IMPL_NOT_REFERENCEABLE,

    /// <summary>Crossgen2 virtual method algorithm and runtime algorithm differ in the presence of duplicate interface implementations.</summary>
    CORINFO_DEVIRTUALIZATION_FAILED_DUPLICATE_INTERFACE,

    /// <summary>Decl method cannot be represented in R2R image.</summary>
    CORINFO_DEVIRTUALIZATION_FAILED_DECL_NOT_REPRESENTABLE,

    /// <summary>Support for type equivalence in devirtualization is not yet implemented in crossgen2.</summary>
    CORINFO_DEVIRTUALIZATION_FAILED_TYPE_EQUIVALENCE,

    /// <summary>Sentinel for maximum value.</summary>
    CORINFO_DEVIRTUALIZATION_COUNT,
}
