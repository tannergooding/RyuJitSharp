// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86
namespace RyuJitSharp;

public ref struct X86Classifier
{
    private readonly ref readonly ClassifierInfo _info;
    private RegisterQueue _regs;
    private uint _stackArgSize;

    public X86Classifier(in ClassifierInfo info)
    {
        _info = ref info;
        _regs = new RegisterQueue([]);

        switch (info.CallConv)
        {
            case CorInfoCallConvExtension.Thiscall:
            {
                _regs = new RegisterQueue([REG_ECX]);
                break;
            }

            case CorInfoCallConvExtension.C:
            case CorInfoCallConvExtension.Stdcall:
            case CorInfoCallConvExtension.CMemberFunction:
            case CorInfoCallConvExtension.StdcallMemberFunction:
            {
                break;
            }

            default:
            {
                var numRegs = IntArgRegs.Length;

                if (info.IsVarArgs)
                {
                    // In varargs methods only the this pointer or retbuff is enregistered.
                    numRegs = info.HasThis || info.HasRetBuff ? 1 : 0;
                }

                _regs = new RegisterQueue(IntArgRegs[..numRegs]);
                break;
            }
        }
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

        uint size;

        if (type is TYP_STRUCT)
        {
            assert(structLayout is not null);
            size = structLayout.Size;
        }
        else
        {
            size = (uint)type.Size;
        }
        var numSlots = (size + TARGET_POINTER_SIZE - 1) / TARGET_POINTER_SIZE;
        var canEnreg = false;

        if ((_regs.Count >= numSlots) && (wellKnownParam is not WellKnownArg.X86TailCallSpecialArg))
        {
            switch (type)
            {
                case TYP_BYTE:
                case TYP_UBYTE:
                case TYP_SHORT:
                case TYP_USHORT:
                case TYP_INT:
                case TYP_REF:
                case TYP_BYREF:
                {
                    canEnreg = true;
                    break;
                }

                case TYP_STRUCT:
                {
                    assert(structLayout is not null);
                    canEnreg = comp.isTrivialPointerSizedStruct(structLayout.ClassHandle);
                    break;
                }

                default:
                {
                    break;
                }
            }
        }

        AbiPassingSegment segment;

        if (canEnreg)
        {
            assert(numSlots == 1);
            segment = AbiPassingSegment.InRegister(_regs.Dequeue(), 0, unchecked((int)size));
        }
        else
        {
            assert((_stackArgSize % TARGET_POINTER_SIZE) == 0);
            uint offset;
            var roundedArgSize = roundUp(size, TARGET_POINTER_SIZE);

            if (_info.CallConv is CorInfoCallConvExtension.Managed)
            {
                // Managed x86 offsets subtract from the top of the left-to-right argument area.
                _stackArgSize = unchecked(_stackArgSize + roundedArgSize);
                offset = _stackArgSize;
            }
            else
            {
                offset = _stackArgSize;
                _stackArgSize = unchecked(_stackArgSize + roundedArgSize);
            }

            segment = AbiPassingSegment.OnStack(unchecked((int)offset), 0, unchecked((int)size));
        }

        return AbiPassingInformation.FromSegmentByValue(comp, segment);
    }
}
#endif
