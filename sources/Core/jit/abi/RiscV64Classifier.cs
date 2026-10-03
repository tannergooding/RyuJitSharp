// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public ref struct RiscV64Classifier
{
    private readonly ref readonly ClassifierInfo _info;
    private RegisterQueue _intRegs;
    private RegisterQueue _floatRegs;
    private uint _stackArgSize;

    public RiscV64Classifier(in ClassifierInfo info)
    {
        _info = ref info;
        _intRegs = new RegisterQueue(IntArgRegs);
        _floatRegs = new RegisterQueue(FltArgRegs);
        assert(!_info.IsVarArgs);
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

        ref readonly var lowering = ref Unsafe.NullRef<CORINFO_FPSTRUCT_LOWERING>();
        uint intFields = 0;
        uint floatFields = 0;
        uint passedSize;
        var passedByRef = false;

        if (varTypeIsStruct(type))
        {
            assert(structLayout is not null);
            passedSize = structLayout.Size;

            if (passedSize > MAX_PASS_MULTIREG_BYTES)
            {
                passedByRef = true;
                passedSize = TARGET_POINTER_SIZE;
            }
            else if (!structLayout.IsBlockLayout)
            {
                lowering = ref comp.GetFpStructLowering(structLayout.ClassHandle);

                if (!lowering.byIntegerCallConv)
                {
                    assert(lowering.numLoweredElements is 1 or 2);
#if DEBUG
                    uint debugIntFields = 0;
#endif
                    for (nint i = 0; i < lowering.numLoweredElements; i++)
                    {
                        var fieldType = lowering.loweredElements[(int)i].VarType;
                        floatFields += varTypeIsFloating(fieldType) ? 1u : 0u;
#if DEBUG
                        debugIntFields += varTypeIsIntegralOrI(fieldType) ? 1u : 0u;
#endif
                    }

                    intFields = (uint)lowering.numLoweredElements - floatFields;
#if DEBUG
                    assert(debugIntFields == intFields);
#endif
                }
            }
        }
        else
        {
            passedSize = (uint)type.Size;
            assert(passedSize <= TARGET_POINTER_SIZE);
            floatFields = varTypeIsFloating(type) ? 1u : 0u;
        }

        assert((floatFields > 0) || (intFields == 0));

        if ((floatFields > 0) && (_floatRegs.Count >= floatFields) && (_intRegs.Count >= intFields))
        {
            if ((floatFields == 1) && (intFields == 0))
            {
                var offset = 0;

                if (!Unsafe.IsNullRef(in lowering))
                {
                    assert(lowering.numLoweredElements == 1);
                    type = lowering.loweredElements[0].VarType;
                    passedSize = (uint)type.Size;
                    offset = lowering.offsets[0];
                }

                assert(varTypeIsFloating(type));
                var segment = AbiPassingSegment.InRegister(
                    _floatRegs.Dequeue(), unchecked((int)offset), unchecked((int)passedSize));

                return AbiPassingInformation.FromSegmentByValue(comp, segment);
            }
            else
            {
                assert(varTypeIsStruct(type));
                assert((floatFields + intFields) == 2);
                assert(!Unsafe.IsNullRef(in lowering));
                assert(!lowering.byIntegerCallConv);
                assert(lowering.numLoweredElements == 2);

                var type0 = lowering.loweredElements[0].VarType;
                var type1 = lowering.loweredElements[1].VarType;
                assert(varTypeIsFloating(type0) || varTypeIsFloating(type1));
                ref var queue0 = ref (varTypeIsFloating(type0) ? ref _floatRegs : ref _intRegs);
                ref var queue1 = ref (varTypeIsFloating(type1) ? ref _floatRegs : ref _intRegs);
                var segment0 = AbiPassingSegment.InRegister(queue0.Dequeue(), lowering.offsets[0], type0.Size);
                var segment1 = AbiPassingSegment.InRegister(queue1.Dequeue(), lowering.offsets[1], type1.Size);

                return AbiPassingInformation.FromSegments(comp, segment0, segment1);
            }
        }
        else
        {
            if (_intRegs.Count > 0)
            {
                if (passedSize <= TARGET_POINTER_SIZE)
                {
                    var segment = AbiPassingSegment.InRegister(_intRegs.Dequeue(), 0, unchecked((int)passedSize));
                    return AbiPassingInformation.FromSegment(comp, passedByRef, segment);
                }
                else
                {
                    assert(varTypeIsStruct(type));
                    var tailSize = passedSize - TARGET_POINTER_SIZE;
                    var head = AbiPassingSegment.InRegister(_intRegs.Dequeue(), 0, TARGET_POINTER_SIZE);
                    var tail = _intRegs.Count > 0
                        ? AbiPassingSegment.InRegister(_intRegs.Dequeue(), TARGET_POINTER_SIZE, unchecked((int)tailSize))
                        : PassOnStack(TARGET_POINTER_SIZE, tailSize);

                    return AbiPassingInformation.FromSegments(comp, head, tail);
                }
            }
            else
            {
                return AbiPassingInformation.FromSegment(comp, passedByRef, PassOnStack(0, passedSize));
            }
        }
    }

    private AbiPassingSegment PassOnStack(int offset, uint size)
    {
        assert(size > 0);
        assert(size <= 2 * TARGET_POINTER_SIZE);
        assert((_stackArgSize % TARGET_POINTER_SIZE) == 0);
        var segment = AbiPassingSegment.OnStack(unchecked((int)_stackArgSize), offset, unchecked((int)size));
        _stackArgSize = unchecked(_stackArgSize + (uint)(size > TARGET_POINTER_SIZE ? 2 * TARGET_POINTER_SIZE : TARGET_POINTER_SIZE));

        return segment;
    }
}
#endif
