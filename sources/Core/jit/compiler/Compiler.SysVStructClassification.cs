// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if UNIX_AMD64_ABI
namespace RyuJitSharp;

public partial class Compiler
{
    private static var_types GetTypeFromClassificationAndSizes(SystemVClassificationType classType, int size)
    {
        var type = TYP_UNKNOWN;
        switch (classType)
        {
            case SystemVClassificationTypeInteger:
            {
                if (size is 1)
                {
                    type = TYP_BYTE;
                }
                else if (size <= 2)
                {
                    type = TYP_SHORT;
                }
                else if (size <= 4)
                {
                    type = TYP_INT;
                }
                else if (size <= 8)
                {
                    type = TYP_LONG;
                }
                else
                {
                    assert(false, "GetTypeFromClassificationAndSizes Invalid Integer classification type.");
                }
                break;
            }

            case SystemVClassificationTypeIntegerReference:
            {
                type = TYP_REF;
                break;
            }

            case SystemVClassificationTypeIntegerByRef:
            {
                type = TYP_BYREF;
                break;
            }

            case SystemVClassificationTypeSSE:
            {
                if (size <= 4)
                {
                    type = TYP_FLOAT;
                }
                else if (size <= 8)
                {
                    type = TYP_DOUBLE;
                }
                else
                {
                    assert(false, "GetTypeFromClassificationAndSizes Invalid SSE classification type.");
                }
                break;
            }

            default:
            {
                assert(false, "GetTypeFromClassificationAndSizes Invalid classification type.");
                break;
            }
        }

        return type;
    }

    /// <summary>Return the type of an eightbyte slot in a SysV struct descriptor.</summary>
    public var_types GetEightByteType(in SYSTEMV_AMD64_CORINFO_STRUCT_REG_PASSING_DESCRIPTOR structDesc, int slotNum)
    {
        var eightByteType = TYP_UNDEF;
        var size = structDesc.eightByteSizes[slotNum];

        switch (structDesc.eightByteClassifications[slotNum])
        {
            case SystemVClassificationTypeInteger:
            {
                if (size <= 4)
                {
                    eightByteType = TYP_INT;
                }
                else if (size <= 8)
                {
                    eightByteType = TYP_LONG;
                }
                else
                {
                    assert(false);
                }
                break;
            }
            case SystemVClassificationTypeIntegerReference:
            {
                assert(size == REGSIZE_BYTES);
                eightByteType = TYP_REF;
                break;
            }
            case SystemVClassificationTypeIntegerByRef:
            {
                assert(size == REGSIZE_BYTES);
                eightByteType = TYP_BYREF;
                break;
            }
            case SystemVClassificationTypeSSE:
            {
                if (size <= 4)
                {
                    eightByteType = TYP_FLOAT;
                }
                else if (size <= 8)
                {
                    eightByteType = TYP_DOUBLE;
                }
                else
                {
                    assert(false);
                }
                break;
            }
            default:
            {
                assert(false);
                break;
            }
        }

        return eightByteType;
    }

    private unsafe void GetStructTypeOffset(
        in SYSTEMV_AMD64_CORINFO_STRUCT_REG_PASSING_DESCRIPTOR structDesc,
        var_types* type0,
        var_types* type1,
        byte* offset0,
        byte* offset1)
    {
        *offset0 = structDesc.eightByteOffsets[0];
        *offset1 = structDesc.eightByteOffsets[1];
        *type0 = TYP_UNKNOWN;
        *type1 = TYP_UNKNOWN;

        if (structDesc.eightByteCount >= 1)
        {
            *type0 = GetEightByteType(structDesc, 0);
        }

        if (structDesc.eightByteCount is 2)
        {
            *type1 = GetEightByteType(structDesc, 1);
        }
    }

    private unsafe void GetStructTypeOffset(
        CORINFO_CLASS_STRUCT_* typeHnd,
        var_types* type0,
        var_types* type1,
        byte* offset0,
        byte* offset1)
    {
        eeGetSystemVAmd64PassStructInRegisterDescriptor(typeHnd, out var structDesc);
        assert(structDesc.passedInRegisters);
        GetStructTypeOffset(structDesc, type0, type1, offset0, offset1);
    }
}
#endif
