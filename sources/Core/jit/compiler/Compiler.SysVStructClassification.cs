// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if UNIX_AMD64_ABI
namespace RyuJitSharp;

public partial class Compiler
{
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
}
#endif
