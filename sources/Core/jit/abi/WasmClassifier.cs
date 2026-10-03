// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_WASM
using System;

namespace RyuJitSharp;

public ref struct WasmClassifier
{
    private uint _localIndex;

    public WasmClassifier(in ClassifierInfo info)
    {
        _localIndex = 0;
    }

    public readonly int StackSize => 0;

    public unsafe AbiPassingInformation Classify(
        Compiler comp, var_types type, ClassLayout? structLayout, WellKnownArg wellKnownParam)
    {
        if (varTypeIsStruct(type))
        {
            var layout = structLayout ?? throw new ArgumentNullException(nameof(structLayout));
            var classHandle = layout.ClassHandle;
            assert(classHandle != NO_CLASS_HANDLE);
            var wasmAbiType = comp.info.compCompHnd->getWasmLowering(classHandle);
            var passByRef = false;
            var abiType = TYP_UNDEF;

            if (wasmAbiType is CORINFO_WASM_TYPE_VOID)
            {
                abiType = TYP_I_IMPL;
                passByRef = true;
            }
            else
            {
                abiType = ToJitType(wasmAbiType);

                // A struct wider than its lowered wasm value is passed by value across several
                // of them. Preserve the actual size of a narrower struct in its single segment.
                var segmentSize = (uint)abiType.Size;
                if (layout.Size > segmentSize)
                {
                    var segmentCount = layout.Size / segmentSize;
                    assert(segmentCount * segmentSize == layout.Size);

                    var info = new AbiPassingInformation(unchecked((int)segmentCount));
                    for (var i = 0; i < segmentCount; i++)
                    {
                        var register = regNumberExtensions.MakeWasmReg(_localIndex++, abiType);
                        info.Segments[unchecked((int)i)] = AbiPassingSegment.InRegister(
                            register, unchecked((int)(i * segmentSize)), unchecked((int)segmentSize));
                    }

                    return info;
                }
            }

            var regType = abiType.ActualType;
            var reg = regNumberExtensions.MakeWasmReg(_localIndex++, regType);
            var segmentSizeForStruct = passByRef
                ? (uint)abiType.Size
                : uint.Min(layout.Size, (uint)abiType.Size);
            var segment = AbiPassingSegment.InRegister(
                reg, 0, unchecked((int)segmentSizeForStruct));
            return AbiPassingInformation.FromSegment(comp, passByRef, segment);
        }

        var scalarReg = regNumberExtensions.MakeWasmReg(_localIndex++, type.ActualType);
        var scalarSegment = AbiPassingSegment.InRegister(scalarReg, 0, type.Size);
        return AbiPassingInformation.FromSegment(comp, false, scalarSegment);
    }

    public static var_types ToJitType(CorInfoWasmType wasmType)
    {
        return wasmType switch {
            CORINFO_WASM_TYPE_I32 => TYP_INT,
            CORINFO_WASM_TYPE_I64 => TYP_LONG,
            CORINFO_WASM_TYPE_F32 => TYP_FLOAT,
            CORINFO_WASM_TYPE_F64 => TYP_DOUBLE,
            CORINFO_WASM_TYPE_V128 => TYP_SIMD16,
            _ => InvalidWasmType(),
        };
    }

    private static var_types InvalidWasmType()
    {
        unreached();
        return TYP_UNDEF;
    }
}
#endif
