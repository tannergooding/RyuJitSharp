// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;

namespace RyuJitSharp;

public struct AbiPassingSegment
{
    // Wasm type tag 7 is invalid (WasmValueType.Count is 7), reserving these values for stack segments.
    private const regNumber REG_STACK_FULL_SLOT = (regNumber)uint.MaxValue;
    private const regNumber REG_STACK_PARTIAL_SLOT = (regNumber)(uint.MaxValue - 1);

    private regNumber _register = REG_STACK_FULL_SLOT;
    private int _stackOffset;

    public int Offset;
    public int Size;

    public AbiPassingSegment()
    {
    }

    public readonly bool IsPassedInRegister =>
        (_register != REG_STACK_FULL_SLOT) && (_register != REG_STACK_PARTIAL_SLOT);

    public readonly bool IsPassedOnStack => !IsPassedInRegister;

    // If this segment is passed in a register, return the particular register.
    public readonly regNumber Register
    {
        get
        {
            assert(Debugger.IsAttached || IsPassedInRegister);
            return IsPassedInRegister ? _register : REG_NA;
        }
    }

    public readonly regMask RegisterMask
    {
        get
        {
            var regNum = Register;
            var regMsk = (regMask)(1L << ((int)(regNum) - RegisterMaskBase));

            if (Configuration.TARGET_ARM && genIsValidFloatReg(regNum) && (Size == 8))
            {
                regMsk |= (regMask)((ulong)regMsk << 1);
            }

            return regMsk;
        }
    }

    public readonly int RegisterMaskBase
    {
        get
        {
            var regNum = _register;

            if (Configuration.FEATURE_MASKED_HW_INTRINSICS &&
                (regNum >= TargetRuntimeMetadata.MaskRegisterFirst))
            {
                assert(regNum <= TargetRuntimeMetadata.MaskRegisterLast);
                return (int)TargetRuntimeMetadata.MaskRegisterFirst;
            }

            var fpFirst = TargetRuntimeMetadata.FloatingRegisterFirst;
            if (regNum >= fpFirst)
            {
                assert(regNum <= TargetRuntimeMetadata.FloatingRegisterLast);
                return (int)fpFirst;
            }

            var intFirst = TargetRuntimeMetadata.IntegerRegisterFirst;
            assert((regNum >= intFirst) && (regNum <= TargetRuntimeMetadata.IntegerRegisterLast));
            return (int)intFirst;
        }
    }

    // If this segment is passed on the stack then return the particular stack
    // offset, relative to the base of stack arguments.
    public readonly int StackOffset
    {
        get
        {
            //   On x86, for the managed ABI where arguments are pushed in order and thus
            //   come in reverse order in the callee, this is the offset to subtract from
            //   the top of the stack to get the argument's address. By top of the stack is
            //   meant esp on entry + 4 for the return address + total size of stack
            //   arguments. In varargs methods the varargs cookie contains the information
            //   required to allow the computation of the total size of stack arguments.
            //
            //   Outside the managed x86 ABI this is the offset to add to the first
            //   argument's address.

            assert(Debugger.IsAttached || IsPassedOnStack);
            return _stackOffset;
        }
    }

    // Get the size of stack consumed. Normally this is 'Size' rounded up to
    // the pointer size, but for apple arm64 ABI some primitives do not consume
    // full stack slots.
    public readonly int StackSize
    {
        get
        {
            assert(Debugger.IsAttached || IsPassedOnStack);
            return _register == REG_STACK_FULL_SLOT ? roundUp(Size, Configuration.TARGET_POINTER_SIZE) : Size;
        }
    }

    /// <summary>Create an AbiPassingSegment representing that a segment is passed in a register.</summary>
    /// <param name="reg">The register the segment is passed in</param>
    /// <param name="offset">The offset of the segment that is passed in the register</param>
    /// <param name="size">The size of the segment passed in the register</param>
    /// <returns>New instance of AbiPassingSegment.</returns>
    public static AbiPassingSegment InRegister(regNumber reg, int offset, int size)
    {
        assert((reg != REG_NA) && (reg != REG_STACK_FULL_SLOT) && (reg != REG_STACK_PARTIAL_SLOT));
        return new AbiPassingSegment {
            _register = reg,
            Offset = offset,
            Size = size,
        };
    }

    /// <summary>Create an AbiPassingSegment representing that a segment is passed on the stack.</summary>
    /// <param name="stackOffset">Offset relative to the first stack parameter/argument</param>
    /// <param name="offset">The offset of the segment that is passed in the register</param>
    /// <param name="size">The size of the segment passed in the register</param>
    /// <returns>New instance of AbiPassingSegment.</returns>
    public static AbiPassingSegment OnStack(int stackOffset, int offset, int size)
    {
        return new AbiPassingSegment {
            _register = REG_STACK_FULL_SLOT,
            _stackOffset = stackOffset,
            Offset = offset,
            Size = size,
        };
    }

    /// <summary>Create a stack segment that does not consume a full pointer-sized slot.</summary>
    public static AbiPassingSegment OnStackWithoutConsumingFullSlot(int stackOffset, int offset, int size)
    {
        return new AbiPassingSegment {
            _register = REG_STACK_PARTIAL_SLOT,
            _stackOffset = stackOffset,
            Offset = offset,
            Size = size,
        };
    }

#if DEBUG
    public readonly void Dump()
    {
        if (IsPassedInRegister)
        {
            jitprintf($"[{Offset:D2}..{Offset + Size:D2}) reg {Register.Name}");
        }
        else
        {
            jitprintf($"[{Offset:D2}..{Offset + Size:D2}) stack @ +{StackOffset:D2}");
        }
    }
#endif

    public readonly var_types GetRegisterType()
    {
        var regNum = Register;
        var regMskBase = REG_NA;
        bool isFloatReg;

        if (Configuration.TARGET_WASM)
        {
            isFloatReg = genIsValidFloatReg(regNum);
        }
        else
        {
            regMskBase = (regNumber)RegisterMaskBase;
            if (Configuration.FEATURE_MASKED_HW_INTRINSICS &&
                (regMskBase == TargetRuntimeMetadata.MaskRegisterFirst))
            {
                assert(Size is 1 or 2 or 4 or 8);
                return TYP_MASK;
            }
            isFloatReg = regMskBase == TargetRuntimeMetadata.FloatingRegisterFirst;
        }

        if (isFloatReg)
        {
            if (Size is 4)
            {
                return TYP_FLOAT;
            }
            if (Size is 8)
            {
                return TYP_DOUBLE;
            }
            if ((Size is 12) && Configuration.TARGET_WASM && Configuration.FEATURE_SIMD)
            {
                return TYP_SIMD16;
            }
            if ((Size is 16) && Configuration.FEATURE_SIMD)
            {
                return TYP_SIMD16;
            }
            return UnexpectedRegisterSize(true);
        }

        if (!Configuration.TARGET_WASM)
        {
            assert((int)regMskBase == (int)TargetRuntimeMetadata.IntegerRegisterFirst);
        }

        return Size switch {
            1 => TYP_UBYTE,
            2 => TYP_USHORT,
            3 or 4 => TYP_INT,
            _ when (Configuration.TARGET_64BIT || Configuration.TARGET_WASM) && (Size is >= 5 and <= 8) => TYP_LONG,
            _ => UnexpectedRegisterSize(false),
        };
    }

    private static var_types UnexpectedRegisterSize(bool floating)
    {
        assert(false, floating
            ? "Unexpected size for floating point register"
            : "Unexpected size for integer register");

        return TYP_UNDEF;
    }

    public readonly var_types GetRegisterType(ClassLayout? layout)
    {
        if ((layout is not null) &&
            (Configuration.TARGET_WASM
                ? regNumberExtensions.IsValidWasmIntReg(_register)
                : (RegisterMaskBase == (int)TargetRuntimeMetadata.IntegerRegisterFirst)))
        {
            assert(Offset < layout.Size);

            if (((Offset % Configuration.TARGET_POINTER_SIZE) == 0) && (Size == Configuration.TARGET_POINTER_SIZE))
            {
                return layout.GetGCPtrType(Offset / Configuration.TARGET_POINTER_SIZE);
            }
        }
        return GetRegisterType();
    }
}
