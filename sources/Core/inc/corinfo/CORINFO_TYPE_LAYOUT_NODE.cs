// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public unsafe struct CORINFO_TYPE_LAYOUT_NODE
{
    // Type handle if this is a SIMD type, i.e. for intrinsic types in
    // System.Numerics and System.Runtime.Intrinsics namespaces. This handle
    // should be used for SIMD type recognition ONLY. During prejitting the
    // returned handle cannot safely be used for arbitrary JIT-EE calls. The
    // safe operations on this handle are:
    // - getClassNameFromMetadata
    // - getClassSize
    // - getHfaType
    // - getTypeInstantiationArgument, but only under the assumption that the returned type handle
    //   is used for primitive type recognition via getTypeForPrimitiveNumericClass
    public CORINFO_CLASS_HANDLE simdTypeHnd;

    // Field handle that should only be used for diagnostic purposes. During
    // prejit we cannot allow arbitrary JIT-EE calls with this field handle, but it can be used
    // for diagnostic purposes (e.g. to obtain the field name).
    public CORINFO_FIELD_HANDLE diagFieldHnd;

    // Index of parent node in the tree
    public uint parent;

    // Offset into the root type of the field
    public uint offset;

    // Size of the type.
    public uint size;

    // Number of fields for type == CORINFO_TYPE_VALUECLASS. This is the number of nodes added.
    public uint numFields;

    // Type of the field.
    public CorInfoType type;

    // For type == CORINFO_TYPE_VALUECLASS indicates whether the type has significant padding.
    // That is, whether or not the JIT always needs to preserve data stored in
    // the parts that are not covered by fields.
    public bool hasSignificantPadding;
}
