// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.SystemVClassificationType;

namespace RyuJitSharp;

// System V struct passing
// The Classification types are described in the ABI spec at https://software.intel.com/sites/default/files/article/402129/mpx-linux64-abi.pdf
public enum SystemVClassificationType : byte
{
    SystemVClassificationTypeUnknown = 0,

    SystemVClassificationTypeStruct = 1,

    SystemVClassificationTypeNoClass = 2,

    SystemVClassificationTypeMemory = 3,

    SystemVClassificationTypeInteger = 4,

    SystemVClassificationTypeIntegerReference = 5,

    SystemVClassificationTypeIntegerByRef = 6,

    SystemVClassificationTypeSSE = 7,

    // Not supported by the CLR.
    // SystemVClassificationTypeSSEUp = Unused,

    // Not supported by the CLR.
    // SystemVClassificationTypeX87 = Unused,

    // Not supported by the CLR.
    // SystemVClassificationTypeX87Up = Unused,

    // Not supported by the CLR.
    // SystemVClassificationTypeComplexX87 = Unused,
}
