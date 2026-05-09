// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

/// <summary> Defines the set of flags that may appear in the GenTree.gtLIRFlags field.</summary>
public partial class LIR
{
    [Flags]
    public enum Flags : byte
    {
        None = 0x00,

        /// <summary>An arbitrary "mark" bit that can be used in place of a more expensive data structure when processing a set of LIR nodes. See for example `LIR.GetTreeRange`.</summary>
        Mark = 0x01,

        /// <summary>
        ///   <para>Set on a node if it produces a value that is not subsequently used.</para>
        ///   <para>Should never be set on nodes that return `false` for `GenTree.IsValue`.</para>
        ///   <para>Note that this bit should not be assumed to be valid at all points during compilation: it is currently only computed during target-dependent lowering.</para>
        /// </summary>
        UnusedValue = 0x02,
                           
                           

        /// <summary>Set on a node if it produces a value, but does not require a register (i.e. it can be used from memory).</summary>
        RegOptional = 0x04,

#if TARGET_WASM
        /// <summary>Set by lowering on nodes that the RA should allocate into a dedicated register (WASM local), for multiple uses.</summary>
        MultiplyUsed = 0x08,
#endif
    }
}
