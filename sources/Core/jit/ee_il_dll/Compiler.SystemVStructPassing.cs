// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public unsafe void eeGetSystemVAmd64PassStructInRegisterDescriptor(
        CORINFO_CLASS_HANDLE structHandle, SYSTEMV_AMD64_CORINFO_STRUCT_REG_PASSING_DESCRIPTOR* descriptor)
    {
        var key = (nint)structHandle;
        var cache = _systemVStructPassingDescriptorCache;
        if ((cache is not null) && cache.TryGetValue(key, out var cachedDescriptor))
        {
            descriptor->CopyFrom(cachedDescriptor);
        }
        else
        {
            var success = info.compCompHnd->getSystemVAmd64PassStructInRegisterDescriptor(structHandle, descriptor);
            noway_assert(success);

            if (success)
            {
                cache ??= [];
                cache.Add(key, *descriptor);
                _systemVStructPassingDescriptorCache = cache;
            }
        }

        if (Configuration.DEBUG && verbose)
        {
            JITDUMP($"**** getSystemVAmd64PassStructInRegisterDescriptor({FMT_DSP_PTR(structHandle)} ({eeGetClassName(structHandle)}), ...) =>\n");
            JITDUMP($"        passedInRegisters = {dspBool(descriptor->passedInRegisters)}\n");
            if (descriptor->passedInRegisters)
            {
                JITDUMP($"        eightByteCount   = {descriptor->eightByteCount}\n");
                for (var i = 0; i < descriptor->eightByteCount; i++)
                {
                    var classification = descriptor->eightByteClassifications[i] switch
                    {
                        SystemVClassificationTypeUnknown => "UNKNOWN",
                        SystemVClassificationTypeStruct => "Struct",
                        SystemVClassificationTypeNoClass => "NoClass",
                        SystemVClassificationTypeMemory => "Memory",
                        SystemVClassificationTypeInteger => "Integer",
                        SystemVClassificationTypeIntegerReference => "IntegerReference",
                        SystemVClassificationTypeIntegerByRef => "IntegerByReference",
                        SystemVClassificationTypeSSE => "SSE",
                        _ => "ILLEGAL",
                    };
                    JITDUMP($"        eightByte #{i} -- classification: {classification}, byteSize: {descriptor->eightByteSizes[i]}, byteOffset: {descriptor->eightByteOffsets[i]}\n");
                }
            }
        }
    }

    public unsafe void eeGetSystemVAmd64PassStructInRegisterDescriptor(
        CORINFO_CLASS_HANDLE structHandle, out SYSTEMV_AMD64_CORINFO_STRUCT_REG_PASSING_DESCRIPTOR descriptor)
    {
        descriptor = default;
        fixed (SYSTEMV_AMD64_CORINFO_STRUCT_REG_PASSING_DESCRIPTOR* result = &descriptor)
        {
            eeGetSystemVAmd64PassStructInRegisterDescriptor(structHandle, result);
        }
    }
}
