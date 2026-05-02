// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CorTypeAttr;
using System;

namespace RyuJitSharp;

/// <summary>TypeDef/ExportedType attr bits, used by DefineTypeDef.</summary>
[Flags]
public enum CorTypeAttr : uint
{
    /// <summary>Use this mask to retrieve the type visibility information.</summary>
    tdVisibilityMask = 0x00000007,

    /// <summary>Class is not public scope.</summary>
    tdNotPublic = 0x00000000,

    /// <summary>Class is public scope.</summary>
    tdPublic = 0x00000001,

    /// <summary>Class is nested with public visibility.</summary>
    tdNestedPublic = 0x00000002,

    /// <summary>Class is nested with private visibility.</summary>
    tdNestedPrivate = 0x00000003,

    /// <summary>Class is nested with family visibility.</summary>
    tdNestedFamily = 0x00000004,

    /// <summary>Class is nested with assembly visibility.</summary>
    tdNestedAssembly = 0x00000005,

    /// <summary>Class is nested with family and assembly visibility.</summary>
    tdNestedFamANDAssem = 0x00000006,

    /// <summary>Class is nested with family or assembly visibility.</summary>
    tdNestedFamORAssem = 0x00000007,

    /// <summary>Use this mask to retrieve class layout information</summary>
    tdLayoutMask = 0x00000018,

    /// <summary>Class fields are auto-laid out</summary>
    tdAutoLayout = 0x00000000,

    /// <summary>Class fields are laid out sequentially</summary>
    tdSequentialLayout = 0x00000008,

    /// <summary>Layout is supplied explicitly</summary>
    tdExplicitLayout = 0x00000010,

    /// <summary>Layout is supplied via the System.Runtime.InteropServices.ExtendedLayoutAttribute</summary>
    tdExtendedLayout = 0x00000018,

    // end layout mask

    /// <summary>Use this mask to retrieve class semantics information.</summary>
    tdClassSemanticsMask = 0x00000020,

    /// <summary>Type is a class.</summary>
    tdClass = 0x00000000,

    /// <summary>Type is an interface.</summary>
    tdInterface = 0x00000020,

    // end semantics mask

    // Special semantics in addition to class semantics.

    /// <summary>Class is abstract</summary>
    tdAbstract = 0x00000080,

    /// <summary>Class is concrete and may not be extended</summary>
    tdSealed = 0x00000100,

    /// <summary>Class name is special.  Name describes how.</summary>
    tdSpecialName = 0x00000400,

    // Implementation attributes.

    /// <summary>Class / interface is imported</summary>
    tdImport = 0x00001000,

    /// <summary>The class is Serializable.</summary>
    tdSerializable = 0x00002000,

    /// <summary>The type is a Windows Runtime type</summary>
    tdWindowsRuntime = 0x00004000,

    /// <summary>Use tdStringFormatMask to retrieve string information for native interop</summary>
    tdStringFormatMask = 0x00030000,

    /// <summary>LPTSTR is interpreted as ANSI in this class</summary>
    tdAnsiClass = 0x00000000,

    /// <summary>LPTSTR is interpreted as UNICODE</summary>
    tdUnicodeClass = 0x00010000,

    /// <summary>LPTSTR is interpreted automatically</summary>
    tdAutoClass = 0x00020000,

    /// <summary>A non-standard encoding specified by CustomFormatMask</summary>
    tdCustomFormatClass = 0x00030000,

    /// <summary>Use this mask to retrieve non-standard encoding information for native interop. The meaning of the values of these 2 bits is unspecified.</summary>
    tdCustomFormatMask = 0x00C00000,

    // end string format mask

    /// <summary>Initialize the class any time before first static field access.</summary>
    tdBeforeFieldInit = 0x00100000,

    /// <summary>This ExportedType is a type forwarder.</summary>
    tdForwarder = 0x00200000,

    /// <summary>Flags reserved for runtime use.</summary>
    tdReservedMask = 0x00040800,

    /// <summary>Runtime should check name encoding.</summary>
    tdRTSpecialName = 0x00000800,

    /// <summary>Class has security associate with it.</summary>
    tdHasSecurity = 0x00040000,
}
