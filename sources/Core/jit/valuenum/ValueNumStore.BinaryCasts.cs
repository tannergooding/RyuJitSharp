// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public sealed partial class ValueNumStore
{
    private ValueNum EvalCastForConstantArgs(var_types type, VNFunc func, ValueNum arg0VN, ValueNum arg1VN)
    {
        assert(VNFuncIsNumericCast(func));
        assert(IsVNConstant(arg0VN) && IsVNConstant(arg1VN));

        if (varTypeIsSmall(type))
        {
            type = TYP_INT;
        }

        var sourceType = TypeOfVN(arg0VN);
        if (IsVNHandle(arg0VN))
        {
            assert(type == TYP_I_IMPL);
        }

        GetCastOperFromVN(arg1VN, out var castToType, out var srcIsUnsigned);
        var checkedCast = func == VNF_CastOvf;

        switch (sourceType)
        {
            case TYP_REF when !Configuration.TARGET_64BIT:
            case TYP_BYREF when !Configuration.TARGET_64BIT:
            case TYP_INT:
            {
                var value = GetConstantInt32(arg0VN);
                assert(!checkedCast || !CheckedOps.CastFromIntOverflows(value, castToType, srcIsUnsigned));

                switch (castToType)
                {
                    case TYP_BYTE:
                    {
                        assert(type == TYP_INT);
                        return VNForIntCon(unchecked((sbyte)value));
                    }

                    case TYP_UBYTE:
                    {
                        assert(type == TYP_INT);
                        return VNForIntCon(unchecked((byte)value));
                    }

                    case TYP_SHORT:
                    {
                        assert(type == TYP_INT);
                        return VNForIntCon(unchecked((short)value));
                    }

                    case TYP_USHORT:
                    {
                        assert(type == TYP_INT);
                        return VNForIntCon(unchecked((ushort)value));
                    }

                    case TYP_INT:
                    case TYP_UINT:
                    {
                        assert(type == TYP_INT);
                        return arg0VN;
                    }

                    case TYP_LONG:
                    case TYP_ULONG:
                    {
                        assert(!IsVNHandle(arg0VN));
                        if (Configuration.TARGET_64BIT)
                        {
                            if (type == TYP_BYREF)
                            {
                                return VNForByrefCon(unchecked((nuint)value));
                            }

                            assert(type == TYP_LONG);
                        }
                        return VNForLongCon(srcIsUnsigned ? unchecked((uint)value) : value);
                    }

                    case TYP_BYREF:
                    {
                        assert(type == TYP_BYREF);
                        return VNForByrefCon(unchecked((nuint)value));
                    }

                    case TYP_FLOAT:
                    {
                        assert(type == TYP_FLOAT);
                        return VNForFloatCon(srcIsUnsigned ? (float)unchecked((uint)value) : (float)value);
                    }

                    case TYP_DOUBLE:
                    {
                        assert(type == TYP_DOUBLE);
                        return VNForDoubleCon(srcIsUnsigned ? (double)unchecked((uint)value) : (double)value);
                    }

                    default:
                    {
                        JitExceptionHelpers.ThrowUnreachableException();
                        return default;
                    }
                }
            }

            case TYP_REF when Configuration.TARGET_64BIT:
            case TYP_BYREF when Configuration.TARGET_64BIT:
            case TYP_LONG:
            {
                var value = GetConstantInt64(arg0VN);
                assert(!checkedCast || !CheckedOps.CastFromLongOverflows(value, castToType, srcIsUnsigned));

                switch (castToType)
                {
                    case TYP_BYTE:
                    {
                        assert(type == TYP_INT);
                        return VNForIntCon(unchecked((sbyte)value));
                    }

                    case TYP_UBYTE:
                    {
                        assert(type == TYP_INT);
                        return VNForIntCon(unchecked((byte)value));
                    }

                    case TYP_SHORT:
                    {
                        assert(type == TYP_INT);
                        return VNForIntCon(unchecked((short)value));
                    }

                    case TYP_USHORT:
                    {
                        assert(type == TYP_INT);
                        return VNForIntCon(unchecked((ushort)value));
                    }

                    case TYP_INT:
                    {
                        assert(type == TYP_INT);
                        return VNForIntCon(unchecked((int)value));
                    }

                    case TYP_UINT:
                    {
                        assert(type == TYP_INT);
                        return VNForIntCon(unchecked((int)(uint)value));
                    }

                    case TYP_LONG:
                    case TYP_ULONG:
                    {
                        assert(type == TYP_LONG);
                        return arg0VN;
                    }

                    case TYP_BYREF:
                    {
                        assert(type == TYP_BYREF);
                        return VNForByrefCon(unchecked((nuint)value));
                    }

                    case TYP_FLOAT:
                    {
                        assert(type == TYP_FLOAT);
                        return VNForFloatCon(srcIsUnsigned ? (float)unchecked((ulong)value) : (float)value);
                    }

                    case TYP_DOUBLE:
                    {
                        assert(type == TYP_DOUBLE);
                        return VNForDoubleCon(srcIsUnsigned ? (double)unchecked((ulong)value) : (double)value);
                    }

                    default:
                    {
                        JitExceptionHelpers.ThrowUnreachableException();
                        return default;
                    }
                }
            }

            case TYP_FLOAT:
            {
                var value = GetConstantSingle(arg0VN);
                assert(!CheckedOps.CastFromFloatOverflows(value, castToType));

                switch (castToType)
                {
                    case TYP_BYTE:
                    {
                        assert(type == TYP_INT);
                        return VNForIntCon(unchecked((sbyte)value));
                    }

                    case TYP_UBYTE:
                    {
                        assert(type == TYP_INT);
                        return VNForIntCon(unchecked((byte)value));
                    }

                    case TYP_SHORT:
                    {
                        assert(type == TYP_INT);
                        return VNForIntCon(unchecked((short)value));
                    }

                    case TYP_USHORT:
                    {
                        assert(type == TYP_INT);
                        return VNForIntCon(unchecked((ushort)value));
                    }

                    case TYP_INT:
                    {
                        assert(type == TYP_INT);
                        return VNForIntCon(unchecked((int)value));
                    }

                    case TYP_UINT:
                    {
                        assert(type == TYP_INT);
                        return VNForIntCon(unchecked((int)(uint)value));
                    }

                    case TYP_LONG:
                    {
                        assert(type == TYP_LONG);
                        return VNForLongCon(unchecked((long)value));
                    }

                    case TYP_ULONG:
                    {
                        assert(type == TYP_LONG);
                        return VNForLongCon(unchecked((long)(ulong)value));
                    }

                    case TYP_FLOAT:
                    {
                        assert(type == TYP_FLOAT);
                        return VNForFloatCon(value);
                    }

                    case TYP_DOUBLE:
                    {
                        assert(type == TYP_DOUBLE);
                        return VNForDoubleCon((double)value);
                    }

                    default:
                    {
                        JitExceptionHelpers.ThrowUnreachableException();
                        return default;
                    }
                }
            }

            case TYP_DOUBLE:
            {
                var value = GetConstantDouble(arg0VN);
                assert(!CheckedOps.CastFromDoubleOverflows(value, castToType));

                switch (castToType)
                {
                    case TYP_BYTE:
                    {
                        assert(type == TYP_INT);
                        return VNForIntCon(unchecked((sbyte)value));
                    }

                    case TYP_UBYTE:
                    {
                        assert(type == TYP_INT);
                        return VNForIntCon(unchecked((byte)value));
                    }

                    case TYP_SHORT:
                    {
                        assert(type == TYP_INT);
                        return VNForIntCon(unchecked((short)value));
                    }

                    case TYP_USHORT:
                    {
                        assert(type == TYP_INT);
                        return VNForIntCon(unchecked((ushort)value));
                    }

                    case TYP_INT:
                    {
                        assert(type == TYP_INT);
                        return VNForIntCon(unchecked((int)value));
                    }

                    case TYP_UINT:
                    {
                        assert(type == TYP_INT);
                        return VNForIntCon(unchecked((int)(uint)value));
                    }

                    case TYP_LONG:
                    {
                        assert(type == TYP_LONG);
                        return VNForLongCon(unchecked((long)value));
                    }

                    case TYP_ULONG:
                    {
                        assert(type == TYP_LONG);
                        return VNForLongCon(unchecked((long)(ulong)value));
                    }

                    case TYP_FLOAT:
                    {
                        assert(type == TYP_FLOAT);
                        return VNForFloatCon((float)value);
                    }

                    case TYP_DOUBLE:
                    {
                        assert(type == TYP_DOUBLE);
                        return VNForDoubleCon(value);
                    }

                    default:
                    {
                        JitExceptionHelpers.ThrowUnreachableException();
                        return default;
                    }
                }
            }

            default:
            {
                JitExceptionHelpers.ThrowUnreachableException();
                return default;
            }
        }
    }

    private ValueNum EvalBitCastForConstantArgs(var_types dstType, ValueNum arg0VN)
    {
        assert(!IsVNHandle(arg0VN));
        var srcType = TypeOfVN(arg0VN);
        assert((srcType.Size == dstType.Size) || (varTypeIsSmall(dstType) && (srcType == TYP_INT)));

        Span<byte> bytes = stackalloc byte[8];
        bytes.Clear();
        switch (srcType)
        {
            case TYP_INT:
            {
                var value = ConstantValue<int>(arg0VN);
                MemoryMarshal.Write(bytes, in value);
                break;
            }

            case TYP_LONG:
            {
                var value = ConstantValue<long>(arg0VN);
                MemoryMarshal.Write(bytes, in value);
                break;
            }

            case TYP_BYREF:
            {
                var value = ConstantValue<nuint>(arg0VN);
                MemoryMarshal.Write(bytes, in value);
                break;
            }

            case TYP_REF:
            {
                assert(arg0VN == VNForNull());
                nuint value = 0;
                MemoryMarshal.Write(bytes, in value);
                break;
            }

            case TYP_FLOAT:
            {
                var value = ConstantValue<float>(arg0VN);
                MemoryMarshal.Write(bytes, in value);
                break;
            }

            case TYP_DOUBLE:
            {
                var value = ConstantValue<double>(arg0VN);
                MemoryMarshal.Write(bytes, in value);
                break;
            }

            case TYP_SIMD8 when Configuration.FEATURE_SIMD:
            {
                var value = GetConstantSimd8(arg0VN);
                MemoryMarshal.Write(bytes, in value);
                break;
            }

            default:
            {
                JitExceptionHelpers.ThrowUnreachableException();
                return default;
            }
        }

        var intValue = MemoryMarshal.Read<int>(bytes);
        if (varTypeIsSmall(dstType))
        {
            assert(FitsIn(varTypeToSigned(dstType), intValue) || FitsIn(varTypeToUnsigned(dstType), intValue));
        }

        switch (dstType)
        {
            case TYP_UBYTE:
                return VNForIntCon(unchecked((byte)intValue));
            case TYP_BYTE:
                return VNForIntCon(unchecked((sbyte)intValue));
            case TYP_USHORT:
                return VNForIntCon(unchecked((ushort)intValue));
            case TYP_SHORT:
                return VNForIntCon(unchecked((short)intValue));
            case TYP_INT:
                return VNForIntCon(intValue);
            case TYP_LONG:
                return VNForLongCon(MemoryMarshal.Read<long>(bytes));
            case TYP_BYREF:
                return VNForByrefCon(MemoryMarshal.Read<nuint>(bytes));
            case TYP_FLOAT:
                return VNForFloatCon(MemoryMarshal.Read<float>(bytes));
            case TYP_DOUBLE:
                return VNForDoubleCon(MemoryMarshal.Read<double>(bytes));
            case TYP_SIMD8 when Configuration.FEATURE_SIMD:
                return VNForSimd8Con(MemoryMarshal.Read<simd8_t>(bytes));
            default:
                JitExceptionHelpers.ThrowUnreachableException();
                return default;
        }
    }
}
