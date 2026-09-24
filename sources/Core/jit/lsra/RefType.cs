// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

// The low two bits encode def/use; the remaining bits refine the reference kind.
public enum RefType : byte
{
    RefTypeInvalid = 0x00,
    RefTypeDef = 0x01,
    RefTypeUse = 0x02,
    RefTypeKill = 0x04,
    RefTypeBB = 0x08,
    RefTypeFixedReg = 0x10,
    RefTypeExpUse = 0x22,
    RefTypeParamDef = 0x11,
    RefTypeDummyDef = 0x21,
    RefTypeZeroInit = 0x31,
    RefTypeUpperVectorSave = 0x41,
    RefTypeUpperVectorRestore = 0x42,
    RefTypeKillGCRefs = 0x80,
}

public enum BlockStartOrEnd
{
    BlockPositionStart = 0,
    BlockPositionEnd = 1,
    PositionCount = 2,
}
