// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    public static insTupleType insTupleTypeInfo(instruction ins)
    {
        if (!Configuration.TARGET_XARCH)
        {
            throw new FatalJitException("xarch instruction tuple information is not available for this target.");
        }

        assert((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins) < (uint)s_tupleTypes.Length);

        return s_tupleTypes[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)];
    }
}
