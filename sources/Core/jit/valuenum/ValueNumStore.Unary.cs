// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public sealed partial class ValueNumStore
{
    private Dictionary<(VNFunc Func, ValueNum Arg0), ValueNum>? _vnFunc1Map;
    private readonly Dictionary<FieldSeq, nint> _fieldSeqIds = [with(ReferenceEqualityComparer.Instance)];
    private readonly List<FieldSeq> _fieldSeqs = [];

    public ValueNum VNForFieldSeq(FieldSeq? fieldSeq)
    {
        nint identity = 0;
        if ((fieldSeq is not null) && !_fieldSeqIds.TryGetValue(fieldSeq, out identity))
        {
            // Compiler-owned tokens preserve canonical reference identity without exposing movable addresses.
            identity = _fieldSeqs.Count + (nint)1;
            _fieldSeqs.Add(fieldSeq);
            _fieldSeqIds.Add(fieldSeq, identity);
        }

        var vn = VNForHandle(identity, GTF_ICON_FIELD_SEQ);
#if DEBUG
        if (_compiler.verbose)
        {
            // Match vnDump's symbolic field-sequence branch; identity tokens are never printed.
            jitprintf("     {");
            _compiler.gtDispFieldSeq(fieldSeq, 0);
            jitprintf($" }} is ${vn:x}\n");
        }
#endif
        return vn;
    }

    public FieldSeq? FieldSeqVNToFieldSeq(ValueNum vn)
    {
        assert(IsVNHandle(vn, GTF_ICON_FIELD_SEQ));
        var identity = ConstantValue<nint>(vn);
        return identity == 0 ? null : _fieldSeqs[checked((int)(identity - 1))];
    }

    public bool IsVNNewArr(ValueNum vn, ref VNFuncApp funcApp)
        => GetVNFunc(vn, ref funcApp) &&
            funcApp.FuncIs(VNF_JitNewArr, VNF_JitNewLclArr, VNF_JitReadyToRunNewArr, VNF_JitReadyToRunNewLclArr);

    public bool IsVNNewLocalArr(ValueNum vn, ref VNFuncApp funcApp)
        => GetVNFunc(vn, ref funcApp) && funcApp.FuncIs(VNF_JitNewLclArr, VNF_JitReadyToRunNewLclArr);

    public bool TryGetNewArrSize(ValueNum vn, out int size)
    {
        var app = new VNFuncApp();
        if (IsVNNewArr(vn, ref app) && IsVNConstant(app.GetArg(1)))
        {
            var value = CoercedConstantValue<nint>(app.GetArg(1));
            if (unchecked((nuint)value) <= int.MaxValue)
            {
                size = (int)value;
                return true;
            }
        }

        size = 0;
        return false;
    }

    public unsafe ValueNum VNForFunc(var_types type, VNFunc func, ValueNum arg0VN)
    {
        assert(func != VNF_MemOpaque);
        assert(arg0VN == VNNormalValue(arg0VN));
        _vnFunc1Map ??= [];
        var key = (func, arg0VN);
        if (_vnFunc1Map.TryGetValue(key, out var result) && (result != NoVN))
        {
            return result;
        }

        // Folding may recursively intern functions, so do not retain a dictionary entry ref.
        result = NoVN;
        _vnFunc1Map[key] = result;
        if (func == VNF_ARR_LENGTH)
        {
            var addressVN = VNNormalValue(arg0VN);
            if (IsVNObjHandle(addressVN))
            {
                var handle = CoercedConstantValue<nuint>(addressVN);
                var length = _compiler.info.compCompHnd->getArrayOrStringLength((CORINFO_OBJECT_HANDLE)handle);
                if (length >= 0)
                {
                    result = VNForIntCon(length);
                }
            }

            var app = new VNFuncApp();
            if ((result == NoVN) && GetVNFunc(addressVN, ref app) && app.FuncIs(VNF_InvariantNonNullLoad))
            {
                var fieldSeqVN = VNNormalValue(app.GetArg(0));
                if (IsVNHandle(fieldSeqVN, GTF_ICON_FIELD_SEQ) && (FieldSeqVNToFieldSeq(fieldSeqVN) is FieldSeq fieldSeq))
                {
                    var field = fieldSeq.FieldHandle;
                    if (field != null)
                    {
                        // Zero the host-width handle before copying target-width data, including crossgen.
                        CORINFO_OBJECT_HANDLE handle = null;
                        if (_compiler.info.compCompHnd->getStaticFieldContent(field, (byte*)&handle, TARGET_POINTER_SIZE, 0, false))
                        {
                            var length = _compiler.info.compCompHnd->getArrayOrStringLength(handle);
                            if (length >= 0)
                            {
                                result = VNForIntCon(length);
                            }
                        }
                    }
                }
            }

            if ((result == NoVN) && TryGetNewArrSize(addressVN, out var knownSize))
            {
                result = VNForIntCon(knownSize);
            }

            if (GetVNFunc(arg0VN, ref app) && app.FuncIs(VNF_JitNewArr, VNF_StrFastAllocate))
            {
                var actualSize = VNIgnoreIntToLongCast(app.GetArg(1));
                if (TypeOfVN(actualSize) == TYP_INT)
                {
                    result = actualSize;
                }
            }
        }
        else if (func == VNF_NOT)
        {
            var app = new VNFuncApp();
            if (GetVNFunc(arg0VN, ref app) && app.FuncIs(VNF_NOT))
            {
                result = app.GetArg(0);
            }
        }

        if ((result == NoVN) && VNEvalCanFoldUnaryFunc(type, func, arg0VN))
        {
            result = EvalFuncForConstantArgs(type, func, arg0VN);
        }

        if (result == NoVN)
        {
            var chunk = GetAllocChunk(type, ChunkExtraAttribs.CEA_Func1);
            var offset = chunk.AllocVN();
            var record = chunk.FuncApp(offset, 1).Span;
            record[0] = (int)func;
            record[1] = arg0VN;
            result = unchecked(chunk.BaseVN + offset);
        }

        _vnFunc1Map[key] = result;
        return result;
    }

    private bool VNEvalCanFoldUnaryFunc(var_types type, VNFunc func, ValueNum arg0VN)
        => IsVNConstant(arg0VN) && (func < VNF_Boundary) &&
            ((genTreeOps)func is GT_NEG or GT_NOT or GT_BSWAP16 or GT_BSWAP);

    private static T EvalIntegralOp<T>(VNFunc func, T value) where T : unmanaged, IBinaryInteger<T>
    {
        switch (func)
        {
            case VNF_NEG:
            {
                return unchecked(-value);
            }

            case VNF_NOT:
            {
                return ~value;
            }

            case VNF_BSWAP16:
            {
                return T.CreateTruncating(BinaryPrimitives.ReverseEndianness(ushort.CreateTruncating(value)));
            }

            case VNF_BSWAP:
            {
                if (Unsafe.SizeOf<T>() == 4)
                {
                    return T.CreateTruncating(BinaryPrimitives.ReverseEndianness(uint.CreateTruncating(value)));
                }

                if (Unsafe.SizeOf<T>() == 8)
                {
                    return T.CreateTruncating(BinaryPrimitives.ReverseEndianness(ulong.CreateTruncating(value)));
                }

                break;
            }
        }

        noway_assert(false);
        return value;
    }

    private ValueNum EvalFuncForConstantArgs(var_types type, VNFunc func, ValueNum arg0VN)
    {
        assert(VNEvalCanFoldUnaryFunc(type, func, arg0VN));
        switch (TypeOfVN(arg0VN))
        {
            case TYP_INT:
            {
                return VNForIntCon(EvalIntegralOp(func, ConstantValue<int>(arg0VN)));
            }

            case TYP_LONG:
            {
                return VNForLongCon(EvalIntegralOp(func, ConstantValue<long>(arg0VN)));
            }

            case TYP_FLOAT:
            {
                if (func == VNF_NEG)
                {
                    return VNForFloatCon(-ConstantValue<float>(arg0VN));
                }

                noway_assert(false);
                return VNForFloatCon(0);
            }

            case TYP_DOUBLE:
            {
                if (func == VNF_NEG)
                {
                    return VNForDoubleCon(-ConstantValue<double>(arg0VN));
                }

                noway_assert(false);
                return VNForDoubleCon(0);
            }

            case TYP_REF:
            {
                assert(!VNHasExc(arg0VN));
                assert(arg0VN == VNForNull());
                assert(func is VNF_ARR_LENGTH or VNF_MDArrLength or VNF_MDARR_LOWER_BOUND);
                return VNWithExc(VNForVoid(), VNExcSetSingleton(VNForFunc(TYP_REF, VNF_NullPtrExc, VNForNull())));
            }
        }

        noway_assert(false);
        return NoVN;
    }
}
