// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics;

namespace RyuJitSharp;

public abstract class GenTreeJitIntrinsic : GenTreeMultiOp
{
    private unsafe CORINFO_METHOD_HANDLE _methodHandle;

#if FEATURE_READYTORUN
    private CORINFO_CONST_LOOKUP _entryPoint;
#endif

    private regNumber _otherReg;
    private MultiRegSpillFlags _spillFlags;   
    private var_types _auxiliaryType;
    private var_types _simdBaseType;
    private byte _simdSize;

    protected NamedIntrinsic _hwIntrinsicId;

    protected GenTreeJitIntrinsic(genTreeOps oper, var_types type, var_types simdBaseType, byte simdSize, params ReadOnlySpan<GenTree> operands)
        : base(oper, type, operands)
    {
        _otherReg = REG_NA;
        _simdBaseType = simdBaseType;
        _simdSize = simdSize;
    }

    /// <summary>For intrinsics than need another type (e.g. Avx2.Gather* or SIMD (by element))</summary>
    public var_types AuxiliaryType
    {
        get
        {
            return _auxiliaryType;
        }

        set
        {
            _auxiliaryType = value;
        }
    }

#if FEATURE_READYTORUN
    public unsafe CORINFO_CONST_LOOKUP EntryPoint
    {
        get
        {
            assert(Debugger.IsAttached || IsUserCall);
            return _entryPoint;
        }

        init
        {
            assert(IsUserCall);
            _entryPoint = value;
        }
    }
#endif

    public bool IsSimd => _simdSize is not 0;

    public unsafe CORINFO_METHOD_HANDLE MethodHandle
    {
        get
        {
            assert(Debugger.IsAttached || IsUserCall);
            return _methodHandle;
        }

        init
        {
            assert(!IsUserCall);
            Flags |= (GTF_HW_USER_CALL | GTF_EXCEPT | GTF_CALL);
            _methodHandle = value;
        }
    }

    /// <summary>SIMD vector base JIT type</summary>
    public var_types SimdBaseType
    {
        get
        {
            return _simdBaseType;
        }

        set
        {
            _simdBaseType = value;
        }
    }

    /// <summary>SIMD vector size in bytes, use 0 for scalar intrinsics</summary>
    public byte SimdSize
    {
        get
        {
            return _simdSize;
        }

        set
        {
            _simdSize = value;
        }
    }

    /// <summary> Get regNumber of i'th position.</summary>
    /// <param name="idx">register position.</param>
    /// <returns>Returns regNumber assigned to i'th position.</returns>
    public regNumber GetRegNumByIdx(byte idx)
    {
        if (idx is 0)
        {
            return RegNum;
        }

#if TARGET_ARM64
        assert(idx < MAX_MULTIREG_COUNT);

        if (NeedsConsecutiveRegisters)
        {
            assert(IsMultiRegNode);
            return RegNum + idx;
        }
#endif

        // should only be used to get otherReg
        assert(idx is 1);
        return _otherReg;
    }

    public GenTreeFlags GetRegSpillFlagByIdx(byte idx) => GetMultiRegSpillFlagsByIdx(_spillFlags, idx);

    /// <summary>Set the regNumber for i'th position.</summary>
    /// <param name="reg">reg number</param>
    /// <param name="idx">register position.</param>
    public void SetRegNumByIdx(regNumber reg, byte idx)
    {
#if TARGET_ARM64
        assert(idx < MAX_MULTIREG_COUNT);

        if (idx is 0)
        {
            RegNum = reg;
            return;
        }

        if (NeedsConsecutiveRegisters)
        {
            assert(IsMultiRegNode);
            assert(reg == (RegNum + idx));
            return;
        }
#endif

        // should only be used to set otherReg
        assert(idx is 1);
        _otherReg = reg;
    }

    public void SetRegSpillFlagByIdx(GenTreeFlags flags, byte idx)
    {
        _spillFlags = SetMultiRegSpillFlagsByIdx(_spillFlags, flags, idx);
    }
}
