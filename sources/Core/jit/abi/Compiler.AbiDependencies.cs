// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Compiler
{
#if TARGET_X86
    public unsafe bool isTrivialPointerSizedStruct(CORINFO_CLASS_HANDLE clsHnd)
    {
        NYI("Compiler::isTrivialPointerSizedStruct (compiler.cpp), x86 ABI nested value-class classification");
        throw new FatalJitException(CORJIT_IMPLLIMITATION, "x86 trivial pointer-sized struct classification is not ported.");
    }
#endif

#if TARGET_RISCV64 || TARGET_LOONGARCH64
    /// <remarks>Boxed records keep returned references stable when the cache grows.</remarks>
    public unsafe ref CORINFO_FPSTRUCT_LOWERING GetFpStructLowering(CORINFO_CLASS_HANDLE structHandle)
    {
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

#if DEBUG
            if (verbose)
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
#endif
        }

        return ref lowering.Value;
    }
#endif
}
