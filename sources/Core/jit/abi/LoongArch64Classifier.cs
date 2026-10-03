// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
using System;

namespace RyuJitSharp;

public ref struct LoongArch64Classifier
{
    private readonly ref readonly ClassifierInfo _info;
    private RegisterQueue _intRegs;
    private RegisterQueue _floatRegs;
    private uint _stackArgSize;

    public LoongArch64Classifier(in ClassifierInfo info)
    {
        _info = ref info;
        _intRegs = new RegisterQueue(IntArgRegs);
        _floatRegs = new RegisterQueue(FltArgRegs);
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

        assert(!_info.IsVarArgs);
        uint passedSize;
        uint slots = 0;
        var argRegTypeInStruct1 = TYP_UNKNOWN;
        var argRegTypeInStruct2 = TYP_UNKNOWN;
        var argRegOffset1 = 0;
        var argRegOffset2 = 0;
        var canPassArgInRegisters = false;
        var passedByRef = false;

        if (varTypeIsStruct(type))
        {
            assert(structLayout is not null);
            passedSize = structLayout.Size;

            if (passedSize > MAX_PASS_MULTIREG_BYTES)
            {
                passedByRef = true;
                slots = 1;
                passedSize = TARGET_POINTER_SIZE;
                canPassArgInRegisters = _intRegs.Count > 0;
            }
            else
            {
                assert(!structLayout.IsBlockLayout);
                ref readonly var lowering = ref comp.GetFpStructLowering(structLayout.ClassHandle);

                if (!lowering.byIntegerCallConv)
                {
                    slots = (uint)lowering.numLoweredElements;

                    if (lowering.numLoweredElements == 1)
                    {
                        canPassArgInRegisters = _floatRegs.Count > 0;
                        argRegTypeInStruct1 = lowering.loweredElements[0].VarType;
                        assert(varTypeIsFloating(argRegTypeInStruct1));
                        argRegOffset1 = lowering.offsets[0];
                    }
                    else
                    {
                        assert(lowering.numLoweredElements == 2);
                        argRegTypeInStruct1 = lowering.loweredElements[0].VarType;
                        argRegTypeInStruct2 = lowering.loweredElements[1].VarType;

                        if (varTypeIsFloating(argRegTypeInStruct1) && varTypeIsFloating(argRegTypeInStruct2))
                        {
                            canPassArgInRegisters = _floatRegs.Count >= 2;
                        }
                        else
                        {
                            assert(varTypeIsFloating(argRegTypeInStruct1) || varTypeIsFloating(argRegTypeInStruct2));
                            canPassArgInRegisters = (_floatRegs.Count > 0) && (_intRegs.Count > 0);
                        }

                        argRegOffset1 = lowering.offsets[0];
                        argRegOffset2 = lowering.offsets[1];
                    }

                    assert(slots is 1 or 2);

                    if (!canPassArgInRegisters)
                    {
                        slots = (passedSize + TARGET_POINTER_SIZE - 1) / TARGET_POINTER_SIZE;
                        // When floating-point registers are exhausted, fall back to integer registers.
                        canPassArgInRegisters = _intRegs.Count >= slots;
                        argRegTypeInStruct1 = TYP_UNKNOWN;
                        argRegTypeInStruct2 = TYP_UNKNOWN;
                    }
                }
                else
                {
                    slots = (passedSize + TARGET_POINTER_SIZE - 1) / TARGET_POINTER_SIZE;
                    canPassArgInRegisters = _intRegs.Count >= slots;
                }

                if (!canPassArgInRegisters && (slots == 2))
                {
                    if (_intRegs.Count > 0)
                    {
                        canPassArgInRegisters = true;
                    }
                }
            }
        }
        else
        {
            assert(type.Size <= TARGET_POINTER_SIZE);
            slots = 1;
            passedSize = (uint)type.Size;

            if (varTypeIsFloating(type))
            {
                canPassArgInRegisters = _floatRegs.Count > 0;

                if (!canPassArgInRegisters)
                {
                    type = TYP_I_IMPL;
                    canPassArgInRegisters = _intRegs.Count > 0;
                }
            }
            else
            {
                canPassArgInRegisters = _intRegs.Count > 0;
            }
        }

        AbiPassingInformation info;

        if (canPassArgInRegisters)
        {
            if (argRegTypeInStruct1 is not TYP_UNKNOWN)
            {
                info = new AbiPassingInformation(unchecked((int)slots));
                ref var regs = ref (varTypeIsFloating(argRegTypeInStruct1) ? ref _floatRegs : ref _intRegs);
                assert(regs.Count > 0);

                passedSize = (uint)argRegTypeInStruct1.Size;
                info.Segments[0] = AbiPassingSegment.InRegister(
                    regs.Dequeue(), argRegOffset1, unchecked((int)passedSize));

                if (argRegTypeInStruct2 is not TYP_UNKNOWN)
                {
                    passedSize = (uint)argRegTypeInStruct2.Size;
                    regs = ref (varTypeIsFloating(argRegTypeInStruct2) ? ref _floatRegs : ref _intRegs);
                    assert(regs.Count > 0);
                    info.Segments[1] = AbiPassingSegment.InRegister(
                        regs.Dequeue(), argRegOffset2, unchecked((int)passedSize));
                }
            }
            else
            {
                ref var regs = ref (varTypeIsFloating(type) ? ref _floatRegs : ref _intRegs);
                var slotSize = Math.Min(passedSize, TARGET_POINTER_SIZE);
                var firstSegment = AbiPassingSegment.InRegister(regs.Dequeue(), 0, unchecked((int)slotSize));

                if (slots == 1)
                {
                    info = AbiPassingInformation.FromSegment(comp, passedByRef, firstSegment);
                }
                else
                {
                    assert(slots == 2);
                    assert(varTypeIsStruct(type));
                    assert(passedSize > TARGET_POINTER_SIZE);

                    info = new AbiPassingInformation(unchecked((int)slots));
                    info.Segments[0] = firstSegment;
                    var tailSize = passedSize - slotSize;

                    if (_intRegs.Count > 0)
                    {
                        info.Segments[1] = AbiPassingSegment.InRegister(
                            _intRegs.Dequeue(), unchecked((int)slotSize), unchecked((int)tailSize));
                    }
                    else
                    {
                        assert(_intRegs.Count == 0);
                        assert(_stackArgSize == 0);
                        info.Segments[1] = AbiPassingSegment.OnStack(0, TARGET_POINTER_SIZE, unchecked((int)tailSize));
                        _stackArgSize = unchecked(_stackArgSize + TARGET_POINTER_SIZE);
                    }
                }
            }
        }
        else
        {
            assert((_stackArgSize % TARGET_POINTER_SIZE) == 0);
            info = AbiPassingInformation.FromSegment(comp, passedByRef,
                AbiPassingSegment.OnStack(unchecked((int)_stackArgSize), 0, unchecked((int)passedSize)));
            _stackArgSize = unchecked(_stackArgSize + roundUp(passedSize, TARGET_POINTER_SIZE));
            _intRegs.Clear();
        }

        return info;
    }
}
#endif
