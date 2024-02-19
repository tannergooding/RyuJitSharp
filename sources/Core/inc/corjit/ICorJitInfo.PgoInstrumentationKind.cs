// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct ICorJitInfo
{
    public enum PgoInstrumentationKind
    {
        // This must be kept in sync with PgoInstrumentationKind in PgoFormat.cs

        //
        // Schema data types
        //

        None = 0,

        FourByte = 1,

        EightByte = 2,

        TypeHandle = 3,

        MethodHandle = 4,

        // Mask of all schema data types
        MarshalMask = 0xF,

        //
        // ExcessAlignment
        //

        Align4Byte = 0x10,

        Align8Byte = 0x20,

        AlignPointer = 0x30,

        // Mask of all schema alignment types
        AlignMask = 0x30,

        DescriptorMin = 0x40,

        // All instrumentation schemas must end with a record which is "Done"
        Done = None,

        // basic block counter using unsigned 4 byte int
        BasicBlockIntCount = (DescriptorMin * 1) | FourByte,

        // basic block counter using unsigned 8 byte int
        BasicBlockLongCount = (DescriptorMin * 1) | EightByte,

        // 4 byte counter that is part of a type histogram. Aligned to match HandleHistogram32's alignment.
        HandleHistogramIntCount = (DescriptorMin * 2) | FourByte | AlignPointer,

        // 8 byte counter that is part of a type histogram
        HandleHistogramLongCount = (DescriptorMin * 2) | EightByte,

        // Histogram of type handles
        HandleHistogramTypes = (DescriptorMin * 3) | TypeHandle,

        // Histogram of method handles
        HandleHistogramMethods = (DescriptorMin * 3) | MethodHandle,

        // Version is encoded in the Other field of the schema
        Version = (DescriptorMin * 4) | None,

        // Number of runs is encoded in the Other field of the schema
        NumRuns = (DescriptorMin * 5) | None,

        // edge counter using unsigned 4 byte int
        EdgeIntCount = (DescriptorMin * 6) | FourByte,

        // edge counter using unsigned 8 byte int
        EdgeLongCount = (DescriptorMin * 6) | EightByte,

        // Compressed get likely class data
        GetLikelyClass = (DescriptorMin * 7) | TypeHandle,

        // Compressed get likely method data
        GetLikelyMethod = (DescriptorMin * 7) | MethodHandle,

        // Same as type/method histograms, but for generic integer values
        ValueHistogramIntCount = (DescriptorMin * 8) | FourByte | AlignPointer,

        ValueHistogramLongCount = (DescriptorMin * 8) | EightByte,

        ValueHistogram = (DescriptorMin * 9) | EightByte,
    }
}
