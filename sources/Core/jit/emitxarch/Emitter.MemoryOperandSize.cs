// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
namespace RyuJitSharp;

public partial class Emitter
{
    public emitAttr emitGetMemOpSize(instrDesc id, bool ignoreEmbeddedBroadcast)
    {
        nint memSize;

        var ins = id.idIns();
        var defaultSize = id.idOpSize();
        var tupleType = insTupleTypeInfo(ins);

        if (tupleType == INS_TT_NONE)
        {
            memSize = (int)defaultSize;
        }
        else if (tupleType == INS_TT_FULL)
        {
            if (!ignoreEmbeddedBroadcast && HasEmbeddedBroadcast(id))
            {
                memSize = GetInputSizeInBytes(id);
            }
            else
            {
                memSize = (int)defaultSize;
            }
        }
        else if (tupleType == (INS_TT_FULL | INS_TT_MEM128))
        {
            // An immediate operand selects a full-vector load, optionally broadcast from a scalar.
            // Without an immediate operand this tuple instead loads 128 bits.
            if (!id.idHasMemAndCns())
            {
                memSize = 16;
            }
            else if (!ignoreEmbeddedBroadcast && HasEmbeddedBroadcast(id))
            {
                memSize = GetInputSizeInBytes(id);
            }
            else
            {
                memSize = (int)defaultSize;
            }
        }
        else if (tupleType == INS_TT_HALF)
        {
            if (!ignoreEmbeddedBroadcast && HasEmbeddedBroadcast(id))
            {
                memSize = GetInputSizeInBytes(id);
            }
            else
            {
                memSize = (int)defaultSize / 2;
            }
        }
        else if (tupleType == INS_TT_FULL_MEM)
        {
            memSize = (int)defaultSize;
        }
        else if (tupleType == (INS_TT_FULL_MEM | INS_TT_MEM128))
        {
            // The immediate form loads a full vector; otherwise it loads 128 bits.
            if (!id.idHasMemAndCns())
            {
                memSize = 16;
            }
            else
            {
                memSize = (int)defaultSize;
            }
        }
        else if (tupleType is INS_TT_TUPLE1_SCALAR or INS_TT_TUPLE1_FIXED)
        {
            memSize = GetInputSizeInBytes(id);
        }
        else if (tupleType == INS_TT_TUPLE2)
        {
            memSize = GetInputSizeInBytes(id) * 2;
        }
        else if (tupleType == INS_TT_TUPLE4)
        {
            memSize = GetInputSizeInBytes(id) * 4;
        }
        else if (tupleType == INS_TT_TUPLE8)
        {
            memSize = GetInputSizeInBytes(id) * 8;
        }
        else if (tupleType == INS_TT_HALF_MEM)
        {
            memSize = (int)defaultSize / 2;
        }
        else if (tupleType == INS_TT_QUARTER_MEM)
        {
            memSize = (int)defaultSize / 4;
        }
        else if (tupleType == INS_TT_EIGHTH_MEM)
        {
            memSize = (int)defaultSize / 8;
        }
        else if (tupleType == INS_TT_MEM128)
        {
            memSize = 16;
        }
        else if (tupleType == INS_TT_MOVDDUP)
        {
            memSize = defaultSize == EA_16BYTE ? 8 : (int)defaultSize;
        }
        else
        {
            throw new FatalJitException("Unexpected memory operand tuple type.");
        }

        return (emitAttr)(int)memSize;
    }
}
#endif
