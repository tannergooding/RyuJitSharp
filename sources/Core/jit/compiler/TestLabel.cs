// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
global using static RyuJitSharp.TestLabel;

namespace RyuJitSharp;

/// <summary></summary>
/// <remarks>This must be kept identical to System.Runtime.CompilerServices.JitTestLabel.TestLabel.</remarks>
public enum TestLabel
{
    TL_SsaName,

    /// <summary>Defines a "VN equivalence class".  (For full VN, including exceptions thrown).</summary>
    TL_VN,       

    /// <summary>Like above, but uses the non-exceptional value of the expression.</summary>
    TL_VNNorm,   

    /// <summary>This must be identified in the JIT as a CSE def</summary>
    TL_CSE_Def,  

    /// <summary>This must be identified in the JIT as a CSE use</summary>
    TL_CSE_Use,

    /// <summary>Expression must (or must not) be hoisted out of the loop.</summary>
    TL_LoopHoist,
}
#endif
