// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;
using System.Runtime.CompilerServices;
using static RyuJitSharp.Compiler.structPassingKind;

namespace RyuJitSharp;

/// <summary>Return type descriptor of a GT_CALL node.</summary>
/// <remarks>
///   <para>x64 Unix, Arm64, Arm32 and x86 allow a value to be returned in multiple registers.</para>
///   <para>For such calls this struct provides the following info on their return type</para>
///   <list type="bullet">
///     <item>type of value returned in each return register</item>
///     <item>ABI return register numbers in which the value is returned</item>
///     <item>count of return registers in which the value is returned</item>
///   </list>
/// </remarks>
public struct ReturnTypeDesc
{
    // TODO-ARM: Update this to meet the needs of Arm64 and Arm32

    // TODO-AllArch: Right now it is used for describing multi-reg returned types.
    // Eventually we would want to use it for describing even single-reg
    // returned types (e.g. structs returned in single register x64/arm).
    // This would allow us not to lie or normalize single struct return
    // values in importer/morph.

    private _regTypeInlineArray _regType;

#if TARGET_RISCV64 || TARGET_LOONGARCH64
    // Structs according to hardware floating-point calling convention are passed as two logical fields, each in
    // separate register, disregarding struct layout such as packing, custom alignment, padding with empty structs, etc.
    // We need size (can be derived from m_regType) & offset of each field for memory load/stores
    private _fieldOffsetInlineArray _fieldOffset;
#endif

#if DEBUG
    private bool _inited;
#endif

    public ReturnTypeDesc()
    {
        Reset();
    }

    /// <summary>check whether the type is returned in multiple return registers.</summary>
    /// <remarks>Note that we only have to examine the first two values to determine this</remarks>
    public readonly bool IsMultiRegRetType
    {
        get
        {
            var result = false;

#if FEATURE_MULTIREG_RET
            if (MAX_RET_REG_COUNT >= 2)
            {
                assert(Debugger.IsAttached || _inited);
                result = ((_regType[0] != TYP_UNKNOWN) && (_regType[1] != TYP_UNKNOWN));
            }
#endif

            return result;
        }
    }

    /// <summary>Get the count of return registers in which the return value is returned.</summary>
    public readonly byte ReturnRegCount
    {
        get
        {
#if DEBUG
            assert(Debugger.IsAttached || _inited);
#endif

            byte regCount = 0;

            for (var i = 0; i < MAX_RET_REG_COUNT; i++)
            {
                if (_regType[i] == TYP_UNKNOWN)
                {
                    break;
                }

                // otherwise
                regCount++;
            }

#if DEBUG
            // Any remaining elements in m_regTypes[] should also be TYP_UNKNOWN
            for (var i = regCount + 1; i < MAX_RET_REG_COUNT; i++)
            {
                assert(_regType[i] == TYP_UNKNOWN);

#if TARGET_RISCV64 || TARGET_LOONGARCH64
                assert(_fieldOffset[i] == 0);
#endif
            }
#endif

            return regCount;
        }
    }

#if DEBUG
    // NOTE: we only use this function when writing out IR dumps.
    // These dumps may take place before the ReturnTypeDesc has been initialized.
    public readonly byte ReturnRegCountOrDefault => (byte)(_inited ? ReturnRegCount : 0);
#endif

    public readonly uint SingleReturnFieldOffset
    {
        get
        {
            assert(!IsMultiRegRetType);
            assert(_regType[0] != TYP_UNKNOWN);

#if TARGET_RISCV64 || TARGET_LOONGARCH64
            return _fieldOffset[0];
#else
            return 0;
#endif
        }
    }

#if SWIFT_SUPPORT
    private static ReadOnlySpan<regNumber> SwiftFloatReturnRegs => REG_SWIFT_FLOATRET_ORDER;

    private static ReadOnlySpan<regNumber> SwiftIntReturnRegs => REG_SWIFT_INTRET_ORDER;
#endif

    /// <summary>Initialize the Return Type Descriptor for a method that returns a TYP_LONG</summary>
    /// <remarks>Only needed for X86 and arm32.</remarks>
    public void InitializeLongReturnType()
    {
#if DEBUG
        assert(!_inited);
#endif

#if TARGET_X86 || TARGET_ARM
        // Setups up a ReturnTypeDesc for returning a long using two registers
        assert(MAX_RET_REG_COUNT >= 2);

        _regType[0] = TYP_INT;
        _regType[1] = TYP_INT;
#else

        _regType[0] = TYP_LONG;
#endif

#if DEBUG
        _inited = true;
#endif
    }

    /// <summary>Initialize the descriptor for a method that returns any type.</summary>
    /// <param name="compiler">The Compiler instance</param>
    /// <param name="type">The return type as specififed by the signature</param>
    /// <param name="retClsHnd">Handle for struct return types</param>
    /// <param name="callConv">Method's calling convention</param>
    public unsafe void InitializeReturnType(Compiler compiler, var_types type, CORINFO_CLASS_HANDLE retClsHnd, CorInfoCallConvExtension callConv)
    {
        if (varTypeIsStruct(type))
        {
            InitializeStructReturnType(compiler, retClsHnd, callConv);
        }
        else if (type == TYP_LONG)
        {
            InitializeLongReturnType();
        }
        else
        {
            if (type is not TYP_VOID)
            {
                assert(varTypeIsEnregisterable(type));
                _regType[0] = type;
            }

#if DEBUG
            _inited = true;
#endif
        }
    }

    /// <summary>Initialize the Return Type Descriptor for a method that returns a struct type</summary>
    /// <param name="compiler">Compiler Instance</param>
    /// <param name="retClsHnd">VM handle to the struct type returned by the method</param>
    /// <param name="callConv"></param>
    public unsafe void InitializeStructReturnType(Compiler compiler, CORINFO_CLASS_HANDLE retClsHnd, CorInfoCallConvExtension callConv)
    {
#if DEBUG
        assert(!_inited);
#endif

        assert(retClsHnd != NO_CLASS_HANDLE);
        var structSize = compiler.info.compCompHnd->getClassSize(retClsHnd);

        var returnType = compiler.GetReturnTypeForStruct(retClsHnd, callConv, out var howToReturnStruct, structSize);

        switch (howToReturnStruct)
        {
            case SPK_EnclosingType:
            case SPK_PrimitiveType:
            {
                assert(returnType is not TYP_UNKNOWN and not TYP_STRUCT);
                _regType[0] = returnType;

#if TARGET_RISCV64 || TARGET_LOONGARCH64
                var lowering = compiler.GetFpStructLowering(retClsHnd);

                if (!lowering->byIntegerCallConv)
                {
                    assert(lowering->numLoweredElements == 1);
                    _fieldOffset[0] = lowering->offsets[0];
                }
#endif
                break;
            }

            case SPK_ByValueAsHfa:
            {
                assert(varTypeIsStruct(returnType));
                var hfaType = compiler.GetHfaType(retClsHnd);

                // We should have an hfa struct type
                assert(varTypeIsValidHfaType(hfaType));

                // Note that the retail build issues a warning about a potential divsion by zero without this "max",
                var elemSize = uint.Max(1u, hfaType.Size);

                // The size of this struct should be evenly divisible by elemSize
                assert((structSize % elemSize) == 0);

                var hfaCount = (structSize / elemSize);

                for (byte i = 0; i < hfaCount; i++)
                {
                    _regType[i] = hfaType;
                }

                compiler.compFloatingPointUsed = true;
                break;
            }

            case SPK_ByValue:
            {
                assert(varTypeIsStruct(returnType));

#if SWIFT_SUPPORT
                if (callConv is CorInfoCallConvExtension.Swift)
                {
                    InitializeSwiftReturnRegs(compiler, retClsHnd);
                    break;
                }
#endif

#if UNIX_AMD64_ABI
                compiler.eeGetSystemVAmd64PassStructInRegisterDescriptor(retClsHnd, out var structDesc);
                assert(structDesc.passedInRegisters);

                for (byte i = 0; i < structDesc.eightByteCount; i++)
                {
                    assert(i < MAX_RET_REG_COUNT);
                    _regType[i] = compiler.GetEightByteType(structDesc, i);
                }
#elif TARGET_ARM64
                // a non-HFA struct returned using two registers
                assert(structSize is > TARGET_POINTER_SIZE and <= (2 * TARGET_POINTER_SIZE));

                var gcPtrs = stackalloc CorInfoGCType[2];
                compiler.info.compCompHnd->getClassGClayout(retClsHnd, (byte*)(gcPtrs));

                for (byte i = 0; i < 2; i++)
                {
                    _regType[i] = compiler.GetJitGCType(gcPtrs[i]);
                }
#elif TARGET_LOONGARCH64 || TARGET_RISCV64
                assert(structSize is > sizeof(float) and <= (2 * TARGET_POINTER_SIZE));

                var gcPtrs = stackalloc CorInfoGCType[2];
                compiler.info.compCompHnd->getClassGClayout(retClsHnd, (byte*)(gcPtrs));

                var lowering = compiler.GetFpStructLowering(retClsHnd);

                if (!lowering.ByIntegerCallConv)
                {
                    compiler.FloatingPointUsed = true;

                    assert(lowering.NumLoweredElements == MAX_RET_REG_COUNT);
                    assert(MAX_RET_REG_COUNT == MAX_FPSTRUCT_LOWERED_ELEMENTS);

                    var foundFloatingPointReg = false;

                    for (byte i = 0; i < MAX_RET_REG_COUNT; i++)
                    {
                        var regType = JitType2VarType(lowering.LoweredElements[i]);
                        var fieldOffset = lowering->offsets[i];

                        _regType[i] = regType;
                        _fieldOffset[i] = fieldOffset;

                        if ((regType is TYP_LONG) && ((fieldOffset % TARGET_POINTER_SIZE) == 0))
                        {
                            var slot = fieldOffset / TARGET_POINTER_SIZE;
                            _regType[i] = compiler.GetJitGCType(gcPtrs[slot]);
                        }
                        else if (varTypeIsFloating(regType))
                        {
                            foundFloatingPointReg = true;
                        }
                    }

                    assert(foundFloatingPointReg);
                }
                else
                {
                    for (byte i = 0; i < 2; i++)
                    {
                        _regType[i] = compiler.GetJitGCType(gcPtrs[i]);
                        _fieldOffset[i] = (uint)(i * TARGET_POINTER_SIZE);
                    }
                }
#elif TARGET_X86
                // an 8-byte struct returned using two registers
                assert(structSize == 8);

                var gcPtrs = stackalloc CorInfoGCType[2];
                compiler.info.compCompHnd->getClassGClayout(retClsHnd, (byte*)(gcPtrs));

                for (byte i = 0; i < 2; i++)
                {
                    _regType[i] = compiler.GetJitGCType(gcPtrs[i]);
                }
#elif TARGET_WASM
                // For Wasm, structs are either returned by-ref or as primitives.
                unreached();
#else

                // This target needs support here!
                NYI("Unsupported TARGET returning a TYP_STRUCT in InitializeStructReturnType");
#endif
                break;
            }

            case SPK_ByReference:
            {
                // We are returning using the return buffer argument, there are no return registers.
                break;
            }

            default:
            {
                // By the contract of getReturnTypeForStruct we should never get here.
                unreached();
                break;
            }
        }

#if DEBUG
        _inited = true;
#endif
    }

    // Reset type descriptor to defaults
    public void Reset()
    {
        for (byte i = 0; i < MAX_RET_REG_COUNT; i++)
        {
            _regType[i] = TYP_UNKNOWN;

#if TARGET_RISCV64 || TARGET_LOONGARCH64
            _fieldOffset[i] = 0;
#endif
        }

#if DEBUG
        _inited = false;
#endif
    }

    /// <summary>Get var_type of the return register specified by index.</summary>
    /// <param name="index">Index of the return register.</param>
    /// <returns>var_type of the return register specified by its index.</returns>
    /// <remarks>asserts if the index does not have a valid register return type.</remarks>
    public readonly var_types GetReturnRegType(byte index)
    {
        var result = _regType[index];
        assert(result != TYP_UNKNOWN);
        return result;
    }

    /// <summary>For the N'th returned register, identified by "index", returns the starting offset in the struct return type of the data being returned.</summary>
    /// <param name="index">The register whose offset to get</param>
    /// <returns>Starting offset of data returned in that register.</returns>
    public readonly uint GetReturnFieldOffset(byte index)
    {
        assert(_regType[index] is not TYP_UNKNOWN);

#if TARGET_RISCV64 || TARGET_LOONGARCH64
        return _fieldOffset[index];
#else
        var offset = 0u;

        for (byte i = 0; i < index; i++)
        {
            offset += _regType[i].Size;
        }
        return offset;
#endif
    }

    /// <summary>Return i'th return register as per target ABI</summary>
    /// <param name="idx">Index of the return register.</param>
    /// <param name="callConv">Associated calling convention</param>
    /// <returns>Returns i'th return register as per target ABI.</returns>
    /// <remarks>
    ///   <para>x86 and ARM return long in multiple registers.</para>
    ///   <para>ARM and ARM64 return HFA struct in multiple registers.</para>
    /// </remarks>
    public readonly regNumber GetAbiReturnReg(byte idx, CorInfoCallConvExtension callConv)
    {
        var count = ReturnRegCount;
        assert(idx < count);

        var resultReg = REG_NA;

#if SWIFT_SUPPORT
        if (callConv == CorInfoCallConvExtension.Swift)
        {
            assert((idx < SwiftIntReturnRegs.Length) && (idx < SwiftFloatReturnRegs.Length));

            byte intRegIdx = 0;
            byte fltRegIdx = 0;

            for (byte i = 0; i < idx; i++)
            {
                if (varTypeUsesIntReg(GetReturnRegType(i)))
                {
                    intRegIdx++;
                }
                else
                {
                    fltRegIdx++;
                }
            }

            if (varTypeUsesIntReg(GetReturnRegType(idx)))
            {
                resultReg = SwiftIntReturnRegs[intRegIdx];
            }
            else
            {
                resultReg = SwiftFloatReturnRegs[fltRegIdx];
            }

            assert(resultReg != REG_NA);
            return resultReg;
        }
#endif

#if UNIX_AMD64_ABI
        var regType0 = GetReturnRegType(0);

        if (idx == 0)
        {
            if (varTypeUsesIntReg(regType0))
            {
                resultReg = REG_INTRET;
            }
            else
            {
                noway_assert(varTypeUsesFloatReg(regType0));
                resultReg = REG_FLOATRET;
            }
        }
        else if (idx == 1)
        {
            var regType1 = GetReturnRegType(1);

            if (varTypeUsesIntReg(regType1))
            {
                if (varTypeIsIntegralOrI(regType0))
                {
                    resultReg = REG_INTRET_1;
                }
                else
                {
                    resultReg = REG_INTRET;
                }
            }
            else
            {
                noway_assert(varTypeUsesFloatReg(regType1));

                if (varTypeUsesFloatReg(regType0))
                {
                    resultReg = REG_FLOATRET_1;
                }
                else
                {
                    resultReg = REG_FLOATRET;
                }
            }
        }
#elif WINDOWS_AMD64_ABI
        assert(idx == 0);

        if (varTypeUsesIntReg(GetReturnRegType(0)))
        {
            resultReg = REG_INTRET;
        }
        else
        {
            assert(varTypeUsesFloatReg(GetReturnRegType(0)));
            resultReg = REG_FLOATRET;
        }
#elif TARGET_X86
        if (idx == 0)
        {
            resultReg = REG_LNGRET_LO;
        }
        else if (idx == 1)
        {
            resultReg = REG_LNGRET_HI;
        }
#elif TARGET_ARM
        var regType = GetReturnRegType(idx);

        if (varTypeIsIntegralOrI(regType))
        {
            // Ints are returned in one return register.
            // Longs are returned in two return registers.
            if (idx == 0)
            {
                resultReg = REG_LNGRET_LO;
            }
            else if (idx == 1)
            {
                resultReg = REG_LNGRET_HI;
            }
        }
        else
        {
            // Floats are returned in one return register (f0).
            // Doubles are returned in one return register (d0).
            // Structs are returned in four registers with HFAs.
            assert(idx < MAX_RET_REG_COUNT); // Up to 4 return registers for HFA's

            if (regType == TYP_DOUBLE)
            {
                resultReg = REG_FLOATRET + unchecked((byte)(idx * 2)); // d0, d1, d2 or d3
            }
            else
            {
                resultReg = REG_FLOATRET + idx; // f0, f1, f2 or f3
            }

            assert(resultReg != REG_NA);
        }
#elif TARGET_ARM64
        var regType = GetReturnRegType(idx);

        if (varTypeIsIntegralOrI(regType))
        {
            noway_assert(idx < 2);                              // Up to 2 return registers for 16-byte structs
            resultReg = (idx == 0) ? REG_INTRET : REG_INTRET_1; // X0 or X1
        }
        else
        {
            noway_assert(idx < 4);          // Up to 4 return registers for HFA's
            resultReg = REG_FLOATRET + idx; // V0, V1, V2 or V3
            assert(resultReg != REG_NA);
        }
#elif TARGET_LOONGARCH64 || TARGET_RISCV64
        var regType = GetReturnRegType(idx);

        if (idx == 0)
        {
            resultReg = varTypeIsIntegralOrI(regType) ? REG_INTRET : REG_FLOATRET; // A0 or FA0
        }
        else
        {
            noway_assert(idx == 1); // Up to 2 return registers for two-float-field structs

            // If the first return register is from the same register file, return the one next to it.
            if (varTypeUsesIntReg(regType))
            {
                resultReg = varTypeIsIntegralOrI(GetReturnRegType(0)) ? REG_INTRET_1 : REG_INTRET; // A0 or A1
            }
            else
            {
                assert(varTypeUsesFloatReg(regType));
                resultReg = varTypeIsIntegralOrI(GetReturnRegType(0)) ? REG_FLOATRET : REG_FLOATRET_1; // FA0 or FA1
            }
        }
#endif

        return resultReg;
    }

#if HAS_FIXED_REGISTER_SET
    // TODO: Port ReturnTypeDesc.GetAbiReturnRegs
    // /// <summary>get the mask of return registers as per target arch ABI.</summary>
    // /// <param name="callConv">The calling convention</param>
    // /// <returns>reg mask of return registers in which the return type is returned.</returns>
    // /// <remarks>This routine can be used when the caller is not particular about the order of return registers and wants to know the set of return registers.</remarks>
    // public regMaskTP GetAbiReturnRegs(CorInfoCallConvExtension callConv)
    // {
    //     var resultMask = RBM_NONE;
    //     var count = ReturnRegCount;
    // 
    //     for (byte i = 0; i < count; i++)
    //     {
    //         resultMask |= genRegMask(GetAbiReturnReg(i, callConv));
    //     }
    //     return resultMask;
    // }
#endif

#if SWIFT_SUPPORT
    /// <summary>Initialize the Return Type Descriptor for a method that returns with the Swift calling convention.</summary>
    /// <param name="compiler">Compiler instance</param>
    /// <param name="retClsHnd">Struct type being returned</param>
    private unsafe void InitializeSwiftReturnRegs(Compiler compiler, CORINFO_CLASS_HANDLE retClsHnd)
    {
        var lowering = compiler.GetSwiftLowering(retClsHnd);
        assert(!lowering->byReference);

        assert(MAX_SWIFT_LOWERED_ELEMENTS <= MAX_RET_REG_COUNT);
        assert(lowering->numLoweredElements <= MAX_RET_REG_COUNT);

        for (byte i = 0; i < lowering->numLoweredElements; i++)
        {
            _regType[i] = JitType2VarType(lowering->loweredElements[i]);
        }
    }
#endif

    [InlineArray(MAX_RET_REG_COUNT)]
    private struct _regTypeInlineArray
    {
        public var_types e0;
    }

#if TARGET_RISCV64 || TARGET_LOONGARCH64
    [InlineArray(MAX_RET_REG_COUNT)]
    private struct _fieldOffsetInlineArray
    {
        public uint e0;
    }
#endif
}
