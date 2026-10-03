// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using System;
using System.Numerics;

namespace RyuJitSharp;

public ref struct Arm32Classifier
{
    private readonly ref readonly ClassifierInfo _info;
    private uint _nextIntReg;
    private uint _floatRegs = 0xFFFF;
    private uint _stackArgSize;

    public Arm32Classifier(in ClassifierInfo info)
    {
        _info = ref info;
    }

    public readonly int StackSize => unchecked((int)_stackArgSize);

    public unsafe AbiPassingInformation Classify(
        Compiler comp, var_types type, ClassLayout? structLayout, WellKnownArg wellKnownParam)
    {
        if (wellKnownParam is WellKnownArg.SecretStubParam)
        {
            return AbiPassingInformation.FromSegmentByValue(comp,
                AbiPassingSegment.InRegister(REG_SECRET_STUB_PARAM, 0, TARGET_POINTER_SIZE));
        }

#if CONFIGURABLE_ARM_ABI
        var useSoftFP = comp.opts.compUseSoftFP;
#else
        var useSoftFP = Compiler.Options.compUseSoftFP;
#endif

        if (!useSoftFP)
        {
            if (varTypeIsStruct(type))
            {
                assert(structLayout is not null);
                var hfaType = comp.GetHfaType(structLayout.ClassHandle);

                if (hfaType is not TYP_UNDEF)
                {
                    var slots = structLayout.Size / (uint)hfaType.Size;
                    return ClassifyFloat(comp, hfaType, slots);
                }
            }

            if (varTypeIsFloating(type))
            {
                return ClassifyFloat(comp, type, 1);
            }
        }

        uint alignment = 4;

        if ((type is TYP_LONG or TYP_DOUBLE) ||
            ((type is TYP_STRUCT) &&
             (comp.info.compCompHnd->getClassAlignmentRequirement(
                 (structLayout ?? throw new ArgumentNullException(nameof(structLayout))).ClassHandle) == 8)))
        {
            alignment = 8;
            _nextIntReg = roundUp(_nextIntReg, 2);
        }

        var size = type is TYP_STRUCT
            ? (structLayout ?? throw new ArgumentNullException(nameof(structLayout))).Size
            : (uint)type.Size;
        var numSlots = (size + 3) / 4;
        var numInRegs = Math.Min(numSlots, 4 - _nextIntReg);
        var anyOnStack = numInRegs < numSlots;

        // A preceding floating-point stack argument prevents splitting an integer argument.
        if ((numInRegs > 0) && anyOnStack && (_stackArgSize != 0))
        {
            numInRegs = 0;
        }

        var info = new AbiPassingInformation(unchecked((int)(numInRegs + (anyOnStack ? 1u : 0u))));

        for (uint i = 0; i < numInRegs; i++)
        {
            var endOffs = Math.Min((i + 1) * 4, size);
            info.Segments[(int)i] = AbiPassingSegment.InRegister(
                (regNumber)((uint)REG_R0 + _nextIntReg + i), unchecked((int)(i * 4)),
                unchecked((int)(endOffs - (i * 4))));
        }

        _nextIntReg += numInRegs;

        if (anyOnStack)
        {
            _stackArgSize = roundUp(_stackArgSize, alignment);
            var stackSize = size - (numInRegs * 4);
            info.Segments[(int)numInRegs] = AbiPassingSegment.OnStack(
                unchecked((int)_stackArgSize), unchecked((int)(numInRegs * 4)), unchecked((int)stackSize));
            _stackArgSize = unchecked(_stackArgSize + roundUp(stackSize, 4));
            _nextIntReg = 4;
        }

        return info;
    }

    private AbiPassingInformation ClassifyFloat(Compiler comp, var_types type, uint numElems)
    {
        assert(type is TYP_FLOAT or TYP_DOUBLE);
        var numConsecutive = type is TYP_FLOAT ? numElems : numElems * 2;
        var startRegMask = _floatRegs;

        // A set bit survives only if it begins a sufficiently long run of available registers.
        for (uint i = 1; i < numConsecutive; i++)
        {
            startRegMask &= _floatRegs >> (int)i;
        }

        if (type is TYP_DOUBLE)
        {
            startRegMask &= 0b0101010101010101;
        }

        if (startRegMask != 0)
        {
            var startRegIndex = (uint)BitOperations.TrailingZeroCount(startRegMask);
            var usedRegsMask = ((1u << (int)numConsecutive) - 1) << (int)startRegIndex;
            assert((_floatRegs & usedRegsMask) == usedRegsMask);

            _floatRegs ^= usedRegsMask;
            var info = new AbiPassingInformation(unchecked((int)numElems));
            var numRegsPerElem = type is TYP_FLOAT ? 1u : 2u;

            for (uint i = 0; i < numElems; i++)
            {
                var reg = (regNumber)((uint)REG_F0 + startRegIndex + (i * numRegsPerElem));
                info.Segments[(int)i] = AbiPassingSegment.InRegister(
                    reg, unchecked((int)(i * (uint)type.Size)), type.Size);
            }

            return info;
        }
        else
        {
            // Once a floating-point argument spills, no later one can use floating-point registers.
            _floatRegs = 0;
            _stackArgSize = roundUp(_stackArgSize, (uint)type.Size);
            var size = numElems * (uint)type.Size;
            var segment = AbiPassingSegment.OnStack(unchecked((int)_stackArgSize), 0, unchecked((int)size));
            var info = AbiPassingInformation.FromSegmentByValue(comp, segment);
            _stackArgSize = unchecked(_stackArgSize + size);

            return info;
        }
    }
}
#endif
