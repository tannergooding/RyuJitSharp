// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if SWIFT_SUPPORT
using System;
using System.Collections.Generic;

namespace RyuJitSharp;

public ref struct SwiftABIClassifier
{
    private PlatformClassifier _classifier;

    public SwiftABIClassifier(in ClassifierInfo info)
    {
        _classifier = new PlatformClassifier(info);
    }

    public readonly int StackSize => _classifier.StackSize;

    public unsafe AbiPassingInformation Classify(
        Compiler comp, var_types type, ClassLayout? structLayout, WellKnownArg wellKnownParam)
    {
        if (wellKnownParam is WellKnownArg.RetBuffer)
        {
            var reg = theFixedRetBuffReg(CorInfoCallConvExtension.Swift);
            return AbiPassingInformation.FromSegmentByValue(comp,
                AbiPassingSegment.InRegister(reg, 0, TARGET_POINTER_SIZE));
        }

        if (wellKnownParam is WellKnownArg.SwiftSelf)
        {
            return AbiPassingInformation.FromSegmentByValue(comp,
                AbiPassingSegment.InRegister(REG_SWIFT_SELF, 0, TARGET_POINTER_SIZE));
        }

        if (wellKnownParam is WellKnownArg.SwiftError)
        {
            // This pointer parameter is unused. Its nominal error register is handled by
            // the prolog without consuming the ordinary argument-register or stack queues.
            return AbiPassingInformation.FromSegmentByValue(comp,
                AbiPassingSegment.InRegister(REG_SWIFT_ERROR, 0, TARGET_POINTER_SIZE));
        }

        if (type is TYP_STRUCT)
        {
            assert(structLayout is not null);
            ref readonly var lowering = ref comp.GetSwiftLowering(structLayout.ClassHandle);

            if (lowering.byReference)
            {
                var abiInfo = _classifier.Classify(comp, TYP_I_IMPL, null, WellKnownArg.None);
                assert(abiInfo.NumSegments == 1);

                return AbiPassingInformation.FromSegment(comp, true, abiInfo.Segments[0]);
            }

            List<AbiPassingSegment> segments = [];

            for (var i = 0; i < lowering.numLoweredElements; i++)
            {
                var elemType = lowering.loweredElements[i].VarType;
                var elemInfo = _classifier.Classify(comp, elemType, null, WellKnownArg.None);

                foreach (ref readonly var segment in elemInfo.Segments)
                {
                    var newSegment = segment;
                    newSegment.Offset = unchecked(newSegment.Offset + lowering.offsets[i]);
                    // A lowered tail primitive can be wider than the actual remaining struct bytes.
                    newSegment.Size = unchecked((int)Math.Min(
                        (uint)newSegment.Size, structLayout.Size - (uint)newSegment.Offset));
                    segments.Add(newSegment);
                }
            }

            var result = new AbiPassingInformation(segments.Count);

            for (var i = 0; i < segments.Count; i++)
            {
                result.Segments[i] = segments[i];
            }

            return result;
        }

        return _classifier.Classify(comp, type, structLayout, wellKnownParam);
    }
}
#endif
