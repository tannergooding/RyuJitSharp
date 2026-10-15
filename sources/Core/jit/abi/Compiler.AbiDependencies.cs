// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Compiler
{
    public unsafe bool isTrivialPointerSizedStruct(CORINFO_CLASS_HANDLE clsHnd)
    {
        if (!Configuration.TARGET_X86)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "Trivial pointer-sized struct classification requires x86.");
        }

        assert(info.compCompHnd->isValueClass(clsHnd));
        if (info.compCompHnd->getClassSize(clsHnd) != Configuration.TARGET_POINTER_SIZE)
        {
            return false;
        }

        for (;;)
        {
            // Every nested value class in the chain must also have exactly one field.
            if (!info.compCompHnd->isValueClass(clsHnd) || info.compCompHnd->getClassNumInstanceFields(clsHnd) is not 1)
            {
                return false;
            }

            var pClsHnd = &clsHnd;
            var fieldHandle = info.compCompHnd->getFieldInClass(clsHnd, 0);
            var fieldType = info.compCompHnd->getFieldType(fieldHandle, pClsHnd);
            var fieldVarType = fieldType.VarType;

            if (fieldType == CORINFO_TYPE_VALUECLASS)
            {
                clsHnd = *pClsHnd;
            }
            else if (varTypeIsI(fieldVarType) && !varTypeIsGC(fieldVarType))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    /// <remarks>Boxed records keep returned references stable when the cache grows.</remarks>
    public unsafe ref CORINFO_FPSTRUCT_LOWERING GetFpStructLowering(CORINFO_CLASS_HANDLE structHandle)
    {
        if (!Configuration.TARGET_RISCV64 && !Configuration.TARGET_LOONGARCH64)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "Floating-point struct lowering is unsupported for this target.");
        }

        var cache = _fpStructLoweringCache ??= [];
        var key = new Pointer<CORINFO_CLASS_STRUCT_>(structHandle);

        if (!cache.TryGetValue(key, out var lowering))
        {
            lowering = new StrongBox<CORINFO_FPSTRUCT_LOWERING>();

            fixed (CORINFO_FPSTRUCT_LOWERING* loweringPointer = &lowering.Value)
            {
                info.compCompHnd->getFpStructLowering(structHandle, loweringPointer);
            }

            cache[key] = lowering;

            if (Configuration.DEBUG && verbose)
            {
                jitprintf($"**** getFpStructInRegistersInfo({FMT_PTR((void*)dspPtr(structHandle))} " +
                    $"({eeGetClassName(structHandle)}, {info.compCompHnd->getClassSize(structHandle)} bytes)) =>\n");

                if (lowering.Value.byIntegerCallConv)
                {
                    jitprintf("        pass by integer calling convention\n");
                }
                else
                {
                    jitprintf($"        may be passed by floating-point calling convention " +
                        $"({lowering.Value.numLoweredElements} fields):\n");

                    for (nint i = 0; i < lowering.Value.numLoweredElements; i++)
                    {
                        var fieldType = lowering.Value.loweredElements[(int)i].VarType;
                        jitprintf($"         * field[{i}]: type {fieldType.Name} at offset {lowering.Value.offsets[(int)i]}\n");
                    }
                }
            }
        }

        return ref lowering.Value;
    }
}
