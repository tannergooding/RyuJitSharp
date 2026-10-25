// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public ref struct Arm64Classifier
{
    private readonly ref readonly ClassifierInfo _info;
    private RegisterQueue _intRegs;
    private RegisterQueue _floatRegs;
    private uint _stackArgSize;

    public Arm64Classifier(in ClassifierInfo info)
    {
        _info = ref info;
        _intRegs = new RegisterQueue(IntArgRegs);
        _floatRegs = new RegisterQueue(FltArgRegs);
    }

    public readonly int StackSize => unchecked((int)roundUp(_stackArgSize, unchecked((uint)Configuration.TARGET_POINTER_SIZE)));

    /// <summary>Classify a parameter for the ARM64 ABI.</summary>
    public unsafe AbiPassingInformation Classify(Compiler comp, var_types type, ClassLayout? structLayout, WellKnownArg wellKnownParam)
    {
        var pointerSize = unchecked((uint)Configuration.TARGET_POINTER_SIZE);

        if (wellKnownParam is WellKnownArg.SecretStubParam)
        {
            return AbiPassingInformation.FromSegment(comp, false,
                AbiPassingSegment.InRegister(REG_SECRET_STUB_PARAM_ARM64, 0, Configuration.TARGET_POINTER_SIZE));
        }

        assert(!varTypeIsMask(type));

        if ((wellKnownParam is WellKnownArg.RetBuffer) && hasFixedRetBuffReg(_info.CallConv))
        {
            return AbiPassingInformation.FromSegment(comp, false,
                AbiPassingSegment.InRegister(REG_ARG_RET_BUFF, 0, Configuration.TARGET_POINTER_SIZE));
        }

        // HFA/HVAs can use more registers than other structures.
        if (varTypeIsStruct(type) && !_info.IsVarArgs)
        {
            var layout = structLayout;
            if (layout is null)
            {
                JitExceptionHelpers.ThrowArgumentNullException(FatalJitArgument.StructLayout);
            }

            var hfaType = comp.GetHfaType(layout.ClassHandle);

            if (hfaType is not TYP_UNDEF)
            {
                var elemSize = (uint)hfaType.Size;
                var slots = layout.Size / elemSize;
                AbiPassingInformation info;

                if (_floatRegs.Count >= slots)
                {
                    info = new AbiPassingInformation(unchecked((int)slots));

                    for (uint i = 0; i < slots; i++)
                    {
                        info.Segments[(int)i] = AbiPassingSegment.InRegister(
                            _floatRegs.Dequeue(), unchecked((int)(i * elemSize)), unchecked((int)elemSize));
                    }
                }
                else
                {
                    var alignment = compAppleArm64Abi() ? Math.Min(elemSize, pointerSize) : pointerSize;
                    _stackArgSize = roundUp(_stackArgSize, alignment);

                    var segment = alignment < pointerSize
                        ? AbiPassingSegment.OnStackWithoutConsumingFullSlot(
                            unchecked((int)_stackArgSize), 0, unchecked((int)layout.Size))
                        : AbiPassingSegment.OnStack(unchecked((int)_stackArgSize), 0, unchecked((int)layout.Size));

                    info = AbiPassingInformation.FromSegment(comp, false, segment);
                    _stackArgSize = unchecked(_stackArgSize + roundUp(layout.Size, alignment));

                    // After passing a float value on the stack, later floats cannot be enregistered.
                    _floatRegs.Clear();
                }

                return info;
            }
        }

        uint slotsNeeded;
        uint passedSize;
        uint structSize = 0;
        var passedByRef = false;

        if (varTypeIsStruct(type))
        {
            var layout = structLayout;
            if (layout is null)
            {
                JitExceptionHelpers.ThrowArgumentNullException(FatalJitArgument.StructLayout);
            }
            structSize = layout.Size;

            // TODO-SVE: We should be able to pass in a Z register.
            if ((structSize > 16) || (type is TYP_SIMD_ARM64))
            {
                passedByRef = true;
                slotsNeeded = 1;
                passedSize = pointerSize;
            }
            else
            {
                slotsNeeded = (structSize + pointerSize - 1) / pointerSize;
                passedSize = structSize;
            }
        }
        else
        {
            assert(type.Size <= Configuration.TARGET_POINTER_SIZE);
            slotsNeeded = 1;
            passedSize = (uint)type.Size;
        }

        assert((slotsNeeded == 1) || (slotsNeeded == 2));

        AbiPassingInformation result;

        if (_info.IsVarArgs && (slotsNeeded == 2) && (_intRegs.Count == 1))
        {
            // Only varargs permits splitting a struct between the last register and the stack.
            assert(compFeatureArgSplit());
            result = AbiPassingInformation.FromSegments(comp,
                AbiPassingSegment.InRegister(_intRegs.Dequeue(), 0, Configuration.TARGET_POINTER_SIZE),
                AbiPassingSegment.OnStack(unchecked((int)_stackArgSize), Configuration.TARGET_POINTER_SIZE,
                    unchecked((int)(structSize - Configuration.TARGET_POINTER_SIZE))));

            _stackArgSize = unchecked(_stackArgSize + pointerSize);
        }
        else
        {
            ref var regs = ref _intRegs;

            // Varargs is Windows-only and passes all parameters in integer registers.
            if (varTypeUsesFloatArgReg(type) && !_info.IsVarArgs && !passedByRef)
            {
                regs = ref _floatRegs;
            }

            if (regs.Count >= slotsNeeded)
            {
                var slotSize = Math.Min(passedSize, pointerSize);
                var firstSegment = AbiPassingSegment.InRegister(regs.Dequeue(), 0, unchecked((int)slotSize));

                if (slotsNeeded == 1)
                {
                    result = AbiPassingInformation.FromSegment(comp, passedByRef, firstSegment);
                }
                else
                {
                    result = new AbiPassingInformation(unchecked((int)slotsNeeded));
                    result.Segments[0] = firstSegment;
                    assert(varTypeIsStruct(type));

                    var tailSize = structSize - slotSize;
                    result.Segments[1] = AbiPassingSegment.InRegister(
                        regs.Dequeue(), unchecked((int)slotSize), unchecked((int)tailSize));
                }
            }
            else
            {
                AbiPassingSegment segment;
                uint alignment;

                if (compAppleArm64Abi())
                {
                    alignment = varTypeIsStruct(type) ? pointerSize : (uint)type.Size;
                    _stackArgSize = roundUp(_stackArgSize, alignment);
                    segment = alignment < pointerSize
                        ? AbiPassingSegment.OnStackWithoutConsumingFullSlot(
                            unchecked((int)_stackArgSize), 0, unchecked((int)passedSize))
                        : AbiPassingSegment.OnStack(unchecked((int)_stackArgSize), 0, unchecked((int)passedSize));
                }
                else
                {
                    alignment = pointerSize;
                    assert((_stackArgSize % pointerSize) == 0);
                    segment = AbiPassingSegment.OnStack(unchecked((int)_stackArgSize), 0, unchecked((int)passedSize));
                }

                result = AbiPassingInformation.FromSegment(comp, passedByRef, segment);
                _stackArgSize = unchecked(_stackArgSize + roundUp(passedSize, alignment));

                // Once this bank spills an argument, later arguments cannot return to its registers.
                regs.Clear();
            }
        }

        assert(result.IsPassedByReference == passedByRef);

        return result;
    }
}
