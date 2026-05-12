// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public struct lvaStructFieldInfo
    {
        /// <summary>Class handle for simd type recognition, see CORINFO_TYPE_LAYOUT_NODE for more details on the restrictions.</summary>
        public unsafe CORINFO_CLASS_HANDLE fldSimdTypeHnd;
        public byte fldOffset;
        public byte fldOrdinal;
        public var_types fldType;
        public int fldSize;

#if DEBUG
        /// <summary>Field handle for diagnostic purposes only. See CORINFO_TYPE_LAYOUT_NODE.</summary>
        public unsafe CORINFO_FIELD_HANDLE diagFldHnd;
#endif
    }
}
