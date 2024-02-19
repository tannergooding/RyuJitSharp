// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct ICorDebugInfo
{
    public struct OffsetMapping
    {
        public uint nativeOffset;

        /// <summary>IL offset or one of the special values in MappingTypes.</summary>
        public uint ilOffset;

        // The debugger needs this so that
        // we don't put Edit and Continue breakpoints where
        // the stack isn't empty. We can put regular breakpoints
        // there, though, so we need a way to discriminate
        // between offsets.
        public SourceTypes source; 
    }
}
