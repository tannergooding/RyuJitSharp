// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;

namespace RyuJitSharp;

public struct AbiPassingSegment
{
    private regNumber m_register = REG_NA;
    private bool m_isFullStackSlot = true;
    private int m_stackOffset;

    public int Offset;
    public int Size;

    public AbiPassingSegment()
    {
    }

    public readonly bool IsPassedInRegister => m_register != REG_NA;

    public readonly bool IsPassedOnStack => m_register == REG_NA;

    // If this segment is passed in a register, return the particular register.
    public readonly regNumber Register
    {
        get
        {
            assert(Debugger.IsAttached || IsPassedInRegister);
            return m_register;
        }
    }

    public readonly int RegisterMask
    {
        get
        {
            var regNum = m_register;
            var regMsk = 1 << ((int)(regNum) - RegisterMaskBase);

#if TARGET_ARM
            if (Size == 8)
            {
                assert(RegisterMaskBase == (int)(REG_FP_FIRST));
                regMsk |= (regMsk << 1);
            }
#endif

            return regMsk;
        }
    }

    public readonly int RegisterMaskBase
    {
        get
        {
            var regNum = m_register;

#if FEATURE_MASKED_HW_INTRINSICS
            if (regNum >= REG_MASK_FIRST)
            {
                assert(regNum <= REG_MASK_LAST);
                return (int)(REG_MASK_FIRST);
            }
#endif

            if (regNum >= REG_FP_FIRST)
            {
                assert(regNum <= REG_FP_LAST);
                return (int)(REG_FP_FIRST);
            }

            assert(regNum is >= REG_INT_FIRST and <= REG_INT_LAST);
            return (int)(REG_INT_FIRST);
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
            return m_stackOffset;
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
            return m_isFullStackSlot ? roundUp(Size, TARGET_POINTER_SIZE) : Size;
        }
    }

#if DEBUG
    public readonly void Dump()
    {
        // TODO: Port AbiPassingSegment.Dump
    }
#endif

    public readonly var_types GetRegisterType()
    {
        var regNum = m_register;
        var regMskBase = RegisterMaskBase;

#if FEATURE_MASKED_HW_INTRINSICS
        if (regMskBase == (int)(REG_MASK_FIRST))
        {
            assert(Size is 1 or 2 or 4 or 8);
            return TYP_MASK;
        }
#endif

        if (regMskBase == (int)(REG_FP_FIRST))
        {
            return Size switch {
                4 => TYP_FLOAT,
                8 => TYP_DOUBLE,
#if FEATURE_SIMD
                16 => TYP_SIMD16,
#endif
                _ => TYP_UNDEF,
            };
        }
        
        assert(regMskBase == (int)(REG_INT_FIRST));

        return Size switch {
            1 => TYP_UBYTE,
            2 => TYP_USHORT,
            3 => TYP_INT,
            4 => TYP_INT,
#if TARGET_64BIT || TARGET_WASM
            5 => TYP_LONG,
            6 => TYP_LONG,
            7 => TYP_LONG,
            8 => TYP_LONG,
#endif
            _ => TYP_UNDEF,
        };
    }

    public readonly var_types GetRegisterType(ClassLayout? layout)
    {
        if ((layout is not null) && (RegisterMaskBase == (int)(REG_INT_FIRST)))
        {
            assert(Offset < layout.Size);

            if (((Offset % TARGET_POINTER_SIZE) == 0) && (Size == TARGET_POINTER_SIZE))
            {
                return layout.GetGCPtrType(Offset / TARGET_POINTER_SIZE);
            }
        }
        return GetRegisterType();
    }
}
