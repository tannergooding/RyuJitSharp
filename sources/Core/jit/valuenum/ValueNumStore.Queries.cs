// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace RyuJitSharp;

public sealed partial class ValueNumStore
{
    public enum VNVisit
    {
        Continue,
        Abort,
    }

    public ValueNum VNForPhiDef(var_types type, int lclNum, int ssaDef, ReadOnlySpan<int> ssaArgs)
    {
        var chunk = GetAllocChunk(type, ChunkExtraAttribs.CEA_PhiDef);
        var offset = chunk.AllocVN();
        ((VNPhiDef[])chunk.Defs)[offset] = new(lclNum, ssaDef, ssaArgs.ToArray());
        return unchecked(chunk.BaseVN + offset);
    }

    public bool GetPhiDef(ValueNum vn, ref VNPhiDef phiDef)
    {
        if (vn == NoVN)
        {
            return false;
        }

        var chunk = _chunks[GetChunkNum(vn)];
        var offset = ChunkOffset(vn);
        assert(offset < chunk.NumUsed);
        if (chunk.Attribs == ChunkExtraAttribs.CEA_PhiDef)
        {
            phiDef = ((VNPhiDef[])chunk.Defs)[offset];
            return true;
        }

        return false;
    }

    public bool IsPhiDef(ValueNum vn)
    {
        if (vn == NoVN)
        {
            return false;
        }

        var chunk = _chunks[GetChunkNum(vn)];
        assert(ChunkOffset(vn) < chunk.NumUsed);
        return chunk.Attribs == ChunkExtraAttribs.CEA_PhiDef;
    }

    public ValueNum VNPhiDefToVN(in VNPhiDef phiDef, int ssaArgNum)
        => _compiler.lvaGetDesc(phiDef.LclNum).GetPerSsaData(phiDef.SsaArgs.Span[ssaArgNum])._vnPair.Conservative;

    public ValueNum VNForMemoryPhiDef(BasicBlock block, ReadOnlySpan<int> ssaArgs)
    {
        var args = ssaArgs.ToArray();
        var chunk = GetAllocChunk(TYP_HEAP, ChunkExtraAttribs.CEA_MemoryPhiDef);
        var offset = chunk.AllocVN();
        ((VNMemoryPhiDef[])chunk.Defs)[offset] = new(block, args);
        return unchecked(chunk.BaseVN + offset);
    }

    public bool GetMemoryPhiDef(ValueNum vn, ref VNMemoryPhiDef memoryPhiDef)
    {
        if (vn == NoVN)
        {
            return false;
        }

        var chunk = _chunks[GetChunkNum(vn)];
        var offset = ChunkOffset(vn);
        assert(offset < chunk.NumUsed);
        if (chunk.Attribs == ChunkExtraAttribs.CEA_MemoryPhiDef)
        {
            memoryPhiDef = ((VNMemoryPhiDef[])chunk.Defs)[offset];
            return true;
        }

        return false;
    }

    public FlowGraphNaturalLoop? LoopOfVN(ValueNum vn)
    {
        VNFuncApp funcApp = default;
        VNMemoryPhiDef memoryPhiDef = default;
        if (GetVNFunc(vn, ref funcApp))
        {
            if (funcApp.Func is VNF_MemOpaque)
            {
                var index = funcApp.GetArg(0);
                if ((index == NoLoop) || (index == UnknownLoop))
                {
                    return null;
                }

                assert(_compiler._loops is not null);
                return _compiler._loops.GetLoopByIndex(index);
            }
            else if (funcApp.Func is VNF_MapStore)
            {
                var index = funcApp.GetArg(3);
                if (index == NoLoop)
                {
                    return null;
                }

                assert(_compiler._loops is not null);
                return _compiler._loops.GetLoopByIndex(index);
            }
        }
        else if (GetMemoryPhiDef(vn, ref memoryPhiDef))
        {
            assert(_compiler._blockToLoop is not null);
            return _compiler._blockToLoop.GetLoop(memoryPhiDef.Block);
        }

        return null;
    }

    public VNVisit VNVisitReachingVNs(ValueNum vn, Func<ValueNum, VNVisit> argVisitor)
    {
        if (!IsPhiDef(vn))
        {
            return argVisitor(vn);
        }

        return VNVisitReachingVNsWorker(vn, argVisitor);
    }

    private VNVisit VNVisitReachingVNsWorker(ValueNum vn, Func<ValueNum, VNVisit> argVisitor)
    {
        var toVisit = new Stack<ValueNum>();
        toVisit.Push(vn);
        var visited = new HashSet<ValueNum> { vn };
        while (toVisit.Count > 0)
        {
            var current = toVisit.Pop();
            VNPhiDef phiDef = default;
            if (GetPhiDef(current, ref phiDef))
            {
                for (var index = 0; index < phiDef.SsaArgs.Length; index++)
                {
                    var child = VNPhiDefToVN(phiDef, index);
                    if (visited.Add(child))
                    {
                        toVisit.Push(child);
                    }
                }
            }
            else if (argVisitor(current) == VNVisit.Abort)
            {
                return VNVisit.Abort;
            }
        }

        return VNVisit.Continue;
    }

    public int GetConstantInt32(ValueNum vn)
    {
        assert(IsVNConstant(vn));
        return TypeOfVN(vn) switch {
            TYP_INT => ConstantValue<int>(vn),
#if !TARGET_64BIT
            TYP_REF or TYP_BYREF => unchecked((int)ConstantValue<nuint>(vn)),
#endif
            _ => throw new UnreachableException(),
        };
    }

    public void PeelOffsets(ref ValueNum vn, out target_ssize_t offset)
    {
#if DEBUG
        var type = TypeOfVN(vn);
        assert(type is TYP_I_IMPL or TYP_REF or TYP_BYREF);
#endif
        offset = 0;
        var app = new VNFuncApp();
        while (GetVNFunc(vn, ref app) && app.FuncIs(VNF_ADD, VNF_Cast))
        {
            if (app.FuncIs(VNF_Cast))
            {
                if (TypeOfVN(vn) is TYP_I_IMPL)
                {
                    GetCastOperFromVN(app.GetArg(1), out var castToType, out _);
                    if (varTypeIsI(castToType) && varTypeIsI(TypeOfVN(app.GetArg(0))))
                    {
                        vn = app.GetArg(0);
                        continue;
                    }
                }

                break;
            }

            // Handles and the null reference are bases, not offsets.
            if (IsVNConstantNonHandle(app.GetArg(0)) && (app.GetArg(0) != VNForNull()))
            {
                offset = unchecked(offset + (target_ssize_t)GetConstantInt64(app.GetArg(0)));
                vn = app.GetArg(1);
            }
            else if (IsVNConstantNonHandle(app.GetArg(1)) && (app.GetArg(1) != VNForNull()))
            {
                offset = unchecked(offset + (target_ssize_t)GetConstantInt64(app.GetArg(1)));
                vn = app.GetArg(0);
            }
            else
            {
                break;
            }
        }
    }

    public void PeelOffsetsI32(ref ValueNum vn, out int offset)
    {
        offset = 0;
        var first = NoVN;
        var second = NoVN;
        while (IsVNBinFunc(vn, VNF_ADD, ref first, ref second))
        {
            if ((TypeOfVN(first) != TYP_INT) || (TypeOfVN(second) != TYP_INT))
            {
                break;
            }

            if (IsVNInt32Constant(first) && !IsVNHandle(first))
            {
                offset = unchecked(offset + ConstantValue<int>(first));
                vn = second;
            }
            else if (IsVNInt32Constant(second) && !IsVNHandle(second))
            {
                offset = unchecked(offset + ConstantValue<int>(second));
                vn = first;
            }
            else
            {
                break;
            }
        }
    }

    public bool IsKnownNonNull(ValueNum vn)
    {
        if (vn == NoVN)
        {
            return false;
        }

        var type = TypeOfVN(vn);
        if (type is not (TYP_I_IMPL or TYP_REF or TYP_BYREF))
        {
            return false;
        }

        PeelOffsets(ref vn, out var offset);
        if ((offset < 0) || _compiler.fgIsBigOffset(unchecked((nint)offset)))
        {
            return false;
        }

        return VNVisitReachingVNs(vn, VisitKnownNonNull) is VNVisit.Continue;
    }

    private VNVisit VisitKnownNonNull(ValueNum vn)
    {
        if (vn != NoVN)
        {
            if (IsVNHandle(vn))
            {
                assert(CoercedConstantValue<nuint>(vn) != 0);
                return VNVisit.Continue;
            }

            var app = new VNFuncApp();
            if (GetVNFunc(vn, ref app) && ((VNFuncExtensions.GetAttributes(app.Func) & VNFOA_KnownNonNull) != 0))
            {
                return VNVisit.Continue;
            }
        }

        return VNVisit.Abort;
    }

    public long GetConstantInt64(ValueNum vn)
    {
        assert(IsVNConstant(vn));
        return TypeOfVN(vn) switch {
            TYP_INT => ConstantValue<int>(vn),
            TYP_LONG => ConstantValue<long>(vn),
            TYP_REF or TYP_BYREF => unchecked((long)ConstantValue<nuint>(vn)),
            _ => throw new UnreachableException(),
        };
    }

    public bool IsVNArrLen(ValueNum vn)
    {
        var app = new VNFuncApp();
        return GetVNFunc(vn, ref app) && app.FuncIs(VNF_ARR_LENGTH, VNF_MDArrLength);
    }

    public bool IsVNHWIntrinsicFunc(ValueNum vn, ref VNFuncApp app, ref NamedIntrinsic id, ref int simdSize, ref var_types simdBaseType)
    {
#if FEATURE_HW_INTRINSICS
        if (!GetVNFunc(vn, ref app) || (app.Func < VNF_HWI_FIRST) || (app.Func > VNF_HWI_LAST))
        {
            return false;
        }

        id = (NamedIntrinsic)((app.Func - VNF_HWI_FIRST) + ((int)NI_HW_INTRINSIC_START + 1));
        simdSize = GetVNHWIntrinsicSizeAndBaseType(app, ref simdBaseType);
        return true;
#else
        return false;
#endif
    }

#if FEATURE_HW_INTRINSICS
    public bool IsVectorPerElementMask(ValueNum vn, var_types simdBaseType, byte simdSize)
    {
        // Keep in sync with GenTree.IsVectorPerElementMask.
        var type = TypeOfVN(vn);
        var elementCount = GenTreeVecCon.ElementCount(simdSize, simdBaseType);
        assert(varTypeIsSimd(type) && (type.Size == simdSize));
        if (IsVNConstant(vn))
        {
            var value = GetConstantSimd(vn);
            return ElementsAreAllBitsSetOrZero(value.AsSpan<byte>(), simdBaseType, elementCount);
        }

        var app = new VNFuncApp();
        NamedIntrinsic id = default;
        var intrinsicSize = 0;
        var intrinsicBaseType = TYP_UNDEF;
        if (!IsVNHWIntrinsicFunc(vn, ref app, ref id, ref intrinsicSize, ref intrinsicBaseType) ||
            (intrinsicSize != simdSize))
        {
            return false;
        }

        if (HWIntrinsicInfo.ReturnsPerElementMask(id))
        {
            // A wider mask lane also supplies narrower masks, but not vice versa:
            // two byte lanes may form 0x00FF rather than a ushort mask.
            return intrinsicBaseType.Size >= simdBaseType.Size;
        }

        var oper = GenTreeHWIntrinsic.GetOperForHWIntrinsicId(id, simdBaseType, out var isScalar);
#if TARGET_ARM64
        if (!isScalar && (oper is GT_EQ or GT_NE or GT_GT or GT_GE or GT_LT or GT_LE))
        {
            return intrinsicBaseType.Size >= simdBaseType.Size;
        }
#endif

        switch (oper)
        {
            case GT_AND:
            case GT_AND_NOT:
            case GT_OR:
            case GT_OR_NOT:
            case GT_XOR:
            case GT_XOR_NOT:
            {
                return IsVectorPerElementMask(app.GetArg(0), simdBaseType, simdSize) &&
                    IsVectorPerElementMask(app.GetArg(1), simdBaseType, simdSize);
            }

            case GT_NOT:
            {
                return IsVectorPerElementMask(app.GetArg(0), simdBaseType, simdSize);
            }

            default:
            {
                assert(!GenTreeHWIntrinsic.OperIsBitwiseHWIntrinsic(oper));
                break;
            }
        }

        return false;
    }

    public int GetVNHWIntrinsicSizeAndBaseType(VNFuncApp app, ref var_types simdBaseType)
    {
        assert((app.Func >= VNF_HWI_FIRST) && (app.Func <= VNF_HWI_LAST));
        assert(app.Arity != 0);
        var simdType = new VNFuncApp();
        var succeeded = GetVNFunc(app.GetArg(app.Arity - 1), ref simdType);
        assert(succeeded && simdType.FuncIs(VNF_SimdType) && (simdType.Arity == 2));
        assert(IsVNConstant(simdType.GetArg(0)) && IsVNConstant(simdType.GetArg(1)));
        simdBaseType = (var_types)GetConstantInt32(simdType.GetArg(1));
        return GetConstantInt32(simdType.GetArg(0));
    }
#endif

    public bool IsVNLog2(ValueNum vn)
    {
        var upperBound = 0;
        return IsVNLog2(vn, ref upperBound);
    }

    public bool IsVNLog2(ValueNum vn, ref int upperBound)
    {
#if FEATURE_HW_INTRINSICS && (TARGET_XARCH || TARGET_ARM64)
        var xorBy = 0;
        var op = NoVN;
        if (IsVNBinFuncWithConst(vn, VNF_XOR, ref op, ref xorBy) && (xorBy is 31 or 63))
        {
            var unused = NoVN;
            _ = IsVNBinFunc(op, VNF_Cast, ref op, ref unused);
#if TARGET_XARCH
            var lzcntFunc = xorBy == 31 ? VNF_HWI_AVX2_LeadingZeroCount : VNF_HWI_AVX2_X64_LeadingZeroCount;
#else
            var lzcntFunc = xorBy == 31 ? VNF_HWI_ArmBase_LeadingZeroCount : VNF_HWI_ArmBase_Arm64_LeadingZeroCount;
#endif
            var orBy = 0;
            if (IsVNBinFunc(op, lzcntFunc, ref op, ref unused) && IsVNBinFuncWithConst(op, VNF_OR, ref op, ref orBy) && (orBy == 1))
            {
                upperBound = xorBy;
                return true;
            }
        }
#endif
        return false;
    }

    // Signed non-negativity visits conservative reaching values, not the phi itself.
    public bool IsVNNeverNegative(ValueNum vn)
        => VNVisitReachingVNs(vn, VisitNeverNegative) == VNVisit.Continue;

    private VNVisit VisitNeverNegative(ValueNum vn)
    {
        if ((vn == NoVN) || !varTypeIsIntegral(TypeOfVN(vn)))
        {
            return VNVisit.Abort;
        }

        if (IsVNConstant(vn))
        {
            return TypeOfVN(vn) switch {
                TYP_INT => GetConstantInt32(vn) >= 0 ? VNVisit.Continue : VNVisit.Abort,
                TYP_LONG => GetConstantInt64(vn) >= 0 ? VNVisit.Continue : VNVisit.Abort,
                _ => VNVisit.Abort,
            };
        }

        if (IsVNArrLen(vn))
        {
            return VNVisit.Continue;
        }

        // TODO-VN: Recognize Span.Length and intrinsics such as Max of nonnegative values.
        var app = new VNFuncApp();
        if (GetVNFunc(vn, ref app))
        {
#if FEATURE_HW_INTRINSICS
            var id = NI_Illegal;
            var simdSize = 0;
            var simdBaseType = TYP_UNDEF;
            if (IsVNHWIntrinsicFunc(vn, ref app, ref id, ref simdSize, ref simdBaseType))
            {
                if (HWIntrinsicInfo.ReturnsBoolean(id) ||
                    (HWIntrinsicInfo.ReturnsScalarT(id) && (simdBaseType is TYP_UBYTE or TYP_USHORT)))
                {
                    return VNVisit.Continue;
                }
            }
#endif
            switch (app.Func)
            {
                case VNF_GT:
                case VNF_GE:
                case VNF_LT:
                case VNF_LE:
                case VNF_EQ:
                case VNF_NE:
                case VNF_GE_UN:
                case VNF_GT_UN:
                case VNF_LE_UN:
                case VNF_LT_UN:
                case VNF_MDArrLowerBound:
                    return VNVisit.Continue;

#if FEATURE_HW_INTRINSICS
                case VNF_HWI_Vector_ExtractMostSignificantBits:
#if TARGET_XARCH
                case VNF_HWI_X86Base_MoveMask:
                case VNF_HWI_AVX_MoveMask:
                case VNF_HWI_AVX2_MoveMask:
                case VNF_HWI_AVX512_MoveMask:
#endif
                {
                    var baseType = TYP_UNDEF;
                    var size = GetVNHWIntrinsicSizeAndBaseType(app, ref baseType);
                    // One bit per element; the native proof is limited to at most 16 bits.
                    if ((size / baseType.Size) <= 16)
                    {
                        return VNVisit.Continue;
                    }
                    break;
                }

#if TARGET_XARCH
                case VNF_HWI_X86Base_PopCount:
                case VNF_HWI_X86Base_X64_PopCount:
                case VNF_HWI_AVX2_LeadingZeroCount:
                case VNF_HWI_AVX2_TrailingZeroCount:
                case VNF_HWI_AVX2_X64_LeadingZeroCount:
                case VNF_HWI_AVX2_X64_TrailingZeroCount:
#elif TARGET_ARM64
                case VNF_HWI_ArmBase_LeadingZeroCount:
                case VNF_HWI_ArmBase_Arm64_LeadingZeroCount:
                case VNF_HWI_ArmBase_Arm64_LeadingSignCount:
#endif
#if TARGET_XARCH || TARGET_ARM64
                    return VNVisit.Continue;
#endif
                case VNF_XOR:
                {
                    if (IsVNLog2(vn))
                    {
                        return VNVisit.Continue;
                    }
                    break;
                }
#endif
                case VNF_LeadingZeroCount:
                case VNF_PopCount:
                case VNF_TrailingZeroCount:
                    return VNVisit.Continue;
            }
        }

        return VNVisit.Abort;
    }
}
